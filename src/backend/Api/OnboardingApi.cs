using System.Text;
using LifeAdviceGuru.Agents;
using LifeAdviceGuru.App.Models;
using LifeAdviceGuru.App.Services;

namespace LifeAdviceGuru.App.Api;

record OnboardingProcessRequest(string SampleId);

/// <summary>One in-good-order checklist row as confirmed (and possibly edited) by Olivia.</summary>
record OnboardingChecklistItem(string? Id, string? Label, string? Status, string? Note);

/// <summary>The Client Case form as confirmed (and possibly edited) by Olivia.</summary>
record OnboardingSubmitRequest(
    string SampleId,
    string? ClientName,
    string? ClientEmail,
    string? ClientPhone,
    string? ClientNumber,
    string? Cover,
    string? RequestType,
    string? ReceivedDate,
    string? AdviserName,
    string? ReferredBy,
    string? RequestSummary,
    string? Amount,
    string? PreferredContact,
    string? Priority,
    string? PriorityReason,
    IReadOnlyList<OnboardingChecklistItem>? Checklist);

/// <summary>
/// One hand-off for a newly opened Client Case: the agent page route id
/// (e.g. <c>paraplanning</c>), why the case goes there, and whether that team
/// picks it up next (<paramref name="Primary"/>) or is told in parallel.
/// </summary>
public record OnboardingRoute(string AgentId, string Reason, bool Primary);

/// <summary>
/// HTTP endpoints behind the "Try it out" tab on the Onboarding agent page
/// (<c>/agents/onboarding</c>). They let the page:
///
/// 1. List the five sample inbound requests (a referral email, an urgent
///    claim email, a call note or a death notification, each with its
///    attachment and what Life Advice Guru already holds on file) from
///    <see cref="RequestSampleCatalog"/>.
/// 2. Run a sample through the <see cref="OnboardingAgent"/> to produce the
///    Client Case fields, the fact find, the in-good-order checklist, the
///    priority and vulnerability flags and the recommended routing.
/// 3. Open the case: mint a <c>REQ-YYYYMMDD-XXXXXX</c> case number in the
///    in-memory <see cref="CaseStore"/> and return where it is routed next,
///    so every downstream agent page can pick it up by case number.
///
/// Every flow works offline: when Foundry isn't configured (or the call
/// fails) the page gets the same fields with a deterministic narrative in the
/// agent's output format.
/// </summary>
public static class OnboardingApi
{
    private static readonly string[] Priorities = ["Standard", "High", "Urgent"];

    /// <summary>How long the non-streaming path waits for the live agent before using the offline result.</summary>
    private static readonly TimeSpan AgentWait = TimeSpan.FromSeconds(90);

    public static void MapOnboardingEndpoints(
        this WebApplication app,
        CaseStore store,
        GuruAgentFactory factory,
        ILogger logger)
    {
        // Sample listing — the email body and attachment text are only
        // returned by the per-sample GET below to keep this payload small.
        app.MapGet("/onboarding/samples", () =>
        {
            var samples = RequestSampleCatalog.All.Select(s => new
            {
                id = s.Id,
                label = s.Label,
                requestType = s.RequestType,
                clientName = s.ClientName,
                clientEmail = s.ClientEmail,
                clientNumber = s.ClientNumber,
                emailSubject = s.EmailSubject,
                emailFrom = s.EmailFrom,
                emailDate = s.EmailDate,
                formFileName = s.FormFileName,
                isClaim = s.IsClaim,
                scenarioId = ScenarioCatalog.FindBySampleId(s.Id)?.Id
            });
            return Results.Ok(samples);
        });

        app.MapGet("/onboarding/samples/{id}", (string id) =>
        {
            var sample = RequestSampleCatalog.FindById(id);
            if (sample is null)
                return Results.NotFound(new { error = $"sample '{id}' not found" });

            return Results.Ok(new
            {
                id = sample.Id,
                label = sample.Label,
                requestType = sample.RequestType,
                clientName = sample.ClientName,
                clientEmail = sample.ClientEmail,
                clientNumber = sample.ClientNumber,
                emailSubject = sample.EmailSubject,
                emailFrom = sample.EmailFrom,
                emailDate = sample.EmailDate,
                emailBody = sample.EmailBody,
                formFileName = sample.FormFileName,
                formDocumentText = sample.FormDocumentText,
                clientFile = sample.ClientFile,
                isClaim = sample.IsClaim
            });
        });

        // Where a case of the given request type is routed after it opens.
        // Lets the page refresh the routing preview when Olivia changes the
        // request type on the Client Case form.
        app.MapGet("/onboarding/routing", (string? requestType) =>
            Results.Ok(new
            {
                requestType = CanonicalRequestType(requestType),
                routing = BuildRouting(requestType)
            }));

        // Engage the Onboarding Agent on a sample. The Client Case fields,
        // fact find, checklist and priority come from the catalogue entry so
        // every scenario plays out reliably; when Foundry is configured the
        // live agent's streamed narrative is shown alongside them, otherwise
        // an offline narrative in the agent's output format is returned.
        // Send `Accept: text/event-stream` for a live SSE stream.
        app.MapPost("/onboarding/process", async (HttpContext httpContext, OnboardingProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.SampleId))
                return Results.BadRequest(new { error = "sampleId is required" });

            var sample = RequestSampleCatalog.FindById(request.SampleId);
            if (sample is null)
                return Results.NotFound(new { error = $"sample '{request.SampleId}' not found" });

            logger.LogInformation("Onboarding process: sampleId={SampleId}", Sanitize(sample.Id));

            var prompt = BuildPrompt(sample);
            var routing = BuildRouting(sample.RequestType);

            object BuildEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var notes = trace?.Text;
                var narrativeSource = "agent";
                if (string.IsNullOrWhiteSpace(notes))
                {
                    notes = BuildFallbackNarrative(sample);
                    narrativeSource = "offline";
                }

                return new
                {
                    sampleId = sample.Id,
                    scenarioId = ScenarioCatalog.FindBySampleId(sample.Id)?.Id,
                    fields = sample.ExpectedFields,
                    factFindTitle = sample.FactFindTitle,
                    factFind = sample.ExpectedFactFind,
                    checklist = sample.ExpectedChecklist,
                    vulnerability = sample.VulnerabilityFlags,
                    priority = sample.ExpectedPriority,
                    priorityReason = sample.ExpectedPriorityReason,
                    routing,
                    agentNotes = notes,
                    narrativeSource,
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
            }

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                var (streamingAgent, createError) = TryCreateAgent(factory, logger);
                await AgentSseStreaming.StreamAsync(httpContext, streamingAgent, prompt,
                    (trace, error) => BuildEnvelope(trace, error ?? createError), logger, "Onboarding Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? agentInvocationError = null;
            if (factory.IsConfigured)
            {
                try
                {
                    var agent = factory.Create("onboarding");
                    traceResult = await agent.RunWithTraceAsync(prompt).WaitAsync(AgentWait, httpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Onboarding Agent invocation failed; using the offline extraction");
                    agentInvocationError = ex is TimeoutException
                        ? "the agent took too long to respond"
                        : ShortError(ex);
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, agentInvocationError));
        });

        // Olivia confirms the (possibly edited) Client Case form: mint a
        // REQ-YYYYMMDD-XXXXXX case number, keep the case in memory for the
        // other agent pages, and return where it is routed next.
        app.MapPost("/onboarding/submit", (OnboardingSubmitRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.SampleId))
                return Results.BadRequest(new { error = "sampleId is required" });

            var sample = RequestSampleCatalog.FindById(request.SampleId);
            if (sample is null)
                return Results.NotFound(new { error = $"sample '{request.SampleId}' not found" });

            if (string.IsNullOrWhiteSpace(request.ClientName))
                return Results.BadRequest(new { error = "clientName is required" });

            var expected = sample.ExpectedFields;
            var requestType = string.IsNullOrWhiteSpace(request.RequestType)
                ? sample.RequestType
                : CanonicalRequestType(request.RequestType);

            var fields = new RequestExtractedFields(
                ClientName: Clean(request.ClientName),
                ClientEmail: Clean(request.ClientEmail),
                ClientPhone: Clean(request.ClientPhone),
                ClientNumber: Clean(request.ClientNumber),
                Cover: Clean(request.Cover),
                RequestType: requestType,
                ReceivedDate: string.IsNullOrWhiteSpace(request.ReceivedDate) ? expected.ReceivedDate : Clean(request.ReceivedDate),
                AdviserName: string.IsNullOrWhiteSpace(request.AdviserName) ? expected.AdviserName : Clean(request.AdviserName),
                ReferredBy: Clean(request.ReferredBy),
                RequestSummary: Clean(request.RequestSummary),
                Amount: Clean(request.Amount),
                PreferredContact: Clean(request.PreferredContact));

            var priority = NormalisePriority(request.Priority) ?? sample.ExpectedPriority;
            var priorityReason = string.IsNullOrWhiteSpace(request.PriorityReason)
                ? sample.ExpectedPriorityReason
                : Clean(request.PriorityReason);

            var record = store.Add(sample.Id, fields, priority, priorityReason);

            var checklist = (request.Checklist is { Count: > 0 }
                    ? request.Checklist.Take(MaxChecklistItems).Select(n => new GoodOrderCheck(
                        Clean(n.Id), Clean(n.Label), NormaliseCheckStatus(n.Status), Clean(n.Note)))
                    : sample.ExpectedChecklist)
                .ToList();
            var outstanding = checklist
                .Where(n => n.Status == "action")
                .Select(n => new { id = n.Id, label = n.Label, note = n.Note })
                .ToArray();

            var routing = BuildRouting(requestType);

            logger.LogInformation("Onboarding submit: opened {CaseNumber} for sample {SampleId}",
                record.CaseNumber, Sanitize(sample.Id));

            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                createdAt = record.CreatedAt,
                clientName = record.ClientName,
                clientNumber = record.ClientNumber,
                requestType = record.RequestType,
                priority = record.Priority,
                priorityReason = record.PriorityReason,
                scenarioId = ScenarioCatalog.FindBySampleId(sample.Id)?.Id,
                checklistTotal = checklist.Count,
                checklistInGoodOrder = checklist.Count - outstanding.Length,
                outstandingChecklist = outstanding,
                routing
            });
        });

        // Read-only view of one case (diagnostics / downstream pages).
        app.MapGet("/onboarding/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// Create the live agent when Foundry is configured. A creation failure
    /// is logged and returned as an error so the stream still completes with
    /// the offline extraction.
    /// </summary>
    private static (GuruAgent? Agent, string? Error) TryCreateAgent(GuruAgentFactory factory, ILogger logger)
    {
        if (!factory.IsConfigured) return (null, null);
        try
        {
            return (factory.Create("onboarding"), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Onboarding Agent could not be created; using the offline extraction");
            return (null, ShortError(ex));
        }
    }

    /// <summary>First line of an exception message, kept short enough for the page's status line.</summary>
    private static string ShortError(Exception ex)
    {
        var message = (ex.Message ?? ex.GetType().Name).Trim();
        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.Length <= 160 ? firstLine : firstLine[..157] + "…";
    }

    private const int MaxFieldLength = 2000;
    private const int MaxChecklistItems = 30;

    private static string Clean(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= MaxFieldLength ? trimmed : trimmed[..MaxFieldLength];
    }

    private static string? NormalisePriority(string? value) =>
        Priorities.FirstOrDefault(p => string.Equals(p, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string NormaliseCheckStatus(string? value) =>
        string.Equals(value?.Trim(), "ok", StringComparison.OrdinalIgnoreCase) ? "ok" : "action";

    private static readonly string[] KnownRequestTypes =
    [
        RequestTypes.NewProtectionAdvice,
        RequestTypes.BusinessProtectionAdvice,
        RequestTypes.IncomeProtectionClaim,
        RequestTypes.DeathClaim,
        RequestTypes.TraumaClaimBankChange,
    ];

    /// <summary>Map a request type onto its canonical <see cref="RequestTypes"/> spelling (case-insensitive).</summary>
    private static string CanonicalRequestType(string? value)
    {
        var trimmed = Clean(value);
        return KnownRequestTypes.FirstOrDefault(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? trimmed;
    }

    /// <summary>
    /// Hand-offs for a newly opened case, by request type, following the
    /// practice's handoff rules: new advice goes to Paraplanning, claims go to
    /// Claims, every new client and any identity, document or bank-change
    /// concern goes to Financial Crime, and the Team Leader hears about
    /// vulnerable clients, death claims, blocks and $2m+ second reviews.
    /// Primary routes pick the case up next; the others are told in parallel.
    /// </summary>
    public static IReadOnlyList<OnboardingRoute> RoutesFor(string? requestType) =>
        CanonicalRequestType(requestType) switch
        {
            RequestTypes.NewProtectionAdvice =>
            [
                new("financial-crime", "KYC for a new client — verify the ID and payslip and screen for PEP and sanctions matches.", true),
                new("paraplanning", "Needs analysis for life, TPD, trauma and income protection — and stop existing super cover lapsing before new cover starts.", true),
                new("client-communications", "Acknowledge the request within 1 business day, say exactly what's still needed, and tell the referral partner only what the client agreed to share.", false),
            ],
            RequestTypes.BusinessProtectionAdvice =>
            [
                new("financial-crime", "KYC on the company and both beneficial owners.", true),
                new("paraplanning", "Business protection needs: buy-sell cover on the business valuation and key person cover.", true),
                new("team-leader", "Indicative total cover of $2,000,000 or more on one case — the advice needs Theo's second review before the application.", false),
            ],
            RequestTypes.IncomeProtectionClaim =>
            [
                new("claims", "Policy and evidence check, claim pack and benefit calculation — lodged only after a person approves.", true),
                new("financial-crime", "Check the earnings evidence and the surgeon's report when they arrive, before the claim is lodged.", false),
                new("team-leader", "Vulnerable client — injury and financial stress need a human touch, and Theo approves the claim before it's lodged.", false),
            ],
            RequestTypes.DeathClaim =>
            [
                new("team-leader", "Compassionate oversight — one named contact (Amelia), and Theo approves every message to the family.", true),
                new("claims", "Death claim — check the policy and the nominated beneficiary, look for other cover and build the claim pack; Theo approves before it's lodged.", true),
                new("financial-crime", "Quietly verify the certified death certificate and the claimant's ID.", false),
            ],
            RequestTypes.TraumaClaimBankChange =>
            [
                new("financial-crime", "Urgent — possible identity takeover. Check the documents and the contact-change timeline before anything else. Contact the client only on verified details.", true),
                new("claims", "Policy and evidence check only — nothing is lodged or paid while the request is verified; benefits are paid to the policy owner.", true),
                new("team-leader", "Any block, and any change to contact or bank details, needs Theo's approval.", false),
            ],
            _ =>
            [
                new("paraplanning", "Review the client's needs and existing cover for Amelia.", true),
            ],
        };

    private static IReadOnlyList<object> BuildRouting(string? requestType) =>
        RoutesFor(requestType)
            .Select(r =>
            {
                var agent = AgentCatalog.FindById(r.AgentId);
                return (object)new
                {
                    agentId = r.AgentId,
                    agentName = agent?.Name ?? r.AgentId,
                    department = agent?.Department ?? r.AgentId,
                    persona = agent?.Persona ?? string.Empty,
                    icon = agent?.Icon ?? "•",
                    accent = agent?.Accent ?? "#0ea5e9",
                    url = $"/agents/{r.AgentId}",
                    reason = r.Reason,
                    primary = r.Primary
                };
            })
            .ToList();

    /// <summary>
    /// Prompt sent to the Onboarding Agent: the inbound email or call note,
    /// the already-text-extracted attachment and what Life Advice Guru holds on file,
    /// plus the Client Case fields and request types it should use.
    /// </summary>
    private static string BuildPrompt(RequestSample sample) =>
        "INBOUND REQUEST — LIFE ADVICE GURU\n" +
        "=====================================\n" +
        $"From: {sample.EmailFrom}\n" +
        $"Date: {sample.EmailDate}\n" +
        $"Subject: {sample.EmailSubject}\n\n" +
        sample.EmailBody +
        $"\n\nATTACHMENT ({sample.FormFileName})\n" +
        "=====================================\n" +
        sample.FormDocumentText +
        "\n\nWHAT LIFE ADVICE GURU HOLDS ON FILE\n" +
        "=====================================\n" +
        sample.ClientFile +
        "\n\n" +
        "Process this inbound request as the Onboarding Agent. Identify the request type (one of: " +
        string.Join("; ", KnownRequestTypes) + "). Capture every Client Case field: client name, email, " +
        "phone, client number, cover, request type, received date, adviser (Amelia, Senior Risk Adviser), " +
        "referred by, request summary, headline amount and preferred contact — using only verified contact " +
        "details. Extract the " + sample.FactFindTitle.ToLowerInvariant() + ", run the in-good-order checklist, " +
        "set the priority (Standard / High / Urgent) with a reason, flag any vulnerability, and recommend the " +
        "routing — in your standard output format. Keep health information to the minimum the next team " +
        "needs, and mask any TFN or bank account number.";

    /// <summary>
    /// Offline narrative in the Onboarding Agent's output format, built from
    /// the catalogue entry so the page still tells the story when Foundry
    /// isn't configured or the call fails.
    /// </summary>
    private static string BuildFallbackNarrative(RequestSample sample)
    {
        var f = sample.ExpectedFields;

        var sb = new StringBuilder();
        sb.AppendLine($"**1. Request Summary** — {f.RequestType} for {f.ClientName}, received {FormatDate(f.ReceivedDate)}. " +
                      $"From: {sample.EmailFrom}. Attachment: *{sample.FormFileName}*. Referred by: {f.ReferredBy}.");
        sb.AppendLine();
        sb.AppendLine("**2. Client Case Fields**");
        sb.AppendLine($"- Client: {f.ClientName} · {f.ClientNumber}");
        sb.AppendLine($"- Verified contact: {f.ClientEmail} · {f.ClientPhone}");
        sb.AppendLine($"- Cover: {f.Cover}");
        sb.AppendLine($"- Headline amount: {f.Amount}");
        sb.AppendLine($"- Adviser: {f.AdviserName}");
        sb.AppendLine($"- Preferred contact: {f.PreferredContact}");
        sb.AppendLine($"- Summary: {f.RequestSummary}");
        sb.AppendLine();
        sb.AppendLine($"**3. {sample.FactFindTitle}**");
        foreach (var item in sample.ExpectedFactFind)
        {
            var value = item.Sensitive ? $"{item.Value} *(health information — shared only with the people who need it)*" : item.Value;
            sb.AppendLine($"- {item.Label}: {value}");
        }
        sb.AppendLine();
        sb.AppendLine("**4. In-Good-Order Checklist** *(Guide — in-good-order checklist for advice and claims)*");
        foreach (var n in sample.ExpectedChecklist)
        {
            var icon = n.Status == "ok" ? "✅" : "⚠️";
            sb.AppendLine($"- {icon} **{n.Label}** — {n.Note}");
        }
        sb.AppendLine();
        sb.AppendLine($"**5. Priority & Vulnerability** — **{sample.ExpectedPriority}.** {sample.ExpectedPriorityReason}");
        sb.AppendLine(sample.VulnerabilityFlags.Count == 0
            ? "- Vulnerability flags: none."
            : $"- Vulnerability flags: {string.Join("; ", sample.VulnerabilityFlags)} — for human handling.");
        sb.AppendLine();
        sb.AppendLine("**6. Routing & Next Steps**");
        foreach (var route in RoutesFor(sample.RequestType))
        {
            var agent = AgentCatalog.FindById(route.AgentId);
            var name = agent is null ? route.AgentId : $"{agent.Icon} {agent.Department} ({agent.Persona})";
            sb.AppendLine($"- {(route.Primary ? "Next" : "Also told")}: {name} — {route.Reason}");
        }
        var outstanding = sample.ExpectedChecklist.Where(n => n.Status != "ok").Select(n => n.Label).ToList();
        if (outstanding.Count == 0)
            sb.AppendLine("- Nothing outstanding — the request is in good order.");
        else if (sample.RequestType == RequestTypes.TraumaClaimBankChange)
            sb.AppendLine($"- Still to resolve: {string.Join("; ", outstanding)}. Don't reply to the request — Financial Crime and Theo decide first, and only the verified contact details are used.");
        else
            sb.AppendLine($"- Still to resolve: {string.Join("; ", outstanding)}. Nothing is rejected — Life Advice Guru says exactly what's missing within 1 business day and keeps everything else moving.");
        sb.Append("- Olivia confirms the Client Case before it opens. The agent gathers and checks — Amelia gives the advice, and people approve every decision that matters.");
        return sb.ToString();
    }

    /// <summary>Format a <c>yyyy-MM-dd</c> date the Australian way, e.g. "Tue 3 Nov 2026".</summary>
    private static string FormatDate(string isoDate) =>
        DateOnly.TryParseExact(isoDate, "yyyy-MM-dd", out var d)
            ? d.ToString("ddd d MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-AU"))
            : isoDate;
}
