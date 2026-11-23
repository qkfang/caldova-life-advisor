using System.Text;
using LifeAdviceGuru.Agents;
using LifeAdviceGuru.App.Mcp;
using LifeAdviceGuru.App.Models;
using LifeAdviceGuru.App.Services;

namespace LifeAdviceGuru.App.Api;

record UnderwritingProcessRequest(string CaseNumber);

/// <summary>
/// HTTP endpoints behind the "Try it out" flow on the Underwriting &amp; New
/// Business agent page (<c>/agents/underwriting</c>, Uma):
///
/// 1. <c>GET /underwriting/cases</c> and <c>GET /underwriting/cases/{caseNumber}</c>
///    — the Client Cases held in <see cref="CaseStore"/>.
/// 2. <c>GET /underwriting/plan/{caseNumber}</c> — the insurer, the evidence
///    needed for each life insured and the disclosure checklist, shown
///    before the agent is engaged.
/// 3. <c>POST /underwriting/process</c> — engages the Underwriting Agent and
///    generates the application and disclosure summary PDF. The plan
///    (<see cref="InsurerProductCatalog"/>, <see cref="InsurerDirectory"/>)
///    and the PDF (<see cref="ApplicationPdfService"/>) are always produced
///    deterministically, so the page completes without a Foundry connection;
///    when Foundry is configured the agent's streamed narrative is shown
///    alongside.
/// 4. <c>GET /underwriting/application/{id}.pdf</c> — download the PDF
///    (from <c>/underwriting/process</c> or the
///    <c>underwriting_generateApplicationSummaryPdf</c> MCP tool).
/// </summary>
public static class UnderwritingApi
{
    public static void MapUnderwritingEndpoints(
        this WebApplication app,
        CaseStore store,
        GuruAgentFactory factory,
        ILogger logger)
    {
        var pdfService = app.Services.GetRequiredService<ApplicationPdfService>();
        var agentOptions = app.Services.GetRequiredService<GuruAgentOptions>();
        var mcpEnabled = !string.IsNullOrWhiteSpace(agentOptions.AppMcpUrl);

        // Every Client Case in the shared store; claim cases are flagged so
        // the page can point them to the Claims Agent.
        app.MapGet("/underwriting/cases", () =>
            Results.Ok(store.All().Select(c =>
            {
                var plan = InsurerProductCatalog.Build(c);
                return new
                {
                    caseNumber = c.CaseNumber,
                    clientName = c.ClientName,
                    clientNumber = c.ClientNumber,
                    cover = c.Cover,
                    requestType = c.RequestType,
                    receivedDate = c.ReceivedDate,
                    amount = c.Amount,
                    priority = c.Priority,
                    applicable = plan.Applicable,
                    createdAt = c.CreatedAt
                };
            })));

        app.MapGet("/underwriting/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });

        // The APL directory (diagnostics and the page's insurer card).
        app.MapGet("/underwriting/insurers", (string? query) =>
            Results.Ok(UnderwritingMcpTools.LookupInsurerResult(query)));

        // Deterministic plan for a case — insurer, requirements, disclosure
        // checklist, timeline and decision — without engaging the agent.
        app.MapGet("/underwriting/plan/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });
            return Results.Ok(PlanView(InsurerProductCatalog.Build(record)));
        });

        // Engage the Underwriting Agent on a case. A deterministic plan and
        // PDF are always returned; with Foundry configured the live agent's
        // narrative is streamed (`Accept: text/event-stream`) or returned in
        // `agentNotes`. Claim cases get a short note pointing to Claims.
        app.MapPost("/underwriting/process", async (HttpContext httpContext, UnderwritingProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            var plan = InsurerProductCatalog.Build(record);
            logger.LogInformation("Underwriting process: caseNumber={CaseNumber} scenario={Scenario} applicable={Applicable} mcp={Mcp}",
                Sanitize(record.CaseNumber), plan.ScenarioKey, plan.Applicable, mcpEnabled);

            if (!plan.Applicable)
            {
                var envelope = NotApplicableEnvelope(record, plan, factory.IsConfigured);
                if (AgentSseStreaming.WantsEventStream(httpContext))
                {
                    await AgentSseStreaming.StreamAsync(httpContext, null, string.Empty, (_, _) => envelope, logger, "Underwriting Agent");
                    return Results.Empty;
                }
                return Results.Ok(envelope);
            }

            var pdf = pdfService.Generate(InsurerProductCatalog.ToPdfInput(record, plan));
            var pdfUrl = UnderwritingMcpTools.RelativeUrl(pdf);
            var prompt = BuildPrompt(record, plan, mcpEnabled, pdf, UnderwritingMcpTools.BaseUrl(app.Configuration));

            string? createError = null;

            object BuildEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var error = agentError ?? createError;
                var notes = trace?.Text;
                var narrativeSource = "agent";
                if (string.IsNullOrWhiteSpace(notes))
                {
                    notes = BuildFallbackNarrative(record, plan, pdfUrl, pdf.FileName, error, factory.IsConfigured);
                    narrativeSource = "deterministic";
                }

                return new
                {
                    caseNumber = record.CaseNumber,
                    clientName = record.ClientName,
                    requestType = record.RequestType,
                    applicable = true,
                    plan = PlanView(plan),
                    applicationPdfUrl = pdfUrl,
                    applicationPdfFileName = pdf.FileName,
                    applicationPdfBytes = pdf.Content.Length,
                    mcpEnabled,
                    agentNotes = notes,
                    narrativeSource,
                    agentConfigured = factory.IsConfigured,
                    agentError = error,
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

            GuruAgent? agent = null;
            if (factory.IsConfigured)
            {
                try
                {
                    agent = factory.Create("underwriting");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Underwriting Agent could not be created; using the deterministic plan");
                    createError = ex.Message;
                }
            }

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                await AgentSseStreaming.StreamAsync(httpContext, agent, prompt, BuildEnvelope, logger, "Underwriting Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? invocationError = null;
            if (agent is not null)
            {
                try
                {
                    traceResult = await agent.RunWithTraceAsync(prompt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Underwriting Agent invocation failed; using the deterministic plan");
                    invocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, invocationError));
        });

        // Download the application and disclosure summary generated by
        // /underwriting/process or the underwriting_generateApplicationSummaryPdf MCP tool.
        app.MapGet("/underwriting/application/{id}.pdf", (string id) =>
        {
            var pdf = pdfService.Get(id);
            return pdf is null
                ? Results.NotFound(new { error = $"application summary '{id}' not found" })
                : Results.File(pdf.Content, "application/pdf", pdf.FileName);
        });
    }

    private static object NotApplicableEnvelope(CaseRecord c, UnderwritingPlan plan, bool agentConfigured) => new
    {
        caseNumber = c.CaseNumber,
        clientName = c.ClientName,
        requestType = c.RequestType,
        applicable = false,
        note = plan.NotApplicableNote,
        redirects = plan.Redirects,
        plan = PlanView(plan),
        agentNotes = $"**Not an underwriting case.** {plan.NotApplicableNote}",
        narrativeSource = "deterministic",
        agentConfigured,
        agentError = (string?)null,
        agentInput = (string?)null,
        agentRawOutput = (object?)null
    };

    // ─────────────────────────────────────────────────────────────────────
    // Prompt
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildPrompt(CaseRecord c, UnderwritingPlan plan, bool mcpEnabled, ApplicationPdfRecord pdf, string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CLIENT CASE FOR UNDERWRITING & NEW BUSINESS");
        sb.AppendLine("===========================================");
        sb.AppendLine($"Case number   : {c.CaseNumber}");
        sb.AppendLine($"Client        : {c.ClientName}");
        sb.AppendLine($"Client number : {c.ClientNumber}");
        sb.AppendLine($"Request type  : {c.RequestType}");
        sb.AppendLine($"Received      : {c.ReceivedDate}");
        sb.AppendLine($"Adviser       : {LifeAdviceFacts.Adviser}");
        sb.AppendLine($"Referred by   : {plan.ReferredBy}");
        sb.AppendLine($"Priority      : {c.Priority} — {c.PriorityReason}");
        sb.AppendLine();
        sb.AppendLine("REQUEST SUMMARY");
        sb.AppendLine("---------------");
        sb.AppendLine(c.RequestSummary);
        sb.AppendLine();
        sb.AppendLine($"APPLICATION ON FILE (as at {plan.AsAtLabel}) — verify it, then explain it");
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine($"Status: {plan.OverallStatus}");
        sb.AppendLine($"Insurer: {plan.Insurer?.Name ?? "to be confirmed from the signed SoA"}; lodged: {plan.LodgedOn}");
        foreach (var l in plan.LivesInsured)
            sb.AppendLine($"- Life insured: {l.Name} ({l.ClientNumber}), age {l.Age}, {l.Occupation}{(l.Smoker is null ? string.Empty : $", {l.Smoker}")}.");
        foreach (var cv in plan.Covers)
            sb.AppendLine($"- Cover: {cv.LifeInsured} — {cv.Cover} {cv.SumInsured}; {cv.Ownership}; premium {InsurerProductCatalog.Money(cv.PremiumStandard)} quoted" +
                          (cv.PremiumFinal != cv.PremiumStandard ? $", {InsurerProductCatalog.Money(cv.PremiumFinal)} after terms" : string.Empty) +
                          $"; terms: {cv.Terms}{(cv.PolicyNumber is null ? string.Empty : $"; policy {cv.PolicyNumber}")}.");
        if (!string.IsNullOrWhiteSpace(plan.PremiumNote))
            sb.AppendLine($"- Premiums: {plan.PremiumNote}");
        foreach (var d in plan.Disclosures)
            sb.AppendLine($"- Disclosure [{d.Status}] {d.Label}: {d.Detail}");
        foreach (var e in plan.Timeline)
            sb.AppendLine($"- Timeline {e.Date}: {e.Event} ({e.Who}){(e.HumanDecision ? " — human decision" : string.Empty)}");
        if (plan.Decisions.Count > 0)
            sb.AppendLine($"- Insurer decision: {plan.DecisionSummary}");
        foreach (var d in plan.Decisions)
            sb.AppendLine($"  · {d.LifeInsured} — {d.Cover}: {d.Decision}.");
        sb.AppendLine($"- Client decision: {plan.ClientDecision.Status} — {plan.ClientDecision.Who}{(plan.ClientDecision.Date is null ? string.Empty : $", {plan.ClientDecision.Date}")}.");
        sb.AppendLine($"- {plan.ClientDecision.CoolingOff}");
        sb.AppendLine();

        var lookupQuery = plan.Insurer?.Name ?? "all";
        if (mcpEnabled)
        {
            sb.AppendLine("TASK (use your MCP tools)");
            sb.AppendLine("-------------------------");
            sb.AppendLine($"1. Call underwriting_lookupInsurer(query: \"{lookupQuery}\") for the insurer's contacts, service levels and thresholds.");
            var step = 2;
            foreach (var l in plan.LivesInsured)
            {
                sb.AppendLine($"{step++}. Call underwriting_checkRequirements(insurer: \"{plan.Insurer?.Name}\", age: {l.Age}, lifeSumInsured: {UnderwritingMcpTools.Amount(l.LifeSumInsured)}, " +
                              $"tpdSumInsured: {UnderwritingMcpTools.Amount(l.TpdSumInsured)}, traumaSumInsured: {UnderwritingMcpTools.Amount(l.TraumaSumInsured)}, " +
                              $"incomeProtectionMonthly: {UnderwritingMcpTools.Amount(l.IncomeProtectionMonthly)}, businessCover: \"{l.BusinessCover ?? string.Empty}\", " +
                              $"selfEmployed: {(l.SelfEmployed ? "true" : "false")}, lifeInsured: \"{l.Name}\").");
            }
            sb.AppendLine($"{step++}. Call underwriting_generateApplicationSummaryPdf(caseNumber: \"{c.CaseNumber}\") and put its downloadUrl in section 5.");
            sb.AppendLine($"{step}. Write your report in the required output format.");
            sb.AppendLine();
            sb.AppendLine($"(This app has already generated a summary for its own case file: {pdf.FileName}. Use the downloadUrl from your own tool call.)");
        }
        else
        {
            sb.AppendLine("MCP TOOL RESULTS (pre-resolved for you in this session — do NOT call the tools again)");
            sb.AppendLine("=====================================================================================");
            sb.AppendLine($"▶ underwriting_lookupInsurer(\"{lookupQuery}\") result:");
            sb.AppendLine(UnderwritingMcpTools.Serialize(UnderwritingMcpTools.LookupInsurerResult(lookupQuery)));
            sb.AppendLine();
            foreach (var check in InsurerProductCatalog.RequirementChecks(plan))
            {
                sb.AppendLine($"▶ underwriting_checkRequirements(\"{check.Insurer}\", age {check.Age}, life {UnderwritingMcpTools.Amount(check.LifeSumInsured)}, …) for {check.LifeInsured}:");
                sb.AppendLine(UnderwritingMcpTools.Serialize(check));
                sb.AppendLine();
            }
            sb.AppendLine("▶ underwriting_generateApplicationSummaryPdf(...) result:");
            sb.AppendLine(UnderwritingMcpTools.Serialize(UnderwritingMcpTools.DownloadPayload(pdf, baseUrl)));
            sb.AppendLine();
            sb.AppendLine("TASK");
            sb.AppendLine("----");
            sb.AppendLine("Using the application on file and the tool results above, write your report in the required output format.");
            sb.AppendLine("Put the PDF's relativeUrl in section 5 as a markdown link: [Download the application and disclosure summary](<relativeUrl>).");
        }

        sb.AppendLine();
        sb.AppendLine("Reminders: explain the duty to take reasonable care before any health question; never accept terms for the client");
        sb.AppendLine("(the client decides with Amelia); never cancel or reduce existing cover before the new cover is accepted and in force;");
        sb.AppendLine("keep health detail out of anything shared with referral partners; plain English; cite \"Underwriting and the duty to");
        sb.AppendLine("take reasonable care\" and the insurer's PDS extract by title.");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Offline narrative (used when the live agent isn't available)
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildFallbackNarrative(
        CaseRecord c, UnderwritingPlan plan, string pdfUrl, string pdfFileName, string? agentError, bool agentConfigured)
    {
        var preface = !agentConfigured
            ? "_Foundry isn't configured here, so this is Uma's offline underwriting plan — the same APL directory, requirement checks and PDF the MCP tools use._"
            : !string.IsNullOrWhiteSpace(agentError)
                ? $"_The Underwriting Agent didn't return a report ({agentError}). Showing Uma's offline underwriting plan instead._"
                : "_The Underwriting Agent finished without narrative text (it may only have called tools). Showing Uma's offline underwriting plan instead._";

        var sb = new StringBuilder();
        sb.AppendLine(preface);
        sb.AppendLine();
        sb.AppendLine($"### Underwriting & new business — {c.CaseNumber} · {string.Join(" and ", plan.LivesInsured.Select(l => l.Name).DefaultIfEmpty(c.ClientName))}");
        sb.AppendLine();
        sb.AppendLine($"**{plan.OverallStatus}.** {plan.Headline}");
        sb.AppendLine();

        sb.AppendLine("#### 1. Application");
        if (plan.Insurer is null)
        {
            sb.AppendLine("The insurer, covers and sums insured come from the signed Statement of Advice — not lodged yet.");
        }
        else
        {
            sb.AppendLine($"**{plan.Insurer.Name}** ({plan.Insurer.PolicyPrefix}, on Life Advice Guru's APL) — lodged {plan.LodgedOn}.");
            sb.AppendLine();
            sb.AppendLine("| Life insured | Cover | Sum insured | Ownership | Premium (yr 1) |");
            sb.AppendLine("|---|---|--:|---|--:|");
            foreach (var cv in plan.Covers)
            {
                var premium = cv.PremiumFinal == cv.PremiumStandard
                    ? InsurerProductCatalog.Money(cv.PremiumFinal)
                    : $"{InsurerProductCatalog.Money(cv.PremiumFinal)} (quoted {InsurerProductCatalog.Money(cv.PremiumStandard)})";
                sb.AppendLine($"| {Md(cv.LifeInsured)} | {Md(cv.Cover)} | {Md(cv.SumInsured)} | {Md(cv.Ownership)} | {premium} |");
            }
            sb.AppendLine();
            sb.AppendLine(plan.PremiumNote);
        }
        sb.AppendLine();

        sb.AppendLine("#### 2. Duty to take reasonable care");
        sb.AppendLine($"> {plan.DutyOfCareScript}");
        sb.AppendLine();
        sb.AppendLine($"{plan.DutyAcknowledgement} _(Underwriting and the duty to take reasonable care)_");
        sb.AppendLine();

        sb.AppendLine("#### 3. Requirements");
        if (plan.Requirements.Count == 0)
        {
            sb.AppendLine("Checked with the insurer once the application is prepared from the signed SoA.");
        }
        else
        {
            sb.AppendLine("| Life insured | Requirement | Why | Status |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var r in plan.Requirements)
                sb.AppendLine($"| {Md(r.LifeInsured)} | {Md(r.Requirement)} | {Md(r.Reason)} | **{Md(r.Status)}**{(r.Date is null ? string.Empty : $" {Md(r.Date)}")} |");
        }
        sb.AppendLine();

        sb.AppendLine("#### 4. Disclosure checklist");
        foreach (var d in plan.Disclosures)
        {
            var icon = d.Status switch { "done" => "✅", "disclosed" => "📝", "action" => "⚠️", _ => "ℹ️" };
            sb.AppendLine($"- {icon} **{d.Label}** — {d.Detail}");
        }
        sb.AppendLine();

        sb.AppendLine("#### 5. Application summary PDF");
        sb.AppendLine($"[Download the application and disclosure summary]({pdfUrl}) — `{pdfFileName}` · status **{plan.ApplicationStatus}**.");
        sb.AppendLine();

        sb.AppendLine("#### 6. Underwriting timeline");
        foreach (var e in plan.Timeline)
            sb.AppendLine($"- **{e.Date}** — {e.Event}{(e.HumanDecision ? " _(human decision)_" : string.Empty)}");
        if (plan.Insurer is not null)
            sb.AppendLine($"- _{plan.Insurer.Name} service levels: {string.Join(" ", plan.Insurer.ServiceLevels)}_");
        sb.AppendLine();

        sb.AppendLine("#### 7. Decision in plain English");
        if (plan.Decisions.Count == 0)
            sb.AppendLine(plan.DecisionSummary);
        foreach (var d in plan.Decisions)
            sb.AppendLine($"- **{d.LifeInsured} — {d.Cover}: {d.Decision}.** {d.PlainEnglish}");
        sb.AppendLine();

        sb.AppendLine("#### 8. Client decision");
        var cd = plan.ClientDecision;
        sb.AppendLine($"**{cd.Status}** — {cd.Who}{(cd.Date is null ? string.Empty : $", {cd.Date}")}. {cd.Summary}");
        foreach (var p in cd.Points)
            sb.AppendLine($"- {p}");
        if (!string.IsNullOrWhiteSpace(cd.CoolingOff))
            sb.AppendLine($"- {cd.CoolingOff}");
        if (!string.IsNullOrWhiteSpace(cd.ExistingCoverNote))
            sb.AppendLine($"- {cd.ExistingCoverNote}");
        sb.AppendLine();

        sb.AppendLine("#### 9. Human Approval Required");
        sb.AppendLine(plan.HumanApprovalReason);
        sb.AppendLine();
        var pds = plan.Insurer is null ? null : ProductDocumentCatalog.PdsTitleFor(plan.Insurer.Name);
        sb.Append($"_Sources: Underwriting and the duty to take reasonable care{(pds is null ? string.Empty : $"; {pds}")}; Directory — insurers on the approved product list._");
        return sb.ToString();
    }

    private static object PlanView(UnderwritingPlan plan) => new
    {
        caseNumber = plan.CaseNumber,
        scenarioKey = plan.ScenarioKey,
        applicable = plan.Applicable,
        notApplicableNote = plan.NotApplicableNote,
        redirects = plan.Redirects,
        asAtLabel = plan.AsAtLabel,
        overallStatus = plan.OverallStatus,
        statusTone = plan.StatusTone,
        headline = plan.Headline,
        referredBy = plan.ReferredBy,
        lodgedOn = plan.LodgedOn,
        insurer = plan.Insurer is null ? null : InsurerDirectory.ToView(plan.Insurer),
        livesInsured = plan.LivesInsured,
        covers = plan.Covers,
        annualPremiumStandard = plan.AnnualPremiumStandard,
        annualPremiumFinal = plan.AnnualPremiumFinal,
        premiumNote = plan.PremiumNote,
        requirementChecks = InsurerProductCatalog.RequirementChecks(plan),
        requirements = plan.Requirements,
        dutyOfCareScript = plan.DutyOfCareScript,
        dutyAcknowledgement = plan.DutyAcknowledgement,
        disclosures = plan.Disclosures,
        timeline = plan.Timeline,
        decisions = plan.Decisions,
        decisionSummary = plan.DecisionSummary,
        clientDecision = plan.ClientDecision,
        applicationStatus = plan.ApplicationStatus,
        notes = plan.Notes,
        humanApprovalRequired = plan.HumanApprovalRequired,
        humanApprovalReason = plan.HumanApprovalReason
    };

    private static string Md(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
