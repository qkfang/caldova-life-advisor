using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using LifeAdviceGuru.App.Services;

namespace LifeAdviceGuru.App.Mcp;

/// <summary>
/// MCP tools that back the Underwriting Agent (Uma, Underwriting
/// Coordinator): the APL insurer directory (<c>underwriting_lookupInsurer</c>),
/// the evidence each insurer needs for the life insured's age and sums
/// insured (<c>underwriting_checkRequirements</c>) and the application and
/// disclosure summary PDF (<c>underwriting_generateApplicationSummaryPdf</c>,
/// downloaded from <c>/underwriting/application/{id}.pdf</c>). Served under
/// <c>/mcp</c> so the Foundry agent can call them over the MCP transport.
/// The static <c>*Result</c> builders are shared with <c>UnderwritingApi</c>
/// so the offline path reports exactly what the tools return.
/// </summary>
[McpServerToolType]
public class UnderwritingMcpTools
{
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ApplicationPdfService _pdfService;
    private readonly CaseStore _caseStore;
    private readonly IConfiguration _configuration;

    public UnderwritingMcpTools(ApplicationPdfService pdfService, CaseStore caseStore, IConfiguration configuration)
    {
        _pdfService = pdfService;
        _caseStore = caseStore;
        _configuration = configuration;
    }

    [McpServerTool(Name = "underwriting_lookupInsurer"),
     Description("Look up an insurer on Life Advice Guru's approved product list (APL): Harbourline Life (HL-), Corella Life (CR-), Lyrebird Life (LB-) and Northbank Life (NB-). Returns each match's profile and strengths, new business, underwriting and claims contacts, service levels, how it underwrites, its evidence thresholds by age and sum insured, its cooling-off offer and notes. Call it first to identify the insurer for the application.")]
    public string LookupInsurer(
        [Description("Insurer name, policy prefix (HL, CR, LB, NB), a policy number such as 'CR-26118204', or a keyword such as 'business protection', 'income protection' or 'trauma'. Use 'all' to list the whole APL.")]
        string query) => Serialize(LookupInsurerResult(query));

    internal static object LookupInsurerResult(string? query)
    {
        var insurers = InsurerDirectory.Lookup(query);
        return new
        {
            query = query ?? string.Empty,
            count = insurers.Count,
            insurers = insurers.Select(InsurerDirectory.ToView),
            note = insurers.Count == 0 ? "No insurer on Life Advice Guru's APL matches — only the four APL insurers can be recommended." : null
        };
    }

    [McpServerTool(Name = "underwriting_checkRequirements"),
     Description("Return the underwriting evidence an APL insurer needs for ONE life insured, given their age and the cover applied for on their life: personal statement (tele-interview), blood profile, medical examination, proof of income or business financial evidence — each with the threshold that triggers it, who arranges it and the typical turnaround. Count every policy on the same life (for example buy-sell plus key person) in the life and TPD amounts. Call it once per life insured.")]
    public string CheckRequirements(
        [Description("Insurer name or policy prefix, e.g. 'Corella Life' or 'NB'.")] string insurer,
        [Description("Life insured's age last birthday.")] int age,
        [Description("Total life cover on this life, in dollars (e.g. 750000).")] decimal lifeSumInsured,
        [Description("Total TPD cover on this life, in dollars (0 if none).")] decimal tpdSumInsured = 0,
        [Description("Trauma cover on this life, in dollars (0 if none).")] decimal traumaSumInsured = 0,
        [Description("Income protection monthly benefit, in dollars (0 if none).")] decimal incomeProtectionMonthly = 0,
        [Description("Business cover on this life: 'buy-sell', 'key person', 'buy-sell and key person', or empty for none.")] string? businessCover = null,
        [Description("True if the life insured is self-employed and applying for income protection.")] bool selfEmployed = false,
        [Description("Optional name of the life insured, echoed in the result.")] string? lifeInsured = null) =>
        Serialize(CheckRequirementsResult(insurer, age, lifeSumInsured, tpdSumInsured, traumaSumInsured,
            incomeProtectionMonthly, businessCover, selfEmployed, lifeInsured));

    internal static object CheckRequirementsResult(
        string? insurer, int age, decimal life, decimal tpd, decimal trauma, decimal ipMonthly,
        string? businessCover, bool selfEmployed, string? lifeInsured)
    {
        var entry = InsurerDirectory.Find(insurer);
        if (entry is null)
            return new { error = $"'{insurer}' isn't on Life Advice Guru's approved product list. Use Harbourline Life, Corella Life, Lyrebird Life or Northbank Life." };
        if (age is < 18 or > 70)
            return new { error = $"Age {age} is outside the 18–70 range the APL insurers accept for new cover." };
        return InsurerDirectory.CheckRequirements(entry, age, life, tpd, trauma, ipMonthly, businessCover, selfEmployed, lifeInsured);
    }

    [McpServerTool(Name = "underwriting_generateApplicationSummaryPdf"),
     Description("Generate the Life Advice Guru 'Application and disclosure summary' PDF for a Client Case: the client, the insurer, every cover with its sum insured, ownership, premium and terms, a summary of the disclosures (without health detail), the underwriting requirements and their status, and the duty-to-take-reasonable-care acknowledgement. Built from the case's application on file; optional arguments override the status line and add notes. Returns JSON with a downloadUrl to include in your reply.")]
    public string GenerateApplicationSummaryPdf(
        [Description("Client Case number, e.g. REQ-20261103-LIF001.")] string caseNumber,
        [Description("Optional status line for the banner, e.g. 'LODGED — AWAITING TELE-INTERVIEW' or 'ACCEPTED — POLICY CR-26118204 IN FORCE FROM WED 18 NOV 2026'. Taken from the application on file when omitted.")]
        string? applicationStatus = null,
        [Description("Optional notes to print on the summary, one per line (no health detail).")] string? notes = null)
    {
        var record = _caseStore.Get(caseNumber ?? string.Empty);
        if (record is null)
            return Serialize(new { error = $"Client Case '{caseNumber}' not found." });

        var plan = InsurerProductCatalog.Build(record);
        if (!plan.Applicable)
            return Serialize(new { error = "There's no application on this case — it isn't an underwriting case.", note = plan.NotApplicableNote });

        var input = InsurerProductCatalog.ToPdfInput(record, plan);
        if (!string.IsNullOrWhiteSpace(applicationStatus))
            input = input with { ApplicationStatus = applicationStatus.Trim() };
        var extra = SplitLines(notes);
        if (extra.Count > 0)
            input = input with { Notes = input.Notes.Concat(extra).ToList() };

        var pdf = _pdfService.Generate(input);
        return Serialize(DownloadPayload(pdf, BaseUrl(_configuration)));
    }

    /// <summary>JSON payload describing a generated PDF (shared with the /underwriting/process flow).</summary>
    internal static object DownloadPayload(ApplicationPdfRecord pdf, string baseUrl)
    {
        var relative = RelativeUrl(pdf);
        return new
        {
            id = pdf.Id,
            fileName = pdf.FileName,
            byteLength = pdf.Content.Length,
            createdAt = pdf.CreatedAt,
            downloadUrl = $"{baseUrl}{relative}",
            relativeUrl = relative
        };
    }

    internal static string RelativeUrl(ApplicationPdfRecord pdf) => $"/underwriting/application/{pdf.Id}.pdf";

    internal static string BaseUrl(IConfiguration configuration)
    {
        var configured = new[] { configuration["APP_BASE_URL"], configuration["APP_MCP_URL"] }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return (configured ?? "http://localhost:5212").Trim().TrimEnd('/');
    }

    internal static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOpts);

    private static IReadOnlyList<string> SplitLines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(l => l.TrimStart('-', '•', '*', ' '))
                .Where(l => l.Length > 0)
                .ToList();

    internal static string Amount(decimal v) => v.ToString("0", CultureInfo.InvariantCulture);
}
