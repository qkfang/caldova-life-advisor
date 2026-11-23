using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CaldovaLife.App.Services;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ModelContextProtocol.Server;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace CaldovaLife.App.Mcp;

/// <summary>
/// MCP tools surfaced to the Insurance Research Agent (Ivy, Insurance
/// Research Analyst). They give the agent deterministic skills it can call
/// while researching a Client Case:
///
/// - <c>research_analysePolicyDocument</c> — read an APL insurer's product
///   sheet (by insurer name or URL): cover types, definitions, features,
///   exclusions and premium styles, plus fit notes for the client when a case
///   number is given (or the wording that applies to a claim).
/// - <c>research_compareInsurers</c> — compare the APL insurers for an advice
///   case: year-1 stepped and level premiums, 10-year totals, the definitions
///   that matter for this client, and the recommendation rationale for Amelia
///   (research for the adviser — never advice to the client).
/// - <c>research_generateComparisonWorkbook</c> — write the comparison
///   workbook (summary, comparison, definitions, projection and charts) to
///   <c>wwwroot/research/output/</c> and return its download URL
///   (<c>/research/download/{fileName}</c>, served by Program.cs).
///
/// The same internals back the <c>/research/*</c> endpoints in
/// <see cref="CaldovaLife.App.Api.ResearchApi"/>, so the page shows the same
/// numbers whether or not the Foundry agent is configured. Product sheets are
/// fictional and live in <c>wwwroot/research/samples/</c>.
/// </summary>
[McpServerToolType]
public class ResearchMcpTools
{
    /// <summary>App-relative URL prefix of the insurer product sheets in wwwroot.</summary>
    internal const string SamplesUrlPrefix = "/research/samples/";

    /// <summary>App-relative URL prefix of the workbook download endpoint (Program.cs).</summary>
    internal const string DownloadUrlPrefix = "/research/download/";

    /// <summary>One product sheet per insurer on Caldova's approved product list (APL).</summary>
    internal static readonly string[] AplSheetFiles =
    {
        "corella-life.json",
        "harbourline-life.json",
        "lyrebird-life.json",
        "northbank-life.json",
    };

    internal const string AplResearchTitle = "Caldova Life Advice — Approved product list and research method";
    internal const string AdviceRulesTitle = "Advice rules — best interests duty, SoA and replacement advice";
    internal const string InsideSuperTitle = "Insurance inside super — permitted cover, Protecting Your Super and opt-in rules";
    internal const string CommissionRulesTitle = "Commission rules — LIF caps, clawback and consent";
    internal const string TaxTitle = "Tax on life insurance — premiums and benefits";
    internal const string ClaimsRulesTitle = "Claims rules — Life Insurance Code of Practice and income protection benefits";
    internal const string UnderwritingRulesTitle = "Underwriting and the duty to take reasonable care";

    private const int MaxWorkbooksKept = 100;
    private const int MaxRemoteDocumentBytes = 2 * 1024 * 1024;

    internal static readonly CultureInfo AuCulture = CultureInfo.GetCultureInfo("en-AU");

    // Relaxed escaping keeps "—", "×" and "$" readable when tool output is
    // embedded in an agent prompt. The output is never written into HTML.
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CaseStore _caseStore;
    private readonly ILogger<ResearchMcpTools> _logger;

    public ResearchMcpTools(
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        CaseStore caseStore,
        ILogger<ResearchMcpTools> logger)
    {
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _caseStore = caseStore;
        _logger = logger;
    }

    private string WebRoot => _environment.WebRootPath ?? "wwwroot";

    /// <summary>Folder the workbooks are written to (served by /research/download/{fileName}).</summary>
    internal string OutputDirectory => Path.Combine(WebRoot, "research", "output");

    // ─────────────────────────────────────────────────────────────────────
    // research_analysePolicyDocument
    // ─────────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "research_analysePolicyDocument"),
     Description("Read one APL insurer's product sheet — by insurer name (Harbourline Life, Corella Life, Lyrebird Life, Northbank Life), policy prefix (HL, CR, LB, NB), file name or URL — and return its cover types, key definitions (terminal illness, TPD own and any occupation, super-linking, trauma cancer, income protection benefit basis and periods, business cover), features, exclusions and premium styles, with the PDS extract title to cite. Pass the Client Case number to add the insurer's quote for the case and fit notes on the definitions that matter for this client — or, for a claim case, the wording that applies to the claim and who handles it.")]
    public async Task<string> AnalysePolicyDocument(
        [Description("Insurer name (e.g. 'Corella Life'), policy prefix (e.g. 'CR'), product sheet file (e.g. corella-life.json), app path (e.g. /research/samples/corella-life.json) or a public http(s) URL of a product sheet or PDS extract.")]
        string insurerOrUrl,
        [Description("Optional Client Case number, e.g. REQ-20261103-LIF001. Adds the quote and client fit notes (advice) or the wording that applies (claim).")]
        string? caseNumber = null)
    {
        if (string.IsNullOrWhiteSpace(insurerOrUrl))
            return JsonError("insurerOrUrl is required — an APL insurer name (e.g. 'Corella Life') or a product sheet URL.");

        try
        {
            var loaded = await LoadDocumentAsync(insurerOrUrl);
            if (loaded.Sheet is null && loaded.Text is null)
            {
                return JsonError($"No product sheet found for '{insurerOrUrl}'. Caldova's APL insurers are Harbourline Life, " +
                                 "Corella Life, Lyrebird Life and Northbank Life.");
            }

            if (loaded.Sheet is null)
            {
                return Serialize(new
                {
                    source = loaded.Source,
                    format = "text",
                    note = "Not a Caldova product sheet, so it wasn't parsed into definitions. Read the excerpt and cite the document by its title.",
                    excerpt = Truncate(loaded.Text!, 6000)
                });
            }

            var brief = string.IsNullOrWhiteSpace(caseNumber) ? null : ResolveBrief(caseNumber!);
            return Serialize(BuildDocumentAnalysis(loaded.Sheet, brief));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "research_analysePolicyDocument failed for {Doc}", Sanitize(insurerOrUrl));
            return JsonError($"Failed to read the product sheet: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // research_compareInsurers
    // ─────────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "research_compareInsurers"),
     Description("Compare the insurers on Caldova's approved product list for an advice case (new family or business protection). Ranks definitions first and price second: must-have definitions for this client (for example own-occupation TPD for a physiotherapist), then year-1 stepped premiums. Returns per-insurer year-1 stepped and level premiums, monthly and cash-flow cost, 10-year totals (illustrative), the definitions that matter with each insurer's result, a stepped-vs-level projection, the recommendation rationale for Amelia (research for the adviser, not advice to the client), the commission note, markdown tables (comparisonMarkdown, definitionsMarkdown, projectionMarkdown) and Mermaid charts (chartMermaid, projectionMermaid) to embed verbatim, and the document titles to cite. Claim cases have no comparison — use research_analysePolicyDocument for the policy wording instead.")]
    public async Task<string> CompareInsurers(
        [Description("Client Case number, e.g. REQ-20261103-LIF001 (Priya Natarajan, family protection) or REQ-20261109-LIF003 (George Papadopoulos and Harpreet Kaur, business protection).")]
        string caseNumber,
        [Description("Optional comma-separated insurers to include, e.g. 'Corella Life, Harbourline Life'. Default: every insurer on the APL.")]
        string? insurers = null)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
            return JsonError("caseNumber is required, e.g. REQ-20261103-LIF001.");

        try
        {
            var brief = ResolveBrief(caseNumber);
            if (brief.Kind == BriefKinds.Claim)
            {
                return JsonError($"{brief.CaseNumber} is a claim ({brief.Client}) — there's nothing to compare. Call " +
                                 $"research_analysePolicyDocument with '{brief.ClaimInsurer}' and this case number for the policy " +
                                 "wording, and hand the claim to the Claims Agent (Ben).");
            }
            if (brief.Kind != BriefKinds.Advice)
                return JsonError($"No research brief for '{caseNumber}'. {brief.Summary}");

            var comparison = await BuildComparisonAsync(brief, insurers);
            return Serialize(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "research_compareInsurers failed for {CaseNumber}", Sanitize(caseNumber));
            return JsonError($"Failed to compare the insurers: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // research_generateComparisonWorkbook
    // ─────────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "research_generateComparisonWorkbook"),
     Description("Build the insurer comparison workbook (.xlsx) for an advice case: Summary (recommendation rationale for Amelia, sources), Comparison (year-1 stepped and level premiums, cash-flow cost, 10-year totals, cover breakdown, with a premium chart), Definitions (the definitions that matter, per insurer) and Projection (10-year stepped vs level, illustrative, with a chart). Saves it under wwwroot/research/output/ and returns JSON with downloadUrl (/research/download/{fileName}). Put the link in your answer as a markdown link.")]
    public async Task<string> GenerateComparisonWorkbook(
        [Description("Client Case number, e.g. REQ-20261103-LIF001. Used for the comparison and the file name.")]
        string caseNumber,
        [Description("Optional comma-separated insurers to include. Default: every insurer on the APL.")]
        string? insurers = null,
        [Description("Optional short research notes for Amelia to print on the Summary sheet (plain text, no personal advice).")]
        string? notesForAdviser = null)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
            return JsonError("caseNumber is required, e.g. REQ-20261103-LIF001.");

        var result = await GenerateWorkbookAsync(caseNumber, insurers, notesForAdviser);
        return result.Error is null ? Serialize(result) : JsonError(result.Error);
    }

    /// <summary>Shared by the MCP tool and <c>POST /research/workbook</c>.</summary>
    internal async Task<ComparisonWorkbookResult> GenerateWorkbookAsync(string caseNumber, string? insurers, string? notesForAdviser)
    {
        try
        {
            var brief = ResolveBrief(caseNumber);
            if (brief.Kind != BriefKinds.Advice)
            {
                return ComparisonWorkbookResult.Failed(caseNumber, brief.Kind == BriefKinds.Claim
                    ? $"{brief.CaseNumber} is a claim — there's no insurer comparison to put in a workbook. Use the policy wording lookup instead."
                    : $"No research brief for '{caseNumber}'.");
            }

            var comparison = await BuildComparisonAsync(brief, insurers);
            if (!comparison.Rows.Any(r => r.Quoted))
            {
                return ComparisonWorkbookResult.Failed(brief.CaseNumber,
                    "No insurer quotes are on file for this case yet — quotes follow Penny's needs analysis.");
            }

            return WriteWorkbook(comparison, notesForAdviser);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "research_generateComparisonWorkbook failed for {CaseNumber}", Sanitize(caseNumber));
            return ComparisonWorkbookResult.Failed(caseNumber, $"Failed to generate the workbook: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Research briefs — what Ivy researches for each case
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Look the case up in the shared store and build its research brief.</summary>
    internal ResearchBrief ResolveBrief(string caseNumber)
    {
        var trimmed = (caseNumber ?? string.Empty).Trim();
        return ResearchBriefs.For(_caseStore.Get(trimmed), trimmed);
    }

    internal ResearchBrief ResolveBrief(CaseRecord record) => ResearchBriefs.For(record, record.CaseNumber);

    // ─────────────────────────────────────────────────────────────────────
    // Comparison — definitions first, price second
    // ─────────────────────────────────────────────────────────────────────

    internal async Task<InsurerComparison> BuildComparisonAsync(ResearchBrief brief, string? insurersFilter = null)
    {
        var sheets = await LoadAplSheetsAsync();
        return BuildComparison(brief, FilterSheets(sheets, insurersFilter, brief));
    }

    /// <summary>Keep the insurers named in the filter (or, for a case without quotes, those listed for that kind of cover).</summary>
    private static IReadOnlyList<ProductSheet> FilterSheets(IReadOnlyList<ProductSheet> sheets, string? filter, ResearchBrief brief)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var wanted = filter.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var picked = sheets.Where(s => wanted.Any(w => MatchesInsurer(s, w))).ToList();
            if (picked.Count > 0) return picked;
        }

        if (!brief.QuotesOnFile && brief.Kind == BriefKinds.Advice)
        {
            var business = brief.Scenario.StartsWith("business", StringComparison.OrdinalIgnoreCase);
            var listed = sheets.Where(s => s.AplListing.Contains(business ? "Business" : "Personal", StringComparison.OrdinalIgnoreCase)).ToList();
            if (listed.Count > 0) return listed;
        }
        return sheets;
    }

    internal static InsurerComparison BuildComparison(ResearchBrief brief, IReadOnlyList<ProductSheet> sheets)
    {
        var drafts = new List<RowDraft>();
        foreach (var sheet in sheets)
        {
            var quote = brief.QuotesOnFile
                ? sheet.Quotes.FirstOrDefault(q => string.Equals(q.Scenario, brief.Scenario, StringComparison.OrdinalIgnoreCase))
                : null;
            var notQuoted = quote is not null
                ? null
                : sheet.NotQuoted.FirstOrDefault(n => string.Equals(n.Scenario, brief.Scenario, StringComparison.OrdinalIgnoreCase))?.Reason
                  ?? (brief.QuotesOnFile
                      ? $"{sheet.Insurer} hasn't quoted for this case."
                      : "No quote yet — premiums follow Penny's needs analysis.");

            var results = brief.Items.Select(i =>
            {
                var (result, note) = ResultFor(sheet, quote, i.Key);
                return new ItemResult(i, result, note);
            }).ToList();

            drafts.Add(new RowDraft(
                sheet, quote, notQuoted,
                MustGaps: results.Count(r => r.Item.Priority == BriefPriorities.MustHave && r.Result == "gap"),
                MustPartly: results.Count(r => r.Item.Priority == BriefPriorities.MustHave && r.Result == "partly"),
                ImportantShort: results.Count(r => r.Item.Priority == BriefPriorities.Important && r.Result is "gap" or "partly"),
                Results: results));
        }

        // Definitions first: a must-have gap ranks an insurer below every insurer that
        // meets the must-haves, whatever its price. Then price. Important and helpful
        // differences are reasons for Amelia, not ranking rules.
        var quoted = drafts.Where(d => d.Quote is not null)
            .OrderBy(d => d.MustGaps).ThenBy(d => d.MustPartly)
            .ThenBy(d => d.Quote!.Year1Premium)
            .ToList();
        var notQuotedDrafts = drafts.Where(d => d.Quote is null).ToList();
        var best = quoted.FirstOrDefault(d => d.MustGaps == 0);

        var rows = new List<ComparisonRow>();
        var rank = 1;
        foreach (var d in quoted) rows.Add(ToRow(d, rank++, ReferenceEquals(d, best)));
        foreach (var d in notQuotedDrafts) rows.Add(ToRow(d, 0, false));

        var projections = quoted
            .Select(d => BuildProjection(d, ReferenceEquals(d, best), brief))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        var definitionSheets = quoted.Count > 0 ? quoted : drafts;
        var definitions = brief.Items.Select(item => new DefinitionRow(
            item.Key, item.Title, item.Priority, item.WhyItMatters,
            definitionSheets.Select(d =>
            {
                var r = d.Results.First(x => x.Item.Key == item.Key);
                var def = d.Sheet.Definitions.FirstOrDefault(x => x.Key == item.Key);
                return new DefinitionCell(d.Sheet.Insurer, r.Result, ResultLabel(r.Result), r.Note, def?.Wording, d.Sheet.PdsTitle);
            }).ToList())).ToList();

        var bestRow = rows.FirstOrDefault(r => r.Recommended);
        var recommendation = BuildRecommendation(brief, quoted, notQuotedDrafts, best, projections);
        var commission = BuildCommissionNote(brief, bestRow);

        var citations = sheets.Select(s => s.PdsTitle)
            .Concat(brief.RuleDocuments)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        var bestProjection = projections.FirstOrDefault(p => p.Recommended) ?? projections.FirstOrDefault();

        return new InsurerComparison(
            CaseNumber: brief.CaseNumber,
            Client: brief.Client,
            Scenario: brief.Scenario,
            Title: brief.Title,
            Summary: brief.Summary,
            CoverRequested: brief.CoverRequested,
            KeyFacts: brief.KeyFacts,
            MonthlyBudget: brief.MonthlyBudget,
            QuotesOnFile: quoted.Count > 0,
            Rows: rows,
            Definitions: definitions,
            Projections: projections,
            Recommendation: recommendation,
            CommissionNote: commission,
            Observations: brief.Observations,
            ComparisonMarkdown: BuildComparisonMarkdown(rows, brief),
            DefinitionsMarkdown: BuildDefinitionsMarkdown(definitions, definitionSheets.Select(d => d.Sheet.Insurer).ToList()),
            ProjectionMarkdown: bestProjection is null ? string.Empty : BuildProjectionMarkdown(bestProjection),
            ChartMermaid: quoted.Count == 0 ? string.Empty : BuildYear1ChartMermaid(rows.Where(r => r.Quoted).ToList()),
            ProjectionMermaid: bestProjection is null ? string.Empty : BuildProjectionMermaid(bestProjection),
            Citations: citations,
            NextSteps: brief.NextSteps,
            HumanApproval: brief.HumanApproval);
    }

    private static (string Result, string Note) ResultFor(ProductSheet sheet, InsurerQuote? quote, string key)
    {
        var term = quote?.TermsQuoted.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
        if (term is not null)
            return ((term.Result ?? "info").Trim().ToLowerInvariant(), term.Note ?? string.Empty);

        var def = sheet.Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        return def is not null
            ? ("info", def.PlainEnglish ?? def.Wording ?? string.Empty)
            : ("na", "Not on this insurer's product sheet.");
    }

    internal static string ResultLabel(string result) => result switch
    {
        "meets" => "Meets",
        "partly" => "Partly",
        "gap" => "Gap",
        "info" => "See wording",
        _ => "Not stated"
    };

    private static string ResultIcon(string result) => result switch
    {
        "meets" => "✅",
        "partly" => "⚠️",
        "gap" => "❌",
        _ => "ℹ️"
    };

    private static ComparisonRow ToRow(RowDraft d, int rank, bool recommended)
    {
        var q = d.Quote;
        var years = q?.Projection?.Years ?? new List<ProjectionYearInput>();
        var mustGapTitles = d.Results.Where(r => r.Item.Priority == BriefPriorities.MustHave && r.Result == "gap").Select(r => r.Item.Title).ToList();
        var shortTitles = d.Results.Where(r => r.Item.Priority != BriefPriorities.Helpful && r.Result is "partly" or "gap" && !mustGapTitles.Contains(r.Item.Title))
            .Select(r => r.Item.Title).ToList();

        string summary;
        if (q is null) summary = "Not quoted";
        else if (mustGapTitles.Count > 0) summary = "Gap on a must-have: " + string.Join("; ", mustGapTitles);
        else if (shortTitles.Count > 0) summary = "Meets the must-haves; short on: " + string.Join("; ", shortTitles);
        else summary = "Meets every must-have and important definition";

        decimal? year1Level = q?.LevelOption?.Year1Premium;
        return new ComparisonRow(
            Rank: rank,
            Insurer: d.Sheet.Insurer,
            ShortName: d.Sheet.ShortName,
            Code: d.Sheet.Code,
            Quoted: q is not null,
            Recommended: recommended,
            NotQuotedReason: d.NotQuotedReason,
            QuoteReference: q?.QuoteReference,
            PdsTitle: d.Sheet.PdsTitle,
            SheetUrl: d.Sheet.Source,
            PremiumStyle: q?.PremiumStyle,
            Year1Stepped: q?.Year1Premium,
            MonthlyStepped: q is null ? null : Round2(q.Year1Premium / 12m),
            InsideSuperYear1: q?.InsideSuperYear1,
            OutsideSuperYear1: q?.OutsideSuperYear1,
            CashFlowMonthly: q?.OutsideSuperYear1 is decimal o ? Round2(o / 12m) : null,
            Year1Level: year1Level,
            LevelOutsideSuperYear1: q?.LevelOption?.OutsideSuperYear1,
            LevelCashFlowMonthly: q?.LevelOption?.OutsideSuperYear1 is decimal lo ? Round2(lo / 12m) : null,
            TenYearStepped: years.Count > 0 ? years.Sum(y => y.Stepped) : null,
            TenYearLevel: years.Count > 0 ? years.Sum(y => y.Level) : null,
            MustHaveGaps: d.MustGaps,
            DefinitionSummary: summary,
            Structure: q?.Structure,
            Covers: q?.Covers ?? new List<QuoteCover>(),
            Strengths: d.Sheet.Strengths,
            WatchPoints: d.Sheet.WatchPoints);
    }

    private static InsurerProjection? BuildProjection(RowDraft d, bool recommended, ResearchBrief brief)
    {
        var q = d.Quote;
        if (q?.Projection is null || q.Projection.Years.Count == 0) return null;

        var input = q.Projection.Years.OrderBy(y => y.Year).ToList();
        decimal cumS = 0, cumL = 0;
        var years = new List<ProjectionYear>();
        int? steppedAbove = null, levelCheaperFrom = null;
        foreach (var y in input)
        {
            cumS += y.Stepped;
            cumL += y.Level;
            if (steppedAbove is null && y.Stepped > y.Level) steppedAbove = y.Year;
            if (levelCheaperFrom is null && cumS > cumL) levelCheaperFrom = y.Year;
            years.Add(new ProjectionYear(y.Year, y.Ages ?? string.Empty, y.Stepped, y.Level, cumS, cumL));
        }

        var first = years[0];
        var n = years.Count;
        var diff = first.Level - first.Stepped;
        var sb = new StringBuilder();
        sb.Append($"Level costs {Money(diff)} more in year 1 ({Money(Round2(diff / 12m))} a month). ");
        if (steppedAbove is int above)
            sb.Append($"The stepped premium passes the level premium in year {above}");
        else
            sb.Append($"The stepped premium stays below the level premium for all {n} years");
        if (levelCheaperFrom is int from)
        {
            sb.Append($", and level is cheaper in total from year {from}: over {n} years stepped totals {Money(cumS)} and level {Money(cumL)} — " +
                      $"{Money(cumS - cumL)} less with level, if the same cover is kept that long.");
        }
        else
        {
            sb.Append($", but over {n} years stepped still totals less: {Money(cumS)} against {Money(cumL)} for level — " +
                      $"{Money(cumL - cumS)} less with stepped. Level would only pay for itself if the same cover is kept well beyond {n} years.");
        }

        return new InsurerProjection(
            Insurer: d.Sheet.Insurer,
            Recommended: recommended,
            Label: q.Projection.Label ?? "Illustrative only — not a quote for future years.",
            AgesLabel: q.Projection.AgesLabel ?? "Age",
            Years: years,
            Year1Difference: diff,
            SteppedAboveLevelFromYear: steppedAbove,
            LevelCheaperInTotalFromYear: levelCheaperFrom,
            TotalStepped: cumS,
            TotalLevel: cumL,
            LevelNote: q.LevelOption?.Note,
            Summary: sb.ToString());
    }

    private static Recommendation BuildRecommendation(
        ResearchBrief brief, List<RowDraft> quoted, List<RowDraft> notQuoted, RowDraft? best, IReadOnlyList<InsurerProjection> projections)
    {
        const string forAdviser = "For Amelia, Senior Risk Adviser. Ivy's research supports the reasonable investigation of products in Amelia's best-interests steps — Amelia decides what to recommend, and this isn't advice to the client.";
        const string disclaimer = "Research for the adviser, not personal advice. Premiums are insurer quotes at standard rates before underwriting; projections are illustrative.";

        if (quoted.Count == 0)
        {
            return new Recommendation(null, null,
                "No recommendation yet — there are no insurer quotes on this case.",
                new[] { "Definitions are compared below. Premiums follow Penny's needs analysis (sums insured, structure, waiting and benefit periods)." },
                notQuoted.Select(d => $"{d.Sheet.Insurer} — {d.NotQuotedReason}").ToList(),
                string.Empty, null, forAdviser, disclaimer);
        }

        if (best is null)
        {
            return new Recommendation(null, null,
                "No insurer meets every must-have definition — Amelia to review before any recommendation.",
                quoted.Select(d => $"{d.Sheet.Insurer} — gap: " +
                    string.Join("; ", d.Results.Where(r => r.Item.Priority == BriefPriorities.MustHave && r.Result == "gap").Select(r => r.Note))).ToList(),
                notQuoted.Select(d => $"{d.Sheet.Insurer} — not quoted: {d.NotQuotedReason}").ToList(),
                string.Empty, null, forAdviser, disclaimer);
        }

        var q = best.Quote!;
        var others = quoted.Where(d => !ReferenceEquals(d, best)).ToList();
        var reasons = new List<string>();

        var musts = best.Results.Where(r => r.Item.Priority == BriefPriorities.MustHave).ToList();
        if (musts.Count > 0)
            reasons.Add($"Definitions first — meets every must-have for {brief.ClientFirstNames}: {string.Join("; ", musts.Select(m => m.Item.Title))}.");

        var differentiators = best.Results
            .Where(r => r.Result == "meets" && others.Any(o => o.Results.First(x => x.Item.Key == r.Item.Key).Result is "partly" or "gap"))
            .OrderBy(r => PriorityOrder(r.Item.Priority))
            .Take(4)
            .Select(r => $"{r.Item.Title}: {TrimEndPunctuation(r.Note)}.");
        reasons.AddRange(differentiators);

        var cheaper = others.Where(o => o.Quote!.Year1Premium < q.Year1Premium).ToList();
        if (cheaper.Count == 0)
        {
            var diffs = others.OrderBy(o => o.Quote!.Year1Premium)
                .Select(o => $"{Money(o.Quote!.Year1Premium - q.Year1Premium)} less than {o.Sheet.Insurer}").ToList();
            reasons.Add($"Price second — the lowest year-1 premium: {Money(q.Year1Premium)} stepped ({Money(Round2(q.Year1Premium / 12m))} a month)" +
                        (diffs.Count > 0 ? $", {JoinAnd(diffs)}." : "."));
        }
        else
        {
            reasons.Add($"Price second — {Money(q.Year1Premium)} stepped in year 1. " +
                        string.Join(" ", cheaper.Select(c => $"{c.Sheet.Insurer} is {Money(q.Year1Premium - c.Quote!.Year1Premium)} cheaper, but it falls short on the definitions above.")));
        }

        string? budgetCheck = null;
        if (brief.MonthlyBudget is decimal budget && q.OutsideSuperYear1 is decimal outside)
        {
            var monthly = Round2(outside / 12m);
            var within = monthly <= budget ? "within" : "over";
            budgetCheck = $"From cash flow: {Money(outside)} a year ({Money(monthly)} a month) — {within} {brief.ClientFirstNames}'s {Money(budget)} a month budget" +
                          (q.InsideSuperYear1 is decimal inside && inside > 0 ? $"; {Money(inside)} a year is paid from super." : ".");
            if (q.LevelOption?.OutsideSuperYear1 is decimal levelOutside)
            {
                var levelMonthly = Round2(levelOutside / 12m);
                budgetCheck += $" With level premiums it would be {Money(levelMonthly)} a month from cash flow ({(levelMonthly <= budget ? "also within" : "over")} budget).";
            }
            reasons.Add(budgetCheck);
        }

        // Be open about the recommended insurer's own shortfalls on what matters.
        foreach (var own in best.Results.Where(r => r.Item.Priority != BriefPriorities.Helpful && r.Result is "partly" or "gap"))
            reasons.Add($"To confirm before the SoA — {own.Item.Title}: {TrimEndPunctuation(own.Note)}.");

        var notChosen = new List<string>();
        foreach (var o in others)
        {
            var oq = o.Quote!;
            var price = oq.Year1Premium > q.Year1Premium
                ? $"{Money(oq.Year1Premium - q.Year1Premium)} a year dearer than {best.Sheet.Insurer}"
                : $"{Money(q.Year1Premium - oq.Year1Premium)} a year cheaper than {best.Sheet.Insurer}";
            var gaps = o.Results.Where(r => r.Item.Priority == BriefPriorities.MustHave && r.Result == "gap").ToList();
            if (gaps.Count > 0)
            {
                var line = $"{o.Sheet.Insurer} ({Money(oq.Year1Premium)}) — not suitable on definitions: {string.Join(" ", gaps.Select(g => g.Note))}";
                line += oq.Year1Premium < q.Year1Premium
                    ? $" It's {price}, but cheapest isn't best if the definition won't pay."
                    : $" It's also {price}.";
                notChosen.Add(line);
            }
            else
            {
                var shortfalls = o.Results.Where(r => r.Item.Priority != BriefPriorities.Helpful && r.Result is "partly" or "gap").ToList();
                notChosen.Add($"{o.Sheet.Insurer} ({Money(oq.Year1Premium)}) — meets the must-haves." +
                              (shortfalls.Count > 0 ? $" Shortfalls: {string.Join(" ", shortfalls.Select(s => TrimEndPunctuation(s.Note) + "."))}" : string.Empty) +
                              $" {Capitalise(price)}.");
            }
        }
        foreach (var d in notQuoted)
            notChosen.Add($"{d.Sheet.Insurer} — not quoted: {d.NotQuotedReason}");

        var projection = projections.FirstOrDefault(p => p.Recommended);
        var steppedVsLevel = projection is null
            ? string.Empty
            : projection.Summary + $" Stepped keeps the cost lower now; level suits a client who expects to keep the same cover for the long term. " +
              $"Amelia decides with {brief.ClientFirstNames}.";

        return new Recommendation(
            Insurer: best.Sheet.Insurer,
            PremiumStyle: "Stepped (level shown for Amelia to discuss)",
            Headline: $"Recommended for Amelia's review: {best.Sheet.Insurer} — {Money(q.Year1Premium)} a year stepped in year 1 ({Money(Round2(q.Year1Premium / 12m))} a month).",
            Reasons: reasons,
            NotChosen: notChosen,
            SteppedVsLevel: steppedVsLevel,
            BudgetCheck: budgetCheck,
            ForAdviser: forAdviser,
            Disclaimer: disclaimer);
    }

    private static string BuildCommissionNote(ResearchBrief brief, ComparisonRow? recommended)
    {
        var sb = new StringBuilder();
        sb.Append("Commission doesn't drive the ranking. Every insurer on the APL pays commission within the same Life Insurance Framework caps — " +
                  "up to 60% of the first-year premium upfront and 20% a year after that (tested on the commission excluding GST), with 100% clawed " +
                  "back if the cover is cancelled or reduced in year 1 and 60% in year 2. A dearer policy only pays more in dollars, which is one more " +
                  "reason the definitions come first.");
        if (recommended?.Year1Stepped is decimal premium)
        {
            sb.Append($" On {recommended.Insurer}'s {Money(premium)} stepped premium, commission at the caps is {Money(Round2(premium * 0.60m))} upfront " +
                      $"and {Money(Round2(premium * 0.20m))} a year from year 2 — disclosed in dollars in the Statement of Advice, and " +
                      $"{brief.ClientFirstNames} must give informed consent to the commission before the cover is issued.");
        }
        sb.Append($" See '{CommissionRulesTitle}'.");
        return sb.ToString();
    }

    private static int PriorityOrder(string priority) => priority switch
    {
        BriefPriorities.MustHave => 0,
        BriefPriorities.Important => 1,
        _ => 2
    };

    // ─────────────────────────────────────────────────────────────────────
    // Markdown tables and Mermaid charts (embedded verbatim by the agent)
    // ─────────────────────────────────────────────────────────────────────

    private static string BuildComparisonMarkdown(IReadOnlyList<ComparisonRow> rows, ResearchBrief brief)
    {
        var quoted = rows.Where(r => r.Quoted).ToList();
        var md = new StringBuilder();
        if (quoted.Count == 0)
        {
            md.AppendLine("| Insurer | APL listing | Status |");
            md.AppendLine("|---|---|---|");
            foreach (var r in rows)
                md.AppendLine($"| {EscapeMd(r.Insurer)} | {EscapeMd(r.PdsTitle)} | {EscapeMd(r.NotQuotedReason ?? "Not quoted")} |");
            return md.ToString().TrimEnd();
        }

        var cashFlow = quoted.Any(r => r.OutsideSuperYear1.HasValue);
        md.Append("| Rank | Insurer | Year-1 premium (stepped) | Monthly |");
        if (cashFlow) md.Append(" From cash flow |");
        md.AppendLine(" Level option (year 1) | 10-year total (stepped / level) | Definitions |");
        md.Append("|:-:|---|--:|--:|");
        if (cashFlow) md.Append("--:|");
        md.AppendLine("--:|--:|---|");

        foreach (var r in rows)
        {
            if (!r.Quoted)
            {
                md.Append($"| — | {EscapeMd(r.Insurer)} | — | — |");
                if (cashFlow) md.Append(" — |");
                md.AppendLine($" — | — | Not quoted: {EscapeMd(r.NotQuotedReason ?? string.Empty)} |");
                continue;
            }

            var name = r.Recommended ? $"**{EscapeMd(r.Insurer)}** (recommended)" : EscapeMd(r.Insurer);
            md.Append($"| {r.Rank}{(r.Recommended ? " ★" : string.Empty)} | {name} | {Money(r.Year1Stepped!.Value)} | {Money(r.MonthlyStepped!.Value)} |");
            if (cashFlow) md.Append($" {(r.CashFlowMonthly is decimal cf ? Money(cf) + " a month" : "—")} |");
            var totals = r.TenYearStepped is decimal ts && r.TenYearLevel is decimal tl ? $"{Money(ts)} / {Money(tl)}" : "—";
            md.AppendLine($" {(r.Year1Level is decimal l ? Money(l) : "—")} | {totals} | {EscapeMd(r.DefinitionSummary)} |");
        }

        md.AppendLine();
        md.Append("_Year-1 premiums are insurer quotes at standard rates before underwriting" +
                  (brief.MonthlyBudget is null ? string.Empty : "; \"from cash flow\" is the part paid outside super") +
                  ". 10-year totals are illustrative — same cover every year, no indexation and no change to premium rates._");
        return md.ToString();
    }

    private static string BuildDefinitionsMarkdown(IReadOnlyList<DefinitionRow> rows, IReadOnlyList<string> insurers)
    {
        if (rows.Count == 0 || insurers.Count == 0) return string.Empty;
        var md = new StringBuilder();
        md.AppendLine("| Definition that matters | Why it matters | " + string.Join(" | ", insurers.Select(EscapeMd)) + " |");
        md.AppendLine("|---|---|" + string.Concat(insurers.Select(_ => "---|")));
        foreach (var r in rows)
        {
            md.Append($"| **{EscapeMd(r.Title)}** ({r.Priority.ToLowerInvariant()}) | {EscapeMd(r.WhyItMatters)} |");
            foreach (var c in r.Insurers)
                md.Append($" {ResultIcon(c.Result)} {c.Label} — {EscapeMd(c.Note)} |");
            md.AppendLine();
        }
        return md.ToString().TrimEnd();
    }

    private static string BuildProjectionMarkdown(InsurerProjection p)
    {
        var md = new StringBuilder();
        md.AppendLine($"**{EscapeMd(p.Insurer)} — stepped vs level over {p.Years.Count} years (illustrative)**");
        md.AppendLine();
        md.AppendLine($"| Year | {EscapeMd(p.AgesLabel)} | Stepped | Level | Total paid (stepped) | Total paid (level) |");
        md.AppendLine("|:-:|:-:|--:|--:|--:|--:|");
        foreach (var y in p.Years)
        {
            md.AppendLine($"| {y.Year} | {EscapeMd(y.Ages)} | {Money(y.Stepped)} | {Money(y.Level)} | " +
                          $"{Money(y.CumulativeStepped)} | {Money(y.CumulativeLevel)} |");
        }
        md.AppendLine();
        md.Append($"_{EscapeMd(p.Label)}_");
        return md.ToString();
    }

    private static string BuildYear1ChartMermaid(IReadOnlyList<ComparisonRow> quoted)
    {
        var max = quoted.Max(r => Math.Max(r.Year1Stepped ?? 0m, r.Year1Level ?? 0m));
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"Year-1 premium by insurer: stepped (bars) and level (line)\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", quoted.Select(r => $"\"{EscapeMermaid(r.Recommended ? r.Insurer + " ★" : r.Insurer)}\""))}]");
        sb.AppendLine($"    y-axis \"AUD a year\" 0 --> {Num(NiceCeiling(max * 1.15m))}");
        sb.AppendLine($"    bar [{string.Join(", ", quoted.Select(r => Num(r.Year1Stepped ?? 0m)))}]");
        if (quoted.All(r => r.Year1Level.HasValue))
            sb.AppendLine($"    line [{string.Join(", ", quoted.Select(r => Num(r.Year1Level!.Value)))}]");
        sb.Append("```");
        return sb.ToString();
    }

    private static string BuildProjectionMermaid(InsurerProjection p)
    {
        var max = p.Years.Max(y => Math.Max(y.Stepped, y.Level));
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"{EscapeMermaid(p.Insurer)}: stepped vs level premiums, illustrative\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", p.Years.Select(y => $"\"Yr {y.Year}\""))}]");
        sb.AppendLine($"    y-axis \"AUD a year\" 0 --> {Num(NiceCeiling(max * 1.15m))}");
        sb.AppendLine($"    line [{string.Join(", ", p.Years.Select(y => Num(y.Stepped)))}]");
        sb.AppendLine($"    line [{string.Join(", ", p.Years.Select(y => Num(y.Level)))}]");
        sb.Append("```");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Policy wording lookup (claim cases) and document analysis
    // ─────────────────────────────────────────────────────────────────────

    internal async Task<WordingLookup> BuildWordingLookupAsync(ResearchBrief brief)
    {
        ProductSheet? sheet = null;
        if (!string.IsNullOrWhiteSpace(brief.ClaimInsurer))
            sheet = (await LoadAplSheetsAsync()).FirstOrDefault(s => MatchesInsurer(s, brief.ClaimInsurer!));
        return BuildWordingLookup(brief, sheet);
    }

    internal static WordingLookup BuildWordingLookup(ResearchBrief brief, ProductSheet? sheet)
    {
        var items = brief.Items.Select(i =>
        {
            var def = sheet?.Definitions.FirstOrDefault(d => string.Equals(d.Key, i.Key, StringComparison.OrdinalIgnoreCase));
            return new WordingItem(
                Key: i.Key,
                Heading: i.Title,
                Cover: def?.Cover ?? "Rule",
                Wording: i.Wording ?? def?.Wording ?? "Not on the product sheet — ask Ben's agent for the policy schedule.",
                PlainEnglish: def?.PlainEnglish,
                WhatItMeans: i.WhyItMatters,
                Source: i.Source ?? sheet?.PdsTitle ?? string.Empty);
        }).ToList();

        var citations = items.Select(i => i.Source)
            .Concat(brief.RuleDocuments)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        var handoverUrl = string.IsNullOrWhiteSpace(brief.HandoverAgentId)
            ? null
            : $"/agents/{brief.HandoverAgentId}?caseNumber={Uri.EscapeDataString(brief.CaseNumber)}";

        var md = new StringBuilder();
        if (brief.PolicyOnFile.Count > 0)
        {
            md.AppendLine("**Policy on file**");
            md.AppendLine();
            foreach (var p in brief.PolicyOnFile) md.AppendLine($"- {p}");
            md.AppendLine();
        }
        if (items.Count > 0)
        {
            md.AppendLine("**Wording that applies to this claim**");
            md.AppendLine();
            var n = 1;
            foreach (var i in items)
            {
                md.AppendLine($"{n++}. **{i.Heading}** — _{i.Source}_");
                md.AppendLine($"   > {i.Wording}");
                md.AppendLine();
                if (!string.IsNullOrWhiteSpace(i.PlainEnglish))
                {
                    md.AppendLine($"   *In plain English:* {i.PlainEnglish}");
                    md.AppendLine();
                }
                md.AppendLine($"   *What it means here:* {i.WhatItMeans}");
                md.AppendLine();
            }
        }

        return new WordingLookup(
            CaseNumber: brief.CaseNumber,
            Client: brief.Client,
            Scenario: brief.Scenario,
            Title: brief.Title,
            Summary: brief.Summary,
            Insurer: sheet?.Insurer ?? brief.ClaimInsurer,
            PolicyNumber: brief.PolicyNumber,
            PdsTitle: sheet?.PdsTitle,
            SheetUrl: sheet?.Source,
            KeyFacts: brief.KeyFacts,
            PolicyOnFile: brief.PolicyOnFile,
            Items: items,
            Handover: brief.Handover,
            HandoverAgentId: brief.HandoverAgentId,
            HandoverUrl: handoverUrl,
            CaseStatus: brief.CaseStatus,
            NextSteps: brief.NextSteps,
            HumanApproval: brief.HumanApproval,
            Markdown: md.ToString().TrimEnd(),
            Citations: citations);
    }

    private static PolicyDocumentAnalysis BuildDocumentAnalysis(ProductSheet sheet, ResearchBrief? brief)
    {
        CaseFit? fit = null;
        if (brief is not null && brief.Kind == BriefKinds.Advice)
        {
            var quote = brief.QuotesOnFile
                ? sheet.Quotes.FirstOrDefault(q => string.Equals(q.Scenario, brief.Scenario, StringComparison.OrdinalIgnoreCase))
                : null;
            var notQuoted = quote is not null
                ? null
                : sheet.NotQuoted.FirstOrDefault(n => string.Equals(n.Scenario, brief.Scenario, StringComparison.OrdinalIgnoreCase))?.Reason
                  ?? "No quote on file for this case.";
            var notes = brief.Items.Select(i =>
            {
                var (result, note) = ResultFor(sheet, quote, i.Key);
                var def = sheet.Definitions.FirstOrDefault(d => string.Equals(d.Key, i.Key, StringComparison.OrdinalIgnoreCase));
                return new FitNote(i.Key, i.Title, i.Priority, i.WhyItMatters, result, ResultLabel(result), note, def?.Wording);
            }).ToList();
            fit = new CaseFit(brief.CaseNumber, brief.Client, brief.Kind, quote, notQuoted, notes,
                Array.Empty<string>(), Array.Empty<WordingItem>(), brief.Handover);
        }
        else if (brief is not null && brief.Kind == BriefKinds.Claim)
        {
            var applies = brief.ClaimInsurer is not null && MatchesInsurer(sheet, brief.ClaimInsurer);
            fit = applies
                ? new CaseFit(brief.CaseNumber, brief.Client, brief.Kind, null, null, Array.Empty<FitNote>(),
                    brief.PolicyOnFile, BuildWordingLookup(brief, sheet).Items, brief.Handover)
                : new CaseFit(brief.CaseNumber, brief.Client, brief.Kind, null, null, Array.Empty<FitNote>(),
                    brief.PolicyOnFile, Array.Empty<WordingItem>(),
                    brief.ClaimInsurer is null
                        ? brief.Handover
                        : $"This claim is on a {brief.ClaimInsurer} policy ({brief.PolicyNumber}) — read that insurer's wording instead. {brief.Handover}");
        }

        return new PolicyDocumentAnalysis(
            Insurer: sheet.Insurer,
            ProductName: sheet.ProductName,
            PdsTitle: sheet.PdsTitle,
            Source: sheet.Source,
            AplListing: sheet.AplListing,
            Summary: sheet.Summary,
            Strengths: sheet.Strengths,
            WatchPoints: sheet.WatchPoints,
            PremiumStyles: sheet.PremiumStyles,
            CoverTypes: sheet.CoverTypes,
            Definitions: sheet.Definitions,
            Features: sheet.Features,
            Exclusions: sheet.Exclusions,
            ForCase: fit,
            Cite: $"Cite as '{sheet.PdsTitle}'.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Comparison workbook (ClosedXML tables + Open XML charts)
    // ─────────────────────────────────────────────────────────────────────

    private static readonly XLColor BrandFill = XLColor.FromHtml("#0F172A");
    private static readonly XLColor AccentColour = XLColor.FromHtml("#0F766E");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#CCFBF1");
    private static readonly XLColor RecommendedFill = XLColor.FromHtml("#F0FDFA");
    private static readonly XLColor MeetsFill = XLColor.FromHtml("#DCFCE7");
    private static readonly XLColor PartlyFill = XLColor.FromHtml("#FEF3C7");
    private static readonly XLColor GapFill = XLColor.FromHtml("#FEE2E2");
    private const string MoneyFormat = "$#,##0";
    private const string MoneyCentsFormat = "$#,##0.00";

    // One colour per insurer in the charts; level premiums use the same colour, dashed.
    private static readonly string[] SeriesColours = { "0F766E", "1D4ED8", "9333EA", "B45309", "BE123C" };

    private ComparisonWorkbookResult WriteWorkbook(InsurerComparison c, string? notesForAdviser)
    {
        var fileName = $"{SafeFileToken(c.CaseNumber)}-insurer-comparison-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}.xlsx";
        var outputDir = OutputDirectory;
        Directory.CreateDirectory(outputDir);
        var fullPath = Path.Combine(outputDir, fileName);
        var sheets = new List<string>();
        var charts = new List<ChartSpec>();

        using (var wb = new XLWorkbook())
        {
            WriteSummarySheet(wb, c, notesForAdviser, sheets);
            WriteComparisonSheet(wb, c, sheets, charts);
            WriteDefinitionsSheet(wb, c, sheets);
            WriteProjectionSheet(wb, c, sheets, charts);
            wb.SaveAs(fullPath);
        }

        // Charts are added to a copy so a failure never leaves a broken file behind.
        var chartTitles = new List<string>();
        if (charts.Count > 0)
        {
            var workingCopy = fullPath + ".charts";
            try
            {
                File.Copy(fullPath, workingCopy, overwrite: true);
                AddCharts(workingCopy, charts);
                File.Move(workingCopy, fullPath, overwrite: true);
                chartTitles.AddRange(charts.Select(ch => $"{ch.SheetName}: {ch.Title}"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not add charts to {File}; the workbook keeps its tables", fileName);
                TryDelete(workingCopy);
            }
        }

        PruneOldWorkbooks(outputDir);
        _logger.LogInformation("research_generateComparisonWorkbook wrote {File}", fileName);

        return new ComparisonWorkbookResult(
            CaseNumber: c.CaseNumber,
            FileName: fileName,
            DownloadUrl: DownloadUrlPrefix + Uri.EscapeDataString(fileName),
            Sheets: sheets,
            Charts: chartTitles,
            RecommendedInsurer: c.Recommendation.Insurer,
            Error: null);
    }

    private static void WriteSummarySheet(XLWorkbook wb, InsurerComparison c, string? notes, List<string> sheets)
    {
        var ws = wb.Worksheets.Add("Summary");
        sheets.Add(ws.Name);

        ws.Cell(1, 1).Value = "Caldova Life Advice — Insurance Research";
        ws.Range(1, 1, 1, 2).Merge().Style
            .Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(BrandFill);
        ws.Cell(2, 1).Value = $"Insurer comparison · {c.CaseNumber} · {c.Client}";
        ws.Range(2, 1, 2, 2).Merge().Style.Font.SetBold().Font.SetFontColor(AccentColour);
        ws.Cell(3, 1).Value =
            $"Prepared {AuToday()} by the Insurance Research Agent (Ivy) for Amelia, Senior Risk Adviser. " +
            "Research for the adviser — Amelia gives the advice in the Statement of Advice.";
        ws.Range(3, 1, 3, 2).Merge().Style.Font.SetItalic().Alignment.SetWrapText(true);
        ws.Row(3).Height = 32;

        WriteHeader(ws, 5, "Item", "Detail");
        var row = 6;

        void Field(string label, string? value, bool bold = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            if (bold) ws.Cell(row, 2).Style.Font.SetBold();
            row++;
        }

        void Bullets(string label, IEnumerable<string> values)
        {
            var first = true;
            foreach (var v in values.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                ws.Cell(row, 1).Value = first ? label : string.Empty;
                ws.Cell(row, 2).Value = "• " + v;
                first = false;
                row++;
            }
        }

        Field("Case", c.CaseNumber);
        Field("Client", c.Client);
        Field("Research", c.Summary);
        Bullets("Cover researched", c.CoverRequested);
        Bullets("Key facts", c.KeyFacts);
        Field("Recommended for Amelia's review", c.Recommendation.Headline, bold: true);
        Bullets("Why", c.Recommendation.Reasons);
        Bullets("Not chosen", c.Recommendation.NotChosen);
        Field("Stepped vs level", c.Recommendation.SteppedVsLevel);
        Bullets("Notes", c.Observations);
        Field("Commission", c.CommissionNote);
        Field("Research notes for Amelia", notes);
        Bullets("Next steps", c.NextSteps);
        Field("Human approval", c.HumanApproval);
        Field("For the adviser", c.Recommendation.ForAdviser);
        Bullets("Sources", c.Citations);
        Field("Status", c.Recommendation.Disclaimer);
        Field("Generated (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

        ws.Range(6, 1, Math.Max(6, row - 1), 1).Style.Font.SetBold().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        ws.Column(1).Width = 32;
        ws.Column(2).Width = 120;
        ws.Column(2).Style.Alignment.SetWrapText(true);
    }

    private static void WriteComparisonSheet(XLWorkbook wb, InsurerComparison c, List<string> sheets, List<ChartSpec> charts)
    {
        var ws = wb.Worksheets.Add("Comparison");
        sheets.Add(ws.Name);
        ws.Cell(1, 1).Value = "Insurers compared — definitions first, price second";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        ws.Cell(2, 1).Value = "Year-1 premiums are insurer quotes at standard rates before underwriting. 10-year totals are illustrative — " +
                              "same cover every year, no indexation and no change to premium rates.";
        ws.Cell(2, 1).Style.Font.SetItalic();

        const int header = 4;
        WriteHeader(ws, header,
            "Rank", "Insurer", "Year-1 premium (stepped)", "Year-1 premium (level)", "Monthly (stepped)",
            "Paid from super (year 1)", "Paid from cash flow (year 1)", "10-year total (stepped)", "10-year total (level)",
            "Definitions", "Quote / status");

        var quoted = c.Rows.Where(r => r.Quoted).ToList();
        var row = header + 1;
        var firstData = row;
        foreach (var r in c.Rows)
        {
            ws.Cell(row, 1).Value = r.Quoted ? (r.Recommended ? $"{r.Rank} ★" : r.Rank.ToString(CultureInfo.InvariantCulture)) : "—";
            ws.Cell(row, 2).Value = r.Insurer;
            SetMoney(ws.Cell(row, 3), r.Year1Stepped);
            SetMoney(ws.Cell(row, 4), r.Year1Level);
            SetMoney(ws.Cell(row, 5), r.MonthlyStepped, cents: true);
            SetMoney(ws.Cell(row, 6), r.InsideSuperYear1);
            SetMoney(ws.Cell(row, 7), r.OutsideSuperYear1);
            SetMoney(ws.Cell(row, 8), r.TenYearStepped);
            SetMoney(ws.Cell(row, 9), r.TenYearLevel);
            ws.Cell(row, 10).Value = r.DefinitionSummary;
            ws.Cell(row, 11).Value = r.Quoted
                ? $"{r.QuoteReference} · {r.PremiumStyle}{(r.Recommended ? " · recommended for Amelia's review" : string.Empty)}"
                : $"Not quoted — {r.NotQuotedReason}";
            if (r.Recommended) ws.Range(row, 1, row, 11).Style.Font.SetBold().Fill.SetBackgroundColor(RecommendedFill);
            row++;
        }

        if (quoted.Count > 0)
        {
            var lastQuoted = firstData + quoted.Count - 1;
            var chartTop = row + 1;
            charts.Add(new ChartSpec(
                SheetName: ws.Name,
                Kind: ChartKind.Column,
                Title: "Year-1 premium by insurer — stepped vs level",
                CategoriesRef: Ref(ws.Name, 2, firstData, 2, lastQuoted),
                Categories: quoted.Select(r => r.Insurer).ToList(),
                Series: new[]
                {
                    new ChartSeriesSpec("Year-1 premium (stepped)", Ref(ws.Name, 3, header), Ref(ws.Name, 3, firstData, 3, lastQuoted),
                        quoted.Select(r => r.Year1Stepped ?? 0m).ToList(), "14B8A6", false),
                    new ChartSeriesSpec("Year-1 premium (level)", Ref(ws.Name, 4, header), Ref(ws.Name, 4, firstData, 4, lastQuoted),
                        quoted.Select(r => r.Year1Level ?? 0m).ToList(), "94A3B8", false),
                },
                ValueAxisTitle: "AUD a year",
                FromCol: 0, FromRow: chartTop - 1, ToCol: 7, ToRow: chartTop + 17));
            row = chartTop + 19;
        }

        ws.Cell(row, 1).Value = "Recommendation for Amelia's review";
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(AccentColour);
        row++;
        WriteParagraph(ws, row++, c.Recommendation.Headline, 11, bold: true);
        foreach (var reason in c.Recommendation.Reasons) WriteParagraph(ws, row++, "• " + reason, 11);
        foreach (var other in c.Recommendation.NotChosen) WriteParagraph(ws, row++, "• Not chosen: " + other, 11);
        row++;

        if (quoted.Count > 0)
        {
            ws.Cell(row, 1).Value = "Cover breakdown";
            ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(AccentColour);
            row++;
            WriteHeader(ws, row, "Insurer", "Cover", "Sum insured / benefit", "Ownership", "Year-1 premium (stepped)", "Year-1 premium (level)");
            row++;
            foreach (var r in quoted)
            {
                foreach (var cover in r.Covers)
                {
                    ws.Cell(row, 1).Value = r.Insurer;
                    ws.Cell(row, 2).Value = cover.Cover;
                    ws.Cell(row, 3).Value = cover.SumInsured ?? string.Empty;
                    ws.Cell(row, 4).Value = cover.Ownership ?? string.Empty;
                    SetMoney(ws.Cell(row, 5), cover.Year1Premium);
                    SetMoney(ws.Cell(row, 6), cover.LevelYear1Premium);
                    row++;
                }
                if (!string.IsNullOrWhiteSpace(r.Structure))
                {
                    ws.Cell(row, 1).Value = r.Insurer;
                    ws.Cell(row, 2).Value = "Structure: " + r.Structure;
                    ws.Range(row, 2, row, 6).Merge().Style.Font.SetItalic().Alignment.SetWrapText(true);
                    ws.Row(row).Height = 30;
                    row++;
                }
            }
            row++;
        }

        ws.Cell(row, 1).Value = "Commission";
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(AccentColour);
        row++;
        WriteParagraph(ws, row++, c.CommissionNote, 11, height: 60);

        ws.SheetView.FreezeRows(header);
        ws.Column(1).Width = 18;
        ws.Column(2).Width = 34;
        foreach (var col in new[] { 3, 4, 5, 6, 7, 8, 9 }) ws.Column(col).Width = 16;
        ws.Column(10).Width = 48;
        ws.Column(11).Width = 52;
        ws.Range(header, 10, Math.Max(header, firstData + c.Rows.Count - 1), 11).Style.Alignment.SetWrapText(true);
    }

    private static void WriteDefinitionsSheet(XLWorkbook wb, InsurerComparison c, List<string> sheets)
    {
        var ws = wb.Worksheets.Add("Definitions");
        sheets.Add(ws.Name);
        ws.Cell(1, 1).Value = $"Definitions that matter — {c.Client}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        ws.Cell(2, 1).Value = "Definitions first: an insurer that misses a must-have is ranked below every insurer that meets them, whatever the price. " +
                              "Cheapest isn't best if the definition won't pay.";
        ws.Cell(2, 1).Style.Font.SetItalic();

        if (c.Definitions.Count == 0) return;
        var insurers = c.Definitions[0].Insurers.Select(i => i.Insurer).ToList();
        const int header = 4;
        var headers = new List<string> { "Definition", "Priority", "Why it matters" };
        headers.AddRange(insurers);
        WriteHeader(ws, header, headers.ToArray());

        var row = header + 1;
        foreach (var d in c.Definitions)
        {
            ws.Cell(row, 1).Value = d.Title;
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 2).Value = d.Priority;
            ws.Cell(row, 3).Value = d.WhyItMatters;
            for (var i = 0; i < d.Insurers.Count; i++)
            {
                var cell = ws.Cell(row, 4 + i);
                cell.Value = $"{d.Insurers[i].Label} — {d.Insurers[i].Note}";
                var fill = d.Insurers[i].Result switch
                {
                    "meets" => MeetsFill,
                    "partly" => PartlyFill,
                    "gap" => GapFill,
                    _ => null
                };
                if (fill is not null) cell.Style.Fill.SetBackgroundColor(fill);
            }
            row++;
        }
        ws.Range(header + 1, 1, row - 1, 3 + insurers.Count).Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);

        row++;
        ws.Cell(row, 1).Value = "Policy wording (from each insurer's PDS extract)";
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(AccentColour);
        row++;
        WriteHeader(ws, row, "Definition", "Insurer", "Wording", "Source");
        row++;
        var wordingStart = row;
        foreach (var d in c.Definitions)
        {
            foreach (var cell in d.Insurers.Where(x => !string.IsNullOrWhiteSpace(x.Wording)))
            {
                ws.Cell(row, 1).Value = d.Title;
                ws.Cell(row, 2).Value = cell.Insurer;
                ws.Cell(row, 3).Value = cell.Wording;
                ws.Cell(row, 4).Value = cell.PdsTitle;
                row++;
            }
        }
        if (row > wordingStart)
            ws.Range(wordingStart, 1, row - 1, 4).Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);

        ws.SheetView.FreezeRows(header);
        ws.Column(1).Width = 36;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 70;
        for (var i = 0; i < insurers.Count; i++) ws.Column(4 + i).Width = Math.Max(ws.Column(4 + i).Width, 44);
    }

    private static void WriteProjectionSheet(XLWorkbook wb, InsurerComparison c, List<string> sheets, List<ChartSpec> charts)
    {
        var ws = wb.Worksheets.Add("Projection");
        sheets.Add(ws.Name);
        ws.Cell(1, 1).Value = "Stepped vs level premiums over 10 years — illustrative only";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);

        var projections = c.Projections;
        if (projections.Count == 0)
        {
            ws.Cell(3, 1).Value = "No projection — there are no insurer quotes on this case yet.";
            return;
        }

        ws.Cell(2, 1).Value = projections[0].Label;
        ws.Cell(2, 1).Style.Font.SetItalic();

        const int header = 4;
        var headers = new List<string> { "Year", projections[0].AgesLabel };
        foreach (var p in projections) { headers.Add($"{p.Insurer} stepped"); headers.Add($"{p.Insurer} level"); }
        foreach (var p in projections) { headers.Add($"{p.Insurer} total paid (stepped)"); headers.Add($"{p.Insurer} total paid (level)"); }
        WriteHeader(ws, header, headers.ToArray());

        var years = projections.Max(p => p.Years.Count);
        var firstData = header + 1;
        for (var i = 0; i < years; i++)
        {
            var r = firstData + i;
            ws.Cell(r, 1).Value = $"Year {i + 1}";
            ws.Cell(r, 2).Value = projections[0].Years.ElementAtOrDefault(i)?.Ages ?? string.Empty;
            for (var p = 0; p < projections.Count; p++)
            {
                var y = projections[p].Years.ElementAtOrDefault(i);
                if (y is null) continue;
                SetMoney(ws.Cell(r, 3 + p * 2), y.Stepped);
                SetMoney(ws.Cell(r, 4 + p * 2), y.Level);
                SetMoney(ws.Cell(r, 3 + projections.Count * 2 + p * 2), y.CumulativeStepped);
                SetMoney(ws.Cell(r, 4 + projections.Count * 2 + p * 2), y.CumulativeLevel);
            }
        }
        var lastData = firstData + years - 1;

        var totalRow = lastData + 1;
        ws.Cell(totalRow, 1).Value = $"{years}-year total";
        for (var p = 0; p < projections.Count; p++)
        {
            SetMoney(ws.Cell(totalRow, 3 + p * 2), projections[p].TotalStepped);
            SetMoney(ws.Cell(totalRow, 4 + p * 2), projections[p].TotalLevel);
        }
        ws.Range(totalRow, 1, totalRow, headers.Count).Style.Font.SetBold();

        var row = totalRow + 2;
        foreach (var p in projections)
            WriteParagraph(ws, row++, $"{p.Insurer}{(p.Recommended ? " (recommended for Amelia's review)" : string.Empty)}: {p.Summary}", Math.Max(8, headers.Count));
        row++;

        var series = new List<ChartSeriesSpec>();
        for (var p = 0; p < projections.Count; p++)
        {
            var colour = SeriesColours[p % SeriesColours.Length];
            var steppedCol = 3 + p * 2;
            series.Add(new ChartSeriesSpec($"{projections[p].Insurer} stepped", Ref(ws.Name, steppedCol, header),
                Ref(ws.Name, steppedCol, firstData, steppedCol, lastData), projections[p].Years.Select(y => y.Stepped).ToList(), colour, false));
            series.Add(new ChartSeriesSpec($"{projections[p].Insurer} level", Ref(ws.Name, steppedCol + 1, header),
                Ref(ws.Name, steppedCol + 1, firstData, steppedCol + 1, lastData), projections[p].Years.Select(y => y.Level).ToList(), colour, true));
        }
        charts.Add(new ChartSpec(
            SheetName: ws.Name,
            Kind: ChartKind.Line,
            Title: "Annual premium over 10 years — stepped (solid) vs level (dashed), illustrative",
            CategoriesRef: Ref(ws.Name, 1, firstData, 1, lastData),
            Categories: Enumerable.Range(1, years).Select(i => $"Year {i}").ToList(),
            Series: series,
            ValueAxisTitle: "AUD a year",
            FromCol: 0, FromRow: row - 1, ToCol: 9, ToRow: row + 21));

        ws.SheetView.FreezeRows(header);
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 18;
        for (var col = 3; col <= headers.Count; col++) ws.Column(col).Width = 19;
        ws.Range(header, 1, header, headers.Count).Style.Alignment.SetWrapText(true);
        ws.Row(header).Height = 32;
    }

    private static void WriteHeader(IXLWorksheet ws, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(row, i + 1).Value = headers[i];
        ws.Range(row, 1, row, headers.Length).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(HeaderFill)
            .Alignment.SetWrapText(true)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Top);
    }

    private static void WriteParagraph(IXLWorksheet ws, int row, string text, int columns, bool bold = false, double height = 30)
    {
        ws.Cell(row, 1).Value = text;
        var range = ws.Range(row, 1, row, columns).Merge();
        range.Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        if (bold) range.Style.Font.SetBold();
        ws.Row(row).Height = height;
    }

    private static void SetMoney(IXLCell cell, decimal? value, bool cents = false)
    {
        if (value is not decimal v) return;
        cell.Value = v;
        cell.Style.NumberFormat.Format = cents ? MoneyCentsFormat : MoneyFormat;
    }

    /// <summary>
    /// ClosedXML can't create charts, so they're injected with the Open XML
    /// SDK after the workbook is saved: one DrawingsPart per sheet holding a
    /// ChartPart per chart, with series that reference the sheet's cells (and
    /// cached values so the charts render before Excel recalculates).
    /// </summary>
    private static void AddCharts(string path, IReadOnlyList<ChartSpec> specs)
    {
        using var doc = SpreadsheetDocument.Open(path, true);
        var wbPart = doc.WorkbookPart ?? throw new InvalidOperationException("The workbook part is missing.");
        var validator = new OpenXmlValidator();
        uint frameId = 2;

        foreach (var group in specs.GroupBy(s => s.SheetName))
        {
            var sheet = wbPart.Workbook.Descendants<S.Sheet>().FirstOrDefault(s => s.Name?.Value == group.Key)
                ?? throw new InvalidOperationException($"Sheet '{group.Key}' not found.");
            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!.Value!);

            var drawingsPart = wsPart.DrawingsPart;
            if (drawingsPart is null)
            {
                drawingsPart = wsPart.AddNewPart<DrawingsPart>();
                var newDrawing = new Xdr.WorksheetDrawing();
                newDrawing.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
                newDrawing.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
                drawingsPart.WorksheetDrawing = newDrawing;
            }
            var drawing = drawingsPart.WorksheetDrawing ??= new Xdr.WorksheetDrawing();

            foreach (var spec in group)
            {
                var chartPart = drawingsPart.AddNewPart<ChartPart>();
                chartPart.ChartSpace = BuildChartSpace(spec);
                chartPart.ChartSpace.Save();
                var chartErrors = validator.Validate(chartPart).Take(3).ToList();
                if (chartErrors.Count > 0)
                    throw new InvalidOperationException("Chart XML is invalid: " + string.Join(" | ", chartErrors.Select(e => e.Description)));
                drawing.Append(BuildChartAnchor(spec, drawingsPart.GetIdOfPart(chartPart), frameId++));
            }
            drawing.Save();

            var drawingErrors = validator.Validate(drawingsPart).Take(3).ToList();
            if (drawingErrors.Count > 0)
                throw new InvalidOperationException("Drawing XML is invalid: " + string.Join(" | ", drawingErrors.Select(e => e.Description)));

            var worksheet = wsPart.Worksheet;
            if (!worksheet.Elements<S.Drawing>().Any())
            {
                var element = new S.Drawing { Id = wsPart.GetIdOfPart(drawingsPart) };
                // <drawing> must come before these elements in the worksheet schema.
                var before = worksheet.ChildElements.FirstOrDefault(e =>
                    e is S.LegacyDrawing or S.LegacyDrawingHeaderFooter or S.Picture or S.OleObjects or S.Controls
                        or S.WebPublishItems or S.TableParts or S.WorksheetExtensionList);
                if (before is not null) worksheet.InsertBefore(element, before);
                else worksheet.Append(element);
            }
            worksheet.Save();
        }
    }

    private static C.ChartSpace BuildChartSpace(ChartSpec spec)
    {
        const uint categoryAxisId = 48650112U;
        const uint valueAxisId = 48672768U;

        var chartSpace = new C.ChartSpace();
        chartSpace.AddNamespaceDeclaration("c", "http://schemas.openxmlformats.org/drawingml/2006/chart");
        chartSpace.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
        chartSpace.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        chartSpace.Append(new C.Date1904 { Val = false });
        chartSpace.Append(new C.EditingLanguage { Val = "en-AU" });
        chartSpace.Append(new C.RoundedCorners { Val = false });

        var chart = new C.Chart();
        chart.Append(ChartTitle(spec.Title, 1300));
        chart.Append(new C.AutoTitleDeleted { Val = false });

        var plotArea = new C.PlotArea(new C.Layout());
        if (spec.Kind == ChartKind.Column)
        {
            var bar = new C.BarChart(
                new C.BarDirection { Val = C.BarDirectionValues.Column },
                new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
                new C.VaryColors { Val = false });
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var s = spec.Series[i];
                bar.Append(new C.BarChartSeries(
                    new C.Index { Val = (uint)i },
                    new C.Order { Val = (uint)i },
                    SeriesText(s),
                    new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = s.ColourHex })),
                    new C.InvertIfNegative { Val = false },
                    CategoryData(spec),
                    ValueData(s)));
            }
            bar.Append(new C.GapWidth { Val = (ushort)80 });
            bar.Append(new C.AxisId { Val = categoryAxisId });
            bar.Append(new C.AxisId { Val = valueAxisId });
            plotArea.Append(bar);
        }
        else
        {
            var line = new C.LineChart(
                new C.Grouping { Val = C.GroupingValues.Standard },
                new C.VaryColors { Val = false });
            for (var i = 0; i < spec.Series.Count; i++)
            {
                var s = spec.Series[i];
                line.Append(new C.LineChartSeries(
                    new C.Index { Val = (uint)i },
                    new C.Order { Val = (uint)i },
                    SeriesText(s),
                    new C.ChartShapeProperties(new A.Outline(
                        new A.SolidFill(new A.RgbColorModelHex { Val = s.ColourHex }),
                        new A.PresetDash { Val = s.Dashed ? A.PresetLineDashValues.Dash : A.PresetLineDashValues.Solid })
                    { Width = 28575 }),
                    new C.Marker(new C.Symbol { Val = s.Dashed ? C.MarkerStyleValues.None : C.MarkerStyleValues.Circle }, new C.Size { Val = (byte)5 }),
                    CategoryData(spec),
                    ValueData(s),
                    new C.Smooth { Val = false }));
            }
            line.Append(new C.ShowMarker { Val = true });
            line.Append(new C.AxisId { Val = categoryAxisId });
            line.Append(new C.AxisId { Val = valueAxisId });
            plotArea.Append(line);
        }

        plotArea.Append(new C.CategoryAxis(
            new C.AxisId { Val = categoryAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
            new C.NumberingFormat { FormatCode = "General", SourceLinked = true },
            new C.MajorTickMark { Val = C.TickMarkValues.Outside },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = valueAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.AutoLabeled { Val = true },
            new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
            new C.LabelOffset { Val = (ushort)100 },
            new C.NoMultiLevelLabels { Val = false }));

        plotArea.Append(new C.ValueAxis(
            new C.AxisId { Val = valueAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Left },
            new C.MajorGridlines(),
            ChartTitle(spec.ValueAxisTitle, 1000),
            new C.NumberingFormat { FormatCode = "\"$\"#,##0", SourceLinked = false },
            new C.MajorTickMark { Val = C.TickMarkValues.Outside },
            new C.MinorTickMark { Val = C.TickMarkValues.None },
            new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
            new C.CrossingAxis { Val = categoryAxisId },
            new C.Crosses { Val = C.CrossesValues.AutoZero },
            new C.CrossBetween { Val = C.CrossBetweenValues.Between }));

        chart.Append(plotArea);
        chart.Append(new C.Legend(new C.LegendPosition { Val = C.LegendPositionValues.Bottom }, new C.Overlay { Val = false }));
        chart.Append(new C.PlotVisibleOnly { Val = true });
        chart.Append(new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap });
        chartSpace.Append(chart);
        return chartSpace;
    }

    private static C.Title ChartTitle(string text, int size) => new(
        new C.ChartText(new C.RichText(
            new A.BodyProperties(),
            new A.ListStyle(),
            new A.Paragraph(
                new A.ParagraphProperties(new A.DefaultRunProperties { FontSize = size, Bold = true }),
                new A.Run(new A.RunProperties { Language = "en-AU", FontSize = size, Bold = true }, new A.Text(text))))),
        new C.Overlay { Val = false });

    private static C.SeriesText SeriesText(ChartSeriesSpec s) => new(
        new C.StringReference(
            new C.Formula(s.NameRef),
            new C.StringCache(new C.PointCount { Val = 1U }, new C.StringPoint(new C.NumericValue(s.Name)) { Index = 0U })));

    private static C.CategoryAxisData CategoryData(ChartSpec spec)
    {
        var cache = new C.StringCache(new C.PointCount { Val = (uint)spec.Categories.Count });
        for (var i = 0; i < spec.Categories.Count; i++)
            cache.Append(new C.StringPoint(new C.NumericValue(spec.Categories[i])) { Index = (uint)i });
        return new C.CategoryAxisData(new C.StringReference(new C.Formula(spec.CategoriesRef), cache));
    }

    private static C.Values ValueData(ChartSeriesSpec s)
    {
        var cache = new C.NumberingCache(new C.FormatCode("General"), new C.PointCount { Val = (uint)s.Values.Count });
        for (var i = 0; i < s.Values.Count; i++)
            cache.Append(new C.NumericPoint(new C.NumericValue(s.Values[i].ToString(CultureInfo.InvariantCulture))) { Index = (uint)i });
        return new C.Values(new C.NumberReference(new C.Formula(s.ValuesRef), cache));
    }

    private static Xdr.TwoCellAnchor BuildChartAnchor(ChartSpec spec, string chartRelationshipId, uint id)
    {
        var anchor = new Xdr.TwoCellAnchor { EditAs = Xdr.EditAsValues.OneCell };
        anchor.Append(new Xdr.FromMarker(
            new Xdr.ColumnId(spec.FromCol.ToString(CultureInfo.InvariantCulture)),
            new Xdr.ColumnOffset("0"),
            new Xdr.RowId(spec.FromRow.ToString(CultureInfo.InvariantCulture)),
            new Xdr.RowOffset("0")));
        anchor.Append(new Xdr.ToMarker(
            new Xdr.ColumnId(spec.ToCol.ToString(CultureInfo.InvariantCulture)),
            new Xdr.ColumnOffset("0"),
            new Xdr.RowId(spec.ToRow.ToString(CultureInfo.InvariantCulture)),
            new Xdr.RowOffset("0")));

        var frame = new Xdr.GraphicFrame { Macro = string.Empty };
        frame.Append(new Xdr.NonVisualGraphicFrameProperties(
            new Xdr.NonVisualDrawingProperties { Id = id, Name = $"Chart {id}" },
            new Xdr.NonVisualGraphicFrameDrawingProperties()));
        frame.Append(new Xdr.Transform(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = 0L, Cy = 0L }));
        frame.Append(new A.Graphic(new A.GraphicData(new C.ChartReference { Id = chartRelationshipId })
        {
            Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
        }));
        anchor.Append(frame);
        anchor.Append(new Xdr.ClientData());
        return anchor;
    }

    private void PruneOldWorkbooks(string outputDir)
    {
        try
        {
            var dir = new DirectoryInfo(outputDir);
            foreach (var stale in dir.EnumerateFiles("*.xlsx").OrderByDescending(f => f.CreationTimeUtc).Skip(MaxWorkbooksKept).ToList())
                stale.Delete();
            foreach (var leftover in dir.EnumerateFiles("*.charts").Where(f => f.CreationTimeUtc < DateTime.UtcNow.AddMinutes(-10)).ToList())
                leftover.Delete();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not prune old comparison workbooks");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Loading product sheets
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Read the product sheet of every insurer on the APL from wwwroot/research/samples/.</summary>
    internal async Task<IReadOnlyList<ProductSheet>> LoadAplSheetsAsync()
    {
        var list = new List<ProductSheet>();
        foreach (var file in AplSheetFiles)
        {
            var path = SamplesUrlPrefix + file;
            var text = await ReadLocalAsync(path);
            var sheet = text is null ? null : ParseProductSheet(text, path);
            if (sheet is not null) list.Add(sheet);
            else _logger.LogWarning("Product sheet missing or unreadable: {Path}", path);
        }
        return list;
    }

    private async Task<LoadedDocument> LoadDocumentAsync(string insurerOrUrl)
    {
        var trimmed = insurerOrUrl.Trim();

        // An app path, e.g. /research/samples/corella-life.json
        if (trimmed.StartsWith('/'))
            return ToDocument(await ReadLocalAsync(trimmed), trimmed);

        // A URL — this app's own sample folder (e.g. through a dev tunnel) is read from disk.
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (uri.AbsolutePath.StartsWith(SamplesUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var local = await ReadLocalAsync(Uri.UnescapeDataString(uri.AbsolutePath));
                if (local is not null) return ToDocument(local, trimmed);
            }
            return ToDocument(await FetchRemoteAsync(uri), trimmed);
        }

        // A product sheet file name, e.g. corella-life.json
        if (Regex.IsMatch(trimmed, @"^[A-Za-z0-9._-]+\.(json|md|txt)$", RegexOptions.IgnoreCase) && !trimmed.Contains(".."))
        {
            var path = SamplesUrlPrefix + trimmed;
            return ToDocument(await ReadLocalAsync(path), path);
        }

        // An insurer name, short name, code or policy number
        var match = (await LoadAplSheetsAsync()).FirstOrDefault(s => MatchesInsurer(s, trimmed));
        return match is null ? new LoadedDocument(null, null, trimmed) : new LoadedDocument(match, null, match.Source);

        static LoadedDocument ToDocument(string? text, string source)
        {
            if (text is null) return new LoadedDocument(null, null, source);
            var sheet = ParseProductSheet(text, source);
            return sheet is not null ? new LoadedDocument(sheet, null, source) : new LoadedDocument(null, text, source);
        }
    }

    internal static bool MatchesInsurer(ProductSheet sheet, string value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return false;
        if (string.Equals(sheet.Insurer, v, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sheet.ShortName, v, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sheet.Code, v, StringComparison.OrdinalIgnoreCase))
            return true;
        // Policy numbers carry the insurer's prefix, e.g. CR-19033105.
        if (!string.IsNullOrEmpty(sheet.PolicyPrefix) && v.StartsWith(sheet.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return true;
        return sheet.ShortName.Length >= 4 && v.Contains(sheet.ShortName, StringComparison.OrdinalIgnoreCase);
    }

    internal static ProductSheet? ParseProductSheet(string content, string source)
    {
        var trimmed = content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!trimmed.StartsWith('{')) return null;
        try
        {
            var sheet = JsonSerializer.Deserialize<ProductSheet>(trimmed, ReadOpts);
            if (sheet is null || string.IsNullOrWhiteSpace(sheet.Insurer)) return null;
            sheet.Source = source;
            if (string.IsNullOrWhiteSpace(sheet.ShortName)) sheet.ShortName = sheet.Insurer.Split(' ')[0];
            return sheet;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Read a file under wwwroot/research/samples/ only (path-traversal safe).</summary>
    private async Task<string?> ReadLocalAsync(string appPath)
    {
        var pathOnly = appPath.Split('?', '#')[0];
        var rel = pathOnly.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var samplesRoot = Path.GetFullPath(Path.Combine(WebRoot, "research", "samples"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(WebRoot, rel));
        if (!full.StartsWith(samplesRoot, StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(full) ? await File.ReadAllTextAsync(full) : null;
    }

    /// <summary>Fetch a public http(s) document; private, loopback and link-local addresses are refused.</summary>
    private async Task<string?> FetchRemoteAsync(Uri uri)
    {
        try
        {
            if (!await IsPublicHostAsync(uri))
            {
                _logger.LogInformation("Refused to fetch a non-public address: {Url}", Sanitize(uri.ToString()));
                return null;
            }

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Product sheet fetch returned {Status} for {Url}", (int)resp.StatusCode, Sanitize(uri.ToString()));
                return null;
            }
            if (resp.Content.Headers.ContentLength is long length && length > MaxRemoteDocumentBytes)
                return null;
            var text = await resp.Content.ReadAsStringAsync();
            return text.Length > MaxRemoteDocumentBytes ? text[..MaxRemoteDocumentBytes] : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            _logger.LogInformation(ex, "Product sheet fetch failed for {Url}", Sanitize(uri.ToString()));
            return null;
        }
    }

    private static async Task<bool> IsPublicHostAsync(Uri uri)
    {
        if (uri.IsLoopback) return false;
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try { addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost); }
            catch (SocketException) { return false; }
        }
        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6Multicast ||
                     ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None));
        }
        var b = ip.GetAddressBytes();
        return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 ||
                 (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                 (b[0] == 192 && b[1] == 168) ||
                 (b[0] == 169 && b[1] == 254) ||
                 (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    internal static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>"$3,180" for whole dollars, "$193.50" otherwise.</summary>
    internal static string Money(decimal v)
    {
        var r = Round2(v);
        return r == decimal.Truncate(r) ? r.ToString("C0", AuCulture) : r.ToString("C2", AuCulture);
    }

    private static string Num(decimal v) => Round2(v).ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal NiceCeiling(decimal value)
    {
        var step = value switch
        {
            <= 2_000m => 250m,
            <= 10_000m => 1_000m,
            <= 50_000m => 5_000m,
            _ => 10_000m
        };
        return Math.Max(step, Math.Ceiling(value / step) * step);
    }

    /// <summary>Today's date in Melbourne, e.g. "Mon 23 Nov 2026".</summary>
    internal static string AuToday()
    {
        var now = DateTime.UtcNow;
        try
        {
            now = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne"));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Fall back to UTC.
        }
        return now.ToString("ddd d MMM yyyy", AuCulture);
    }

    internal static string EscapeMd(string? s) => (s ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string EscapeMermaid(string s) => s.Replace("\"", "'");

    private static string JoinAnd(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1]
    };

    private static string TrimEndPunctuation(string? s) => (s ?? string.Empty).Trim().TrimEnd('.', ';', ',');

    private static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static string ColumnLetter(int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            var rem = (column - 1) % 26;
            name = (char)('A' + rem) + name;
            column = (column - 1) / 26;
        }
        return name;
    }

    private static string Ref(string sheet, int col, int row) => $"'{sheet}'!${ColumnLetter(col)}${row}";

    private static string Ref(string sheet, int col1, int row1, int col2, int row2) =>
        $"'{sheet}'!${ColumnLetter(col1)}${row1}:${ColumnLetter(col2)}${row2}";

    private static string SafeFileToken(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-');
        var token = sb.ToString().Trim('-');
        return token.Length == 0 ? "case" : token.Length > 60 ? token[..60] : token;
    }

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');

    internal static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string JsonError(string message) => Serialize(new { error = message });
}

// ─────────────────────────────────────────────────────────────────────────
// Research briefs — one per scenario, plus generic fallbacks for live cases
// ─────────────────────────────────────────────────────────────────────────

internal static class BriefKinds
{
    public const string Advice = "advice";
    public const string Claim = "claim";
    public const string Unknown = "unknown";
}

internal static class BriefPriorities
{
    public const string MustHave = "Must have";
    public const string Important = "Important";
    public const string Helpful = "Helpful";
    public const string AppliesToClaim = "Applies to this claim";
}

internal static class ResearchScenarios
{
    public const string FamilyProtection = "family-protection";
    public const string FraudulentClaim = "fraudulent-claim";
    public const string BusinessProtection = "business-protection";
    public const string IncomeProtectionClaim = "income-protection-claim";
    public const string DeathClaim = "death-claim";
}

internal static class ResearchBriefs
{
    private const string Lyrebird = "Lyrebird Life";
    private const string Harbourline = "Harbourline Life";
    private const string Corella = "Corella Life";

    /// <summary>
    /// Map a case to its scenario: by onboarding sample id, by the seeded
    /// <c>-LIF00n</c> tag, by a scenario id passed directly, or by request type
    /// plus client name.
    /// </summary>
    public static string? ScenarioFor(CaseRecord? record, string caseNumber)
    {
        switch ((record?.SampleId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "onboarding-priya": return ResearchScenarios.FamilyProtection;
            case "onboarding-grace": return ResearchScenarios.FraudulentClaim;
            case "onboarding-george": return ResearchScenarios.BusinessProtection;
            case "onboarding-tom": return ResearchScenarios.IncomeProtectionClaim;
            case "onboarding-robert": return ResearchScenarios.DeathClaim;
        }

        var id = (caseNumber ?? string.Empty).Trim();
        var tag = Regex.Match(id, @"-LIF00([1-5])$", RegexOptions.IgnoreCase);
        if (tag.Success)
        {
            return tag.Groups[1].Value switch
            {
                "1" => ResearchScenarios.FamilyProtection,
                "2" => ResearchScenarios.FraudulentClaim,
                "3" => ResearchScenarios.BusinessProtection,
                "4" => ResearchScenarios.IncomeProtectionClaim,
                _ => ResearchScenarios.DeathClaim
            };
        }

        switch (id.ToLowerInvariant())
        {
            case ResearchScenarios.FamilyProtection:
            case ResearchScenarios.FraudulentClaim:
            case ResearchScenarios.BusinessProtection:
            case ResearchScenarios.IncomeProtectionClaim:
            case ResearchScenarios.DeathClaim:
                return id.ToLowerInvariant();
        }

        if (record is null) return null;
        bool Named(params string[] names) => names.Any(n => (record.ClientName ?? string.Empty).Contains(n, StringComparison.OrdinalIgnoreCase));
        return record.RequestType switch
        {
            RequestTypes.NewProtectionAdvice when Named("Priya Natarajan") => ResearchScenarios.FamilyProtection,
            RequestTypes.TraumaClaimBankChange when Named("Grace Williams") => ResearchScenarios.FraudulentClaim,
            RequestTypes.BusinessProtectionAdvice when Named("George Papadopoulos", "Harpreet Kaur") => ResearchScenarios.BusinessProtection,
            RequestTypes.IncomeProtectionClaim when Named("Tom Bradley") => ResearchScenarios.IncomeProtectionClaim,
            RequestTypes.DeathClaim when Named("Robert Chen", "Mei Chen") => ResearchScenarios.DeathClaim,
            _ => null
        };
    }

    public static ResearchBrief For(CaseRecord? record, string caseNumber)
    {
        var scenario = ScenarioFor(record, caseNumber);
        string Number(string seeded) => record?.CaseNumber ?? seeded;

        return scenario switch
        {
            ResearchScenarios.FamilyProtection => Priya(Number("REQ-20261103-LIF001")),
            ResearchScenarios.FraudulentClaim => Grace(Number("REQ-20261106-LIF002")),
            ResearchScenarios.BusinessProtection => George(Number("REQ-20261109-LIF003")),
            ResearchScenarios.IncomeProtectionClaim => Tom(Number("REQ-20261111-LIF004")),
            ResearchScenarios.DeathClaim => Robert(Number("REQ-20261113-LIF005")),
            _ => record is null ? Unknown(caseNumber) : Generic(record)
        };
    }

    // ── Scenario 1 — Priya: new family protection advice ─────────────────

    private static ResearchBrief Priya(string caseNumber) => new(
        Scenario: ResearchScenarios.FamilyProtection,
        Kind: BriefKinds.Advice,
        CaseNumber: caseNumber,
        Client: "Priya Natarajan",
        ClientFirstNames: "Priya",
        Title: "Family protection — compare the APL insurers",
        Summary: "Compare the APL insurers for Priya's family protection package — definitions first, then premiums: life $750,000, " +
                 "TPD $600,000, trauma $150,000 and income protection of $6,125 a month.",
        KeyFacts: new[]
        {
            "Priya Natarajan, 34, physiotherapist at a private clinic in Brunswick; salary $105,000 plus super; non-smoker.",
            "Lives in Coburg with her partner Arjun Mehta and daughter Asha (2); their second baby is due in March 2027.",
            "Bought a home in October 2026 — joint mortgage $680,000; savings $45,000. Referred by Hannah Lewis (Yarra Home Loans) with Priya's consent.",
            "Health disclosed: mild asthma (well controlled with a preventer inhaler); lower back strain in 2023 (six weeks of physio, full recovery).",
            "Existing cover: Riverbend Super default death and TPD cover of $210,000 (kept). Kestrel Retail Super death cover of $50,000 stops on 30 Nov 2026 under Protecting Your Super unless Priya elects to keep it — she keeps it until the new cover starts.",
        },
        CoverRequested: new[]
        {
            "Life cover $750,000 with a terminal illness benefit — inside super",
            "TPD $600,000 — any occupation inside super, with an own-occupation extension outside super (super-linked)",
            "Trauma cover $150,000 — outside super",
            "Income protection $6,125 a month (70% of $105,000), 90-day waiting period, benefit period to age 65 — outside super",
        },
        MonthlyBudget: 300m,
        Items: new[]
        {
            new BriefItem("tpd-own-occupation", "TPD — own occupation for a physiotherapist", BriefPriorities.MustHave,
                "Priya is a physiotherapist. A back, shoulder or hand injury could end her physio career while she could still do other work — an own-occupation definition pays in that case; an any-occupation definition might not."),
            new BriefItem("ip-benefit-basis", "Income protection — how the benefit is worked out", BriefPriorities.MustHave,
                "Her $105,000 salary carries the family and the $680,000 mortgage. The benefit is capped at 70% of her earnings in the 12 months before disability — $6,125 a month."),
            new BriefItem("ip-periods", "Income protection — 90-day wait, paid to age 65", BriefPriorities.MustHave,
                "Savings and leave cover the first 90 days; after that the benefit needs to run to age 65 if she can't return to work."),
            new BriefItem("tpd-super-linking", "Super-linking (Penny's inside/outside super structure)", BriefPriorities.Important,
                "Penny's structure holds life and any-occupation TPD inside super, with the own-occupation part outside super — linking keeps part of the premium off the family's cash flow."),
            new BriefItem("exclusion-review", "Exclusion review (2023 back strain)", BriefPriorities.Important,
                "She disclosed a back strain in 2023. If underwriting adds a back exclusion to income protection, the chance to have it reviewed after symptom-free years matters."),
            new BriefItem("ip-long-claims", "Income protection — claims longer than 2 years", BriefPriorities.Important,
                "Her benefit period runs to age 65. A switch to an any-occupation test after 2 years could stop payments while she still can't work as a physio."),
            new BriefItem("trauma-cancer", "Trauma — how a cancer diagnosis is confirmed", BriefPriorities.Helpful,
                "Trauma pays a lump sum on a serious illness such as cancer; the evidence each insurer needs to confirm the diagnosis differs."),
        },
        Observations: new[]
        {
            "Trauma and own-occupation TPD can't be held inside super (a trustee can only provide insured benefits that match a condition of release), which is why Penny's structure holds them outside super — see 'Insurance inside super — permitted cover, Protecting Your Super and opt-in rules'.",
            "Income protection premiums held outside super are generally tax-deductible because the cover protects her salary; premiums for life, TPD and trauma cover aren't — see 'Tax on life insurance — premiums and benefits'.",
            "Nothing is being replaced: Priya keeps her Riverbend Super cover and keeps the Kestrel Retail Super cover until the new cover is in force.",
        },
        QuotesOnFile: true,
        ClaimInsurer: null,
        PolicyNumber: null,
        PolicyOnFile: Array.Empty<string>(),
        Handover: "Penny (Paraplanning) puts the research into the Statement of Advice; Amelia reviews it, gives the advice and signs.",
        HandoverAgentId: "paraplanning",
        CaseStatus: "Completed — Corella Life cover in force from 18 Nov 2026 (policy CR-26118204)",
        NextSteps: new[]
        {
            "Send the comparison and the workbook to Penny for the Statement of Advice — Amelia reviews the research and gives the advice.",
            "Amelia talks Priya through stepped and level premiums; the choice is hers to make with Priya.",
            "Once Priya signs the Authority to Proceed, Uma's agent lodges the application with the chosen insurer.",
            "Keep the Kestrel Retail Super cover until the new cover is in force (Priya's decision, with Amelia).",
        },
        HumanApproval: "Yes — Amelia reviews this research, gives the advice and signs the Statement of Advice. The insurer and the premium style are her recommendation to Priya, not the agent's.",
        RuleDocuments: new[]
        {
            ResearchMcpTools.AplResearchTitle,
            ResearchMcpTools.AdviceRulesTitle,
            ResearchMcpTools.InsideSuperTitle,
            ResearchMcpTools.CommissionRulesTitle,
            ResearchMcpTools.TaxTitle,
        });

    // ── Scenario 3 — George and Harpreet: business protection ────────────

    private static ResearchBrief George(string caseNumber) => new(
        Scenario: ResearchScenarios.BusinessProtection,
        Kind: BriefKinds.Advice,
        CaseNumber: caseNumber,
        Client: "George Papadopoulos and Harpreet Kaur (Northcote Custom Joinery Pty Ltd)",
        ClientFirstNames: "George and Harpreet",
        Title: "Business protection — compare the APL insurers",
        Summary: "Compare the APL insurers for Northcote Custom Joinery's business protection — buy-sell life and TPD of $1,200,000 " +
                 "for each owner and $400,000 key person cover on George.",
        KeyFacts: new[]
        {
            "George Papadopoulos, 52, master joiner and co-founder (50%) of Northcote Custom Joinery Pty Ltd; Harpreet Kaur, 47, operations and finance director (50%). Both non-smokers.",
            "The business was founded in 2011: 14 staff, revenue $3.1m, net profit $520,000. Valued at $2,400,000 by Marcus Tan (Bayside Business Accountants) at 30 Jun 2026.",
            "Referred by Marcus Tan on 9 Nov 2026 — there is no succession plan in place.",
            "Their lawyer drafts the buy-sell agreement; Caldova doesn't draft legal documents.",
            "Total cover on the case is $2,800,000, so the advice gets Theo's second review ($2,000,000 or more).",
        },
        CoverRequested: new[]
        {
            "Buy-sell: life and TPD $1,200,000 on George, owned by George",
            "Buy-sell: life and TPD $1,200,000 on Harpreet, owned by Harpreet",
            "Key person: life and TPD $400,000 on George, owned by the company for a revenue purpose",
        },
        MonthlyBudget: null,
        Items: new[]
        {
            new BriefItem("business-buy-sell", "Buy-sell cover — self-owned life and TPD", BriefPriorities.MustHave,
                "Each owner's 50% share is worth $1,200,000. Self-owned cover funds the buy-sell agreement, so the surviving owner can buy the share and the family receives its value."),
            new BriefItem("business-key-person", "Key person cover — company-owned, revenue purpose", BriefPriorities.MustHave,
                "Marcus estimates losing George would cost about $400,000 of profit over 18 months while a replacement is found and trained."),
            new BriefItem("financial-underwriting", "Financial underwriting for $1.2m+ a life", BriefPriorities.Important,
                "Each owner's cover is $1,200,000 or more, so the insurer needs business financial evidence. Using Marcus's valuation and two years' accounts gets the cover in place sooner."),
            new BriefItem("tpd-business-cover", "TPD definition on the business cover", BriefPriorities.Important,
                "George works with his hands as a master joiner; Harpreet runs operations and finance. Whether TPD is own or any occupation decides when it pays — and the buy-sell agreement's TPD trigger should match the policy."),
            new BriefItem("exclusion-review", "Loading and exclusion review", BriefPriorities.Helpful,
                "At 52, a health finding at underwriting (such as blood pressure or cholesterol) is more likely — the chance to have a loading reviewed matters."),
        },
        Observations: new[]
        {
            "The tax treatment of key person and buy-sell premiums and benefits depends on the purpose and the ownership — Marcus Tan, as their accountant, confirms it (see 'Tax on life insurance — premiums and benefits').",
            "Caldova doesn't draft the buy-sell agreement — the owners' lawyer does, and its death and TPD triggers should match each policy's definitions.",
            "Quotes are at standard rates before underwriting; the insurer may add a loading or an exclusion once it has the medical evidence.",
        },
        QuotesOnFile: true,
        ClaimInsurer: null,
        PolicyNumber: null,
        PolicyOnFile: Array.Empty<string>(),
        Handover: "Theo gives the second review; Penny puts the research into the Statement of Advice, and Amelia gives the advice and signs.",
        HandoverAgentId: "paraplanning",
        CaseStatus: "Completed — Northbank Life cover in force from 20 Nov 2026 (NB-26300418, NB-26300419, NB-26300420)",
        NextSteps: new[]
        {
            "Theo's second review — total cover on the case is $2,800,000 ($2,000,000 or more).",
            "Send the comparison and the workbook to Penny for the Statement of Advice — Amelia reviews the research and gives the advice.",
            "With George and Harpreet's consent, ask Marcus Tan to confirm the tax treatment, and their lawyer to match the buy-sell agreement's triggers to the policy definitions.",
            "After the Authority to Proceed, Uma's agent arranges the underwriting requirements with the chosen insurer.",
        },
        HumanApproval: "Yes — total cover of $2,800,000 needs Theo's second review, and Amelia gives the advice and signs the Statement of Advice.",
        RuleDocuments: new[]
        {
            ResearchMcpTools.AplResearchTitle,
            ResearchMcpTools.AdviceRulesTitle,
            ResearchMcpTools.CommissionRulesTitle,
            ResearchMcpTools.TaxTitle,
        });

    // ── Scenario 2 — Grace: trauma claim request on a changed contact ────

    private static ResearchBrief Grace(string caseNumber) => new(
        Scenario: ResearchScenarios.FraudulentClaim,
        Kind: BriefKinds.Claim,
        CaseNumber: caseNumber,
        Client: "Grace Williams",
        ClientFirstNames: "Grace",
        Title: "Policy wording lookup — Lyrebird Life trauma cover",
        Summary: "Policy wording for the trauma claim request on Grace Williams's Lyrebird Life policy. There's nothing to compare — " +
                 "the request is with Ben (Claims) and Felix (Financial Crime).",
        KeyFacts: new[]
        {
            "Grace Williams, 61, semi-retired school principal (relief teaching), Hawthorn; a Caldova client since 2019 (CL-100377).",
            "At her annual review on 28 Sep 2026 Grace told Amelia she'd be in Italy from 20 Oct to 1 Dec 2026.",
            "On 2 Nov 2026 a caller changed the mobile and email on Grace's file; on 6 Nov a request arrived from the new email for a $200,000 trauma claim for breast cancer, to be paid into a new account ending 4471 in the name 'G Williams Consulting Pty Ltd'.",
            "The oncologist's report attached to the request has no histopathology report.",
        },
        CoverRequested: Array.Empty<string>(),
        MonthlyBudget: null,
        Items: new[]
        {
            new BriefItem("trauma-cancer", "Trauma — cancer definition", BriefPriorities.AppliesToClaim,
                "The oncologist's report attached to this request has no histopathology report. These terms are the same as in Lyrebird's 2019 PDS, which covers Grace's policy."),
            new BriefItem("trauma-claim-evidence", "What a trauma claim needs", BriefPriorities.AppliesToClaim,
                "A claim form signed by Grace as policy owner, her certified ID, the specialist's report and the histopathology report — and any change of contact or bank details confirmed through the mobile or email held before the change."),
            new BriefItem("benefit-payment", "Who the benefit is paid to", BriefPriorities.AppliesToClaim,
                "The trauma benefit can only be paid to Grace, as policy owner, into an account in her own name. An account in the name 'G Williams Consulting Pty Ltd' can't be used — the policy owner is Grace, not a company."),
            new BriefItem("contact-changes", "Changes of contact or bank details", BriefPriorities.AppliesToClaim,
                "Any change to Grace's details is confirmed through the mobile or email held before the change (her verified mobile is 0491 570 157), and Lyrebird calls the policy owner on the verified number before paying into bank details changed in the last 30 days."),
            new BriefItem("claims-integrity", "Lyrebird's Claims Integrity team", BriefPriorities.AppliesToClaim,
                "Whether to alert Lyrebird's Claims Integrity team about this request is Theo's decision, after Felix's review."),
            new BriefItem("nominated-beneficiaries", "Life cover and nominated beneficiaries", BriefPriorities.AppliesToClaim,
                "Grace's $500,000 life cover and her nominations (Daniel 50%, James 50%) aren't affected by this request."),
        },
        Observations: Array.Empty<string>(),
        QuotesOnFile: false,
        ClaimInsurer: Lyrebird,
        PolicyNumber: "LB-19004417",
        PolicyOnFile: new[]
        {
            "Insurer: Lyrebird Life — policy LB-19004417 (outside super, stepped premiums)",
            "Policy owner and life insured: Grace Williams",
            "Life cover $500,000 — nominated beneficiaries Daniel 50%, James 50%",
            "Trauma cover $200,000",
        },
        Handover: "Ben's Claims Agent owns this request and Felix's Financial Crime Agent is reviewing the contact and bank-detail changes. Ivy's lookup is wording only — it doesn't assess the claim, and Grace is treated as the person to protect.",
        HandoverAgentId: "claims",
        CaseStatus: "Blocked — no claim lodged; $200,000 protected",
        NextSteps: new[]
        {
            "Share the wording with Ben (Claims) and Felix (Financial Crime). Nothing is lodged with Lyrebird Life while they check the request.",
            "Any contact with Grace goes to her verified mobile (0491 570 157) — Cara's agent handles it.",
            "Blocking the request and alerting Lyrebird Life's Claims Integrity team are Theo's decisions.",
        },
        HumanApproval: "Yes — blocking a request on financial-crime grounds and referring it to the insurer's fraud team are Theo's decisions, and lodging any claim needs approval.",
        RuleDocuments: new[]
        {
            ResearchMcpTools.ClaimsRulesTitle,
            "Guide — financial crime red flags and controls for life insurance",
        });

    // ── Scenario 4 — Tom: income protection claim ────────────────────────

    private static ResearchBrief Tom(string caseNumber) => new(
        Scenario: ResearchScenarios.IncomeProtectionClaim,
        Kind: BriefKinds.Claim,
        CaseNumber: caseNumber,
        Client: "Tom Bradley",
        ClientFirstNames: "Tom",
        Title: "Policy wording lookup — Harbourline Life income protection",
        Summary: "Policy wording for Tom Bradley's income protection claim on his Harbourline Life policy. There's nothing to compare — " +
                 "Ben's Claims Agent builds the claim pack, calculates the benefit and lodges the claim after approval.",
        KeyFacts: new[]
        {
            "Tom Bradley, 41, self-employed electrician (sole trader, Bradley Electrical), Frankston; client since 2023 (CL-102265). His wife Kylie is his support person, with his written authority.",
            "Fell from a ladder on a job on Wed 14 Oct 2026 — torn rotator cuff in his right shoulder; surgery on 21 Oct; his surgeon expects four to six months off work.",
            "Earnings evidence on file: net business income of $96,000 for the 12 months to 30 Sep 2026 (BAS and profit and loss statement).",
            "Tom is under financial stress — a vulnerable client, so every update is plain and kind.",
        },
        CoverRequested: Array.Empty<string>(),
        MonthlyBudget: null,
        Items: new[]
        {
            new BriefItem("ip-benefit-basis", "How the monthly benefit is worked out", BriefPriorities.AppliesToClaim,
                "The benefit is the lesser of the insured $7,000 and 70% of Tom's average monthly earnings in the 12 months before 14 Oct 2026. On the evidence on file — $96,000, or $8,000 a month — that's 70% × $8,000 = $5,600 a month. Ben's agent confirms the calculation."),
            new BriefItem("ip-total-disability", "Total disability", BriefPriorities.AppliesToClaim,
                "Ben's agent checks the surgeon's report against this definition — Tom isn't working after his shoulder surgery and is under his surgeon's care."),
            new BriefItem("ip-periods", "Waiting period and when payments start", BriefPriorities.AppliesToClaim,
                "With a 30-day waiting period from 14 Oct 2026, the benefit accrues from 13 Nov 2026 and is paid monthly in arrears — the first payment about 13 Dec 2026. Claim payments increase each year in line with CPI."),
            new BriefItem("ip-long-claims", "Claims longer than 2 years", BriefPriorities.AppliesToClaim,
                "If Tom's claim lasted more than 2 years, Harbourline would assess it against any occupation suited to his education, training or experience, with 3 months' notice. His surgeon expects four to six months off work."),
            new BriefItem("ip-claim-evidence", "What the claim needs", BriefPriorities.AppliesToClaim,
                "A claim form signed by Tom (or Kylie, with his written authority), his certified ID, the surgeon's report, and earnings evidence for the 12 months before 14 Oct 2026 — his BAS, profit and loss statement or tax returns."),
            new BriefItem("benefit-payment", "How benefits are paid", BriefPriorities.AppliesToClaim,
                "Paid monthly to Tom as the policy owner, only into an account in his own name. The benefit replaces income, so it's taxable income (see 'Tax on life insurance — premiums and benefits')."),
            new BriefItem("rule-idii", "Income protection rules for newer policies", BriefPriorities.AppliesToClaim,
                "Tom's policy started in March 2023, so its 12-month earnings basis follows APRA's measures — the insured $7,000 is a ceiling, not a guarantee.",
                Wording: "APRA expects individual income protection policies issued from 1 October 2021 to base benefits on the life insured's earnings at the time of the claim (no older than 12 months), and to cap all benefits combined at 90% of earnings for the first 6 months of a claim and 70% after that.",
                Source: ResearchMcpTools.ClaimsRulesTitle),
        },
        Observations: Array.Empty<string>(),
        QuotesOnFile: false,
        ClaimInsurer: Harbourline,
        PolicyNumber: "HL-23051877",
        PolicyOnFile: new[]
        {
            "Insurer: Harbourline Life — policy HL-23051877 (income protection, outside super), started 15 Mar 2023",
            "Insured monthly benefit $7,000 (set from $120,000 a year at application)",
            "Waiting period 30 days; benefit period to age 65",
            "Premiums $2,880 a year",
        },
        Handover: "Ben's Claims Agent checks the policy and evidence, builds the claim pack, calculates the benefit and lodges the claim with Harbourline Life after Theo's approval.",
        HandoverAgentId: "claims",
        CaseStatus: "Partially approved — accepted at $5,600 a month, not the $7,000 insured",
        NextSteps: new[]
        {
            "Share the wording with Ben — his agent calculates the benefit and builds the claim pack.",
            "Under the Life Insurance Code of Practice, Harbourline decides an income-related claim within 2 months of receiving it (or of the end of the waiting period, if later) — Ben's agent tracks the clock.",
            "Note for Amelia (not advice): Tom's insured $7,000 is more than his recent earnings support. She may look at the benefit at his next review — the adviser decides.",
        },
        HumanApproval: "Yes — lodging the claim needs Theo's approval, and Tom's financial stress means a vulnerable-client review (Amelia calls him personally before the letter).",
        RuleDocuments: new[]
        {
            ResearchMcpTools.ClaimsRulesTitle,
            ResearchMcpTools.TaxTitle,
        });

    // ── Scenario 5 — Robert: death claim on Mei's policy ─────────────────

    private static ResearchBrief Robert(string caseNumber) => new(
        Scenario: ResearchScenarios.DeathClaim,
        Kind: BriefKinds.Claim,
        CaseNumber: caseNumber,
        Client: "Robert Chen",
        ClientFirstNames: "Robert",
        Title: "Policy wording lookup — Corella Life life cover",
        Summary: "Policy wording for the death claim on Mei Chen's Corella Life policy, where Robert is the nominated beneficiary. " +
                 "There's nothing to compare — Ben's Claims Agent handles the claim with care.",
        KeyFacts: new[]
        {
            "Mei Chen (CL-101934), a pharmacist, died suddenly of a brain aneurysm on Mon 12 Oct 2026, aged 42. Her husband Robert (CL-101935) is caring for Lily (12) and Max (9).",
            "Robert emailed Amelia on Fri 13 Nov 2026 with a certified copy of the death certificate and his ID.",
            "Mei's Riverbend Super account also had default death cover of $180,000 — a separate claim with the fund's trustee. A super death benefit paid to a spouse as a lump sum is tax-free.",
            "A bereaved family — every message is gentle and goes through one named contact, Amelia.",
        },
        CoverRequested: Array.Empty<string>(),
        MonthlyBudget: null,
        Items: new[]
        {
            new BriefItem("nominated-beneficiaries", "Who receives the life cover", BriefPriorities.AppliesToClaim,
                "Mei nominated Robert for 100%, so Corella pays the $1,000,000 directly to Robert — it doesn't go through Mei's estate."),
            new BriefItem("earlier-policies", "Mei's 2019 policy terms", BriefPriorities.AppliesToClaim,
                "Mei's policy was advised by Amelia in March 2019 — its life cover, terminal illness and nomination terms are the same as those described here."),
            new BriefItem("death-claim-evidence", "What a death claim needs", BriefPriorities.AppliesToClaim,
                "The claim form, Robert's certified ID and the certified death certificate (both already provided), the policy number CR-19033105, Mei's nomination on Corella's records and Robert's bank details."),
            new BriefItem("benefit-payment", "How the benefit is paid", BriefPriorities.AppliesToClaim,
                "Only into an account in Robert's own name. Life cover paid to a nominated beneficiary outside super is generally not taxable (see 'Tax on life insurance — premiums and benefits')."),
            new BriefItem("claims-support", "Support through the claim", BriefPriorities.AppliesToClaim,
                "Corella gives the family a dedicated case manager for the claim, alongside Amelia as their one named contact at Caldova."),
            new BriefItem("rule-icas29", "Time limit on non-disclosure", BriefPriorities.AppliesToClaim,
                "Mei's policy started in March 2019 — more than seven years ago — so it can't be avoided for a non-fraudulent non-disclosure.",
                Wording: "Under the Insurance Contracts Act 1984 s29(3), an insurer can avoid a life policy for a non-fraudulent failure to disclose or misrepresentation only within three years of the contract being entered into.",
                Source: ResearchMcpTools.UnderwritingRulesTitle),
        },
        Observations: Array.Empty<string>(),
        QuotesOnFile: false,
        ClaimInsurer: Corella,
        PolicyNumber: "CR-19033105",
        PolicyOnFile: new[]
        {
            "Insurer: Corella Life — policy CR-19033105 (outside super), advised by Amelia in March 2019",
            "Policy owner and life insured: Mei Chen",
            "Life cover $1,000,000",
            "Nominated beneficiary: Robert Chen, 100%",
        },
        Handover: "Ben's Claims Agent checks the policy, raises the approval and lodges the claim with Corella Life; Cara's agent keeps every message to the family gentle.",
        HandoverAgentId: "claims",
        CaseStatus: "Completed — $1,000,000 paid to Robert on 23 Nov 2026",
        NextSteps: new[]
        {
            "Share the wording with Ben — every death claim needs approval before it's lodged.",
            "Ben's agent gives Robert the checklist for the separate $180,000 Riverbend Super claim.",
            "When Corella accepts a lump-sum claim of $25,000 or more, the Life Insurance Code of Practice has it suggest independent financial advice — Amelia is there if Robert wants help.",
        },
        HumanApproval: "Yes — every death claim needs approval before it's lodged, and every message to a bereaved family is approved first.",
        RuleDocuments: new[]
        {
            ResearchMcpTools.ClaimsRulesTitle,
            ResearchMcpTools.UnderwritingRulesTitle,
            ResearchMcpTools.TaxTitle,
        });

    // ── Live cases that don't match a scenario ───────────────────────────

    private static ResearchBrief Generic(CaseRecord record)
    {
        var isBusiness = record.RequestType == RequestTypes.BusinessProtectionAdvice;
        var isAdvice = isBusiness || record.RequestType == RequestTypes.NewProtectionAdvice;
        if (!isAdvice)
        {
            return new ResearchBrief(
                Scenario: "claim",
                Kind: BriefKinds.Claim,
                CaseNumber: record.CaseNumber,
                Client: record.ClientName,
                ClientFirstNames: FirstName(record.ClientName),
                Title: "Policy wording lookup",
                Summary: "This is a claim, so there's nothing to compare. The claim's insurer and policy number aren't on the case yet — " +
                         "Ben's Claims Agent checks the policy schedule, and Ivy can look up any APL insurer's wording on request.",
                KeyFacts: new[] { record.RequestSummary },
                CoverRequested: Array.Empty<string>(),
                MonthlyBudget: null,
                Items: Array.Empty<BriefItem>(),
                Observations: Array.Empty<string>(),
                QuotesOnFile: false,
                ClaimInsurer: null,
                PolicyNumber: null,
                PolicyOnFile: string.IsNullOrWhiteSpace(record.Cover) ? Array.Empty<string>() : new[] { record.Cover },
                Handover: "Ben's Claims Agent owns the claim — it checks the policy, builds the claim pack and lodges the claim after approval.",
                HandoverAgentId: "claims",
                CaseStatus: null,
                NextSteps: new[] { "Ask Ben's agent for the policy schedule, then look up the insurer's wording here." },
                HumanApproval: "Yes — lodging any claim needs approval.",
                RuleDocuments: new[] { ResearchMcpTools.ClaimsRulesTitle });
        }

        var items = isBusiness
            ? new[]
            {
                new BriefItem("business-buy-sell", "Buy-sell cover", BriefPriorities.MustHave, "Funds the buy-sell agreement if an owner dies or is totally and permanently disabled."),
                new BriefItem("business-key-person", "Key person cover", BriefPriorities.MustHave, "Protects the business's profit if a key person dies or is totally and permanently disabled."),
                new BriefItem("financial-underwriting", "Financial underwriting", BriefPriorities.Important, "Large business covers need financial evidence — simpler requirements get cover in place sooner."),
                new BriefItem("tpd-business-cover", "TPD definition on the business cover", BriefPriorities.Important, "Whether TPD is own or any occupation decides when it pays, and the buy-sell agreement should match it."),
                new BriefItem("exclusion-review", "Loading and exclusion review", BriefPriorities.Helpful, "Matters if underwriting adds a loading or an exclusion."),
            }
            : new[]
            {
                new BriefItem("tpd-own-occupation", "TPD — own occupation", BriefPriorities.MustHave, "Whether the client's occupation can get own-occupation TPD decides how likely a TPD claim is to be paid."),
                new BriefItem("ip-benefit-basis", "Income protection — how the benefit is worked out", BriefPriorities.MustHave, "Sets how much income protection can actually pay."),
                new BriefItem("ip-periods", "Income protection — waiting and benefit periods", BriefPriorities.MustHave, "Sets when payments start and how long they last."),
                new BriefItem("tpd-super-linking", "Super-linking", BriefPriorities.Important, "Lets part of the premium be paid from super."),
                new BriefItem("exclusion-review", "Exclusion review", BriefPriorities.Important, "Matters if underwriting adds an exclusion."),
                new BriefItem("trauma-cancer", "Trauma — cancer definition", BriefPriorities.Helpful, "How each insurer defines cancer and early-stage cancers."),
            };

        return new ResearchBrief(
            Scenario: isBusiness ? "business-generic" : "family-generic",
            Kind: BriefKinds.Advice,
            CaseNumber: record.CaseNumber,
            Client: record.ClientName,
            ClientFirstNames: FirstName(record.ClientName),
            Title: isBusiness ? "Business protection — compare the APL definitions" : "Personal protection — compare the APL definitions",
            Summary: "No insurer quotes are on file yet — premiums follow Penny's needs analysis (sums insured, structure and waiting periods). " +
                     "Ivy can compare the APL insurers' definitions now and add premiums once the quotes are in.",
            KeyFacts: new[] { record.RequestSummary },
            CoverRequested: string.IsNullOrWhiteSpace(record.Cover) ? Array.Empty<string>() : new[] { record.Cover },
            MonthlyBudget: null,
            Items: items,
            Observations: Array.Empty<string>(),
            QuotesOnFile: false,
            ClaimInsurer: null,
            PolicyNumber: null,
            PolicyOnFile: Array.Empty<string>(),
            Handover: "Penny's Paraplanning Agent runs the needs analysis first; Ivy adds the premiums once the quotes are in.",
            HandoverAgentId: "paraplanning",
            CaseStatus: null,
            NextSteps: new[]
            {
                "Ask Penny's agent for the needs analysis (sums insured, structure, waiting and benefit periods).",
                "Request quotes from the APL insurers, then re-run the comparison.",
            },
            HumanApproval: "Yes — Amelia gives the advice and signs the Statement of Advice.",
            RuleDocuments: new[] { ResearchMcpTools.AplResearchTitle, ResearchMcpTools.AdviceRulesTitle });
    }

    private static ResearchBrief Unknown(string caseNumber) => new(
        Scenario: "unknown",
        Kind: BriefKinds.Unknown,
        CaseNumber: caseNumber,
        Client: "Unknown",
        ClientFirstNames: "the client",
        Title: "Case not found",
        Summary: $"Case '{caseNumber}' isn't in the case store — pick one of the five scenario cases (REQ-20261103-LIF001 to REQ-20261113-LIF005) or open a case with the Onboarding Agent.",
        KeyFacts: Array.Empty<string>(),
        CoverRequested: Array.Empty<string>(),
        MonthlyBudget: null,
        Items: Array.Empty<BriefItem>(),
        Observations: Array.Empty<string>(),
        QuotesOnFile: false,
        ClaimInsurer: null,
        PolicyNumber: null,
        PolicyOnFile: Array.Empty<string>(),
        Handover: null,
        HandoverAgentId: null,
        CaseStatus: null,
        NextSteps: Array.Empty<string>(),
        HumanApproval: "Not applicable.",
        RuleDocuments: Array.Empty<string>());

    private static string FirstName(string name)
    {
        var first = (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "the client" : first;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Product sheet shapes (wwwroot/research/samples/*.json)
// ─────────────────────────────────────────────────────────────────────────

internal sealed class ProductSheet
{
    public string DocumentType { get; set; } = string.Empty;
    public string Insurer { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string PolicyPrefix { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? SuperPlan { get; set; }
    public string PdsTitle { get; set; } = string.Empty;
    public string? AsAt { get; set; }
    public string AplListing { get; set; } = string.Empty;
    public string? FictionNote { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> WatchPoints { get; set; } = new();
    public PremiumStyles? PremiumStyles { get; set; }
    public List<CoverType> CoverTypes { get; set; } = new();
    public List<PolicyDefinition> Definitions { get; set; } = new();
    public List<ProductFeature> Features { get; set; } = new();
    public List<ProductExclusion> Exclusions { get; set; } = new();
    public List<InsurerQuote> Quotes { get; set; } = new();
    public List<NotQuotedEntry> NotQuoted { get; set; } = new();

    /// <summary>App path or URL the sheet was read from.</summary>
    public string Source { get; set; } = string.Empty;
}

internal sealed class PremiumStyles
{
    public string? Stepped { get; set; }
    public string? Level { get; set; }
    public string? Indexation { get; set; }
}

internal sealed class CoverType
{
    public string Cover { get; set; } = string.Empty;
    public string? Key { get; set; }
    public bool InsideSuper { get; set; }
    public bool OutsideSuper { get; set; }
    public string? EntryAges { get; set; }
    public string? ExpiryAge { get; set; }
    public string? Notes { get; set; }
}

internal sealed class PolicyDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Wording { get; set; }
    public string? PlainEnglish { get; set; }
}

internal sealed class ProductFeature
{
    public string? Key { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

internal sealed class ProductExclusion
{
    public string Cover { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

internal sealed class InsurerQuote
{
    public string Scenario { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string? QuoteReference { get; set; }
    public string? QuotedOn { get; set; }
    public string? LivesInsured { get; set; }
    public string? RateBasis { get; set; }
    public string PremiumStyle { get; set; } = "Stepped";
    public decimal Year1Premium { get; set; }
    public decimal? InsideSuperYear1 { get; set; }
    public decimal? OutsideSuperYear1 { get; set; }
    public string? Structure { get; set; }
    public List<QuoteCover> Covers { get; set; } = new();
    public LevelOption? LevelOption { get; set; }
    public List<TermQuoted> TermsQuoted { get; set; } = new();
    public QuoteProjection? Projection { get; set; }
}

internal sealed class QuoteCover
{
    public string Cover { get; set; } = string.Empty;
    public string? SumInsured { get; set; }
    public string? Ownership { get; set; }
    public decimal Year1Premium { get; set; }
    public decimal? LevelYear1Premium { get; set; }
}

internal sealed class LevelOption
{
    public decimal Year1Premium { get; set; }
    public decimal? InsideSuperYear1 { get; set; }
    public decimal? OutsideSuperYear1 { get; set; }
    public string? Note { get; set; }
}

internal sealed class TermQuoted
{
    public string Key { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string? Note { get; set; }
}

internal sealed class QuoteProjection
{
    public string? Label { get; set; }
    public string? AgesLabel { get; set; }
    public List<ProjectionYearInput> Years { get; set; } = new();
}

internal sealed class ProjectionYearInput
{
    public int Year { get; set; }
    public string? Ages { get; set; }
    public decimal Stepped { get; set; }
    public decimal Level { get; set; }
}

internal sealed class NotQuotedEntry
{
    public string Scenario { get; set; } = string.Empty;
    public string? CaseNumber { get; set; }
    public string? Reason { get; set; }
}

internal sealed record LoadedDocument(ProductSheet? Sheet, string? Text, string Source);

// ─────────────────────────────────────────────────────────────────────────
// Research shapes (serialised camelCase in tool output and page JSON)
// ─────────────────────────────────────────────────────────────────────────

/// <summary>One definition that matters for the client, or one piece of wording that applies to a claim.</summary>
internal sealed record BriefItem(
    string Key,
    string Title,
    string Priority,
    string WhyItMatters,
    string? Wording = null,
    string? Source = null);

/// <summary>What Ivy researches for one Client Case.</summary>
internal sealed record ResearchBrief(
    string Scenario,
    string Kind,
    string CaseNumber,
    string Client,
    string ClientFirstNames,
    string Title,
    string Summary,
    IReadOnlyList<string> KeyFacts,
    IReadOnlyList<string> CoverRequested,
    decimal? MonthlyBudget,
    IReadOnlyList<BriefItem> Items,
    IReadOnlyList<string> Observations,
    bool QuotesOnFile,
    string? ClaimInsurer,
    string? PolicyNumber,
    IReadOnlyList<string> PolicyOnFile,
    string? Handover,
    string? HandoverAgentId,
    string? CaseStatus,
    IReadOnlyList<string> NextSteps,
    string HumanApproval,
    IReadOnlyList<string> RuleDocuments);

internal sealed record ItemResult(BriefItem Item, string Result, string Note);

internal sealed record RowDraft(
    ProductSheet Sheet,
    InsurerQuote? Quote,
    string? NotQuotedReason,
    int MustGaps,
    int MustPartly,
    int ImportantShort,
    IReadOnlyList<ItemResult> Results);

internal sealed record ComparisonRow(
    int Rank,
    string Insurer,
    string ShortName,
    string Code,
    bool Quoted,
    bool Recommended,
    string? NotQuotedReason,
    string? QuoteReference,
    string PdsTitle,
    string SheetUrl,
    string? PremiumStyle,
    decimal? Year1Stepped,
    decimal? MonthlyStepped,
    decimal? InsideSuperYear1,
    decimal? OutsideSuperYear1,
    decimal? CashFlowMonthly,
    decimal? Year1Level,
    decimal? LevelOutsideSuperYear1,
    decimal? LevelCashFlowMonthly,
    decimal? TenYearStepped,
    decimal? TenYearLevel,
    int MustHaveGaps,
    string DefinitionSummary,
    string? Structure,
    IReadOnlyList<QuoteCover> Covers,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> WatchPoints);

internal sealed record DefinitionCell(string Insurer, string Result, string Label, string Note, string? Wording, string PdsTitle);

internal sealed record DefinitionRow(string Key, string Title, string Priority, string WhyItMatters, IReadOnlyList<DefinitionCell> Insurers);

internal sealed record ProjectionYear(int Year, string Ages, decimal Stepped, decimal Level, decimal CumulativeStepped, decimal CumulativeLevel);

internal sealed record InsurerProjection(
    string Insurer,
    bool Recommended,
    string Label,
    string AgesLabel,
    IReadOnlyList<ProjectionYear> Years,
    decimal Year1Difference,
    int? SteppedAboveLevelFromYear,
    int? LevelCheaperInTotalFromYear,
    decimal TotalStepped,
    decimal TotalLevel,
    string? LevelNote,
    string Summary);

internal sealed record Recommendation(
    string? Insurer,
    string? PremiumStyle,
    string Headline,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> NotChosen,
    string SteppedVsLevel,
    string? BudgetCheck,
    string ForAdviser,
    string Disclaimer);

internal sealed record InsurerComparison(
    string CaseNumber,
    string Client,
    string Scenario,
    string Title,
    string Summary,
    IReadOnlyList<string> CoverRequested,
    IReadOnlyList<string> KeyFacts,
    decimal? MonthlyBudget,
    bool QuotesOnFile,
    IReadOnlyList<ComparisonRow> Rows,
    IReadOnlyList<DefinitionRow> Definitions,
    IReadOnlyList<InsurerProjection> Projections,
    Recommendation Recommendation,
    string CommissionNote,
    IReadOnlyList<string> Observations,
    string ComparisonMarkdown,
    string DefinitionsMarkdown,
    string ProjectionMarkdown,
    string ChartMermaid,
    string ProjectionMermaid,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> NextSteps,
    string HumanApproval);

internal sealed record WordingItem(
    string Key,
    string Heading,
    string Cover,
    string Wording,
    string? PlainEnglish,
    string WhatItMeans,
    string Source);

internal sealed record WordingLookup(
    string CaseNumber,
    string Client,
    string Scenario,
    string Title,
    string Summary,
    string? Insurer,
    string? PolicyNumber,
    string? PdsTitle,
    string? SheetUrl,
    IReadOnlyList<string> KeyFacts,
    IReadOnlyList<string> PolicyOnFile,
    IReadOnlyList<WordingItem> Items,
    string? Handover,
    string? HandoverAgentId,
    string? HandoverUrl,
    string? CaseStatus,
    IReadOnlyList<string> NextSteps,
    string HumanApproval,
    string Markdown,
    IReadOnlyList<string> Citations);

internal sealed record FitNote(
    string Key,
    string Title,
    string Priority,
    string WhyItMatters,
    string Result,
    string Label,
    string Note,
    string? Wording);

internal sealed record CaseFit(
    string CaseNumber,
    string Client,
    string Kind,
    InsurerQuote? Quote,
    string? NotQuotedReason,
    IReadOnlyList<FitNote> FitNotes,
    IReadOnlyList<string> PolicyOnFile,
    IReadOnlyList<WordingItem> ClaimWording,
    string? Handover);

internal sealed record PolicyDocumentAnalysis(
    string Insurer,
    string ProductName,
    string PdsTitle,
    string Source,
    string AplListing,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> WatchPoints,
    PremiumStyles? PremiumStyles,
    IReadOnlyList<CoverType> CoverTypes,
    IReadOnlyList<PolicyDefinition> Definitions,
    IReadOnlyList<ProductFeature> Features,
    IReadOnlyList<ProductExclusion> Exclusions,
    CaseFit? ForCase,
    string Cite);

internal sealed record ComparisonWorkbookResult(
    string CaseNumber,
    string? FileName,
    string? DownloadUrl,
    IReadOnlyList<string> Sheets,
    IReadOnlyList<string> Charts,
    string? RecommendedInsurer,
    string? Error)
{
    public static ComparisonWorkbookResult Failed(string caseNumber, string error) =>
        new(caseNumber, null, null, Array.Empty<string>(), Array.Empty<string>(), null, error);
}

internal enum ChartKind { Column, Line }

internal sealed record ChartSeriesSpec(
    string Name,
    string NameRef,
    string ValuesRef,
    IReadOnlyList<decimal> Values,
    string ColourHex,
    bool Dashed);

internal sealed record ChartSpec(
    string SheetName,
    ChartKind Kind,
    string Title,
    string CategoriesRef,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ChartSeriesSpec> Series,
    string ValueAxisTitle,
    int FromCol,
    int FromRow,
    int ToCol,
    int ToRow);
