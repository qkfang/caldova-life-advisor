using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LifeAdviceGuru.App.Models;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// One specimen document available to the Financial Crime "Try it out" tab.
/// Mirrors an entry in <c>wwwroot/fincrime/samples/manifest.json</c> (generated
/// by <c>tools/generate_fincrime_samples.py</c>). The optional fields are the
/// case-file record of the document, named like the Azure Content
/// Understanding fields, so the same checks run offline.
/// </summary>
/// <param name="Expected"><c>legit</c>, <c>suspicious</c>, <c>tampered</c> or <c>fake</c>.</param>
/// <param name="Owner">Scenario key of the client the document belongs to (<c>priya</c>, <c>grace</c>, …).</param>
/// <param name="KeyAmount">Headline amount: annual salary (payslip), net business income (BAS / P&amp;L) or closing balance (bank statement).</param>
public record FinCrimeSampleDocument(
    string Id,
    string Label,
    string Kind,
    string Src,
    string Expected,
    string Description,
    string? Owner = null,
    string? HolderName = null,
    string? DocumentNumber = null,
    string? Issuer = null,
    string? IssueDate = null,
    string? ExpiryDate = null,
    string? DateOfBirth = null,
    string? RegistrationDate = null,
    string? DateOfDeath = null,
    string? AccountOpenedDate = null,
    string? ConsultationDate = null,
    string? PeriodStart = null,
    string? PeriodEnd = null,
    decimal? KeyAmount = null,
    string? KeyAmountLabel = null,
    string? ProviderNumber = null,
    string? DiagnosisEvidence = null,
    string? Officers = null,
    string? CompanyStatus = null,
    string? Mrz = null,
    string? Certification = null,
    string? SecurityFeatures = null,
    string? TamperIndicators = null,
    string? ElectronicVerification = null);

/// <summary>A document attached to a Client Case (points at a manifest entry).</summary>
public record FinCrimeCaseDocumentRef(string Id, string Label, string Kind, string Src);

/// <summary>
/// A person or company connected to a case — the client, the life insured,
/// the policy owner, a beneficiary, a support person or a company and its
/// beneficial owners.
/// </summary>
public record FinCrimeCaseParty(
    string Name,
    string Role,
    string? DateOfBirth = null,
    IReadOnlyList<string>? Aliases = null,
    bool IsCompany = false);

/// <summary>
/// One entry on the client activity timeline. <c>Type</c> drives the risk
/// rules (<c>travel-notice</c>, <c>contact-change</c>, <c>new-device</c>,
/// <c>bank-account</c>, <c>claim</c>, <c>request</c>, <c>referral</c>,
/// <c>kyc</c>, <c>check</c>, <c>policy</c>, <c>info</c>); <c>Flag</c> is
/// <c>red</c>, <c>amber</c>, <c>ok</c> or <c>info</c>.
/// </summary>
public record FinCrimeActivityEvent(
    DateTime At,
    bool HasTime,
    string Type,
    string Channel,
    string Summary,
    string Detail,
    string Flag,
    string? Location = null,
    string? WhenText = null)
{
    /// <summary>Display timestamp, e.g. "Mon 02 Nov 2026, 10:41".</summary>
    public string When => WhenText ?? (HasTime
        ? At.ToString("ddd dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
        : At.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture));
}

/// <summary>
/// One screening or verification result held on the case (identity, PEP and
/// sanctions, company and beneficial owners, contact and bank details).
/// <c>Status</c> is <c>pass</c>, <c>fail</c>, <c>warn</c> or <c>info</c>.
/// </summary>
public record FinCrimeScreeningResult(string Group, string Check, string Result, string Status);

/// <summary>A travel notice the client gave Life Advice Guru (e.g. at an annual review).</summary>
public record FinCrimeTravelNotice(DateTime From, DateTime To, string Place, string RecordedAt);

/// <summary>How the scenario ends once people have made their decisions.</summary>
/// <param name="Status"><c>Cleared</c> or <c>Blocked</c>.</param>
public record FinCrimeScenarioOutcome(string Status, string Icon, string Headline, string Detail);

/// <summary>
/// Financial-crime view of a Client Case: who is on it, who a benefit may be
/// paid to, the client activity timeline, the screening results, the evidence
/// rules the documents are checked against and the knowledge documents to
/// cite. Scenario cases are matched by sample id or by their seeded case number;
/// cases opened live for an unknown sample get a generic profile.
/// </summary>
/// <param name="ScenarioKey"><c>priya</c>, <c>grace</c>, <c>george</c>, <c>tom</c>, <c>robert</c>, or empty for a live case.</param>
/// <param name="PaymentRequested">True when a claim, benefit or other money-out request is on the case.</param>
/// <param name="AcceptedPayees">Names a benefit may be paid to (the policy owner or the nominated beneficiary).</param>
/// <param name="RequestedPayees">Account names on the payment account the request asks Life Advice Guru to use.</param>
/// <param name="IncomeOnFile">Income the earnings evidence should support (fact find or claim).</param>
/// <param name="EvidenceRule">The policy's evidence rule for a medical claim, with its source, when it matters.</param>
/// <param name="AmountAtRisk">The amount a block would protect, when money is being requested.</param>
public record FinCrimeCaseProfile(
    string ScenarioKey,
    bool IsScenario,
    string Focus,
    bool PaymentRequested,
    IReadOnlyList<FinCrimeCaseParty> Parties,
    IReadOnlyList<string> AcceptedPayees,
    IReadOnlyList<string> RequestedPayees,
    string VerifiedContact,
    FinCrimeTravelNotice? TravelNotice,
    decimal? IncomeOnFile,
    string? EvidenceRule,
    decimal? AmountAtRisk,
    IReadOnlyList<FinCrimeActivityEvent> Timeline,
    IReadOnlyList<FinCrimeScreeningResult> Screening,
    IReadOnlyList<string> References,
    string NextStep,
    FinCrimeScenarioOutcome? Outcome);

/// <summary>
/// Loads the specimen document manifest shipped under
/// <c>wwwroot/fincrime/samples/manifest.json</c>, attaches each scenario's
/// documents to its Client Case and holds the financial-crime case profiles
/// used by the Financial Crime "Try it out" tab.
/// </summary>
public class FinCrimeCaseDocumentStore
{
    /// <summary>Scenario keys in canonical order (scenario 1 … 5).</summary>
    public static readonly IReadOnlyList<string> ScenarioKeys = ["priya", "grace", "george", "tom", "robert"];

    /// <summary>Seeded case number of each scenario (spec §4).</summary>
    public static readonly IReadOnlyDictionary<string, string> ScenarioCaseNumbers =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["priya"] = "REQ-20261103-LIF001",
            ["grace"] = "REQ-20261106-LIF002",
            ["george"] = "REQ-20261109-LIF003",
            ["tom"] = "REQ-20261111-LIF004",
            ["robert"] = "REQ-20261113-LIF005",
        };

    private static readonly IReadOnlyDictionary<string, string> ClientNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["priya"] = "Priya Natarajan",
            ["grace"] = "Grace Williams",
            ["george"] = "George Papadopoulos and Harpreet Kaur",
            ["tom"] = "Tom Bradley",
            ["robert"] = "Robert Chen",
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly string _webRoot;
    private readonly IReadOnlyDictionary<string, FinCrimeSampleDocument> _samples;
    private readonly IReadOnlyList<FinCrimeSampleDocument> _orderedSamples;
    private readonly ConcurrentDictionary<string, List<string>> _caseDocs =
        new(StringComparer.OrdinalIgnoreCase);

    public FinCrimeCaseDocumentStore(IWebHostEnvironment env, ILogger<FinCrimeCaseDocumentStore> logger)
    {
        _webRoot = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _orderedSamples = LoadManifest(_webRoot, logger);
        _samples = _orderedSamples.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>All specimen documents declared in the manifest, in manifest order.</summary>
    public IReadOnlyList<FinCrimeSampleDocument> AllSamples() => _orderedSamples;

    public FinCrimeSampleDocument? FindSample(string id) =>
        !string.IsNullOrWhiteSpace(id) && _samples.TryGetValue(id, out var s) ? s : null;

    /// <summary>
    /// Resolve sample ids to manifest entries, preserving the requested order
    /// and skipping unknown or duplicate ids.
    /// </summary>
    public IReadOnlyList<FinCrimeSampleDocument> ResolveSamples(IEnumerable<string>? ids)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<FinCrimeSampleDocument>();
        foreach (var id in ids ?? Enumerable.Empty<string>())
        {
            var sample = FindSample(id);
            if (sample is not null && seen.Add(sample.Id)) resolved.Add(sample);
        }
        return resolved;
    }

    /// <summary>
    /// Scenario key for a case: from the Onboarding sample id
    /// (<c>onboarding-grace</c>) or, for the seeded cases, from the case number
    /// tag (<c>…-LIF002</c> is scenario 2). Empty for any other live case.
    /// </summary>
    public static string ScenarioKeyFor(CaseRecord record)
    {
        var sampleId = record.SampleId ?? string.Empty;
        foreach (var key in ScenarioKeys)
        {
            if (sampleId.Equals($"onboarding-{key}", StringComparison.OrdinalIgnoreCase)) return key;
        }

        var match = Regex.Match(record.CaseNumber ?? string.Empty, @"-LIF(\d{3})$", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n >= 1 && n <= ScenarioKeys.Count)
            return ScenarioKeys[n - 1];
        return string.Empty;
    }

    /// <summary>Client name(s) for a scenario key, for messages.</summary>
    public static string ClientNameFor(string scenarioKey) =>
        ClientNames.TryGetValue(scenarioKey ?? string.Empty, out var name) ? name : scenarioKey ?? string.Empty;

    /// <summary>Attach the scenario's documents to a case the first time it is seen.</summary>
    public void EnsureCaseAttachments(CaseRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.CaseNumber)) return;
        var key = ScenarioKeyFor(record);
        _caseDocs.GetOrAdd(record.CaseNumber, _ => key.Length == 0
            ? new List<string>()
            : _orderedSamples
                .Where(s => string.Equals(s.Owner, key, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Id)
                .ToList());
    }

    /// <summary>Documents attached to the given case (empty when none were seeded).</summary>
    public IReadOnlyList<FinCrimeCaseDocumentRef> DocumentsForCase(string caseNumber)
    {
        if (string.IsNullOrWhiteSpace(caseNumber) || !_caseDocs.TryGetValue(caseNumber, out var ids))
            return Array.Empty<FinCrimeCaseDocumentRef>();
        return ids
            .Select(FindSample)
            .Where(s => s is not null)
            .Select(s => new FinCrimeCaseDocumentRef(s!.Id, s.Label, s.Kind, s.Src))
            .ToList();
    }

    /// <summary>
    /// Physical path of a specimen image under <c>wwwroot</c>, or null when the
    /// src is not a file inside the web root.
    /// </summary>
    public string? ResolvePhysicalPath(FinCrimeSampleDocument sample)
    {
        if (string.IsNullOrWhiteSpace(sample.Src)) return null;
        var relative = sample.Src.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(_webRoot);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return File.Exists(full) ? full : null;
    }

    /// <summary>Financial-crime profile for a case (scenario data or a generic fallback).</summary>
    public FinCrimeCaseProfile ProfileFor(CaseRecord record) => ScenarioKeyFor(record) switch
    {
        "priya" => PriyaProfile(),
        "grace" => GraceProfile(),
        "george" => GeorgeProfile(),
        "tom" => TomProfile(),
        "robert" => RobertProfile(),
        _ => GenericProfile(record),
    };

    private static IReadOnlyList<FinCrimeSampleDocument> LoadManifest(string webRoot, ILogger logger)
    {
        var path = Path.Combine(webRoot, "fincrime", "samples", "manifest.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Financial crime sample manifest not found at {Path} — no specimen documents are available", path);
            return Array.Empty<FinCrimeSampleDocument>();
        }

        try
        {
            using var stream = File.OpenRead(path);
            var doc = JsonSerializer.Deserialize<ManifestRoot>(stream, JsonOptions);
            return (doc?.Samples ?? new List<FinCrimeSampleDocument>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the financial crime sample manifest from {Path}", path);
            return Array.Empty<FinCrimeSampleDocument>();
        }
    }

    private sealed record ManifestRoot(List<FinCrimeSampleDocument> Samples);

    // ── Scenario profiles (spec §4) ─────────────────────────────────────────

    private const string RedFlagsGuide = "Guide — financial crime red flags and controls for life insurance";
    private const string ApprovalsMatrix = "Matrix — authority, approvals and escalation";
    private const string InGoodOrderGuide = "Guide — in-good-order checklist for advice and claims";

    private static DateTime On(int year, int month, int day) => new(year, month, day);
    private static DateTime At(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0);

    private static FinCrimeCaseProfile PriyaProfile() => new(
        ScenarioKey: "priya",
        IsScenario: true,
        Focus: "New client — identity (KYC), income evidence, PEP and sanctions screening before the advice starts.",
        PaymentRequested: false,
        Parties: [new("Priya Natarajan", "Client and life insured (new client)", "1992-02-14")],
        AcceptedPayees: ["Priya Natarajan"],
        RequestedPayees: [],
        VerifiedContact: "0491 570 156 · priya.natarajan@example.com (from the referral, confirmed with Priya)",
        TravelNotice: null,
        IncomeOnFile: 105_000m,
        EvidenceRule: null,
        AmountAtRisk: null,
        Timeline:
        [
            new(On(2026, 11, 3), false, "request", "Email · referral partner",
                "Referral from Hannah Lewis, Yarra Home Loans",
                "Sent with Priya's consent after she bought a home in Coburg (joint mortgage $680,000). Priya asks for advice on protecting her family.",
                "info"),
            new(On(2026, 11, 3), false, "kyc", "Fact find · identity documents",
                "Driver licence and payslip supplied",
                "Uploaded by Priya for the fact find and the identity check.",
                "info"),
            new(On(2026, 11, 3), false, "kyc", "Document Verification Service (DVS)",
                "Driver licence verified electronically — match",
                "The licence details match the issuer's record.",
                "ok"),
            new(On(2026, 11, 3), false, "kyc", "PEP and sanctions screening",
                "No politically exposed person or sanctions match",
                "Screened before Life Advice Guru starts the advice.",
                "ok"),
            new(On(2026, 11, 3), false, "info", "Client history",
                "New client — no account history yet",
                "No contact-detail changes, devices, bank-detail changes or claims on record.",
                "info"),
        ],
        Screening:
        [
            new("Identity (KYC)", "Driver licence — Document Verification Service", "Match — 03/11/2026", "pass"),
            new("Identity (KYC)", "Income evidence — payslip", "Annual salary $105,000 + super, as in the fact find", "pass"),
            new("PEP and sanctions", "Politically exposed person (PEP) screening", "No match", "pass"),
            new("PEP and sanctions", "Sanctions screening (DFAT Consolidated List)", "No match", "pass"),
        ],
        References:
        [
            "Case REQ-20261103-LIF001 — referral and fact find",
            InGoodOrderGuide,
            RedFlagsGuide,
        ],
        NextStep: "Record KYC as complete on the Client Case and release it to Penny (Paraplanning) for the needs analysis.",
        Outcome: new("Cleared", "✅", "KYC complete — low risk",
            "Identity and income documents are genuine and there is no PEP or sanctions match. The case continues to Paraplanning."));

    private static FinCrimeCaseProfile GraceProfile() => new(
        ScenarioKey: "grace",
        IsScenario: true,
        Focus: "Trauma claim and a new bank account, four days after the contact details changed — check for identity takeover.",
        PaymentRequested: true,
        Parties:
        [
            new("Grace Williams", "Client, policy owner and life insured — Lyrebird Life LB-19004417", "1965-07-22"),
            new("Daniel Williams", "Nominated beneficiary (50%)"),
            new("James Williams", "Nominated beneficiary (50%)"),
        ],
        AcceptedPayees: ["Grace Williams"],
        RequestedPayees: ["G Williams Consulting Pty Ltd"],
        VerifiedContact: "0491 570 157 · grace.williams@example.com (verified before the 02/11/2026 change)",
        TravelNotice: new(On(2026, 10, 20), On(2026, 12, 1), "Italy", "her annual review with Amelia on 28/09/2026"),
        IncomeOnFile: null,
        EvidenceRule: "Lyrebird Life's trauma cover pays for cancer only on a specialist diagnosis confirmed by histopathology (Lyrebird Life — Protection PDS extract).",
        AmountAtRisk: 200_000m,
        Timeline:
        [
            new(On(2026, 9, 28), false, "travel-notice", "Annual review · Amelia",
                "Travel notice recorded — Italy, 20 Oct to 1 Dec 2026",
                "At her annual review Grace told Amelia she'd be travelling in Italy from 20 October to 1 December 2026.",
                "info", "Italy"),
            new(At(11, 2, 10, 41), true, "contact-change", "Phone · client services",
                "Mobile and email changed by phone — no code sent to the verified mobile",
                "A caller claiming to be Grace passed the basic checks (name, date of birth, address) and said \"I've lost my phone\". Mobile 0491 570 157 → 0491 570 159; email grace.williams@example.com → grace.w.claims@example.net. No one-time code was sent to the existing mobile.",
                "red"),
            new(At(11, 2, 10, 58), true, "new-device", "Client portal · login",
                "Portal login from a new device on a Sydney IP address",
                "17 minutes after the contact change. The travel notice says Grace is in Italy until 1 December.",
                "red", "Sydney, NSW"),
            new(On(2026, 11, 3), false, "bank-account", "Bank statement (sent with the claim)",
                "Account ending 4471 opened in the name 'G Williams Consulting Pty Ltd'",
                "Opened with a $10 cash deposit, the day after the contact change.",
                "amber"),
            new(At(11, 6, 7, 52), true, "claim", "Email · from the changed address",
                "Trauma claim for breast cancer — the full $200,000, marked \"urgent\"",
                "From grace.w.claims@example.net, about 93 hours after the contact change. Asks for the whole trauma benefit to be paid into the new account ending 4471 in the name 'G Williams Consulting Pty Ltd', and for Life Advice Guru to contact her only on the new mobile 0491 570 159 and the new email address. Attached: an oncologist's report, a driver licence and a bank statement.",
                "red"),
            new(On(2026, 11, 6), false, "referral", "Claims · Ben's policy check",
                "Claims referred the case to Financial Crime",
                "Cancer claims need a specialist diagnosis confirmed by histopathology, and benefits are paid to the policy owner — a new account in another name can't be used.",
                "amber"),
            new(On(2026, 11, 6), false, "policy", "Policy history",
                "No claims since 2019; premiums still paid from Grace's own account",
                "Lyrebird Life LB-19004417 (outside super): life cover $500,000 and trauma cover $200,000. Nominated beneficiaries Daniel 50%, James 50%.",
                "info"),
        ],
        Screening:
        [
            new("Identity (KYC)", "Identity on file", "Verified when Grace became a client in 2019", "pass"),
            new("Identity (KYC)", "PEP and sanctions screening — Grace Williams", "No match", "pass"),
            new("Contact and bank details", "Contact-detail change on 02/11/2026", "Not verified — no code sent to the verified mobile 0491 570 157", "fail"),
            new("Contact and bank details", "Payment account named on the claim", "'G Williams Consulting Pty Ltd' — not the policy owner, Grace Williams", "fail"),
            new("Client history", "Travel notice", "Italy, 20/10/2026 – 01/12/2026 (recorded 28/09/2026)", "info"),
            new("Client history", "Claims on the policy", "None since the policy started in 2019", "info"),
        ],
        References:
        [
            "Case REQ-20261106-LIF002 — claim email and attachments",
            "Case REQ-20261106-LIF002 — client activity log",
            "Case REQ-20261106-LIF002 — document checks",
            "Case REQ-20261106-LIF002 — travel notice",
            "Lyrebird Life — Protection PDS extract",
            RedFlagsGuide,
            ApprovalsMatrix,
        ],
        NextStep: "Theo decides on the hold and each control. If he approves, Cara's agent prepares the call to Grace on her verified mobile and Ben confirms nothing was lodged.",
        Outcome: new("Blocked", "🛡️", "Blocked — $200,000 protected",
            "After Theo's approval: no claim lodged with Lyrebird Life, Grace's verified contact details restored, portal access locked and Lyrebird Life's fraud team alerted. Called on her verified mobile, Grace — in Florence — confirmed she is well and never asked. Her $500,000 life cover and $200,000 trauma cover are safe."));

    private static FinCrimeCaseProfile GeorgeProfile() => new(
        ScenarioKey: "george",
        IsScenario: true,
        Focus: "Business protection — KYC on the company and both beneficial owners before the advice starts.",
        PaymentRequested: false,
        Parties:
        [
            new("George Papadopoulos", "Client — director, 50% shareholder and life insured", "1974-04-19"),
            new("Harpreet Kaur", "Client — director, secretary, 50% shareholder and life insured", "1979-09-08"),
            new("Northcote Custom Joinery Pty Ltd", "Company — proposed owner of the key person policy", IsCompany: true),
        ],
        AcceptedPayees: ["George Papadopoulos", "Harpreet Kaur", "Northcote Custom Joinery Pty Ltd"],
        RequestedPayees: [],
        VerifiedContact: "0491 570 158 · george@northcotejoinery.example.com.au (or via Marcus Tan, Bayside Business Accountants, with consent)",
        TravelNotice: null,
        IncomeOnFile: null,
        EvidenceRule: null,
        AmountAtRisk: null,
        Timeline:
        [
            new(On(2026, 11, 9), false, "request", "Email · referral partner",
                "Referral from Marcus Tan, Bayside Business Accountants",
                "George and Harpreet co-own Northcote Custom Joinery Pty Ltd 50/50 and have no succession plan. Business valued at $2,400,000 at 30 Jun 2026.",
                "info"),
            new(On(2026, 11, 9), false, "kyc", "Company register search",
                "Company extract — Northcote Custom Joinery Pty Ltd",
                "Registered since 14/03/2011. Directors George Papadopoulos and Harpreet Kaur; 100 ordinary shares held 50/50, beneficially.",
                "ok"),
            new(On(2026, 11, 10), false, "kyc", "Document Verification Service (DVS)",
                "Both beneficial owners identified and verified",
                "George: driver licence — match. Harpreet: passport — match.",
                "ok"),
            new(On(2026, 11, 10), false, "kyc", "PEP and sanctions screening",
                "Company, George and Harpreet — no match",
                "Screened before Life Advice Guru starts the advice.",
                "ok"),
        ],
        Screening:
        [
            new("Company and beneficial owners", "Company status", "Registered since 14/03/2011", "pass"),
            new("Company and beneficial owners", "Directors and shareholders", "George Papadopoulos 50%, Harpreet Kaur 50% — as in the referral", "pass"),
            new("Company and beneficial owners", "Beneficial owners", "Both identified and verified (50% each, beneficially held)", "pass"),
            new("Identity (KYC)", "George Papadopoulos — driver licence (DVS)", "Match", "pass"),
            new("Identity (KYC)", "Harpreet Kaur — passport (DVS)", "Match", "pass"),
            new("PEP and sanctions", "PEP screening — company and both owners", "No match", "pass"),
            new("PEP and sanctions", "Sanctions screening (DFAT Consolidated List)", "No match", "pass"),
        ],
        References:
        [
            "Case REQ-20261109-LIF003 — referral and business details",
            "Case REQ-20261109-LIF003 — valuation summary",
            RedFlagsGuide,
        ],
        NextStep: "Record the company KYC and both beneficial owners on the Client Case and release it to Penny (Paraplanning).",
        Outcome: new("Cleared", "✅", "KYC complete — company and both owners verified, low risk",
            "The company extract and both IDs are genuine, the beneficial owners are identified and there is no PEP or sanctions match. The case continues to Paraplanning."));

    private static FinCrimeCaseProfile TomProfile() => new(
        ScenarioKey: "tom",
        IsScenario: true,
        Focus: "Income protection claim — check the earnings evidence and the surgeon's report before Ben lodges the claim.",
        PaymentRequested: true,
        Parties:
        [
            new("Tom Bradley", "Client, policy owner and life insured — Harbourline Life HL-23051877", "1985-03-03",
                ["Thomas Bradley", "Thomas (Tom) Bradley", "Tom Bradley — Bradley Electrical", "Bradley Electrical"]),
            new("Kylie Bradley", "Support person — written authority from Tom"),
        ],
        AcceptedPayees: ["Tom Bradley", "Thomas Bradley"],
        RequestedPayees: [],
        VerifiedContact: "0491 570 110 · tom.bradley@example.com",
        TravelNotice: null,
        IncomeOnFile: 96_000m,
        EvidenceRule: null,
        AmountAtRisk: null,
        Timeline:
        [
            new(On(2023, 3, 15), false, "policy", "Policy start",
                "Harbourline Life HL-23051877 started",
                "Income protection: $7,000 a month insured, 30-day waiting period, benefit period to age 65. Identity verified at application.",
                "info"),
            new(On(2026, 10, 14), false, "info", "Injury",
                "Fell from a ladder at work — torn rotator cuff, right shoulder",
                "Tom is a self-employed electrician (Bradley Electrical).",
                "info"),
            new(On(2026, 10, 21), false, "info", "Surgery",
                "Arthroscopic rotator cuff repair",
                "Surgeon expects four to six months off work.",
                "info"),
            new(On(2026, 11, 11), false, "claim", "Phone · client",
                "Claim notification — Kylie joins as Tom's support person",
                "Kylie has Tom's written authority. Earnings evidence and the surgeon's report supplied.",
                "info"),
            new(On(2026, 11, 11), false, "check", "Client history",
                "No recent changes to contact or bank details, and no other claims",
                "The benefit will be paid to an account in Tom's own name, given on the claim form.",
                "ok"),
        ],
        Screening:
        [
            new("Identity (KYC)", "Identity on file", "Verified at application (March 2023)", "pass"),
            new("Identity (KYC)", "Support person authority", "Kylie Bradley — written authority from Tom on file", "pass"),
            new("Contact and bank details", "Contact and bank details", "No changes in the last 30 days", "pass"),
            new("Contact and bank details", "Payment account", "To be an account in Tom's own name, on the claim form", "info"),
            new("Claim evidence", "Earnings evidence — BAS and profit and loss", "Net business income $96,000, 12 months to 30/09/2026", "pass"),
            new("Claim evidence", "Medical evidence — treating surgeon's report", "MRI-confirmed injury and surgery on 21/10/2026", "pass"),
            new("Client history", "Claims on the policy", "None before this claim", "info"),
        ],
        References:
        [
            "Case REQ-20261111-LIF004 — claim notification",
            "Case REQ-20261111-LIF004 — earnings evidence",
            "Harbourline Life — Protection PDS extract",
            RedFlagsGuide,
        ],
        NextStep: "Record the document checks and release the claim to Ben (Claims). Tom is under financial stress, so the case stays flagged for Theo's vulnerable-client review.",
        Outcome: new("Cleared", "✅", "Documents genuine — low risk",
            "The BAS, the profit and loss and the surgeon's report are genuine, and nothing on the case has changed recently. The claim continues with Ben."));

    private static FinCrimeCaseProfile RobertProfile() => new(
        ScenarioKey: "robert",
        IsScenario: true,
        Focus: "Death claim — check the death certificate and the beneficiary's identity, without asking the family for more.",
        PaymentRequested: true,
        Parties:
        [
            new("Mei Chen", "Life insured and policy owner (deceased) — Corella Life CR-19033105", "1984-06-05"),
            new("Robert Chen", "Nominated beneficiary (100%) and claimant — Mei's husband", "1982-01-30"),
        ],
        AcceptedPayees: ["Robert Chen"],
        RequestedPayees: [],
        VerifiedContact: "Robert Chen · 0491 570 313 · robert.chen@example.com — Amelia is the family's single contact",
        TravelNotice: null,
        IncomeOnFile: null,
        EvidenceRule: null,
        AmountAtRisk: null,
        Timeline:
        [
            new(On(2019, 3, 1), false, "policy", "Policy start",
                "Corella Life CR-19033105 advised by Amelia",
                "Life cover $1,000,000; policy owner and life insured Mei Chen; nominated beneficiary Robert Chen (100%). Mei and Robert verified as clients in 2019.",
                "info", WhenText: "Mar 2019"),
            new(On(2026, 10, 12), false, "info", "Bereavement",
                "Mei Chen died suddenly, aged 42",
                "Brain aneurysm.",
                "info"),
            new(On(2026, 10, 19), false, "info", "Births, Deaths and Marriages",
                "Death registered",
                "Registration 2026/041857.",
                "info"),
            new(On(2026, 11, 13), false, "claim", "Email · client",
                "Death notification from Robert",
                "Robert emailed Amelia a certified copy of the death certificate and a certified copy of his driver licence.",
                "info"),
            new(On(2026, 11, 13), false, "check", "Client history",
                "No changes or requests on Mei's policy before or after her death",
                "No contact-detail, bank-detail or ownership changes, and no earlier claims.",
                "ok"),
        ],
        Screening:
        [
            new("Claim evidence", "Beneficiary nomination", "Robert Chen 100% — on file since 2019", "pass"),
            new("Claim evidence", "Death certificate", "Certified copy — registration 2026/041857", "pass"),
            new("Identity (KYC)", "Robert Chen — driver licence (certified copy, DVS)", "Match", "pass"),
            new("PEP and sanctions", "PEP and sanctions screening — Robert Chen", "No match", "pass"),
            new("Contact and bank details", "Payment account", "To be an account in Robert's own name, on the Corella Life claim form", "info"),
        ],
        References:
        [
            "Case REQ-20261113-LIF005 — death notification",
            "Case REQ-20261113-LIF005 — death certificate",
            "Case REQ-20261113-LIF005 — policy schedule and nomination",
            "Corella Life — Protection PDS extract",
            RedFlagsGuide,
        ],
        NextStep: "Record the checks and release the claim to Ben (Claims). Amelia stays the family's single contact, and every message to the family goes to Theo for approval.",
        Outcome: new("Cleared", "✅", "Documents genuine — low risk",
            "The death certificate and Robert's ID are genuine and Robert is the nominated beneficiary. Nothing more is asked of the family; the claim continues with Ben."));

    private static FinCrimeCaseProfile GenericProfile(CaseRecord r)
    {
        var received = DateTime.TryParseExact(r.ReceivedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d
            : r.CreatedAt.LocalDateTime.Date;
        var isClaim = r.RequestType is RequestTypes.TraumaClaimBankChange or RequestTypes.IncomeProtectionClaim or RequestTypes.DeathClaim;
        var contact = string.Join(" · ", new[] { r.ClientPhone, r.ClientEmail }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new FinCrimeCaseProfile(
            ScenarioKey: string.Empty,
            IsScenario: false,
            Focus: isClaim
                ? "Claim — check identity, the claim documents and any change of contact or bank details."
                : "New advice — identity (KYC), PEP and sanctions screening.",
            PaymentRequested: isClaim,
            Parties: [new(r.ClientName, "Client")],
            AcceptedPayees: [r.ClientName],
            RequestedPayees: [],
            VerifiedContact: string.IsNullOrWhiteSpace(contact) ? "Contact details on the client file" : contact,
            TravelNotice: null,
            IncomeOnFile: null,
            EvidenceRule: null,
            AmountAtRisk: null,
            Timeline:
            [
                new(received, false, isClaim ? "claim" : "request", "Request",
                    $"Request received — {r.RequestType}",
                    string.IsNullOrWhiteSpace(r.RequestSummary) ? "Captured by the Onboarding Agent." : r.RequestSummary,
                    "info"),
                new(received, false, "info", "Client history",
                    "No other client activity recorded",
                    "This case was opened live with the Onboarding Agent, so there is no further client history in the scenario data.",
                    "info"),
            ],
            Screening:
            [
                new("PEP and sanctions", "PEP and sanctions screening", "Not yet run for this case", "info"),
            ],
            References: [RedFlagsGuide, InGoodOrderGuide],
            NextStep: "Run identity and PEP / sanctions screening, check any documents, then release the case to the next team.",
            Outcome: null);
    }
}
