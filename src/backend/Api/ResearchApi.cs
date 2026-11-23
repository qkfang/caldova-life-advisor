using System.Text;
using System.Text.RegularExpressions;
using CaldovaLife.Agents;
using CaldovaLife.App.Mcp;
using CaldovaLife.App.Services;
using static CaldovaLife.App.Mcp.ResearchMcpTools;

namespace CaldovaLife.App.Api;

record ResearchProcessRequest(string CaseNumber);
record ResearchWorkbookRequest(string CaseNumber, string? Notes);
record ResearchChatRequest(string? PreviousResponseId, string Message, string? CaseNumber);

/// <summary>
/// HTTP endpoints behind the "Try it out" flow on the Insurance Research
/// Agent page (<c>/agents/insurance-research</c>, Ivy — Insurance Research
/// Analyst):
///
/// 1. <c>GET /research/cases</c> and <c>GET /research/cases/{caseNumber}</c>
///    — every Client Case in <see cref="CaseStore"/> with its research brief
///    (advice: the cover to compare and the definitions that matter; claim:
///    the policy on file) and the APL product sheets.
/// 2. <c>POST /research/process</c> — engages the Insurance Research Agent.
///    The comparison (advice cases) or policy wording lookup (claim cases) is
///    always computed in-process with the same code as the MCP tools, so the
///    page shows the table, chart, definitions and projection even when
///    Foundry isn't configured. With Foundry the agent's streamed report is
///    shown; with <c>APP_MCP_URL</c> set the agent calls the
///    <c>research_*</c> tools itself, otherwise their results are
///    pre-resolved into its prompt.
/// 3. <c>POST /research/workbook</c> — builds the comparison workbook (the
///    same code as <c>research_generateComparisonWorkbook</c>).
/// 4. <c>POST /research/chat</c> — follow-up questions in the same Foundry
///    conversation; answered from the research when the live agent isn't
///    available.
/// </summary>
public static class ResearchApi
{
    private const string AgentRole = "insurance-research";
    private const string AgentLabel = "Insurance Research Agent";

    public static void MapResearchEndpoints(
        this WebApplication app,
        CaseStore store,
        CaldovaAgentFactory factory,
        ILogger logger)
    {
        var agentOptions = app.Services.GetRequiredService<CaldovaAgentOptions>();
        var tools = ActivatorUtilities.CreateInstance<ResearchMcpTools>(app.Services);
        var mcpEnabled = !string.IsNullOrWhiteSpace(agentOptions.AppMcpUrl);

        // Every Client Case in the shared store — advice cases get a comparison,
        // claim cases a policy wording lookup.
        app.MapGet("/research/cases", () =>
            Results.Ok(store.All().Select(c =>
            {
                var brief = tools.ResolveBrief(c);
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
                    createdAt = c.CreatedAt,
                    kind = brief.Kind,
                    researchTitle = brief.Title
                };
            })));

        // One case, its research brief and the APL product sheets.
        app.MapGet("/research/cases/{caseNumber}", async (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });

            var brief = tools.ResolveBrief(record);
            var sheets = await tools.LoadAplSheetsAsync();
            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                clientName = record.ClientName,
                clientNumber = record.ClientNumber,
                cover = record.Cover,
                requestType = record.RequestType,
                receivedDate = record.ReceivedDate,
                adviserName = record.AdviserName,
                referredBy = record.ReferredBy,
                requestSummary = record.RequestSummary,
                amount = record.Amount,
                priority = record.Priority,
                priorityReason = record.PriorityReason,
                research = BriefView(brief),
                productSheets = sheets.Select(s => SheetView(s, brief)),
                chatSuggestions = ChatSuggestions(brief)
            });
        });

        // Engage the Insurance Research Agent. Clients can ask for a live SSE
        // stream with `Accept: text/event-stream`; otherwise one JSON envelope.
        app.MapPost("/research/process", async (HttpContext httpContext, ResearchProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            var brief = tools.ResolveBrief(record);
            logger.LogInformation("Research process: caseNumber={CaseNumber} scenario={Scenario} kind={Kind} mcp={Mcp}",
                Sanitize(record.CaseNumber), Sanitize(brief.Scenario), brief.Kind, mcpEnabled);

            InsurerComparison? comparison = brief.Kind == BriefKinds.Advice ? await tools.BuildComparisonAsync(brief) : null;
            WordingLookup? wording = brief.Kind == BriefKinds.Claim ? await tools.BuildWordingLookupAsync(brief) : null;
            var prompt = BuildPrompt(record, brief, comparison, wording, mcpEnabled);

            string? createError = null;

            object BuildEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var error = agentError ?? createError;
                var notes = trace?.Text;
                if (string.IsNullOrWhiteSpace(notes))
                    notes = BuildFallbackNarrative(brief, comparison, wording, error, factory.IsConfigured);

                return new
                {
                    caseNumber = record.CaseNumber,
                    clientName = record.ClientName,
                    requestType = record.RequestType,
                    kind = brief.Kind,
                    research = BriefView(brief),
                    comparison,
                    wording,
                    mcpEnabled,
                    agentNotes = notes,
                    agentError = error,
                    agentConfigured = factory.IsConfigured,
                    agentInput = trace?.Input,
                    // Lets the page chain follow-up chat turns into the same Foundry conversation.
                    previousResponseId = trace?.ResponseId,
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

            CaldovaAgent? agent = null;
            if (factory.IsConfigured)
            {
                try
                {
                    agent = factory.Create(AgentRole);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "{Agent} could not be created; using the offline research", AgentLabel);
                    createError = ex.Message;
                }
            }

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                await AgentSseStreaming.StreamAsync(httpContext, agent, prompt, BuildEnvelope, logger, AgentLabel);
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
                    logger.LogWarning(ex, "{Agent} invocation failed; using the offline research", AgentLabel);
                    invocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, invocationError));
        });

        // Build the comparison workbook (same code as research_generateComparisonWorkbook).
        app.MapPost("/research/workbook", async (ResearchWorkbookRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            if (notes is { Length: > 2000 }) notes = notes[..2000];

            var result = await tools.GenerateWorkbookAsync(record.CaseNumber, null, notes);
            logger.LogInformation("Research workbook: caseNumber={CaseNumber} file={File} error={Error}",
                Sanitize(record.CaseNumber), Sanitize(result.FileName ?? "-"), Sanitize(result.Error ?? "-"));
            return result.Error is null
                ? Results.Ok(result)
                : Results.UnprocessableEntity(new { error = result.Error });
        });

        // Follow-up chat. Chains off previousResponseId when the live agent is
        // available; otherwise answers from the research for the case so the
        // page still holds a conversation offline.
        app.MapPost("/research/chat", async (HttpContext httpContext, ResearchChatRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return Results.BadRequest(new { error = "message is required" });

            var live = factory.IsConfigured && !string.IsNullOrWhiteSpace(request.PreviousResponseId);
            var record = string.IsNullOrWhiteSpace(request.CaseNumber) ? null : store.Get(request.CaseNumber);
            var brief = record is null ? null : tools.ResolveBrief(record);
            InsurerComparison? comparison = brief?.Kind == BriefKinds.Advice ? await tools.BuildComparisonAsync(brief) : null;
            WordingLookup? wording = brief?.Kind == BriefKinds.Claim ? await tools.BuildWordingLookupAsync(brief) : null;

            logger.LogInformation("Research chat: live={Live} caseNumber={CaseNumber}", live, Sanitize(request.CaseNumber ?? string.Empty));

            string? createError = null;
            CaldovaAgent? agent = null;
            if (live)
            {
                try
                {
                    agent = factory.Create(AgentRole);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "{Agent} could not be created for chat", AgentLabel);
                    createError = ex.Message;
                }
            }

            object BuildChatEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var error = agentError ?? createError;
                var notes = trace?.Text;
                var offline = string.IsNullOrWhiteSpace(notes);
                if (offline)
                {
                    var reason = !factory.IsConfigured
                        ? "the Foundry agent isn't configured in this environment"
                        : !string.IsNullOrWhiteSpace(error)
                            ? $"the live agent didn't reply ({error})"
                            : string.IsNullOrWhiteSpace(request.PreviousResponseId)
                                ? "there's no live conversation to continue"
                                : "the live agent returned no text";
                    notes = $"_Offline reply — {reason}, so Ivy is answering from the research on this case._\n\n" +
                            OfflineChatReply(brief, comparison, wording, request.Message);
                }

                return new
                {
                    agentNotes = notes,
                    agentError = error,
                    agentConfigured = factory.IsConfigured,
                    offline,
                    agentInput = trace?.Input,
                    previousResponseId = trace?.ResponseId ?? request.PreviousResponseId,
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
                await AgentSseStreaming.StreamChatAsync(
                    httpContext, agent, request.PreviousResponseId ?? string.Empty, request.Message,
                    BuildChatEnvelope, logger, AgentLabel);
                return Results.Empty;
            }

            AgentTraceResult? chatTrace = null;
            string? chatError = null;
            if (agent is not null)
            {
                try
                {
                    chatTrace = await agent.ChatWithTraceAsync(request.PreviousResponseId!, request.Message);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "{Agent} chat invocation failed", AgentLabel);
                    chatError = ex.Message;
                }
            }

            return Results.Ok(BuildChatEnvelope(chatTrace, chatError));
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // Page views
    // ─────────────────────────────────────────────────────────────────────

    private static object BriefView(ResearchBrief b) => new
    {
        scenario = b.Scenario,
        kind = b.Kind,
        title = b.Title,
        summary = b.Summary,
        client = b.Client,
        clientFirstNames = b.ClientFirstNames,
        keyFacts = b.KeyFacts,
        coverRequested = b.CoverRequested,
        monthlyBudget = b.MonthlyBudget,
        definitionsThatMatter = b.Items.Select(i => new { key = i.Key, title = i.Title, priority = i.Priority, whyItMatters = i.WhyItMatters }),
        observations = b.Observations,
        quotesOnFile = b.QuotesOnFile,
        claimInsurer = b.ClaimInsurer,
        policyNumber = b.PolicyNumber,
        policyOnFile = b.PolicyOnFile,
        handover = b.Handover,
        handoverAgentId = b.HandoverAgentId,
        handoverAgentName = AgentName(b.HandoverAgentId),
        handoverUrl = HandoverUrl(b),
        caseStatus = b.CaseStatus,
        journeyNote = JourneyNote(b),
        nextSteps = b.NextSteps,
        humanApproval = b.HumanApproval
    };

    private static object SheetView(ProductSheet s, ResearchBrief b)
    {
        var quote = b.QuotesOnFile
            ? s.Quotes.FirstOrDefault(q => string.Equals(q.Scenario, b.Scenario, StringComparison.OrdinalIgnoreCase))
            : null;
        var notQuoted = s.NotQuoted.FirstOrDefault(n => string.Equals(n.Scenario, b.Scenario, StringComparison.OrdinalIgnoreCase))?.Reason;

        bool relevant;
        string status;
        if (b.Kind == BriefKinds.Claim)
        {
            relevant = b.ClaimInsurer is not null && MatchesInsurer(s, b.ClaimInsurer);
            status = relevant ? $"Policy on file · {b.PolicyNumber}" : "Not on this claim";
        }
        else if (quote is not null)
        {
            relevant = true;
            status = $"Quoted · {Money(quote.Year1Premium)} a year stepped";
        }
        else
        {
            relevant = !b.QuotesOnFile && notQuoted is null;
            status = notQuoted is not null ? "Not quoted" : b.QuotesOnFile ? "No quote on file" : "Definitions only";
        }

        return new
        {
            insurer = s.Insurer,
            shortName = s.ShortName,
            code = s.Code,
            productName = s.ProductName,
            pdsTitle = s.PdsTitle,
            aplListing = s.AplListing,
            url = s.Source,
            relevant,
            status,
            notQuotedReason = quote is null ? notQuoted : null,
            preview = SheetPreview(s)
        };
    }

    /// <summary>Plain-text summary of a product sheet for the hover popovers.</summary>
    private static string SheetPreview(ProductSheet s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(s.Summary);
        sb.AppendLine();
        sb.AppendLine($"APL listing: {s.AplListing}");
        sb.AppendLine("Premiums: stepped or level");
        sb.AppendLine();
        sb.AppendLine("Cover types");
        foreach (var c in s.CoverTypes)
            sb.AppendLine($"  • {c.Cover}{(c.InsideSuper ? " — inside or outside super" : " — outside super")}");
        if (s.Strengths.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Strengths");
            foreach (var x in s.Strengths.Take(3)) sb.AppendLine($"  • {x}");
        }
        if (s.WatchPoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Watch points");
            foreach (var x in s.WatchPoints.Take(3)) sb.AppendLine($"  • {x}");
        }
        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> ChatSuggestions(ResearchBrief b) => b.Scenario switch
    {
        ResearchScenarios.FamilyProtection => new[]
        {
            "Why Corella Life for Priya?",
            "Explain own vs any occupation TPD for a physio.",
            "Stepped or level over 10 years?",
            "How does it fit Priya's $300 a month budget?",
            "Does commission affect the ranking?",
        },
        ResearchScenarios.BusinessProtection => new[]
        {
            "Why Northbank Life for George and Harpreet?",
            "Which TPD definition applies to each owner?",
            "How does financial underwriting differ?",
            "Stepped or level for business cover?",
            "Does commission affect the ranking?",
        },
        ResearchScenarios.FraudulentClaim => new[]
        {
            "What does Lyrebird's cancer definition need?",
            "Who can the trauma benefit be paid to?",
            "What does a trauma claim need?",
            "Is Grace's life cover affected?",
        },
        ResearchScenarios.IncomeProtectionClaim => new[]
        {
            "How is Tom's monthly benefit worked out?",
            "When do payments start?",
            "What evidence does Harbourline need?",
            "Is the benefit taxable?",
        },
        ResearchScenarios.DeathClaim => new[]
        {
            "Who receives Mei's life cover?",
            "What does Corella need for the claim?",
            "Can Corella avoid the policy for non-disclosure?",
            "What about Mei's super cover?",
        },
        _ when b.Kind == BriefKinds.Advice => new[]
        {
            "Compare the TPD definitions.",
            "Compare the income protection definitions.",
            "What's needed before quotes?",
        },
        _ => new[] { "What do you need to look up the policy wording?" }
    };

    private static string? HandoverUrl(ResearchBrief b) =>
        string.IsNullOrWhiteSpace(b.HandoverAgentId)
            ? null
            : $"/agents/{b.HandoverAgentId}?caseNumber={Uri.EscapeDataString(b.CaseNumber)}";

    private static string? AgentName(string? agentId) => agentId switch
    {
        "claims" => "Claims Agent (Ben)",
        "paraplanning" => "Paraplanning Agent (Penny)",
        null or "" => null,
        _ => agentId
    };

    private static string? JourneyNote(ResearchBrief b) => b.Kind switch
    {
        BriefKinds.Claim => b.ClaimInsurer is null
            ? "This is a claim, so Ivy doesn't compare insurers — Ben's Claims Agent owns it. Ask Ben's agent for the policy schedule, then Ivy can look up the insurer's wording."
            : $"This is a claim, so Ivy doesn't compare insurers — Ben's Claims Agent owns it. Ivy can still help with a policy wording lookup for {b.ClaimInsurer}.",
        BriefKinds.Advice when !b.QuotesOnFile =>
            "No insurer quotes are on this case yet — Ivy compares the definitions now; premiums follow Penny's needs analysis.",
        BriefKinds.Unknown => b.Summary,
        _ => null
    };

    // ─────────────────────────────────────────────────────────────────────
    // Prompt
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildPrompt(CaseRecord c, ResearchBrief b, InsurerComparison? comparison, WordingLookup? wording, bool mcpEnabled)
    {
        var isClaim = b.Kind == BriefKinds.Claim;
        var sb = new StringBuilder();
        sb.AppendLine(isClaim ? "CLIENT CASE — POLICY WORDING LOOKUP (CLAIM)" : "CLIENT CASE FOR INSURANCE RESEARCH");
        sb.AppendLine("===========================================");
        sb.AppendLine($"Case number   : {c.CaseNumber}");
        sb.AppendLine($"Client        : {c.ClientName}");
        sb.AppendLine($"Client number : {c.ClientNumber}");
        sb.AppendLine($"Cover         : {c.Cover}");
        sb.AppendLine($"Request type  : {c.RequestType}");
        sb.AppendLine($"Received      : {c.ReceivedDate}");
        sb.AppendLine($"Adviser       : {c.AdviserName}");
        sb.AppendLine($"Referred by   : {c.ReferredBy}");
        sb.AppendLine($"Priority      : {c.Priority} — {c.PriorityReason}");
        sb.AppendLine();
        sb.AppendLine("REQUEST SUMMARY");
        sb.AppendLine("---------------");
        sb.AppendLine(c.RequestSummary);
        sb.AppendLine();
        sb.AppendLine($"RESEARCH BRIEF — {b.Title}");
        sb.AppendLine("------------------------------");
        sb.AppendLine(b.Summary);
        AppendList(sb, "Key facts", b.KeyFacts);
        AppendList(sb, "Cover to research (from Penny's needs analysis)", b.CoverRequested);
        if (b.MonthlyBudget is decimal budget)
            sb.AppendLine($"Budget for premiums from cash flow: up to {Money(budget)} a month.");
        if (isClaim)
        {
            AppendList(sb, "Policy on file", b.PolicyOnFile);
            AppendList(sb, "Wording to look up", b.Items.Select(i => $"{i.Title} — {i.WhyItMatters}"));
            if (!string.IsNullOrWhiteSpace(b.Handover)) sb.AppendLine($"Handover: {b.Handover}");
        }
        else
        {
            AppendList(sb, "Definitions that matter for this client", b.Items.Select(i => $"[{i.Priority}] {i.Title} — {i.WhyItMatters}"));
        }
        AppendList(sb, "Notes", b.Observations);
        sb.AppendLine();

        sb.AppendLine("APL PRODUCT SHEETS");
        sb.AppendLine("------------------");
        if (comparison is not null)
        {
            foreach (var r in comparison.Rows)
                sb.AppendLine($"- {r.Insurer}: {r.SheetUrl} — cite \"{r.PdsTitle}\"{(r.Quoted ? string.Empty : $" (not quoted: {r.NotQuotedReason})")}");
        }
        else if (wording?.SheetUrl is not null)
        {
            sb.AppendLine($"- {wording.Insurer}: {wording.SheetUrl} — cite \"{wording.PdsTitle}\" (the insurer on this claim)");
        }
        sb.AppendLine();

        if (mcpEnabled)
        {
            sb.AppendLine("TASK (use your MCP tools)");
            sb.AppendLine("-------------------------");
            if (isClaim)
            {
                sb.AppendLine($"1. Call research_analysePolicyDocument with insurerOrUrl \"{b.ClaimInsurer ?? "the claim's insurer"}\" and caseNumber \"{c.CaseNumber}\".");
                sb.AppendLine("2. Write the policy wording lookup in the claim output format. Don't compare insurers or build a workbook.");
            }
            else
            {
                sb.AppendLine($"1. Call research_analysePolicyDocument for each APL insurer above, passing caseNumber \"{c.CaseNumber}\".");
                sb.AppendLine($"2. Call research_compareInsurers with caseNumber \"{c.CaseNumber}\". Embed comparisonMarkdown and chartMermaid (a ```mermaid``` block)");
                sb.AppendLine("   in \"Insurers compared\", definitionsMarkdown in \"Definitions that matter\", and projectionMarkdown with projectionMermaid");
                sb.AppendLine("   in \"Stepped vs level\" — verbatim.");
                sb.AppendLine($"3. Call research_generateComparisonWorkbook with caseNumber \"{c.CaseNumber}\" and a one-line notesForAdviser. Put the");
                sb.AppendLine("   downloadUrl in \"Recommendation for Amelia\" as a markdown link: [Download the comparison workbook](<downloadUrl>).");
                sb.AppendLine("4. Write your report in the required output format.");
            }
        }
        else
        {
            sb.AppendLine("MCP TOOL RESULTS (pre-resolved for you in this session — do NOT call the tools again)");
            sb.AppendLine("=====================================================================================");
            if (comparison is not null)
            {
                var rec = comparison.Recommendation;
                sb.AppendLine($"▶ research_compareInsurers(\"{c.CaseNumber}\"):");
                sb.AppendLine(rec.Headline);
                AppendList(sb, "Reasons", rec.Reasons);
                AppendList(sb, "Not chosen", rec.NotChosen);
                if (!string.IsNullOrWhiteSpace(rec.SteppedVsLevel)) sb.AppendLine($"Stepped vs level: {rec.SteppedVsLevel}");
                sb.AppendLine($"Commission: {comparison.CommissionNote}");
                sb.AppendLine();
                sb.AppendLine("comparisonMarkdown:");
                sb.AppendLine(comparison.ComparisonMarkdown);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(comparison.ChartMermaid))
                {
                    sb.AppendLine("chartMermaid:");
                    sb.AppendLine(comparison.ChartMermaid);
                    sb.AppendLine();
                }
                sb.AppendLine("definitionsMarkdown:");
                sb.AppendLine(comparison.DefinitionsMarkdown);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(comparison.ProjectionMarkdown))
                {
                    sb.AppendLine("projectionMarkdown:");
                    sb.AppendLine(comparison.ProjectionMarkdown);
                    sb.AppendLine();
                    sb.AppendLine("projectionMermaid:");
                    sb.AppendLine(comparison.ProjectionMermaid);
                    sb.AppendLine();
                }
                sb.AppendLine($"Documents to cite: {string.Join("; ", comparison.Citations)}");
                sb.AppendLine();
                sb.AppendLine("▶ research_generateComparisonWorkbook was not run in this session — Amelia builds the workbook from the page's");
                sb.AppendLine("  \"Build the comparison workbook\" step. Say so in \"Recommendation for Amelia\".");
                sb.AppendLine();
                sb.AppendLine("TASK");
                sb.AppendLine("----");
                sb.AppendLine("1. Embed comparisonMarkdown and the chart verbatim in \"Insurers compared\", definitionsMarkdown in \"Definitions that");
                sb.AppendLine("   matter\", and the projection table and chart in \"Stepped vs level\".");
                sb.AppendLine("2. Write your report in the required output format.");
            }
            else if (wording is not null)
            {
                sb.AppendLine($"▶ research_analysePolicyDocument(\"{wording.Insurer ?? "unknown insurer"}\", \"{c.CaseNumber}\") — wording that applies:");
                sb.AppendLine(string.IsNullOrWhiteSpace(wording.Markdown) ? "(The policy schedule isn't on the case yet.)" : wording.Markdown);
                sb.AppendLine();
                sb.AppendLine($"Documents to cite: {string.Join("; ", wording.Citations)}");
                sb.AppendLine();
                sb.AppendLine("TASK");
                sb.AppendLine("----");
                sb.AppendLine("Write the policy wording lookup in the claim output format. Don't compare insurers or build a workbook.");
            }
        }
        sb.AppendLine();

        sb.AppendLine(isClaim
            ? "Remember: wording only — you don't assess, decide or lodge claims; Ben's Claims Agent does, with approval. Never suggest the " +
              "client did anything wrong, keep it gentle, and treat health details as sensitive. Cite each document by its exact title."
            : "Remember: research for Amelia, not advice to the client — Amelia gives the advice and signs the Statement of Advice. " +
              "Definitions first, price second; commission never drives the choice. Cite each PDS extract and rule document by its exact title.");
        return sb.ToString();
    }

    private static void AppendList(StringBuilder sb, string heading, IEnumerable<string> items)
    {
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return;
        sb.AppendLine($"{heading}:");
        foreach (var i in list) sb.AppendLine($"- {i}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Offline narrative (used when the live agent isn't available)
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildFallbackNarrative(
        ResearchBrief b, InsurerComparison? comparison, WordingLookup? wording, string? agentError, bool agentConfigured)
    {
        var preface = !agentConfigured
            ? "_Offline — the Foundry agent isn't configured in this environment, so this is Ivy's research from the same tools the agent calls on the APL product sheets._"
            : !string.IsNullOrWhiteSpace(agentError)
                ? $"_The Insurance Research Agent didn't return a report ({agentError}). Showing Ivy's offline research instead._"
                : "_The Insurance Research Agent finished without narrative text (it may only have called tools). Showing Ivy's offline research instead._";

        var sb = new StringBuilder();
        sb.AppendLine(preface);
        sb.AppendLine();

        if (comparison is not null)
        {
            var rec = comparison.Recommendation;
            var names = b.ClientFirstNames;
            sb.AppendLine($"### Insurance research — {b.CaseNumber} · {b.Client}");
            sb.AppendLine();
            sb.AppendLine("#### 1. Research summary");
            sb.AppendLine(b.Summary);
            sb.AppendLine();
            sb.AppendLine($"**{rec.Headline}**");
            sb.AppendLine();

            sb.AppendLine("#### 2. Insurers compared");
            sb.AppendLine(comparison.ComparisonMarkdown);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(comparison.ChartMermaid))
            {
                sb.AppendLine(comparison.ChartMermaid);
                sb.AppendLine();
            }

            sb.AppendLine($"#### 3. Definitions that matter for {names}");
            sb.AppendLine(comparison.DefinitionsMarkdown);
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(rec.SteppedVsLevel) || !string.IsNullOrWhiteSpace(comparison.ProjectionMarkdown))
            {
                sb.AppendLine("#### 4. Stepped vs level");
                if (!string.IsNullOrWhiteSpace(rec.SteppedVsLevel)) sb.AppendLine(rec.SteppedVsLevel);
                if (!string.IsNullOrWhiteSpace(rec.BudgetCheck))
                {
                    sb.AppendLine();
                    sb.AppendLine(rec.BudgetCheck);
                }
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(comparison.ProjectionMarkdown))
                {
                    sb.AppendLine(comparison.ProjectionMarkdown);
                    sb.AppendLine();
                    sb.AppendLine(comparison.ProjectionMermaid);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("#### 5. Recommendation for Amelia");
            sb.AppendLine(rec.Headline);
            sb.AppendLine();
            foreach (var r in rec.Reasons) sb.AppendLine($"- {r}");
            if (rec.NotChosen.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Not chosen:");
                foreach (var n in rec.NotChosen) sb.AppendLine($"- {n}");
            }
            sb.AppendLine();
            sb.AppendLine($"_{comparison.CommissionNote}_");
            if (comparison.Observations.Count > 0)
            {
                sb.AppendLine();
                foreach (var o in comparison.Observations) sb.AppendLine($"- {o}");
            }
            sb.AppendLine();
            sb.AppendLine("Next steps:");
            foreach (var s in comparison.NextSteps) sb.AppendLine($"- {s}");
            sb.AppendLine("- Build the comparison workbook (comparison, definitions, 10-year projection and charts) from the next steps on this page.");
            sb.AppendLine();

            sb.AppendLine("#### 6. Human approval required");
            sb.AppendLine(comparison.HumanApproval);
            sb.AppendLine();
            sb.Append($"_Sources: {string.Join("; ", comparison.Citations)}. {rec.ForAdviser}_");
            return sb.ToString();
        }

        if (wording is not null)
        {
            sb.AppendLine($"### Policy wording lookup — {b.CaseNumber} · {b.Client}");
            sb.AppendLine();
            var note = JourneyNote(b);
            if (!string.IsNullOrWhiteSpace(note))
            {
                sb.AppendLine($"> {note}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(wording.Markdown))
            {
                sb.AppendLine(wording.Markdown);
                sb.AppendLine();
            }
            sb.AppendLine("#### Handover to Claims");
            if (!string.IsNullOrWhiteSpace(wording.Handover)) sb.AppendLine(wording.Handover);
            if (!string.IsNullOrWhiteSpace(wording.HandoverUrl))
            {
                sb.AppendLine();
                sb.AppendLine($"[Open the Claims Agent for this case]({wording.HandoverUrl})");
            }
            sb.AppendLine();
            foreach (var s in wording.NextSteps) sb.AppendLine($"- {s}");
            sb.AppendLine();
            sb.AppendLine("#### Human approval required");
            sb.AppendLine(wording.HumanApproval);
            sb.AppendLine();
            if (wording.Citations.Count > 0)
                sb.Append($"_Sources: {string.Join("; ", wording.Citations)}. Policy wording only — Ivy doesn't assess or decide claims._");
            return sb.ToString();
        }

        sb.AppendLine($"### {b.Title}");
        sb.AppendLine();
        sb.Append(b.Summary);
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Offline chat
    // ─────────────────────────────────────────────────────────────────────

    private static string OfflineChatReply(ResearchBrief? b, InsurerComparison? comparison, WordingLookup? wording, string message)
    {
        if (b is null)
            return "Pick a case in step 1 and engage me first — then I can answer questions about that case's research.";

        var q = message.ToLowerInvariant();
        // Word-start matching, so "level" matches "levels" but "ip" doesn't match "trip".
        bool Has(params string[] words) => words.Any(w => Regex.IsMatch(q, @"\b" + Regex.Escape(w)));

        if (comparison is not null) return AdviceChatReply(b, comparison, Has);
        if (wording is not null) return ClaimChatReply(b, wording, Has);
        return b.Summary;
    }

    private static string AdviceChatReply(ResearchBrief b, InsurerComparison c, Func<string[], bool> has)
    {
        bool Has(params string[] words) => has(words);
        var rec = c.Recommendation;

        string Definition(params string[] keys)
        {
            var rows = c.Definitions.Where(d => keys.Contains(d.Key)).ToList();
            if (rows.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var d in rows)
            {
                sb.AppendLine($"**{d.Title}** ({d.Priority.ToLowerInvariant()}) — {d.WhyItMatters}");
                sb.AppendLine();
                foreach (var cell in d.Insurers) sb.AppendLine($"- **{cell.Insurer}:** {cell.Label} — {cell.Note}");
                sb.AppendLine();
            }
            sb.Append($"_From each insurer's PDS extract: {string.Join("; ", rows.SelectMany(r => r.Insurers).Select(i => i.PdsTitle).Distinct())}._");
            return sb.ToString();
        }

        var insurerAsked = c.Rows.FirstOrDefault(r => Has(r.ShortName.ToLowerInvariant()));

        if (Has("should", "advise", "advice", "best for"))
        {
            return "I can't give personal advice — Amelia gives the advice and signs the Statement of Advice. Here's what the research shows " +
                   $"for her review:\n\n**{rec.Headline}**\n\n{string.Join("\n", rec.Reasons.Select(r => $"- {r}"))}";
        }

        if (Has("commission", "lif cap", "life insurance framework", "clawback", "consent"))
            return c.CommissionNote;

        if (Has("stepped", "level", "projection", "over time", "10 year", "ten year", "break even", "break-even"))
        {
            var p = c.Projections.FirstOrDefault(x => insurerAsked is not null && x.Insurer == insurerAsked.Insurer)
                    ?? c.Projections.FirstOrDefault(x => x.Recommended)
                    ?? c.Projections.FirstOrDefault();
            if (p is null) return "There's no projection yet — the insurers haven't quoted on this case.";
            var rows = p.Years.Where(y => y.Year is 1 or 5 or 10 || y.Year == p.Years.Count).DistinctBy(y => y.Year)
                .Select(y => $"| {y.Year} | {Money(y.Stepped)} | {Money(y.Level)} | {Money(y.CumulativeStepped)} | {Money(y.CumulativeLevel)} |");
            return $"**{p.Insurer} — stepped vs level (illustrative)**\n\n{p.Summary}\n\n" +
                   "| Year | Stepped | Level | Total paid (stepped) | Total paid (level) |\n|:-:|--:|--:|--:|--:|\n" +
                   string.Join("\n", rows) +
                   $"\n\n_{p.Label}_ Which style suits {b.ClientFirstNames} is Amelia's call.";
        }

        if (Has("own occupation", "own-occupation", "any occupation", "any-occupation", "tpd"))
        {
            var text = Definition("tpd-own-occupation", "tpd-business-cover", "tpd-super-linking");
            if (text.Length > 0) return text;
        }

        if (Has("income protection", "ip", "waiting", "benefit period", "70%", "70 per", "2 years", "two years"))
        {
            var text = Definition("ip-benefit-basis", "ip-periods", "ip-long-claims");
            if (text.Length > 0) return text;
        }

        if (Has("trauma", "cancer", "histopathology"))
        {
            var text = Definition("trauma-cancer");
            if (text.Length > 0) return text;
        }

        if (Has("exclusion", "back", "loading"))
        {
            var text = Definition("exclusion-review");
            if (text.Length > 0) return text;
        }

        if (Has("super", "linking", "linked"))
        {
            var text = Definition("tpd-super-linking");
            var notes = c.Observations.Where(o => o.Contains("super", StringComparison.OrdinalIgnoreCase)).ToList();
            if (text.Length > 0 || notes.Count > 0)
                return text + (notes.Count > 0 ? "\n\n" + string.Join("\n", notes.Select(n => $"- {n}")) : string.Empty);
        }

        if (Has("budget", "cash flow", "afford", "a month", "monthly"))
        {
            if (!string.IsNullOrWhiteSpace(rec.BudgetCheck)) return rec.BudgetCheck!;
            var monthly = c.Rows.Where(r => r.Quoted).Select(r => $"- **{r.Insurer}:** {Money(r.MonthlyStepped ?? 0m)} a month stepped");
            return "There's no personal budget on this case. Monthly equivalents of the year-1 premiums:\n\n" + string.Join("\n", monthly);
        }

        if (Has("underwriting", "financial evidence", "accounts", "financials"))
        {
            var text = Definition("financial-underwriting");
            if (text.Length > 0) return text;
        }

        if (Has("buy-sell", "buy sell", "key person", "valuation"))
        {
            var text = Definition("business-buy-sell", "business-key-person");
            if (text.Length > 0) return text;
        }

        if (Has("tax", "deductible", "assessable"))
        {
            var notes = c.Observations.Where(o => o.Contains("tax", StringComparison.OrdinalIgnoreCase)).ToList();
            return notes.Count > 0
                ? string.Join("\n", notes.Select(n => $"- {n}"))
                : $"Tax treatment is covered in '{TaxTitle}' — the client's accountant confirms it for their situation.";
        }

        if (Has("workbook", "excel", "xlsx", "spreadsheet", "download"))
        {
            return "Use **Build the comparison workbook** on this page. It has four sheets — Summary (the recommendation rationale for Amelia " +
                   "and sources), Comparison (premiums, cash flow, 10-year totals and a premium chart), Definitions (each insurer's result " +
                   "and wording) and Projection (10 years of stepped vs level, with a chart).";
        }

        if (Has("approv", "sign", "second review", "theo"))
            return c.HumanApproval;

        if (insurerAsked is not null)
        {
            var notChosen = rec.NotChosen.FirstOrDefault(n => n.StartsWith(insurerAsked.Insurer, StringComparison.OrdinalIgnoreCase));
            var price = insurerAsked.Quoted
                ? $"{Money(insurerAsked.Year1Stepped ?? 0m)} a year stepped in year 1 ({Money(insurerAsked.MonthlyStepped ?? 0m)} a month), level option {Money(insurerAsked.Year1Level ?? 0m)}."
                : $"Not quoted — {insurerAsked.NotQuotedReason}";
            var why = insurerAsked.Recommended
                ? $"It's the insurer recommended for Amelia's review:\n\n{string.Join("\n", rec.Reasons.Select(r => $"- {r}"))}"
                : notChosen ?? insurerAsked.DefinitionSummary;
            return $"**{insurerAsked.Insurer}** — {price}\n\n{why}\n\n_Cite: {insurerAsked.PdsTitle}._";
        }

        if (Has("why", "recommend", "which insurer", "best"))
            return $"**{rec.Headline}**\n\n{string.Join("\n", rec.Reasons.Select(r => $"- {r}"))}" +
                   (rec.NotChosen.Count > 0 ? $"\n\nNot chosen:\n{string.Join("\n", rec.NotChosen.Select(n => $"- {n}"))}" : string.Empty);

        if (Has("price", "premium", "cost", "cheap", "dear", "expensive", "quote"))
        {
            var lines = c.Rows.Select(r => r.Quoted
                ? $"- **{r.Insurer}{(r.Recommended ? " (recommended)" : string.Empty)}:** {Money(r.Year1Stepped ?? 0m)} stepped, {Money(r.Year1Level ?? 0m)} level in year 1"
                : $"- **{r.Insurer}:** not quoted — {r.NotQuotedReason}");
            return "Year-1 premiums at standard rates before underwriting:\n\n" + string.Join("\n", lines) +
                   "\n\nPrice comes second — an insurer that misses a must-have definition ranks below the others whatever it costs.";
        }

        return $"{c.Summary}\n\n**{rec.Headline}**\n\nYou can ask me why that insurer, about a **definition** (TPD, income protection, trauma), " +
               "**stepped vs level**, the **budget**, **commission**, the **workbook**, or what needs **approval**.";
    }

    private static string ClaimChatReply(ResearchBrief b, WordingLookup w, Func<string[], bool> has)
    {
        bool Has(params string[] words) => has(words);

        string Item(params string[] keys)
        {
            var items = w.Items.Where(i => keys.Contains(i.Key)).ToList();
            if (items.Count == 0) return string.Empty;
            return string.Join("\n\n", items.Select(i =>
                $"**{i.Heading}** — _{i.Source}_\n\n> {i.Wording}\n\n{(string.IsNullOrWhiteSpace(i.PlainEnglish) ? string.Empty : $"In plain English: {i.PlainEnglish}\n\n")}" +
                $"What it means here: {i.WhatItMeans}"));
        }

        var handover = string.IsNullOrWhiteSpace(w.HandoverUrl)
            ? w.Handover ?? string.Empty
            : $"{w.Handover} [Open the Claims Agent]({w.HandoverUrl})";

        if (Has("should", "will it be paid", "approve", "decide", "assess", "accept", "advice"))
            return "I can't assess or decide the claim — Ben's Claims Agent and the insurer do that, with approval. I can show you the wording that applies.\n\n" + handover;

        if (Has("fraud", "scam", "suspicious", "changed", "hold", "block"))
        {
            return b.Scenario == ResearchScenarios.FraudulentClaim
                ? "That's for Felix's Financial Crime Agent and Theo — Ivy only looks up the wording, and Grace is treated as the person to protect. " +
                  "What the wording says: the benefit can only be paid to Grace, as policy owner, into an account in her own name.\n\n" + handover
                : "I only look up the policy wording; any concern about documents or bank details goes to Felix's Financial Crime Agent.\n\n" + handover;
        }

        var answer =
            Has("waiting", "when", "first payment", "start") ? Item("ip-periods") :
            Has("paid to", "payment", "bank", "account", "who receives", "who gets", "receive", "pay") ? Item("benefit-payment", "nominated-beneficiaries") :
            Has("evidence", "document", "claim need", "need for", "proof", "form") ? Item("trauma-claim-evidence", "ip-claim-evidence", "death-claim-evidence") :
            Has("cancer", "histopathology", "trauma", "diagnos") ? Item("trauma-cancer") :
            Has("beneficiar", "nominat", "estate") ? Item("nominated-beneficiaries") :
            Has("how much", "calculat", "monthly benefit", "worked out", "70", "earnings", "insured") ? Item("ip-benefit-basis", "rule-idii") :
            Has("tax") ? string.Join("\n\n", w.Items.Where(i => i.WhatItMeans.Contains("tax", StringComparison.OrdinalIgnoreCase))
                .Select(i => $"**{i.Heading}:** {i.WhatItMeans}")) :
            Has("disclos", "misrep", "avoid", "three year", "3 year", "3-year") ? Item("rule-icas29") :
            Has("life cover") ? Item("nominated-beneficiaries") :
            string.Empty;

        if (answer.Length == 0 && Has("super", "riverbend"))
        {
            var facts = b.KeyFacts.Where(k => k.Contains("super", StringComparison.OrdinalIgnoreCase)).ToList();
            if (facts.Count > 0) answer = string.Join("\n", facts.Select(f => $"- {f}")) + "\n\nBen's agent gives the family the checklist for the super claim.";
        }

        if (answer.Length == 0 && Has("compare", "workbook", "excel", "quote"))
            answer = "There's nothing to compare on a claim, so there's no comparison or workbook — just the wording that applies.";

        if (answer.Length > 0) return answer + "\n\n" + handover;

        var headings = string.Join(", ", w.Items.Select(i => i.Heading.ToLowerInvariant()));
        return $"{w.Summary}\n\nI can show the wording on: {headings}.\n\n{handover}";
    }

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
