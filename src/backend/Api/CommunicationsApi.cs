using System.Text;
using System.Text.RegularExpressions;
using CaldovaLife.Agents;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

record CommunicationsProcessRequest(string CaseNumber);

record CommunicationsVoiceRequest(string Message, string? CaseNumber, string? PreviousResponseId);

/// <summary>
/// HTTP endpoints behind the "Try it out" and "Voice chat" tabs on the Client
/// Communications agent page (<c>/agents/client-communications</c>).
///
/// The flow:
///
/// 1. List the Client Cases held in <see cref="CaseStore"/> (the five seeded
///    scenario cases plus any opened live with the Onboarding Agent) so the
///    user can pick one.
/// 2. Engage the Client Communications Agent (Cara) on the selected case. The
///    page always receives Cara's deterministic case summary and drafts from
///    <see cref="ClientCommunicationsDrafter"/> — client letters, call scripts,
///    referral-partner updates (only what the client consented to share) and
///    an SMS, each with tone and compliance notes — and, when Foundry is
///    configured, the live agent's streamed narrative as well.
/// 3. A typed "talk to Cara" endpoint used by the Voice chat tab whenever the
///    real-time Voice Live connection isn't available: the live voice agent
///    answers when Foundry is configured, otherwise a deterministic,
///    case-aware spoken-style reply is returned.
/// </summary>
public static class CommunicationsApi
{
    public static void MapCommunicationsEndpoints(
        this WebApplication app,
        CaseStore store,
        CaldovaAgentFactory factory,
        ILogger logger)
    {
        // Lightweight list for the case pickers (Try it out + Voice chat),
        // most recent first.
        app.MapGet("/communications/cases", () =>
        {
            var cases = store.All().Select(c => new
            {
                caseNumber = c.CaseNumber,
                clientName = c.ClientName,
                requestType = c.RequestType,
                clientNumber = c.ClientNumber,
                adviserName = c.AdviserName,
                referredBy = c.ReferredBy,
                priority = c.Priority,
                createdAt = c.CreatedAt
            });
            return Results.Ok(cases);
        });

        // Full Client Case record for a single case.
        app.MapGet("/communications/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });

        // Engage Cara on the selected case. Deterministic drafts are always
        // produced server-side so the page completes end-to-end offline; when
        // Foundry is configured the live agent is invoked too and its
        // narrative is returned in `agentNotes` (streamed as SSE `delta`
        // events when the client sends `Accept: text/event-stream`).
        app.MapPost("/communications/process", async (HttpContext httpContext, CommunicationsProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            logger.LogInformation("Communications process: caseNumber={CaseNumber}", Sanitize(record.CaseNumber));

            var bundle = ClientCommunicationsDrafter.Build(record);
            var prompt = BuildDraftingPrompt(record, bundle);

            object BuildEnvelope(AgentTraceResult? trace, string? agentError) => new
            {
                caseNumber = record.CaseNumber,
                clientName = record.ClientName,
                requestType = record.RequestType,
                clientNumber = record.ClientNumber,
                priority = record.Priority,
                adviserName = record.AdviserName,
                referredBy = record.ReferredBy,
                stage = bundle.Stage,
                outcome = bundle.Outcome,
                summary = bundle.Summary,
                vulnerabilityFlags = bundle.VulnerabilityFlags,
                humanApprovalRequired = bundle.HumanApprovalRequired,
                humanApprovalReason = bundle.HumanApprovalReason,
                approver = bundle.Approver,
                drafts = bundle.Drafts,
                partnerNote = bundle.PartnerNote,
                adviserNote = bundle.AdviserNote,
                sources = bundle.Sources,
                agentNotes = trace?.Text,
                agentConfigured = factory.IsConfigured,
                agentError,
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
                        streamingAgent = factory.Create("communications");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Client Communications Agent could not be created; using deterministic drafts");
                        createError = ex.Message;
                    }
                }
                await AgentSseStreaming.StreamAsync(
                    httpContext,
                    streamingAgent,
                    prompt,
                    (trace, error) => BuildEnvelope(trace, error ?? createError),
                    logger,
                    "Client Communications Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? agentInvocationError = null;
            if (factory.IsConfigured)
            {
                try
                {
                    var agent = factory.Create("communications");
                    traceResult = await agent.RunWithTraceAsync(prompt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Client Communications Agent invocation failed; falling back to deterministic drafts");
                    agentInvocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, agentInvocationError));
        });

        // Typed "talk to Cara" endpoint behind the Voice chat tab. Used when
        // the real-time Voice Live call isn't connected (or isn't configured):
        // the browser sends the caller's words, gets a short spoken-style
        // reply, and can read it aloud with the Web Speech API. With Foundry
        // configured the conversational voice agent answers (chaining turns
        // via previousResponseId); otherwise a deterministic, case-aware reply
        // keeps the tab working offline.
        app.MapPost("/communications/voice", async (CommunicationsVoiceRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "message is required" });

            CaseRecord? record = string.IsNullOrWhiteSpace(request.CaseNumber)
                ? null
                : store.Get(request.CaseNumber);

            logger.LogInformation(
                "Communications voice: caseNumber={CaseNumber}, chained={Chained}",
                Sanitize(request.CaseNumber ?? "(none)"),
                !string.IsNullOrWhiteSpace(request.PreviousResponseId));

            string reply;
            string? responseId = null;
            var agentUsed = false;

            if (factory.IsConfigured)
            {
                try
                {
                    var agent = factory.Create("communications-voice");
                    AgentTraceResult result;
                    if (!string.IsNullOrWhiteSpace(request.PreviousResponseId))
                    {
                        try
                        {
                            result = await agent.ChatWithTraceAsync(request.PreviousResponseId!, request.Message);
                        }
                        catch (Exception ex)
                        {
                            logger.LogInformation(ex, "Voice chat chaining failed; starting a fresh turn");
                            result = await agent.RunWithTraceAsync(BuildVoicePrompt(record, request.Message));
                        }
                    }
                    else
                    {
                        result = await agent.RunWithTraceAsync(BuildVoicePrompt(record, request.Message));
                    }

                    reply = StripMarkdown(result.Text);
                    responseId = result.ResponseId;
                    agentUsed = !string.IsNullOrWhiteSpace(reply);
                    if (!agentUsed) reply = FallbackVoiceReply(request.Message, record);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Client Communications voice agent invocation failed; using the scripted reply");
                    reply = FallbackVoiceReply(request.Message, record);
                }
            }
            else
            {
                reply = FallbackVoiceReply(request.Message, record);
            }

            return Results.Ok(new
            {
                reply,
                responseId,
                agentConfigured = factory.IsConfigured,
                agentUsed
            });
        });
    }

    // ── Prompts ───────────────────────────────────────────────────────────────
    private static string BuildDraftingPrompt(CaseRecord r, ClientCommunicationsDrafter.DraftBundle bundle)
    {
        var scenario = LifeScenarioCases.KeyOf(r);
        var type = (r.RequestType ?? string.Empty).ToLowerInvariant();
        var hasPartner = ClientCommunicationsDrafter.HasReferralPartner(r);
        var sb = new StringBuilder();

        sb.AppendLine("CLIENT CASE FOR CLIENT COMMUNICATIONS");
        sb.AppendLine("=====================================");
        sb.AppendLine($"Case number    : {r.CaseNumber}");
        sb.AppendLine($"Client         : {r.ClientName}");
        sb.AppendLine($"Email / phone  : {r.ClientEmail} / {r.ClientPhone}");
        sb.AppendLine($"Preferred      : {r.PreferredContact}");
        sb.AppendLine($"Client number  : {r.ClientNumber}");
        sb.AppendLine($"Cover          : {r.Cover}");
        sb.AppendLine($"Request type   : {r.RequestType}");
        sb.AppendLine($"Received       : {r.ReceivedDate}");
        sb.AppendLine($"Adviser        : {r.AdviserName}");
        sb.AppendLine($"Referred by    : {(hasPartner ? r.ReferredBy : "Existing client — no referral partner")}");
        sb.AppendLine($"Amount         : {r.Amount}");
        sb.AppendLine($"Priority       : {r.Priority} — {r.PriorityReason}");
        sb.AppendLine($"Request summary: {r.RequestSummary}");
        sb.AppendLine();
        sb.AppendLine("CASE FILE NOTES (what has happened so far — use only these facts)");
        sb.AppendLine("-----------------------------------------------------------------");
        sb.AppendLine($"Stage          : {bundle.Stage}");
        sb.AppendLine($"Outcome        : {bundle.Outcome}");
        sb.AppendLine($"What happened  : {bundle.Summary.WhatHappened}");
        sb.AppendLine("What's next    :");
        foreach (var step in bundle.Summary.WhatsNext) sb.AppendLine($"- {step}");
        if (bundle.VulnerabilityFlags.Count > 0)
        {
            sb.AppendLine("Vulnerability  :");
            foreach (var flag in bundle.VulnerabilityFlags) sb.AppendLine($"- {flag}");
        }
        sb.AppendLine("Documents      : " + string.Join("; ", bundle.Sources));
        sb.AppendLine();
        sb.AppendLine("TASK");
        sb.AppendLine("----");
        sb.AppendLine("Write the communications for this case as Cara, for review before anything is sent. Start with a 3–5 line case " +
                      "summary: the client's likely sentiment, what happened and what happens next. Then:");
        switch (scenario)
        {
            case LifeScenarioCases.Priya:
                sb.AppendLine("1. A welcome letter from Amelia to Priya: cover, policy number, premiums in dollars (inside and outside super), " +
                              "the Kestrel and Riverbend cover, the cooling-off period, how to claim and complaint rights.");
                sb.AppendLine("2. A plain-English policy summary that explains the income protection exclusion for back and spine conditions — what it " +
                              "is, why, that life, TPD and trauma are unaffected, and the review after two symptom-free years.");
                sb.AppendLine("3. A short update to Hannah Lewis (Yarra Home Loans): cover is in place, nothing more. Priya consented to share the " +
                              "outcome only — no sums insured, premiums or health details.");
                sb.AppendLine("4. An SMS (under 160 characters, nothing sensitive).");
                break;
            case LifeScenarioCases.Grace:
                sb.AppendLine("1. A call script for Cara to call Grace on her verified mobile 0491 570 157 only (never 0491 570 159 or " +
                              "grace.w.claims@example.net): reassure first, explain simply, confirm she didn't make the claim, what we've done, " +
                              "identity-protection steps.");
                sb.AppendLine("2. A reassurance letter to grace.williams@example.com: her cover is safe and nothing was paid, what happened, what we've " +
                              "done, identity-protection steps (including IDCARE), no links and no code requests.");
                sb.AppendLine("3. An SMS to the verified mobile only. Never imply Grace did anything wrong, and don't share investigation detail.");
                break;
            case LifeScenarioCases.George:
                sb.AppendLine("1. A letter from Amelia to George: his buy-sell and the company's key person policies, premiums, the 25% loading " +
                              "explained kindly (it changes the price, not the cover; Northbank reviews it in two years), the lawyer finalising the " +
                              "buy-sell agreement, tax questions to Marcus, cooling-off and complaint rights.");
                sb.AppendLine("2. A separate letter to Harpreet about her own policy — never mention George's health or loading.");
                sb.AppendLine("3. An update to Marcus Tan (Bayside Business Accountants), with consent: cover in place from 20 Nov 2026 and the " +
                              "lawyer finalising the buy-sell agreement. No health or underwriting details.");
                sb.AppendLine("4. An SMS to George.");
                break;
            case LifeScenarioCases.Tom:
                sb.AppendLine("1. Talking points for Amelia to call Tom first (Kylie may join as his support person).");
                sb.AppendLine("2. A kind letter explaining $5,600 a month against the $7,000 insured, with the working (70% of $8,000), the dates " +
                              "(accrues 13 Nov, first payment about 13 Dec), that payments are taxable income, free financial counselling and his " +
                              "right to complain to Harbourline Life and then AFCA.");
                sb.AppendLine("3. An SMS sent only after Amelia's call. Do not suggest changing his cover — that is Amelia's decision at his next review.");
                break;
            case LifeScenarioCases.Robert:
                sb.AppendLine("1. Gentle talking points for Amelia to call Robert first.");
                sb.AppendLine("2. A gentle condolence-toned letter from Amelia: $1,000,000 paid to Robert on 23 Nov 2026 as Mei's nominated beneficiary " +
                              "(not through the estate, not taxable), and a simple checklist for the separate $180,000 claim with Riverbend Super, " +
                              "which the trustee pays under the fund's rules. One named contact: Amelia.");
                sb.AppendLine("3. A gentle SMS sent only after Amelia's call. No product or review conversation.");
                break;
            default:
                sb.AppendLine("This case has just been received — acknowledge it, explain what happens next and don't pre-empt any decision.");
                sb.AppendLine("1. An acknowledgement email to the client.");
                if (hasPartner && !type.Contains("claim"))
                    sb.AppendLine($"2. A short thank-you to the referral partner ({r.ReferredBy}) — receipt of the referral only, with the client's consent.");
                sb.AppendLine("3. An SMS (under 160 characters, nothing sensitive).");
                if (type.Contains("death"))
                    sb.AppendLine("Bereavement: condolences first, plain English and one named contact (Amelia).");
                else if (type.Contains("bank"))
                    sb.AppendLine("A claim with a bank-detail change: write only to the contact details verified before any recent change, invite the " +
                                  "client to call if they didn't make the request, and never confirm the new bank details.");
                else if (type.Contains("claim"))
                    sb.AppendLine("A claim: be kind and practical, explain what the insurer may need and that the claim is lodged only after approval.");
                break;
        }
        sb.AppendLine();
        sb.AppendLine("For each draft give Channel & Audience, Draft Message, Tone & Compliance Check and Human Approval Required.");
        sb.AppendLine($"Compliance must cover: no personal advice (Amelia gives the advice); facts that match the case; next steps and dates; " +
                      $"contact details (Caldova Life Advice {PracticeContacts.Phone}); complaint rights where relevant — Caldova (or the insurer, for a " +
                      $"claim decision) first, a written response within {LifeRules.IdrResponse}, then AFCA ({LifeRules.AfcaContact}); and health " +
                      "information shared only with the people who need it.");
        sb.AppendLine("Use only the facts above — never invent amounts, dates or rules. Mask TFNs and bank accounts. Cite documents by their exact titles.");
        return sb.ToString();
    }

    private static string BuildVoicePrompt(CaseRecord? record, string message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Cara from Caldova Life Advice, speaking with a caller on a voice call. Reply in two to four short, warm, " +
                      "plain-English sentences — no markdown, lists or headings. Never invent case facts; if you don't know, say you'll check " +
                      "and follow up. Never give personal advice — Amelia, the adviser, does that.");
        sb.AppendLine();
        if (record is not null)
        {
            var bundle = ClientCommunicationsDrafter.Build(record);
            sb.AppendLine("CALLER & CASE CONTEXT (the caller's identity has already been verified — do not read this aloud):");
            sb.AppendLine($"Case number   : {record.CaseNumber}");
            sb.AppendLine($"Caller        : {record.ClientName}");
            sb.AppendLine($"Client number : {record.ClientNumber}");
            sb.AppendLine($"Request type  : {record.RequestType}");
            sb.AppendLine($"Stage         : {bundle.Stage}");
            sb.AppendLine($"What happened : {bundle.Summary.WhatHappened}");
            sb.AppendLine("What's next   : " + string.Join(" ", bundle.Summary.WhatsNext));
            if (bundle.VulnerabilityFlags.Count > 0)
                sb.AppendLine("Handle with care: " + string.Join(" ", bundle.VulnerabilityFlags));
            sb.AppendLine();
        }
        sb.AppendLine("CALLER SAID:");
        sb.AppendLine(message);
        return sb.ToString();
    }

    // ── Deterministic voice replies ───────────────────────────────────────────
    private static string FallbackVoiceReply(string message, CaseRecord? record)
    {
        var intent = DetectIntent(message);
        if (record is null) return GenericReply(intent);

        var caseReply = CaseReply(record, intent);
        if (caseReply is not null) return caseReply;

        var first = ClientCommunicationsDrafter.FirstName(record.ClientName);
        if (intent is "status" or "general" or "greeting")
        {
            var condolence = (record.RequestType ?? string.Empty).Contains("death", StringComparison.OrdinalIgnoreCase)
                ? "I'm so sorry for your loss. "
                : string.Empty;
            return $"Hi {first}, it's Cara from Caldova Life Advice. {condolence}Your request, {record.CaseNumber}, is with our team now, and we'll be in " +
                   "touch as soon as there's an update — there's nothing you need to do right now. Is there anything I can help with in the meantime?";
        }
        return $"Hi {first}. " + GenericReply(intent);
    }

    private static string DetectIntent(string message)
    {
        var t = (message ?? string.Empty).ToLowerInvariant().Replace('’', '\'');
        bool Any(params string[] words) => words.Any(w => t.Contains(w));

        if (Any("changed my", "change my details", "someone changed", "hacked", "scam", "fraud", "didn't ask", "did not ask",
                "not me", "suspicious", "stolen", "identity", "someone called", "someone rang"))
            return "security";
        if (Any("passed away", "passed", "died", "death", "funeral", "lost my wife", "lost my husband", "lost my partner"))
            return "bereavement";
        if (Any("when will", "first payment", "get paid", "be paid", "payment", "paid"))
            return "payment";
        if (Any("hurt", "injur", "accident", "can't work", "cannot work", "off work", "surgery", "income protection"))
            return "claim-injury";
        if (Any("cancer", "diagnos", "illness", "sick", "heart attack", "stroke", "trauma"))
            return "claim-illness";
        if (Any("claim"))
            return "claim";
        if (Any("business", "partner", "buy-sell", "buy sell", "key person", "succession", "company"))
            return "business";
        if (Any("premium", "cost", "price", "expensive", "went up", "go up"))
            return "premium";
        if (Any("exclusion", "loading", "covered for", "my cover", "my policy", "what does", "cover start", "start"))
            return "cover";
        if (Any("bought a home", "new home", "mortgage", "baby", "protect my family", "life insurance", "new cover", "get cover"))
            return "new-advice";
        if (Any("status", "update", "what's happening", "whats happening", "where is", "where's", "how long"))
            return "status";
        if (Any("hello", "hi ", "hey", "good morning", "good afternoon", "g'day"))
            return "greeting";
        return "general";
    }

    // Case-aware replies once the caller has been verified against a case.
    // Returns null when the caller's question is about something else, so
    // the generic answer for that topic is used instead.
    private static string? CaseReply(CaseRecord record, string intent)
    {
        switch (LifeScenarioCases.KeyOf(record))
        {
            case LifeScenarioCases.Priya when intent is not ("security" or "bereavement" or "business" or "claim" or "claim-injury" or "claim-illness"):
                return "Hi Priya, it's Cara from Caldova Life Advice. Your Corella Life cover started on Wednesday the 18th of November — life cover of " +
                       "$750,000, TPD of $600,000, trauma of $150,000 and income protection of $6,125 a month. The one thing to know is that your income " +
                       "protection doesn't cover back or spine conditions, and Corella will review that after two symptom-free years. Your welcome letter " +
                       "and plain-English summary are in your email — is there anything you'd like me to explain?";
            case LifeScenarioCases.Grace when intent is not ("bereavement" or "business" or "new-advice" or "premium"):
                return "Grace, thank you for calling, and I'm so sorry this happened while you're away. Your cover is safe — no claim was lodged and " +
                       "nothing was paid. We've put your own mobile and email back, locked your portal and told Lyrebird Life's fraud team, and from now " +
                       "on we'll only contact you on 0491 570 157. You haven't done anything wrong. Would you like me to go through a few steps to protect " +
                       "your identity?";
            case LifeScenarioCases.George when intent is not ("security" or "bereavement" or "claim" or "claim-injury" or "claim-illness"):
                return "Hi George, it's Cara from Caldova Life Advice. Your Northbank Life cover has been in force since Friday the 20th of November — " +
                       "$1.2 million of buy-sell cover each for you and Harpreet, and $400,000 of key person cover on you, owned by the company. Your " +
                       "lawyer is finalising the buy-sell agreement, and Northbank will review your loading in two years. Anything I can help with?";
            case LifeScenarioCases.Tom when intent is not ("security" or "bereavement" or "business" or "new-advice"):
                return "Hi Tom, it's Cara — how's the shoulder? Harbourline has accepted your claim at $5,600 a month. That's 70% of your average " +
                       "monthly earnings over the year before your injury, which is lower than the $7,000 you're insured for. It counts from the 13th of " +
                       "November and it's paid monthly in arrears, so your first payment should arrive around the 13th of December. Would you like Amelia " +
                       "or Ben to call you to go through it?";
            case LifeScenarioCases.Robert when intent is not ("security" or "business" or "new-advice" or "premium"):
                return "Robert, I'm so sorry about Mei. Corella paid her life cover of $1,000,000 into your account on Monday the 23rd of November — " +
                       "directly to you, as her nominated beneficiary — and it isn't taxable. Mei's $180,000 of cover with Riverbend Super is a separate " +
                       "claim, and Ben will help you with it one step at a time. Amelia is your contact for anything at all — would you like her to call you?";
            default:
                return null;
        }
    }

    private static string GenericReply(string intent) => intent switch
    {
        "security" =>
            "That must be really worrying — thank you for calling straight away. For your protection, we never change contact or bank details, or " +
            "lodge a claim, on a call like this. I'll flag your file now, and a specialist will call you back on the number we had on file before any " +
            "change. Please don't share any codes you've been sent — and IDCARE, a free identity support service, can help on 1800 595 160.",
        "bereavement" =>
            "I'm so sorry for your loss. I'll help you one step at a time. Could you tell me your loved one's full name and, if you have it, their " +
            "policy number? We'll need a certified copy of the death certificate and some identification for you, and Amelia will be your contact " +
            "the whole way through.",
        "claim-injury" =>
            "I'm sorry to hear you've been hurt. I can help you start a claim. Ben, our Benefit Claims Advocate, will check your policy — the waiting " +
            "period and what the insurer needs, like your doctor's report and proof of your earnings — and we'll only lodge it once it's ready. Could I " +
            "take your name and policy number, and the date you stopped work?",
        "claim-illness" =>
            "I'm really sorry you're going through this. We'll help with the claim so you can focus on your health. Ben from our Claims team will check " +
            "what your policy covers and what the insurer needs — usually a report from your specialist. Could I take your name and policy number?",
        "claim" =>
            "I can help with that. Ben, our Benefit Claims Advocate, will check your policy and what the insurer needs, and we'll only lodge the claim " +
            "once it's ready and approved. Could I take your name and policy number, and a little about what's happened?",
        "payment" =>
            "I can check that for you. Could you give me your client number — it starts with C-L — or the case reference starting with R-E-Q?",
        "cover" =>
            "Happy to help you understand your cover. I can explain what your policy says in plain English, but if you're thinking about changing it, " +
            "Amelia, our Senior Risk Adviser, is the right person — she gives the advice. Could I have your name and client number?",
        "premium" =>
            "I understand — premiums matter. Stepped premiums usually rise each year as you get older, while level premiums start higher and rise more " +
            "slowly. I can't recommend a change, but Amelia can review your cover with you. Would you like me to book a time with her?",
        "business" =>
            "We help business owners protect their business with buy-sell and key person cover. Amelia, our Senior Risk Adviser, would start with a " +
            "conversation about your business, what it's worth and who it depends on. Would you like me to book you in with her?",
        "new-advice" =>
            "That's a great thing to be thinking about. Amelia, our Senior Risk Adviser, starts with a fact find — your income, debts, family and any " +
            "cover you already have, including through super — and then gives you her advice in writing. Would you like me to book a time with her?",
        "status" =>
            "I can check that for you. Could you give me your client number, starting with C-L, or the case reference starting with R-E-Q?",
        _ =>
            "Hi, you're speaking with Cara from Caldova Life Advice. I can help with your cover, a claim, or a request you've made. What can I help " +
            "you with today?"
    };

    // Strip basic markdown so the reply sounds natural when read aloud by the
    // browser's speech synthesiser.
    private static string StripMarkdown(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var cleaned = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw.TrimStart();
            if (line.StartsWith("```")) continue;
            while (line.StartsWith('#')) line = line[1..];
            if (line.StartsWith("- ") || line.StartsWith("* ")) line = line[2..];
            line = Regex.Replace(line, @"^\d+\.\s+", "");
            line = line.Replace("**", "").Replace("__", "").Replace("`", "");
            cleaned.Add(line.Trim());
        }
        return string.Join(" ", cleaned.Where(l => l.Length > 0)).Trim();
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');
}
