using CaldovaLife.App.Services;

namespace CaldovaLife.App.Models;

/// <summary>
/// One sample inbound request — an email (or call note) plus its attached
/// form or document — for the Onboarding "Try it out" tab on
/// <c>/agents/onboarding</c>. Each sample is one of the five canonical
/// scenarios in <c>docs/scenario-1..5</c>, and also seeds the matching Client
/// Case in <see cref="CaseStore.SeedDefaults"/> so every downstream agent page
/// has a case to work on.
/// </summary>
/// <param name="ClientFile">What Caldova already holds about the client, or a note that they're new.</param>
/// <param name="ExpectedFactFind">The fact find (or claim details) the agent extracts.</param>
/// <param name="ExpectedChecklist">The in-good-order checklist the agent runs.</param>
/// <param name="VulnerabilityFlags">Vulnerability indicators for human handling (empty when none).</param>
public record RequestSample(
    string Id,
    string Label,
    string RequestType,
    string ClientName,
    string ClientEmail,
    string ClientNumber,
    string EmailSubject,
    string EmailFrom,
    string EmailDate,
    string EmailBody,
    string FormFileName,
    string FormDocumentText,
    string ClientFile,
    RequestExtractedFields ExpectedFields,
    IReadOnlyList<FactFindItem> ExpectedFactFind,
    IReadOnlyList<GoodOrderCheck> ExpectedChecklist,
    IReadOnlyList<string> VulnerabilityFlags,
    string ExpectedPriority,
    string ExpectedPriorityReason)
{
    /// <summary>True for claim notifications (the fact find becomes "claim details").</summary>
    public bool IsClaim => RequestType is RequestTypes.IncomeProtectionClaim
        or RequestTypes.DeathClaim
        or RequestTypes.TraumaClaimBankChange;

    /// <summary>Heading for the extracted fact find on the page and in the narrative.</summary>
    public string FactFindTitle => IsClaim ? "Claim details" : "Fact find";
}

/// <summary>
/// Canonical fields the Onboarding Agent maps from the email and its
/// attachments onto the Client Case. Mirrors the "Client Case Fields" section
/// of the <see cref="CaldovaLife.Agents.OnboardingAgent"/> instructions.
/// </summary>
public record RequestExtractedFields(
    string ClientName,
    string ClientEmail,
    string ClientPhone,
    string ClientNumber,
    string Cover,
    string RequestType,
    string ReceivedDate,
    string AdviserName,
    string ReferredBy,
    string RequestSummary,
    string Amount,
    string PreferredContact);

/// <summary>
/// One line of the extracted fact find. <paramref name="Sensitive"/> marks
/// health information, which is shared only with the people who need it.
/// </summary>
public record FactFindItem(string Label, string Value, bool Sensitive = false);

/// <summary>
/// One line of the in-good-order checklist. <c>Status</c> is <c>"ok"</c> (in
/// good order) or <c>"action"</c> (needs follow-up).
/// </summary>
public record GoodOrderCheck(string Id, string Label, string Status, string Note);

/// <summary>
/// Static catalogue of the five sample requests, in canonical scenario order:
/// Priya (new family protection advice), Grace (an urgent trauma claim and
/// bank change after an identity takeover), George (business protection),
/// Tom (income protection claim) and Robert (death claim). Kept server-side
/// so one C# definition drives the listing API, the offline extraction and
/// the seeded cases.
/// </summary>
public static class RequestSampleCatalog
{
    /// <summary>Caldova's adviser on every case — she gives the advice and signs every SoA.</summary>
    public const string Adviser = "Amelia, Senior Risk Adviser";

    public static IReadOnlyList<RequestSample> All { get; } = Build();

    public static RequestSample? FindById(string id) =>
        All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<RequestSample> Build() => new[]
    {
        // ── 1. Priya Natarajan — referral from a mortgage broker ─────────────
        new RequestSample(
            Id: "onboarding-priya",
            Label: "New family protection — Priya Natarajan (referral from Hannah Lewis, Yarra Home Loans)",
            RequestType: RequestTypes.NewProtectionAdvice,
            ClientName: "Priya Natarajan",
            ClientEmail: "priya.natarajan@example.com",
            ClientNumber: "CL-104582",
            EmailSubject: "Referral — Priya Natarajan — family protection after a home purchase",
            EmailFrom: "Hannah Lewis <hannah.lewis@yarrahomeloans.example.com.au>",
            EmailDate: "Tue, 3 Nov 2026 09:12",
            EmailBody:
                "Hi Amelia,\n\n" +
                "I'd like to refer Priya Natarajan to you — she's happy for me to pass on her details.\n\n" +
                "Priya and her partner Arjun settled on their first home in Coburg last month, with a joint " +
                "mortgage of $680,000. They have a little girl, Asha (2), and a second baby due in March 2027. " +
                "Priya is a physiotherapist on $105,000 plus super, and she's worried the family couldn't keep " +
                "the house if something happened to her.\n\n" +
                "She's filled in your fact-find questionnaire (attached), read your Financial Services Guide and " +
                "signed the privacy and health-information consent and the authority for you to get her " +
                "existing super cover details. Her driver licence and a recent payslip are attached too.\n\n" +
                "Priya has asked that I'm only told once her cover is in place — please keep the details " +
                "between you.\n\n" +
                "Kind regards,\n" +
                "Hannah Lewis\n" +
                "Mortgage Broker | Yarra Home Loans\n" +
                "(03) 5550 1427 | hannah.lewis@yarrahomeloans.example.com.au\n\n" +
                "Attachments: Fact-find-Priya-Natarajan.pdf; Consent-and-authority-Natarajan.pdf; " +
                "Driver-licence-Natarajan.jpg; Payslip-Oct-2026-Natarajan.pdf",
            FormFileName: "Fact-find-Priya-Natarajan.pdf",
            FormDocumentText:
                "CALDOVA LIFE ADVICE — PERSONAL FACT FIND (PROTECTION)\n" +
                "=====================================================\n\n" +
                "1. About you\n" +
                "Name: Priya Natarajan            Date of birth: 14/02/1992 (34)\n" +
                "Address: Coburg VIC 3058\n" +
                "Email: priya.natarajan@example.com    Mobile: 0491 570 156\n" +
                "Occupation: Physiotherapist — private clinic, Brunswick (full-time, clinical)\n" +
                "Income: $105,000 a year + super           Smoker: No\n\n" +
                "2. Your family\n" +
                "Partner: Arjun Mehta (36), software engineer — has default cover in his own super " +
                "(not part of this request)\n" +
                "Children: Asha (2); second baby due March 2027\n\n" +
                "3. Your home and debts\n" +
                "Home: bought October 2026, Coburg\n" +
                "Mortgage: $680,000 (joint with Arjun) — no other debts\n" +
                "Savings: $45,000\n" +
                "Budget for premiums: up to $300 a month from cash flow\n\n" +
                "4. What matters to you\n" +
                "\"If something happened to me, I want the mortgage gone and the kids looked after — and Arjun " +
                "able to take time off to be with them.\"\n\n" +
                "5. Cover you already hold\n" +
                "a) Riverbend Super (current employer's fund) — balance $96,400 — default death and TPD cover " +
                "$210,000. \"I want to keep this.\"\n" +
                "b) Kestrel Retail Super (old hospital job) — balance $8,200 — default death cover $50,000. " +
                "Last contribution 30/07/2025. \"I haven't touched it since I changed jobs.\"\n" +
                "No cover held outside super.\n\n" +
                "6. Health basics (confidential — shared only with the insurer you apply to)\n" +
                "Asthma: mild, well controlled with a preventer inhaler\n" +
                "Back: lower back strain in 2023 — six weeks of physio, full recovery\n" +
                "Other conditions: none disclosed\n\n" +
                "7. Consents and documents\n" +
                "Financial Services Guide received: Yes (with the fact-find pack, 03/11/2026)\n" +
                "Privacy and health-information consent: signed 03/11/2026\n" +
                "Authority to obtain existing cover details (Riverbend Super, Kestrel Retail Super): signed 03/11/2026\n" +
                "Referral partner may be told: \"Only that my cover is in place\"\n" +
                "ID: Victorian driver licence (copy attached)    Payslip: October 2026 (attached)\n\n" +
                "Signed: Priya Natarajan        Date: 03/11/2026",
            ClientFile:
                "New client — no Caldova client file yet. Client number CL-104582 reserved at intake.\n" +
                "Referral partner on file: Hannah Lewis, Yarra Home Loans (referral agreement in place).",
            ExpectedFields: new RequestExtractedFields(
                ClientName: "Priya Natarajan",
                ClientEmail: "priya.natarajan@example.com",
                ClientPhone: "0491 570 156",
                ClientNumber: "CL-104582",
                Cover: "Riverbend Super death & TPD $210,000 (keep); Kestrel Retail Super death $50,000 (at risk 30 Nov 2026); nothing outside super",
                RequestType: RequestTypes.NewProtectionAdvice,
                ReceivedDate: "2026-11-03",
                AdviserName: Adviser,
                ReferredBy: "Hannah Lewis, Yarra Home Loans",
                RequestSummary:
                    "New family protection advice for Priya (34, physiotherapist, $105,000 + super) after buying " +
                    "a home in Coburg with a $680,000 joint mortgage. Partner Arjun, daughter Asha (2), second baby " +
                    "due March 2027; savings $45,000; premium budget up to $300 a month. Existing cover is in super " +
                    "only: Riverbend Super death and TPD $210,000 (keep) and an old Kestrel Retail Super account " +
                    "($50,000 death cover) with no contributions since 30 Jul 2025, so that cover is at risk of " +
                    "stopping on 30 Nov 2026.",
                Amount: "$680,000 joint mortgage to protect",
                PreferredContact: "Mobile 0491 570 156 or email — updates to Hannah: outcome only"),
            ExpectedFactFind: new[]
            {
                new FactFindItem("Client", "Priya Natarajan, 34 — physiotherapist (private clinic, Brunswick); non-smoker"),
                new FactFindItem("Income", "$105,000 a year + super"),
                new FactFindItem("Dependants", "Partner Arjun Mehta (36, has his own super cover — not part of this case); daughter Asha (2); second baby due March 2027"),
                new FactFindItem("Debts", "Joint mortgage $680,000 (home bought October 2026); no other debts"),
                new FactFindItem("Savings and budget", "Savings $45,000; up to $300 a month for premiums from cash flow"),
                new FactFindItem("Goals", "Clear the mortgage and look after the children if something happens to Priya; give Arjun time off to be with them"),
                new FactFindItem("Existing cover (super)", "Riverbend Super death and TPD $210,000 (keep); Kestrel Retail Super death $50,000 — no contributions since 30 Jul 2025"),
                new FactFindItem("Existing cover (outside super)", "None"),
                new FactFindItem("Health basics", "Mild asthma, well controlled; lower back strain in 2023, full recovery", Sensitive: true),
            },
            ExpectedChecklist: new[]
            {
                new GoodOrderCheck("fsg", "Financial Services Guide given", "ok", "Caldova's FSG was sent with the fact-find pack and Priya confirms she received it (3 Nov 2026)."),
                new GoodOrderCheck("privacy", "Privacy and health-information consent", "ok", "Signed by Priya on 3 Nov 2026."),
                new GoodOrderCheck("id", "Identity", "ok", "Driver licence and payslip received — sent to Financial Crime for KYC, as for every new client."),
                new GoodOrderCheck("authority", "Authority to obtain existing cover details", "ok", "Signed for Riverbend Super and Kestrel Retail Super on 3 Nov 2026."),
                new GoodOrderCheck("fact-find", "Fact find complete", "ok", "Goals, dependants, debts, income, occupation, smoker status, health basics, existing cover and the premium budget are all captured."),
                new GoodOrderCheck("referral-consent", "Referral partner consent", "ok", "Priya agreed to Hannah's referral. Hannah may be told the outcome only (cover in place) — no health details."),
                new GoodOrderCheck("commission-consent", "Commission consent", "action", "The SoA must show the commission in dollars (there's no advice fee for protection-only advice), and Priya's informed written consent — with the disclosures the law requires and a copy for her — is needed before any cover is issued."),
                new GoodOrderCheck("existing-cover", "Existing cover at risk", "action", "Kestrel Retail Super hasn't received a contribution since 30 Jul 2025. Under Protecting Your Super a fund can't keep insurance on an account with no amount received for 16 continuous months unless the account holder elects in writing to keep it — so the $50,000 death cover is due to stop on 30 Nov 2026. Flag to Penny now; keeping or cancelling cover is Priya's decision, with Amelia."),
            },
            VulnerabilityFlags: Array.Empty<string>(),
            ExpectedPriority: "High",
            ExpectedPriorityReason:
                "Existing cover at risk: Priya's Kestrel Retail Super death cover is due to stop on 30 Nov 2026 " +
                "(27 days away) unless she elects in writing to keep it, and the family has a new $680,000 " +
                "mortgage with no cover outside super. No vulnerability indicators."),

        // ── 2. Grace Williams — urgent trauma claim to a new account ──────────
        new RequestSample(
            Id: "onboarding-grace",
            Label: "Urgent trauma claim to a new account — Grace Williams (sent from a newly changed email)",
            RequestType: RequestTypes.TraumaClaimBankChange,
            ClientName: "Grace Williams",
            ClientEmail: "grace.williams@example.com",
            ClientNumber: "CL-100377",
            EmailSubject: "URGENT — trauma claim — Grace Williams",
            EmailFrom: "Grace Williams <grace.w.claims@example.net>",
            EmailDate: "Fri, 6 Nov 2026 07:52",
            EmailBody:
                "Hello,\n\n" +
                "I have been diagnosed with breast cancer and I need to claim my full trauma cover of $200,000 " +
                "under my Lyrebird Life policy LB-19004417 as soon as possible. My specialist says treatment has " +
                "to start straight away and I need the money urgently.\n\n" +
                "Please pay the benefit into my new account ending 4471, in the name G Williams Consulting Pty " +
                "Ltd. My old bank account is closed.\n\n" +
                "I'm overseas and I've lost my old phone, so please only contact me on my new mobile " +
                "0491 570 159 or this email address.\n\n" +
                "The oncologist's report, my driver licence and a bank statement are attached.\n\n" +
                "Thank you,\n" +
                "Grace Williams\n\n" +
                "Attachments: Oncology-report-Dr-A-Moretti.pdf; Licence-GWilliams.jpg; " +
                "Bank-statement-GW-Consulting.pdf",
            FormFileName: "3 attachments — oncologist's report, driver licence, bank statement",
            FormDocumentText:
                "ATTACHMENTS RECEIVED WITH THE EMAIL (text extracted)\n" +
                "====================================================\n\n" +
                "1. Oncology-report-Dr-A-Moretti.pdf\n" +
                "   Letterhead: Dr A. Moretti, Eastern Oncology Group\n" +
                "   Patient: Grace Williams, DOB 22/07/1965\n" +
                "   States a diagnosis of breast cancer on 21/10/2026 and that \"treatment should start urgently\"\n" +
                "   Histopathology report: none attached\n\n" +
                "2. Licence-GWilliams.jpg\n" +
                "   Victorian driver licence image — name Grace Williams, DOB 22/07/1965, Hawthorn VIC\n\n" +
                "3. Bank-statement-GW-Consulting.pdf\n" +
                "   Interim statement — BSB 06*-***, account ****4471\n" +
                "   Account name: G Williams Consulting Pty Ltd\n" +
                "   This is the account the email asks Caldova to pay",
            ClientFile:
                "CALDOVA CLIENT FILE — Grace Williams (CL-100377) — client since 2019\n" +
                "Verified contact details (held before 02/11/2026): mobile 0491 570 157 · grace.williams@example.com\n" +
                "Cover: Lyrebird Life LB-19004417 (outside super, stepped) — life $500,000; trauma $200,000\n" +
                "Nominated beneficiaries: Daniel Williams 50%, James Williams 50%\n" +
                "Annual review with Amelia, 28/09/2026 — travel notice: Italy, 20/10/2026 to 01/12/2026\n" +
                "Activity log:\n" +
                "• Mon 02/11/2026 10:41 — phone call: caller passed name, date of birth and address checks; " +
                "mobile changed to 0491 570 159 and email to grace.w.claims@example.net (\"I've lost my phone\"); " +
                "no code sent to the old mobile\n" +
                "• Mon 02/11/2026 10:58 — client portal login from a new device (Sydney IP address)\n" +
                "• Fri 06/11/2026 07:52 — this email (trauma claim and new bank account)",
            ExpectedFields: new RequestExtractedFields(
                ClientName: "Grace Williams",
                ClientEmail: "grace.williams@example.com",
                ClientPhone: "0491 570 157",
                ClientNumber: "CL-100377",
                Cover: "Lyrebird Life LB-19004417 (outside super): life $500,000, trauma $200,000",
                RequestType: RequestTypes.TraumaClaimBankChange,
                ReceivedDate: "2026-11-06",
                AdviserName: Adviser,
                ReferredBy: "Existing client (since 2019)",
                RequestSummary:
                    "An email from a newly changed address asks for Grace's full $200,000 trauma benefit (breast " +
                    "cancer, as stated) to be paid urgently into a new account ending 4471 in the name 'G Williams " +
                    "Consulting Pty Ltd'. Grace's mobile and email were changed by phone on 2 Nov 2026, about 93 " +
                    "hours before this email, and a travel notice (Italy, 20 Oct – 1 Dec 2026) is on file. The " +
                    "contact details shown are the verified ones held before the change.",
                Amount: "$200,000 trauma benefit requested",
                PreferredContact: "Verified mobile 0491 570 157 only — not the new details"),
            ExpectedFactFind: new[]
            {
                new FactFindItem("Policy owner and life insured", "Grace Williams, 61 — semi-retired school principal, Hawthorn"),
                new FactFindItem("Policy", "Lyrebird Life LB-19004417 (outside super) — trauma $200,000; life $500,000"),
                new FactFindItem("Condition claimed", "Breast cancer, diagnosis stated as 21 Oct 2026", Sensitive: true),
                new FactFindItem("Medical evidence supplied", "Oncologist's report from 'Dr A. Moretti, Eastern Oncology Group'; no histopathology report", Sensitive: true),
                new FactFindItem("Amount and payee", "$200,000 to a new account ending 4471 in the name 'G Williams Consulting Pty Ltd' — not the policy owner"),
                new FactFindItem("Recent changes", "Mobile and email changed by phone on 2 Nov 2026 10:41; portal login from a new device (Sydney IP) 2 Nov 10:58"),
                new FactFindItem("Travel notice", "Italy, 20 Oct – 1 Dec 2026 (told to Amelia at her 28 Sep 2026 review)"),
                new FactFindItem("Nominated beneficiaries (life cover)", "Daniel 50%, James 50%"),
            },
            ExpectedChecklist: new[]
            {
                new GoodOrderCheck("policy", "Policy on file", "ok", "Lyrebird Life LB-19004417 is on file: Grace is the policy owner and life insured, with $200,000 trauma cover."),
                new GoodOrderCheck("contact-change", "Recent change of contact details", "action", "Mobile and email were changed by phone on 2 Nov 2026 with no code sent to the old mobile, and this request came from the new email about 93 hours later. A change in the last 30 days goes to Financial Crime before anything else is actioned. Use only the verified mobile 0491 570 157."),
                new GoodOrderCheck("bank-details", "Payment details", "action", "New account ending 4471 is in the name 'G Williams Consulting Pty Ltd'. Benefits are paid to the policy owner — never a third party or a different name — and any bank-detail change needs verification and a person's approval."),
                new GoodOrderCheck("claim-forms", "Insurer claim form and medical authority", "action", "None supplied — the request is an email with attachments only. Don't send claim forms to the new email address."),
                new GoodOrderCheck("medical-evidence", "Medical evidence", "action", "Oncologist's report attached but no histopathology report. Ben's agent checks the evidence against the Lyrebird Life definition."),
                new GoodOrderCheck("id", "Identity document", "action", "Driver licence image supplied — refer to Financial Crime for an authenticity check before anything else."),
                new GoodOrderCheck("travel-notice", "Travel notice on file", "action", "Grace told Amelia on 28 Sep 2026 that she'd be in Italy from 20 Oct to 1 Dec 2026 — yet the portal was opened from Sydney on 2 Nov, and this request asks us to use only the new details."),
                new GoodOrderCheck("pressure", "Urgency and channel steering", "action", "'Urgent', the full sum insured, 'my old account is closed' and 'only contact me on my new mobile or this email' are common scam indicators. Don't reply to or call the new details."),
            },
            VulnerabilityFlags: new[]
            {
                "Possible scam or identity takeover — Grace may be the target",
                "Client overseas (travel notice on file)",
            },
            ExpectedPriority: "Urgent",
            ExpectedPriorityReason:
                "Possible identity takeover: Grace's contact details were changed about 93 hours before an urgent " +
                "claim for the full $200,000 to a new account in another name, while she is overseas. Route to " +
                "Financial Crime before anything else, and don't contact the requester on the new details. This " +
                "doesn't suggest Grace has done anything wrong — she may be the target."),

        // ── 3. George Papadopoulos — referral from an accountant ─────────────
        new RequestSample(
            Id: "onboarding-george",
            Label: "Business protection — George Papadopoulos & Harpreet Kaur (referral from Marcus Tan, Bayside Business Accountants)",
            RequestType: RequestTypes.BusinessProtectionAdvice,
            ClientName: "George Papadopoulos",
            ClientEmail: "george@northcotejoinery.example.com.au",
            ClientNumber: "CL-104611",
            EmailSubject: "Referral — George Papadopoulos & Harpreet Kaur — Northcote Custom Joinery",
            EmailFrom: "Marcus Tan <marcus.tan@baysideaccountants.example.com.au>",
            EmailDate: "Mon, 9 Nov 2026 10:05",
            EmailBody:
                "Hi Amelia,\n\n" +
                "I'd like to refer two of my business clients to you, George Papadopoulos and Harpreet Kaur. " +
                "They're the directors and 50/50 owners of Northcote Custom Joinery Pty Ltd, and both have " +
                "agreed to this referral.\n\n" +
                "They've built a great business since 2011 — 14 staff, revenue of $3.1 million and a net profit " +
                "of $520,000 last year — but they have no succession plan. If either of them died or became " +
                "seriously disabled, the other couldn't afford to buy out their share, and the family would be " +
                "left holding half a joinery business.\n\n" +
                "I valued the business at $2.4 million at 30 June 2026. George is the master joiner, and I " +
                "estimate losing him would cost about $400,000 of profit over 18 months while a replacement is " +
                "found and trained.\n\n" +
                "Their business details form is attached. With their authority I'll share the valuation summary " +
                "and the last two years' financial statements. Their lawyer will draft any buy-sell agreement.\n\n" +
                "They're happy for me to be told the outcome once the cover is in place.\n\n" +
                "Regards,\n" +
                "Marcus Tan CA\n" +
                "Partner | Bayside Business Accountants\n" +
                "(03) 5550 2861 | marcus.tan@baysideaccountants.example.com.au\n\n" +
                "Attachments: Business-details-Northcote-Custom-Joinery.pdf",
            FormFileName: "Business-details-Northcote-Custom-Joinery.pdf",
            FormDocumentText:
                "CALDOVA LIFE ADVICE — BUSINESS PROTECTION FACT FIND\n" +
                "===================================================\n\n" +
                "The business\n" +
                "Name: Northcote Custom Joinery Pty Ltd (founded 2011)\n" +
                "Activity: custom joinery and cabinetry — 14 staff\n" +
                "FY2026 revenue: $3.1 million    Net profit: $520,000\n" +
                "Valuation: $2,400,000 at 30/06/2026 (Marcus Tan, Bayside Business Accountants)\n" +
                "Owners: two directors, 50% each — no other shareholders\n" +
                "Buy-sell agreement: none    Existing business cover: none\n\n" +
                "Owner 1\n" +
                "George Papadopoulos — DOB 19/04/1974 (52) — master joiner and co-founder\n" +
                "Thornbury VIC 3071 · george@northcotejoinery.example.com.au · 0491 570 158\n" +
                "Married to Helen    Smoker: No\n\n" +
                "Owner 2\n" +
                "Harpreet Kaur — DOB 08/09/1979 (47) — operations and finance director\n" +
                "Smoker: No\n\n" +
                "What matters to you\n" +
                "\"If one of us dies or can't work again, the other should be able to buy their share at a fair " +
                "price — and the family should be paid properly.\"\n" +
                "\"If we lose George's skills, the business needs time and money to replace him.\" " +
                "(about $400,000 of profit over 18 months — Marcus Tan's estimate)\n\n" +
                "Health basics (confidential): to be completed with each owner separately\n\n" +
                "Consents and documents (both owners)\n" +
                "Financial Services Guide received: Yes (09/11/2026)\n" +
                "Privacy and health-information consent: signed by both, 09/11/2026\n" +
                "Authority to obtain existing cover details: signed by both, 09/11/2026\n" +
                "Authority for Marcus Tan to share the valuation and the last two years' financial statements: signed by both\n" +
                "Referral partner may be told: \"The outcome, once the cover is in place\"\n" +
                "ID: George — Victorian driver licence; Harpreet — Australian passport; company extract (attached)\n\n" +
                "Signed: George Papadopoulos · Harpreet Kaur        Date: 09/11/2026",
            ClientFile:
                "New clients — no Caldova client file yet. Client numbers reserved at intake: George CL-104611, " +
                "Harpreet CL-104612.\n" +
                "Referral partner on file: Marcus Tan, Bayside Business Accountants (referral agreement in place).",
            ExpectedFields: new RequestExtractedFields(
                ClientName: "George Papadopoulos",
                ClientEmail: "george@northcotejoinery.example.com.au",
                ClientPhone: "0491 570 158",
                ClientNumber: "CL-104611",
                Cover: "No business cover in place — buy-sell and key person cover requested",
                RequestType: RequestTypes.BusinessProtectionAdvice,
                ReceivedDate: "2026-11-09",
                AdviserName: Adviser,
                ReferredBy: "Marcus Tan, Bayside Business Accountants",
                RequestSummary:
                    "Business protection for Northcote Custom Joinery Pty Ltd: co-owners George Papadopoulos (52, " +
                    "master joiner) and Harpreet Kaur (47, operations and finance director, CL-104612), 50/50, with no " +
                    "succession plan. Business valued at $2.4 million at 30 Jun 2026; 14 staff; net profit $520,000. " +
                    "They need buy-sell cover so either owner can buy the other's share, and key person cover on " +
                    "George (about $400,000 of profit at risk over 18 months). Their lawyer drafts the buy-sell agreement.",
                Amount: "$2,400,000 business valuation",
                PreferredContact: "Email George, copying Harpreet; Marcus may be told the outcome"),
            ExpectedFactFind: new[]
            {
                new FactFindItem("Business", "Northcote Custom Joinery Pty Ltd — founded 2011, 14 staff"),
                new FactFindItem("Owners", "George Papadopoulos (52) 50% · Harpreet Kaur (47) 50% — both directors, both non-smokers"),
                new FactFindItem("Revenue and profit", "FY2026 revenue $3.1 million; net profit $520,000"),
                new FactFindItem("Valuation", "$2,400,000 at 30 Jun 2026 (Marcus Tan) — each 50% share is worth $1,200,000"),
                new FactFindItem("Goals", "Fund a buy-out if either owner dies or is seriously disabled; protect the business if it loses George's skills"),
                new FactFindItem("Key person impact", "About $400,000 of profit over 18 months while a replacement is found and trained (Marcus's estimate)"),
                new FactFindItem("Existing cover", "No business cover; no buy-sell agreement — their lawyer will draft one"),
                new FactFindItem("Family", "George is married to Helen (Thornbury)"),
                new FactFindItem("Health basics", "To be collected separately and confidentially with each owner", Sensitive: true),
            },
            ExpectedChecklist: new[]
            {
                new GoodOrderCheck("fsg", "Financial Services Guide given", "ok", "Both owners confirm they received Caldova's FSG on 9 Nov 2026."),
                new GoodOrderCheck("privacy", "Privacy and health-information consent", "ok", "Signed by George and Harpreet on 9 Nov 2026."),
                new GoodOrderCheck("id", "Identity and company KYC", "ok", "Company extract and ID for both beneficial owners (George's driver licence, Harpreet's passport) received — sent to Financial Crime for KYC, as for every new client."),
                new GoodOrderCheck("authority", "Authorities", "ok", "Both owners authorised Caldova to obtain existing cover details, and Marcus to share the valuation and the last two years' financial statements."),
                new GoodOrderCheck("financial-evidence", "Business evidence", "ok", "Valuation of $2.4 million at 30 Jun 2026, Marcus's key person estimate (about $400,000 over 18 months) and two years' financial statements — the insurer will ask for them at these sums insured."),
                new GoodOrderCheck("referral-consent", "Referral partner consent", "ok", "Marcus may be told the outcome once the cover is in place — no health details."),
                new GoodOrderCheck("buy-sell", "Buy-sell agreement", "action", "None in place. The owners' lawyer drafts it — Caldova doesn't draft legal documents."),
                new GoodOrderCheck("commission-consent", "Commission consent", "action", "The SoA must show the commission in dollars, and each policy owner's informed written consent — with the disclosures the law requires and a copy for them — is needed before any cover is issued."),
                new GoodOrderCheck("second-review", "Second review", "action", "Indicative total cover is about $2.8 million (buy-sell $1.2 million each on the $2.4 million valuation, plus $400,000 key person cover). That's $2,000,000 or more on one case, so Theo's second review is needed before the application."),
            },
            VulnerabilityFlags: Array.Empty<string>(),
            ExpectedPriority: "Standard",
            ExpectedPriorityReason:
                "No vulnerability indicators and no existing cover at risk. Indicative total cover of about " +
                "$2.8 million means Theo's second review ($2,000,000 or more on one case) before the application — " +
                "flag it now so it doesn't hold anything up."),

        // ── 4. Tom Bradley — income protection claim notification ─────────────
        new RequestSample(
            Id: "onboarding-tom",
            Label: "Income protection claim — Tom Bradley (phone call, with Kylie as support person)",
            RequestType: RequestTypes.IncomeProtectionClaim,
            ClientName: "Tom Bradley",
            ClientEmail: "tom.bradley@example.com",
            ClientNumber: "CL-102265",
            EmailSubject: "Call note — income protection claim — Tom Bradley (CL-102265)",
            EmailFrom: "Tom Bradley (phone call, 0491 570 110) — with Kylie Bradley, support person",
            EmailDate: "Wed, 11 Nov 2026 09:40",
            EmailBody:
                "Call note — Wed 11 Nov 2026, 09:40 (14 minutes) — taken by Olivia\n\n" +
                "Tom Bradley called to tell us he can't work after an accident. His wife Kylie joined the call as " +
                "his support person; Tom asked that she can speak for him, and he signed an authority straight " +
                "after the call (attached).\n\n" +
                "What happened: on Wed 14 Oct 2026 Tom fell from a ladder on a job and tore the rotator cuff in " +
                "his right shoulder. He had surgery on 21 Oct. His surgeon expects four to six months off work.\n\n" +
                "Work and money: Tom is a self-employed electrician (sole trader, Bradley Electrical). He hasn't " +
                "worked since the fall, so the family has lost its main income. Kylie, a part-time bookkeeper, " +
                "keeps the books; she says net business income was about $96,000 for the 12 months to 30 Sep " +
                "2026 and will send the BAS and profit and loss from their tax agent.\n\n" +
                "How they're going: Tom sounded worried — \"the bills don't stop and we've got two kids\". He asked " +
                "how long a claim takes and whether he'll get the full $7,000. I said Ben will check the policy and " +
                "come back to them, and that Amelia will be in touch.\n\n" +
                "Still to come: Harbourline Life's claim form; the surgeon's report; the BAS and profit and loss; " +
                "a bank account in Tom's own name for the benefit.",
            FormFileName: "Support-person-authority-Tom-Bradley.pdf",
            FormDocumentText:
                "AUTHORITY FOR A SUPPORT PERSON\n" +
                "==============================\n\n" +
                "I, Tom Bradley (DOB 03/03/1985, client number CL-102265), authorise my wife, Kylie Bradley " +
                "(mobile 0491 570 737), to act as my support person and to speak with Caldova Life Advice and " +
                "Harbourline Life about my income protection claim on policy HL-23051877, and to receive " +
                "information about it on my behalf.\n\n" +
                "I can change or withdraw this authority at any time by telling Caldova.\n\n" +
                "Signed: Tom Bradley        Date: 11/11/2026\n\n" +
                "About the claim (as Tom told us on the call)\n" +
                "Policy: Harbourline Life income protection HL-23051877 (outside super)\n" +
                "What happened: fell from a ladder at work on 14/10/2026 — torn rotator cuff, right shoulder\n" +
                "Treatment: surgery 21/10/2026; surgeon expects 4 to 6 months off work\n" +
                "Last day worked: 14/10/2026",
            ClientFile:
                "CALDOVA CLIENT FILE — Tom Bradley (CL-102265) — client since 2023\n" +
                "Verified contact details: mobile 0491 570 110 · tom.bradley@example.com\n" +
                "Cover: Harbourline Life income protection HL-23051877 (outside super, stepped), from 15/03/2023 — " +
                "$7,000 a month (set from $120,000 a year at application), 30-day waiting period, to age 65; " +
                "premiums $2,880 a year; in force\n" +
                "Family: wife Kylie (39, part-time bookkeeper), two children (8 and 5) — Frankston\n" +
                "No recent changes to contact or bank details.",
            ExpectedFields: new RequestExtractedFields(
                ClientName: "Tom Bradley",
                ClientEmail: "tom.bradley@example.com",
                ClientPhone: "0491 570 110",
                ClientNumber: "CL-102265",
                Cover: "Harbourline Life income protection HL-23051877: $7,000 a month, 30-day wait, to age 65",
                RequestType: RequestTypes.IncomeProtectionClaim,
                ReceivedDate: "2026-11-11",
                AdviserName: Adviser,
                ReferredBy: "Existing client (since 2023)",
                RequestSummary:
                    "Tom, a self-employed electrician, can't work after falling from a ladder on 14 Oct 2026 (torn " +
                    "rotator cuff, right shoulder; surgery 21 Oct; four to six months off expected). He's claiming on " +
                    "his Harbourline Life income protection (insured $7,000 a month, 30-day waiting period). Kylie is " +
                    "his support person under his written authority. The family has lost its main income. Still to " +
                    "come: the claim form, the surgeon's report, the BAS and profit and loss for the 12 months to " +
                    "30 Sep 2026, and a bank account in Tom's own name.",
                Amount: "$7,000 a month insured benefit",
                PreferredContact: "Mobile 0491 570 110 — Kylie Bradley may speak for Tom (written authority)"),
            ExpectedFactFind: new[]
            {
                new FactFindItem("Life insured", "Tom Bradley, 41 — self-employed electrician (sole trader, Bradley Electrical)"),
                new FactFindItem("Policy", "Harbourline Life income protection HL-23051877 (outside super), in force from 15 Mar 2023"),
                new FactFindItem("Insured benefit", "$7,000 a month · 30-day waiting period · benefit period to age 65"),
                new FactFindItem("Event", "Fell from a ladder on a job, Wed 14 Oct 2026; no work since"),
                new FactFindItem("Injury and treatment", "Torn rotator cuff, right shoulder; surgery 21 Oct 2026; four to six months off work expected", Sensitive: true),
                new FactFindItem("Earnings", "About $96,000 net business income for the 12 months to 30 Sep 2026 (Kylie) — BAS and profit and loss to come"),
                new FactFindItem("Family", "Wife Kylie (39, part-time bookkeeper); two children (8 and 5)"),
                new FactFindItem("Support person", "Kylie Bradley — written authority signed by Tom on 11 Nov 2026"),
            },
            ExpectedChecklist: new[]
            {
                new GoodOrderCheck("policy", "Policy found and in force", "ok", "Harbourline Life HL-23051877 is on file and in force (from 15 Mar 2023)."),
                new GoodOrderCheck("support-person", "Support-person authority", "ok", "Tom's written authority (11 Nov 2026) lets Kylie speak with Caldova and Harbourline Life for him — insurers recognise support people under the Life Insurance Code of Practice."),
                new GoodOrderCheck("claim-form", "Insurer claim form", "action", "Harbourline Life's claim form to be completed and signed — Ben's agent will prefill it so Tom only has to check and sign."),
                new GoodOrderCheck("medical-evidence", "Surgeon's report", "action", "The treating surgeon's report is needed: the injury, the surgery on 21 Oct and the expected time off work."),
                new GoodOrderCheck("earnings-evidence", "Earnings evidence", "action", "BAS and profit and loss for the 12 months before the injury (to 30 Sep 2026) — Kylie is sending them."),
                new GoodOrderCheck("payment-details", "Payment account", "action", "A bank account in Tom's own name for the benefit."),
            },
            VulnerabilityFlags: new[]
            {
                "Financial stress — self-employed, the family has lost its main income",
                "Injury",
                "Support person: Kylie (written authority)",
            },
            ExpectedPriority: "High",
            ExpectedPriorityReason:
                "Tom is self-employed and can't work, so the family has lost its main income — financial stress " +
                "and injury, flagged for human handling. His 30-day waiting period ends on 13 Nov 2026, so the " +
                "claim pack should be ready promptly. Kylie is his support person under his written authority."),

        // ── 5. Robert Chen — death claim notification ─────────────────────────
        new RequestSample(
            Id: "onboarding-robert",
            Label: "Death claim — Robert Chen for his wife Mei Chen",
            RequestType: RequestTypes.DeathClaim,
            ClientName: "Robert Chen",
            ClientEmail: "robert.chen@example.com",
            ClientNumber: "CL-101935",
            EmailSubject: "Mei Chen — Corella Life policy",
            EmailFrom: "Robert Chen <robert.chen@example.com>",
            EmailDate: "Fri, 13 Nov 2026 08:31",
            EmailBody:
                "Dear Amelia,\n\n" +
                "I'm sorry to be writing with this news. My wife Mei passed away suddenly on Monday 12 October. " +
                "It was a brain aneurysm — there was no warning at all.\n\n" +
                "You helped us set up Mei's life insurance with Corella Life back in 2019, and I think I'm named " +
                "on it. I've attached a certified copy of the death certificate and a certified copy of my driver " +
                "licence.\n\n" +
                "I'm looking after Lily and Max at home, and honestly I don't know where to start with any of " +
                "this. Could you please let me know what you need from me, and roughly how long it takes?\n\n" +
                "Thank you,\n" +
                "Robert Chen\n" +
                "0491 570 313\n\n" +
                "Attachments: Death-certificate-Mei-Chen-certified.pdf; Driver-licence-Robert-Chen-certified.pdf",
            FormFileName: "Death-certificate-Mei-Chen-certified.pdf",
            FormDocumentText:
                "CERTIFIED COPY — DEATH CERTIFICATE (summary of the attached copy)\n" +
                "=================================================================\n\n" +
                "Issued by: Registry of Births, Deaths and Marriages Victoria\n" +
                "Registration number: 2026/041857        Registered: 19/10/2026\n" +
                "Name of deceased: Mei Chen\n" +
                "Date of birth: 05/06/1984        Date of death: 12/10/2026 (aged 42)\n" +
                "Place of death: Box Hill VIC\n" +
                "Cause of death: brain aneurysm\n" +
                "Occupation: pharmacist\n" +
                "Usual residence: Box Hill VIC 3128\n" +
                "Marital status: married — spouse Robert Chen\n" +
                "Children: Lily (12), Max (9)\n" +
                "Certified true copy: Justice of the Peace (Victoria), 11/11/2026\n\n" +
                "Second attachment: certified copy of a Victorian driver licence — Robert Chen, " +
                "DOB 30/01/1982, Box Hill VIC 3128 (certified 11/11/2026)",
            ClientFile:
                "CALDOVA CLIENT FILE — Mei Chen (CL-101934) and Robert Chen (CL-101935) — clients since 2019\n" +
                "Policy: Corella Life CR-19033105 (outside super) — advised by Amelia, March 2019\n" +
                "Policy owner and life insured: Mei Chen · Life cover: $1,000,000\n" +
                "Nominated beneficiary: Robert Chen — 100%\n" +
                "Robert's verified contact details: 0491 570 313 · robert.chen@example.com\n" +
                "No recent changes to contact or bank details.",
            ExpectedFields: new RequestExtractedFields(
                ClientName: "Robert Chen",
                ClientEmail: "robert.chen@example.com",
                ClientPhone: "0491 570 313",
                ClientNumber: "CL-101935",
                Cover: "Corella Life CR-19033105 (Mei Chen, outside super): life $1,000,000 — nominated beneficiary Robert 100%",
                RequestType: RequestTypes.DeathClaim,
                ReceivedDate: "2026-11-13",
                AdviserName: Adviser,
                ReferredBy: "Existing clients (since 2019)",
                RequestSummary:
                    "Robert's wife Mei (CL-101934) died suddenly on 12 Oct 2026, aged 42. He has sent a certified copy of her " +
                    "death certificate and a certified copy of his driver licence. Mei's Corella Life policy " +
                    "CR-19033105 (outside super) has $1,000,000 life cover and names Robert as the beneficiary " +
                    "(100%). Robert is caring for Lily (12) and Max (9) and has asked what happens next and how " +
                    "long it takes.",
                Amount: "$1,000,000 life cover",
                PreferredContact: "Phone or email (Robert) — one named contact: Amelia"),
            ExpectedFactFind: new[]
            {
                new FactFindItem("Life insured (deceased)", "Mei Chen, 42 — pharmacist, Box Hill"),
                new FactFindItem("Date of death", "Mon 12 Oct 2026"),
                new FactFindItem("Cause of death", "Brain aneurysm (sudden)", Sensitive: true),
                new FactFindItem("Policy", "Corella Life CR-19033105 (outside super) — life cover $1,000,000"),
                new FactFindItem("Policy owner and beneficiary", "Owner: Mei Chen · nominated beneficiary: Robert Chen (100%)"),
                new FactFindItem("Claimant", "Robert Chen, 44 — husband — secondary school teacher"),
                new FactFindItem("Dependants", "Lily (12) and Max (9)"),
                new FactFindItem("Documents received", "Certified death certificate (BDM Victoria 2026/041857); certified driver licence (Robert)"),
            },
            ExpectedChecklist: new[]
            {
                new GoodOrderCheck("death-certificate", "Certified death certificate", "ok", "Certified copy received (BDM Victoria, registration 2026/041857)."),
                new GoodOrderCheck("claimant-id", "Claimant identification", "ok", "Certified copy of Robert's driver licence received — Financial Crime verifies it quietly in the background."),
                new GoodOrderCheck("policy-nomination", "Policy and nominated beneficiary", "ok", "CR-19033105 is on file with Robert as the nominated beneficiary (100%) — Claims confirms the nomination."),
                new GoodOrderCheck("claim-form", "Insurer death claim form", "action", "Corella Life's death claim form, completed with Robert with Ben's help — Ben's agent prefills it so Robert only has to check and sign."),
                new GoodOrderCheck("payment-details", "Payment account", "action", "A bank account in Robert's own name for the payment — confirm it with him by phone, never by email."),
                new GoodOrderCheck("other-cover", "Other cover", "action", "Check for other cover the family may not know about, such as default cover in Mei's super — Ben's agent tells Robert what it finds."),
                new GoodOrderCheck("contact-plan", "Care plan", "ok", "One named contact (Amelia); Theo approves every message to the family; gentle, plain language and no chasing."),
            },
            VulnerabilityFlags: new[]
            {
                "Bereavement — sudden death of his wife",
                "Sole carer of two children",
            },
            ExpectedPriority: "High",
            ExpectedPriorityReason:
                "Bereavement — Robert's wife died suddenly and he is caring for two children. Compassionate, " +
                "human-led handling: Theo oversees the case, Amelia is Robert's one named contact, and Theo " +
                "approves the death claim in Teams before it's lodged."),
    };
}
