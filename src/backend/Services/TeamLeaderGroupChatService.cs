using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using LifeAdviceGuru.Agents;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// One participant in the Team Leader group chat. <c>Id</c> is the agent's
/// page route id, <c>AgentRole</c> the preferred <see cref="GuruAgentFactory"/> key.
/// </summary>
public record GroupChatParticipant(string Id, string Persona, string Role, string AgentRole);

/// <summary>
/// One streamed turn event written to the group-chat session channel and
/// surfaced over HTTP. <c>Kind</c> is one of: <c>info</c> (orchestration
/// status), <c>mode</c> (the session switched to offline; <c>Text</c> is the
/// new mode), <c>ask</c> (Theo's question to a specialist), <c>reply</c>
/// (specialist's answer), <c>decide</c> (a human decision Theo records),
/// <c>conclude</c> (Theo's plan), <c>error</c>, <c>done</c>.
/// </summary>
public record GroupChatTurn(
    string Kind,
    string SpeakerId,
    string SpeakerPersona,
    string SpeakerRole,
    string Text,
    DateTimeOffset Timestamp);

/// <summary>
/// Cumulative state of a single group-chat session; held in memory so the
/// SSE subscriber receives every turn even if it connects late.
/// <c>Mode</c> is <c>live</c> (Foundry agents) or <c>offline</c> (the
/// deterministic replay used when Foundry isn't configured or can't be
/// reached).
/// </summary>
public class GroupChatSession
{
    public string Id { get; }
    public string CaseNumber { get; }
    /// <summary><c>live</c> or <c>offline</c>; switches to <c>offline</c> if the live agents can't be reached.</summary>
    public string Mode { get; set; }
    public List<GroupChatTurn> Turns { get; } = new();
    public Channel<GroupChatTurn> Events { get; } =
        Channel.CreateUnbounded<GroupChatTurn>(new UnboundedChannelOptions
        {
            // Exactly one orchestration task writes to this channel and the
            // matching SSE handler is the only reader for the session.
            SingleReader = true,
            SingleWriter = true
        });
    public bool Completed { get; set; }

    public GroupChatSession(string id, string caseNumber, string mode)
    {
        Id = id;
        CaseNumber = caseNumber;
        Mode = mode;
    }
}

/// <summary>
/// Orchestrates a multi-agent group chat about a single Client Case.
///
/// Modelled on the <c>GroupChatManager</c> / <c>GroupChatHost</c> pattern in
/// <c>Microsoft.Agents.AI.Workflows</c> (see
/// <c>AgentWorkflowBuilder.CreateGroupChatBuilderWith</c>): the Team Leader
/// Agent (Theo) acts as the group-chat manager (<c>SelectNextAgentAsync</c> +
/// <c>ShouldTerminateAsync</c>), and the seven other Life Advice Guru
/// agents — Olivia (Onboarding), Penny (Paraplanning), Ivy (Insurance
/// Research), Felix (Financial Crime), Uma (Underwriting), Ben (Claims) and
/// Cara (Client Communications) — take part as specialists.
///
/// The transcript is kept as <see cref="ChatMessage"/> instances — the type
/// the framework's <c>GroupChatManager</c> consumes — so the orchestration
/// could later move onto the framework's workflow runtime once the Foundry
/// agents are wrapped as <c>AIAgent</c> instances.
///
/// When Foundry isn't configured (or the live agents fail before the
/// discussion starts) the service replays a scripted case review built from
/// the canonical case facts. Each scenario case is reviewed at the point it
/// reaches Theo's desk — the same point the Team Leader dashboard shows — and
/// the chat ends with the human decision.
/// </summary>
public class TeamLeaderGroupChatService
{
    // Hard cap on the number of group-chat turns. Each iteration is framed
    // to Theo (the group-chat manager) as one "turn": he either picks a
    // specialist to speak or concludes. The cap guarantees the chat ends
    // even if Theo never elects to conclude on his own.
    private const int MaxIterations = 20;

    // Pacing for the scripted replay so the transcript reads like a live chat.
    private static readonly TimeSpan AskDelay = TimeSpan.FromMilliseconds(550);
    private static readonly TimeSpan ReplyDelay = TimeSpan.FromMilliseconds(1200);

    private const string LeaderId = "team-leader";
    private const string LeaderPersona = "Theo";
    private const string LeaderRole = "Practice Team Leader";

    private readonly CaseStore _store;
    private readonly GuruAgentFactory _agentFactory;
    private readonly ILogger<TeamLeaderGroupChatService> _logger;

    private readonly ConcurrentDictionary<string, GroupChatSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Specialist participants in the group chat — every practice role
    /// except the Team Leader, who chairs as the manager.
    /// </summary>
    public static readonly IReadOnlyList<GroupChatParticipant> Participants = new[]
    {
        new GroupChatParticipant("onboarding",            "Olivia", "Onboarding & Requests Officer",    "onboarding"),
        new GroupChatParticipant("paraplanning",          "Penny",  "Paraplanner",                      "paraplanning"),
        new GroupChatParticipant("insurance-research",    "Ivy",    "Insurance Research Analyst",       "insurance-research"),
        new GroupChatParticipant("financial-crime",       "Felix",  "Financial Crime Investigator",     "fincrime"),
        new GroupChatParticipant("underwriting",          "Uma",    "Underwriting Coordinator",         "underwriting"),
        new GroupChatParticipant("claims",                "Ben",    "Benefit Claims Advocate",          "claims"),
        new GroupChatParticipant("client-communications", "Cara",   "Client Communications Specialist", "communications"),
    };

    // Factory keys to try for each participant, in order — the preferred key
    // first, then earlier aliases the agent factory may still use.
    private static readonly Dictionary<string, string[]> AgentRoleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["paraplanning"] = ["paraplanning", "benefits"],
        ["insurance-research"] = ["insurance-research", "research", "investments"],
        ["underwriting"] = ["underwriting", "transfers"],
        ["claims"] = ["claims", "payments"],
    };

    public TeamLeaderGroupChatService(
        CaseStore store,
        GuruAgentFactory agentFactory,
        ILogger<TeamLeaderGroupChatService> logger)
    {
        _store = store;
        _agentFactory = agentFactory;
        _logger = logger;
    }

    public bool IsConfigured => _agentFactory.IsConfigured;

    public CaseRecord? GetCase(string caseNumber) => _store.Get(caseNumber);

    public GroupChatSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    /// <summary>
    /// Start a group-chat session for the given case. Returns immediately;
    /// orchestration runs on a background task and pushes
    /// <see cref="GroupChatTurn"/> events to the session channel — live
    /// Foundry agents when configured, otherwise the offline replay.
    /// </summary>
    public GroupChatSession StartSession(CaseRecord clientCase)
    {
        var mode = IsConfigured ? "live" : "offline";
        var session = new GroupChatSession(Guid.NewGuid().ToString("N"), clientCase.CaseNumber, mode);
        _sessions[session.Id] = session;
        _ = Task.Run(() => mode == "live"
            ? RunLiveAsync(session, clientCase)
            : RunScriptedAsync(session, clientCase));
        return session;
    }

    // ── Live orchestration (Foundry agents) ───────────────────────────────────
    private async Task RunLiveAsync(GroupChatSession session, CaseRecord clientCase)
    {
        try
        {
            await EmitLeaderAsync(session, "info",
                $"Opening the group chat for case {clientCase.CaseNumber} — {clientCase.ClientName}, {Lower(clientCase.RequestType)}. Inviting the team…");

            // Conversation history kept in MAF's ChatMessage shape. Each
            // message's AuthorName carries the speaker persona so Theo can
            // refer back to who said what.
            var history = new List<ChatMessage>
            {
                new(ChatRole.System, BuildBrief(clientCase)) { AuthorName = "CaseFile" }
            };

            var leader = _agentFactory.Create("team-leader");
            var specialistsById = Participants.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                // Manager step: ask Theo who should speak next, or conclude.
                var managerPrompt = BuildManagerPrompt(clientCase, history, iteration, MaxIterations);
                var managerResult = await leader.RunWithTraceAsync(managerPrompt);
                var decision = ParseManagerDecision(managerResult.Text);

                if (decision is null)
                {
                    _logger.LogWarning("Team Leader returned an unparseable decision; concluding. Raw: {Raw}",
                        Truncate(managerResult.Text, 400));
                    await EmitLeaderAsync(session, "conclude", managerResult.Text.Trim());
                    history.Add(new ChatMessage(ChatRole.Assistant, managerResult.Text) { AuthorName = LeaderPersona });
                    break;
                }

                if (string.Equals(decision.Action, "conclude", StringComparison.OrdinalIgnoreCase))
                {
                    var summary = string.IsNullOrWhiteSpace(decision.Summary)
                        ? "The team has covered what this case needs. Closing the discussion."
                        : decision.Summary!;
                    await EmitLeaderAsync(session, "conclude", summary);
                    history.Add(new ChatMessage(ChatRole.Assistant, summary) { AuthorName = LeaderPersona });
                    break;
                }

                if (!specialistsById.TryGetValue(decision.Speaker ?? "", out var participant))
                {
                    await EmitLeaderAsync(session, "conclude",
                        $"(Theo picked an unknown speaker '{decision.Speaker}'. Closing the discussion.)");
                    break;
                }

                var question = string.IsNullOrWhiteSpace(decision.Prompt)
                    ? $"{participant.Persona}, what's your view on this case?"
                    : decision.Prompt!;

                await EmitLeaderAsync(session, "ask", $"@{participant.Persona} ({participant.Role}) — {question}");
                history.Add(new ChatMessage(ChatRole.Assistant, $"To {participant.Persona}: {question}")
                {
                    AuthorName = LeaderPersona
                });

                // Specialist step.
                GuruAgent specialist;
                try
                {
                    specialist = CreateSpecialist(participant);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create specialist {Role}", participant.AgentRole);
                    await EmitAsync(session, "error", participant.Id, participant.Persona, participant.Role,
                        $"Couldn't bring {participant.Persona} into the chat: {ex.Message}");
                    continue;
                }

                var specialistPrompt = BuildSpecialistPrompt(clientCase, participant, question, history);
                AgentTraceResult specialistResult;
                try
                {
                    specialistResult = await specialist.RunWithTraceAsync(specialistPrompt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Specialist {Role} failed", participant.AgentRole);
                    await EmitAsync(session, "error", participant.Id, participant.Persona, participant.Role,
                        $"{participant.Persona} couldn't respond: {ex.Message}");
                    continue;
                }

                var reply = string.IsNullOrWhiteSpace(specialistResult.Text)
                    ? "(no response)"
                    : specialistResult.Text.Trim();
                await EmitAsync(session, "reply", participant.Id, participant.Persona, participant.Role, reply);
                history.Add(new ChatMessage(ChatRole.Assistant, reply) { AuthorName = participant.Persona });
            }

            if (!session.Turns.Any(t => t.Kind == "conclude"))
            {
                await EmitLeaderAsync(session, "conclude",
                    $"Reached the {MaxIterations}-turn limit. Closing the discussion and recording the transcript on the case.");
            }
        }
        catch (Exception ex) when (!session.Turns.Any(t => t.Kind is "ask" or "reply"))
        {
            // Nothing has been discussed yet — replay the case review offline
            // so the page still completes.
            _logger.LogWarning(ex, "Live group chat unavailable for case {CaseNumber}; replaying the case review offline",
                clientCase.CaseNumber);
            session.Mode = "offline";
            await EmitLeaderAsync(session, "mode", "offline");
            await EmitLeaderAsync(session, "info",
                "The live Foundry agents can't be reached right now, so the team's review is replayed offline from the case facts.");
            await RunScriptCoreAsync(session, clientCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Group-chat orchestration failed for case {CaseNumber}", clientCase.CaseNumber);
            await EmitLeaderAsync(session, "error", "Orchestration failed: " + ex.Message);
        }
        finally
        {
            await CompleteAsync(session);
        }
    }

    private GuruAgent CreateSpecialist(GroupChatParticipant participant)
    {
        var keys = AgentRoleAliases.TryGetValue(participant.AgentRole, out var aliases)
            ? aliases
            : [participant.AgentRole];
        ArgumentException? lastError = null;
        foreach (var key in keys)
        {
            try
            {
                return _agentFactory.Create(key);
            }
            catch (ArgumentException ex)
            {
                // Unknown role key — try the next alias.
                lastError = ex;
            }
        }
        throw lastError ?? new ArgumentException($"Unknown agent role '{participant.AgentRole}'.");
    }

    // ── Scripted replay (no Foundry) ─────────────────────────────────────────
    private async Task RunScriptedAsync(GroupChatSession session, CaseRecord clientCase)
    {
        try
        {
            await EmitLeaderAsync(session, "info",
                $"Opening the group chat for case {clientCase.CaseNumber} — {clientCase.ClientName}, {Lower(clientCase.RequestType)}. " +
                "Offline: Foundry isn't configured, so the team's review is replayed from the case facts.");
            await RunScriptCoreAsync(session, clientCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scripted group chat failed for case {CaseNumber}", clientCase.CaseNumber);
            await EmitLeaderAsync(session, "error", "Replay failed: " + ex.Message);
        }
        finally
        {
            await CompleteAsync(session);
        }
    }

    private async Task RunScriptCoreAsync(GroupChatSession session, CaseRecord clientCase)
    {
        var script = ScriptFor(clientCase);
        var byId = Participants.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var step in script.Steps)
        {
            await Task.Delay(AskDelay);
            if (step.SpeakerId == LeaderId)
            {
                // A human decision Theo records in the chat.
                await EmitLeaderAsync(session, "decide", step.Answer);
                continue;
            }

            if (!byId.TryGetValue(step.SpeakerId, out var participant)) continue;
            await EmitLeaderAsync(session, "ask", $"@{participant.Persona} ({participant.Role}) — {step.Question}");
            await Task.Delay(ReplyDelay);
            await EmitAsync(session, "reply", participant.Id, participant.Persona, participant.Role, step.Answer);
        }

        await Task.Delay(AskDelay);
        await EmitLeaderAsync(session, "conclude", script.Conclusion);
    }

    private static async Task CompleteAsync(GroupChatSession session)
    {
        session.Completed = true;
        await session.Events.Writer.WriteAsync(new GroupChatTurn(
            "done", "system", "system", "system", "", DateTimeOffset.UtcNow));
        session.Events.Writer.TryComplete();
    }

    private static Task EmitLeaderAsync(GroupChatSession session, string kind, string text) =>
        EmitAsync(session, kind, LeaderId, LeaderPersona, LeaderRole, text);

    private static async Task EmitAsync(
        GroupChatSession session, string kind, string speakerId,
        string persona, string role, string text)
    {
        var turn = new GroupChatTurn(kind, speakerId, persona, role, text, DateTimeOffset.UtcNow);
        session.Turns.Add(turn);
        await session.Events.Writer.WriteAsync(turn);
    }

    // ── Case facts shared by the prompts (and the Team Leader dashboard) ──────

    /// <summary>
    /// Canonical key facts on file for a Client Case, as at the point the
    /// case reaches Theo's desk. Used to ground the live group-chat prompts
    /// and the Team Leader agent's practice review.
    /// </summary>
    public static IReadOnlyList<string> KeyFacts(CaseRecord c) => LifeScenarioCases.KeyOf(c) switch
    {
        LifeScenarioCases.Priya =>
        [
            "Priya Natarajan, 34 (DOB 14/02/1992), physiotherapist at a private clinic in Brunswick, salary $105,000 + super, non-smoker; lives in Coburg with partner Arjun Mehta (36) and daughter Asha (2); second baby due March 2027. Client CL-104582.",
            "Referred by Hannah Lewis (Yarra Home Loans) on Tue 3 Nov 2026 with Priya's consent, after buying a home in October: joint mortgage $680,000; savings $45,000; premium budget up to $300 a month.",
            $"Existing cover: Riverbend Super default death and TPD $210,000 (kept). Kestrel Retail Super (balance $8,200) default death cover $50,000 — no contributions since 30 Jul 2025, so under Protecting Your Super (SIS Act s68AAA) it stops after {LifeRules.PysInactivity} of inactivity (30 Nov 2026) unless Priya elects in writing to keep it. She elected to keep it until the new cover is in force.",
            "Health disclosed: mild asthma (well controlled, preventer inhaler); lower back strain in 2023 (six weeks of physio, full recovery).",
            "Needs analysis: life $1,010,000 less $45,000 savings and $210,000 super cover = $755,000 → $750,000; TPD $600,000; trauma $150,000; income protection $6,125 a month (70% of $105,000), 90-day waiting period, benefit period to age 65.",
            "Structure: life and any-occupation TPD inside super (Corella Life's super plan, funded by rollover from Riverbend Super); own-occupation TPD extension outside super (super-linked); trauma and income protection outside super.",
            "Research: Corella Life $3,180 a year stepped (recommended), Harbourline Life $3,412, Lyrebird Life $3,690; Corella level option $4,050 in year 1. Inside super $858 a year; outside super $2,322 a year ($193.50 a month).",
            "Commission within the LIF caps, disclosed in dollars with Priya's written consent: $1,908 upfront and $636 a year from year 2. No advice fee.",
            "SoA drafted 5 Nov; Amelia signed 6 Nov; Priya signed the Authority to Proceed 7 Nov. KYC complete (driver licence and payslip legit; no PEP or sanctions match).",
            "Application lodged with Corella Life 9 Nov (duty to take reasonable care explained first); tele-interview 10 Nov. Decision Mon 16 Nov: life, TPD and trauma at standard rates (asthma accepted at standard); income protection with an exclusion for back and spine conditions, reviewed after two symptom-free years.",
            "Accepting the exclusion is Priya's decision with Amelia (Tue 17 Nov). If accepted, policy CR-26118204 starts Wed 18 Nov 2026.",
        ],
        LifeScenarioCases.Grace =>
        [
            "Grace Williams, 61 (DOB 22/07/1965), semi-retired school principal, Hawthorn; widowed in 2018; sons Daniel (32) and James (29). Client since 2019, CL-100377. Verified mobile 0491 570 157; verified email grace.williams@example.com.",
            "Lyrebird Life policy LB-19004417 (outside super, stepped): life cover $500,000 and trauma cover $200,000; nominated beneficiaries Daniel 50%, James 50%.",
            "Travel notice (annual review with Amelia, 28 Sep 2026): in Italy from 20 Oct to 1 Dec 2026.",
            "Mon 2 Nov 10:41 — a caller claiming to be Grace passed basic checks (name, date of birth, address) and changed the mobile to 0491 570 159 and the email to grace.w.claims@example.net (\"I've lost my phone\"); no code was sent to the old mobile. 10:58 — client portal login from a new device on a Sydney IP.",
            "Fri 6 Nov 07:52 — email from the new address: a trauma claim for breast cancer for the full $200,000, \"urgent\", paid into a new account ending 4471 in the name \"G Williams Consulting Pty Ltd\". About 93 hours after the contact change.",
            "Attachments: oncologist's report (\"Dr A. Moretti, Eastern Oncology Group\") — fake (pasted letterhead, edited dates, font mismatch, provider number in the wrong format, no histopathology); driver licence — tampered (photo region edited); bank statement — suspicious (account opened 3 Nov; name doesn't match the policy owner).",
            "Lyrebird Life pays cancer claims only on a confirmed specialist diagnosis with histopathology, and pays benefits to the policy owner.",
            "Blocking on financial-crime grounds and referring suspected fraud to the insurer are human decisions (Theo). Grace is the likely victim — never suggest otherwise.",
        ],
        LifeScenarioCases.George =>
        [
            "George Papadopoulos, 52 (DOB 19/04/1974), master joiner and co-founder (50%) of Northcote Custom Joinery Pty Ltd (founded 2011, 14 staff, revenue $3.1m, net profit $520,000); lives in Thornbury with his wife Helen. Client CL-104611; george@northcotejoinery.example.com.au; 0491 570 158.",
            "Business partner Harpreet Kaur, 47 (DOB 08/09/1979), operations and finance director (50%). Client CL-104612.",
            "Referred by Marcus Tan (Bayside Business Accountants) on Mon 9 Nov 2026 — no succession plan. Business valued at $2,400,000 (Marcus's valuation at 30 Jun 2026).",
            "Buy-sell: life and TPD $1,200,000 each (50% of $2.4m); each partner owns the policy on their own life; backed by a buy-sell agreement their lawyer drafts (Life Advice Guru doesn't draft legal documents).",
            "Key person: $400,000 life and TPD on George, owned by the company for a revenue purpose (Marcus estimates losing George would cost about $400,000 of profit over 18 months while a replacement is found and trained). Tax treatment confirmed by Marcus as their accountant.",
            "Total cover on the case $2,800,000 — advice of $2,000,000 or more on one case gets Theo's second review before the SoA is presented.",
            "Research, year-1 stepped premiums at standard rates: Northbank Life $8,620 (recommended: business-protection features and fast financial underwriting), Corella Life $9,180, Harbourline Life $9,410. Northbank split: George buy-sell $4,140, George key person $1,380 (paid by the company), Harpreet buy-sell $3,100.",
            "KYC on the company and both beneficial owners: company extract and both IDs legit; no PEP or sanctions match.",
            "Underwriting requirements once the Authority to Proceed is signed: George — blood profile, medical examination and financial evidence (two years' company financials and the valuation); Harpreet — blood profile and financial evidence.",
        ],
        LifeScenarioCases.Tom =>
        [
            "Tom Bradley, 41 (DOB 03/03/1985), self-employed electrician (sole trader, Bradley Electrical), Frankston; wife Kylie (39, part-time bookkeeper); two children (8 and 5). Client since 2023, CL-102265; 0491 570 110; tom.bradley@example.com.",
            "Harbourline Life income protection HL-23051877 (outside super), started 15 Mar 2023: insured monthly benefit $7,000 (set from $120,000 a year at application), 30-day waiting period, benefit period to age 65, premiums $2,880 a year (tax-deductible).",
            "Injury Wed 14 Oct 2026: fell from a ladder on a job — torn rotator cuff (right shoulder); surgery 21 Oct; surgeon expects four to six months off work.",
            "Tom called on Wed 11 Nov; Kylie joined as his support person with his written authority. Financial stress flagged — vulnerable client.",
            "Benefit basis: Harbourline's policy (written after APRA's individual disability income insurance changes, so the benefit is based on income at claim) pays the lesser of the insured benefit and 70% of average monthly earnings in the 12 months before disability.",
            "Net business income for the 12 months to 30 Sep 2026: $96,000 (BAS and profit and loss) → $8,000 a month → 70% = $5,600 a month.",
            "Benefit $5,600 a month (not $7,000) accrues from 13 Nov 2026 after the 30-day waiting period, paid monthly in arrears; first payment about 13 Dec 2026; taxable income.",
            "Felix: BAS, profit and loss and surgeon's report legit. Lodging any claim needs human approval (Teams); Amelia calls Tom personally before the letter.",
        ],
        LifeScenarioCases.Robert =>
        [
            "Robert Chen, 44 (DOB 30/01/1982), secondary school teacher, Box Hill; children Lily (12) and Max (9). Clients since 2019: Mei CL-101934, Robert CL-101935; 0491 570 313; robert.chen@example.com.",
            "His wife Mei Chen (DOB 05/06/1984), a pharmacist, died suddenly of a brain aneurysm on Mon 12 Oct 2026, aged 42.",
            "Mei's Corella Life policy CR-19033105 (outside super), advised by Amelia in March 2019: life cover $1,000,000; policy owner and life insured Mei; nominated beneficiary Robert 100%.",
            $"In force for more than seven years — well past the {LifeRules.MisrepresentationWindow} window for non-fraudulent misrepresentation. Premiums paid, no exclusions, nomination valid → paid directly to Robert, not through Mei's estate.",
            "Mei's Riverbend Super account also had default death cover of $180,000 — a separate claim with the fund's trustee, paid under the fund's rules.",
            "Fri 13 Nov: Robert emailed Amelia a certified copy of the death certificate and his ID. Felix: both legit.",
            "Bereavement — vulnerable. One named contact (Amelia); Theo approves every message to the family. Every death claim needs human approval before lodgement (Teams).",
        ],
        _ =>
        [
            $"{c.RequestType} for {c.ClientName} ({c.ClientNumber}), received {PracticeDates.Display(c.ReceivedDate)}: {c.RequestSummary}",
            $"Cover: {c.Cover}. Amount: {c.Amount}. Referred by: {ReferredByText(c)}.",
            $"Priority: {c.Priority} — {c.PriorityReason}",
        ]
    };

    private static string ReferredByText(CaseRecord c) =>
        string.IsNullOrWhiteSpace(c.ReferredBy) ? "Existing client" : c.ReferredBy;

    private static string BuildBrief(CaseRecord c)
    {
        var lines = new List<string>
        {
            "CLIENT CASE FOR GROUP-CHAT REVIEW",
            "=================================",
            $"Case number    : {c.CaseNumber}",
            $"Client         : {c.ClientName}",
            $"Client email   : {c.ClientEmail}",
            $"Client phone   : {c.ClientPhone}",
            $"Client number  : {c.ClientNumber}",
            $"Cover          : {c.Cover}",
            $"Request type   : {c.RequestType}",
            $"Received       : {c.ReceivedDate}",
            $"Adviser        : {c.AdviserName}",
            $"Referred by    : {ReferredByText(c)}",
            $"Amount         : {c.Amount}",
            $"Priority       : {c.Priority} ({c.PriorityReason})",
            "",
            "REQUEST SUMMARY",
            "---------------",
            c.RequestSummary,
            "",
            "KEY FACTS ON FILE",
            "-----------------",
        };
        lines.AddRange(KeyFacts(c).Select(f => "- " + f));
        return string.Join("\n", lines);
    }

    private static string BuildManagerPrompt(
        CaseRecord c, IReadOnlyList<ChatMessage> history, int iteration, int maxIterations)
    {
        var roster = string.Join("\n", Participants.Select(p => $"- {p.Id}: {p.Persona}, {p.Role}"));
        var transcript = RenderTranscript(history);

        return
            "You are Theo, the Practice Team Leader at Life Advice Guru, chairing a group chat with\n" +
            "the practice specialists about ONE Client Case. You are the GROUP-CHAT MANAGER: you decide\n" +
            "which specialist speaks next, or whether the team has covered what the case needs and the\n" +
            "discussion should end. Amelia, the Senior Risk Adviser, gives the advice and signs every SoA.\n\n" +
            $"This is turn {iteration + 1} of at most {maxIterations}.\n\n" +
            "PARTICIPANTS (use the id in the JSON 'speaker' field):\n" +
            roster + "\n\n" +
            "CLIENT CASE:\n" +
            BuildBrief(c) + "\n\n" +
            "TRANSCRIPT SO FAR:\n" +
            (string.IsNullOrWhiteSpace(transcript) ? "(empty)" : transcript) + "\n\n" +
            "DECIDE the next step. Options:\n" +
            "  1. Pick the most relevant specialist who has NOT yet covered their part of the case,\n" +
            "     following the lifecycle Discover → Verify → Advise → Implement → Confirm, and ask\n" +
            "     them ONE focused question.\n" +
            "  2. Conclude once the team has covered what this case needs — the fact find and the\n" +
            "     in-good-order checklist, identity and financial crime, the needs analysis and SoA,\n" +
            "     research, underwriting or the claim, and client and referral-partner communications —\n" +
            "     or earlier if the case is straightforward. The summary must be a short numbered plan\n" +
            "     with owners, the human decisions needed (and who makes each one), the clocks that apply\n" +
            "     and the expected outcome. Recommend — a person decides.\n\n" +
            "Respond with ONLY a JSON object (no prose, no markdown fences) in this exact shape:\n" +
            "{\n" +
            "  \"action\":  \"ask\" | \"conclude\",\n" +
            "  \"speaker\": \"<participant id>\"        // required when action=ask\n" +
            "  \"prompt\":  \"<question to specialist>\" // required when action=ask\n" +
            "  \"summary\": \"<Theo's plan>\"            // required when action=conclude\n" +
            "}\n";
    }

    private static string BuildSpecialistPrompt(
        CaseRecord c, GroupChatParticipant participant, string question,
        IReadOnlyList<ChatMessage> history)
    {
        var transcript = RenderTranscript(history);
        return
            "You are taking part in a Life Advice Guru practice GROUP CHAT about Client Case\n" +
            $"{c.CaseNumber}, chaired by Theo (Practice Team Leader).\n\n" +
            "Stay in character as your role and answer Theo's question directly and concisely\n" +
            "(under 150 words). Do NOT repeat your full standard report — speak conversationally,\n" +
            "as if in a case review with the team. Use only the facts on the case. Never give\n" +
            "personal advice (Amelia, the adviser, gives the advice), never accuse a client of fraud,\n" +
            "treat health information as sensitive and mask TFNs and bank accounts. End with a single\n" +
            "line beginning 'Recommendation:' summarising your input.\n\n" +
            "CLIENT CASE:\n" + BuildBrief(c) + "\n\n" +
            "TRANSCRIPT SO FAR:\n" +
            (string.IsNullOrWhiteSpace(transcript) ? "(empty)" : transcript) + "\n\n" +
            $"THEO ASKS YOU ({participant.Persona}, {participant.Role}):\n" + question;
    }

    private static string RenderTranscript(IReadOnlyList<ChatMessage> history)
    {
        var lines = new List<string>();
        foreach (var msg in history)
        {
            if (msg.Role == ChatRole.System) continue;
            var who = string.IsNullOrWhiteSpace(msg.AuthorName) ? msg.Role.Value : msg.AuthorName;
            lines.Add($"{who}: {msg.Text}");
        }
        return string.Join("\n\n", lines);
    }

    private static ManagerDecision? ParseManagerDecision(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Strip markdown code fences if the model added them despite instructions.
        var cleaned = Regex.Replace(text, "```(?:json)?", "", RegexOptions.IgnoreCase).Trim();
        var braceStart = cleaned.IndexOf('{');
        var braceEnd = cleaned.LastIndexOf('}');
        if (braceStart < 0 || braceEnd <= braceStart) return null;

        var json = cleaned.Substring(braceStart, braceEnd - braceStart + 1);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new ManagerDecision(
                Action: root.TryGetProperty("action", out var a) ? a.GetString() : null,
                Speaker: root.TryGetProperty("speaker", out var s) ? s.GetString() : null,
                Prompt: root.TryGetProperty("prompt", out var p) ? p.GetString() : null,
                Summary: root.TryGetProperty("summary", out var sm) ? sm.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

    private static string Lower(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "request" : s.ToLowerInvariant();

    private record ManagerDecision(string? Action, string? Speaker, string? Prompt, string? Summary);

    // ── Scripted case reviews (canonical case facts) ──────────────────────────
    // A step whose SpeakerId is the leader is a human decision Theo records.
    private record ScriptStep(string SpeakerId, string Question, string Answer);

    private record GroupChatScript(IReadOnlyList<ScriptStep> Steps, string Conclusion);

    private static ScriptStep Decide(string decision) => new(LeaderId, string.Empty, decision);

    private static GroupChatScript ScriptFor(CaseRecord c) => LifeScenarioCases.KeyOf(c) switch
    {
        LifeScenarioCases.Priya => PriyaScript(c),
        LifeScenarioCases.Grace => GraceScript(c),
        LifeScenarioCases.George => GeorgeScript(c),
        LifeScenarioCases.Tom => TomScript(c),
        LifeScenarioCases.Robert => RobertScript(c),
        _ => GenericScript(c)
    };

    // Priya — at Corella's underwriting decision (Mon 16 Nov 2026).
    private static GroupChatScript PriyaScript(CaseRecord c) => new(
    [
        new("onboarding",
            "Take us back to the start — what came in, and what did you catch?",
            "Hannah Lewis at Yarra Home Loans referred Priya on Tue 3 Nov, with her consent, after she and Arjun bought their home in Coburg — a $680,000 joint mortgage, Asha is 2 and a second baby is due in March. " +
            "The fact find shows a $105,000 salary, $45,000 in savings and up to $300 a month for premiums. " +
            $"I caught one thing early: her old Kestrel Retail Super account has had no contributions since 30 Jul 2025, so under Protecting Your Super its $50,000 death cover stops after {LifeRules.PysInactivity} — on 30 Nov 2026 — unless she tells the fund in writing that she wants to keep it.\n" +
            "Recommendation: deal with the Kestrel cover in the advice, before 30 Nov."),
        new("financial-crime",
            "Identity and KYC?",
            "Done. Priya's driver licence and payslip are legit, KYC is complete, and there's no PEP or sanctions match.\n" +
            "Recommendation: no hold."),
        new("paraplanning",
            "Walk us through the needs analysis.",
            "Life: $680,000 mortgage, $100,000 for the children's education, $200,000 of family income support and $30,000 for final expenses — $1,010,000 — less $45,000 savings and her $210,000 Riverbend Super cover leaves $755,000, so $750,000. " +
            "TPD: $810,000 less $210,000 = $600,000. Trauma: $152,500, so $150,000. Income protection: 70% of $105,000 = $6,125 a month, 90-day waiting period, to age 65. " +
            "Life and any-occupation TPD sit inside super with an own-occupation TPD extension outside super; trauma and income protection sit outside super.\n" +
            "Recommendation: over to Ivy for the research."),
        new("insurance-research",
            "Which insurer fits, and what does it cost?",
            "Year-1 stepped premiums for the full package: Corella Life $3,180, Harbourline Life $3,412 and Lyrebird Life $3,690. Corella is the lowest cost and offers own-occupation TPD for physiotherapists. " +
            "Its level-premium option was $4,050 in year 1. Outside super Priya pays $2,322 a year — $193.50 a month, inside her $300 budget — and $858 a year is paid from super.\n" +
            "Recommendation: Corella Life on cost and definitions."),
        new("paraplanning",
            "And the Statement of Advice?",
            "I drafted it on Thu 5 Nov. The SoA — Amelia's advice — recommends she keeps the Kestrel cover until the new cover is in force; cancelling or replacing cover is her decision. " +
            "There's no advice fee; Corella's commission of $1,908 upfront and $636 a year from year 2 is shown in dollars, and Priya gave her written consent. Amelia signed the SoA on 6 Nov and Priya signed the Authority to Proceed on 7 Nov.\n" +
            "Recommendation: nothing outstanding on the advice file."),
        new("underwriting",
            "Where has Corella landed?",
            "I explained the duty to take reasonable care before we lodged on 9 Nov — and that she doesn't need to disclose genetic test results, which insurers can't ask for or use from 8 Oct 2026 — and Priya did a tele-interview on 10 Nov. Corella's decision came in today: life, TPD and trauma at standard rates, and her asthma accepted at standard. " +
            "Income protection comes with an exclusion for back and spine conditions, because of the 2023 back strain she disclosed — Corella will review it after two symptom-free years. " +
            "If she accepts, policy CR-26118204 starts on Wed 18 Nov.\n" +
            "Recommendation: accepting the exclusion is Priya's decision with Amelia — I'll confirm with Corella once she decides."),
        new("client-communications",
            "What will Priya and Hannah hear from us?",
            "Once Priya decides: a welcome letter and a plain-English policy summary that explains what the back exclusion means, her premiums and her cooling-off period. " +
            "Hannah gets a short note that cover is in place — nothing more. Priya consented to share the outcome only, so no health details.\n" +
            "Recommendation: send after Priya's decision — spot-check only."),
    ],
        $"Thanks, team — a clean file. The exclusion is Priya's decision with Amelia, not ours. Plan for {c.CaseNumber}:\n" +
        "1. Amelia calls Priya today and explains Corella's decision in plain English; Priya decides by Tue 17 Nov.\n" +
        "2. If she accepts, Uma confirms with Corella Life: policy CR-26118204 starts Wed 18 Nov 2026.\n" +
        "3. The Kestrel cover stays until the new policy starts — any change after that is Priya's call, with Amelia's advice.\n" +
        $"4. Cara sends the welcome letter and policy summary (with the cooling-off period of {LifeRules.CoolingOff}), and Hannah's outcome-only note.\n" +
        "5. File audit: best-interests steps, the needs analysis, the replacement check and the commission consent are all on file.\n" +
        "Expected outcome: ✅ Completed — life $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month, with a back exclusion on income protection.");

    // Grace — at the block decision (Fri 6 Nov 2026).
    private static GroupChatScript GraceScript(CaseRecord c) => new(
    [
        new("onboarding",
            "What came in, and why is it Urgent?",
            "At 07:52 this morning an email from grace.w.claims@example.net made a trauma claim for breast cancer for the full $200,000, marked urgent, to be paid into a new account ending 4471 in the name \"G Williams Consulting Pty Ltd\". " +
            "Grace's mobile and email were changed by phone on Mon 2 Nov at 10:41 — \"I've lost my phone\", and no code went to the old mobile — and a new device logged in to her portal from a Sydney IP at 10:58. " +
            "Her travel notice says she's in Italy until 1 Dec.\n" +
            "Recommendation: nothing lodged — I've opened the case as Urgent for Ben and Felix."),
        new("claims",
            "Does the claim stand up against the policy?",
            "No. Grace's Lyrebird Life policy LB-19004417 has $200,000 of trauma cover and $500,000 of life cover. A cancer claim needs a confirmed specialist diagnosis with histopathology, and there isn't one. " +
            "Lyrebird pays benefits to the policy owner, so a new account in another name can't be used.\n" +
            "Recommendation: don't lodge — over to Felix."),
        new("financial-crime",
            "What's your read?",
            "Risk HIGH. The timeline joins up: the contact change on Mon 2 Nov at 10:41, a new device 17 minutes later, then this claim about 93 hours after the change. " +
            "The oncologist's report is fake — a pasted letterhead, edited dates, a font mismatch, a provider number in the wrong format and no histopathology. The licence photo has been edited, and the bank account was opened on 3 Nov in a name that isn't Grace's. " +
            "Everything points to someone using Grace's identity — Grace is the likely victim here, not a suspect.\n" +
            "Recommendation: block the claim and the bank change, restore her verified details, lock the portal, alert Lyrebird Life's fraud team and contact Grace only on 0491 570 157."),
        Decide("I approve the block. Restore Grace's verified contact details, lock her portal access and alert Lyrebird Life's fraud team. Nothing is lodged and nothing is paid."),
        new("claims",
            "Confirm where things stand with Lyrebird.",
            "Confirmed — no claim has been lodged with Lyrebird, the bank change is rejected and the policy is untouched: $500,000 life and $200,000 trauma, with Daniel and James still the beneficiaries at 50% each.\n" +
            "Recommendation: close the claim request as blocked."),
        new("client-communications",
            "How do we reach the real Grace?",
            "Only on her verified mobile, 0491 570 157, and grace.williams@example.com — never the details added on 2 Nov. Florence is ten hours behind Melbourne, so I'll call at 6pm our time, 8am hers. " +
            "The call script leads with reassurance — her cover is safe — and makes it clear she did nothing wrong. Then a reassurance letter with identity-protection steps.\n" +
            "Recommendation: call first, letter second — I approve both myself before they go, as the authority matrix requires for any message to a fraud victim."),
    ],
        $"Plan for {c.CaseNumber}:\n" +
        "1. Blocked: no claim lodged and the bank change rejected (Ben).\n" +
        "2. Grace's verified contact details restored, and portal access locked until she re-verifies (Felix).\n" +
        "3. Lyrebird Life's fraud team alerted with the timeline and the document findings, describing Grace as the likely victim (Felix).\n" +
        "4. Cara calls Grace on 0491 570 157, then sends the reassurance letter and identity-protection steps — Cara approves her letters before they go.\n" +
        "5. Coaching: a contact-detail change needs a code to, or a call-back on, the verified mobile held before the change; basic checks alone aren't enough.\n" +
        "Outcome: 🛡️ Blocked — no claim lodged; $200,000 protected; Grace's $500,000 life cover and $200,000 trauma cover are safe.");

    // George — at Theo's second review (total cover $2,800,000).
    private static GroupChatScript GeorgeScript(CaseRecord c) => new(
    [
        new("onboarding",
            "What did Marcus send us?",
            "Marcus Tan at Bayside Business Accountants referred George Papadopoulos and Harpreet Kaur on Mon 9 Nov. They each own 50% of Northcote Custom Joinery — founded 2011, 14 staff, $3.1m revenue and $520,000 net profit — and they have no succession plan. " +
            "Marcus valued the business at $2,400,000 at 30 Jun 2026. I've opened files for both: CL-104611 and CL-104612.\n" +
            "Recommendation: KYC on the company and both owners, then Penny."),
        new("financial-crime",
            "KYC on the company and both owners?",
            "Complete. The company extract and both IDs are legit, George and Harpreet are the beneficial owners at 50% each, and there's no PEP or sanctions match.\n" +
            "Recommendation: no hold."),
        new("paraplanning",
            "What does the business need, and how should it be held?",
            "Buy-sell: life and TPD of $1,200,000 each — 50% of the $2.4m valuation — with each partner owning the policy on their own life, backed by a buy-sell agreement their lawyer drafts. We don't draft legal documents. " +
            "Key person: $400,000 of life and TPD on George, owned by the company for a revenue purpose — Marcus estimates losing George would cost about $400,000 of profit over 18 months. Marcus has confirmed the tax treatment as their accountant. " +
            "Total cover is $2,800,000, so it needs your second review before Amelia presents the SoA.\n" +
            "Recommendation: second review, then the SoA."),
        new("insurance-research",
            "Who did you compare?",
            "Year-1 stepped premiums at standard rates for all three covers: Northbank Life $8,620, Corella Life $9,180 and Harbourline Life $9,410. Northbank is recommended for its business-protection features and fast financial underwriting. " +
            "The Northbank split is George's buy-sell $4,140, George's key person $1,380 — paid by the company — and Harpreet's buy-sell $3,100.\n" +
            "Recommendation: Northbank Life."),
        Decide("Second review done. The sums tie to Marcus's valuation and profit estimate, the ownership matches each purpose, the lawyer owns the agreement and the research compares three insurers on our APL. I approve — Amelia can present and sign the SoA."),
        new("underwriting",
            "What will Northbank need once they sign the Authority to Proceed?",
            "For their ages and sums insured: George — a blood profile, a medical examination and financial evidence, meaning two years of company financials and the valuation; Harpreet — a blood profile and financial evidence. " +
            "I'll walk them both through the duty to take reasonable care before anything is lodged. If Northbank offers a loading or an exclusion, accepting it is their decision with Amelia.\n" +
            "Recommendation: lodge with Northbank as soon as the Authority to Proceed is signed."),
        new("client-communications",
            "Who hears what, and when?",
            "Separate letters to George and Harpreet once cover starts — each sees only their own cover and their own underwriting outcome. Marcus gets an update with their consent: cover in place and their lawyer finalising the buy-sell agreement. No health details.\n" +
            "Recommendation: draft when Northbank issues the policies."),
    ],
        $"Plan for {c.CaseNumber}:\n" +
        "1. Second review complete — I approve the $2,800,000 business protection advice.\n" +
        "2. Amelia presents and signs the SoA; George and Harpreet sign the Authority to Proceed.\n" +
        "3. Uma lodges with Northbank Life and tracks the medical and financial requirements.\n" +
        "4. Any loading or exclusion is George's or Harpreet's decision with Amelia.\n" +
        "5. Their lawyer drafts the buy-sell agreement; Cara updates Marcus with their consent.\n" +
        "Expected outcome: ✅ buy-sell cover of $1.2 million each and $400,000 key person cover with Northbank Life, subject to underwriting.");

    // Tom — at the lodgement decision (Wed 11 Nov 2026).
    private static GroupChatScript TomScript(CaseRecord c) => new(
    [
        new("onboarding",
            "What do we know so far?",
            "Tom called this afternoon, Wed 11 Nov, with Kylie on the line as his support person — we have his written authority. He fell from a ladder on a job on 14 Oct, tore his right rotator cuff and had surgery on 21 Oct; his surgeon expects four to six months off work. " +
            "He's a sole trader with two young children, so I've flagged financial stress and set the priority to High.\n" +
            "Recommendation: over to Ben for the policy and evidence check."),
        new("claims",
            "What does his policy pay, and why?",
            "Harbourline Life income protection HL-23051877, in force since 15 Mar 2023 with premiums paid: $7,000 a month insured, a 30-day waiting period and a benefit period to age 65. " +
            "Like every policy written since APRA's income protection changes, the benefit is based on his income at the time of the claim: Harbourline pays the lesser of the insured benefit and 70% of his average monthly earnings in the 12 months before the injury. " +
            "His BAS and profit and loss show $96,000 net for the year to 30 Sep — $8,000 a month — so 70% is $5,600. " +
            "The benefit accrues from 13 Nov, after the waiting period, and is paid monthly in arrears, so the first payment is about 13 Dec. It's taxable income. The claim pack is ready.\n" +
            "Recommendation: lodge at an expected $5,600 a month — I need your approval."),
        new("financial-crime",
            "Are the documents genuine?",
            "Yes. The BAS, the profit and loss and the surgeon's report are all legit, and the payment account is in Tom's name with no recent changes.\n" +
            "Recommendation: no hold."),
        Decide("I've done the vulnerable-client review, and I approve lodging the claim. Amelia — please call Tom personally before any letter goes, so he hears about the $5,600 from a person first."),
        new("claims",
            "Next steps on lodgement?",
            "I'll raise the Teams approval card for your sign-off and lodge with Harbourline Life tomorrow, Thu 12 Nov. Harbourline usually decides within 5 business days of having everything, and the Life Insurance Code of Practice backstop is 2 months from the end of the waiting period — by Wed 13 Jan 2027 — with updates at least every 20 business days. I'll track it and keep Tom and Kylie posted.\n" +
            "Recommendation: lodge on Thursday."),
        new("client-communications",
            "And the letter?",
            "After Amelia's call: a plain-English letter that shows the $5,600 working against the $7,000 insured, when payments start, that they're taxable income, where to find free financial counselling and his right to complain. " +
            "There's also a note for Amelia — not advice: his insured benefit is more than his recent income supports, so she may want to look at it at his next review.\n" +
            "Recommendation: send only after Amelia's call — I approve the letter before it goes."),
    ],
        $"Plan for {c.CaseNumber}:\n" +
        "1. Lodgement approved — Ben lodges with Harbourline Life on Thu 12 Nov at an expected $5,600 a month.\n" +
        "2. Amelia calls Tom (and Kylie) first; Cara's letter follows, approved by Cara.\n" +
        "3. Ben tracks Harbourline's decision — the Life Code backstop is Wed 13 Jan 2027 — and stays Tom's day-to-day contact.\n" +
        "4. The note about his insured benefit stays with Amelia — whether anything changes is her advice to give, at his next review.\n" +
        "Expected outcome: 🟡 Partially approved — $5,600 a month, not the $7,000 insured, because the benefit follows his earnings in the 12 months before the injury.");

    // Robert — at the lodgement decision (Mon 16 Nov 2026).
    private static GroupChatScript RobertScript(CaseRecord c) => new(
    [
        new("onboarding",
            "What do we have from Robert?",
            "Robert emailed Amelia on Fri 13 Nov with a certified copy of Mei's death certificate and his ID. Mei died suddenly of a brain aneurysm on Mon 12 Oct, aged 42; Robert is caring for Lily, 12, and Max, 9. They've been clients since 2019. " +
            "I've set the priority to High and flagged bereavement.\n" +
            "Recommendation: compassionate handling, with one named contact."),
        Decide("I'll oversee this case personally. Amelia is the family's one named contact, and I'll approve every message to the family before it goes."),
        new("financial-crime",
            "Are the documents genuine?",
            "Yes — the death certificate and Robert's ID are legit. We won't need to ask the family for anything more.\n" +
            "Recommendation: no further checks."),
        new("claims",
            "What does the policy pay, and to whom?",
            "Corella Life CR-19033105, advised by Amelia in March 2019: life cover of $1,000,000, owned by Mei on her own life, with Robert as nominated beneficiary at 100%. " +
            $"It's in force with premiums paid and no exclusions, and it's been in force for more than seven years — well past the {LifeRules.MisrepresentationWindow} window for non-fraudulent misrepresentation. " +
            "The nomination is valid, so the benefit is paid directly to Robert, not through Mei's estate. I also found $180,000 of default death cover in Mei's Riverbend Super account — a separate claim with the fund's trustee.\n" +
            "Recommendation: lodge with Corella today — every death claim needs your approval."),
        Decide("Approved in Teams — Ben, please lodge Mei's claim with Corella Life today."),
        new("client-communications",
            "How do we keep this gentle?",
            "Amelia calls Robert first. Then a short, gentle letter from Amelia when Corella pays, with a simple checklist for the Riverbend Super claim so he doesn't have to remember anything from the call. " +
            "No jargon, one name and one number. Every message comes to you first.\n" +
            "Recommendation: draft for your approval."),
    ],
        $"Thank you, everyone. Plan for {c.CaseNumber}:\n" +
        "1. Ben lodges Mei's claim with Corella Life today, Mon 16 Nov. Corella gives every death claim a dedicated case manager and usually decides within 5 business days of having everything; the Life Code backstop is 6 months, with updates at least every 20 business days. Ben will follow it closely.\n" +
        "2. Ben helps Robert start the Riverbend Super claim for the $180,000, which the trustee pays under the fund's rules.\n" +
        "3. Amelia stays the family's one contact, and every message comes to me first.\n" +
        "4. Cara drafts the letter and the super checklist, ready for when Corella pays.\n" +
        "Expected outcome: ✅ $1,000,000 paid directly to Robert, with Mei's $180,000 super claim under way.");

    // Cases opened live with the Onboarding Agent — a first case review
    // shaped by the request type.
    private static GroupChatScript GenericScript(CaseRecord c)
    {
        var t = (c.RequestType ?? string.Empty).ToLowerInvariant();
        var opening = new ScriptStep("onboarding",
            "What's on the case, and is it in good order?",
            $"{c.RequestType} for {c.ClientName} ({c.ClientNumber}), received {PracticeDates.Display(c.ReceivedDate)}. {c.RequestSummary} Priority {c.Priority}: {c.PriorityReason}\n" +
            "Recommendation: I'll finish the in-good-order checklist and route it to the right specialist.");

        if (t.Contains("death"))
        {
            return new GroupChatScript(
            [
                opening,
                Decide("This is a bereavement. I'll oversee the case, Amelia is the family's one named contact, and every message to the family comes to me first."),
                new("financial-crime", "Are the documents genuine?",
                    "I'll check the death certificate and the claimant's identification quietly in the background, so the family isn't asked twice.\n" +
                    "Recommendation: no hold unless a document doesn't check out."),
                new("claims", "What will you check before lodging?",
                    "That the policy is in force with premiums paid, any exclusions, the ownership and the nominated beneficiaries — and whether there's other cover, for example inside super. I'll build the claim pack and raise the approval.\n" +
                    "Recommendation: lodge once you approve — every death claim needs a person's sign-off."),
                new("client-communications", "What does the family hear today?",
                    "A gentle acknowledgement from Amelia: condolences first, what happens next and one name to call. It comes to you before it goes.\n" +
                    "Recommendation: send after your approval."),
            ],
                $"Plan for {c.CaseNumber}:\n" +
                "1. Felix verifies the documents; Ben checks the policy and builds the claim pack.\n" +
                "2. I approve the lodgement, and every message to the family.\n" +
                "3. Amelia stays the family's one named contact.");
        }

        if (t.Contains("claim"))
        {
            var bankChange = t.Contains("bank");
            return new GroupChatScript(
            [
                opening,
                new("claims", "What does the policy need before we can lodge?",
                    "I'll check the definition, the waiting period, any exclusions and the evidence the insurer needs — usually a specialist's or treating doctor's report, plus proof of earnings for income protection — and build the claim pack.\n" +
                    "Recommendation: lodge only after a person approves."),
                new("financial-crime", bankChange ? "There's a bank change with this claim — what do we do?" : "Any red flags?",
                    bankChange
                        ? "Benefits are paid to the policy owner, so I'll verify the bank change with the client on contact details we held before any recent change, and check the documents for tampering before anything goes to the insurer.\n" +
                          "Recommendation: hold the bank change until it's verified."
                        : "I'll check the medical and financial documents and look for any recent change of contact or bank details.\n" +
                          "Recommendation: no hold unless something doesn't check out."),
                new("client-communications", "What does the client hear today?",
                    "A kind acknowledgement: we've got it, what happens next, what we may need and who to call. I approve every message to a seriously ill or injured client before it goes.\n" +
                    "Recommendation: send within one business day."),
            ],
                $"Plan for {c.CaseNumber}:\n" +
                "1. Ben checks the policy and evidence and builds the claim pack.\n" +
                "2. Felix completes the document and bank-detail checks before anything is lodged.\n" +
                "3. I approve the lodgement and any bank-detail change; Cara approves her letters to the client.\n" +
                "4. Ben tracks the insurer's decision under the Life Insurance Code of Practice.");
        }

        var business = t.Contains("business");
        return new GroupChatScript(
        [
            opening,
            new("financial-crime", "Identity and KYC?",
                (business
                    ? "I'll verify the company and every beneficial owner, and screen for PEP and sanctions matches.\n"
                    : "I'll verify the client's identity and screen for PEP and sanctions matches.\n") +
                "Recommendation: no hold unless screening flags something."),
            new("paraplanning", "How will you work out the cover needed?",
                business
                    ? "From the business valuation and the owners' shares for buy-sell cover, and the profit at risk for key person cover — showing the working. Their lawyer drafts any buy-sell agreement.\n" +
                      "Recommendation: needs analysis, then research, then the SoA for Amelia."
                    : "From the debts, dependants, income and existing cover — including any cover inside super — showing the working for each amount.\n" +
                      "Recommendation: needs analysis, then research, then the SoA for Amelia."),
            new("insurance-research", "Which insurers will you compare?",
                "The insurers on our APL — on definitions and features as well as stepped and level premiums.\n" +
                "Recommendation: compare before the SoA."),
            new("underwriting", "What happens after the Authority to Proceed?",
                "I'll walk the client through the duty to take reasonable care, lodge the application and track the medical and financial requirements. Any loading or exclusion is the client's decision with Amelia.\n" +
                "Recommendation: lodge once the Authority to Proceed is signed."),
            new("client-communications", "What does the client hear today?",
                "An acknowledgement from Amelia: what happens next, what we'll need and how we're paid, in plain English.\n" +
                "Recommendation: send today."),
        ],
            $"Plan for {c.CaseNumber}:\n" +
            "1. Olivia completes the fact find; Felix completes KYC.\n" +
            "2. Penny runs the needs analysis and drafts the SoA; Ivy compares the APL insurers.\n" +
            "3. Amelia gives the advice and signs the SoA; total cover of $2,000,000 or more comes to me for a second review first.\n" +
            "4. Replacing or cancelling any existing cover is the client's decision with Amelia.");
    }
}
