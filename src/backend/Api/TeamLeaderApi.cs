using System.Text;
using CaldovaLife.Agents;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

record TeamLeaderProcessRequest(string CaseNumber);

/// <summary>
/// HTTP endpoints behind the "Try it out" tab on the Team Leader agent page
/// (<c>/agents/team-leader</c>):
///
/// 1. List the Client Cases held in <see cref="CaseStore"/> so the user can
///    pick the case Theo should focus on.
/// 2. Engage the Team Leader Agent. The page always receives Theo's
///    deterministic practice dashboard — the pipeline by lifecycle stage
///    (Discover, Verify, Advise, Implement, Confirm), underwriting, claim and
///    complaint clocks, escalations, the approval queue, vulnerable clients,
///    file-quality audits against the best interests duty, coaching insights
///    and today's priorities — built from the cases in the practice. The five
///    scenario cases are shown at the point each one reaches Theo's desk;
///    cases opened live with the Onboarding Agent sit at Discover. When
///    Foundry is configured the live agent reviews the same practice and its
///    streamed narrative is shown alongside.
/// </summary>
public static class TeamLeaderApi
{
    // ── Dashboard shapes (serialised camelCase to the page) ───────────────────
    internal record StageCount(string Stage, int Count, string Hint);
    internal record PipelineCase(string CaseNumber, string ClientName, string RequestType, string Side, string Priority, string Stage, string Owner, string NextAction);
    internal record PracticeClock(string Clock, string CaseNumber, string ClientName, string Rule, string Status, string State);
    internal record Escalation(string CaseNumber, string ClientName, string Severity, string Reason, string Owner, string Clock);
    internal record ApprovalItem(int Priority, string CaseNumber, string ClientName, string Item, string Approver, string Authority, string Recommendation, string Due);
    internal record VulnerableClient(string CaseNumber, string ClientName, string Flag, string Handling);
    internal record AuditCheck(string Label, string Status, string Note);
    internal record FileAudit(string CaseNumber, string ClientName, string Standard, string Result, IReadOnlyList<AuditCheck> Checks);
    internal record CaseFocus(
        string CaseNumber, string ClientName, string RequestType, string Side, string Stage, string Owner, string AsAt,
        string Summary, string Decision, string Recommendation, IReadOnlyList<string> KeyFacts);
    internal record PracticeView(
        string AsAt,
        IReadOnlyList<StageCount> StageCounts,
        IReadOnlyList<PipelineCase> Cases,
        IReadOnlyList<PracticeClock> Clocks,
        IReadOnlyList<Escalation> Escalations,
        IReadOnlyList<ApprovalItem> Approvals,
        IReadOnlyList<VulnerableClient> Vulnerable,
        IReadOnlyList<FileAudit> FileAudits,
        IReadOnlyList<string> Coaching,
        IReadOnlyList<string> Priorities);

    private record CaseProfile(
        string Stage,
        string Side,
        string Owner,
        string AsAt,
        string NextAction,
        IReadOnlyList<PracticeClock> Clocks,
        Escalation? Escalation,
        IReadOnlyList<ApprovalItem> Approvals,
        VulnerableClient? Vulnerable,
        FileAudit Audit,
        string Summary,
        string Decision,
        string Recommendation,
        string? Today = null,
        string? Coaching = null);

    private const string Discover = "Discover";
    private const string Verify = "Verify";
    private const string Advise = "Advise";
    private const string Implement = "Implement";
    private const string Confirm = "Confirm";

    private const string ProtectionAdvice = "Protection Advice";
    private const string ClaimsAndCare = "Claims & Care";

    private const string Theo = "Theo (Practice Team Leader)";
    private const string Amelia = "Amelia (Senior Risk Adviser)";

    private const string BestInterests = "Best interests duty — advice file";
    private const string ClaimsHandling = "Claims handling and the Life Insurance Code of Practice";

    public static void MapTeamLeaderEndpoints(
        this WebApplication app,
        CaseStore store,
        CaldovaAgentFactory factory,
        ILogger logger)
    {
        // Cases in the practice, most recent first — populates the case
        // picker on both the Try it out and Group Chat tabs.
        app.MapGet("/team-leader/cases", () =>
        {
            var cases = store.All().Select(c => new
            {
                caseNumber = c.CaseNumber,
                clientName = c.ClientName,
                requestType = c.RequestType,
                clientNumber = c.ClientNumber,
                priority = c.Priority,
                receivedDate = c.ReceivedDate,
                createdAt = c.CreatedAt
            });
            return Results.Ok(cases);
        });

        // Full Client Case record for a single case.
        app.MapGet("/team-leader/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });

        // Engage Theo on the selected case: the deterministic practice view
        // is always returned; with Foundry configured the live agent reviews
        // the same practice (streamed over SSE when the client asks for it).
        app.MapPost("/team-leader/process", async (HttpContext httpContext, TeamLeaderProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var focusCase = store.Get(request.CaseNumber);
            if (focusCase is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            logger.LogInformation("Team Leader process: caseNumber={CaseNumber}", Sanitize(focusCase.CaseNumber));

            var practice = BuildPractice(store.All());
            var focus = BuildFocus(focusCase);
            var prompt = BuildPrompt(focusCase, focus, practice);

            object BuildEnvelope(AgentTraceResult? trace, string? agentError) => new
            {
                caseNumber = focusCase.CaseNumber,
                clientName = focusCase.ClientName,
                requestType = focusCase.RequestType,
                practice,
                focus,
                agentNotes = trace?.Text,
                agentError,
                agentConfigured = factory.IsConfigured,
                agentInput = trace?.Input,
                agentRawOutput = trace is null ? null : new
                {
                    text = trace.Text,
                    citations = trace.Citations,
                    outputItems = trace.OutputItems,
                    responseId = trace.ResponseId,
                    durationMs = trace.DurationMs
                }
            };

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                CaldovaAgent? streamingAgent = null;
                string? createError = null;
                if (factory.IsConfigured)
                {
                    try
                    {
                        streamingAgent = factory.Create("team-leader");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Team Leader Agent could not be created; showing the deterministic practice view");
                        createError = ex.Message;
                    }
                }
                await AgentSseStreaming.StreamAsync(
                    httpContext,
                    streamingAgent,
                    prompt,
                    (trace, error) => BuildEnvelope(trace, error ?? createError),
                    logger,
                    "Team Leader Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? agentInvocationError = null;
            if (factory.IsConfigured)
            {
                try
                {
                    var agent = factory.Create("team-leader");
                    traceResult = await agent.RunWithTraceAsync(prompt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Team Leader Agent invocation failed; showing the deterministic practice view");
                    agentInvocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, agentInvocationError));
        });
    }

    // ── Practice model ────────────────────────────────────────────────────────
    private static PracticeView BuildPractice(IReadOnlyList<CaseRecord> cases)
    {
        var profiles = cases.Select(c => (Case: c, Profile: ProfileFor(c))).ToList();

        string[] stages = [Discover, Verify, Advise, Implement, Confirm];
        var hints = new Dictionary<string, string>
        {
            [Discover] = "fact find and in-good-order checklist with Olivia",
            [Verify] = "identity, documents and financial crime with Felix",
            [Advise] = "needs analysis, research and the SoA with Penny, Ivy and Amelia",
            [Implement] = "underwriting with Uma, or the claim with Ben",
            [Confirm] = "letters and updates with Cara",
        };
        var stageCounts = stages
            .Select(s => new StageCount(s, profiles.Count(p => p.Profile.Stage == s), hints[s]))
            .ToList();

        var pipeline = profiles
            .OrderBy(p => Array.IndexOf(stages, p.Profile.Stage))
            .ThenBy(p => p.Case.CaseNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PipelineCase(p.Case.CaseNumber, p.Case.ClientName, p.Case.RequestType, p.Profile.Side,
                p.Case.Priority, p.Profile.Stage, p.Profile.Owner, p.Profile.NextAction))
            .ToList();

        var clocks = profiles
            .SelectMany(p => p.Profile.Clocks)
            .OrderBy(c => StateRank(c.State))
            .ToList();

        var approvals = profiles
            .SelectMany(p => p.Profile.Approvals)
            .OrderBy(a => a.Priority)
            .ToList();

        // Escalations: most severe first, then in approval-queue order.
        var escalations = profiles
            .Where(p => p.Profile.Escalation is not null)
            .OrderBy(p => SeverityRank(p.Profile.Escalation!.Severity))
            .ThenBy(p => p.Profile.Approvals.Count > 0 ? p.Profile.Approvals.Min(a => a.Priority) : int.MaxValue)
            .Select(p => p.Profile.Escalation!)
            .ToList();

        var vulnerable = profiles
            .Where(p => p.Profile.Vulnerable is not null)
            .Select(p => p.Profile.Vulnerable!)
            .ToList();

        var audits = profiles
            .OrderBy(p => p.Case.CaseNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Profile.Audit)
            .ToList();

        var coaching = profiles
            .Where(p => p.Profile.Coaching is not null)
            .OrderBy(p => p.Case.CaseNumber, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.Profile.Coaching!)
            .ToList();
        coaching.Add("File quality: Theo audits every advice case with total cover of $2,000,000 or more, every claim and every financial-crime block, " +
                     "plus a sample of other files each month, against the best-interests safe-harbour steps (s961B(2)) and the in-good-order checklist.");

        // Today's priorities: the approval queue in order, then triage of
        // anything newly received.
        var priorities = profiles
            .Where(p => p.Profile.Today is not null)
            .OrderBy(p => p.Profile.Approvals.Count > 0 ? p.Profile.Approvals.Min(a => a.Priority) : int.MaxValue)
            .Select(p => $"{p.Case.CaseNumber} — {p.Profile.Today}")
            .ToList();
        var received = profiles.Count(p => p.Profile.Stage == Discover);
        if (received > 0)
            priorities.Add($"Triage the {received} newly received case{(received == 1 ? "" : "s")} with Olivia and route each one to the right specialist.");

        var asAt = $"{cases.Count} open Client Case{(cases.Count == 1 ? "" : "s")} · scenario cases shown at the point each one reached Theo's desk";

        return new PracticeView(asAt, stageCounts, pipeline, clocks, escalations, approvals, vulnerable, audits, coaching, priorities);
    }

    private static CaseFocus BuildFocus(CaseRecord c)
    {
        var p = ProfileFor(c);
        return new CaseFocus(
            c.CaseNumber, c.ClientName, c.RequestType, p.Side, p.Stage, p.Owner, p.AsAt,
            p.Summary, p.Decision, p.Recommendation, TeamLeaderGroupChatService.KeyFacts(c));
    }

    // Where each case sits in the practice. Scenario cases are shown at the
    // point they reach Theo's desk; cases opened live are freshly received.
    private static CaseProfile ProfileFor(CaseRecord c) => LifeScenarioCases.KeyOf(c) switch
    {
        LifeScenarioCases.Priya => Priya(c),
        LifeScenarioCases.Grace => Grace(c),
        LifeScenarioCases.George => George(c),
        LifeScenarioCases.Tom => Tom(c),
        LifeScenarioCases.Robert => Robert(c),
        _ => Received(c)
    };

    // 1. Priya — Corella's underwriting decision is in (Mon 16 Nov 2026).
    private static CaseProfile Priya(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        return new CaseProfile(
            Stage: Implement,
            Side: ProtectionAdvice,
            Owner: "Uma (Underwriting & New Business) → Amelia",
            AsAt: "Mon 16 Nov 2026 — Corella Life's underwriting decision received",
            NextAction: "Amelia explains Corella's decision; Priya decides whether to accept the income protection exclusion by Tue 17 Nov.",
            Clocks:
            [
                new("Exclusion decision", n, who,
                    "Accepting a loading, an exclusion or a decline at underwriting is the client's decision with the adviser",
                    "Due Tue 17 Nov 2026 — the policy would start Wed 18 Nov", "due"),
                new("Kestrel Retail Super cover", n, who,
                    $"Protecting Your Super (SIS Act s68AAA) — no cover after {LifeRules.PysInactivity} with no contributions unless the client elects in writing to keep it",
                    "Would have stopped Mon 30 Nov 2026 — Priya elected in writing to keep it until the new cover starts", "ok"),
                new("Corella Life underwriting", n, who,
                    "Corella service standard — underwriting decision within 5 business days of the last requirement (Directory — insurers on the approved product list)",
                    "Tele-interview Tue 10 Nov → decision Mon 16 Nov: 4 business days — met", "ok"),
                new("Cooling-off period", n, who,
                    $"{LifeRules.CoolingOff} from confirmation of the policy, or the end of the 5th business day after issue if earlier; not once a claim is made (Corporations Act s1019B)",
                    "Starts when Corella issues policy CR-26118204", "watch"),
            ],
            Escalation: new(n, who, "Watch",
                "Corella offered income protection with an exclusion for back and spine conditions; life, TPD and trauma are at standard rates.",
                "Uma → Amelia", "Priya decides by Tue 17 Nov"),
            Approvals:
            [
                new(10, n, who,
                    "Accept Corella's income protection exclusion for back and spine conditions (reviewed after two symptom-free years)",
                    "Priya, with Amelia",
                    "Accepting a loading, an exclusion or a decline at underwriting",
                    "Amelia explains it in plain English before anything is written down; life, TPD and trauma are unaffected",
                    "Tue 17 Nov 2026"),
            ],
            Vulnerable: null,
            Audit: new FileAudit(n, who, BestInterests, "Pass",
            [
                new("Objectives, financial situation and needs identified", "pass",
                    "s961B(2)(a) — fact find from Hannah's referral: income, $680,000 mortgage, dependants, savings, budget, existing cover and health."),
                new("Needs analysis shows the working", "pass",
                    "Life $755,000 → $750,000; TPD $600,000; trauma $150,000; IP $6,125 a month — each step on file."),
                new("Existing cover considered before replacing anything", "pass",
                    "Riverbend cover kept and counted; Kestrel cover kept until the new cover starts — cancelling it is Priya's decision."),
                new("Reasonable investigation of products", "pass",
                    "s961B(2)(e) — three APL insurers compared on definitions and stepped and level premiums."),
                new("Commission disclosed in dollars, with informed consent", "pass",
                    "$1,908 upfront and $636 a year from year 2 — within the LIF caps (60% / 20%); Priya's written consent on file (s963BB, cover issued after 9 Jul 2025)."),
                new("SoA signed by the adviser; Authority to Proceed", "pass",
                    "Amelia signed the SoA on 6 Nov; Priya signed the Authority to Proceed on 7 Nov."),
                new("Duty to take reasonable care explained", "pass",
                    "Insurance Contracts Act s20B — Uma explained it before the application was lodged on 9 Nov."),
            ]),
            Summary: "New family protection advice with Corella Life — life $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month. SoA signed 6 Nov; Corella's decision came in on 16 Nov.",
            Decision: "Priya decides, with Amelia, whether to accept the income protection exclusion.",
            Recommendation: "Make sure Amelia speaks with Priya before anything is written down. The exclusion only affects income protection, and the Kestrel cover stays until the new policy starts.",
            Today: "Make sure Amelia talks Priya through Corella's back exclusion before any letter goes — it's Priya's decision, due Tue 17 Nov.",
            Coaching: "Priya: Olivia caught the Kestrel Protecting Your Super deadline at the fact find — check every super account for inactivity deadlines before advice, so no family loses cover while new cover is underwritten.");
    }

    // 2. Grace — the block decision (Fri 6 Nov 2026).
    private static CaseProfile Grace(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        return new CaseProfile(
            Stage: Verify,
            Side: ClaimsAndCare,
            Owner: "Felix (Financial Crime) → Theo",
            AsAt: "Fri 6 Nov 2026 — Felix's risk assessment complete",
            NextAction: "Theo decides the block and the referral to Lyrebird Life's fraud team; Cara calls Grace on her verified mobile 0491 570 157.",
            Clocks:
            [
                new("Call Grace on her verified mobile", n, who,
                    "Reach the policy owner on contact details verified before any change, before anything else happens",
                    "Today — 6pm Melbourne is 8am in Florence (Grace is in Italy until 1 Dec)", "due"),
                new("Claim lodgement", n, who,
                    "A claim is lodged only after a person approves it",
                    "Not lodged — held for Theo's decision", "paused"),
            ],
            Escalation: new(n, who, "Urgent",
                "Suspected identity takeover: an \"urgent\" trauma claim for the full $200,000 with payment to a new account in another name, about 93 hours after a phone change of contact details; fake oncologist's report and tampered licence.",
                "Felix → Theo", "Decide today"),
            Approvals:
            [
                new(1, n, who,
                    "Block the trauma claim and the bank change; restore Grace's verified contact details and lock portal access",
                    Theo,
                    "Blocking a request on financial-crime grounds, and any change to bank details",
                    "Approve — no claim is lodged and nothing is paid; Grace's $500,000 life and $200,000 trauma cover stay untouched",
                    "Today, Fri 6 Nov 2026"),
                new(2, n, who,
                    "Alert Lyrebird Life's Claims Integrity (fraud) team",
                    Theo,
                    "Referring suspected fraud to an insurer",
                    "Approve — share the timeline and document findings, describing Grace as the likely victim",
                    "Today, Fri 6 Nov 2026"),
                new(3, n, who,
                    "Cara's call script and reassurance letter to Grace",
                    "Cara (Client Communications Specialist)",
                    "Any communication to a fraud victim — Cara approves her letters",
                    "Approve the call script now and the letter after the call; verified details only, and nothing that suggests Grace did anything wrong",
                    "Before Cara calls at 6pm"),
            ],
            Vulnerable: new(n, who,
                "Scam and identity-takeover victim, travelling overseas (Italy until 1 Dec)",
                "Contact only on her verified mobile 0491 570 157 and grace.williams@example.com; every message approved by Theo; never imply she did anything wrong"),
            Audit: new FileAudit(n, who, ClaimsHandling + " · financial crime controls", "Action needed",
            [
                new("Claim checked against the policy definition", "pass",
                    "Lyrebird Life pays cancer claims on a confirmed specialist diagnosis with histopathology — none supplied."),
                new("Benefits paid to the policy owner only", "pass",
                    "Bank change to an account in another name rejected."),
                new("Documents checked for tampering", "pass",
                    "Oncologist's report fake; licence photo edited; bank account opened 3 Nov in another name."),
                new("Change-of-contact-details control", "warn",
                    "The phone change on 2 Nov passed basic checks with no code sent to the old mobile — a control gap to close."),
                new("Client treated as the likely victim", "pass",
                    "No accusatory language in the case notes or in Cara's drafts."),
            ]),
            Summary: "An \"urgent\" trauma claim for the full $200,000 on Grace's Lyrebird Life policy, with payment to a new account in another name — financial-crime risk HIGH. Grace is in Italy.",
            Decision: "Approve the block, the restored contact details and portal lock, and the referral to Lyrebird Life's fraud team.",
            Recommendation: "Approve now — no claim has been lodged and nothing has been paid. Then let Cara call Grace on 0491 570 157 with the approved script.",
            Today: "Approve Grace's block and the referral to Lyrebird Life's fraud team first, then Cara's call script — she calls Grace at 6pm (8am in Florence).",
            Coaching: "Grace: a phone change of contact details passed on basic checks alone (name, date of birth and address). The standard is a code to, or a call-back on, the verified mobile or email held before the change — and any claim, payment or bank-detail change within 30 days of a contact change is held for a call-back on that verified number.");
    }

    // 3. George — Theo's second review (total cover $2,800,000).
    private static CaseProfile George(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        return new CaseProfile(
            Stage: Advise,
            Side: ProtectionAdvice,
            Owner: "Penny (Paraplanning) → Theo",
            AsAt: "Advice ready — waiting on the second review before Amelia presents the SoA",
            NextAction: "Theo's second review of the $2,800,000 business protection advice; then Amelia reviews and signs the SoA.",
            Clocks:
            [
                new("Second review", n, who,
                    "Advice with total cover of $2,000,000 or more on one case gets a second review before the SoA is presented",
                    "Due now — $2,800,000 on the case", "due"),
                new("Northbank Life underwriting", n, who,
                    "Caldova lodges within 2 business days of the Authority to Proceed; Northbank's fast financial underwriting decides within 5 business days, and medical underwriting within 5 business days of the last requirement",
                    "Not started — George: blood profile, medical examination and financial evidence; Harpreet: blood profile and financial evidence", "paused"),
            ],
            Escalation: new(n, who, "Watch",
                "Total cover of $2,800,000 (buy-sell $1,200,000 each plus $400,000 key person) — second review required before the SoA.",
                "Penny → Theo", "Before the SoA is presented"),
            Approvals:
            [
                new(8, n, who,
                    "Second review — buy-sell life and TPD of $1,200,000 each for George and Harpreet, plus $400,000 key person cover on George (total $2,800,000)",
                    Theo,
                    "Advice with total cover of $2,000,000 or more on one case",
                    "Approve — sums tie to Marcus's $2,400,000 valuation and $400,000 profit estimate; ownership matches each purpose; three APL insurers compared",
                    "Before the SoA is presented"),
                new(9, n, who,
                    "Statement of Advice — Northbank Life, $8,620 a year at standard rates",
                    Amelia,
                    "Every Statement of Advice is signed by the adviser",
                    "Sign after the second review; the buy-sell agreement stays with their lawyer",
                    "After Theo's review"),
            ],
            Vulnerable: null,
            Audit: new FileAudit(n, who, BestInterests, "In progress",
            [
                new("Business objectives and needs identified", "pass",
                    "No succession plan; valuation $2,400,000 at 30 Jun 2026 and profit at risk of about $400,000 over 18 months, both from Marcus."),
                new("Ownership matches each purpose", "pass",
                    "Buy-sell self-owned by each partner; key person owned by the company for a revenue purpose."),
                new("Legal documents left to the lawyer", "pass",
                    "Their lawyer drafts the buy-sell agreement — Caldova doesn't draft legal documents."),
                new("Tax treatment confirmed by the accountant", "pass",
                    "Marcus confirmed the treatment as their accountant."),
                new("Reasonable investigation of products", "pass",
                    "s961B(2)(e) — Northbank, Corella and Harbourline compared at standard rates."),
                new("Second review ($2,000,000 or more)", "warn",
                    "Waiting on Theo — $2,800,000 of total cover."),
                new("Commission disclosed in dollars, with informed consent", "warn",
                    "To be disclosed in the SoA and consented to in writing by George and Harpreet (s963BB — cover issued after 9 Jul 2025)."),
                new("SoA signed by the adviser", "warn",
                    "Amelia signs after the second review."),
            ]),
            Summary: "Business protection for Northcote Custom Joinery: buy-sell life and TPD of $1,200,000 each for the two 50% owners and $400,000 key person cover on George, with Northbank Life ($8,620 a year at standard rates).",
            Decision: "Second review of advice with total cover of $2,800,000 — approve before Amelia presents and signs the SoA.",
            Recommendation: "Approve — the sums are anchored to the accountant's valuation and profit estimate, the ownership matches each purpose and the lawyer owns the buy-sell agreement.",
            Today: "Complete the second review of George and Harpreet's $2,800,000 business protection advice so Amelia can present the SoA.",
            Coaching: "George: the referral pack from Marcus — valuation, profit at risk and the accountant's tax view — is the model for business protection. Ask referral partners for it up front.");
    }

    // 4. Tom — the lodgement decision (Wed 11 Nov 2026).
    private static CaseProfile Tom(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        return new CaseProfile(
            Stage: Implement,
            Side: ClaimsAndCare,
            Owner: "Ben (Claims) → Theo",
            AsAt: "Wed 11 Nov 2026 — claim pack ready to lodge",
            NextAction: "Theo's vulnerable-client review and approval to lodge with Harbourline Life on Thu 12 Nov; Amelia calls Tom first.",
            Clocks:
            [
                new("Claim lodgement", n, who,
                    "Lodging any claim needs a person's approval (Teams); approved claims are lodged the same or next business day (Caldova service standard)",
                    "Lodge Thu 12 Nov 2026", "due"),
                new("Waiting period", n, who,
                    "30-day waiting period from the injury on 14 Oct 2026",
                    "Ends 13 Nov 2026 — the benefit accrues from Fri 13 Nov", "ok"),
                new("Harbourline's claim decision", n, who,
                    LifeRules.CodeIncomeClaimDecision,
                    "Harbourline's own standard: usually within 5 business days of having all information. Life Code backstop: by Wed 13 Jan 2027 (2 months from the end of the waiting period); " +
                    LifeRules.CodeWrittenDecision, "watch"),
                new("First benefit payment", n, who,
                    "Paid monthly in arrears",
                    "About 13 Dec 2026 — $5,600", "watch"),
                new("Complaint (IDR) clock", n, who,
                    $"Written response within {LifeRules.IdrResponse} (ASIC RG 271; Life Code cl 7.13), always in writing for a complaint about a claim's value (RG 271.75); then AFCA",
                    "Not running — starts only if Tom complains about the benefit amount", "watch"),
            ],
            Escalation: new(n, who, "High",
                "Vulnerable client (injury and financial stress); the benefit will be $5,600 a month — $1,400 less than the $7,000 he expects.",
                "Ben → Theo", "Lodge Thu 12 Nov"),
            Approvals:
            [
                new(4, n, who,
                    "Lodge the income protection claim with Harbourline Life — expected benefit $5,600 a month (not $7,000)",
                    Theo + " in Teams",
                    "Lodging any claim",
                    "Approve — evidence verified by Felix; the benefit is the lesser of $7,000 and 70% of his average monthly earnings in the 12 months before the injury",
                    "Thu 12 Nov 2026"),
                new(5, n, who,
                    "Amelia calls Tom first; then Cara's letter explaining $5,600 against $7,000",
                    "Theo (Amelia's call) · Cara (the letter)",
                    "Vulnerable-client review; any communication to a seriously ill or injured client — Cara approves her letters",
                    "Ask Amelia to call Tom (and Kylie) before anything is written; Cara approves the plain-English letter with his right to complain once Amelia has called",
                    "Before the letter goes"),
            ],
            Vulnerable: new(n, who,
                "Injury (shoulder surgery 21 Oct) and financial stress — self-employed, with a young family",
                "Kylie is his support person under his written authority (insurers must recognise support persons — Life Code cl 6.13); Amelia calls first; plain English; every message approved by Theo"),
            Audit: new FileAudit(n, who, ClaimsHandling, "Pass — note for the adviser",
            [
                new("Policy and benefit basis checked", "pass",
                    "Lesser of $7,000 insured and 70% of average monthly earnings in the 12 months before disability."),
                new("Evidence verified", "pass",
                    "BAS, profit and loss and the surgeon's report legit (Felix)."),
                new("Support person authority on file", "pass",
                    "Kylie, with Tom's written authority."),
                new("Vulnerable-client handling", "pass",
                    "Amelia calls first; Cara's letter is approved before it goes."),
                new("Insured benefit against earnings", "warn",
                    "$7,000 insured against $5,600 supported — a note for Amelia's next review, not advice."),
            ]),
            Summary: "Income protection claim on Harbourline Life HL-23051877 after a shoulder injury: $7,000 a month insured, but the benefit follows 70% of his earnings in the 12 months before the injury — $5,600 a month.",
            Decision: "Approve lodging the claim, and ask Amelia to call Tom personally before the letter.",
            Recommendation: "Approve today so Ben lodges on Thursday; the $5,600 should come from Amelia in a call before it arrives in writing.",
            Today: "Review Tom's vulnerable-client brief, approve the lodgement in Teams for Thu 12 Nov and ask Amelia to call him before Cara's letter.",
            Coaching: "Tom: his insured benefit was set from $120,000 a year at application, but recent earnings support $5,600 a month. Prompt reviews for self-employed clients when income changes — whether to change cover is always the adviser's call.");
    }

    // 5. Robert — the lodgement decision (Mon 16 Nov 2026).
    private static CaseProfile Robert(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        return new CaseProfile(
            Stage: Implement,
            Side: ClaimsAndCare,
            Owner: "Ben (Claims) → Theo (compassionate oversight)",
            AsAt: "Mon 16 Nov 2026 — claim pack ready to lodge",
            NextAction: "Theo approves lodging Mei's death claim with Corella Life today and every message to the family.",
            Clocks:
            [
                new("Claim lodgement", n, who,
                    "Every death or terminal illness claim needs a person's approval (Teams); approved claims are lodged the same or next business day (Caldova service standard)",
                    "Lodge today, Mon 16 Nov 2026", "due"),
                new("Corella's claim decision", n, who,
                    LifeRules.CodeLumpSumClaimDecision,
                    "Corella's own standard: a dedicated case manager, a death claim decision usually within 5 business days of having all information and payment within 3 business days of acceptance. " +
                    "Life Code backstop: by 16 May 2027; " + LifeRules.CodeWrittenDecision, "watch"),
                new("Updates to the family", n, who,
                    "At least every 10 business days while anything is open, from one named contact (Caldova service standard)",
                    "Amelia is the contact; Theo approves every message", "watch"),
                new("Riverbend Super claim ($180,000)", n, who,
                    "A separate claim with the fund's trustee, paid under the fund's rules",
                    "Ben helps Robert with the fund's claim form and checklist", "watch"),
            ],
            Escalation: new(n, who, "High",
                "Bereavement — Mei died suddenly on 12 Oct; the death claim and every message to the family need Theo's approval.",
                "Theo (case owner)", "Lodge today"),
            Approvals:
            [
                new(6, n, who,
                    "Lodge the death claim — Mei Chen's Corella Life policy CR-19033105, life cover $1,000,000, to Robert as nominated beneficiary",
                    Theo + " in Teams",
                    "Every death or terminal illness claim",
                    "Approve — in force since March 2019, premiums paid, no exclusions, nomination valid; paid directly to Robert, not through the estate",
                    "Mon 16 Nov 2026"),
                new(7, n, who,
                    "Every message to the Chen family",
                    Theo,
                    "Any communication to a bereaved family",
                    "Approve each one — Amelia is the family's one named contact and calls before anything is written",
                    "Each message"),
            ],
            Vulnerable: new(n, who,
                "Bereavement — his wife Mei died suddenly on 12 Oct 2026; he is caring for Lily (12) and Max (9)",
                "One named contact (Amelia); Theo approves every message; no jargon; nothing for the family to chase"),
            Audit: new FileAudit(n, who, ClaimsHandling, "Pass",
            [
                new("Policy in force, premiums paid, no exclusions", "pass",
                    "Corella Life CR-19033105, in force since March 2019."),
                new("Beneficiary nomination valid", "pass",
                    "Robert 100% — paid directly to him, not through Mei's estate."),
                new("Non-disclosure window", "pass",
                    $"In force for more than seven years — past the {LifeRules.MisrepresentationWindow} window for avoiding cover over a non-fraudulent misrepresentation (Insurance Contracts Act s29(3))."),
                new("Other cover found", "pass",
                    "Riverbend Super default death cover of $180,000 — a separate claim with the fund's trustee."),
                new("Advice after payment", "pass",
                    "Corella must suggest independent financial advice for an accepted lump sum of $25,000 or more (Life Code cl 5.61); Amelia offers a review only when Robert is ready."),
                new("Compassionate handling", "pass",
                    "One named contact; every message approved by Theo."),
            ]),
            Summary: "Death claim on Mei Chen's Corella Life policy — life cover of $1,000,000 to Robert as nominated beneficiary — plus $180,000 of default death cover in her Riverbend Super account.",
            Decision: "Approve lodging the death claim, and every message to the family.",
            Recommendation: "Approve now so Ben lodges today; Amelia calls Robert before any letter, and Ben starts the Riverbend Super checklist with him.",
            Today: "Approve the Chen death claim in Teams so Ben lodges with Corella Life today, and keep Amelia as the family's one contact.",
            Coaching: "Robert: Ben found Mei's $180,000 of super cover the family didn't know about. Make the search for cover inside super a standard step on every death claim.");
    }

    // Cases opened live with the Onboarding Agent — freshly received.
    private static CaseProfile Received(CaseRecord c)
    {
        var n = c.CaseNumber;
        var who = c.ClientName;
        var t = (c.RequestType ?? string.Empty).ToLowerInvariant();
        var isDeath = t.Contains("death");
        var isClaim = t.Contains("claim");
        var bankChange = t.Contains("bank");
        var side = isClaim ? ClaimsAndCare : ProtectionAdvice;

        var (next, clock, expected) =
            isDeath
                ? ("Financial Crime (documents), then Claims",
                   new PracticeClock("Claim lodgement", n, who,
                       "Every death or terminal illness claim needs a person's approval",
                       "After the policy and document checks", "watch"),
                   "the claim lodgement and every message to the family")
            : isClaim
                ? (bankChange ? "Financial Crime (bank change and documents), then Claims" : "Claims, then Financial Crime (documents)",
                   new PracticeClock("Claim lodgement", n, who,
                       "Lodging any claim needs a person's approval",
                       bankChange ? "Held until the bank change is verified" : "After the policy and evidence check", bankChange ? "paused" : "watch"),
                   bankChange ? "the claim lodgement and the bank-detail change" : "the claim lodgement")
            : t.Contains("business")
                ? ("Financial Crime (KYC on the company and owners), then Paraplanning",
                   new PracticeClock("Second review", n, who,
                       "Advice with total cover of $2,000,000 or more on one case gets a second review",
                       "Checked once the needs analysis is done", "watch"),
                   "the SoA (and a second review if total cover is $2,000,000 or more)")
                : ("Financial Crime (KYC), then Paraplanning",
                   new PracticeClock("Statement of Advice", n, who,
                       "Every SoA is signed by the adviser",
                       "After the needs analysis and research", "watch"),
                   "the SoA, and any replacement of existing cover");

        var priority = c.Priority ?? string.Empty;
        Escalation? escalation =
            priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase) ? new(n, who, "Urgent", c.PriorityReason, "Olivia → " + next, "Triage now")
            : priority.Equals("High", StringComparison.OrdinalIgnoreCase) ? new(n, who, "High", c.PriorityReason, "Olivia → " + next, "Triage today")
            : null;

        VulnerableClient? vulnerable =
            isDeath ? new(n, who, "Bereavement", "Compassionate handling; one named contact; every message approved by Theo")
            : bankChange ? new(n, who, "Possible scam target (claim with a bank change)", "Contact only on details verified before any recent change; bank-detail changes need Theo's approval")
            : isClaim ? new(n, who, "Illness or injury", "Plain English; Cara approves every letter; check for financial stress")
            : null;

        var acknowledgement = new PracticeClock("Acknowledgement", n, who,
            "Requests acknowledged, and anything not in good order explained, within 1 business day (Caldova service standard)",
            $"Received {PracticeDates.Display(c.ReceivedDate)} — Cara's acknowledgement is drafted", "due");

        return new CaseProfile(
            Stage: Discover,
            Side: side,
            Owner: "Olivia (Onboarding & Requests)",
            AsAt: $"Received {PracticeDates.Display(c.ReceivedDate)}",
            NextAction: $"Finish the in-good-order checklist and route to {next}.",
            Clocks: [acknowledgement, clock],
            Escalation: escalation,
            Approvals: [],
            Vulnerable: vulnerable,
            Audit: new FileAudit(n, who, isClaim ? ClaimsHandling : BestInterests, "Not yet audited",
            [
                new("File audit", "n/a", isClaim
                    ? "Audited once the claim pack is ready to lodge."
                    : "Audited once the SoA is drafted."),
            ]),
            Summary: c.RequestSummary,
            Decision: $"No decision yet — a person approves {expected} later.",
            Recommendation: $"Route to {next}; Cara sends the acknowledgement today.");
    }

    private static int StateRank(string state) => state switch
    {
        "due" => 0,
        "running" => 1,
        "paused" => 2,
        "watch" => 3,
        _ => 4
    };

    private static int SeverityRank(string severity) => severity switch
    {
        "Urgent" => 0,
        "High" => 1,
        "Watch" => 2,
        _ => 3
    };

    // ── Prompt for the live Team Leader Agent ────────────────────────────────
    private static string BuildPrompt(CaseRecord c, CaseFocus focus, PracticeView practice)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TEAM LEADER PRACTICE REVIEW — CALDOVA LIFE ADVICE");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Focus case     : {c.CaseNumber}");
        sb.AppendLine($"Client         : {c.ClientName} ({c.ClientNumber})");
        sb.AppendLine($"Cover          : {c.Cover}");
        sb.AppendLine($"Request type   : {c.RequestType}");
        sb.AppendLine($"Received       : {c.ReceivedDate}");
        sb.AppendLine($"Adviser        : {c.AdviserName}");
        sb.AppendLine($"Referred by    : {(string.IsNullOrWhiteSpace(c.ReferredBy) ? "Existing client" : c.ReferredBy)}");
        sb.AppendLine($"Amount         : {c.Amount}");
        sb.AppendLine($"Priority       : {c.Priority} — {c.PriorityReason}");
        sb.AppendLine($"Side / stage   : {focus.Side} / {focus.Stage} — {focus.AsAt}");
        sb.AppendLine($"Owner          : {focus.Owner}");
        sb.AppendLine($"Request        : {c.RequestSummary}");
        sb.AppendLine();
        sb.AppendLine("KEY FACTS ON FILE (focus case)");
        foreach (var fact in focus.KeyFacts) sb.AppendLine($"- {fact}");
        sb.AppendLine();
        sb.AppendLine($"PRACTICE SNAPSHOT — {practice.AsAt}");
        sb.AppendLine("Pipeline by stage: " + string.Join(", ", practice.StageCounts.Select(s => $"{s.Stage} {s.Count}")));
        foreach (var pc in practice.Cases)
            sb.AppendLine($"- {pc.CaseNumber} · {pc.ClientName} · {pc.RequestType} · {pc.Side} · {pc.Priority} · {pc.Stage} · owner {pc.Owner} · next: {pc.NextAction}");
        sb.AppendLine();
        sb.AppendLine("CLOCKS");
        foreach (var clock in practice.Clocks)
            sb.AppendLine($"- [{clock.State}] {clock.Clock} — {clock.CaseNumber} ({clock.ClientName}): {clock.Status}. Rule: {clock.Rule}");
        sb.AppendLine();
        sb.AppendLine("ITEMS WAITING FOR A HUMAN DECISION");
        foreach (var a in practice.Approvals)
            sb.AppendLine($"- {a.CaseNumber} ({a.ClientName}): {a.Item} — approver: {a.Approver}; why a person: {a.Authority}; due {a.Due}");
        sb.AppendLine();
        sb.AppendLine("VULNERABLE CLIENTS");
        foreach (var v in practice.Vulnerable)
            sb.AppendLine($"- {v.CaseNumber} ({v.ClientName}): {v.Flag}");
        sb.AppendLine();
        sb.AppendLine("FILE AUDITS");
        foreach (var audit in practice.FileAudits)
        {
            var open = audit.Checks.Where(k => k.Status == "warn").Select(k => k.Label).ToList();
            sb.AppendLine($"- {audit.CaseNumber} ({audit.ClientName}) · {audit.Standard}: {audit.Result}" +
                          (open.Count > 0 ? $" — open: {string.Join("; ", open)}" : string.Empty));
        }
        sb.AppendLine();
        sb.AppendLine("TASK");
        sb.AppendLine("----");
        sb.AppendLine("Review the practice as Theo's Team Leader Agent and produce your structured output: Practice Summary, " +
                      "Clocks & Escalations, Approval Queue, Vulnerable Clients, File Quality & Coaching and Recommended " +
                      "Priorities for Today. Lead with the focus case where it matters, state each clock's status, and flag " +
                      "vulnerable clients for a human touch. Use only the facts above, cite the rule or document behind each " +
                      "approval, recommend — never decide — and never give personal advice.");
        return sb.ToString();
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');
}
