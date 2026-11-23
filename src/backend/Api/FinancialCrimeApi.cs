using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CaldovaLife.Agents;
using CaldovaLife.App.Models;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

record FinCrimeVerifyRequest(string CaseNumber, List<string>? DocumentIds);
record FinCrimeProcessRequest(string CaseNumber, List<string>? DocumentIds);

/// <summary>
/// One explainable risk indicator: what was found, the evidence (with
/// document numbers), where it came from and the points it adds to the score.
/// </summary>
/// <param name="Severity"><c>high</c>, <c>medium</c>, <c>low</c> or <c>info</c> (watch-point).</param>
public record FinCrimeRiskIndicator(string Key, string Title, string Evidence, string Source, int Points, string Severity);

/// <summary>
/// A recommended control for the people on the case. When
/// <paramref name="RequiresApproval"/> is true nothing happens until
/// <paramref name="Approver"/> approves it.
/// </summary>
public record FinCrimeRecommendedAction(string Action, string Detail, bool RequiresApproval, string? Approver = null);

/// <summary>
/// HTTP endpoints behind the "Try it out" tab on the Financial Crime Agent page
/// (<c>/agents/financial-crime</c>):
///
/// 1. <c>GET /fincrime/cases</c> — Client Cases held in memory (the five
///    scenario cases plus any opened live with the Onboarding Agent).
/// 2. <c>GET /fincrime/cases/{caseNumber}/activity</c> — the client activity
///    timeline, the people on the case, KYC / PEP / sanctions screening and the
///    knowledge documents to cite.
/// 3. <c>GET /fincrime/samples</c> and <c>GET /fincrime/cases/{caseNumber}/documents</c>
///    — the specimen documents and the ones attached to the case.
/// 4. <c>POST /fincrime/documents/verify</c> — document authenticity (Azure
///    Content Understanding plus a deterministic checks layer).
/// 5. <c>POST /fincrime/process</c> — engage Felix's Financial Crime Agent with
///    the case, timeline, screening and document findings; returns an
///    explainable risk score, the indicators and the recommended actions, each
///    marked when it needs human approval (streamed over SSE when the client
///    sends <c>Accept: text/event-stream</c>).
///
/// When Foundry or Content Understanding isn't configured (or can't be
/// reached), a deterministic assessment built from the scenario data keeps the
/// page working offline.
/// </summary>
public static class FinancialCrimeApi
{
    private const int Baseline = 5;
    private const string Theo = "Theo, Practice Team Leader";

    private static readonly TimeSpan AgentCreateWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AgentRunWait = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan FoundryRetryAfter = TimeSpan.FromMinutes(2);

    // After Foundry fails, skip it for a short while so the page answers
    // straight away from the offline assessment (UTC ticks).
    private static long _skipFoundryUntilTicks;

    private static readonly Regex InsurerRegex = new(@"\b(Harbourline|Corella|Lyrebird|Northbank) Life\b", RegexOptions.Compiled);
    private static readonly Regex MobileRegex = new(@"\b04\d{2} \d{3} \d{3}\b", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"[\w.+-]+@[\w-]+(\.[\w-]+)+", RegexOptions.Compiled);

    public static void MapFinancialCrimeEndpoints(
        this WebApplication app,
        CaseStore store,
        CaldovaAgentFactory factory,
        FinCrimeCaseDocumentStore docStore,
        FinCrimeDocumentVerifier verifier,
        ILogger logger)
    {
        // Cases for the "Pick a case" dropdown.
        app.MapGet("/fincrime/cases", () =>
        {
            var cases = store.All().Select(r =>
            {
                docStore.EnsureCaseAttachments(r);
                var profile = docStore.ProfileFor(r);
                return new
                {
                    caseNumber = r.CaseNumber,
                    sampleId = r.SampleId,
                    clientName = r.ClientName,
                    clientEmail = r.ClientEmail,
                    clientPhone = r.ClientPhone,
                    clientNumber = r.ClientNumber,
                    cover = r.Cover,
                    requestType = r.RequestType,
                    receivedDate = r.ReceivedDate,
                    adviserName = r.AdviserName,
                    referredBy = r.ReferredBy,
                    requestSummary = r.RequestSummary,
                    amount = r.Amount,
                    preferredContact = r.PreferredContact,
                    priority = r.Priority,
                    priorityReason = r.PriorityReason,
                    createdAt = r.CreatedAt.ToString("u"),
                    isScenario = profile.IsScenario,
                    scenarioKey = profile.ScenarioKey,
                    focus = profile.Focus,
                    documentCount = docStore.DocumentsForCase(r.CaseNumber).Count,
                };
            });
            return Results.Ok(cases);
        });

        // Client activity timeline, people, screening and knowledge documents for a case.
        app.MapGet("/fincrime/cases/{caseNumber}/activity", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });

            var profile = docStore.ProfileFor(record);
            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                isScenario = profile.IsScenario,
                scenarioKey = profile.ScenarioKey,
                focus = profile.Focus,
                verifiedContact = profile.VerifiedContact,
                travelNotice = TravelNoticeView(profile.TravelNotice),
                parties = PartiesView(profile),
                timeline = TimelineView(profile),
                screening = profile.Screening,
                references = profile.References,
                nextStep = profile.NextStep,
            });
        });

        // Specimen documents (drives the "Add specimen documents" popover).
        app.MapGet("/fincrime/samples", () =>
            Results.Ok(docStore.AllSamples().Select(s => new
            {
                id = s.Id,
                label = s.Label,
                kind = s.Kind,
                src = s.Src,
                expected = s.Expected,
                description = s.Description,
                owner = s.Owner,
                ownerName = string.IsNullOrWhiteSpace(s.Owner) ? null : FinCrimeCaseDocumentStore.ClientNameFor(s.Owner),
            })));

        // Documents attached to a case (seeded per scenario from the manifest).
        app.MapGet("/fincrime/cases/{caseNumber}/documents", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });

            docStore.EnsureCaseAttachments(record);
            return Results.Ok(docStore.DocumentsForCase(record.CaseNumber));
        });

        // Scan documents: per-document verdict, reasons and every check performed.
        app.MapPost("/fincrime/documents/verify", async (FinCrimeVerifyRequest request, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            docStore.EnsureCaseAttachments(record);
            var ids = request.DocumentIds ?? docStore.DocumentsForCase(record.CaseNumber).Select(d => d.Id).ToList();
            var samples = docStore.ResolveSamples(ids);
            var documents = await verifier.VerifyAsync(record, samples, useCache: false, ctx.RequestAborted);

            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                contentUnderstandingConfigured = verifier.ContentUnderstandingConfigured,
                checkNames = verifier.CheckNames,
                documents,
            });
        });

        // Engage Felix's Financial Crime Agent on the case.
        app.MapPost("/fincrime/process", async (HttpContext httpContext, FinCrimeProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            logger.LogInformation("Financial crime review: caseNumber={CaseNumber}", Sanitize(record.CaseNumber));

            // Reuse the documents scanned in step 3 when available; default to the
            // documents attached to the case when the page sends none.
            docStore.EnsureCaseAttachments(record);
            var ids = request.DocumentIds ?? docStore.DocumentsForCase(record.CaseNumber).Select(d => d.Id).ToList();
            var samples = docStore.ResolveSamples(ids);
            var documents = await verifier.VerifyAsync(record, samples, useCache: true, httpContext.RequestAborted);

            var profile = docStore.ProfileFor(record);
            var seededCount = docStore.AllSamples().Count(s =>
                profile.ScenarioKey.Length > 0 && string.Equals(s.Owner, profile.ScenarioKey, StringComparison.OrdinalIgnoreCase));
            var assessment = Assess(record, profile, documents, seededCount);
            var prompt = BuildAgentPrompt(record, profile, documents);
            string? agentCreateError = null;

            object BuildEnvelope(AgentTraceResult? trace, string? agentError) => new
            {
                caseNumber = record.CaseNumber,
                clientName = record.ClientName,
                focus = profile.Focus,
                riskLevel = assessment.Level,
                riskScore = assessment.Score,
                scoreNote = assessment.ScoreNote,
                baseline = Baseline,
                riskSummary = assessment.Summary,
                indicators = assessment.Indicators,
                checksClear = assessment.ChecksClear,
                actions = assessment.Actions,
                approvalRequired = assessment.ApprovalRequired,
                reportingNote = assessment.ReportingNote,
                amountAtRisk = profile.AmountAtRisk is { } atRisk ? Money(atRisk) : null,
                nextStep = profile.NextStep,
                outcome = profile.Outcome,
                verifiedContact = profile.VerifiedContact,
                timeline = TimelineView(profile),
                screening = profile.Screening,
                references = profile.References,
                documents,
                checkNames = verifier.CheckNames,
                contentUnderstandingConfigured = verifier.ContentUnderstandingConfigured,
                agentRiskLevel = ParseAgentLevel(trace?.Text),
                agentRiskScore = ParseAgentScore(trace?.Text),
                agentNotes = trace?.Text,
                agentConfigured = factory.IsConfigured,
                agentError = agentError ?? agentCreateError,
                agentInput = trace?.Input,
                agentRawOutput = trace is null ? null : new
                {
                    text = trace.Text,
                    citations = trace.Citations,
                    outputItems = trace.OutputItems,
                    responseId = trace.ResponseId,
                    durationMs = trace.DurationMs,
                },
            };

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                // Creating the agent registers it with Foundry, so it can fail
                // (or stall) too; the stream then still ends with the
                // deterministic assessment.
                CaldovaAgent? streamingAgent = null;
                if (factory.IsConfigured)
                {
                    (streamingAgent, agentCreateError) = await TryCreateAgentAsync(factory, logger, httpContext.RequestAborted);
                }
                await AgentSseStreaming.StreamAsync(httpContext, streamingAgent, prompt, BuildEnvelope, logger, "Financial Crime Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? agentInvocationError = null;
            if (factory.IsConfigured)
            {
                var (agent, createError) = await TryCreateAgentAsync(factory, logger, httpContext.RequestAborted);
                agentInvocationError = createError;
                if (agent is not null)
                {
                    try
                    {
                        traceResult = await agent.RunWithTraceAsync(prompt).WaitAsync(AgentRunWait, httpContext.RequestAborted);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !httpContext.RequestAborted.IsCancellationRequested)
                    {
                        logger.LogWarning(ex, "Financial Crime Agent invocation failed; using the deterministic assessment only");
                        SkipFoundryForAWhile();
                        agentInvocationError = ex is TimeoutException
                            ? $"The agent didn't answer within {AgentRunWait.TotalSeconds:0} seconds."
                            : ex.Message;
                    }
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, agentInvocationError));
        });
    }

    /// <summary>
    /// Create Felix's agent without letting an unreachable Foundry project hold
    /// the page: creation registers the agent version, so it can fail or stall.
    /// After a failure Foundry is skipped for a couple of minutes.
    /// </summary>
    private static async Task<(CaldovaAgent? Agent, string? Error)> TryCreateAgentAsync(
        CaldovaAgentFactory factory, ILogger logger, CancellationToken ct)
    {
        if (DateTime.UtcNow.Ticks < Volatile.Read(ref _skipFoundryUntilTicks))
            return (null, "Foundry wasn't reachable a moment ago, so this is the offline assessment.");

        try
        {
            var agent = await Task.Run(() => factory.Create("fincrime"), ct).WaitAsync(AgentCreateWait, ct);
            return (agent, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Financial Crime Agent could not be created; using the deterministic assessment only");
            SkipFoundryForAWhile();
            return (null, ex is TimeoutException
                ? $"Foundry didn't respond within {AgentCreateWait.TotalSeconds:0} seconds."
                : ex.Message);
        }
    }

    private static void SkipFoundryForAWhile() =>
        Volatile.Write(ref _skipFoundryUntilTicks, DateTime.UtcNow.Add(FoundryRetryAfter).Ticks);

    // ── Views ───────────────────────────────────────────────────────────────

    private static object? TravelNoticeView(FinCrimeTravelNotice? t) => t is null
        ? null
        : new { place = t.Place, from = Dmy(t.From), to = Dmy(t.To), recordedAt = t.RecordedAt };

    private static IEnumerable<object> PartiesView(FinCrimeCaseProfile profile) =>
        profile.Parties.Select(p => new
        {
            name = p.Name,
            role = p.Role,
            dateOfBirth = FormatDate(p.DateOfBirth),
            isCompany = p.IsCompany,
        });

    private static IEnumerable<object> TimelineView(FinCrimeCaseProfile profile) =>
        profile.Timeline.Select(e => new
        {
            when = e.When,
            type = e.Type,
            channel = e.Channel,
            summary = e.Summary,
            detail = e.Detail,
            flag = e.Flag,
            location = e.Location,
        });

    // ── Prompt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the prompt for Felix's agent: the Client Case, the people on it,
    /// the client activity timeline, KYC / PEP / sanctions screening, the
    /// document authenticity findings (numbered so the agent can cite them),
    /// the request email, the knowledge documents to cite and the scoring
    /// rubric that keeps the risk score explainable.
    /// </summary>
    private static string BuildAgentPrompt(
        CaseRecord c,
        FinCrimeCaseProfile profile,
        IReadOnlyList<FinCrimeDocumentVerification> documents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CLIENT CASE FOR FINANCIAL CRIME REVIEW");
        sb.AppendLine("======================================");
        sb.AppendLine($"Case number: {c.CaseNumber}");
        sb.AppendLine($"Request type: {c.RequestType}");
        sb.AppendLine($"Received: {FormatDate(c.ReceivedDate)} (treat this as today's date)");
        sb.AppendLine($"Client: {c.ClientName} — {c.ClientNumber}");
        sb.AppendLine($"Cover: {c.Cover}");
        sb.AppendLine($"Adviser: {c.AdviserName}{(string.IsNullOrWhiteSpace(c.ReferredBy) ? string.Empty : $" · referred by {c.ReferredBy}")}");
        sb.AppendLine($"Amount: {c.Amount}");
        sb.AppendLine($"Priority at intake: {c.Priority} — {c.PriorityReason}");
        sb.AppendLine($"What to check: {profile.Focus}");
        sb.AppendLine($"Contact details we trust for call-backs: {profile.VerifiedContact}");
        if (profile.TravelNotice is { } travel)
            sb.AppendLine($"Travel notice: {travel.Place}, {Dmy(travel.From)} to {Dmy(travel.To)} (recorded at {travel.RecordedAt})");
        if (profile.EvidenceRule is { } rule)
            sb.AppendLine($"Evidence rule: {rule}");
        sb.AppendLine();
        sb.AppendLine("Request summary (as captured by Onboarding):");
        sb.AppendLine(c.RequestSummary);
        sb.AppendLine();

        sb.AppendLine("PEOPLE AND COMPANIES ON THE CASE");
        foreach (var p in profile.Parties)
        {
            var dob = FormatDate(p.DateOfBirth);
            sb.AppendLine($"- {p.Name} — {p.Role}{(dob is null ? string.Empty : $" — DOB on file {dob}")}");
        }
        if (profile.AcceptedPayees.Count > 0)
            sb.AppendLine($"Benefits may be paid to: {string.Join(", ", profile.AcceptedPayees)}");
        if (profile.RequestedPayees.Count > 0)
            sb.AppendLine($"Payment account named on the request: {string.Join(", ", profile.RequestedPayees)}");
        sb.AppendLine();

        sb.AppendLine("CLIENT ACTIVITY TIMELINE (Melbourne time)");
        foreach (var e in profile.Timeline)
            sb.AppendLine($"- {e.When} · {e.Channel} · {e.Summary}. {e.Detail} [{e.Flag}]");
        sb.AppendLine();

        sb.AppendLine("KYC, PEP / SANCTIONS AND OTHER SCREENING");
        foreach (var s in profile.Screening)
            sb.AppendLine($"- {s.Group} — {s.Check}: {s.Result} [{s.Status}]");
        sb.AppendLine();

        if (documents.Count > 0)
        {
            sb.AppendLine("DOCUMENT AUTHENTICITY FINDINGS");
            sb.AppendLine("Each document was read by Azure Content Understanding (or from the case-file record when it");
            sb.AppendLine("isn't available) and scored by a deterministic checks layer. Cite each document by its number.");
            for (var i = 0; i < documents.Count; i++)
            {
                var d = documents[i];
                sb.AppendLine($"Document #{i + 1} — {d.Label} ({FinCrimeDocumentVerifier.KindLabel(d.Kind)})");
                sb.AppendLine($"  Verdict: {d.Verdict} · Source: {d.Source}");
                sb.AppendLine($"  Summary: {d.Summary}");
                foreach (var check in d.Checks.Where(x => x.Status is "fail" or "warn"))
                    sb.AppendLine($"  [{check.Status}] {check.Name} — {check.Detail}");
                var passed = d.Checks.Where(x => x.Status == "pass").Select(x => x.Name).ToList();
                if (passed.Count > 0) sb.AppendLine($"  Passed: {string.Join("; ", passed)}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("DOCUMENT AUTHENTICITY FINDINGS");
            sb.AppendLine("No documents were included in this review.");
            sb.AppendLine();
        }

        if (RequestSampleCatalog.FindById(c.SampleId) is { } email)
        {
            sb.AppendLine("REQUEST EMAIL (as received)");
            sb.AppendLine($"From: {email.EmailFrom}");
            sb.AppendLine($"Date: {email.EmailDate}");
            sb.AppendLine($"Subject: {email.EmailSubject}");
            sb.AppendLine(email.EmailBody.Length > 1500 ? email.EmailBody[..1500] + "…" : email.EmailBody);
            sb.AppendLine();
        }

        sb.AppendLine("KNOWLEDGE DOCUMENTS TO CITE (life_knowledge index)");
        foreach (var title in profile.References)
            sb.AppendLine($"- {title}");
        sb.AppendLine();

        sb.AppendLine("RISK RATING (Guide — financial crime red flags and controls for life insurance)");
        sb.AppendLine("Level: Low — no red flags, or one that is explained; Medium — one unexplained red flag; High — two or");
        sb.AppendLine("more unexplained red flags, any document that isn't genuine, or a payment to an account in another");
        sb.AppendLine("name together with any other flag. High-risk requests are held and escalated to Theo at once.");
        sb.AppendLine("Score (0–100, keeps the rating explainable): start at 5 and add — contact details changed without");
        sb.AppendLine("verifying the client +18 (a verified change +8); new device in a place that contradicts a travel");
        sb.AppendLine("notice +12 (a new device elsewhere +6); claim or payment request within 30 days of a contact change");
        sb.AppendLine("+15; payment to an account not in the name of the policy owner or nominated beneficiary +12; first");
        sb.AppendLine("fake or tampered document +15, each further one +10; other document needing a closer look +6 each");
        sb.AppendLine("(max 12); PEP or sanctions match +20; watch-points: new client +3, client under financial stress +3,");
        sb.AppendLine("company ownership +2, bereavement +2. A High rating scores at least 65 and a Medium one at least 35.");
        sb.AppendLine();

        sb.AppendLine("TASK");
        sb.AppendLine("Review this Client Case for fraud, scams, identity takeover and KYC gaps. Use the knowledge base and");
        sb.AppendLine("cite the documents above by title. Produce your output sections, refer to documents by number and");
        sb.AppendLine("keep the language neutral — never accuse anyone; the client may be the victim. Use only the trusted");
        sb.AppendLine("contact details above for any call-back. Every block, control and referral to an insurer needs");
        sb.AppendLine("Theo's approval — recommend, don't decide.");
        return sb.ToString();
    }

    // ── Deterministic assessment ────────────────────────────────────────────

    private sealed record Assessment(
        string Level,
        int Score,
        string Summary,
        IReadOnlyList<FinCrimeRiskIndicator> Indicators,
        IReadOnlyList<string> ChecksClear,
        IReadOnlyList<FinCrimeRecommendedAction> Actions,
        string ApprovalRequired,
        string? ReportingNote,
        string? ScoreNote);

    /// <summary>
    /// Explainable risk assessment from the case profile (timeline, payees,
    /// travel notice, screening) and the document verdicts. Every point on the
    /// score comes from a named indicator so a person can see exactly why.
    /// </summary>
    private static Assessment Assess(
        CaseRecord c,
        FinCrimeCaseProfile p,
        IReadOnlyList<FinCrimeDocumentVerification> documents,
        int seededDocumentCount)
    {
        var indicators = new List<FinCrimeRiskIndicator>();
        var clear = new List<string>();
        var client = p.Parties.FirstOrDefault(x => !x.IsCompany)?.Name ?? c.ClientName;
        var first = FirstName(client);
        var insurer = InsurerFor(p);

        var change = p.Timeline.FirstOrDefault(e => e.Type == "contact-change");
        var device = p.Timeline.FirstOrDefault(e => e.Type == "new-device");
        var bankEvent = p.Timeline.FirstOrDefault(e => e.Type == "bank-account");
        var claim = p.Timeline.LastOrDefault(e => e.Type == "claim");
        var policyHistory = p.Timeline.LastOrDefault(e => e.Type == "policy");
        var travel = p.TravelNotice;

        // Contact details changed without verifying the client.
        if (change is not null)
        {
            var unverified = change.Flag == "red";
            indicators.Add(new("contact-change",
                unverified ? "Contact details changed without verifying the client" : "Contact details changed recently",
                $"{change.When} — {change.Detail}",
                "Client activity log", unverified ? 18 : 8, unverified ? "high" : "medium"));
        }

        // New device, especially somewhere a travel notice says the client isn't.
        if (device is not null)
        {
            var away = travel is not null && device.Location is { } seenAt &&
                       !seenAt.Contains(travel.Place, StringComparison.OrdinalIgnoreCase) &&
                       device.At.Date >= travel.From && device.At.Date <= travel.To
                ? travel
                : null;
            var after = change is not null && device.At >= change.At
                ? $", {Math.Round((device.At - change.At).TotalMinutes)} minutes after the contact change"
                : string.Empty;
            var travelText = away is not null
                ? $" The travel notice from {away.RecordedAt} puts {first} in {away.Place} from {Dmy(away.From)} to {Dmy(away.To)}."
                : string.Empty;
            indicators.Add(new("new-device",
                away is not null ? "Login from a new device where the client isn't" : "Online access from a new device",
                $"{device.When} — {device.Summary}{after}.{travelText}",
                "Client activity log", away is not null ? 12 : 6, away is not null ? "high" : "medium"));
        }

        // A claim or payment request within 30 days of a contact change
        // (the guide holds these and calls back on the verified number).
        if (claim is not null && change is not null && claim.At >= change.At && (claim.At - change.At).TotalDays <= 30)
        {
            var hours = (int)Math.Round((claim.At - change.At).TotalHours);
            var urgent = claim.Summary.Contains("urgent", StringComparison.OrdinalIgnoreCase);
            var full = claim.Summary.Contains("full", StringComparison.OrdinalIgnoreCase);
            var history = policyHistory is not null && policyHistory.Summary.StartsWith("No claims", StringComparison.OrdinalIgnoreCase)
                ? $" {policyHistory.Summary}."
                : string.Empty;
            indicators.Add(new("claim-pattern",
                $"{(urgent ? "Urgent claim" : "Claim")}{(full ? " for the full benefit" : string.Empty)}, about {hours} hours after the contact change",
                $"{claim.When} — {claim.Summary}. {claim.Detail}{history}",
                "Claim email", 15, "high"));
        }

        // Payment to an account that isn't in the policy owner's or beneficiary's name.
        var mismatched = p.RequestedPayees
            .Where(n => !p.AcceptedPayees.Any(a => NamesMatch(n, a)))
            .ToList();
        if (mismatched.Count > 0)
        {
            var opened = bankEvent is not null
                ? $" The account (ending {LastDigits(bankEvent.Summary) ?? "—"}) was opened on {Dmy(bankEvent.At)}" +
                  (change is not null && bankEvent.At.Date >= change.At.Date
                      ? $", {(bankEvent.At.Date - change.At.Date).Days} day{((bankEvent.At.Date - change.At.Date).Days == 1 ? "" : "s")} after the contact change."
                      : ".")
                : string.Empty;
            var owner = p.AcceptedPayees.FirstOrDefault() ?? client;
            indicators.Add(new("payee",
                "Benefit to be paid into a new account in another name",
                $"The request asks Caldova to pay '{string.Join("', '", mismatched)}' — not {owner}, the policy owner.{opened} {(insurer ?? "The insurer")} pays benefits to the policy owner, so an account in another name can't be used.",
                "Claim email", 12, "high"));
        }

        // Documents.
        var numbered = documents.Select((d, i) => (Doc: d, No: i + 1)).ToList();
        var altered = numbered.Where(x => x.Doc.Verdict is "fake" or "tampered").ToList();
        for (var i = 0; i < altered.Count; i++)
        {
            var (doc, no) = altered[i];
            var concerns = doc.Checks.Where(x => x.Status is "fail" or "warn").Select(x => x.Detail).ToList();
            indicators.Add(new($"document-{no}",
                doc.Verdict == "fake"
                    ? $"Document #{no} looks fake: {ShortLabel(doc.Label)}"
                    : $"Document #{no} has been tampered with: {ShortLabel(doc.Label)}",
                $"{doc.Summary} {string.Join(" ", concerns)}".Trim(),
                $"Document #{no}", i == 0 ? 15 : 10, "high"));
        }

        var extraDocPoints = 0;
        foreach (var (doc, no) in numbered.Where(x => x.Doc.Verdict == "suspicious"))
        {
            var index = doc.Kind == "bank-statement" ? indicators.FindIndex(x => x.Key == "payee") : -1;
            if (index >= 0)
            {
                var existing = indicators[index];
                indicators[index] = existing with
                {
                    Evidence = $"{existing.Evidence} See Document #{no}: {doc.Summary}",
                    Source = $"{existing.Source}, Document #{no}",
                };
            }
            else
            {
                var points = Math.Min(6, 12 - extraDocPoints);
                extraDocPoints += points;
                indicators.Add(new($"document-review-{no}", $"Document #{no} needs verification: {ShortLabel(doc.Label)}",
                    doc.Summary, $"Document #{no}", points, "medium"));
            }
        }
        foreach (var (doc, no) in numbered.Where(x => x.Doc.Verdict == "legit"))
            clear.Add($"Document #{no} ({ShortLabel(doc.Label)}) — consistent with a genuine document.");

        // PEP / sanctions matches (other screening rows are shown in the review step).
        foreach (var s in p.Screening)
        {
            if (s.Group.Contains("sanctions", StringComparison.OrdinalIgnoreCase) && s.Status == "fail")
                indicators.Add(new("screening", $"Screening match — {s.Check}", s.Result, "PEP and sanctions screening", 20, "high"));
            else if (s.Status == "pass")
                clear.Add($"{s.Check}: {s.Result}.");
        }
        foreach (var e in p.Timeline.Where(e => e.Flag == "ok" && e.Type == "check"))
        {
            var line = $"{e.Summary}.";
            if (!clear.Contains(line)) clear.Add(line);
        }

        // Watch-points by scenario.
        var watchPoint = WatchPoint(p, first);
        if (watchPoint is not null) indicators.Add(watchPoint);

        // Level follows 'Guide — financial crime red flags and controls for life
        // insurance': High for two or more unexplained red flags or any document
        // that isn't genuine (a payment to an account in another name plus any
        // other flag is already two); Medium for one; Low for none. The score
        // explains the level and is lifted into the level's band when needed.
        var redFlags = indicators.Where(x => x.Severity is not "info").ToList();
        var level = redFlags.Count >= 2 || altered.Count > 0 ? "High"
                  : redFlags.Count == 1 ? "Medium"
                  : "Low";
        var raw = Math.Clamp(Baseline + indicators.Sum(x => x.Points), 1, 99);
        var score = level switch
        {
            "High" => Math.Max(raw, 65),
            "Medium" => Math.Clamp(raw, 35, 64),
            _ => raw,
        };
        string? scoreNote = score == raw ? null : level switch
        {
            "High" => $"Raised to {score}: the red-flags guide rates {(altered.Count > 0 ? "any document that isn't genuine" : "two or more unexplained red flags")} as High.",
            _ => $"Set to {score}: the red-flags guide rates one unexplained red flag as Medium.",
        };

        var strongest = redFlags.OrderByDescending(x => x.Points).Take(4).Select(x => LowerFirst(x.Title));
        var allSeededLegit = seededDocumentCount > 0 && documents.Count >= seededDocumentCount && documents.All(d => d.Verdict == "legit");
        var takeover = indicators.Any(x => x.Key == "contact-change");
        var summary = level switch
        {
            "High" =>
                $"High risk ({score}/100). {redFlags.Count} red flag{(redFlags.Count == 1 ? " needs" : "s need")} verifying" +
                (takeover && redFlags.Count > 1 ? " and together they match the pattern of an identity takeover" : string.Empty) +
                " — the strongest: " + string.Join("; ", strongest) +
                $". Recommend holding the request and contacting {first} only on the details we already trust. {first} is most likely the victim, not a suspect.",
            "Medium" =>
                $"Medium risk ({score}/100). One finding needs verifying before the request continues — " +
                string.Join("; ", strongest.Take(1)) +
                $". Verify with {first} on the contact details already on file; other work can continue in parallel.",
            _ => $"Low risk ({score}/100). " + LowRiskSummary(p, documents, allSeededLegit),
        };

        var actions = BuildActions(level, c, p, change, device, first, insurer);
        var approval = level switch
        {
            "High" =>
                $"Yes — Theo, the Practice Team Leader, must approve each control before it happens: blocking the request on financial-crime grounds, restoring the contact details, locking portal access, contacting {first} and referring the suspected fraud to {insurer ?? "the insurer"}. Felix's agent recommends; people decide.",
            "Medium" =>
                "Yes — delaying the request on financial-crime grounds needs Theo's approval. Verification calls on the trusted contact details can go ahead.",
            _ => "No financial-crime decision needed — the result is recorded on the Client Case." + LowRiskApprovalNote(p),
        };

        return new Assessment(level, score, summary, indicators, clear, actions, approval, ReportingNote(level, p, insurer, first), scoreNote);
    }

    private static FinCrimeRiskIndicator? WatchPoint(FinCrimeCaseProfile p, string first) => p.ScenarioKey switch
    {
        "priya" => new("new-client", "Watch-point: new client, no history to compare against",
            "Normal for a new client. Until there's history, identity verification and PEP / sanctions screening carry the weight.",
            "Request type", 3, "info"),
        "george" => new("company-ownership", "Watch-point: cover owned by a company",
            "The company would own the key person policy, so Caldova identifies the company and every person who owns or controls it — George and Harpreet, 50% each.",
            "Request type", 2, "info"),
        "tom" => new("financial-stress", "Watch-point: client under financial stress",
            "Not a concern about Tom. People under financial stress can be targeted by scams, so any request to change where benefits are paid is verified with a call-back on his verified mobile.",
            "Request type", 3, "info"),
        "robert" => new("bereavement", "Watch-point: bereavement",
            "Death claims can attract impersonation, so the death certificate and the beneficiary's ID are checked — and nothing more is asked of the family.",
            "Request type", 2, "info"),
        _ => null,
    };

    private static string LowRiskSummary(FinCrimeCaseProfile p, IReadOnlyList<FinCrimeDocumentVerification> documents, bool allSeededLegit)
    {
        if (allSeededLegit)
        {
            switch (p.ScenarioKey)
            {
                case "priya":
                    return "Priya's driver licence and payslip are genuine, her identity is verified (DVS) and there's no PEP or sanctions match — KYC is complete.";
                case "george":
                    return "The company extract and both partners' IDs are genuine. Northcote Custom Joinery Pty Ltd is registered, George and Harpreet are its directors and 50/50 beneficial owners, and there's no PEP or sanctions match for the company or either owner.";
                case "tom":
                    return "The BAS and profit and loss summary (net business income $96,000) and the surgeon's report are genuine, Kylie's support-person authority is on file and nothing about Tom's contact or bank details has changed.";
                case "robert":
                    return "The certified death certificate for Mei Chen and Robert's ID are genuine, and Robert is the nominated beneficiary. Nothing more needs to be asked of the family.";
            }
        }
        var legit = documents.Count(d => d.Verdict == "legit");
        return documents.Count == 0
            ? "No elevated indicators in the case information. No documents were included in this review."
            : $"No elevated indicators — {legit} of {documents.Count} document{(documents.Count == 1 ? "" : "s")} consistent with a genuine document.";
    }

    private static string LowRiskApprovalNote(FinCrimeCaseProfile p) => p.ScenarioKey switch
    {
        "george" => " Theo's second review of the $2,800,000 total cover is a separate advice control.",
        "tom" => " Lodging the claim is a separate approval in Claims.",
        "robert" => " Lodging the death claim is a separate approval in Claims, and every message to Robert's family needs Theo's approval.",
        _ => string.Empty,
    };

    /// <summary>
    /// Cautious note on referrals and AML/CTF. An advice licensee has AML/CTF
    /// obligations only when it arranges a designated service (table 1, item
    /// 54); pure-risk cover without a surrender value generally isn't one, so
    /// the agent never says a report is or isn't required — compliance decides.
    /// </summary>
    private static string? ReportingNote(string level, FinCrimeCaseProfile p, string? insurer, string first) => level switch
    {
        "High" =>
            $"Referral: {insurer ?? "the insurer"}'s fraud team decides whether to investigate the claim. Whether anything must also be reported to AUSTRAC is for Caldova's compliance adviser to decide — an advice licensee has AML/CTF reporting duties only when it arranges a designated service, and pure-risk cover like {first}'s generally isn't one. Keep the suspicion confidential.",
        "Medium" => "No referral at this stage — reassess once the client has been verified on the trusted contact details.",
        _ => p.ScenarioKey switch
        {
            "priya" => "AML/CTF: arranging the rollover from Riverbend Super that funds Priya's cover inside super may be a designated service Caldova arranges as a licensee (item 54). If so, this KYC is the initial customer due diligence it needs — Caldova's compliance adviser decides.",
            "george" => "Caldova's KYC standard for business clients: identify the company and every person who owns or controls it. Pure-risk cover generally isn't an AML/CTF designated service — Caldova's compliance adviser decides whether any AML/CTF obligation applies.",
            _ => null,
        },
    };

    private static List<FinCrimeRecommendedAction> BuildActions(
        string level,
        CaseRecord c,
        FinCrimeCaseProfile p,
        FinCrimeActivityEvent? change,
        FinCrimeActivityEvent? device,
        string first,
        string? insurer)
    {
        var actions = new List<FinCrimeRecommendedAction>();
        var mobile = MobileRegex.Match(p.VerifiedContact) is { Success: true } m ? m.Value : null;
        var email = EmailRegex.Match(p.VerifiedContact) is { Success: true } e ? e.Value : null;
        var trusted = string.Join(" and ", new[] { mobile is null ? null : $"mobile {mobile}", email }.Where(x => x is not null));

        if (level == "High")
        {
            actions.Add(new("Hold the claim and the bank-detail change",
                $"Don't lodge anything with {insurer ?? "the insurer"} and don't change the bank details. Ben confirms no claim has been lodged.",
                true, Theo));
            actions.Add(new("Restore the verified contact details",
                (trusted.Length > 0 ? $"Put back {trusted}" : "Put back the contact details verified before the change") +
                (change is not null ? $" and treat the change on {Dmy(change.At)} as unverified." : "."),
                true, Theo));
            actions.Add(new("Lock client portal access",
                "Lock the portal and end any active sessions" +
                (device?.Location is { } where ? $", including the new device seen in {where}" : string.Empty) +
                $". Reset the login once {first} is verified.",
                true, Theo));
            actions.Add(new(mobile is not null ? $"Call {first} on {mobile}" : $"Call {first} on the verified number",
                $"Only on the verified number held before the change — never the new number or email. Calm, plain English and no accusation: {first} is most likely the victim. Cara's agent drafts the call notes and a reassurance letter with identity-protection steps: change email and portal passwords, turn on multi-factor authentication, watch for unexpected messages or logins, and contact the bank if bank details may have been exposed.",
                true, Theo));
            actions.Add(new($"Alert {insurer ?? "the insurer"}'s fraud team",
                "Refer the suspected fraud with the evidence pack (activity timeline and document findings). The insurer decides on any investigation.",
                true, Theo));
            actions.Add(new("Keep it confidential",
                $"Share the evidence only with the people who need it (Theo, Ben, Cara and {insurer ?? "the insurer"}'s fraud team), and never through the changed contact details.",
                false));
            return actions;
        }

        if (level == "Medium")
        {
            actions.Add(new("Verify before continuing",
                $"Confirm the request with {first} on {p.VerifiedContact} before anything changes or any money moves.",
                false));
            actions.Add(new("Resolve the document findings",
                "Ask for a certified copy or the original of any document flagged for review, and check it again.",
                false));
            actions.Add(new("Hold only with approval",
                "If the request can't be verified, recommend a hold — delaying it on financial-crime grounds needs approval.",
                true, Theo));
            return actions;
        }

        switch (p.ScenarioKey)
        {
            case "priya":
                actions.Add(new("Record KYC as complete on the Client Case", "Identity, income evidence and PEP / sanctions screening are all clear.", false));
                actions.Add(new("Release the case to Penny (Paraplanning)", "The needs analysis can start.", false));
                actions.Add(new("Standard monitoring once cover starts",
                    $"Verify any change of contact or bank details with a code to {first}'s verified mobile and a call-back.", false));
                break;
            case "george":
                actions.Add(new("Record the company KYC on the Client Case", "Northcote Custom Joinery Pty Ltd and both beneficial owners are identified and verified.", false));
                actions.Add(new("Release the case to Penny (Paraplanning)", "The business protection needs analysis can start.", false));
                actions.Add(new("Check the key person premium account at application",
                    "The company would own and pay for the key person policy — Uma confirms the premium account is in the company's name.", false));
                break;
            case "tom":
                actions.Add(new("Record the document checks on the Client Case", "Earnings evidence and the surgeon's report are genuine.", false));
                actions.Add(new("Release the claim to Ben (Claims)", "Ben can complete the claim pack and the benefit calculation.", false));
                actions.Add(new("Pay benefits only to an account in Tom's own name",
                    $"Verify any request to change it with a call-back on {mobile ?? "his verified number"}.", false));
                actions.Add(new("Keep the vulnerable-client flag", "Tom is under financial stress — Theo's vulnerable-client review goes ahead.", false));
                break;
            case "robert":
                actions.Add(new("Record the checks on the Client Case", "The death certificate and Robert's ID are genuine.", false));
                actions.Add(new("Release the claim to Ben (Claims)", "Robert is the nominated beneficiary, so the benefit is paid to him directly.", false));
                actions.Add(new("Pay only to an account in Robert's own name", "No extra documents or call-backs are needed.", false));
                actions.Add(new("Keep it gentle",
                    "Amelia stays the family's single contact, and every message to the family goes to Theo for approval.", true, Theo));
                break;
            default:
                actions.Add(new("Record the result on the Client Case", $"Note the screening and document results on {c.CaseNumber}.", false));
                actions.Add(new("Continue along the standard path", "No financial-crime action needed beyond routine checks.", false));
                break;
        }
        return actions;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string? InsurerFor(FinCrimeCaseProfile p) =>
        p.Parties.Select(x => InsurerRegex.Match(x.Role)).FirstOrDefault(m => m.Success)?.Value;

    private static string? LastDigits(string text) =>
        Regex.Match(text, @"ending (\d{3,4})") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Label without a trailing note in brackets, e.g. "(sent with the claim)".</summary>
    private static string ShortLabel(string label) =>
        Regex.Replace(label, @"\s*\([^()]*\)\s*$", string.Empty);

    private static int? ParseAgentScore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, @"\b(\d{1,3})\s*(?:/|out of)\s*100\b", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) && v <= 100 ? v : null;
    }

    private static string? ParseAgentLevel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var section = Regex.Match(text, @"Risk (?:Score|Level)[^\n]*(\n[^\n]*){0,3}", RegexOptions.IgnoreCase);
        var scope = section.Success ? section.Value : text;
        var m = Regex.Match(scope, @"\b(High|Medium|Low)\b", RegexOptions.IgnoreCase);
        return m.Success ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Value.ToLowerInvariant()) : null;
    }

    private static DateTime? ParseDate(string? raw) =>
        DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private static string? FormatDate(string? iso) =>
        ParseDate(iso) is { } d ? Dmy(d) : iso;

    private static string Dmy(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Money(decimal amount) => amount.ToString("$#,##0", CultureInfo.InvariantCulture);

    private static bool NamesMatch(string a, string b)
    {
        static string Norm(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9&]+", " ").Trim();
        return Norm(a) == Norm(b);
    }

    private static string FirstName(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;

    private static string LowerFirst(string s) =>
        string.IsNullOrEmpty(s) || (s.Length > 1 && char.IsUpper(s[1]))
            ? s
            : char.ToLowerInvariant(s[0]) + s[1..];

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');
}
