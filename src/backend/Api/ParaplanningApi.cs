using System.Text;
using System.Text.Json;
using LifeAdviceGuru.Agents;
using LifeAdviceGuru.App.Models;
using LifeAdviceGuru.App.Services;

namespace LifeAdviceGuru.App.Api;

record ParaplanningProcessRequest(string CaseNumber);

/// <summary>
/// HTTP endpoints behind the "Try it out" flow on the Paraplanning agent
/// page (<c>/agents/paraplanning</c>, Penny). They let the page:
///
/// 1. List the Client Cases in the <see cref="CaseStore"/> (the five seeded
///    scenario cases plus any opened live on the Onboarding page).
/// 2. Show one case and the rule and product documents that apply to it.
/// 3. Engage the Foundry <see cref="ParaplanningAgent"/> on the case and
///    return its narrative plus the advice checklist parsed from the trailing
///    <c>```json</c> block it emits (status, statusLabel, statusReason,
///    summary, items). The needs analysis, SoA outline, premiums, commission
///    and adviser sign-off always come from <see cref="ProductDocumentCatalog"/>;
///    when Foundry isn't configured, the call fails or the JSON block is
///    missing, the deterministic checklist and narrative are used too.
/// Claim cases get a short note pointing to the Claims Agent instead.
/// </summary>
public static class ParaplanningApi
{
    public static void MapParaplanningEndpoints(
        this WebApplication app,
        CaseStore store,
        GuruAgentFactory factory,
        ILogger logger)
    {
        // Dropdown source — every Client Case currently held in memory.
        app.MapGet("/paraplanning/cases", () =>
        {
            var cases = store.All().Select(r => new
            {
                caseNumber = r.CaseNumber,
                sampleId = r.SampleId,
                clientName = r.ClientName,
                clientNumber = r.ClientNumber,
                cover = r.Cover,
                requestType = r.RequestType,
                receivedDate = r.ReceivedDate,
                priority = r.Priority,
                applicable = ProductDocumentCatalog.BuildReport(r).Applicable,
                createdAt = r.CreatedAt
            });
            return Results.Ok(cases);
        });

        // Full Client Case, rendered on the page before the agent is engaged.
        app.MapGet("/paraplanning/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });

        // Every rule and product document in the catalogue.
        app.MapGet("/paraplanning/rules", () => Results.Ok(ProductDocumentCatalog.All));

        // The documents applied to one case, plus the clause ids its advice
        // checklist cites, so the page can show exactly which rules apply.
        app.MapGet("/paraplanning/rules/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });

            var report = ProductDocumentCatalog.BuildReport(record);
            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                applicable = report.Applicable,
                documents = ProductDocumentCatalog.DocumentsFor(report),
                checkedClauses = report.Items.SelectMany(i => ClauseTokens(i.ClauseRef)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            });
        });

        // Deterministic paraplanning report — needs analysis, checklist, SoA
        // outline, premiums, commission and sign-off.
        app.MapGet("/paraplanning/advice/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });
            return Results.Ok(ProjectReport(ProductDocumentCatalog.BuildReport(record)));
        });

        // Engage the Paraplanning Agent on the selected case. The prompt
        // carries the case, the figures on file, the applicable documents and
        // the checklist ids + clause refs so the agent's JSON lines up with the
        // UI. Send `Accept: text/event-stream` for a live SSE stream;
        // otherwise a single JSON envelope is returned.
        app.MapPost("/paraplanning/process", async (HttpContext httpContext, ParaplanningProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            var report = ProductDocumentCatalog.BuildReport(record);
            logger.LogInformation("Paraplanning process: caseNumber={CaseNumber} scenario={Scenario} applicable={Applicable}",
                Sanitize(record.CaseNumber), report.ScenarioKey, report.Applicable);

            if (!report.Applicable)
            {
                var envelope = NotApplicableEnvelope(record, report, factory.IsConfigured);
                if (AgentSseStreaming.WantsEventStream(httpContext))
                {
                    await AgentSseStreaming.StreamAsync(httpContext, null, string.Empty, (_, _) => envelope, logger, "Paraplanning Agent");
                    return Results.Empty;
                }
                return Results.Ok(envelope);
            }

            var documents = ProductDocumentCatalog.DocumentsFor(report);
            var prompt = BuildPrompt(record, report, documents);

            object BuildEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var rawText = trace?.Text;

                // Prefer the agent's own checklist; fall back to the
                // deterministic report when it is missing or unparseable.
                var checklist = TryExtractAgentChecklist(rawText, report)
                    ?? ProjectChecklist(report, "deterministic");

                // The narrative tab shows the prose only; the JSON block is
                // still available unmodified in agentRawOutput.text.
                var notes = StripTrailingJsonFence(rawText);
                var narrativeSource = "agent";
                if (string.IsNullOrWhiteSpace(notes))
                {
                    notes = BuildFallbackNarrative(report, documents, agentError, factory.IsConfigured);
                    narrativeSource = "deterministic";
                }

                return new
                {
                    caseNumber = record.CaseNumber,
                    applicable = true,
                    agentNotes = notes,
                    narrativeSource,
                    agentConfigured = factory.IsConfigured,
                    agentError,
                    agentInput = trace?.Input,
                    agentChecklist = checklist,
                    adviceReport = ProjectReport(report),
                    ruleDocumentIds = report.RuleDocumentIds,
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
                    (trace, error) => BuildEnvelope(trace, error ?? createError), logger, "Paraplanning Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? agentInvocationError = null;
            var (agent, creationError) = TryCreateAgent(factory, logger);
            if (agent is not null)
            {
                try
                {
                    traceResult = await agent.RunWithTraceAsync(prompt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Paraplanning Agent invocation failed; returning the offline paraplanning report");
                    agentInvocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, agentInvocationError ?? creationError));
        });
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// Create the live agent when Foundry is configured. A creation failure
    /// is logged and returned as an error so the stream still completes with
    /// the offline report.
    /// </summary>
    private static (GuruAgent? Agent, string? Error) TryCreateAgent(GuruAgentFactory factory, ILogger logger)
    {
        if (!factory.IsConfigured) return (null, null);
        try
        {
            return (factory.Create("paraplanning"), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Paraplanning Agent could not be created; returning the offline paraplanning report");
            return (null, ex.Message);
        }
    }

    private static object NotApplicableEnvelope(CaseRecord c, AdviceReport report, bool agentConfigured) => new
    {
        caseNumber = c.CaseNumber,
        applicable = false,
        note = report.NotApplicableNote,
        redirects = report.Redirects,
        agentNotes = $"**Not a paraplanning case.** {report.NotApplicableNote}",
        narrativeSource = "deterministic",
        agentConfigured,
        agentError = (string?)null,
        agentInput = (string?)null,
        agentChecklist = (object?)null,
        adviceReport = ProjectReport(report),
        ruleDocumentIds = report.RuleDocumentIds,
        agentRawOutput = (object?)null
    };

    internal static IEnumerable<string> ClauseTokens(string clauseRef) =>
        (clauseRef ?? string.Empty)
            .Split(new[] { ',', ';', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim('(', ')', '.'))
            .Where(t => t.Contains('-'));

    private static string StatusText(ChecklistStatus status) => status.ToString().ToLowerInvariant();

    private static object[] ProjectItems(AdviceReport report) =>
        report.Items.Select(i => (object)new
        {
            id = i.Id,
            category = i.Category,
            label = i.Label,
            status = StatusText(i.Status),
            finding = i.Finding,
            clauseRef = i.ClauseRef,
        }).ToArray();

    /// <summary>
    /// Project the deterministic checklist into the JSON shape the agent is
    /// asked to emit, so the page renders the checklist from one field
    /// whichever source produced it.
    /// </summary>
    private static object ProjectChecklist(AdviceReport report, string source) => new
    {
        caseNumber = report.CaseNumber,
        status = report.Status.ToString(),
        statusLabel = report.StatusLabel,
        statusReason = report.StatusReason,
        summary = report.Summary,
        items = ProjectItems(report),
        source,
    };

    /// <summary>The full deterministic report as the page consumes it.</summary>
    private static object ProjectReport(AdviceReport r) => new
    {
        caseNumber = r.CaseNumber,
        scenarioKey = r.ScenarioKey,
        applicable = r.Applicable,
        notApplicableNote = r.NotApplicableNote,
        redirects = r.Redirects,
        status = r.Status.ToString(),
        statusLabel = r.StatusLabel,
        statusReason = r.StatusReason,
        summary = r.Summary,
        needs = r.Needs,
        needsTotal = r.NeedsTotal,
        items = ProjectItems(r),
        soaOutline = r.SoaOutline,
        premiums = r.Premiums,
        premiumNote = r.PremiumNote,
        commission = r.Commission,
        signOff = r.SignOff,
        ruleDocumentIds = r.RuleDocumentIds,
        missingInformation = r.MissingInformation,
        plainEnglishExplanation = r.PlainEnglishExplanation,
        humanApproval = r.HumanApproval
    };

    /// <summary>
    /// Prompt sent to the Paraplanning Agent: the Client Case, the figures on
    /// the case file, the research, the text of every applicable document
    /// with clause ids and the checklist (ids, clause refs and preliminary
    /// findings). Contact details are left out — they aren't needed.
    /// </summary>
    private static string BuildPrompt(CaseRecord c, AdviceReport report, IReadOnlyList<ProductRuleDocument> documents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CLIENT CASE FOR PARAPLANNING");
        sb.AppendLine("============================");
        sb.AppendLine($"Case number: {c.CaseNumber}");
        sb.AppendLine($"Client: {c.ClientName}");
        sb.AppendLine($"Client number: {c.ClientNumber}");
        sb.AppendLine($"Cover: {c.Cover}");
        sb.AppendLine($"Request type: {c.RequestType}");
        sb.AppendLine($"Received: {c.ReceivedDate}");
        sb.AppendLine($"Adviser: {LifeAdviceFacts.Adviser}");
        sb.AppendLine($"Referred by: {(string.IsNullOrWhiteSpace(c.ReferredBy) ? "Existing client" : c.ReferredBy)}");
        sb.AppendLine($"Headline figure: {c.Amount}");
        sb.AppendLine($"Onboarding priority: {c.Priority} — {c.PriorityReason}");
        sb.AppendLine();
        sb.AppendLine("REQUEST SUMMARY");
        sb.AppendLine("---------------");
        sb.AppendLine(c.RequestSummary);
        sb.AppendLine();

        if (report.Needs.Count > 0)
        {
            sb.AppendLine("FIGURES ON THE CASE FILE (verify the working; don't change the client's facts)");
            sb.AppendLine("-------------------------------------------------------------------------------");
            foreach (var n in report.Needs)
            {
                sb.AppendLine($"{n.Title} — {n.LifeInsured}: " + string.Join("; ", n.Lines.Select(l => string.IsNullOrEmpty(l.Amount) ? l.Label : $"{l.Label} {l.Amount}")) +
                              $" → {n.Recommended} ({n.Basis})");
            }
            sb.AppendLine();
        }
        if (report.Premiums.Count > 0)
        {
            sb.AppendLine("RESEARCH AND PREMIUMS (from the Insurance Research Agent)");
            sb.AppendLine("--------------------------------------------------------");
            foreach (var p in report.Premiums)
                sb.AppendLine($"- {p.LifeInsured} — {p.Cover} {p.SumInsured}; {p.Structure}; {p.Premium} a year; paid by {p.PaidBy}.");
            sb.AppendLine($"- {report.PremiumNote}");
            if (report.Commission is { } com)
                sb.AppendLine($"- Commission: {com.Insurer}, {com.PremiumBasis}; upfront {com.Upfront}; ongoing {com.Ongoing}.");
            sb.AppendLine();
        }

        sb.AppendLine("RULE AND PRODUCT DOCUMENTS TO APPLY (cite them by title and clause id)");
        sb.AppendLine("--------------------------------------------------------------------");
        foreach (var doc in documents)
        {
            sb.AppendLine($"[{doc.Title}] — {doc.DocumentType}, {doc.EffectiveDate}");
            foreach (var s in doc.Sections)
                sb.AppendLine($"  {s.ClauseId} — {s.Title}: {s.Text} ({s.LegalReference})");
            sb.AppendLine();
        }

        sb.AppendLine("ADVICE CHECKLIST (one entry per check — keep every id and clauseRef)");
        sb.AppendLine("--------------------------------------------------------------------");
        foreach (var i in report.Items)
            sb.AppendLine($"- id={i.Id} | clauseRef={i.ClauseRef} | [{i.Category}] {i.Label} — preliminary status: {StatusText(i.Status)} — {i.Finding}");
        sb.AppendLine();
        sb.Append(
            "Prepare the paraplanning work for this Client Case as the Paraplanning Agent. Show the needs analysis working, the strategy and " +
            "ownership (inside or outside super, super-linking, who owns and pays), the best interests duty steps, the existing-cover and " +
            "replacement position, premiums and commission in dollars with the consent, and the draft SoA outline for Amelia. Use the " +
            "life_knowledge index for the full documents and the case files. For EVERY checklist item confirm pass, fail or info, say why and " +
            "cite its clause id. Use your standard output format. Then, at the very end of your reply, emit the fenced ```json block described " +
            "in your instructions with status, statusLabel, statusReason, summary and one entry in items for every checklist id above (keep " +
            "each id and clauseRef). You don't give advice — Amelia reviews and signs the SoA. Never show a full TFN or bank account number.");
        return sb.ToString();
    }

    /// <summary>
    /// Offline narrative in the agent's own output format, built from the
    /// deterministic report so the page still tells the full story when
    /// Foundry isn't configured or the call fails.
    /// </summary>
    private static string BuildFallbackNarrative(AdviceReport r, IReadOnlyList<ProductRuleDocument> documents, string? agentError, bool agentConfigured)
    {
        var sb = new StringBuilder();
        sb.AppendLine(!agentConfigured
            ? "_Foundry isn't configured here, so this is Penny's offline paraplanning draft — the same figures, rule documents and checklist the agent receives._"
            : !string.IsNullOrWhiteSpace(agentError)
                ? $"_The Paraplanning Agent didn't return a report ({agentError}). Showing Penny's offline paraplanning draft instead._"
                : "_The Paraplanning Agent's reply had no narrative. Showing Penny's offline paraplanning draft instead._");
        sb.AppendLine();
        sb.AppendLine($"**1. Status** — {r.StatusLabel}. {r.StatusReason}");
        sb.AppendLine();

        sb.AppendLine("**2. Needs analysis**");
        if (r.Needs.Count == 0)
            sb.AppendLine("- Waiting for the completed fact find.");
        foreach (var n in r.Needs)
        {
            sb.AppendLine();
            sb.AppendLine($"_{n.Title} — {n.LifeInsured}_");
            sb.AppendLine();
            sb.AppendLine("| Item | Amount |");
            sb.AppendLine("|---|--:|");
            foreach (var l in n.Lines.Where(x => x.Kind != "note" || !string.IsNullOrEmpty(x.Amount)))
            {
                var label = l.Kind is "total" or "result" ? $"**{Md(l.Label)}**" : Md(l.Label);
                var amount = l.Kind is "total" or "result" ? $"**{Md(l.Amount)}**" : Md(l.Amount);
                sb.AppendLine($"| {label} | {amount} |");
            }
            sb.AppendLine($"| **Recommended** | **{Md(n.Recommended)}** |");
            foreach (var l in n.Lines.Where(x => x.Kind == "note" && string.IsNullOrEmpty(x.Amount)))
                sb.AppendLine($"\n_{l.Label}_");
        }
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(r.NeedsTotal))
        {
            sb.AppendLine(r.NeedsTotal);
            sb.AppendLine();
        }

        void Section(string number, string title, params string[] categories)
        {
            sb.AppendLine($"**{number}. {title}**");
            var items = r.Items.Where(i => categories.Contains(i.Category)).ToList();
            if (items.Count == 0) sb.AppendLine("- Nothing to note.");
            foreach (var i in items)
                sb.AppendLine($"- **{i.Label}** ({i.ClauseRef}) — {i.Finding}");
            sb.AppendLine();
        }

        Section("3", "Strategy and ownership", "Strategy and ownership");
        Section("4", "Best interests duty", "Scope and facts", "Needs");
        Section("5", "Existing cover and replacement", "Existing cover");

        sb.AppendLine("**6. Premiums and commission**");
        foreach (var p in r.Premiums)
            sb.AppendLine($"- {p.LifeInsured} — {p.Cover} {p.SumInsured} ({p.Structure}): {p.Premium} a year, paid by {p.PaidBy}.");
        if (!string.IsNullOrWhiteSpace(r.PremiumNote))
            sb.AppendLine($"- {r.PremiumNote}");
        if (r.Commission is { } com)
        {
            sb.AppendLine($"- Commission from {com.Insurer} on {com.PremiumBasis}: upfront {com.Upfront}; ongoing {com.Ongoing}. {com.Basis}");
            sb.AppendLine($"- Consent: {com.Consent} {com.Gst}");
            sb.AppendLine($"- Clawback: {com.Clawback}");
        }
        sb.AppendLine();

        sb.AppendLine("**7. Advice checklist review**");
        foreach (var i in r.Items)
        {
            var icon = i.Status switch
            {
                ChecklistStatus.Pass => "✅",
                ChecklistStatus.Fail => "❌",
                _ => "ℹ️",
            };
            sb.AppendLine($"- {icon} **{i.Label}** ({i.ClauseRef}) — {StatusText(i.Status)}: {i.Finding}");
        }
        sb.AppendLine();

        sb.AppendLine("**8. Draft SoA outline**");
        if (r.SoaOutline.Count == 0)
            sb.AppendLine("- Drafted once the fact find is complete.");
        foreach (var s in r.SoaOutline)
            sb.AppendLine($"- **{s.Title}** — {string.Join(" ", s.Points)}");
        sb.AppendLine();

        sb.AppendLine("**9. Missing information**");
        if (r.MissingInformation.Count == 0)
            sb.AppendLine("- None.");
        foreach (var line in r.MissingInformation)
            sb.AppendLine("- " + line);
        sb.AppendLine();

        sb.AppendLine($"**10. Human approval required** — {r.SignOff.Heading}. {r.HumanApproval}");
        sb.AppendLine();
        sb.AppendLine($"_Plain English:_ {r.PlainEnglishExplanation}");
        sb.AppendLine();
        sb.Append("_Sources: " + string.Join("; ", documents.Select(d => d.Title)) + "._");
        return sb.ToString();
    }

    private static string Md(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    /// <summary>
    /// Locate the fenced <c>```json … ```</c> block in the agent's reply and
    /// parse it as the advice checklist. Takes the last block that parses,
    /// because the agent is told to put it at the very end.
    /// </summary>
    private static object? TryExtractAgentChecklist(string? agentText, AdviceReport fallback)
    {
        if (string.IsNullOrWhiteSpace(agentText)) return null;

        const string fence = "```";
        object? parsed = null;
        var search = 0;
        while (search < agentText.Length)
        {
            var openIdx = agentText.IndexOf(fence, search, StringComparison.Ordinal);
            if (openIdx < 0) break;

            var langStart = openIdx + fence.Length;
            var langEnd = agentText.IndexOf('\n', langStart);
            if (langEnd < 0) break;

            var lang = agentText.Substring(langStart, langEnd - langStart).Trim();
            var bodyStart = langEnd + 1;
            var closeIdx = agentText.IndexOf(fence, bodyStart, StringComparison.Ordinal);
            if (closeIdx < 0) break;

            if (string.Equals(lang, "json", StringComparison.OrdinalIgnoreCase))
            {
                var body = agentText.Substring(bodyStart, closeIdx - bodyStart).Trim();
                var maybe = TryParseChecklistJson(body, fallback);
                if (maybe is not null) parsed = maybe;
            }

            search = closeIdx + fence.Length;
        }

        // Some models drop the fence — try the outermost braces instead.
        if (parsed is null)
        {
            var firstBrace = agentText.IndexOf('{');
            var lastBrace = agentText.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                parsed = TryParseChecklistJson(agentText.Substring(firstBrace, lastBrace - firstBrace + 1), fallback);
        }

        return parsed;
    }

    /// <summary>
    /// Parse and normalise the agent's checklist JSON. Missing labels, clause
    /// refs or categories are filled from the deterministic item with the
    /// same id; statuses are coerced to pass / fail / info and the overall
    /// status to one of the three enum names.
    /// </summary>
    private static object? TryParseChecklistJson(string body, AdviceReport fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                return null;

            static string? Str(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var known = new Dictionary<string, AdviceChecklistItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in fallback.Items)
                known.TryAdd(item.Id, item);

            var items = new List<object>();
            foreach (var it in itemsEl.EnumerateArray())
            {
                if (it.ValueKind != JsonValueKind.Object) continue;
                var id = Str(it, "id") ?? string.Empty;
                known.TryGetValue(id, out var match);

                var label = Str(it, "label");
                if (string.IsNullOrWhiteSpace(label)) label = match?.Label ?? id;
                var clauseRef = Str(it, "clauseRef");
                if (string.IsNullOrWhiteSpace(clauseRef)) clauseRef = match?.ClauseRef ?? string.Empty;

                items.Add(new
                {
                    id,
                    category = match?.Category ?? "Agent findings",
                    label,
                    status = NormaliseStatus(Str(it, "status")),
                    finding = Str(it, "finding") ?? string.Empty,
                    clauseRef,
                });
            }
            if (items.Count == 0) return null;

            var statusLabel = Str(root, "statusLabel");
            var status = NormaliseAdviceStatus(Str(root, "status"))
                ?? NormaliseAdviceStatus(statusLabel)
                ?? fallback.Status;
            if (string.IsNullOrWhiteSpace(statusLabel))
                statusLabel = ProductDocumentCatalog.DefaultLabel(status);

            return new
            {
                caseNumber = fallback.CaseNumber,
                status = status.ToString(),
                statusLabel,
                statusReason = Str(root, "statusReason") ?? string.Empty,
                summary = Str(root, "summary") ?? string.Empty,
                items = items.ToArray(),
                source = "agent",
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormaliseStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "pass" or "passed" or "met" or "ok" or "yes" => "pass",
            "fail" or "failed" or "not met" or "no" => "fail",
            _ => "info",
        };
    }

    private static AdviceStatus? NormaliseAdviceStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = new string(value.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        if (key.StartsWith("ready") || key.Contains("draftsoa")) return AdviceStatus.ReadyForAdviser;
        if (key.StartsWith("need") || key.Contains("moreinfo")) return AdviceStatus.NeedMoreInfo;
        if (key.StartsWith("not")) return AdviceStatus.NotApplicable;
        return null;
    }

    /// <summary>
    /// Remove a trailing fenced <c>```json … ```</c> block from the agent's
    /// reply so the narrative tab shows only the prose write-up.
    /// </summary>
    private static string? StripTrailingJsonFence(string? agentText)
    {
        if (string.IsNullOrWhiteSpace(agentText)) return agentText;

        var trimmed = agentText.TrimEnd();
        if (!trimmed.EndsWith("```", StringComparison.Ordinal)) return agentText;

        var bodyEnd = trimmed.Length - 3;
        if (bodyEnd <= 0) return agentText;
        var openIdx = trimmed.LastIndexOf("```", bodyEnd - 1, StringComparison.Ordinal);
        if (openIdx < 0) return agentText;

        var langStart = openIdx + 3;
        var langEnd = trimmed.IndexOf('\n', langStart);
        if (langEnd < 0 || langEnd > bodyEnd) return agentText;
        var lang = trimmed.Substring(langStart, langEnd - langStart).Trim();
        if (!string.Equals(lang, "json", StringComparison.OrdinalIgnoreCase)) return agentText;

        return trimmed.Substring(0, openIdx).TrimEnd();
    }
}
