using System.Globalization;
using System.Text.RegularExpressions;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// One evidence requirement an insurer asks for at underwriting, triggered by
/// the life insured's age and the cover applied for. A rule with no threshold
/// applies to every application in its age band.
/// </summary>
/// <param name="Requirement">What the insurer needs, e.g. "Blood profile".</param>
/// <param name="Category">"Personal statement", "Medical" or "Financial".</param>
/// <param name="MinAge">Youngest age (last birthday) the rule applies to.</param>
/// <param name="MaxAge">Oldest age the rule applies to.</param>
/// <param name="LifeTpdAbove">Triggered when total life or TPD cover on one life is above this amount.</param>
/// <param name="TraumaAbove">Triggered when trauma cover on the life insured is above this amount.</param>
/// <param name="IncomeProtectionMonthlyAbove">Triggered when the income protection monthly benefit is above this amount (0 = any income protection).</param>
/// <param name="BusinessCover">Triggered when the business cover applied for includes this kind ("buy-sell" or "key person").</param>
/// <param name="Turnaround">Typical time to complete the requirement.</param>
/// <param name="ArrangedBy">Who arranges it.</param>
public record RequirementRule(
    string Requirement,
    string Category,
    int MinAge,
    int MaxAge,
    decimal? LifeTpdAbove,
    decimal? TraumaAbove,
    decimal? IncomeProtectionMonthlyAbove,
    string? BusinessCover,
    string Turnaround,
    string ArrangedBy);

/// <summary>
/// One insurer on Life Advice Guru's approved product list (APL), as the
/// Underwriting &amp; New Business team sees it — mirrors "Directory — insurers
/// on the approved product list" in the <c>life_knowledge</c> index:
/// covers, contacts, service standards, underwriting thresholds and the
/// Life Advice Guru cases with that insurer. Every insurer and contact is fictional.
/// </summary>
public record InsurerDirectoryEntry(
    string Name,
    string ShortName,
    string PolicyPrefix,
    string Profile,
    string Covers,
    string NewBusinessContact,
    string UnderwritingContact,
    string ClaimsContact,
    string? ClaimsIntegrityContact,
    IReadOnlyList<string> ServiceLevels,
    string UnderwritingMethod,
    IReadOnlyList<RequirementRule> RequirementRules,
    string IncomeEvidence,
    string CoolingOff,
    string Notes,
    string PracticeCases,
    IReadOnlyList<string> Keywords);

/// <summary>One requirement returned by <see cref="InsurerDirectory.CheckRequirements"/>.</summary>
public record RequirementResult(
    string Requirement,
    string Category,
    string Reason,
    string Turnaround,
    string ArrangedBy);

/// <summary>
/// The evidence an insurer needs for one life insured, for the age and the
/// cover applied for (the <c>underwriting_checkRequirements</c> MCP tool).
/// </summary>
public record RequirementCheck(
    string Insurer,
    string PolicyPrefix,
    string? LifeInsured,
    int Age,
    decimal LifeSumInsured,
    decimal TpdSumInsured,
    decimal TraumaSumInsured,
    decimal IncomeProtectionMonthly,
    string? BusinessCover,
    IReadOnlyList<RequirementResult> Requirements,
    IReadOnlyList<string> Reminders);

/// <summary>
/// Deterministic, in-memory directory of the four insurers on Life Advice Guru's APL
/// — Harbourline Life (<c>HL-</c>), Corella Life (<c>CR-</c>), Lyrebird Life
/// (<c>LB-</c>) and Northbank Life (<c>NB-</c>) — mirroring the
/// <c>life_knowledge</c> directory document. Backs the
/// <c>underwriting_lookupInsurer</c> and <c>underwriting_checkRequirements</c>
/// MCP tools and the deterministic <c>/underwriting/*</c> flow.
/// </summary>
public static class InsurerDirectory
{
    /// <summary>Exact knowledge-base title of the directory document.</summary>
    public const string KnowledgeBaseTitle = "Directory — insurers on the approved product list";

    /// <summary>What Uma does when an insurer misses its service standard.</summary>
    public const string ChasingRule =
        "If an insurer misses its service standard, Uma (underwriting) or Ben (claims) chases by email and phone and tells the client " +
        "what is happening. Escalate to Theo, the Practice Team Leader, after 10 business days without a response.";

    private const string Pathology = "Collected at an accredited pathology centre; results usually in 2–3 business days.";
    private const string Examination = "Nurse or doctor visit booked through the insurer's medical panel.";
    private const string TeleInterview = "Phone interview with the insurer's underwriting team.";
    private const string CoolingOff14 = "14 days from the earlier of receiving confirmation of the policy and the end of the fifth business day after it's issued; not available once a claim has been made (Corporations Act s1019B).";

    private static RequirementRule TeleInterviewRule(string shortName) =>
        new("Tele-interview (personal statement)", "Personal statement", 18, 70, null, null, null, null, TeleInterview, $"{shortName} underwriting team");

    private static RequirementRule Medical(string requirement, int minAge, decimal? lifeTpdAbove, string turnaround) =>
        new(requirement, "Medical", minAge, 70, lifeTpdAbove, null, null, null, turnaround, "Insurer's medical panel");

    public static readonly InsurerDirectoryEntry HarbourlineLife = new(
        Name: "Harbourline Life",
        ShortName: "Harbourline",
        PolicyPrefix: "HL-",
        Profile: "Insurer on Life Advice Guru's APL — the Harbourline Protection Plan. No super-linking.",
        Covers: "Life (with terminal illness benefit), TPD (any occupation inside or outside super; own occupation outside super only), trauma, income protection; key person and buy-sell cover. No super-linking.",
        NewBusinessContact: "New business · newbusiness@harbourlinelife.example.com.au · (03) 5550 1100",
        UnderwritingContact: "Underwriting · underwriting@harbourlinelife.example.com.au · (03) 5550 1101",
        ClaimsContact: "Claims · claims@harbourlinelife.example.com.au · (03) 5550 1102",
        ClaimsIntegrityContact: null,
        ServiceLevels: new[]
        {
            "Application acknowledged within 1 business day.",
            "Underwriting decision within 5 business days of the last requirement.",
            "A named claims consultant within 2 business days of a claim.",
            "Income protection claim decision usually within 5 business days of receiving all information.",
        },
        UnderwritingMethod: "Tele-interview for every application; blood profile and medical examination by age and total life or TPD cover on one life; income evidence for income protection.",
        RequirementRules: new[]
        {
            TeleInterviewRule("Harbourline"),
            Medical("Blood profile", 46, 1_000_000m, Pathology),
            Medical("Medical examination", 51, 1_500_000m, Examination),
            new("Income evidence for income protection", "Financial", 18, 70, null, null, 0m, null,
                "Supplied by the client (or their accountant) with the application.", "Client"),
        },
        IncomeEvidence: "Latest payslip (employees) or two years' tax returns or BAS and profit and loss statements (self-employed).",
        CoolingOff: CoolingOff14,
        Notes: "Income protection pays the lesser of the insured monthly benefit and 70% of average monthly earnings in the 12 months before the disability (policies issued from 1 Oct 2021).",
        PracticeCases: "Income protection HL-23051877 (Tom Bradley, REQ-20261111-LIF004 — lodged 12 Nov 2026, accepted 19 Nov 2026).",
        Keywords: new[] { "harbourline", "hl", "income protection", "ip", "self-employed", "key person", "buy-sell" });

    public static readonly InsurerDirectoryEntry CorellaLife = new(
        Name: "Corella Life",
        ShortName: "Corella",
        PolicyPrefix: "CR-",
        Profile: "Insurer on Life Advice Guru's APL — the Corella Protection Plan and the Corella Super Plan, with super-linking.",
        Covers: "Life (with terminal illness benefit), TPD, trauma, income protection; super-linking — life and any-occupation TPD inside Corella's super plan, with an own-occupation TPD extension, trauma and income protection outside super linked to it; key person and buy-sell cover.",
        NewBusinessContact: "New business · newbusiness@corellalife.example.com.au · (03) 5550 1200",
        UnderwritingContact: "Underwriting · underwriting@corellalife.example.com.au · (03) 5550 1201",
        ClaimsContact: "Claims · claims@corellalife.example.com.au · (03) 5550 1202",
        ClaimsIntegrityContact: null,
        ServiceLevels: new[]
        {
            "Tele-interview booked within 2 business days of the application.",
            "Underwriting decision within 5 business days of the last requirement.",
            "A dedicated case manager for every death and terminal illness claim.",
            "Death claim decision usually within 5 business days of receiving all information, with payment within 3 business days of acceptance.",
        },
        UnderwritingMethod: "Tele-interview for every application. Ages 18–45: tele-interview only up to $1,500,000 life or TPD, $500,000 trauma or $10,000 a month income protection; above those amounts or from age 46, a blood profile; from age 51 above $1,500,000, a medical examination.",
        RequirementRules: new[]
        {
            TeleInterviewRule("Corella"),
            new("Blood profile", "Medical", 18, 45, 1_500_000m, null, null, null, Pathology, "Insurer's medical panel"),
            new("Blood profile", "Medical", 18, 45, null, 500_000m, null, null, Pathology, "Insurer's medical panel"),
            new("Blood profile", "Medical", 18, 45, null, null, 10_000m, null, Pathology, "Insurer's medical panel"),
            Medical("Blood profile", 46, null, Pathology),
            Medical("Medical examination", 51, 1_500_000m, Examination),
        },
        IncomeEvidence: "Earnings are confirmed at claim time; Life Advice Guru keeps the client's latest payslip from the fact find on file.",
        CoolingOff: CoolingOff14,
        Notes: "Corella reviews an income protection exclusion on request after two symptom-free years. The own-occupation TPD extension can't be bought on its own — it's linked to any-occupation TPD inside the Corella Super Plan.",
        PracticeCases: "CR-26118204 (Priya Natarajan, REQ-20261103-LIF001 — tele-interview 10 Nov 2026, decision 16 Nov 2026); CR-19033105 (Mei Chen, REQ-20261113-LIF005 — death claim lodged 16 Nov 2026, accepted 20 Nov 2026, paid 23 Nov 2026).",
        Keywords: new[] { "corella", "cr", "family", "families", "physiotherapist", "super", "super-linked", "super-linking", "own occupation", "tpd", "key person", "buy-sell" });

    public static readonly InsurerDirectoryEntry LyrebirdLife = new(
        Name: "Lyrebird Life",
        ShortName: "Lyrebird",
        PolicyPrefix: "LB-",
        Profile: "Insurer on Life Advice Guru's APL — Lyrebird Life Protection for individuals and families. No business protection.",
        Covers: "Life (with terminal illness benefit), TPD, trauma and income protection for individuals and families. No business protection.",
        NewBusinessContact: "New business · newbusiness@lyrebirdlife.example.com.au · (03) 5550 1300",
        UnderwritingContact: "Underwriting · underwriting@lyrebirdlife.example.com.au · (03) 5550 1301",
        ClaimsContact: "Claims · claims@lyrebirdlife.example.com.au · (03) 5550 1302",
        ClaimsIntegrityContact: "Claims Integrity (fraud) team · integrity@lyrebirdlife.example.com.au · (03) 5550 1303 — for alerts about suspected fraud or identity takeover; Life Advice Guru alerts it only after the Practice Team Leader approves.",
        ServiceLevels: new[]
        {
            "Underwriting decision within 7 business days of the last requirement.",
            "Trauma claim decision usually within 10 business days of receiving all information.",
        },
        UnderwritingMethod: "Tele-interview for every application; blood profile and medical examination by age and total life or TPD cover on one life.",
        RequirementRules: new[]
        {
            TeleInterviewRule("Lyrebird"),
            Medical("Blood profile", 46, 750_000m, Pathology),
            Medical("Medical examination", 51, 1_250_000m, Examination),
        },
        IncomeEvidence: "Earnings evidence for income protection is checked at claim time.",
        CoolingOff: CoolingOff14,
        Notes: "Trauma cancer claims need a specialist's diagnosis supported by histopathology. Lyrebird pays only into an account in the policy owner's name (or the nominated beneficiary's, for life cover).",
        PracticeCases: "LB-19004417 (Grace Williams, REQ-20261106-LIF002 — fraud team alerted 6 Nov 2026; no claim lodged).",
        Keywords: new[] { "lyrebird", "lb", "trauma", "cancer", "families", "fraud", "integrity" });

    public static readonly InsurerDirectoryEntry NorthbankLife = new(
        Name: "Northbank Life",
        ShortName: "Northbank",
        PolicyPrefix: "NB-",
        Profile: "Insurer on Life Advice Guru's APL — strongest in business protection (Northbank Business Protection).",
        Covers: "Life (with terminal illness benefit), TPD, trauma and income protection; buy-sell cover (owned by each owner on their own life, or cross-owned) and key person cover (revenue or capital purpose, owned by the business).",
        NewBusinessContact: "New business · business@northbanklife.example.com.au · (03) 5550 1400",
        UnderwritingContact: "Underwriting · underwriting@northbanklife.example.com.au · (03) 5550 1401",
        ClaimsContact: "Claims · claims@northbanklife.example.com.au · (03) 5550 1402",
        ClaimsIntegrityContact: null,
        ServiceLevels: new[]
        {
            "Fast financial underwriting — accepts the accountant's valuation and the last two years' financial statements as financial evidence, with a financial underwriting decision within 5 business days.",
            "Medical underwriting decision within 5 business days of the last requirement.",
        },
        UnderwritingMethod: "Tele-interview for every application; blood profile and medical examination by age and total life or TPD cover on one life; financial evidence for all business protection.",
        RequirementRules: new[]
        {
            TeleInterviewRule("Northbank"),
            Medical("Blood profile", 46, 1_000_000m, Pathology),
            Medical("Medical examination", 51, 1_500_000m, Examination),
            new("Financial evidence — buy-sell (business valuation and two years' financial statements)", "Financial", 18, 70, null, null, null, "buy-sell",
                "Supplied by the business's accountant; financial underwriting decision within 5 business days.", "Business's accountant"),
            new("Financial evidence — key person (two years' financial statements and the accountant's estimate of the loss)", "Financial", 18, 70, null, null, null, "key person",
                "Supplied by the business's accountant; financial underwriting decision within 5 business days.", "Business's accountant"),
        },
        IncomeEvidence: "Financial evidence for all business protection: business valuation and two years' financial statements (buy-sell); two years' financial statements and the accountant's estimate of the loss (key person).",
        CoolingOff: CoolingOff14,
        Notes: "Loadings and exclusions are reviewed on request after two years if the client's health results improve. Northbank requires a buy-sell agreement prepared by the owners' lawyer and doesn't draft one.",
        PracticeCases: "NB-26300418 (George Papadopoulos, buy-sell), NB-26300419 (Harpreet Kaur, buy-sell) and NB-26300420 (key person on George, owned by Northcote Custom Joinery Pty Ltd) — REQ-20261109-LIF003, in force 20 Nov 2026.",
        Keywords: new[] { "northbank", "nb", "business", "business protection", "buy-sell", "buy sell", "key person", "company" });

    private static readonly IReadOnlyList<InsurerDirectoryEntry> _all = new[]
    {
        HarbourlineLife, CorellaLife, LyrebirdLife, NorthbankLife
    };

    /// <summary>The four insurers on Life Advice Guru's approved product list.</summary>
    public static IReadOnlyList<InsurerDirectoryEntry> All => _all;

    /// <summary>
    /// Search the directory by insurer name, policy prefix (<c>HL</c>,
    /// <c>CR</c>, <c>LB</c>, <c>NB</c>, or a full policy number such as
    /// <c>CR-26118204</c>) or keyword ("business protection", "trauma",
    /// "income protection"). An empty query or "all" returns the whole APL.
    /// </summary>
    public static IReadOnlyList<InsurerDirectoryEntry> Lookup(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)
            || query.Trim().Equals("apl", StringComparison.OrdinalIgnoreCase))
            return _all;

        var q = query.Trim().ToLowerInvariant();
        var tokens = Regex.Split(q, @"[\s,;|/]+")
            .Where(t => t.Length >= 2 && t is not "and" and not "the" and not "life" and not "cover" and not "insurer")
            .ToList();

        var scored = _all.Select(e =>
        {
            var score = 0;
            var name = e.Name.ToLowerInvariant();
            if (q.Contains(name) || name.Contains(q)) score += 10;
            foreach (var t in tokens)
            {
                if (name.Contains(t)) score += 4;
                var prefix = e.PolicyPrefix.TrimEnd('-').ToLowerInvariant();
                if (t == prefix || t.StartsWith(prefix + "-", StringComparison.Ordinal)) score += 10;
                if (e.Keywords.Any(k => k.Equals(t, StringComparison.OrdinalIgnoreCase))) score += 3;
                else if (t.Length >= 4 && e.Keywords.Any(k => k.Contains(t))) score += 2;
            }
            if (e.Keywords.Any(k => k.Contains(' ') && q.Contains(k))) score += 3;
            return (Entry: e, Score: score);
        });

        return scored.Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Select(s => s.Entry)
            .ToList();
    }

    /// <summary>Find an insurer by name, short name or policy prefix; null if not on the APL.</summary>
    public static InsurerDirectoryEntry? Find(string? nameOrPrefix)
    {
        if (string.IsNullOrWhiteSpace(nameOrPrefix)) return null;
        var q = nameOrPrefix.Trim();
        return _all.FirstOrDefault(e =>
                   e.Name.Equals(q, StringComparison.OrdinalIgnoreCase)
                   || e.ShortName.Equals(q, StringComparison.OrdinalIgnoreCase)
                   || e.PolicyPrefix.TrimEnd('-').Equals(q.TrimEnd('-'), StringComparison.OrdinalIgnoreCase)
                   || q.StartsWith(e.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
               ?? Lookup(q).FirstOrDefault();
    }

    /// <summary>
    /// The evidence <paramref name="insurer"/> needs for one life insured,
    /// given their age and the cover applied for. Thresholds apply to the
    /// total life or TPD cover on one life — every policy on the same life
    /// (for example buy-sell plus key person) is added together.
    /// </summary>
    public static RequirementCheck CheckRequirements(
        InsurerDirectoryEntry insurer,
        int age,
        decimal lifeSumInsured,
        decimal tpdSumInsured = 0m,
        decimal traumaSumInsured = 0m,
        decimal incomeProtectionMonthly = 0m,
        string? businessCover = null,
        bool selfEmployed = false,
        string? lifeInsured = null)
    {
        var business = (businessCover ?? string.Empty).Trim();
        var hasBusiness = business.Length > 0 && !business.Equals("none", StringComparison.OrdinalIgnoreCase);
        var lifeTpd = Math.Max(lifeSumInsured, tpdSumInsured);
        var results = new List<RequirementResult>();

        foreach (var rule in insurer.RequirementRules)
        {
            if (age < rule.MinAge || age > rule.MaxAge) continue;

            string? reason = null;
            if (rule.LifeTpdAbove is decimal lt)
            {
                if (lifeTpd > lt)
                    reason = $"Age {age} with {Money(lifeTpd)} of life or TPD cover on one life — {insurer.ShortName} asks for this {AgeBand(rule)} above {Money(lt)}.";
            }
            else if (rule.TraumaAbove is decimal ta)
            {
                if (traumaSumInsured > ta)
                    reason = $"Trauma cover of {Money(traumaSumInsured)} — above {insurer.ShortName}'s tele-interview-only limit of {Money(ta)} {AgeBand(rule)}.";
            }
            else if (rule.IncomeProtectionMonthlyAbove is decimal ip)
            {
                if (incomeProtectionMonthly > ip)
                    reason = ip == 0m
                        ? $"Income protection of {Money(incomeProtectionMonthly)} a month — {insurer.ShortName} needs {(selfEmployed ? "two years' tax returns or BAS and profit and loss statements (self-employed)" : "the latest payslip (employee)")}."
                        : $"Income protection of {Money(incomeProtectionMonthly)} a month — above {insurer.ShortName}'s tele-interview-only limit of {Money(ip)} a month {AgeBand(rule)}.";
            }
            else if (rule.BusinessCover is string kind)
            {
                if (hasBusiness && business.Contains(kind, StringComparison.OrdinalIgnoreCase))
                    reason = $"{char.ToUpperInvariant(kind[0])}{kind[1..]} cover — {insurer.ShortName} needs financial evidence for all business protection.";
            }
            else
            {
                reason = rule.MinAge > 18
                    ? $"Age {age} — {insurer.ShortName} asks for this from age {rule.MinAge}."
                    : $"Every {insurer.ShortName} application.";
            }

            if (reason is null) continue;
            if (results.Any(r => r.Requirement.Equals(rule.Requirement, StringComparison.OrdinalIgnoreCase))) continue;
            results.Add(new RequirementResult(rule.Requirement, rule.Category, reason, rule.Turnaround, rule.ArrangedBy));
        }

        var reminders = new List<string>
        {
            "Explain the duty to take reasonable care before the first health or lifestyle question.",
            "Never ask for or use genetic test results — the client doesn't need to disclose them.",
            "The insurer may ask for a GP or specialist report if an answer needs more detail.",
            "Don't cancel or reduce any existing cover until the new cover is accepted and in force — that's the client's decision with Amelia.",
        };

        return new RequirementCheck(
            Insurer: insurer.Name,
            PolicyPrefix: insurer.PolicyPrefix,
            LifeInsured: string.IsNullOrWhiteSpace(lifeInsured) ? null : lifeInsured.Trim(),
            Age: age,
            LifeSumInsured: lifeSumInsured,
            TpdSumInsured: tpdSumInsured,
            TraumaSumInsured: traumaSumInsured,
            IncomeProtectionMonthly: incomeProtectionMonthly,
            BusinessCover: hasBusiness ? business : null,
            Requirements: results,
            Reminders: reminders);
    }

    private static string AgeBand(RequirementRule r) =>
        r.MaxAge >= 70
            ? (r.MinAge <= 18 ? "at any age" : $"from age {r.MinAge}")
            : $"for ages {r.MinAge}–{r.MaxAge}";

    /// <summary>Plain-English summary of an insurer's evidence thresholds, one line per rule.</summary>
    public static IReadOnlyList<string> ThresholdSummary(InsurerDirectoryEntry e) =>
        e.RequirementRules.Select(r =>
        {
            var band = AgeBand(r);
            if (r.LifeTpdAbove is decimal lt) return $"{r.Requirement}: {band}, above {Money(lt)} of life or TPD cover on one life.";
            if (r.TraumaAbove is decimal ta) return $"{r.Requirement}: {band}, trauma cover above {Money(ta)}.";
            if (r.IncomeProtectionMonthlyAbove is decimal ip)
                return ip == 0m ? $"{r.Requirement}: {e.IncomeEvidence}" : $"{r.Requirement}: {band}, income protection above {Money(ip)} a month.";
            if (r.BusinessCover is string kind) return $"{r.Requirement}: every {kind} application.";
            return r.MinAge > 18 ? $"{r.Requirement}: {band}, any amount." : $"{r.Requirement}: every application.";
        }).ToList();

    /// <summary>JSON-friendly view of a directory entry (used by the MCP tool and the page).</summary>
    public static object ToView(InsurerDirectoryEntry e) => new
    {
        name = e.Name,
        policyPrefix = e.PolicyPrefix,
        profile = e.Profile,
        covers = e.Covers,
        newBusinessContact = e.NewBusinessContact,
        underwritingContact = e.UnderwritingContact,
        claimsContact = e.ClaimsContact,
        claimsIntegrityContact = e.ClaimsIntegrityContact,
        serviceLevels = e.ServiceLevels,
        underwritingMethod = e.UnderwritingMethod,
        underwritingThresholds = ThresholdSummary(e),
        incomeEvidence = e.IncomeEvidence,
        coolingOff = e.CoolingOff,
        notes = e.Notes,
        practiceCases = e.PracticeCases,
        chasing = ChasingRule
    };

    internal static string Money(decimal v) =>
        v == decimal.Truncate(v)
            ? v.ToString("C0", CultureInfo.GetCultureInfo("en-AU"))
            : v.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
}
