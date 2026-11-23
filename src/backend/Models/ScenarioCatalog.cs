namespace CaldovaLife.App.Models;

/// <summary>
/// One step in a scenario's Client Case journey, owned by one AI agent.
/// </summary>
/// <param name="Stage">Lifecycle stage — Discover, Verify, Advise, Implement or Confirm — or "Oversight" for the Team Leader Office.</param>
public record ScenarioStep(
    int Number,
    string Title,
    string AgentName,
    string AgentIcon,
    string Summary,
    string Stage);

/// <summary>
/// Metadata and steps for one of the five Caldova Life Advice scenarios.
/// Rendered by <c>Components/Pages/Scenarios/Scenario.razor</c> and listed in
/// the NavMenu and on the agent console home page.
/// </summary>
/// <param name="Outcome">Short status, ending in the status emoji (e.g. "Completed ✅").</param>
/// <param name="OutcomeDetail">One-sentence outcome with the key figures.</param>
/// <param name="SampleId">The Onboarding sample (<see cref="RequestSampleCatalog"/>) that starts the case.</param>
/// <param name="KeyFacts">Headline facts and figures for the case.</param>
/// <param name="HumanApprovals">The decisions people make on this case.</param>
public record ScenarioDefinition(
    string Id,
    string Number,
    string Title,
    string ShortTitle,
    string Customer,
    string Persona,
    string Tagline,
    string CaseNumber,
    string Outcome,
    string OutcomeDetail,
    string SampleId,
    IReadOnlyList<string> KeyFacts,
    IReadOnlyList<string> HumanApprovals,
    IReadOnlyList<ScenarioStep> Steps,
    string Icon,
    string Accent);

/// <summary>
/// Static catalog of the five Caldova Life Advice scenarios. Mirrors
/// <c>docs/scenario-1..5</c>, the seeded cases in
/// <c>CaseStore.SeedDefaults</c> and the Caldova House frontend scenarios.
/// </summary>
public static class ScenarioCatalog
{
    private const string Onboarding = "Onboarding Agent";
    private const string Paraplanning = "Paraplanning Agent";
    private const string Research = "Insurance Research Agent";
    private const string FinCrime = "Financial Crime Agent";
    private const string Underwriting = "Underwriting Agent";
    private const string Claims = "Claims Agent";
    private const string Comms = "Client Communications Agent";
    private const string Leader = "Team Leader Agent";

    public static IReadOnlyList<ScenarioDefinition> All { get; } = new[]
    {
        // ── 1. Priya — new family protection advice ─────────────────────────
        new ScenarioDefinition(
            Id: "family-protection",
            Number: "1",
            Title: "New Family Protection Advice",
            ShortTitle: "Family protection",
            Customer: "Priya Natarajan",
            Persona: "Priya Natarajan — new client referred by Hannah Lewis (Yarra Home Loans)",
            Tagline: "A new home, a growing family — and cover that fits the budget.",
            CaseNumber: "REQ-20261103-LIF001",
            Outcome: "Completed ✅",
            OutcomeDetail: "Covered from 18 Nov 2026: life $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month, with a back exclusion on income protection; the old super cover was kept until the new cover started.",
            SampleId: "onboarding-priya",
            KeyFacts: new[]
            {
                "Priya Natarajan, 34, physiotherapist · CL-104582",
                "Joint mortgage $680,000 · daughter Asha (2), second baby due March 2027",
                "Salary $105,000 + super · savings $45,000 · premium budget up to $300 a month",
                "Riverbend Super death and TPD cover $210,000 (kept) · Kestrel Retail Super death cover $50,000 at risk on 30 Nov 2026",
                "Corella Life recommended: $3,180 a year (stepped) — $193.50 a month from cash flow",
                "Commission within the LIF caps: $1,908 upfront and $636 a year from year 2 — shown in dollars, with Priya's written consent",
                "Policy CR-26118204 in force from Wed 18 Nov 2026, with a 14-day cooling-off period",
            },
            HumanApprovals: new[]
            {
                "Amelia reviews and signs the Statement of Advice (Fri 6 Nov).",
                "Priya signs the Authority to Proceed and consents to the commission in writing (Sat 7 Nov).",
                "Keeping the Kestrel Retail Super cover is Priya's decision — no cover is cancelled or replaced without a person.",
                "Priya accepts the back exclusion on income protection with Amelia (Tue 17 Nov).",
            },
            Steps: new[]
            {
                new ScenarioStep(1, "Referral and fact find", Onboarding, "📥",
                    "Hannah Lewis refers Priya with her consent. Olivia's agent reads the email and the fact-find questionnaire, runs the in-good-order checklist and spots that Priya's old Kestrel Retail Super cover stops on 30 Nov 2026 unless she elects in writing to keep it.",
                    "Discover"),
                new ScenarioStep(2, "Identity and KYC", FinCrime, "🛡️",
                    "Verify Priya's driver licence and payslip, and screen for PEP and sanctions matches. KYC complete — nothing found.",
                    "Verify"),
                new ScenarioStep(3, "Needs analysis", Paraplanning, "🧮",
                    "Work out what the family needs, step by step: life $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month. Recommend Priya keeps her old super cover until the new cover starts.",
                    "Advise"),
                new ScenarioStep(4, "Compare the insurers", Research, "🔎",
                    "Compare the APL insurers on definitions and premiums: Corella Life $3,180 a year (stepped), with own-occupation TPD for physiotherapists, against Harbourline Life $3,412 and Lyrebird Life $3,690.",
                    "Advise"),
                new ScenarioStep(5, "Statement of Advice", Paraplanning, "🧮",
                    "Draft the SoA for Amelia: life and TPD inside super, own-occupation TPD, trauma and income protection outside super, $193.50 a month from cash flow and the commission in dollars. Amelia signs; Priya signs the Authority to Proceed.",
                    "Advise"),
                new ScenarioStep(6, "Underwriting", Underwriting, "🩺",
                    "Explain the duty to take reasonable care, lodge the Corella Life application and track the tele-interview. Standard rates, with a back and spine exclusion on income protection that Priya accepts with Amelia.",
                    "Implement"),
                new ScenarioStep(7, "Welcome and referral update", Comms, "✉️",
                    "Send Priya a welcome letter and a plain-English policy summary, and tell Hannah the cover is in place — no health details.",
                    "Confirm"),
            },
            Icon: "🏡",
            Accent: "#14b8a6"),

        // ── 2. Grace — identity takeover and a fake trauma claim ────────────
        new ScenarioDefinition(
            Id: "fraudulent-claim",
            Number: "2",
            Title: "Identity Takeover & Fake Trauma Claim — Blocked",
            ShortTitle: "Fraudulent claim blocked",
            Customer: "Grace Williams",
            Persona: "Grace Williams — existing client, travelling in Italy",
            Tagline: "An urgent $200,000 claim to a new account is stopped before it's lodged.",
            CaseNumber: "REQ-20261106-LIF002",
            Outcome: "Blocked 🛡️",
            OutcomeDetail: "No claim lodged; $200,000 protected; Grace's $500,000 life cover and $200,000 trauma cover are safe.",
            SampleId: "onboarding-grace",
            KeyFacts: new[]
            {
                "Grace Williams, 61 · client since 2019 · CL-100377",
                "Lyrebird Life LB-19004417: life cover $500,000 and trauma cover $200,000",
                "Travel notice on file: Italy, 20 Oct – 1 Dec 2026",
                "Mobile and email changed by phone Mon 2 Nov 10:41 · claim email Fri 6 Nov 07:52 — about 93 hours later",
                "Requested: the full $200,000 trauma benefit, paid to a new account ending 4471 in another name",
                "Documents: oncologist's report fake · driver licence tampered · bank statement suspicious",
            },
            HumanApprovals: new[]
            {
                "Theo approves the block, restores Grace's verified contact details and locks portal access.",
                "Theo approves alerting Lyrebird Life's fraud team.",
                "Every message to Grace is approved by a person — she is the target, not the suspect.",
            },
            Steps: new[]
            {
                new ScenarioStep(1, "Urgent claim email", Onboarding, "📥",
                    "An 'urgent' email from a new address asks for Grace's full $200,000 trauma benefit to be paid into a new account. Olivia's agent links it to the contact change four days earlier and opens the case as Urgent.",
                    "Discover"),
                new ScenarioStep(2, "Policy check", Claims, "🤝",
                    "Check the Lyrebird Life policy: a cancer claim needs a confirmed specialist diagnosis with histopathology, and benefits are paid to the policy owner — not a new account in another name. Refer to Financial Crime.",
                    "Implement"),
                new ScenarioStep(3, "Identity-takeover investigation", FinCrime, "🛡️",
                    "Join up the timeline and the documents: a fake oncologist's report, a tampered licence and a brand-new bank account in another name. Risk HIGH — each red flag explained, without accusing Grace.",
                    "Verify"),
                new ScenarioStep(4, "Approve the block", Leader, "🧭",
                    "Theo approves the block, restores Grace's verified contact details, locks portal access and approves alerting Lyrebird Life's fraud team.",
                    "Oversight"),
                new ScenarioStep(5, "Confirm nothing was lodged", Claims, "🤝",
                    "Confirm that no claim was lodged with Lyrebird Life and that no money moved.",
                    "Implement"),
                new ScenarioStep(6, "Call the real Grace", Comms, "✉️",
                    "Call Grace on her verified mobile. In Florence, she confirms she's well and never asked. Send a reassurance letter with identity-protection steps.",
                    "Confirm"),
            },
            Icon: "🛡️",
            Accent: "#ef4444"),

        // ── 3. George — business protection ─────────────────────────────────
        new ScenarioDefinition(
            Id: "business-protection",
            Number: "3",
            Title: "Business Protection — Buy-Sell and Key Person Cover",
            ShortTitle: "Business protection",
            Customer: "George Papadopoulos",
            Persona: "George Papadopoulos & Harpreet Kaur — business owners referred by Marcus Tan (Bayside Business Accountants)",
            Tagline: "Two business partners get a succession plan that pays out when it matters.",
            CaseNumber: "REQ-20261109-LIF003",
            Outcome: "Completed ✅",
            OutcomeDetail: "Buy-sell cover of $1.2 million each and $400,000 key person cover in force from 20 Nov 2026, with a 25% loading on George's cover.",
            SampleId: "onboarding-george",
            KeyFacts: new[]
            {
                "George Papadopoulos, 52 (CL-104611) and Harpreet Kaur, 47 (CL-104612) — 50/50 owners",
                "Northcote Custom Joinery Pty Ltd: founded 2011, 14 staff, revenue $3.1m, net profit $520,000",
                "Business valued at $2,400,000 at 30 Jun 2026 (Marcus Tan)",
                "Buy-sell: life and TPD $1,200,000 each, self-owned · key person: $400,000 on George, company-owned",
                "Total cover $2,800,000 — Theo's second review",
                "Northbank Life $8,620 a year (stepped) — $10,000 a year after George's 25% loading",
            },
            HumanApprovals: new[]
            {
                "Theo's second review — total cover of $2,000,000 or more on one case.",
                "Amelia reviews and signs the Statement of Advice.",
                "George accepts the 25% loading with Amelia (Thu 19 Nov).",
                "Marcus, as their accountant, confirms the tax treatment of each policy.",
                "Their lawyer drafts the buy-sell agreement — Caldova doesn't draft legal documents.",
            },
            Steps: new[]
            {
                new ScenarioStep(1, "Accountant's referral", Onboarding, "📥",
                    "Marcus Tan refers George and Harpreet with the business details form: 50/50 owners, 14 staff, a $2.4 million valuation — and no succession plan.",
                    "Discover"),
                new ScenarioStep(2, "KYC on the company and owners", FinCrime, "🛡️",
                    "Verify the company extract and both IDs and identify the beneficial owners. No PEP or sanctions match.",
                    "Verify"),
                new ScenarioStep(3, "Business needs analysis", Paraplanning, "🧮",
                    "Buy-sell life and TPD of $1.2 million each, owned by each partner on their own life, and $400,000 key person cover on George owned by the company — $2.8 million in total.",
                    "Advise"),
                new ScenarioStep(4, "Compare the insurers", Research, "🔎",
                    "Northbank Life $8,620 a year (stepped) for all three covers, with the strongest business-protection features, against Corella Life $9,180 and Harbourline Life $9,410.",
                    "Advise"),
                new ScenarioStep(5, "Second review", Leader, "🧭",
                    "Total cover of $2.8 million is over the $2 million threshold, so Theo reviews the advice before the application goes to the insurer.",
                    "Oversight"),
                new ScenarioStep(6, "Underwriting", Underwriting, "🩺",
                    "Blood tests, a medical exam and financial evidence. George's cholesterol and blood pressure mean a 25% loading on his life and TPD cover, which he accepts with Amelia. Policies in force from 20 Nov 2026.",
                    "Implement"),
                new ScenarioStep(7, "Letters and accountant update", Comms, "✉️",
                    "Write to George and Harpreet, and tell Marcus (with their consent) that the cover is in place and their lawyer will finalise the buy-sell agreement.",
                    "Confirm"),
            },
            Icon: "💼",
            Accent: "#0ea5e9"),

        // ── 4. Tom — income protection claim ────────────────────────────────
        new ScenarioDefinition(
            Id: "income-protection-claim",
            Number: "4",
            Title: "Income Protection Claim After an Injury",
            ShortTitle: "Income protection claim",
            Customer: "Tom Bradley",
            Persona: "Tom Bradley — self-employed electrician, with his wife Kylie as support person",
            Tagline: "An injured tradie's claim is paid — with a clear reason it's $5,600, not $7,000.",
            CaseNumber: "REQ-20261111-LIF004",
            Outcome: "Partially approved 🟡",
            OutcomeDetail: "Claim accepted at $5,600 a month, not the $7,000 insured, because the benefit follows his earnings in the 12 months before the injury.",
            SampleId: "onboarding-tom",
            KeyFacts: new[]
            {
                "Tom Bradley, 41, self-employed electrician · CL-102265",
                "Harbourline Life income protection HL-23051877: $7,000 a month insured, 30-day waiting period, to age 65",
                "Injured Wed 14 Oct 2026 — torn rotator cuff, surgery 21 Oct, four to six months off work",
                "Net business income $96,000 in the 12 months to 30 Sep 2026 — $8,000 a month",
                "Benefit $5,600 a month from 13 Nov 2026 (taxable income) — first payment about 13 Dec 2026",
                "Kylie Bradley is his support person under his written authority",
            },
            HumanApprovals: new[]
            {
                "Theo's vulnerable-client review — he approves lodgement in Teams.",
                "Amelia calls Tom personally before the letter goes out.",
                "Whether Tom's insured benefit should change is Amelia's call at his next review, not the agent's.",
            },
            Steps: new[]
            {
                new ScenarioStep(1, "Claim notification", Onboarding, "📥",
                    "Tom calls with Kylie as his support person under his written authority. Olivia's agent opens the case and flags the injury and the financial stress.",
                    "Discover"),
                new ScenarioStep(2, "Policy check and benefit", Claims, "🤝",
                    "Check the Harbourline Life policy and the evidence, build the claim pack and calculate the benefit: 70% of $8,000 a month is $5,600 — less than the $7,000 insured — and explain why.",
                    "Implement"),
                new ScenarioStep(3, "Evidence checks", FinCrime, "🛡️",
                    "Check the BAS, the profit and loss and the surgeon's report. All genuine.",
                    "Verify"),
                new ScenarioStep(4, "Vulnerable-client review", Leader, "🧭",
                    "Theo reviews the case, approves lodgement and asks Amelia to call Tom personally before the letter goes out.",
                    "Oversight"),
                new ScenarioStep(5, "Lodge the claim", Claims, "🤝",
                    "After the Teams approval, lodge with Harbourline Life on 12 Nov. Harbourline accepts on 19 Nov at $5,600 a month from 13 Nov.",
                    "Implement"),
                new ScenarioStep(6, "A clear, kind letter", Comms, "✉️",
                    "Explain the benefit, why it's $5,600 and when the first payment arrives (about 13 Dec 2026), in plain English.",
                    "Confirm"),
            },
            Icon: "🩹",
            Accent: "#c9a14a"),

        // ── 5. Robert — death claim ──────────────────────────────────────────
        new ScenarioDefinition(
            Id: "death-claim",
            Number: "5",
            Title: "Death Claim — Supporting a Bereaved Family",
            ShortTitle: "Death claim",
            Customer: "Robert Chen",
            Persona: "Robert Chen — husband of the late Mei Chen",
            Tagline: "Taking the paperwork weight off a grieving family.",
            CaseNumber: "REQ-20261113-LIF005",
            Outcome: "Completed ✅",
            OutcomeDetail: "$1,000,000 paid to Robert on 23 Nov 2026, with Mei's $180,000 super claim under way.",
            SampleId: "onboarding-robert",
            KeyFacts: new[]
            {
                "Mei Chen died suddenly on Mon 12 Oct 2026, aged 42",
                "Corella Life CR-19033105 (outside super): life cover $1,000,000 · nominated beneficiary Robert 100%",
                "Robert Chen, 44 · CL-101935 · caring for Lily (12) and Max (9)",
                "Lodged with Corella Life 16 Nov · accepted 20 Nov · paid 23 Nov 2026",
                "Paid outside super, so the $1,000,000 is generally not taxable",
                "Mei's Riverbend Super default death cover of $180,000 — a separate claim with the fund's trustee; a super death benefit paid to a spouse is tax-free",
            },
            HumanApprovals: new[]
            {
                "Theo owns the case and approves every message to the family.",
                "Theo approves the death claim in Teams before Ben's agent lodges it.",
                "Robert has one named contact: Amelia.",
            },
            Steps: new[]
            {
                new ScenarioStep(1, "Death notification", Onboarding, "📥",
                    "Robert emails Amelia with a certified copy of Mei's death certificate and his ID. Olivia's agent opens the case as High priority and flags the bereavement.",
                    "Discover"),
                new ScenarioStep(2, "Compassionate oversight", Leader, "🧭",
                    "Theo owns the case: Robert gets one named contact (Amelia), and Theo approves every message to the family.",
                    "Oversight"),
                new ScenarioStep(3, "Document checks", FinCrime, "🛡️",
                    "Quietly verify the death certificate and Robert's ID in the background. Both genuine.",
                    "Verify"),
                new ScenarioStep(4, "Policy, nomination and lodgement", Claims, "🤝",
                    "Policy in force, premiums paid, no exclusions and Robert the nominated beneficiary — so the benefit goes directly to him, not through Mei's estate. Ben's agent also finds $180,000 of cover in Mei's super. After the Teams approval, lodge with Corella Life on 16 Nov.",
                    "Implement"),
                new ScenarioStep(5, "Benefit paid", Claims, "🤝",
                    "Corella Life accepts the claim on 20 Nov and pays $1,000,000 to Robert on 23 Nov 2026.",
                    "Implement"),
                new ScenarioStep(6, "Gentle letters", Comms, "✉️",
                    "Write gentle letters Robert can keep, with a simple checklist for Mei's super claim.",
                    "Confirm"),
            },
            Icon: "🤍",
            Accent: "#8b9cf7"),
    };

    public static ScenarioDefinition? FindById(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    public static ScenarioDefinition? FindBySampleId(string sampleId) =>
        All.FirstOrDefault(s => string.Equals(s.SampleId, sampleId, StringComparison.OrdinalIgnoreCase));
}