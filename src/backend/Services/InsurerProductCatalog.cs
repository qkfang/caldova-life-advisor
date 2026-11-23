using System.Globalization;
using LifeAdviceGuru.App.Models;

namespace LifeAdviceGuru.App.Services;

/// <summary>One person whose life is insured on the application, with the cover applied for on their life.</summary>
public record LifeInsuredProfile(
    string Name,
    string ClientNumber,
    int Age,
    string DateOfBirth,
    string Occupation,
    string? Smoker,
    decimal LifeSumInsured,
    decimal TpdSumInsured,
    decimal TraumaSumInsured,
    decimal IncomeProtectionMonthly,
    string? BusinessCover,
    bool SelfEmployed);

/// <summary>One cover on the application, with its ownership, premium and underwriting terms.</summary>
/// <param name="TermsTone">"ok", "warn" or "info" — drives the page's pill colour.</param>
public record UnderwritingCover(
    string LifeInsured,
    string Cover,
    string SumInsured,
    string Ownership,
    string Structure,
    decimal PremiumStandard,
    decimal PremiumFinal,
    string PaidBy,
    string Terms,
    string TermsTone,
    string? PolicyNumber);

/// <summary>One piece of underwriting evidence and where it's up to.</summary>
public record UnderwritingRequirement(
    string LifeInsured,
    string Requirement,
    string Category,
    string Reason,
    string Status,
    string Tone,
    string? Date,
    string? Outcome);

/// <summary>One line of the disclosure checklist Uma's agent works through with the client.</summary>
/// <param name="Status">"done", "disclosed", "action" or "info".</param>
public record DisclosureItem(string Id, string Label, string Status, string Detail);

/// <summary>One event on the underwriting timeline.</summary>
public record UnderwritingEvent(string Date, string Event, string Who, string Tone, bool HumanDecision);

/// <summary>The insurer's decision on one cover, explained in plain English.</summary>
public record UnderwritingDecisionItem(string LifeInsured, string Cover, string Decision, string Tone, string PlainEnglish);

/// <summary>The client's decision on the insurer's terms — always made by the client with the adviser.</summary>
public record ClientDecisionStep(
    string Status,
    string Tone,
    string Who,
    string? Date,
    string Summary,
    IReadOnlyList<string> Points,
    string CoolingOff,
    string ExistingCoverNote);

/// <summary>A link to the agent page that looks after a case Underwriting doesn't handle.</summary>
public record AgentPointer(string Label, string Href);

/// <summary>
/// Deterministic underwriting plan for a Client Case — what Uma's agent
/// produces: the insurer, the lives insured and covers, the evidence needed
/// for each age and sum insured, the disclosure checklist, the timeline, the
/// insurer's decision in plain English and the client's decision with the
/// adviser. Returned by <see cref="InsurerProductCatalog.Build"/> so the Try
/// it out page works with or without a Foundry connection.
/// </summary>
public record UnderwritingPlan(
    string CaseNumber,
    string ScenarioKey,
    bool Applicable,
    string? NotApplicableNote,
    IReadOnlyList<AgentPointer> Redirects,
    string AsAtLabel,
    string OverallStatus,
    string StatusTone,
    string Headline,
    string ReferredBy,
    string LodgedOn,
    InsurerDirectoryEntry? Insurer,
    string InsurerQuery,
    IReadOnlyList<LifeInsuredProfile> LivesInsured,
    IReadOnlyList<UnderwritingCover> Covers,
    decimal AnnualPremiumStandard,
    decimal AnnualPremiumFinal,
    string PremiumNote,
    IReadOnlyList<UnderwritingRequirement> Requirements,
    string DutyOfCareScript,
    string DutyAcknowledgement,
    IReadOnlyList<DisclosureItem> Disclosures,
    IReadOnlyList<string> PdfDisclosures,
    IReadOnlyList<UnderwritingEvent> Timeline,
    IReadOnlyList<UnderwritingDecisionItem> Decisions,
    string DecisionSummary,
    ClientDecisionStep ClientDecision,
    string ApplicationStatus,
    IReadOnlyList<string> Notes,
    bool HumanApprovalRequired,
    string HumanApprovalReason);

/// <summary>
/// Builds the deterministic underwriting plan for each Client Case — Priya's
/// family protection application with Corella Life and George and Harpreet's
/// business protection applications with Northbank Life — and a polite
/// pointer to the Claims Agent for the three claim cases. Figures and dates
/// follow the scenario canon; insurer details come from
/// <see cref="InsurerDirectory"/>.
/// </summary>
public static class InsurerProductCatalog
{
    /// <summary>The "today" the five scenario cases are reported as at.</summary>
    public static readonly DateOnly ScenarioToday = new(2026, 11, 23);

    private static readonly CultureInfo AuCulture = CultureInfo.GetCultureInfo("en-AU");

    // Onboarding sample ids and seeded case-number tags for each scenario.
    private static readonly Dictionary<string, (string Key, string RequestType)> SampleScenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["onboarding-priya"] = ("priya", RequestTypes.NewProtectionAdvice),
        ["onboarding-grace"] = ("grace", RequestTypes.TraumaClaimBankChange),
        ["onboarding-george"] = ("george", RequestTypes.BusinessProtectionAdvice),
        ["onboarding-tom"] = ("tom", RequestTypes.IncomeProtectionClaim),
        ["onboarding-robert"] = ("robert", RequestTypes.DeathClaim),
    };

    private static readonly Dictionary<string, (string Key, string RequestType)> SeededTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LIF001"] = ("priya", RequestTypes.NewProtectionAdvice),
        ["LIF002"] = ("grace", RequestTypes.TraumaClaimBankChange),
        ["LIF003"] = ("george", RequestTypes.BusinessProtectionAdvice),
        ["LIF004"] = ("tom", RequestTypes.IncomeProtectionClaim),
        ["LIF005"] = ("robert", RequestTypes.DeathClaim),
    };

    /// <summary>
    /// The scenario a case belongs to — "priya", "grace", "george", "tom" or
    /// "robert" — or null for any other case (including a scenario case whose
    /// request type was changed on the Onboarding form).
    /// </summary>
    public static string? ScenarioKey(CaseRecord c)
    {
        if (SampleScenarios.TryGetValue(c.SampleId ?? string.Empty, out var s))
            return string.Equals(s.RequestType, c.RequestType, StringComparison.OrdinalIgnoreCase) ? s.Key : null;

        var tag = (c.CaseNumber ?? string.Empty).Split('-').LastOrDefault() ?? string.Empty;
        if (SeededTags.TryGetValue(tag, out var t) && string.Equals(t.RequestType, c.RequestType, StringComparison.OrdinalIgnoreCase))
            return t.Key;
        return null;
    }

    /// <summary>True for the new-advice request types (Priya's and George's journeys).</summary>
    public static bool IsAdviceRequest(CaseRecord c) =>
        string.Equals(c.RequestType, RequestTypes.NewProtectionAdvice, StringComparison.OrdinalIgnoreCase)
        || string.Equals(c.RequestType, RequestTypes.BusinessProtectionAdvice, StringComparison.OrdinalIgnoreCase)
        || c.RequestType.Contains("advice", StringComparison.OrdinalIgnoreCase);

    public static UnderwritingPlan Build(CaseRecord c) => ScenarioKey(c) switch
    {
        "priya" => Priya(c),
        "george" => George(c),
        "grace" => NotApplicable(c, "grace"),
        "tom" => NotApplicable(c, "tom"),
        "robert" => NotApplicable(c, "robert"),
        _ => IsAdviceRequest(c) ? GenericAdvice(c) : NotApplicable(c, "claim"),
    };

    /// <summary>"Wed 18 Nov 2026".</summary>
    public static string Label(DateOnly d) => d.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    private static string D(int month, int day) => Label(new DateOnly(2026, month, day));

    public static string Money(decimal v) =>
        v == decimal.Truncate(v) ? v.ToString("C0", AuCulture) : v.ToString("C2", AuCulture);

    /// <summary>The <c>underwriting_checkRequirements</c> result for every life insured on the plan.</summary>
    public static IReadOnlyList<RequirementCheck> RequirementChecks(UnderwritingPlan plan) =>
        plan.Insurer is null
            ? Array.Empty<RequirementCheck>()
            : plan.LivesInsured.Select(l => InsurerDirectory.CheckRequirements(
                plan.Insurer, l.Age, l.LifeSumInsured, l.TpdSumInsured, l.TraumaSumInsured,
                l.IncomeProtectionMonthly, l.BusinessCover, l.SelfEmployed, l.Name)).ToList();

    private static string CoolingOffLine(InsurerDirectoryEntry insurer, string startLabel) =>
        $"{LifeAdviceFacts.CoolingOffSummary} {insurer.Name} issued the cover on {startLabel}.";

    /// <summary>
    /// Map a plan to the application and disclosure summary PDF. Health
    /// detail is left out — the summary records what was disclosed, and the
    /// full answers stay with the insurer.
    /// </summary>
    public static ApplicationPdfInput ToPdfInput(CaseRecord c, UnderwritingPlan plan)
    {
        var lives = plan.LivesInsured;
        var clients = lives.Count == 0 ? c.ClientName : string.Join(" and ", lives.Select(l => l.Name));
        if (plan.Covers.Any(cv => cv.Ownership.StartsWith("Company-owned", StringComparison.Ordinal)))
        {
            var owner = plan.Covers.First(cv => cv.Ownership.StartsWith("Company-owned", StringComparison.Ordinal)).Ownership
                .Replace("Company-owned — ", string.Empty, StringComparison.Ordinal);
            clients += $" (and {owner} as owner of the key person policy)";
        }
        var numbers = lives.Count switch
        {
            0 => c.ClientNumber,
            1 => lives[0].ClientNumber,
            _ => string.Join(", ", lives.Select(l => $"{l.Name.Split(' ')[0]} {l.ClientNumber}")),
        };

        var covers = plan.Covers.Select(cv => new ApplicationPdfCover(
            LifeInsured: cv.LifeInsured,
            Cover: cv.Cover,
            SumInsured: cv.SumInsured,
            Ownership: cv.Ownership,
            Premium: cv.PremiumFinal == cv.PremiumStandard
                ? Money(cv.PremiumFinal)
                : $"{Money(cv.PremiumFinal)} (quoted {Money(cv.PremiumStandard)})",
            Terms: cv.Terms,
            PolicyNumber: cv.PolicyNumber)).ToList();

        var ownership = plan.Covers
            .GroupBy(cv => cv.Ownership)
            .Select(g => $"{g.Key}: {string.Join("; ", g.Select(cv => $"{cv.LifeInsured} — {cv.Cover}"))}.")
            .ToList();

        var requirements = plan.Requirements.Select(r => new ApplicationPdfRequirement(
            LifeInsured: r.LifeInsured,
            Requirement: r.Requirement,
            Status: r.Date is null ? r.Status : $"{r.Status} {r.Date}",
            Detail: r.Outcome ?? r.Reason)).ToList();

        var notes = new List<string>();
        if (plan.ClientDecision.Date is not null)
            notes.Add($"Client decision: {plan.ClientDecision.Status.ToLowerInvariant()} — {plan.ClientDecision.Who}, {plan.ClientDecision.Date}.");
        if (!string.IsNullOrWhiteSpace(plan.ClientDecision.ExistingCoverNote))
            notes.Add(plan.ClientDecision.ExistingCoverNote);

        return new ApplicationPdfInput(
            CaseNumber: c.CaseNumber,
            Clients: clients,
            ClientNumbers: numbers,
            RequestType: c.RequestType,
            Adviser: LifeAdviceFacts.Adviser,
            ReferredBy: string.IsNullOrWhiteSpace(plan.ReferredBy) ? "Existing client" : plan.ReferredBy,
            Insurer: plan.Insurer is null
                ? "To be confirmed from the signed Statement of Advice"
                : $"{plan.Insurer.Name} (policy prefix {plan.Insurer.PolicyPrefix}) — on Life Advice Guru's approved product list",
            InsurerContact: plan.Insurer?.NewBusinessContact ?? "—",
            LodgedOn: plan.LodgedOn,
            AsAt: plan.AsAtLabel,
            ApplicationStatus: plan.ApplicationStatus,
            Covers: covers,
            PremiumSummary: plan.PremiumNote,
            Ownership: ownership,
            Disclosures: plan.PdfDisclosures,
            Requirements: requirements,
            DutyOfCareText: plan.DutyOfCareScript,
            DutyOfCareAcknowledgement: plan.DutyAcknowledgement,
            DecisionSummary: string.IsNullOrWhiteSpace(plan.DecisionSummary) ? null : plan.DecisionSummary,
            CoolingOff: string.IsNullOrWhiteSpace(plan.ClientDecision.CoolingOff) ? null : plan.ClientDecision.CoolingOff,
            Notes: notes);
    }

    // ── Scenario 1 — Priya: family protection with Corella Life ────────────
    private static UnderwritingPlan Priya(CaseRecord c)
    {
        var insurer = InsurerDirectory.CorellaLife;
        const string priya = "Priya Natarajan";
        const string policy = "CR-26118204";
        var start = D(11, 18);

        var life = new LifeInsuredProfile(priya, "CL-104582", 34, "14/02/1992",
            "Physiotherapist, private clinic in Brunswick (salary $105,000 + super)", "Non-smoker",
            LifeSumInsured: 750_000m, TpdSumInsured: 600_000m, TraumaSumInsured: 150_000m,
            IncomeProtectionMonthly: 6_125m, BusinessCover: null, SelfEmployed: false);

        const string insideSuper = "Inside super — held in the Corella Life super plan for Priya";
        const string outsideSuper = "Outside super — owned by Priya";
        var covers = new[]
        {
            new UnderwritingCover(priya, "Life cover (with terminal illness benefit)", "$750,000", insideSuper, "Inside super",
                468m, 468m, "Super plan — funded by a partial rollover from Riverbend Super", "Standard rates", "ok", policy),
            new UnderwritingCover(priya, "TPD — any occupation", "$600,000", insideSuper, "Inside super",
                390m, 390m, "Super plan — funded by a partial rollover from Riverbend Super", "Standard rates", "ok", policy),
            new UnderwritingCover(priya, "TPD — own-occupation extension (super-linked)", "$600,000", outsideSuper, "Outside super (super-linked)",
                222m, 222m, "Priya", "Standard rates", "ok", policy),
            new UnderwritingCover(priya, "Trauma cover", "$150,000", outsideSuper, "Outside super",
                640m, 640m, "Priya", "Standard rates", "ok", policy),
            new UnderwritingCover(priya, "Income protection — 90-day waiting period, benefit period to age 65", "$6,125 a month", outsideSuper, "Outside super",
                1_460m, 1_460m, "Priya (premiums tax-deductible)", "Exclusion: back and spine conditions (review after two symptom-free years)", "warn", policy),
        };

        var check = InsurerDirectory.CheckRequirements(insurer, life.Age, life.LifeSumInsured, life.TpdSumInsured,
            life.TraumaSumInsured, life.IncomeProtectionMonthly, null, false, priya);
        var requirements = check.Requirements.Select(r => new UnderwritingRequirement(
                priya, r.Requirement, r.Category, r.Reason, "Complete", "ok", D(11, 10),
                "Answers read back and confirmed by Priya."))
            .Concat(new[]
            {
                new UnderwritingRequirement(priya, "Blood profile or medical examination", "Medical",
                    "Age 34 and within Corella's tele-interview-only limits for ages 18–45: $1,500,000 life or TPD, $500,000 trauma and $10,000 a month income protection.",
                    "Not required", "info", null, null),
                new UnderwritingRequirement(priya, "Underwriter's assessment", "Decision",
                    "Corella's underwriter reviews the tele-interview answers.",
                    "Decision made", "ok", D(11, 16), "Standard rates; income protection with a back and spine exclusion."),
            }).ToList();

        var disclosures = new[]
        {
            new DisclosureItem("duty", "Duty to take reasonable care explained before the health questions", "done",
                $"Explained on {D(11, 9)}, before the application was lodged and before any health question — including the asthma and the 2023 back strain — with the reminder that she doesn't need to disclose genetic test results. Priya confirmed she understood that answering carefully protects any future claim."),
            new DisclosureItem("existing-cover", "Existing cover disclosed", "disclosed",
                "Riverbend Super: death and TPD $210,000 — keeping it. Kestrel Retail Super: death cover $50,000 — Priya told Kestrel to keep it (otherwise Protecting Your Super would stop it on 30 Nov 2026), so it stays until the new cover is in force."),
            new DisclosureItem("occupation", "Occupation and income", "disclosed",
                "Physiotherapist at a private clinic in Brunswick; salary $105,000 + super. Payslip sighted at the fact find."),
            new DisclosureItem("smoking", "Smoking status", "disclosed", "Non-smoker."),
            new DisclosureItem("health-asthma", "Health — asthma", "disclosed",
                "Mild asthma, well controlled with a preventer inhaler."),
            new DisclosureItem("health-back", "Health — back", "disclosed",
                "Lower back strain in 2023 — six weeks of physio, full recovery."),
            new DisclosureItem("read-back", "Answers read back and confirmed", "done",
                $"At the end of the tele-interview on {D(11, 10)} Corella's underwriter read the answers back and Priya confirmed them."),
            new DisclosureItem("privacy", "Health information kept to the people who need it", "info",
                "Health details go only to Corella Life and Life Advice Guru's advice file. Hannah Lewis receives only the outcome — Priya consented to share that and nothing more."),
        };

        var timeline = new[]
        {
            new UnderwritingEvent(D(11, 3), "Referral from Hannah Lewis (Yarra Home Loans); Client Case opened.", "Olivia", "info", false),
            new UnderwritingEvent(D(11, 5), "Statement of Advice drafted by the Paraplanning Agent.", "Penny", "info", false),
            new UnderwritingEvent(D(11, 6), "Amelia reviews and signs the Statement of Advice.", "Amelia", "ok", true),
            new UnderwritingEvent(D(11, 7), "Priya signs the Authority to Proceed.", "Priya", "ok", true),
            new UnderwritingEvent(D(11, 9), "Duty to take reasonable care explained; application lodged with Corella Life.", "Uma", "info", false),
            new UnderwritingEvent(D(11, 10), "Tele-interview with Corella's underwriter; answers confirmed.", "Corella Life", "info", false),
            new UnderwritingEvent(D(11, 16), "Decision: standard rates on life, TPD and trauma (asthma accepted at standard); income protection with a back and spine exclusion.", "Corella Life", "warn", false),
            new UnderwritingEvent(D(11, 17), "Priya accepts Corella's terms with Amelia.", "Priya with Amelia", "ok", true),
            new UnderwritingEvent(start, $"Policy {policy} starts; cooling-off period begins. Handed to Client Communications for the welcome letter.", "Corella Life", "ok", false),
        };

        var decisions = new[]
        {
            new UnderwritingDecisionItem(priya, "Life cover $750,000", "Standard rates", "ok",
                "Accepted exactly as applied for."),
            new UnderwritingDecisionItem(priya, "TPD $600,000 (any occupation in super, own-occupation extension outside)", "Standard rates", "ok",
                "Accepted exactly as applied for, including the own-occupation definition for physiotherapists."),
            new UnderwritingDecisionItem(priya, "Trauma $150,000", "Standard rates", "ok",
                "Accepted exactly as applied for."),
            new UnderwritingDecisionItem(priya, "Asthma", "Accepted at standard rates", "ok",
                "Well-controlled asthma doesn't change the price or the cover — no loading and no exclusion."),
            new UnderwritingDecisionItem(priya, "Income protection $6,125 a month", "Exclusion — back and spine conditions", "warn",
                "Priya's income protection won't pay a claim caused by a back or spine condition, because of the 2023 back strain. Every other illness or injury is covered as normal. Corella will review the exclusion once Priya has had two symptom-free years — she can ask through Amelia."),
        };

        return new UnderwritingPlan(
            CaseNumber: c.CaseNumber,
            ScenarioKey: "priya",
            Applicable: true,
            NotApplicableNote: null,
            Redirects: Array.Empty<AgentPointer>(),
            AsAtLabel: Label(ScenarioToday),
            OverallStatus: $"Completed — policy {policy} in force from {start}",
            StatusTone: "ok",
            Headline: $"Corella Life accepted Priya's application on {D(11, 16)}: standard rates, with a back and spine exclusion on income protection. Priya accepted with Amelia on {D(11, 17)}; cover started on {start}.",
            ReferredBy: "Hannah Lewis, mortgage broker, Yarra Home Loans",
            LodgedOn: D(11, 9),
            Insurer: insurer,
            InsurerQuery: "Corella Life",
            LivesInsured: new[] { life },
            Covers: covers,
            AnnualPremiumStandard: 3_180m,
            AnnualPremiumFinal: 3_180m,
            PremiumNote: "Year-1 stepped premiums: $3,180 in total — $858 a year inside super (life $468 + TPD $390, paid by the super plan) and $2,322 a year outside super ($193.50 a month, within Priya's $300 budget). No loading, so the premium didn't change at underwriting.",
            Requirements: requirements,
            DutyOfCareScript: LifeAdviceFacts.DutyOfCareScript,
            DutyAcknowledgement: $"Acknowledged by Priya Natarajan on {D(11, 9)}, before the health questions.",
            Disclosures: disclosures,
            PdfDisclosures: new[]
            {
                $"Duty to take reasonable care explained on {D(11, 9)}, before any health or lifestyle question.",
                "Existing cover: Riverbend Super death and TPD $210,000 (kept); Kestrel Retail Super death cover $50,000 (kept until the new cover is in force).",
                "Occupation and income: physiotherapist; salary $105,000 + super.",
                "Smoking status: non-smoker.",
                $"Health and lifestyle questions answered in full at the tele-interview on {D(11, 10)}; two medical history items disclosed (details held by Corella Life).",
                "Answers read back and confirmed by Priya.",
            },
            Timeline: timeline,
            Decisions: decisions,
            DecisionSummary: $"Corella Life decided on {D(11, 16)}: life, TPD and trauma at standard rates, and income protection with an exclusion for back and spine conditions, reviewable after two symptom-free years. No loading — premiums stay at $3,180 for year 1.",
            ClientDecision: new ClientDecisionStep(
                Status: "Accepted by the client",
                Tone: "ok",
                Who: "Priya Natarajan, with Amelia",
                Date: D(11, 17),
                Summary: "Priya accepted Corella's terms — including the back and spine exclusion on income protection — after Amelia talked them through. Uma's agent prepared the explanation; it never accepts terms for a client.",
                Points: new[]
                {
                    "What the exclusion covers, what it doesn't, and how to ask Corella to review it after two symptom-free years.",
                    "Premiums unchanged: $3,180 in year 1 (stepped) — $2,322 outside super ($193.50 a month) and $858 inside super.",
                    $"Policy {policy} starts on {start}.",
                    "Riverbend Super's $210,000 cover stays; the Kestrel cover was kept until the new cover started.",
                },
                CoolingOff: CoolingOffLine(insurer, start),
                ExistingCoverNote: "No existing cover was cancelled or reduced before the new cover was accepted and in force. Any change to the Kestrel or Riverbend cover is Priya's decision with Amelia."),
            ApplicationStatus: $"ACCEPTED — POLICY {policy} IN FORCE FROM {start.ToUpperInvariant()}",
            Notes: new[]
            {
                "Super-linked structure: life and any-occupation TPD inside super (premiums funded by a partial rollover from Riverbend Super, so the Riverbend account and its cover stay open); own-occupation TPD extension, trauma and income protection outside super.",
                "Lodged 1 business day after the Authority to Proceed (Life Advice Guru's standard: within 2 business days). Corella booked the tele-interview the next business day and decided 4 business days after it — within its 5-business-day standard.",
                "Policy issued; handed to Client Communications for the welcome letter, the plain-English policy summary and an outcome-only update to Hannah Lewis.",
            },
            HumanApprovalRequired: true,
            HumanApprovalReason: "Yes — accepting an exclusion is the client's decision with the adviser (Priya accepted with Amelia on Tue 17 Nov). Replacing or cancelling existing cover is also a human decision; nothing was cancelled before the new cover started.");
    }

    // ── Scenario 3 — George and Harpreet: business protection with Northbank ─
    private static UnderwritingPlan George(CaseRecord c)
    {
        var insurer = InsurerDirectory.NorthbankLife;
        const string george = "George Papadopoulos";
        const string harpreet = "Harpreet Kaur";
        const string company = "Northcote Custom Joinery Pty Ltd";
        var start = D(11, 20);

        var georgeLife = new LifeInsuredProfile(george, "CL-104611", 52, "19/04/1974",
            $"Master joiner and co-founder (50%), {company}", null,
            LifeSumInsured: 1_600_000m, TpdSumInsured: 1_600_000m, TraumaSumInsured: 0m,
            IncomeProtectionMonthly: 0m, BusinessCover: "buy-sell and key person", SelfEmployed: false);
        var harpreetLife = new LifeInsuredProfile(harpreet, "CL-104612", 47, "08/09/1979",
            $"Operations and finance director (50%), {company}", null,
            LifeSumInsured: 1_200_000m, TpdSumInsured: 1_200_000m, TraumaSumInsured: 0m,
            IncomeProtectionMonthly: 0m, BusinessCover: "buy-sell", SelfEmployed: false);

        const string loading = "+25% loading on life and TPD (Northbank reviews it in two years)";
        var covers = new[]
        {
            new UnderwritingCover(george, "Buy-sell — life and TPD", "$1,200,000", "Self-owned — George owns the policy on his own life", "Outside super",
                4_140m, 5_175m, "George", loading, "warn", "NB-26300418"),
            new UnderwritingCover(harpreet, "Buy-sell — life and TPD", "$1,200,000", "Self-owned — Harpreet owns the policy on her own life", "Outside super",
                3_100m, 3_100m, "Harpreet", "Standard rates", "ok", "NB-26300419"),
            new UnderwritingCover(george, "Key person — life and TPD (revenue purpose)", "$400,000", $"Company-owned — {company}", "Outside super",
                1_380m, 1_725m, "The company", loading, "warn", "NB-26300420"),
        };

        UnderwritingRequirement Req(string who, RequirementResult r)
        {
            (string Status, string Tone, string? Date, string? Outcome) state = r.Requirement switch
            {
                var x when x.StartsWith("Tele-interview", StringComparison.Ordinal)
                    => ("Complete", "ok", D(11, 13), "Answers read back and confirmed."),
                "Blood profile"
                    => ("Complete", "ok", D(11, 16), "Results received and reviewed by Northbank's underwriter."),
                "Medical examination"
                    => ("Complete", "ok", D(11, 13), "Completed through Northbank's medical panel."),
                var x when x.StartsWith("Financial evidence — key person", StringComparison.Ordinal)
                    => ("Accepted", "ok", D(11, 13), "Two years' company financial statements and Marcus Tan's estimate of the loss ($400,000 over 18 months) — accepted by Northbank's financial underwriting."),
                var x when x.StartsWith("Financial evidence", StringComparison.Ordinal)
                    => ("Accepted", "ok", D(11, 13), "Marcus Tan's valuation ($2,400,000 at 30 Jun 2026) and two years' company financial statements — accepted by Northbank's financial underwriting."),
                _ => ("Complete", "ok", null, null),
            };
            return new UnderwritingRequirement(who, r.Requirement, r.Category, r.Reason, state.Status, state.Tone, state.Date, state.Outcome);
        }

        var georgeCheck = InsurerDirectory.CheckRequirements(insurer, georgeLife.Age, georgeLife.LifeSumInsured,
            georgeLife.TpdSumInsured, 0m, 0m, georgeLife.BusinessCover, false, george);
        var harpreetCheck = InsurerDirectory.CheckRequirements(insurer, harpreetLife.Age, harpreetLife.LifeSumInsured,
            harpreetLife.TpdSumInsured, 0m, 0m, harpreetLife.BusinessCover, false, harpreet);
        var requirements = georgeCheck.Requirements.Select(r => Req(george, r))
            .Concat(harpreetCheck.Requirements.Select(r => Req(harpreet, r)))
            .Append(new UnderwritingRequirement(harpreet, "Medical examination", "Medical",
                "Age 47 — Northbank asks for a medical examination only from age 51 above $1,500,000 of life or TPD cover.",
                "Not required", "info", null, null))
            .ToList();

        var disclosures = new[]
        {
            new DisclosureItem("duty", "Duty to take reasonable care explained before the health questions", "done",
                $"Explained to George and Harpreet separately on {D(11, 12)}, before any health or lifestyle question, with the reminder that they don't need to disclose genetic test results. Both confirmed they understood. The company-owned key person policy may not be a consumer contract — Northbank's application explains the duty that applies to it."),
            new DisclosureItem("existing-cover", "Existing cover disclosed", "disclosed",
                "No existing buy-sell or key person cover — nothing is being replaced."),
            new DisclosureItem("occupation", "Occupations and duties", "disclosed",
                "George: master joiner and co-founder. Harpreet: operations and finance director. Both own 50% of Northcote Custom Joinery Pty Ltd (14 staff)."),
            new DisclosureItem("health", "Health and lifestyle questions", "disclosed",
                "Answered in full by each life insured at the tele-interview. Pathology and George's examination results went straight to Northbank's underwriter."),
            new DisclosureItem("financial", "Financial evidence", "disclosed",
                "Two years' company financials and the $2,400,000 valuation at 30 Jun 2026 — supplied by Marcus Tan (Bayside Business Accountants) with the clients' consent."),
            new DisclosureItem("ownership", "Ownership confirmed", "done",
                "Buy-sell: each partner owns the policy on their own life, backed by a buy-sell agreement their lawyer drafts. Key person: the company owns the policy; both directors sign for it."),
            new DisclosureItem("read-back", "Answers read back and confirmed", "done",
                $"Each tele-interview on {D(11, 13)} ended with the answers read back and confirmed."),
            new DisclosureItem("privacy", "Health information kept to the people who need it", "info",
                "Test results go only to Northbank and Life Advice Guru's advice file. Marcus Tan receives only the outcome, with consent."),
        };

        var timeline = new[]
        {
            new UnderwritingEvent(D(11, 9), "Referral from Marcus Tan (Bayside Business Accountants); Client Case opened.", "Olivia", "info", false),
            new UnderwritingEvent(D(11, 11), "Theo's second review (total cover $2,800,000); Amelia signs the Statement of Advice.", "Theo and Amelia", "ok", true),
            new UnderwritingEvent(D(11, 12), "George and Harpreet sign the Authority to Proceed; duty to take reasonable care explained; applications lodged with Northbank Life.", "Uma", "info", true),
            new UnderwritingEvent(D(11, 12), "Company financials and the valuation sent to Northbank by Marcus Tan, with consent.", "Marcus Tan", "info", false),
            new UnderwritingEvent(D(11, 13), "Tele-interviews; blood profiles collected; George's medical examination; financial evidence accepted.", "Northbank Life", "info", false),
            new UnderwritingEvent(D(11, 16), "Pathology results received by Northbank's underwriter.", "Northbank Life", "info", false),
            new UnderwritingEvent(D(11, 18), "Decision: Harpreet at standard rates; George +25% loading on life and TPD (review in two years).", "Northbank Life", "warn", false),
            new UnderwritingEvent(D(11, 19), "George accepts the loading with Amelia; the directors accept for the company-owned key person policy.", "George with Amelia", "ok", true),
            new UnderwritingEvent(start, "Policies NB-26300418, NB-26300419 and NB-26300420 in force; cooling-off period begins. Handed to Client Communications.", "Northbank Life", "ok", false),
        };

        var decisions = new[]
        {
            new UnderwritingDecisionItem(harpreet, "Buy-sell life and TPD $1,200,000", "Standard rates", "ok",
                "Accepted exactly as applied for — $3,100 a year."),
            new UnderwritingDecisionItem(george, "Buy-sell life and TPD $1,200,000", "+25% loading", "warn",
                "George's cholesterol (7.2 mmol/L) and blood pressure (150/95) mean a higher risk, so Northbank charges 25% more for his life and TPD cover: $5,175 a year instead of $4,140. The cover amount and terms are the same — no exclusions."),
            new UnderwritingDecisionItem(george, "Key person life and TPD $400,000", "+25% loading", "warn",
                "The same loading applies because it's George's life: $1,725 a year instead of $1,380, paid by the company."),
            new UnderwritingDecisionItem(george, "Loading review", "Reviewable in two years", "info",
                "Northbank will review the loading in two years if George asks, with new medical evidence. If his cholesterol and blood pressure improve, the loading can come down or come off."),
        };

        return new UnderwritingPlan(
            CaseNumber: c.CaseNumber,
            ScenarioKey: "george",
            Applicable: true,
            NotApplicableNote: null,
            Redirects: Array.Empty<AgentPointer>(),
            AsAtLabel: Label(ScenarioToday),
            OverallStatus: $"Completed — three Northbank policies in force from {start}",
            StatusTone: "ok",
            Headline: $"Northbank Life accepted Harpreet at standard rates and George with a 25% loading on life and TPD. George accepted with Amelia on {D(11, 19)}; buy-sell and key person cover started on {start}.",
            ReferredBy: "Marcus Tan, accountant, Bayside Business Accountants",
            LodgedOn: D(11, 12),
            Insurer: insurer,
            InsurerQuery: "Northbank Life business protection",
            LivesInsured: new[] { georgeLife, harpreetLife },
            Covers: covers,
            AnnualPremiumStandard: 8_620m,
            AnnualPremiumFinal: 10_000m,
            PremiumNote: "Quoted at standard rates: $8,620 a year (George buy-sell $4,140, key person $1,380, Harpreet $3,100). After George's loading: $10,000 a year — George buy-sell $5,175, key person $1,725 (paid by the company) and Harpreet $3,100.",
            Requirements: requirements,
            DutyOfCareScript: LifeAdviceFacts.DutyOfCareScript,
            DutyAcknowledgement: $"Acknowledged separately by George Papadopoulos and Harpreet Kaur on {D(11, 12)}, before the health questions.",
            Disclosures: disclosures,
            PdfDisclosures: new[]
            {
                $"Duty to take reasonable care explained to each life insured on {D(11, 12)}, before any health or lifestyle question.",
                "Existing cover: no existing buy-sell or key person cover — nothing is being replaced.",
                "Occupations: George — master joiner and co-founder; Harpreet — operations and finance director (50% each of Northcote Custom Joinery Pty Ltd).",
                "Health and lifestyle questions answered in full by each life insured; medical evidence went straight to Northbank's underwriter (details held by Northbank Life).",
                "Financial evidence: two years' company financials and the business valuation ($2,400,000 at 30 Jun 2026), supplied by the business's accountant with consent.",
                "Answers read back and confirmed.",
            },
            Timeline: timeline,
            Decisions: decisions,
            DecisionSummary: $"Northbank Life decided on {D(11, 18)}: Harpreet at standard rates; a 25% loading on George's life and TPD cover (buy-sell and key person), reviewable in two years. Premiums after loading: $10,000 a year.",
            ClientDecision: new ClientDecisionStep(
                Status: "Accepted by the clients",
                Tone: "ok",
                Who: "George Papadopoulos, with Amelia (and Harpreet as co-director for the key person policy)",
                Date: D(11, 19),
                Summary: "George accepted the 25% loading after Amelia explained what it means and what it costs; as directors, George and Harpreet accepted for the company-owned key person policy. Uma's agent prepared the explanation; it never accepts terms for a client.",
                Points: new[]
                {
                    "What a loading is: the same cover at a higher premium because of a higher assessed risk — no exclusions.",
                    "Premiums after loading: $10,000 a year in total (quoted $8,620 at standard rates).",
                    "Northbank reviews the loading in two years if George asks, with new medical evidence.",
                    "The buy-sell agreement is drafted and finalised by their lawyer — Life Advice Guru doesn't draft legal documents.",
                },
                CoolingOff: CoolingOffLine(insurer, start),
                ExistingCoverNote: "No existing cover is being replaced — these are new buy-sell and key person policies."),
            ApplicationStatus: $"ACCEPTED — POLICIES NB-26300418, NB-26300419 AND NB-26300420 IN FORCE FROM {start.ToUpperInvariant()}",
            Notes: new[]
            {
                "Total cover on the case is $2,800,000, so Theo's second review was completed before Amelia signed the SoA and before the applications went to underwriting.",
                "Lodged the same day as the Authority to Proceed (Life Advice Guru's standard: within 2 business days). Northbank's financial underwriting decided the day after the evidence arrived, and the medical decision came 2 business days after the last result — both within its 5-business-day standards.",
                "Policies issued together; handed to Client Communications for letters to George and Harpreet and an outcome-only update to Marcus Tan (their lawyer will finalise the buy-sell agreement).",
            },
            HumanApprovalRequired: true,
            HumanApprovalReason: "Yes — accepting a loading is the client's decision with the adviser (George accepted with Amelia on Thu 19 Nov). The $2,800,000 case also had Theo's second review before the SoA was signed.");
    }

    // ── Claim cases — nothing to underwrite ────────────────────────────────
    private static UnderwritingPlan NotApplicable(CaseRecord c, string key)
    {
        var claims = new AgentPointer("Open the case in Claims", $"/agents/claims?case={Uri.EscapeDataString(c.CaseNumber)}");
        var (note, redirects) = key switch
        {
            "grace" => (
                "Grace Williams's case is a claim request (a trauma claim and a bank-detail change) on her existing Lyrebird Life policy — there's no new application, so there's nothing for Underwriting to do. Ben's Claims Agent and Felix's Financial Crime Agent are looking after it.",
                new[] { claims, new AgentPointer("Open the case in Financial Crime", $"/agents/financial-crime?case={Uri.EscapeDataString(c.CaseNumber)}") }),
            "tom" => (
                "Tom Bradley's case is an income protection claim on his existing Harbourline Life policy — there's no new application, so there's nothing for Underwriting to do. Ben's Claims Agent is supporting Tom and Kylie through the claim.",
                new[] { claims }),
            "robert" => (
                "Robert Chen's case is a death claim on Mei Chen's Corella Life policy — there's nothing for Underwriting to do. Ben's Claims Agent is supporting Robert, with Theo overseeing every message to the family.",
                new[] { claims }),
            _ => (
                $"This case is a \"{c.RequestType}\" request, not a new application — there's nothing for Underwriting to do. The Claims Agent looks after claims on existing policies.",
                new[] { claims }),
        };

        return new UnderwritingPlan(
            CaseNumber: c.CaseNumber,
            ScenarioKey: key,
            Applicable: false,
            NotApplicableNote: note,
            Redirects: redirects,
            AsAtLabel: Label(ScenarioToday),
            OverallStatus: "Not an underwriting case",
            StatusTone: "info",
            Headline: note,
            ReferredBy: c.ReferredBy,
            LodgedOn: "Not applicable",
            Insurer: null,
            InsurerQuery: string.Empty,
            LivesInsured: Array.Empty<LifeInsuredProfile>(),
            Covers: Array.Empty<UnderwritingCover>(),
            AnnualPremiumStandard: 0m,
            AnnualPremiumFinal: 0m,
            PremiumNote: string.Empty,
            Requirements: Array.Empty<UnderwritingRequirement>(),
            DutyOfCareScript: LifeAdviceFacts.DutyOfCareScript,
            DutyAcknowledgement: "Not applicable — there's no application on this case.",
            Disclosures: Array.Empty<DisclosureItem>(),
            PdfDisclosures: Array.Empty<string>(),
            Timeline: Array.Empty<UnderwritingEvent>(),
            Decisions: Array.Empty<UnderwritingDecisionItem>(),
            DecisionSummary: string.Empty,
            ClientDecision: new ClientDecisionStep("Not applicable", "info", "—", null, "There's no underwriting decision for the client to make on this case.",
                Array.Empty<string>(), string.Empty, string.Empty),
            ApplicationStatus: "NOT AN UNDERWRITING CASE",
            Notes: Array.Empty<string>(),
            HumanApprovalRequired: false,
            HumanApprovalReason: "No — there's no application on this case.");
    }

    // ── Any other advice case — waiting for the signed SoA ─────────────────
    private static UnderwritingPlan GenericAdvice(CaseRecord c)
    {
        var received = DateOnly.TryParseExact(c.ReceivedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? Label(d)
            : c.ReceivedDate;

        return new UnderwritingPlan(
            CaseNumber: c.CaseNumber,
            ScenarioKey: "generic",
            Applicable: true,
            NotApplicableNote: null,
            Redirects: new[] { new AgentPointer("Open the case in Paraplanning", $"/agents/paraplanning?case={Uri.EscapeDataString(c.CaseNumber)}") },
            AsAtLabel: Label(DateOnly.FromDateTime(DateTime.Today)),
            OverallStatus: "Waiting for the Authority to Proceed",
            StatusTone: "warn",
            Headline: "Underwriting starts once Amelia has signed the Statement of Advice and the client has signed the Authority to Proceed — the recommended insurer, covers and sums insured come from the SoA.",
            ReferredBy: string.IsNullOrWhiteSpace(c.ReferredBy) ? "Existing client" : c.ReferredBy,
            LodgedOn: "Not lodged yet",
            Insurer: null,
            InsurerQuery: string.Empty,
            LivesInsured: Array.Empty<LifeInsuredProfile>(),
            Covers: Array.Empty<UnderwritingCover>(),
            AnnualPremiumStandard: 0m,
            AnnualPremiumFinal: 0m,
            PremiumNote: "Premiums come from the insurer quote in the signed SoA.",
            Requirements: Array.Empty<UnderwritingRequirement>(),
            DutyOfCareScript: LifeAdviceFacts.DutyOfCareScript,
            DutyAcknowledgement: "To be acknowledged by each life insured before the health questions.",
            Disclosures: new[]
            {
                new DisclosureItem("duty", "Duty to take reasonable care explained before the health questions", "action",
                    "To do when the application is prepared — before any health or lifestyle question."),
                new DisclosureItem("existing-cover", "Existing cover disclosed", "action",
                    "List every existing policy and super fund cover. Nothing is cancelled until the new cover is accepted and in force."),
            },
            PdfDisclosures: new[]
            {
                "Duty to take reasonable care: to be explained before any health or lifestyle question.",
                "Existing cover: to be listed in full; nothing is cancelled until the new cover is accepted and in force.",
            },
            Timeline: new[] { new UnderwritingEvent(received, "Request received; Client Case opened.", "Olivia", "info", false) },
            Decisions: Array.Empty<UnderwritingDecisionItem>(),
            DecisionSummary: "No application has been lodged yet.",
            ClientDecision: new ClientDecisionStep("Not started", "info", $"{c.ClientName}, with Amelia", null,
                "When the insurer decides, the client decides with Amelia whether to accept any loading or exclusion.",
                Array.Empty<string>(), LifeAdviceFacts.CoolingOffSummary,
                "Existing cover stays in place until the new cover is accepted and in force."),
            ApplicationStatus: "NOT LODGED — WAITING FOR THE AUTHORITY TO PROCEED",
            Notes: new[] { "Prepare the application from the signed SoA; check the insurer's requirements for each life insured's age and sum insured." },
            HumanApprovalRequired: true,
            HumanApprovalReason: "Yes — the adviser signs the SoA and the client signs the Authority to Proceed before any application is lodged.");
    }
}
