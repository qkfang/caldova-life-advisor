using LifeAdviceGuru.App.Services;

namespace LifeAdviceGuru.App.Models;

/// <summary>
/// Verified rules and Life Advice Guru practice settings shared by the Paraplanning
/// and Underwriting flows. Regulatory figures come from the FY2026-27 fact
/// sheet (Corporations Act, Insurance Contracts Act, SIS, ITAA 1997 and the
/// ASIC life insurance commissions instrument); practice settings (the
/// adviser, the second-review threshold, Life Advice Guru's commission option) are
/// Life Advice Guru's own fictional policy.
/// </summary>
public static class LifeAdviceFacts
{
    /// <summary>Life Advice Guru's adviser — gives the advice and signs every SoA.</summary>
    public const string Adviser = "Amelia, Senior Risk Adviser";

    /// <summary>Life Advice Guru practice rule: total cover on one case at or above this gets a second review.</summary>
    public const decimal SecondReviewThreshold = 2_000_000m;

    /// <summary>LIF cap — upfront commission as a share of the first-year policy cost (ASIC Instrument 2017/510 s5(2)).</summary>
    public const decimal LifUpfrontCap = 0.60m;

    /// <summary>LIF cap — ongoing commission as a share of each later year's policy cost (s5(3)).</summary>
    public const decimal LifOngoingCap = 0.20m;

    /// <summary>Cooling-off period for a new risk insurance policy (Corporations Act s1019B).</summary>
    public const int CoolingOffDays = 14;

    /// <summary>Protecting Your Super — months of inactivity after which cover stops unless the account holder elects to keep it (SIS Act s68AAA).</summary>
    public const int ProtectingYourSuperMonths = 16;

    public const string CoolingOffSummary =
        "Cooling-off: the client has 14 days to cancel a new policy and get their premiums back, counted from when they receive confirmation " +
        "of the policy or the end of the 5th business day after it's issued, whichever is earlier. The right ends once a claim is made " +
        "(Corporations Act s1019B).";

    public const string DutyOfCareScript =
        "Before you answer the insurer's health and lifestyle questions: the law asks you to take reasonable care not to make a " +
        "misrepresentation. Answer every question honestly and fully, and ask if a question isn't clear. If an answer isn't right, " +
        "the insurer may be able to reduce the cover or change its terms — or, if it wouldn't have offered cover at all, cancel it " +
        "within three years. A fraudulent answer lets the insurer cancel the policy. You don't need to tell the insurer about any " +
        "genetic test results, and we will never ask for them.";
}

/// <summary>
/// One clause in a rule or product document — the unit the Paraplanning
/// Agent cites in its advice checklist.
/// </summary>
/// <param name="ClauseId">
/// Clause id cited by checklist items. Prefixes: <c>HLP-</c>, <c>CRP-</c>,
/// <c>LBP-</c>, <c>NBP-</c> (insurer PDS extracts), <c>FSG-</c> (Life Advice Guru's
/// FSG), <c>APL-</c> (approved product list), <c>ADV-</c> (advice rules),
/// <c>SUP-</c> (insurance inside super), <c>UW-</c> (underwriting),
/// <c>CLM-</c> (claims rules), <c>TAX-</c> (tax), <c>COM-</c> (commission)
/// and <c>MAT-</c> (authority matrix).
/// </param>
/// <param name="Title">Short clause heading.</param>
/// <param name="Text">Plain-English rule text.</param>
/// <param name="LegalReference">Underlying law or source (e.g. "Corporations Act s947D").</param>
public record RuleSection(string ClauseId, string Title, string Text, string LegalReference);

/// <summary>
/// An extract of one document in the <c>life_knowledge</c> index — an
/// insurer's PDS extract, a Life Advice Guru guide or a rule summary — titled exactly
/// as the knowledge base titles it, so the agent's citations and the page's
/// reference panel match.
/// </summary>
/// <param name="Id">Stable document id (e.g. <c>advice-rules</c>).</param>
/// <param name="Number">Position in the rules-and-products list (1–12); 13 for the authority matrix.</param>
/// <param name="Title">Exact knowledge-base title.</param>
/// <param name="DocumentType">"PDS extract", "Life Advice Guru guide", "Rule summary" or "Practice matrix".</param>
/// <param name="Issuer">Who issues the document.</param>
/// <param name="AppliesTo">What the rules apply to.</param>
/// <param name="EffectiveDate">Date the extract is current at.</param>
/// <param name="Summary">One-line description.</param>
/// <param name="Sections">The clauses, each with a citable clause id.</param>
/// <param name="Requirements">What must be on file before the rules can be applied.</param>
/// <param name="Restrictions">Limits and things that can't be done under these rules.</param>
public record ProductRuleDocument(
    string Id,
    int Number,
    string Title,
    string DocumentType,
    string Issuer,
    string AppliesTo,
    string EffectiveDate,
    string Summary,
    IReadOnlyList<RuleSection> Sections,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> Restrictions);

/// <summary>
/// Status of one line of the advice checklist. The UI shows <c>Pass</c> as a
/// green tick, <c>Fail</c> as a red cross and <c>Info</c> as an amber note (an
/// action, a caution or a human-approval step).
/// </summary>
public enum ChecklistStatus
{
    Pass,
    Fail,
    Info
}

/// <summary>
/// One strategy, ownership or rule check the Paraplanning Agent works
/// through for a Client Case, citing the clause it relies on.
/// </summary>
public record AdviceChecklistItem(
    string Id,
    string Category,
    string Label,
    ChecklistStatus Status,
    string Finding,
    string ClauseRef);

/// <summary>Where the draft advice is up to.</summary>
public enum AdviceStatus
{
    ReadyForAdviser,
    NeedMoreInfo,
    NotApplicable
}

/// <summary>One line of a needs calculation. <c>Kind</c> is "add", "less", "total", "note" or "result".</summary>
public record NeedsLine(string Label, string Amount, string Kind);

/// <summary>One needs calculation, with the working shown line by line.</summary>
public record NeedsCalculation(
    string Id,
    string Title,
    string LifeInsured,
    IReadOnlyList<NeedsLine> Lines,
    string Recommended,
    string Basis);

/// <summary>One section of the draft Statement of Advice outline.</summary>
public record SoaSection(string Id, string Title, IReadOnlyList<string> Points);

/// <summary>One premium line in the draft SoA.</summary>
public record PremiumLine(
    string LifeInsured,
    string Cover,
    string SumInsured,
    string Structure,
    string Premium,
    string PaidBy);

/// <summary>The commission disclosure in dollars, with the consent and clawback terms.</summary>
public record CommissionDisclosure(
    string Insurer,
    string PremiumBasis,
    string Upfront,
    string Ongoing,
    string Basis,
    string Consent,
    string Gst,
    string Clawback);

/// <summary>The adviser sign-off box — the draft isn't advice until the adviser signs.</summary>
public record AdviserSignOff(
    string Heading,
    string Adviser,
    string Status,
    string Statement,
    IReadOnlyList<string> History,
    IReadOnlyList<string> Approvals);

/// <summary>
/// Deterministic paraplanning report for one Client Case: the needs
/// analysis with the working, the strategy / ownership / rules checklist
/// with clause references, the draft SoA outline, premiums and commission in
/// dollars, and the adviser sign-off. Mirrors the trailing JSON block the
/// <see cref="LifeAdviceGuru.Agents.ParaplanningAgent"/> emits for the
/// checklist; claim cases carry a polite pointer to the Claims Agent instead.
/// </summary>
public record AdviceReport(
    string CaseNumber,
    string ScenarioKey,
    bool Applicable,
    string? NotApplicableNote,
    IReadOnlyList<AgentPointer> Redirects,
    AdviceStatus Status,
    string StatusLabel,
    string StatusReason,
    string Summary,
    IReadOnlyList<NeedsCalculation> Needs,
    string NeedsTotal,
    IReadOnlyList<AdviceChecklistItem> Items,
    IReadOnlyList<SoaSection> SoaOutline,
    IReadOnlyList<PremiumLine> Premiums,
    string PremiumNote,
    CommissionDisclosure? Commission,
    AdviserSignOff SignOff,
    IReadOnlyList<string> RuleDocumentIds,
    IReadOnlyList<string> MissingInformation,
    string PlainEnglishExplanation,
    string HumanApproval);

/// <summary>
/// Static catalogue of the <c>life_knowledge</c> rule and product documents
/// (titles exactly as indexed) and the per-case paraplanning reports for the
/// five scenarios:
/// - Priya (<c>REQ-20261103-LIF001</c>) — family protection; draft SoA ready
///   for Amelia; Kestrel cover kept under Protecting Your Super.
/// - George (<c>REQ-20261109-LIF003</c>) — buy-sell and key person; draft SoA
///   ready once Theo's second review ($2.8m) is done.
/// - Grace, Tom and Robert — claim cases, pointed to the Claims Agent.
/// </summary>
public static class ProductDocumentCatalog
{
    private const string DocHarbourline = "pds-harbourline";
    private const string DocCorella = "pds-corella";
    private const string DocLyrebird = "pds-lyrebird";
    private const string DocNorthbank = "pds-northbank";
    private const string DocFsg = "guru-fsg";
    private const string DocApl = "guru-apl";
    private const string DocAdvice = "advice-rules";
    private const string DocSuper = "insurance-in-super";
    private const string DocUnderwriting = "underwriting-duty";
    private const string DocClaims = "claims-rules";
    private const string DocTax = "tax-life-insurance";
    private const string DocCommission = "commission-rules";
    private const string DocMatrix = "authority-matrix";

    private const string RulesAsAt = "As at 23 Nov 2026 (FY2026-27)";
    private const string Compliance = "Life Advice Guru — compliance summary of the law";

    public static IReadOnlyList<ProductRuleDocument> All { get; } = BuildDocuments();

    public static ProductRuleDocument? FindById(string id) =>
        All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Find the document and section that define <paramref name="clauseId"/>.</summary>
    public static (ProductRuleDocument Document, RuleSection Section)? FindClause(string clauseId)
    {
        foreach (var doc in All)
        {
            var section = doc.Sections.FirstOrDefault(s =>
                string.Equals(s.ClauseId, clauseId, StringComparison.OrdinalIgnoreCase));
            if (section is not null) return (doc, section);
        }
        return null;
    }

    /// <summary>The rule documents applied to the case behind <paramref name="report"/>, in display order.</summary>
    public static IReadOnlyList<ProductRuleDocument> DocumentsFor(AdviceReport report) =>
        report.RuleDocumentIds
            .Select(FindById)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();

    /// <summary>Exact knowledge-base title of an APL insurer's PDS extract, or null.</summary>
    public static string? PdsTitleFor(string insurerName) =>
        All.FirstOrDefault(d => d.DocumentType == "PDS extract"
                                && d.Title.StartsWith(insurerName, StringComparison.OrdinalIgnoreCase))?.Title;

    /// <summary>Default badge text for a status.</summary>
    public static string DefaultLabel(AdviceStatus status) => status switch
    {
        AdviceStatus.ReadyForAdviser => "Draft SoA ready for Amelia",
        AdviceStatus.NotApplicable => "Not a paraplanning case",
        _ => "Need more information",
    };

    /// <summary>
    /// The paraplanning report for <paramref name="c"/>: the worked scenarios
    /// for Priya and George, a polite pointer to Claims for the claim cases,
    /// and a "need more information" report for any other advice request.
    /// </summary>
    public static AdviceReport BuildReport(CaseRecord c) => InsurerProductCatalog.ScenarioKey(c) switch
    {
        "priya" => PriyaReport(c),
        "george" => GeorgeReport(c),
        "grace" => ClaimReport(c, "grace"),
        "tom" => ClaimReport(c, "tom"),
        "robert" => ClaimReport(c, "robert"),
        _ => InsurerProductCatalog.IsAdviceRequest(c) ? GenericReport(c) : ClaimReport(c, "claim"),
    };

    private static AdviceChecklistItem Item(string id, string category, string label, ChecklistStatus status, string finding, string clauseRef) =>
        new(id, category, label, status, finding, clauseRef);

    private static RuleSection S(string id, string title, string text, string reference) => new(id, title, text, reference);

    // ── Rule and product documents ─────────────────────────────────────────

    private static IReadOnlyList<ProductRuleDocument> BuildDocuments() => new[]
    {
        new ProductRuleDocument(
            Id: DocHarbourline,
            Number: 1,
            Title: "Harbourline Life — Protection PDS extract",
            DocumentType: "PDS extract",
            Issuer: "Harbourline Life Limited (fictional insurer on Life Advice Guru's APL)",
            AppliesTo: "Harbourline Protection Plan (policy numbers HL-…)",
            EffectiveDate: "PDS extract — PDS dated 1 July 2026",
            Summary: "Life cover with a terminal illness benefit, TPD, trauma and income protection for individuals, and key person and buy-sell cover for businesses. No super-linking.",
            Sections: new[]
            {
                S("HLP-1", "Life cover",
                    "Pays the sum insured on death. Terminal illness benefit: the sum insured is paid early if two medical practitioners, one of them a specialist in the condition, certify that the life insured is likely to die within 24 months.",
                    "Harbourline PDS, rule 1"),
                S("HLP-2", "TPD cover",
                    "Any occupation: because of illness or injury, the life insured has been off work for 3 consecutive months and is unlikely ever to work again in any occupation suited to their education, training or experience. Own occupation (outside super only): unlikely ever to work again in their own occupation. When TPD is packaged with life cover, a TPD payment reduces the life cover by the same amount.",
                    "Harbourline PDS, rule 2"),
                S("HLP-3", "Trauma cover",
                    "A lump sum when the life insured is diagnosed with a listed condition, such as cancer, heart attack or stroke, or suffers a listed serious injury, confirmed by a specialist medical practitioner. Mental health conditions aren't covered.",
                    "Harbourline PDS, rule 3"),
                S("HLP-4", "Income protection",
                    "A monthly benefit while the life insured is totally disabled after the waiting period (30, 60 or 90 days), for a benefit period of 2 years, 5 years or to age 65; insured benefit at application up to 70% of annual pre-tax earnings, divided by 12. For policies issued from 1 Oct 2021, Harbourline pays the lesser of the insured monthly benefit and 70% of average monthly earnings in the 12 months before the disability — for a self-employed person, the net income the business generates from their personal exertion, shown in BAS, profit and loss statements or tax returns. Benefits accrue from the end of the waiting period, are paid monthly in arrears and increase each year with CPI. Example: insured $7,000 a month; average earnings $8,000 a month; 70% × $8,000 = $5,600 a month payable.",
                    "Harbourline PDS, rule 4; within APRA's IDII limits"),
                S("HLP-5", "Business protection",
                    "Key person cover (life and TPD on a key person, owned by the business) and buy-sell cover (life and TPD on each business owner).",
                    "Harbourline PDS, rule 5"),
                S("HLP-6", "Premiums",
                    "Stepped (rise with age) or level (start higher and rise more slowly). Sums insured increase each year with CPI unless the policy owner declines the increase.",
                    "Harbourline PDS, rule 6"),
                S("HLP-7", "Applying",
                    "The policy owner and life insured must take reasonable care not to make a misrepresentation. Harbourline won't ask for or use genetic test results.",
                    "Harbourline PDS, rule 7"),
                S("HLP-8", "Cooling-off",
                    "14 days from the earlier of receiving confirmation of the policy and the end of the fifth business day after it's issued; not available once a claim has been made.",
                    "Harbourline PDS, rule 8"),
                S("HLP-9", "Who is paid",
                    "Benefits are paid to the policy owner. Life cover is paid to the nominated beneficiaries if the policy owner has nominated them, otherwise to the policy owner's estate. Payments go only into an account in the payee's own name.",
                    "Harbourline PDS, rule 9"),
                S("HLP-10", "Claims",
                    "A named claims consultant within 2 business days of a claim. Harbourline subscribes to the Life Insurance Code of Practice.",
                    "Harbourline PDS, rule 10"),
                S("HLP-11", "Adviser remuneration",
                    "Commission of up to 60% of the first year's premium and up to 20% of later premiums (excluding GST), only with the client's informed consent. The SoA shows the amounts in dollars.",
                    "Harbourline PDS, rule 11"),
            },
            Requirements: new[]
            {
                "Claim form signed by the policy owner (or a representative with written authority) and certified ID of the claimant.",
                "Income protection: treating doctor's or surgeon's report and earnings evidence for the 12 months before the disability (BAS, profit and loss statement, tax returns or payslips).",
                "Trauma: specialist's report confirming the diagnosis against the policy definition.",
                "Bank account in the payee's own name.",
            },
            Restrictions: new[]
            {
                "Own-occupation TPD can't be held inside super, and Harbourline doesn't offer super-linking.",
                "Trauma cover doesn't cover mental health conditions and isn't available inside super.",
                "Income protection isn't agreed value — the benefit can be less than the insured monthly benefit when earnings have fallen.",
                "Benefits are never paid to a third party or into an account in another name.",
            }),

        new ProductRuleDocument(
            Id: DocCorella,
            Number: 2,
            Title: "Corella Life — Protection PDS extract",
            DocumentType: "PDS extract",
            Issuer: "Corella Life Limited (fictional insurer on Life Advice Guru's APL)",
            AppliesTo: "Corella Protection Plan and Corella Super Plan (policy numbers CR-…)",
            EffectiveDate: "PDS extract — PDS dated 1 July 2026",
            Summary: "Super-linking — life and any-occupation TPD inside Corella's super plan, with an own-occupation TPD extension, trauma and income protection outside super — plus exclusion reviews, nominated beneficiaries and key person and buy-sell cover.",
            Sections: new[]
            {
                S("CRP-1", "Life cover",
                    "Pays the sum insured on death. Terminal illness benefit: paid early if two medical practitioners, one of them a specialist in the condition, certify that the life insured is likely to die within 24 months.",
                    "Corella PDS, rule 1"),
                S("CRP-2", "TPD cover, any occupation",
                    "Inside or outside super: because of illness or injury, the life insured is unlikely ever to work again in any occupation suited to their education, training or experience.",
                    "Corella PDS, rule 2"),
                S("CRP-3", "Own-occupation TPD extension (outside super, super-linked)",
                    "Pays if the life insured is unlikely ever to work again in their own occupation, even if they could do other work. A TPD claim is assessed against the any-occupation cover inside super first; if that definition isn't met but the own-occupation definition is, the extension pays. The two parts are priced and linked together, the extension never pays more than the linked TPD sum insured, and its benefits are paid to the policy owner personally. Suits people whose work is physical or specialised, such as physiotherapists.",
                    "Corella PDS, rule 3"),
                S("CRP-4", "Cover inside super",
                    "The Corella Super Plan's trustee owns the cover. Premiums are deducted from the person's Corella Super Plan account, which can be funded by a rollover from another super fund. Super cover can only pay on events that match a condition of release, so own-occupation TPD and trauma are held outside super. Benefits from cover inside super are paid by the trustee under the plan's rules and any valid nomination.",
                    "Corella PDS, rule 4"),
                S("CRP-5", "Trauma cover (outside super)",
                    "A lump sum on diagnosis of a listed condition, such as cancer, heart attack or stroke, confirmed by a specialist medical practitioner. Mental health conditions aren't covered.",
                    "Corella PDS, rule 5"),
                S("CRP-6", "Income protection (outside super)",
                    "Waiting periods of 30, 60 or 90 days; benefit periods of 2 years, 5 years or to age 65; insured monthly benefit at application up to 70% of annual pre-tax earnings, divided by 12. Corella pays the lesser of the insured monthly benefit and 70% of average monthly earnings in the 12 months before the disability, monthly in arrears from the end of the waiting period.",
                    "Corella PDS, rule 6"),
                S("CRP-7", "Exclusion review",
                    "Where Corella has excluded a pre-existing condition from income protection (for example back and spine conditions), the policy owner can ask Corella to review the exclusion after two symptom-free years. If Corella removes it, cover for that condition applies from the review date.",
                    "Corella PDS, rule 7"),
                S("CRP-8", "Premiums",
                    "Stepped (rise with age) or level (start higher and rise more slowly). Sums insured increase each year with CPI unless the policy owner declines.",
                    "Corella PDS, rule 8"),
                S("CRP-9", "Underwriting",
                    "A tele-interview for every application. Corella may accept cover at standard rates, with a loading or an exclusion, or decline. The policy owner and life insured must take reasonable care not to make a misrepresentation; Corella won't ask for or use genetic test results.",
                    "Corella PDS, rule 9"),
                S("CRP-10", "Who is paid (outside super)",
                    "Benefits are paid to the policy owner. If the policy owner is the life insured and has nominated beneficiaries, life cover is paid directly to them in the shares nominated, not through the estate; without a nomination it's paid to the estate. Nominations can be changed in writing at any time.",
                    "Corella PDS, rule 10"),
                S("CRP-11", "Cooling-off",
                    "14 days from the earlier of receiving confirmation of the policy and the end of the fifth business day after it's issued; not available once a claim has been made.",
                    "Corella PDS, rule 11"),
                S("CRP-12", "Claims",
                    "A dedicated case manager for every death and terminal illness claim. Corella subscribes to the Life Insurance Code of Practice.",
                    "Corella PDS, rule 12"),
                S("CRP-13", "Adviser remuneration",
                    "Commission of up to 60% of the first year's premium and up to 20% of later premiums (excluding GST), only with the client's informed consent. The SoA shows the amounts in dollars.",
                    "Corella PDS, rule 13"),
                S("CRP-14", "Earlier policies",
                    "The life cover, terminal illness and beneficiary nomination terms are the same as in Corella's 2019 PDS.",
                    "Corella PDS, rule 14"),
            },
            Requirements: new[]
            {
                "Claim form and certified ID of the claimant.",
                "Death: certified copy of the death certificate, the policy number and the beneficiary nomination on Corella's records (or the trustee's records, inside super).",
                "Income protection: treating doctor's report and earnings evidence for the 12 months before the disability.",
                "Exclusion review: medical evidence of two symptom-free years.",
                "Bank account in the payee's own name.",
            },
            Restrictions: new[]
            {
                "Own-occupation TPD and trauma can't be held inside super.",
                "The own-occupation extension can't be bought on its own — it's linked to any-occupation TPD inside super, and never pays twice for the same disability.",
                "Income protection isn't offered on an agreed-value basis.",
                "Exclusions on the policy schedule apply until Corella removes them.",
            }),

        new ProductRuleDocument(
            Id: DocLyrebird,
            Number: 3,
            Title: "Lyrebird Life — Protection PDS extract",
            DocumentType: "PDS extract",
            Issuer: "Lyrebird Life Limited (fictional insurer on Life Advice Guru's APL)",
            AppliesTo: "Lyrebird Life Protection (policy numbers LB-…)",
            EffectiveDate: "PDS extract — PDS dated 1 July 2026",
            Summary: "Life cover with a terminal illness benefit, TPD, trauma and income protection for individuals and families — no business protection or super-linking — with Lyrebird's cancer definition and its controls on changes of details and payments.",
            Sections: new[]
            {
                S("LBP-1", "Life cover",
                    "Pays the sum insured on death, with a terminal illness benefit paid early if two medical practitioners, one a specialist in the condition, certify the life insured is likely to die within 24 months. Life cover is paid directly to the nominated beneficiaries, not through the estate.",
                    "Lyrebird PDS, rule 1"),
                S("LBP-2", "TPD cover",
                    "Any occupation (inside or outside super) or own occupation (outside super only).",
                    "Lyrebird PDS, rule 2"),
                S("LBP-3", "Trauma cover — cancer",
                    "A lump sum on diagnosis of a listed condition, such as cancer, heart attack, stroke or major head injury; mental health conditions aren't covered. Cancer means a malignant tumour, shown by the uncontrolled growth and spread of malignant cells and the invasion of tissue, confirmed by a specialist and supported by histopathology. A GP's letter or imaging alone isn't enough.",
                    "Lyrebird PDS, rule 3"),
                S("LBP-4", "Income protection",
                    "Waiting periods of 30, 60 or 90 days; benefit periods of 2 years, 5 years or to age 65. Lyrebird pays the lesser of the insured monthly benefit and 70% of average monthly earnings in the 12 months before the disability.",
                    "Lyrebird PDS, rule 4"),
                S("LBP-5", "Who is paid",
                    "Trauma, TPD and income protection benefits are paid to the policy owner, only into an account in the policy owner's name (or the nominated beneficiary's, for life cover) — never to third parties or company accounts unless the company owns the policy.",
                    "Lyrebird PDS, rule 5"),
                S("LBP-6", "Changes of contact or bank details",
                    "Each change is confirmed with a code sent to the mobile or email held before the change, or with a signed request and certified ID. If bank details changed in the 30 days before a benefit payment, Lyrebird calls the policy owner on the verified number before paying.",
                    "Lyrebird PDS, rule 6"),
                S("LBP-7", "Claims Integrity",
                    "Lyrebird's Claims Integrity team investigates suspected fraud and identity takeover. Advisers can alert the team about a policy.",
                    "Lyrebird PDS, rule 7"),
                S("LBP-8", "Premiums",
                    "Stepped (rise with age) or level (start higher and rise more slowly).",
                    "Lyrebird PDS, rule 8"),
                S("LBP-9", "Applying",
                    "The policy owner and life insured must take reasonable care not to make a misrepresentation. Lyrebird won't ask for or use genetic test results.",
                    "Lyrebird PDS, rule 9"),
                S("LBP-10", "Cooling-off",
                    "14 days from the earlier of receiving confirmation of the policy and the end of the fifth business day after it's issued; not available once a claim has been made.",
                    "Lyrebird PDS, rule 10"),
                S("LBP-11", "Claims",
                    "Lyrebird subscribes to the Life Insurance Code of Practice.",
                    "Lyrebird PDS, rule 11"),
                S("LBP-12", "Adviser remuneration",
                    "Commission of up to 60% of the first year's premium and up to 20% of later premiums (excluding GST), only with the client's informed consent.",
                    "Lyrebird PDS, rule 12"),
                S("LBP-13", "Earlier policies",
                    "The cancer definition, beneficiary nominations and payment rules are the same as in Lyrebird's 2019 PDS.",
                    "Lyrebird PDS, rule 13"),
            },
            Requirements: new[]
            {
                "Claim form signed by the policy owner and certified ID of the policy owner.",
                "Trauma (cancer): specialist's report confirming the diagnosis, and the histopathology report.",
                "Any change of contact or bank details confirmed through the verified mobile or email held before the change.",
                "Bank account in the policy owner's own name (or the nominated beneficiary's, for life cover).",
            },
            Restrictions: new[]
            {
                "No business protection and no super-linking.",
                "A cancer claim can't be assessed without a specialist's diagnosis supported by histopathology.",
                "Benefits are never paid to a third party or into an account in another name.",
            }),

        new ProductRuleDocument(
            Id: DocNorthbank,
            Number: 4,
            Title: "Northbank Life — Business Protection PDS extract",
            DocumentType: "PDS extract",
            Issuer: "Northbank Life Limited (fictional insurer on Life Advice Guru's APL)",
            AppliesTo: "Northbank Business Protection — buy-sell and key person cover (policy numbers NB-…)",
            EffectiveDate: "PDS extract — PDS dated 1 July 2026",
            Summary: "Buy-sell cover for business owners and key person cover for businesses, with fast financial underwriting.",
            Sections: new[]
            {
                S("NBP-1", "Buy-sell cover",
                    "Life and TPD cover (trauma optional) on each business owner, sized to that owner's share of the business value, so the remaining owners can buy the share and the departing owner or their family receives its value. Self-ownership: each owner owns the policy on their own life and the benefit is paid to them, their nominated beneficiaries or their estate, with the buy-sell agreement setting out how the share is transferred; or cross-ownership. Northbank requires a buy-sell agreement prepared by the owners' lawyer, and doesn't draft or review it. TPD on buy-sell and key person cover is assessed on an own-occupation basis: the life insured is unlikely ever to work in their own occupation again because of illness or injury. The buy-sell agreement's TPD trigger should use the same definition.",
                    "Northbank PDS, rule 1"),
                S("NBP-2", "Key person cover",
                    "Life and TPD cover (trauma optional) on a person whose death or disability would cost the business money. The business owns the policy and receives the benefit. The purpose is recorded at application: revenue (to replace lost profit while a replacement is found and trained) or capital (for example to repay business debt). The sum insured is supported by the accountant's estimate of the loss.",
                    "Northbank PDS, rule 2"),
                S("NBP-3", "Financial underwriting",
                    "Northbank accepts the business valuation and the last two years' financial statements prepared by the business's accountant as financial evidence, and usually decides within 5 business days.",
                    "Northbank PDS, rule 3"),
                S("NBP-4", "Medical underwriting",
                    "For total life or TPD cover on one life: a tele-interview for every application; a blood profile from age 46 above $1,000,000; a blood profile and medical examination from age 51 above $1,500,000.",
                    "Northbank PDS, rule 4"),
                S("NBP-5", "Underwriting outcomes",
                    "Standard rates, a loading (a percentage added to the premium, for example +25%), an exclusion, or a decline. Northbank reviews a loading or exclusion on request after two years if the life insured's health results improve.",
                    "Northbank PDS, rule 5"),
                S("NBP-6", "Premiums",
                    "Stepped (rise with age) or level (start higher and rise more slowly). The business pays key person premiums; under self-ownership each owner pays the premiums for their own buy-sell policy.",
                    "Northbank PDS, rule 6"),
                S("NBP-7", "Applying",
                    "The policy owner and life insured must take reasonable care not to make a misrepresentation. Northbank won't ask for or use genetic test results.",
                    "Northbank PDS, rule 7"),
                S("NBP-8", "Cooling-off",
                    "14 days from the earlier of receiving confirmation of the policy and the end of the fifth business day after it's issued; not available once a claim has been made.",
                    "Northbank PDS, rule 8"),
                S("NBP-9", "Tax",
                    "The tax treatment of premiums and benefits depends on who owns the policy and why it was taken out. Northbank recommends that the business confirm it with its accountant.",
                    "Northbank PDS, rule 9"),
                S("NBP-10", "Claims",
                    "Northbank subscribes to the Life Insurance Code of Practice.",
                    "Northbank PDS, rule 10"),
                S("NBP-11", "Adviser remuneration",
                    "Commission of up to 60% of the first year's premium and up to 20% of later premiums (excluding GST), only with the client's informed consent.",
                    "Northbank PDS, rule 11"),
            },
            Requirements: new[]
            {
                "Company extract showing the owners and their shareholdings.",
                "Business valuation from the accountant and the last two years' financial statements.",
                "Accountant's estimate of the loss (key person cover).",
                "Blood profile and medical examination where the thresholds apply.",
                "Buy-sell agreement prepared by the owners' lawyer.",
            },
            Restrictions: new[]
            {
                "Buy-sell sums insured can't exceed the owner's share of the business value supported by the financial evidence; key person sums insured can't exceed the loss estimated by the accountant.",
                "Northbank doesn't draft buy-sell agreements or give tax or legal advice.",
                "Business protection isn't available inside super.",
                "Benefits are paid only into an account in the policy owner's name (the business, for key person cover).",
            }),

        new ProductRuleDocument(
            Id: DocFsg,
            Number: 5,
            Title: "Life Advice Guru — Financial Services Guide and remuneration",
            DocumentType: "Life Advice Guru guide",
            Issuer: "Life Advice Guru Pty Ltd (fictional advice practice)",
            AppliesTo: "Every Life Advice Guru client",
            EffectiveDate: "FSG v2026.1 — dated 1 July 2026",
            Summary: "Who Life Advice Guru is, the services it provides, the documents clients receive, how Life Advice Guru and its people are paid, its associations, privacy and how to complain.",
            Sections: new[]
            {
                S("FSG-1", "Who we are",
                    "Life Advice Guru Pty Ltd, Guru House, Melbourne: a life insurance advice practice licensed for personal advice on, and dealing in, life risk insurance products. Life Advice Guru acts for its clients, not for the insurers.",
                    "Life Advice Guru FSG, item 1"),
                S("FSG-2", "Our adviser",
                    "Amelia, Senior Risk Adviser, gives all of Life Advice Guru's personal advice and signs every Statement of Advice. She meets the education standard, completes at least 40 hours of continuing professional development each year, follows the Financial Planners and Advisers Code of Ethics 2019 and is on ASIC's Financial Advisers Register.",
                    "Life Advice Guru FSG, item 2"),
                S("FSG-3", "What we do",
                    "Personal advice on life cover (with a terminal illness benefit), TPD, trauma, income protection and business protection (key person and buy-sell cover), including whether cover is held inside or outside super; applying for cover and supporting clients through underwriting; supporting clients and families through claims; annual reviews. No advice on investments, super investment options, tax or legal matters.",
                    "Life Advice Guru FSG, item 3"),
                S("FSG-4", "Documents you receive",
                    "This FSG (or the FSG information on our website); a Statement of Advice for personal advice, showing our remuneration, including commission, in dollars; a Record of Advice for further advice when your circumstances haven't changed significantly; and each insurer's PDS.",
                    "Life Advice Guru FSG, item 4"),
                S("FSG-5", "How Life Advice Guru is paid",
                    "No advice fee for protection-only advice. The insurer pays Life Advice Guru commission within the Life Insurance Framework caps: up to 60% of the first year's policy cost (premiums and fees, excluding GST) and up to 20% of the policy cost in each later year. Example: on a first-year premium of $3,180, up to $1,908 upfront and up to $636 a year ongoing.",
                    "Life Advice Guru FSG, item 5"),
                S("FSG-6", "Your consent to commission",
                    "Life Advice Guru only receives commission with your informed consent. Before you consent we tell you the insurer, the rate as a percentage of the policy cost, how often and for how long it's paid (including renewals), the services we provide (annual reviews and claims support), that the law requires your consent and that it can't be withdrawn. You consent in writing (or we keep a written record of verbal consent) and get a copy.",
                    "Life Advice Guru FSG, item 6"),
                S("FSG-7", "Clawback",
                    "If a policy is cancelled in its first year, the insurer recovers 100% of the upfront commission from Life Advice Guru; in its second year, 60%.",
                    "Life Advice Guru FSG, item 7"),
                S("FSG-8", "How our people are paid",
                    "Amelia and the team are paid salaries. Life Advice Guru doesn't pay or receive referral fees: referral partners such as Hannah Lewis (Yarra Home Loans) and Marcus Tan (Bayside Business Accountants) aren't paid for referrals.",
                    "Life Advice Guru FSG, item 8"),
                S("FSG-9", "Not independent",
                    "Because Life Advice Guru receives commission from insurers, we don't call ourselves independent, impartial or unbiased.",
                    "Life Advice Guru FSG, item 9"),
                S("FSG-10", "Associations",
                    "No insurer owns Life Advice Guru or has a financial interest in it. Life Advice Guru recommends cover from the insurers on its APL: Harbourline Life, Corella Life, Lyrebird Life and Northbank Life.",
                    "Life Advice Guru FSG, item 10"),
                S("FSG-11", "Privacy",
                    "Health information is sensitive: collected with consent, used only for your advice, application or claim, and shared only with the people who need it, such as the insurer. Referral partners are told only what you agree to, never your health details.",
                    "Life Advice Guru FSG, item 11"),
                S("FSG-12", "Complaints",
                    "Tell us first; we give a final written response within 30 calendar days. If you're not satisfied, you can take your complaint to the Australian Financial Complaints Authority (AFCA), a free, independent scheme.",
                    "Life Advice Guru FSG, item 12"),
                S("FSG-13", "Records",
                    "Records of your advice, including the best-interests steps, are kept for 7 years, and records of your commission consent for at least 5 years.",
                    "Life Advice Guru FSG, item 13"),
            },
            Requirements: new[]
            {
                "Date the FSG was given (or the client was pointed to the FSG information on Life Advice Guru's website).",
                "Signed privacy and health-information consent.",
                "Written commission consent (or a written record of verbal consent), with a copy given to the client.",
            },
            Restrictions: new[]
            {
                "No ongoing fee arrangements — there's no advice fee for protection-only advice.",
                "No advice on investments, super investment options, tax or legal matters; no buy-sell agreements drafted.",
                "AI agents support the team but never give advice; only Amelia does.",
            }),

        new ProductRuleDocument(
            Id: DocApl,
            Number: 6,
            Title: "Life Advice Guru — Approved product list and research method",
            DocumentType: "Life Advice Guru guide",
            Issuer: "Life Advice Guru Pty Ltd (fictional advice practice)",
            AppliesTo: "Every product recommendation",
            EffectiveDate: "APL 2026-27 — reviewed 1 July 2026",
            Summary: "The insurers and products Amelia can recommend, and the method Insurance Research uses to research and compare them for every Statement of Advice.",
            Sections: new[]
            {
                S("APL-1", "APL insurers for 2026-27",
                    "Harbourline Life (HL-): life, TPD (own occupation outside super only), trauma and income protection; key person and buy-sell; no super-linking. Corella Life (CR-): super-linking (life and any-occupation TPD inside super, own-occupation extension, trauma and income protection outside); key person and buy-sell. Lyrebird Life (LB-): life, TPD, trauma and income protection for individuals and families; no business protection. Northbank Life (NB-): buy-sell and key person cover with fast financial underwriting — always researched for business protection.",
                    "Life Advice Guru APL 2026-27"),
                S("APL-2", "How insurers get on the APL",
                    "Insurance Research and Theo review the APL every year against definitions, features and exclusions; premiums over time, stepped and level; underwriting service; claims philosophy and claims data, including APRA's claims statistics; Life Code subscription; and each product's target market determination.",
                    "Life Advice Guru APL 2026-27"),
                S("APL-3", "Research for each SoA",
                    "The reasonable investigation of products in the best interests duty: start from the needs analysis and the client's circumstances; compare the definitions that matter for this client; compare features and exclusions; compare premiums in dollars, year 1 and stepped against level over time; shortlist with reasons and a comparison workbook. Cheapest isn't best if the definition won't pay. Amelia decides what to recommend and records why in the SoA.",
                    "Life Advice Guru APL 2026-27; Corporations Act s961B(2)(e)"),
                S("APL-4", "Target market determinations",
                    "Insurers make a TMD for each product. Giving personal advice and arranging a product to implement it are excluded conduct, so the best interests duty governs Amelia's recommendation; Ivy still notes when a client sits outside a product's target market.",
                    "Corporations Act s994A, s994B, s994E(3)"),
                S("APL-5", "Off-APL products",
                    "If no APL product suits a client, Amelia can research a product off the APL with Theo's approval, or tell the client that Life Advice Guru can't help with that need.",
                    "Life Advice Guru APL 2026-27"),
            },
            Requirements: new[]
            {
                "Needs analysis, product comparison and premium workbook on the file.",
                "Reasons for the shortlisted product; Theo's approval for any product off the APL.",
            },
            Restrictions: new[]
            {
                "The APL doesn't limit the best interests duty.",
                "Agents compare and shortlist; they never recommend a product to the client.",
                "Premiums are indicative, at standard rates, until the insurer has underwritten the application.",
            }),

        new ProductRuleDocument(
            Id: DocAdvice,
            Number: 7,
            Title: "Advice rules — best interests duty, SoA and replacement advice",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Personal advice to retail clients on life risk insurance",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026",
            Summary: "The best interests duty and related obligations, what an SoA must contain and when, Records of Advice, replacement advice, hawking, design and distribution, and Life Advice Guru practice.",
            Sections: new[]
            {
                S("ADV-1", "Best interests duty and the safe harbour",
                    "The adviser must act in the client's best interests. Safe harbour: (a) identify the client's objectives, financial situation and needs disclosed through their instructions; (b) identify the subject matter of the advice and the relevant circumstances; (c) make reasonable inquiries where information is incomplete or inaccurate; (d) assess whether the adviser has the expertise, and decline if not; (e) if recommending a product, reasonably investigate the products and assess them; (f) base all judgements on the client's relevant circumstances; (g) take any other step reasonably regarded as in the client's best interests. Scaled advice is allowed.",
                    "Corporations Act s961B"),
                S("ADV-2", "Related obligations",
                    "Give advice only if it's reasonable to conclude it's appropriate for the client; warn the client if the advice is based on incomplete or inaccurate information; give the client's interests priority when there's a conflict. Contraventions can attract civil penalties.",
                    "Corporations Act s961G, s961H, s961J, s961K, s961Q"),
                S("ADV-3", "Statement of Advice",
                    "Personal advice needs an SoA; the small-investment exemption doesn't apply to life risk advice. Give it when the advice is given, or as soon as practicable afterwards and before any further connected service — in time-critical cases within 5 business days, or before the cooling-off period starts.",
                    "Corporations Act s946A, s946AA(1)(b)(iii), s946C"),
                S("ADV-4", "SoA content",
                    "The advice and its basis; the adviser's name and contact details; remuneration, including commission, stated in dollars; other interests and associations; any warning — with the detail the client would reasonably require, presented clearly, concisely and effectively. An SoA can't be combined with an FSG or a PDS.",
                    "Corporations Act s947B, s947C, s947E"),
                S("ADV-5", "Replacement advice",
                    "When the advice is to replace one product with another (for example cancel existing cover and take out new cover), the SoA must also set out the charges on disposing of the old product and acquiring the new one, any benefits lost (temporarily or otherwise) and any other significant consequences — or say there will or may be such charges, losses or consequences if they aren't known.",
                    "Corporations Act s947D"),
                S("ADV-6", "Record of Advice",
                    "For further advice when the client's relevant circumstances and the basis of the advice aren't significantly different, an RoA can be used instead of an SoA. It sets out the advice (or brief particulars and their basis) plus any s947D items, is kept for 7 years, and the client can ask for a copy.",
                    "Corporations Regs reg 7.7.10AE, reg 7.7.09; ASIC INFO 266; s942B(8)"),
                S("ADV-7", "Records",
                    "Keep records of the best-interests steps, the advice and why it's appropriate, and the actions taken to prioritise the client's interests, for 7 years.",
                    "Corporations Act s912G; ASIC Instrument 2024/508"),
                S("ADV-8", "Commission",
                    "Shown in dollars in the SoA, with the client's informed consent before it can be received.",
                    "Corporations Act s947B, s963BB"),
                S("ADV-9", "Hawking",
                    "Offering a product during unsolicited real-time contact is prohibited, but offers made in the course of personal advice by someone bound by the best interests duty are an exception, and contacting an existing client about a product they already hold isn't hawking. Life Advice Guru contacts a referred client only after the referral partner has passed on the client's consent.",
                    "Corporations Act s992A; ASIC RG 38"),
                S("ADV-10", "Design and distribution",
                    "Giving personal advice and arranging a product to implement it are excluded conduct; insurers still make a target market determination for each product.",
                    "Corporations Act s994A, s994B, s994E(3)"),
                S("ADV-11", "Life Advice Guru practice",
                    "Amelia signs every SoA and RoA. Existing cover is never cancelled until the new cover is in force, and replacing or cancelling cover is always the client's decision with Amelia. Advice with total cover of $2,000,000 or more on one case gets Theo's second review.",
                    "Life Advice Guru practice; Matrix — authority, approvals and escalation"),
                S("ADV-12", "Proposed changes — not law",
                    "The government's announced 'tranche 2' measures (a proposed client advice record, a new class of adviser and removing only the broadest safe-harbour step) aren't law at 23 Nov 2026. The current SoA, RoA and s961B rules apply.",
                    "Treasury announcements, 2025-26 (proposed)"),
            },
            Requirements: new[]
            {
                "Fact find: objectives, financial situation, needs and relevant circumstances.",
                "Needs analysis with the working shown, and the research comparison of products.",
                "Existing cover details, and the s947D disclosures for any replacement.",
                "Commission in dollars and the client's informed consent; SoA signed by Amelia and the client's Authority to Proceed.",
            },
            Restrictions: new[]
            {
                "AI agents never give personal advice, never sign or send an SoA, and never tell a client to replace or cancel cover.",
                "An RoA can't be used when the client's circumstances or the basis of the advice have changed significantly.",
                "Proposed tranche 2 changes must not be presented as law.",
            }),

        new ProductRuleDocument(
            Id: DocSuper,
            Number: 8,
            Title: "Insurance inside super — permitted cover, Protecting Your Super and opt-in rules",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Cover held through a super fund",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026",
            Summary: "Which cover can be held inside super, when cover on an inactive account stops, the opt-in rules, how super-linking works and the tax points that follow.",
            Sections: new[]
            {
                S("SUP-1", "Permitted cover",
                    "A super trustee may only provide insured benefits for events that match a condition of release: death, terminal medical condition, permanent incapacity and temporary incapacity. So new own-occupation TPD and trauma cover can't be held inside super; existing benefits for people who joined their fund before 1 Jul 2014 are preserved.",
                    "SIS Regs reg 4.07D"),
                S("SUP-2", "Terminal medical condition",
                    "Two registered medical practitioners, at least one a specialist in the relevant area, certify that the illness is likely to result in death within 24 months of the certification.",
                    "SIS Regs reg 6.01A"),
                S("SUP-3", "TPD inside super is any occupation",
                    "Two legally qualified medical practitioners certify that the person is unlikely ever to be gainfully employed in a capacity for which they're reasonably qualified because of education, experience or training.",
                    "ITAA 1997 s995-1"),
                S("SUP-4", "Protecting Your Super",
                    "A trustee must not provide insurance on a choice or MySuper product that has been inactive — no amount received — for 16 continuous months, unless the person has told the fund in writing that they want to keep the cover. The election carries over to a successor fund that gives equivalent rights. Example: Priya Natarajan's Kestrel Retail Super account has received nothing since 30 Jul 2025, so its $50,000 death cover stops on 30 Nov 2026 unless she elects to keep it.",
                    "SIS Act s68AAA (from 1 Jul 2019)"),
                S("SUP-5", "Opt-in rules",
                    "Insurance needs the person to opt in if they're under 25 and new to the fund, or if their account balance is under $6,000. People who already had cover usually keep it if their balance later falls below $6,000.",
                    "From 1 Apr 2020"),
                S("SUP-6", "Default cover",
                    "Default life and TPD cover in super usually starts from age 25; TPD cover in super usually ends at 65 and life cover at 70.",
                    "Moneysmart"),
                S("SUP-7", "Super-linking",
                    "One arrangement split between a policy owned by the super trustee and a policy owned personally: life and any-occupation TPD sit in the fund, and own-occupation TPD and trauma are held personally. The two are priced and linked together, claims are assessed against the any-occupation super cover first, and benefits from the personally owned part are paid to the individual.",
                    "Industry practice"),
                S("SUP-8", "Paying premiums from super",
                    "Premiums come out of the super account rather than the household budget, and the account can be topped up by contributions or a rollover from another fund. Premiums paid through super aren't personally tax-deductible.",
                    "ATO — income protection insurance"),
                S("SUP-9", "Who receives a super death benefit",
                    "The trustee pays it under the fund's rules and any valid nomination.",
                    "SIS Act"),
                S("SUP-10", "Tax on benefits from super",
                    "A death benefit lump sum to a death-benefits dependant (a spouse or former spouse, a child under 18, a person in an interdependency relationship, or another dependant) is tax-free; for a non-dependant the taxable component is taxed at up to 15% (taxed element) or 30% (untaxed element) plus the 2% Medicare levy. TPD lump sums are tax-free from age 60; below preservation age (60 for people born from 1 Jul 1964) the taxable component is taxed at up to 20% plus the Medicare levy.",
                    "ITAA 1997 s302-60, s302-195, s302-145, s301-10, s301-35"),
                S("SUP-11", "Commission",
                    "Commission on group super cover and default cover in super is conflicted remuneration, so Life Advice Guru never receives commission on default cover.",
                    "Corporations Act s963B(2)"),
            },
            Requirements: new[]
            {
                "Latest super statements showing the cover, the balance and the date of the last amount received.",
                "Written election to keep cover on an inactive account (Protecting Your Super).",
            },
            Restrictions: new[]
            {
                "No new own-occupation TPD or trauma cover inside super.",
                "Cover on an account that has received nothing for 16 continuous months stops unless the person has elected in writing to keep it.",
                "AI agents never tell a client to cancel or keep super cover — that's the client's decision with Amelia.",
            }),

        new ProductRuleDocument(
            Id: DocUnderwriting,
            Number: 9,
            Title: "Underwriting and the duty to take reasonable care",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Every application for life risk cover",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026",
            Summary: "What a client must do when applying, how an insurer can respond to a misrepresentation, the genetic information protection from 8 Oct 2026, cooling-off, and how Life Advice Guru handles underwriting outcomes.",
            Sections: new[]
            {
                S("UW-1", "Duty to take reasonable care",
                    "Before a consumer insurance contract is entered into, the insured must take reasonable care not to make a misrepresentation. Whether reasonable care was taken depends on the circumstances: the type of contract and its target market, the insurer's material, how clear and specific its questions were, whether an agent acted for the insured, and whether it's a new contract. Not answering, or an obviously incomplete or irrelevant answer, isn't by itself a misrepresentation; a fraudulent misrepresentation is always a breach. Cover owned by a business, such as key person cover, may not be a consumer contract — the insurer's application explains the duty that applies.",
                    "Insurance Contracts Act s20A, s20B (from 5 Oct 2021)"),
                S("UW-2", "Genetic information",
                    "From 8 Oct 2026, not disclosing protected genetic information isn't a misrepresentation for life insurance, and insurers can't ask for or use it in underwriting. Life Advice Guru's agents never ask for, record or use genetic test results, and tell clients they don't need to disclose them.",
                    "Insurance Contracts Act s20B(5A), s33H"),
                S("UW-3", "What a life insurer can do after a misrepresentation",
                    "No remedy if the insurer would have entered into the contract anyway. Fraud: the insurer may avoid the contract. Not fraudulent, and the insurer wouldn't have offered cover on any terms: it may avoid the contract, but only within 3 years. Otherwise it may reduce the sum insured to S × P ÷ Q — for death cover only within 3 years — or, for cover such as income protection, TPD or trauma, vary the terms. Variations take effect from the start of the contract.",
                    "Insurance Contracts Act s29"),
                S("UW-4", "Underwriting outcomes",
                    "Standard rates; a loading (a percentage added to the premium); an exclusion (a condition that isn't covered); or a decline. Accepting a loading, an exclusion or a decline is always the client's decision, made with Amelia. Insurers set their evidence requirements by age and sum insured — see 'Directory — insurers on the approved product list'.",
                    "Life Advice Guru practice"),
                S("UW-5", "Cooling-off",
                    "14 days, starting from the earlier of when the client receives confirmation of the policy and the end of the fifth business day after it's issued. Not available once a claim has been made. The client can give notice in any way the insurer permits.",
                    "Corporations Act s1019B; reg 7.9.64A"),
                S("UW-6", "Life Advice Guru practice",
                    "Uma's agent explains the duty before the first health question; the client answers every question themselves, carefully and completely; existing cover isn't cancelled until the new cover is in force; health information is shared only with the insurer and the people who need it.",
                    "Life Advice Guru practice"),
            },
            Requirements: new[]
            {
                "Application answered by the client after the duty to take reasonable care was explained; tele-interview record.",
                "Medical and financial evidence the insurer requires for the age and sum insured.",
                "Client's signed acceptance of any loading or exclusion.",
            },
            Restrictions: new[]
            {
                "AI agents never answer health questions for a client, never suggest what to leave out, never ask for genetic test results, and never accept a loading, an exclusion or a decline on a client's behalf.",
                "The 3-year limit in s29(3) doesn't apply to fraud.",
            }),

        new ProductRuleDocument(
            Id: DocClaims,
            Number: 10,
            Title: "Claims rules — Life Insurance Code of Practice and income protection benefits",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Claims on life risk policies",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026 (Life Code effective 1 Mar 2025)",
            Summary: "The Life Code standards for claims, how income protection benefits are worked out under APRA's measures, and how Life Advice Guru supports a claim.",
            Sections: new[]
            {
                S("CLM-1", "The Life Code",
                    "Issued by the Council of Australian Life Insurers; the current edition took effect on 1 Mar 2025. It covers life cover with terminal illness, TPD, trauma and income protection from life insurers, and doesn't bind advisers unless they adopt it. A review's proposals aren't in force.",
                    "Life Insurance Code of Practice cl 1.4, 1.7, 1.10"),
                S("CLM-2", "Claim decisions",
                    "Income-related claims: decide within 2 months of the Claim Received Date, or the end of the waiting period if later. Lump-sum claims (death, terminal illness, trauma, TPD): within 6 months. A written decision within 15 business days of receiving all the information.",
                    "Life Code cl 5.48–5.50"),
                S("CLM-3", "Keeping the claimant informed",
                    "Explain the Code, cover and process within 10 business days of a claim; updates at least every 20 business days; reply to information requests within 10 business days; pay income protection by the due date or within 5 business days of all information; suggest independent financial advice for accepted non-income claims of $25,000 or more, unless paid to a super trustee.",
                    "Life Code cl 5.5–5.7, 5.61, 5.63"),
                S("CLM-4", "Vulnerability",
                    "Insurers respond to vulnerability, including financial distress; recognise a support person in all reasonable ways; offer hardship options; and respond to requests for urgent access to benefits within 5 business days of the evidence.",
                    "Life Code cl 6.1, 6.13, 6.18, 6.25–6.27"),
                S("CLM-5", "Complaints",
                    "Final written response within 30 calendar days; a complaint about claim handling is a standard complaint.",
                    "Life Code cl 7.13; ASIC RG 271"),
                S("CLM-6", "APRA's income protection measures",
                    "No new agreed-value income protection from 31 Mar 2020; from 1 Oct 2021 benefits are based on income at the time of the claim, and all benefits combined can't exceed 90% of earnings for the first 6 months of a claim and 70% after that. The 5-year contract-term measure was suspended; there's no prescribed switch from own to any occupation.",
                    "APRA IDII measures (letter of 30 Sep 2020)"),
                S("CLM-7", "Worked example",
                    "Insured monthly benefit $7,000; average earnings in the 12 months before the disability $8,000 a month; the lesser of the insured benefit and 70% of earnings = $5,600 a month. The benefit is taxable income.",
                    "Life Code; APRA IDII measures"),
                S("CLM-8", "How Life Advice Guru supports a claim",
                    "Ben's agent checks the policy and evidence, builds the claim pack and calculates the expected benefit; Theo approves lodging every claim; Ben lodges and tracks the insurer; Cara keeps the client or family informed; vulnerable clients have one named contact.",
                    "Life Advice Guru practice"),
            },
            Requirements: new[]
            {
                "Claim form, certified ID, policy schedule and medical evidence that meets the definition.",
            },
            Restrictions: new[]
            {
                "The insurer decides every claim; agents never lodge a claim without human approval.",
            }),

        new ProductRuleDocument(
            Id: DocTax,
            Number: 11,
            Title: "Tax on life insurance — premiums and benefits",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Premiums and benefits, outside and inside super",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026",
            Summary: "The general tax treatment of life insurance premiums and benefits for individuals and businesses — general information only; Life Advice Guru doesn't give tax advice.",
            Sections: new[]
            {
                S("TAX-1", "Income protection outside super",
                    "Premiums are deductible where the cover protects salary and wages; benefits that replace salary or income are assessable, whether paid regularly or as a lump sum. Combined policies are apportioned. Premiums paid through super aren't personally deductible. Self-employed people confirm the treatment with their accountant.",
                    "ATO — income protection insurance"),
                S("TAX-2", "Life, TPD and trauma outside super",
                    "Premiums for life, trauma or critical care cover aren't deductible for individuals, and the proceeds are generally not taxable: the CGT exemptions are s118-300 for life policies (for example the original owner) and s118-37(1)(a)(ii) for compensation for an illness or injury to you or a relative. A terminal illness benefit is treated as a payment under a life policy.",
                    "ITAA 1997 s118-300, s118-37; TD 2007/4"),
                S("TAX-3", "Key person cover",
                    "Under the ATO's long-standing ruling IT 155, term cover for a revenue purpose (replacing lost revenue or profit) generally has deductible premiums and assessable proceeds; cover for a capital purpose has neither. Whether IT 155 still reflects the ATO's current view hasn't been confirmed, so Life Advice Guru always relies on the business's accountant.",
                    "IT 155; Carapark Holdings (1966)"),
                S("TAX-4", "Buy-sell cover",
                    "Premiums for cover that funds the buy-out of an owner are capital and not deductible. Where each owner insures their own life, the ATO has treated the death proceeds as capital and not assessable. The CGT exemption depends on who owns the policy, so the ownership structure matters, and transferring a policy for consideration can lose the exemption.",
                    "IT 155 para 9(b); ATO ID 2003/1189; ITAA 1997 s118-300"),
                S("TAX-5", "Benefits from super",
                    "A death benefit lump sum to a death-benefits dependant is tax-free; for a non-dependant the taxable component is taxed at up to 15% or 30% plus the 2% Medicare levy. TPD lump sums from super are tax-free from age 60.",
                    "ITAA 1997 s302-60, s302-195, s302-145, s301-10"),
            },
            Requirements: new[]
            {
                "Policy ownership and who pays the premiums; the purpose of business cover, confirmed by the accountant.",
            },
            Restrictions: new[]
            {
                "Agents state the general rules, never a client's personal tax position.",
                "IT 155's currency is unconfirmed — key person treatment is always confirmed by the accountant.",
            }),

        new ProductRuleDocument(
            Id: DocCommission,
            Number: 12,
            Title: "Commission rules — LIF caps, clawback and consent",
            DocumentType: "Rule summary",
            Issuer: Compliance,
            AppliesTo: "Commission on life risk insurance (not group or default super cover)",
            EffectiveDate: "Rule guide FY2026-27 v1.0 — current at 23 Nov 2026",
            Summary: "How life risk commission is capped and clawed back under the Life Insurance Framework, the informed-consent rule from 9 Jul 2025, and how Life Advice Guru shows commission in dollars.",
            Sections: new[]
            {
                S("COM-1", "When commission is allowed",
                    "Commission on life risk insurance isn't conflicted remuneration if the benefit ratio is the same every year (level) or it meets the benefit-ratio and clawback requirements — and either way the client's informed consent is required. Commission on group super policies and default cover in super is still conflicted remuneration.",
                    "Corporations Act s963B, s963BB"),
                S("COM-2", "Benefit ratio",
                    "The commission divided by the policy cost; the policy cost is the premiums plus fees and instalment fees.",
                    "Corporations Act s963B(3A)–(3B)"),
                S("COM-3", "Caps",
                    "Upfront: up to 60% of the policy cost in the first year for products issued from 1 Jan 2020. Ongoing: up to 20% of the policy cost in each later year. Client-initiated increases: up to 60% of the annualised increase, then 20%; CPI increases attract only the 20%.",
                    "ASIC Corporations (Life Insurance Commissions) Instrument 2017/510"),
                S("COM-4", "Level commission",
                    "There's no numeric cap; the condition is an identical benefit ratio every year. Never quote a level commission cap.",
                    "Corporations Act s963B(1)(b)"),
                S("COM-5", "Clawback",
                    "Cancelled, not continued or reduced in year 1: 100% of the first-year commission (pro-rated for reductions); in year 2: 60%; after 24 months: nothing. No clawback on death.",
                    "ASIC Instrument 2017/510 s6"),
                S("COM-6", "Worked examples",
                    "A $1,000 first-year premium allows up to $600 upfront; cancelled in year 2, 60% × $600 = $360 is clawed back.",
                    "ASIC Instrument 2017/510"),
                S("COM-7", "GST",
                    "The caps are tested on the commission excluding GST; where a commission includes GST, the GST isn't a benefit for conflicted-remuneration purposes.",
                    "ASIC RG 246"),
                S("COM-8", "Informed consent",
                    "For insurance issued or sold on or after 9 Jul 2025: before the client consents, disclose the insurer, the commission rate as a percentage of the policy cost, how often and for how long it's paid (including renewals), the services, that the law requires consent and that consent is irrevocable. Consent in writing (or a written record of verbal consent), a copy to the client, records kept at least 5 years. Consent covers disclosed renewals; there's no annual re-consent for life risk.",
                    "Corporations Act s963BB, s1101C(3); ASIC INFO 292"),
                S("COM-9", "Dates often misreported",
                    "The caps have applied since 1 Jan 2018 (60% from 1 Jan 2020) and consent since 9 Jul 2025. The instrument is due to sunset on 1 Oct 2027 unless it's remade.",
                    "Federal Register of Legislation"),
                S("COM-10", "Life Advice Guru practice",
                    "No advice fee for protection-only advice; commission in dollars in the SoA; written consent with the SoA. Example: Corella Life, first-year policy cost $3,180 — upfront 60% = $1,908; ongoing 20% = $636 a year from year 2, changing as the premium changes; amounts exclude GST.",
                    "Life Advice Guru practice; Corporations Act s947B"),
            },
            Requirements: new[]
            {
                "Commission disclosure in the SoA: insurer, rate, frequency and period, services, consent required and irrevocable.",
                "The client's written consent (or a written record of verbal consent) and a copy given to the client.",
            },
            Restrictions: new[]
            {
                "No commission on group super cover or default cover in super.",
                "Never quote a level commission cap; never let commission influence a comparison; never ask a client to pay a clawback.",
            }),

        new ProductRuleDocument(
            Id: DocMatrix,
            Number: 13,
            Title: "Matrix — authority, approvals and escalation",
            DocumentType: "Practice matrix",
            Issuer: "Life Advice Guru — Practice Team Leader",
            AppliesTo: "Every Client Case",
            EffectiveDate: "Current at 23 Nov 2026",
            Summary: "What the team can action and what always needs a human decision — agents gather facts, draft, check, calculate, compare and recommend; people approve.",
            Sections: new[]
            {
                S("MAT-1", "Every SoA and RoA",
                    "Amelia signs every Statement of Advice and Record of Advice.",
                    "Life Advice Guru matrix, item 1"),
                S("MAT-2", "Replacing or cancelling existing cover",
                    "The client decides with Amelia; the SoA sets out the replacement disclosures.",
                    "Life Advice Guru matrix, item 2"),
                S("MAT-3", "Underwriting terms",
                    "Accepting a loading, an exclusion or a decline at underwriting — the client decides with Amelia.",
                    "Life Advice Guru matrix, item 3"),
                S("MAT-4", "Claims",
                    "Lodging any claim, and every death or terminal illness claim — Theo approves through the Teams approval card before Ben lodges.",
                    "Life Advice Guru matrix, item 4"),
                S("MAT-5", "Financial crime",
                    "Blocking a request on financial-crime grounds, and referring suspected fraud to an insurer — Theo.",
                    "Life Advice Guru matrix, item 5"),
                S("MAT-6", "Ownership, beneficiaries and bank details",
                    "Any change — Theo, after Financial Crime has confirmed the request on the client's verified contact details.",
                    "Life Advice Guru matrix, item 6"),
                S("MAT-7", "Second review — $2,000,000 or more",
                    "Advice with total cover of $2,000,000 or more on one case gets Theo's second review.",
                    "Life Advice Guru matrix, item 7"),
                S("MAT-8", "Complaint responses",
                    "Theo approves every complaint response.",
                    "Life Advice Guru matrix, item 8"),
                S("MAT-9", "Sensitive communications",
                    "Any communication to a fraud victim, a seriously ill client or a bereaved family is approved by a person before it goes: Cara for her letters, and Theo for every message to a bereaved family.",
                    "Life Advice Guru matrix, item 9"),
                S("MAT-10", "Life Advice Guru service standards",
                    "Requests acknowledged within 1 business day; applications lodged within 2 business days of the Authority to Proceed; approved claims lodged the same or next business day; clients updated at least every 10 business days while anything is open; vulnerable clients have one named contact.",
                    "Life Advice Guru matrix — service standards"),
            },
            Requirements: Array.Empty<string>(),
            Restrictions: new[]
            {
                "Agents gather facts, draft, check, calculate, compare and recommend — people approve. Only Amelia gives advice.",
            }),
    };

    // ── Scenario 1 — Priya: family protection ──────────────────────────────

    private static AdviceReport PriyaReport(CaseRecord c) => new(
        CaseNumber: c.CaseNumber,
        ScenarioKey: "priya",
        Applicable: true,
        NotApplicableNote: null,
        Redirects: Array.Empty<AgentPointer>(),
        Status: AdviceStatus.ReadyForAdviser,
        StatusLabel: "Draft SoA ready for Amelia",
        StatusReason:
            "The needs analysis, strategy, ownership, research, premiums and commission are complete and every rule check passes or is noted. " +
            "One action is time-critical: Priya's Kestrel cover stops on 30 Nov 2026 unless she tells the fund in writing to keep it.",
        Summary:
            "For Amelia's review: life $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month with Corella Life — life and " +
            "any-occupation TPD inside super, the own-occupation TPD extension, trauma and income protection outside super. Year-1 premiums $3,180 " +
            "(stepped), $2,322 of it outside super ($193.50 a month). Keep Riverbend's cover, and keep Kestrel's until the new cover is in force.",
        Needs: new[]
        {
            new NeedsCalculation("life", "Life cover", "Priya Natarajan", new[]
            {
                new NeedsLine("Joint mortgage", "$680,000", "add"),
                new NeedsLine("Children's education (2 × $50,000)", "$100,000", "add"),
                new NeedsLine("Family income support ($40,000 × 5 years)", "$200,000", "add"),
                new NeedsLine("Final expenses and emergency fund", "$30,000", "add"),
                new NeedsLine("Total need", "$1,010,000", "total"),
                new NeedsLine("Less savings", "−$45,000", "less"),
                new NeedsLine("Less existing super death cover (Riverbend Super)", "−$210,000", "less"),
                new NeedsLine("Shortfall", "$755,000", "result"),
                new NeedsLine("Kestrel Retail Super's $50,000 isn't counted — it stops on 30 Nov 2026 unless Priya elects to keep it, and it's only kept until the new cover starts.", "", "note"),
            }, "$750,000", "Shortfall of $755,000, rounded to the nearest $50,000."),
            new NeedsCalculation("tpd", "TPD cover", "Priya Natarajan", new[]
            {
                new NeedsLine("Joint mortgage", "$680,000", "add"),
                new NeedsLine("Rehabilitation and home modifications", "$100,000", "add"),
                new NeedsLine("Emergency fund", "$30,000", "add"),
                new NeedsLine("Total need", "$810,000", "total"),
                new NeedsLine("Less existing super TPD cover (Riverbend Super)", "−$210,000", "less"),
                new NeedsLine("Shortfall", "$600,000", "result"),
            }, "$600,000", "Any-occupation TPD inside super, with an own-occupation extension outside super."),
            new NeedsCalculation("trauma", "Trauma cover", "Priya Natarajan", new[]
            {
                new NeedsLine("Six months' income (50% × $105,000)", "$52,500", "add"),
                new NeedsLine("Medical costs and Arjun's time off work", "$50,000", "add"),
                new NeedsLine("Debt reduction", "$50,000", "add"),
                new NeedsLine("Total need", "$152,500", "result"),
            }, "$150,000", "Total need of $152,500, rounded to the nearest $50,000."),
            new NeedsCalculation("ip", "Income protection", "Priya Natarajan", new[]
            {
                new NeedsLine("Salary", "$105,000 a year", "add"),
                new NeedsLine("Insurable share (Corella insures up to 70%)", "× 70%", "note"),
                new NeedsLine("Annual benefit", "$73,500 a year", "total"),
                new NeedsLine("Monthly benefit ($73,500 ÷ 12)", "$6,125 a month", "result"),
            }, "$6,125 a month", "70% of salary, divided by 12; 90-day waiting period; benefit period to age 65."),
        },
        NeedsTotal: "Recommended: life $750,000 · TPD $600,000 · trauma $150,000 · income protection $6,125 a month (90-day waiting period, benefit period to age 65).",
        Items: new[]
        {
            Item("scope", "Scope and facts", "Scope of advice agreed", ChecklistStatus.Pass,
                "Personal and family protection after the home purchase: life, TPD, trauma and income protection for Priya. Arjun's cover isn't part of this advice — he has default cover in his own super.",
                "ADV-1"),
            Item("fact-find", "Scope and facts", "Fact find complete and identity verified", ChecklistStatus.Pass,
                "Salary $105,000 + super, joint mortgage $680,000, savings $45,000, Asha (2) and a baby due March 2027, existing cover and health basics on file. Financial Crime: driver licence and payslip legitimate, KYC complete, no PEP or sanctions match.",
                "ADV-1"),
            Item("needs", "Needs", "Needs analysis shown step by step", ChecklistStatus.Pass,
                "Life $750,000, TPD $600,000, trauma $150,000 and income protection $6,125 a month — the working is in the needs analysis.",
                "ADV-1, ADV-2"),
            Item("budget", "Needs", "Premiums fit the budget", ChecklistStatus.Pass,
                "Outside super: $2,322 a year = $193.50 a month, within Priya's $300 a month budget. Inside super: $858 a year, paid from super.",
                "ADV-2"),
            Item("super-life-tpd", "Strategy and ownership", "Life and any-occupation TPD inside super", ChecklistStatus.Pass,
                "Death and permanent incapacity are permitted insured events in super, so life cover and any-occupation TPD can sit in Corella's super plan.",
                "SUP-1, CRP-4"),
            Item("own-occ", "Strategy and ownership", "Own-occupation TPD outside super, super-linked", ChecklistStatus.Pass,
                "Own-occupation TPD can't be new cover inside super, so Corella's own-occupation extension sits outside super, linked to the any-occupation TPD inside — important for a physiotherapist.",
                "SUP-1, SUP-7, CRP-3"),
            Item("outside-super", "Strategy and ownership", "Trauma and income protection outside super", ChecklistStatus.Pass,
                "Trauma can't be held in super. Income protection outside super keeps the premiums tax-deductible for Priya.",
                "SUP-1, TAX-1"),
            Item("premium-funding", "Strategy and ownership", "Inside-super premiums funded without losing cover", ChecklistStatus.Pass,
                "A partial rollover from Riverbend Super pays the $858 inside-super premium. The Riverbend account stays open, so its $210,000 death and TPD cover continues. Paying premiums from super reduces Priya's retirement savings — the SoA says so.",
                "CRP-4, SUP-8"),
            Item("pys-kestrel", "Existing cover", "Kestrel Retail Super cover — Protecting Your Super", ChecklistStatus.Info,
                "No contributions since 30 Jul 2025, so the $50,000 death cover stops after 16 months of inactivity — on 30 Nov 2026 — unless Priya elects in writing to keep it. Recommend she elects to keep it until the new Corella cover is in force. The opt-in rules don't apply (she's 34 and the balance is $8,200).",
                "SUP-4, SUP-5"),
            Item("replacement", "Existing cover", "Replacement analysis — nothing cancelled now", ChecklistStatus.Pass,
                "No cover is replaced or cancelled: Riverbend's $210,000 stays, and Kestrel's $50,000 stays until the new cover starts. If Priya later cancels the Kestrel cover, the SoA sets out what she'd lose and any charges — her decision with Amelia.",
                "ADV-5, MAT-2"),
            Item("research", "Research and premiums", "On the APL, with the research compared", ChecklistStatus.Pass,
                "Year-1 stepped premiums for the full package: Corella Life $3,180 (recommended — lowest cost and own-occupation TPD for physiotherapists), Harbourline Life $3,412, Lyrebird Life $3,690.",
                "APL-1, APL-3, ADV-1"),
            Item("stepped-level", "Research and premiums", "Stepped and level premiums explained", ChecklistStatus.Info,
                "Stepped premiums ($3,180 in year 1) rise with age; level premiums ($4,050 in year 1) start higher and rise more slowly. The draft recommends stepped to fit the budget now and shows the level option for comparison.",
                "CRP-8"),
            Item("commission", "Commission", "Commission in dollars, within the caps, with consent", ChecklistStatus.Pass,
                "No advice fee. Corella pays commission within the caps: upfront 60% × $3,180 = $1,908; ongoing 20% = $636 a year from year 2. Priya gives written informed consent first. The caps are tested on the commission excluding GST.",
                "COM-3, COM-7, COM-8, FSG-5"),
            Item("ip-tax", "Commission", "Income protection premiums deductible", ChecklistStatus.Info,
                "The $1,460 income protection premium, held outside super, is generally tax-deductible for Priya; the life, TPD and trauma premiums aren't. Benefits paid would be assessable income.",
                "TAX-1, TAX-2"),
            Item("duty-cooling", "Underwriting", "Duty to take reasonable care and cooling-off", ChecklistStatus.Info,
                "The SoA explains that Priya must take reasonable care answering Corella's questions (she has mild asthma and a 2023 back strain to disclose), and that she has a 14-day cooling-off period once the policy is issued.",
                "UW-1, UW-5"),
            Item("sign-off", "Approvals", "Adviser review and sign-off", ChecklistStatus.Info,
                "Amelia reviews and signs the SoA before anything is implemented. Lump-sum cover totals $1,500,000 — under $2,000,000 — so no second review is needed.",
                "MAT-1, MAT-7"),
        },
        SoaOutline: new[]
        {
            new SoaSection("scope", "1. Scope of advice", new[]
            {
                "Personal and family protection for Priya Natarajan after buying the family home: life, TPD, trauma and income protection.",
                "Not included: Arjun's cover (he has default cover in his own super), investment or super advice.",
                "Protection-only advice — no advice fee.",
            }),
            new SoaSection("situation", "2. Your situation and goals", new[]
            {
                "Priya, 34, physiotherapist, salary $105,000 + super; partner Arjun; daughter Asha (2); second baby due March 2027.",
                "Joint mortgage $680,000 on the Coburg home bought in October 2026; savings $45,000; premium budget up to $300 a month.",
                "Goal: if something happens to Priya, the family keeps the home and the children's future is looked after.",
            }),
            new SoaSection("recommendations", "3. Recommendations", new[]
            {
                "Life cover $750,000 (with terminal illness benefit) and any-occupation TPD $600,000 inside super, through the Corella Life super plan.",
                "Own-occupation TPD extension $600,000 outside super, linked to the TPD inside super.",
                "Trauma cover $150,000 and income protection $6,125 a month (90-day waiting period, benefit period to age 65) outside super.",
            }),
            new SoaSection("best-interests", "4. Why this is in your best interests", new[]
            {
                "Cover is based on the needs analysis, net of savings and existing cover (working attached).",
                "Own-occupation TPD protects Priya if she can't work as a physiotherapist, not just in any job.",
                "Putting life and any-occupation TPD inside super keeps the cash-flow cost within budget.",
            }),
            new SoaSection("research", "5. Insurer and research", new[]
            {
                "Corella Life recommended: $3,180 in year 1 (stepped) — lowest cost, and own-occupation TPD for physiotherapists.",
                "Compared: Harbourline Life $3,412 and Lyrebird Life $3,690 for the same cover.",
            }),
            new SoaSection("premiums", "6. Premiums", new[]
            {
                "Inside super: life $468 + TPD $390 = $858 a year, paid by a partial rollover from Riverbend Super.",
                "Outside super: own-occupation TPD extension $222 + trauma $640 + income protection $1,460 = $2,322 a year ($193.50 a month).",
                "Stepped premiums rise with age; the level option is $4,050 in year 1.",
            }),
            new SoaSection("commission", "7. Fees and commission", new[]
            {
                "No advice fee.",
                "Corella pays Life Advice Guru commission: $1,908 upfront (60% of the first-year premium) and $636 a year from year 2 (20%).",
                "Priya's written informed consent is needed before Life Advice Guru receives it; the consent can't be revoked.",
                "If the policy is cancelled in year 1, Corella claws back 100% of the upfront commission, or 60% in year 2 — Life Advice Guru doesn't pass this on.",
            }),
            new SoaSection("replacement", "8. Your existing cover (replacement analysis)", new[]
            {
                "Riverbend Super death and TPD $210,000 — keep it; it's counted in the needs analysis.",
                "Kestrel Retail Super death cover $50,000 — elect in writing to keep it before 30 Nov 2026, so it doesn't stop under Protecting Your Super, and keep it until the new Corella cover is in force.",
                "Nothing is cancelled now. Any later cancellation is Priya's decision with Amelia, with the cover and features she'd lose set out first.",
            }),
            new SoaSection("underwriting", "9. Applying, underwriting and cooling-off", new[]
            {
                "Answer Corella's questions with reasonable care — including the asthma and the 2023 back strain.",
                "Corella may offer standard terms, a loading or an exclusion; accepting any change is Priya's decision with Amelia.",
                "14-day cooling-off once the policy is issued.",
            }),
            new SoaSection("next-steps", "10. Next steps", new[]
            {
                "Amelia reviews and signs this SoA.",
                "Priya signs the Authority to Proceed and the commission consent.",
                "Uma's team lodges the application with Corella Life.",
            }),
        },
        Premiums: new[]
        {
            new PremiumLine("Priya Natarajan", "Life cover (with terminal illness benefit)", "$750,000", "Inside super", "$468", "Super plan (partial rollover from Riverbend Super)"),
            new PremiumLine("Priya Natarajan", "TPD — any occupation", "$600,000", "Inside super", "$390", "Super plan (partial rollover from Riverbend Super)"),
            new PremiumLine("Priya Natarajan", "TPD — own-occupation extension", "$600,000", "Outside super (super-linked)", "$222", "Priya"),
            new PremiumLine("Priya Natarajan", "Trauma cover", "$150,000", "Outside super", "$640", "Priya"),
            new PremiumLine("Priya Natarajan", "Income protection (90 days, to age 65)", "$6,125 a month", "Outside super", "$1,460", "Priya"),
        },
        PremiumNote: "Year-1 stepped premiums: $3,180 in total — $858 inside super and $2,322 outside super ($193.50 a month). Level option: $4,050 in year 1.",
        Commission: new CommissionDisclosure(
            Insurer: "Corella Life",
            PremiumBasis: "$3,180 first-year premium (stepped)",
            Upfront: "$1,908 — 60% of the first-year premium",
            Ongoing: "$636 a year from year 2 — 20% of each later year's premium, so it moves with the premium",
            Basis: "Within the LIF caps: upfront up to 60% of the first-year policy cost, ongoing up to 20%. No advice fee for protection-only advice.",
            Consent: "Priya's written informed consent before Life Advice Guru receives commission: the insurer, the rates as a percentage of the policy cost, how often and how long they're paid (including renewals), the services (annual reviews and claims support), that the law requires consent and that it can't be revoked.",
            Gst: "The caps are tested on the commission excluding GST.",
            Clawback: "Cancelled in year 1: 100% of the upfront commission ($1,908) is clawed back; in year 2: 60% ($1,144.80) — pro-rated if the cover is reduced instead; nothing after 24 months. Life Advice Guru doesn't pass clawbacks on to Priya."),
        SignOff: new AdviserSignOff(
            Heading: "Adviser sign-off required: Amelia",
            Adviser: LifeAdviceFacts.Adviser,
            Status: "Signed by Amelia on Fri 6 Nov 2026",
            Statement: "Drafted by the Paraplanning Agent for Penny and Amelia. It isn't advice until Amelia has reviewed it, made any changes she wants and signed the Statement of Advice.",
            History: new[]
            {
                "Thu 5 Nov 2026 — draft SoA prepared by the Paraplanning Agent; Penny checks the working.",
                "Fri 6 Nov 2026 — Amelia reviews and signs the SoA.",
                "Sat 7 Nov 2026 — Priya signs the Authority to Proceed; she gave her written commission consent with the SoA and has a copy.",
            },
            Approvals: new[]
            {
                "Amelia signs the SoA — every SoA (MAT-1).",
                "Keeping or cancelling the Kestrel cover is Priya's decision with Amelia (MAT-2).",
                "No second review — lump-sum cover totals $1,500,000, under $2,000,000 (MAT-7).",
            }),
        RuleDocumentIds: new[] { DocAdvice, DocSuper, DocCorella, DocCommission, DocFsg, DocTax, DocApl, DocUnderwriting, DocMatrix },
        MissingInformation: new[]
        {
            "Priya's written election to keep her Kestrel Retail Super cover before 30 Nov 2026 (SUP-4) — recommended in the SoA; Priya has since told Kestrel to keep it.",
        },
        PlainEnglishExplanation:
            "If Priya died, her family would need about $1,010,000 — mostly the mortgage, the children's education and income for the family. Her " +
            "savings and existing super cover already meet $255,000 of that, so the draft suggests $750,000 of life cover. It also covers her if she " +
            "can never work as a physio again ($600,000), has a serious illness ($150,000) or is off work for a while ($6,125 a month). The premiums " +
            "outside super are $193.50 a month, within her $300 budget. Amelia reviews this draft and gives the advice.",
        HumanApproval:
            "Yes — Amelia must review and sign the Statement of Advice before anything is implemented, and any decision to keep, replace or cancel " +
            "existing cover is Priya's, with Amelia (MAT-1, MAT-2).");

    // ── Scenario 3 — George and Harpreet: business protection ──────────────

    private static AdviceReport GeorgeReport(CaseRecord c) => new(
        CaseNumber: c.CaseNumber,
        ScenarioKey: "george",
        Applicable: true,
        NotApplicableNote: null,
        Redirects: Array.Empty<AgentPointer>(),
        Status: AdviceStatus.ReadyForAdviser,
        StatusLabel: "Draft SoA ready — second review first",
        StatusReason:
            "The buy-sell and key person needs, ownership, tax notes, research, premiums and commission are complete. Total cover on the case is " +
            "$2,800,000, so Theo's second review comes before Amelia signs.",
        Summary:
            "For Amelia's review, after Theo's second review: buy-sell life and TPD cover of $1,200,000 on each owner, self-owned and backed by a " +
            "buy-sell agreement their lawyer drafts, and $400,000 key person life and TPD cover on George owned by the company — all with Northbank " +
            "Life at $8,620 a year (year 1, standard rates).",
        Needs: new[]
        {
            new NeedsCalculation("buy-sell-george", "Buy-sell — George's share", "George Papadopoulos", new[]
            {
                new NeedsLine("Business valuation (Marcus Tan, 30 Jun 2026)", "$2,400,000", "add"),
                new NeedsLine("George's share", "× 50%", "note"),
                new NeedsLine("Buy-sell sum insured", "$1,200,000", "result"),
            }, "$1,200,000 life and TPD on George — owned by George", "50% × $2,400,000. Self-ownership: George owns the policy on his own life."),
            new NeedsCalculation("buy-sell-harpreet", "Buy-sell — Harpreet's share", "Harpreet Kaur", new[]
            {
                new NeedsLine("Business valuation (Marcus Tan, 30 Jun 2026)", "$2,400,000", "add"),
                new NeedsLine("Harpreet's share", "× 50%", "note"),
                new NeedsLine("Buy-sell sum insured", "$1,200,000", "result"),
            }, "$1,200,000 life and TPD on Harpreet — owned by Harpreet", "50% × $2,400,000. Self-ownership: Harpreet owns the policy on her own life."),
            new NeedsCalculation("key-person", "Key person — George", "George Papadopoulos", new[]
            {
                new NeedsLine("Company net profit (context)", "$520,000 a year", "note"),
                new NeedsLine("Profit lost over 18 months while a replacement is found and trained (Marcus Tan's estimate)", "$400,000", "add"),
                new NeedsLine("Key person sum insured", "$400,000", "result"),
            }, "$400,000 life and TPD on George — owned by Northcote Custom Joinery Pty Ltd", "Revenue purpose: replaces lost profit while George's skills are replaced."),
            new NeedsCalculation("total", "Total cover on the case", "George and Harpreet", new[]
            {
                new NeedsLine("Buy-sell — George", "$1,200,000", "add"),
                new NeedsLine("Buy-sell — Harpreet", "$1,200,000", "add"),
                new NeedsLine("Key person — George", "$400,000", "add"),
                new NeedsLine("Total cover", "$2,800,000", "result"),
            }, "Second review required", "$2,800,000 is $2,000,000 or more, so Theo reviews the advice before Amelia signs."),
        },
        NeedsTotal: "Total cover on the case: $2,800,000 — Theo's second review before Amelia signs.",
        Items: new[]
        {
            Item("scope", "Scope and facts", "Scope of advice: business protection", ChecklistStatus.Pass,
                "Buy-sell and key person cover for the two 50% owners of Northcote Custom Joinery Pty Ltd (founded 2011, 14 staff, revenue $3.1m, net profit $520,000). Personal and family cover is outside this advice.",
                "ADV-1"),
            Item("kyc", "Scope and facts", "KYC on the company and both owners", ChecklistStatus.Pass,
                "Financial Crime: company extract and both IDs legitimate; no PEP or sanctions match for the company or its beneficial owners.",
                "ADV-1"),
            Item("valuation", "Needs", "Business valuation basis", ChecklistStatus.Pass,
                "Marcus Tan's valuation of $2,400,000 at 30 Jun 2026; each owner holds 50%.",
                "NBP-1, NBP-3"),
            Item("buy-sell", "Strategy and ownership", "Buy-sell: $1,200,000 life and TPD on each owner, self-owned", ChecklistStatus.Pass,
                "50% of $2,400,000 each. Self-ownership: each partner owns the policy on their own life, so the proceeds go to the owner whose share is being bought out.",
                "NBP-1, TAX-4"),
            Item("agreement", "Strategy and ownership", "Buy-sell agreement drafted by their lawyer", ChecklistStatus.Info,
                "Life Advice Guru doesn't draft legal documents. Their lawyer drafts the buy-sell agreement setting out how the shares change hands on death or TPD; the policies fund it.",
                "NBP-1"),
            Item("key-person", "Strategy and ownership", "Key person: $400,000 life and TPD on George, company-owned", ChecklistStatus.Pass,
                "Marcus estimates losing George would cost about $400,000 of profit over 18 months while a replacement is found and trained — a revenue purpose. Northcote Custom Joinery Pty Ltd owns the policy and pays the premium.",
                "NBP-2"),
            Item("tax", "Strategy and ownership", "Tax treatment — confirmed by the accountant", ChecklistStatus.Info,
                "Key person cover for a revenue purpose: premiums generally deductible and proceeds assessable (IT 155). Self-owned buy-sell: premiums not deductible; death proceeds capital and not assessable. Marcus Tan confirmed the treatment as their accountant — Life Advice Guru doesn't give tax advice.",
                "TAX-3, TAX-4, NBP-9"),
            Item("research", "Research and premiums", "On the APL, with the research compared", ChecklistStatus.Pass,
                "Year-1 stepped premiums at standard rates for all three covers: Northbank Life $8,620 (recommended — business-protection features and fast financial underwriting), Corella Life $9,180, Harbourline Life $9,410.",
                "APL-1, APL-3, ADV-1"),
            Item("premiums", "Research and premiums", "Premiums and who pays", ChecklistStatus.Pass,
                "George's buy-sell $4,140 (paid by George), key person $1,380 (paid by the company), Harpreet's buy-sell $3,100 (paid by Harpreet) = $8,620 a year.",
                "NBP-6"),
            Item("underwriting", "Underwriting", "Underwriting expectations set", ChecklistStatus.Info,
                "For their ages and sums insured: George (52) — blood profile, medical examination and financial evidence; Harpreet (47) — blood profile and financial evidence. Results could mean a loading or an exclusion — accepting it is the clients' decision with Amelia.",
                "NBP-4, NBP-5, UW-4"),
            Item("replacement", "Existing cover", "Replacement advice", ChecklistStatus.Pass,
                "No existing buy-sell or key person cover is being replaced, so the replacement disclosures don't apply.",
                "ADV-5"),
            Item("commission", "Commission", "Commission in dollars, within the caps, with consent", ChecklistStatus.Pass,
                "No advice fee. Northbank pays commission within the caps: upfront 60% × $8,620 = $5,172; ongoing 20% = $1,724 a year from year 2 (on the quoted premium — recalculated if underwriting changes it). George, Harpreet and the company give written informed consent first.",
                "COM-3, COM-8, FSG-5"),
            Item("second-review", "Approvals", "Second review — total cover $2,800,000", ChecklistStatus.Info,
                "Advice with total cover of $2,000,000 or more on one case gets Theo's second review before Amelia signs.",
                "MAT-7"),
            Item("sign-off", "Approvals", "Adviser review and sign-off", ChecklistStatus.Info,
                "Amelia reviews and signs the SoA after Theo's second review; nothing is implemented until the clients sign the Authority to Proceed.",
                "MAT-1"),
        },
        SoaOutline: new[]
        {
            new SoaSection("scope", "1. Scope of advice", new[]
            {
                "Business protection for George Papadopoulos and Harpreet Kaur, 50% owners of Northcote Custom Joinery Pty Ltd: buy-sell and key person cover.",
                "Not included: personal and family cover, legal documents (their lawyer drafts the buy-sell agreement) and tax advice (Marcus Tan, their accountant).",
                "Protection-only advice — no advice fee.",
            }),
            new SoaSection("situation", "2. Your business and goals", new[]
            {
                "Founded 2011; 14 staff; revenue $3.1m; net profit $520,000; valued at $2,400,000 (30 Jun 2026).",
                "Referred by Marcus Tan, Bayside Business Accountants, because there's no succession plan.",
                "Goal: if either owner dies or can never work again, the other can buy their share at a fair price and the business keeps going.",
            }),
            new SoaSection("recommendations", "3. Recommendations", new[]
            {
                "Buy-sell life and TPD cover of $1,200,000 on George and $1,200,000 on Harpreet, each owning the policy on their own life.",
                "Key person life and TPD cover of $400,000 on George, owned by the company for a revenue purpose.",
                "A buy-sell agreement drafted by their lawyer, funded by the buy-sell policies.",
            }),
            new SoaSection("ownership", "4. Ownership and tax", new[]
            {
                "Self-ownership keeps the buy-sell proceeds with the owner whose share is bought; the agreement sets out the share transfer.",
                "Key person (revenue purpose): premiums generally deductible to the company and proceeds assessable; buy-sell premiums aren't deductible — Marcus Tan confirmed the treatment as their accountant.",
            }),
            new SoaSection("research", "5. Insurer and research", new[]
            {
                "Northbank Life recommended: $8,620 a year at standard rates — business-protection features and fast financial underwriting.",
                "Compared: Corella Life $9,180 and Harbourline Life $9,410.",
            }),
            new SoaSection("premiums", "6. Premiums and who pays", new[]
            {
                "George's buy-sell $4,140 (George), key person $1,380 (the company), Harpreet's buy-sell $3,100 (Harpreet) = $8,620 a year, stepped.",
                "Underwriting may change the premiums — any changed terms are the clients' decision with Amelia.",
            }),
            new SoaSection("commission", "7. Fees and commission", new[]
            {
                "No advice fee.",
                "Northbank pays Life Advice Guru commission: $5,172 upfront (60% of the quoted first-year premium) and $1,724 a year from year 2 (20%), recalculated on the final premium.",
                "Written informed consent from George, Harpreet and the company before Life Advice Guru receives it.",
            }),
            new SoaSection("underwriting", "8. Applying and underwriting", new[]
            {
                "George (52): blood profile, medical examination and financial evidence. Harpreet (47): blood profile and financial evidence.",
                "Financial evidence: two years' company financials and the valuation, from Marcus Tan with consent.",
                "Answer the health questions with reasonable care; 14-day cooling-off once the policies are issued.",
            }),
            new SoaSection("second-review", "9. Second review", new[]
            {
                "Total cover on the case is $2,800,000 — $2,000,000 or more — so Theo, the Practice Team Leader, reviews the advice before Amelia signs.",
            }),
            new SoaSection("next-steps", "10. Next steps", new[]
            {
                "Theo's second review, then Amelia reviews and signs this SoA.",
                "George, Harpreet and the company sign the Authority to Proceed and the commission consent.",
                "Their lawyer drafts the buy-sell agreement; Uma's team lodges the applications with Northbank Life.",
            }),
        },
        Premiums: new[]
        {
            new PremiumLine("George Papadopoulos", "Buy-sell — life and TPD", "$1,200,000", "Self-owned (George)", "$4,140", "George"),
            new PremiumLine("Harpreet Kaur", "Buy-sell — life and TPD", "$1,200,000", "Self-owned (Harpreet)", "$3,100", "Harpreet"),
            new PremiumLine("George Papadopoulos", "Key person — life and TPD", "$400,000", "Company-owned", "$1,380", "Northcote Custom Joinery Pty Ltd"),
        },
        PremiumNote: "Northbank Life, year-1 stepped premiums at standard rates: $8,620 a year in total.",
        Commission: new CommissionDisclosure(
            Insurer: "Northbank Life",
            PremiumBasis: "$8,620 first-year premium at standard rates (quoted)",
            Upfront: "$5,172 — 60% of the first-year premium",
            Ongoing: "$1,724 a year from year 2 — 20% of each later year's premium",
            Basis: "Within the LIF caps. The dollar amounts are recalculated on the final premium if underwriting changes it; the rates don't change.",
            Consent: "Written informed consent from George, Harpreet and the company (as owner of the key person policy) before Life Advice Guru receives commission.",
            Gst: "The caps are tested on the commission excluding GST.",
            Clawback: "100% of the upfront commission is clawed back if a policy is cancelled in year 1, 60% in year 2, nothing after 24 months — not passed on to the clients."),
        SignOff: new AdviserSignOff(
            Heading: "Adviser sign-off required: Amelia",
            Adviser: LifeAdviceFacts.Adviser,
            Status: "Signed by Amelia on Wed 11 Nov 2026, after Theo's second review",
            Statement: "Drafted by the Paraplanning Agent for Penny and Amelia. It isn't advice until Theo has completed the second review and Amelia has reviewed and signed the Statement of Advice.",
            History: new[]
            {
                "Tue 10 Nov 2026 — draft SoA prepared by the Paraplanning Agent; Penny checks the working.",
                "Wed 11 Nov 2026 — Theo's second review (total cover $2,800,000); Amelia reviews and signs the SoA.",
                "Thu 12 Nov 2026 — George and Harpreet sign the Authority to Proceed; their written commission consent was given with the SoA.",
            },
            Approvals: new[]
            {
                "Theo's second review — total cover of $2,000,000 or more (MAT-7).",
                "Amelia signs the SoA (MAT-1).",
                "Their lawyer drafts the buy-sell agreement; Marcus Tan confirmed the tax treatment.",
            }),
        RuleDocumentIds: new[] { DocAdvice, DocNorthbank, DocTax, DocCommission, DocFsg, DocApl, DocUnderwriting, DocMatrix },
        MissingInformation: new[]
        {
            "The buy-sell agreement — drafted and finalised by their lawyer; the policies fund it, and Life Advice Guru doesn't draft it (NBP-1).",
        },
        PlainEnglishExplanation:
            "If George or Harpreet died or could never work again, the other would need $1.2 million to buy their half of the business at Marcus's " +
            "$2.4 million valuation — so each owns a $1.2 million policy on their own life, and their lawyer's buy-sell agreement says how the shares " +
            "change hands. The company also needs $400,000 to cover lost profit while it replaces George's skills. Because the total is $2.8 million, " +
            "Theo reviews the advice before Amelia signs it.",
        HumanApproval:
            "Yes — Theo's second review (total cover of $2,800,000 is $2,000,000 or more) and Amelia's signature on the SoA come before anything is " +
            "implemented (MAT-1, MAT-7).");

    // ── Claim cases — nothing for Paraplanning to draft ────────────────────

    private static AdviceReport ClaimReport(CaseRecord c, string key)
    {
        var claims = new AgentPointer("Open the case in Claims", $"/agents/claims?case={Uri.EscapeDataString(c.CaseNumber)}");
        var (note, redirects) = key switch
        {
            "grace" => (
                "Grace Williams's case is a claim request on her existing Lyrebird Life policy, not a request for new advice — so there's nothing for Paraplanning to draft. Ben's Claims Agent and Felix's Financial Crime Agent are handling it.",
                new[] { claims, new AgentPointer("Open the case in Financial Crime", $"/agents/financial-crime?case={Uri.EscapeDataString(c.CaseNumber)}") }),
            "tom" => (
                "Tom Bradley's case is an income protection claim, not a request for new advice — Ben's Claims Agent is supporting Tom and Kylie. A note for Amelia (not advice): Tom's insured $7,000 a month is more than his recent earnings support, so at his next review she may consider whether the benefit should change. That's her decision.",
                new[] { claims }),
            "robert" => (
                "Robert Chen's case is a death claim on Mei Chen's Corella Life policy — there's nothing for Paraplanning to draft. Ben's Claims Agent is supporting Robert, with Theo overseeing every message to the family.",
                new[] { claims }),
            _ => (
                $"This is a \"{c.RequestType}\" request, not a request for new advice — the Claims Agent looks after claims on existing policies.",
                new[] { claims }),
        };

        return new AdviceReport(
            CaseNumber: c.CaseNumber,
            ScenarioKey: key,
            Applicable: false,
            NotApplicableNote: note,
            Redirects: redirects,
            Status: AdviceStatus.NotApplicable,
            StatusLabel: DefaultLabel(AdviceStatus.NotApplicable),
            StatusReason: note,
            Summary: note,
            Needs: Array.Empty<NeedsCalculation>(),
            NeedsTotal: string.Empty,
            Items: Array.Empty<AdviceChecklistItem>(),
            SoaOutline: Array.Empty<SoaSection>(),
            Premiums: Array.Empty<PremiumLine>(),
            PremiumNote: string.Empty,
            Commission: null,
            SignOff: new AdviserSignOff("No Statement of Advice on this case", LifeAdviceFacts.Adviser, "Not applicable",
                "There's no new advice to draft or sign on a claim case.", Array.Empty<string>(), Array.Empty<string>()),
            RuleDocumentIds: Array.Empty<string>(),
            MissingInformation: Array.Empty<string>(),
            PlainEnglishExplanation: note,
            HumanApproval: "No — there's no Statement of Advice on this case.");
    }

    // ── Any other advice request — the fact find comes first ───────────────

    private static AdviceReport GenericReport(CaseRecord c) => new(
        CaseNumber: c.CaseNumber,
        ScenarioKey: "generic",
        Applicable: true,
        NotApplicableNote: null,
        Redirects: new[] { new AgentPointer("Back to Onboarding", "/agents/onboarding") },
        Status: AdviceStatus.NeedMoreInfo,
        StatusLabel: DefaultLabel(AdviceStatus.NeedMoreInfo),
        StatusReason: "The fact find doesn't yet hold everything the needs analysis needs — debts, dependants, income, savings and existing cover.",
        Summary: "Complete the fact find, then the Paraplanning Agent works out the cover needed and drafts the SoA for Amelia.",
        Needs: Array.Empty<NeedsCalculation>(),
        NeedsTotal: "Needs analysis waiting for the completed fact find.",
        Items: new[]
        {
            Item("fact-find", "Scope and facts", "Fact find complete", ChecklistStatus.Fail,
                $"The request ({c.RequestType}) doesn't yet include debts, dependants, income, savings and existing cover.",
                "ADV-1"),
            Item("identity", "Scope and facts", "Identity verified", ChecklistStatus.Info,
                "Financial Crime verifies identity for every new client before advice is implemented.",
                "ADV-1"),
            Item("existing-cover", "Existing cover", "Existing cover listed", ChecklistStatus.Info,
                "List every policy and super fund cover; nothing is cancelled until new cover is in force.",
                "ADV-5, UW-6"),
            Item("sign-off", "Approvals", "Adviser review and sign-off", ChecklistStatus.Info,
                "Amelia signs every SoA; advice with total cover of $2,000,000 or more gets a second review first.",
                "MAT-1, MAT-7"),
        },
        SoaOutline: Array.Empty<SoaSection>(),
        Premiums: Array.Empty<PremiumLine>(),
        PremiumNote: string.Empty,
        Commission: null,
        SignOff: new AdviserSignOff("Adviser sign-off required: Amelia", LifeAdviceFacts.Adviser, "Not drafted yet",
            "The SoA is drafted once the fact find is complete; it isn't advice until Amelia signs it.", Array.Empty<string>(),
            new[] { "Amelia signs the SoA (MAT-1)." }),
        RuleDocumentIds: new[] { DocAdvice, DocSuper, DocCommission, DocMatrix },
        MissingInformation: new[]
        {
            "Debts, dependants, income and savings (ADV-1).",
            "Existing cover, inside and outside super (ADV-5).",
            "Health basics and occupation for underwriting (UW-1).",
        },
        PlainEnglishExplanation: "We need a few more details before we can work out how much cover would help — Olivia's team will be in touch.",
        HumanApproval: "Yes — Amelia signs every SoA (MAT-1).");
}
