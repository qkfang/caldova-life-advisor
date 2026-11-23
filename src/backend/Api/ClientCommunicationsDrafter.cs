using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

/// <summary>
/// Deterministic draft generator for the Client Communications "Try it out"
/// tab (Cara). The five seeded scenario cases are shown at the point Cara
/// writes the outcome, with her case summary (sentiment, what happened,
/// what's next) and ready-to-review drafts for every audience the case needs:
/// the client (letters, a plain-English policy summary), call scripts for
/// Cara or Amelia, referral-partner updates limited to what the client
/// consented to share, and an SMS. Cases opened live with the Onboarding
/// Agent get a type-aware acknowledgement instead. Every draft carries tone
/// notes and a compliance check — no personal advice, facts that match the
/// case, required disclosures, complaint rights (IDR, then AFCA), privacy of
/// health information — and the human approval it needs. The page therefore
/// always has realistic output offline; when Foundry is configured the live
/// agent's streamed narrative is shown alongside these drafts.
/// </summary>
internal static class ClientCommunicationsDrafter
{
    /// <summary>One line of a draft's compliance check. <c>Status</c> is <c>pass</c>, <c>warn</c> or <c>n/a</c>.</summary>
    internal record ComplianceCheck(string Label, string Status, string Note);

    /// <summary>
    /// One ready-to-review communication. <c>Audience</c> is <c>client</c>,
    /// <c>call</c>, <c>partner</c> or <c>sms</c> (used by the page to group the cards).
    /// </summary>
    internal record Draft(
        string Id,
        string Audience,
        string AudienceLabel,
        string Channel,
        string Recipient,
        string? Subject,
        string Body,
        string Tone,
        IReadOnlyList<ComplianceCheck> Compliance,
        bool ApprovalRequired,
        string ApprovalReason);

    /// <summary>Cara's plain-English read of the case before she writes anything.</summary>
    internal record CaseSummary(
        string SentimentLabel,
        string Sentiment,
        string WhatHappened,
        IReadOnlyList<string> WhatsNext);

    /// <param name="Approver">Who approves the drafts before anything is sent.</param>
    /// <param name="PartnerNote">Why there is no referral-partner update, when there isn't one.</param>
    /// <param name="AdviserNote">An internal note for Amelia — never advice, never sent to the client.</param>
    /// <param name="Sources">Knowledge documents (<c>life_knowledge</c> titles) the drafts rely on.</param>
    internal record DraftBundle(
        string Stage,
        string Outcome,
        CaseSummary Summary,
        IReadOnlyList<string> VulnerabilityFlags,
        bool HumanApprovalRequired,
        string HumanApprovalReason,
        string Approver,
        IReadOnlyList<Draft> Drafts,
        string? PartnerNote,
        string? AdviserNote,
        IReadOnlyList<string> Sources);

    private const string Phone = PracticeContacts.Phone;
    private const string Hours = PracticeContacts.Hours;

    private const string AmeliaSignature =
        "Amelia\nSenior Risk Adviser\nCaldova Life Advice · " + PracticeContacts.Address + "\n" + PracticeContacts.Phone + " · " + PracticeContacts.AmeliaEmail;
    private const string CaraSignature =
        "Cara\nClient Communications Specialist\nCaldova Life Advice · " + PracticeContacts.Address + "\n" + PracticeContacts.Phone;

    private const string SpotCheck =
        "Spot-check — routine message with no vulnerability, decline or partial outcome.";
    private const string TheoApproves = "Theo (Practice Team Leader)";
    private const string CaraApproves = "Cara (Client Communications Specialist)";

    // Knowledge document titles (life_knowledge index).
    private const string DocFsg = "Caldova Life Advice — Financial Services Guide and remuneration";
    private const string DocAdviceRules = "Advice rules — best interests duty, SoA and replacement advice";
    private const string DocSuperRules = "Insurance inside super — permitted cover, Protecting Your Super and opt-in rules";
    private const string DocUnderwriting = "Underwriting and the duty to take reasonable care";
    private const string DocClaimsRules = "Claims rules — Life Insurance Code of Practice and income protection benefits";
    private const string DocTax = "Tax on life insurance — premiums and benefits";
    private const string DocFinCrime = "Guide — financial crime red flags and controls for life insurance";
    private const string DocMatrix = "Matrix — authority, approvals and escalation";
    private const string DocInGoodOrder = "Guide — in-good-order checklist for advice and claims";
    private const string DocHarbourline = "Harbourline Life — Protection PDS extract";
    private const string DocCorella = "Corella Life — Protection PDS extract";
    private const string DocLyrebird = "Lyrebird Life — Protection PDS extract";
    private const string DocNorthbank = "Northbank Life — Business Protection PDS extract";

    internal static DraftBundle Build(CaseRecord r) => LifeScenarioCases.KeyOf(r) switch
    {
        LifeScenarioCases.Priya => Priya(r),
        LifeScenarioCases.Grace => Grace(r),
        LifeScenarioCases.George => George(r),
        LifeScenarioCases.Tom => Tom(r),
        LifeScenarioCases.Robert => Robert(r),
        _ => Generic(r)
    };

    /// <summary>
    /// True for the five seeded scenario cases (<c>REQ-yyyyMMdd-LIFnnn</c>),
    /// which are shown at the point Cara writes the outcome. Cases opened
    /// live with the Onboarding Agent are freshly received.
    /// </summary>
    internal static bool IsScenarioCase(CaseRecord r) => LifeScenarioCases.KeyOf(r) is not null;

    // ── 1. Priya — welcome letter, policy summary, outcome-only note to Hannah ─
    private static DraftBundle Priya(CaseRecord r)
    {
        var summary = new CaseSummary(
            SentimentLabel: "Relieved · busy",
            Sentiment:
                "Priya is practical and busy — a physiotherapist with a toddler, a new mortgage and a second baby due in March. " +
                "She'll want to know her family is covered from today, what the back exclusion means in plain words, and that there's nothing else she has to do. " +
                "Hannah only needs to know that cover is in place.",
            WhatHappened:
                "Hannah Lewis (Yarra Home Loans) referred Priya on 3 Nov 2026 after she and Arjun bought their Coburg home. Amelia's advice — SoA signed 6 Nov, " +
                "Authority to Proceed 7 Nov — put in place life cover of $750,000, TPD $600,000, trauma $150,000 and income protection of $6,125 a month with Corella Life. " +
                "Corella accepted life, TPD and trauma at standard rates and offered income protection with an exclusion for back and spine conditions, which Priya accepted " +
                "with Amelia on 17 Nov. Policy CR-26118204 started on Wed 18 Nov 2026. Her Kestrel Retail Super cover was kept until the new cover started.",
            WhatsNext:
            [
                "Send the welcome letter and the plain-English policy summary to Priya — spot-check only.",
                $"Cooling-off: {LifeRules.CoolingOff} from when Priya receives confirmation of the policy (or the end of the fifth business day after it's issued, if earlier) — not once a claim is made.",
                "Send Hannah an outcome-only note — Priya consented to share the outcome, so no cover details and no health details.",
                "Any change to the Kestrel Retail Super cover from here is Priya's decision with Amelia — Cara doesn't suggest one.",
                "Corella will review the back exclusion after two symptom-free years — note the review on Priya's file.",
            ]);

        var welcome = new Draft(
            Id: "client-welcome",
            Audience: "client",
            AudienceLabel: "Welcome letter",
            Channel: "Letter (by email)",
            Recipient: $"{r.ClientName} <{r.ClientEmail}>",
            Subject: "Welcome to Caldova, Priya — your family's cover starts today",
            Body: $"""
                Dear Priya,

                Congratulations on your new home — and welcome to Caldova Life Advice. I'm pleased to confirm that your new cover with Corella Life starts today, **Wednesday 18 November 2026**, so you, Arjun and Asha are protected as your family grows.

                **Your cover at a glance**

                | Cover | Amount | Where it's held |
                |---|---|---|
                | Life cover (with a terminal illness benefit) | $750,000 | Inside super — Corella Life's super plan |
                | TPD cover (total and permanent disability) | $600,000 | Inside super, with an own-occupation extension held outside super and linked to it |
                | Trauma cover | $150,000 | Outside super |
                | Income protection | $6,125 a month after a 90-day waiting period, to age 65 | Outside super |

                Your policy number is **CR-26118204**. I've attached a plain-English summary that explains what each cover does — and the one exclusion on your income protection.

                **What you'll pay**
                - Inside super: **$858 a year**, paid from your Corella Life super plan, which is funded by a rollover from your Riverbend Super account — not from your pay.
                - Outside super: **$2,322 a year ($193.50 a month)** for the own-occupation TPD extension, trauma cover and income protection.
                - Your premiums are stepped, so they'll usually rise each year as you get older.

                **Your existing super cover**
                - Your Riverbend Super death and TPD cover of $210,000 stays in place — we counted it when we worked out how much new cover you need.
                - We kept your $50,000 Kestrel Retail Super cover in place until today, so there was no gap. Please talk to me before you make any change to it.

                **If you change your mind**
                {LifeRules.CoolingOffPlain} Corella's policy documents explain how to cancel.

                **If you ever need to claim**
                Call us first on {Phone}. Ben, our Benefit Claims Advocate, will help you with every step.

                {Complaints("call me or reply to this email")}

                Warm regards,
                {AmeliaSignature}
                """,
            Tone:
                "Warm and congratulatory (a new home, a growing family), then crisp for a busy professional: one table, three short sections, " +
                "and every term — TPD, own occupation, stepped premiums — explained in a few words.",
            Compliance:
            [
                Pass("No new advice", "Confirms the cover Amelia advised in the signed SoA; any change to the Kestrel cover goes back to Amelia."),
                Pass("Facts match the policy", "CR-26118204 from 18 Nov 2026; sums insured and premiums as issued by Corella."),
                Pass("Required disclosures", $"Premiums in dollars (stepped), a cooling-off period of {LifeRules.CoolingOff} (Corporations Act s1019B), how to claim, complaint rights."),
                Pass("Complaint rights", $"Caldova first (written response within {LifeRules.IdrResponse}), then AFCA with contact details."),
                Pass("Health information", "Only Priya's own cover terms — the exclusion is explained in the attached summary."),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        var policySummary = new Draft(
            Id: "client-policy-summary",
            Audience: "client",
            AudienceLabel: "Plain-English policy summary",
            Channel: "Attachment to the welcome letter",
            Recipient: $"{r.ClientName} — attached to the welcome letter",
            Subject: "Your Corella Life cover in plain English — policy CR-26118204",
            Body: $"""
                **Your Corella Life cover in plain English**
                Policy CR-26118204 · starts Wednesday 18 November 2026 · life insured: Priya Natarajan

                **Life cover — $750,000**
                Paid through your super for your family if you die. Corella also pays it early if two doctors, one of them a specialist, certify that you're likely to die within 24 months (Corella's terminal illness benefit). It's there to clear the $680,000 home loan, help with the children's education and give your family time.

                **TPD cover — $600,000**
                Paid if illness or injury means you're unlikely ever to work again.
                - Inside super, the test is whether you could work in any job you're suited to by your education, training or experience.
                - Your own-occupation extension, held outside super and linked to your super cover, means a claim can also be paid if you're unlikely ever to work again as a physiotherapist. A claim is checked against the super cover first, and the two parts never pay more than $600,000 in total.

                **Trauma cover — $150,000**
                Paid if a specialist confirms you have one of the serious conditions listed in the policy, such as cancer, a heart attack or a stroke. It helps with time off work, medical costs and Arjun's time off to look after you. Mental health conditions aren't covered, and the policy sets out the exact definitions.

                **Income protection — up to $6,125 a month**
                Pays a monthly benefit if illness or injury stops you working, once you've been off work for the 90-day waiting period, and for as long as you can't work, up to age 65. It pays the lower of $6,125 and 70% of your average monthly earnings in the 12 months before you stopped work, monthly in arrears. Payments count as taxable income.

                **The one exclusion — back and spine conditions**
                Corella won't pay income protection for back or spine conditions. This is because of the back strain you told Corella about in your application.
                - It applies to income protection only. Your life, TPD and trauma cover are not affected.
                - Everything else about your income protection works as normal.
                - Corella will review the exclusion after two symptom-free years — we'll help you ask for the review. If Corella removes it, your income protection covers back and spine conditions from the review date.

                **What you pay in year 1**

                | | A year |
                |---|---|
                | Inside super — life $468 and TPD $390 | $858 |
                | Outside super — own-occupation TPD extension $222, trauma $640 and income protection $1,460 | $2,322 ($193.50 a month) |
                | **Total** | **$3,180** |

                Premiums are stepped — they usually rise each year as you get older. Income protection premiums held outside super are generally tax-deductible; your tax adviser can confirm how this applies to you.

                **Good to know**
                - Your sums insured go up each year in line with CPI unless you tell Corella you don't want the increase — your premium rises with them.
                - Keep your premiums up to date so your cover stays in place.
                - If you need to claim, call us first on {Phone} — we'll help you with every step.
                - This summary doesn't replace your policy documents. The full terms, definitions and exclusions are in Corella Life's product disclosure statement and your policy schedule.
                """,
            Tone:
                "Plain English, one heading per cover, short sentences. The exclusion gets its own section and is explained without alarm: what it is, why, " +
                "what it doesn't affect and when it will be reviewed.",
            Compliance:
            [
                Pass("Exclusion explained", "What's excluded, why, which cover it affects and the review after two symptom-free years."),
                Pass("Matches the PDS", $"Terminal illness, TPD (super-linked), trauma, income protection and exclusion review as described in {DocCorella}."),
                Pass("Premiums in dollars", "Year-1 split inside and outside super; stepped premiums explained."),
                Pass("Tax stated carefully", "IP payments are taxable income; premiums generally deductible — confirm with a tax adviser."),
                Pass("No new advice", "Describes the cover in place; no recommendations."),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        var hannah = new Draft(
            Id: "partner-update",
            Audience: "partner",
            AudienceLabel: "Referral partner update — outcome only",
            Channel: "Email",
            Recipient: "Hannah Lewis <hannah.lewis@yarrahomeloans.example.com.au> · Yarra Home Loans",
            Subject: "Priya Natarajan — cover in place",
            Body: $"""
                Hi Hannah,

                Thank you again for introducing Priya to us. With her permission, I'm letting you know that the protection cover we recommended for her is now in place, from 18 November 2026.

                Priya asked us to share the outcome only, so I won't go into the details of her cover or her application. If you'd like to know more, she's happy for you to ask her directly.

                It's always a pleasure to look after your clients — thank you for thinking of us.

                Kind regards,
                {AmeliaSignature}
                """,
            Tone: "Warm and brief — a thank-you to a referral partner that shares the outcome and nothing more.",
            Compliance:
            [
                Pass("Consent scope", "Priya's written consent covers the outcome only — no sums insured, premiums or insurer details."),
                Pass("No health information", "Nothing about her application, health disclosures or the exclusion."),
                Pass("Privacy", "No personal details beyond her name and the outcome."),
            ],
            ApprovalRequired: false,
            ApprovalReason: "Spot-check — confirm Priya's consent is on file before sending.");

        var sms = new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: "SMS",
            Recipient: $"{r.ClientName} · {r.ClientPhone}",
            Subject: null,
            Body: $"Caldova: Hi Priya, your Corella Life cover started today (18 Nov). Your welcome letter and policy summary are in your email. Questions? {Phone}",
            Tone: "Short and friendly; points to the letter for the detail.",
            Compliance:
            [
                Pass("Non-sensitive", "No amounts, health details or policy terms by SMS."),
                Pass("Points to the full letter", "The letter carries the cover, premiums, cooling-off and complaint rights."),
                Pass("Contact details", Phone),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        return new DraftBundle(
            Stage: "Cover in force from 18 Nov 2026",
            Outcome: "Completed",
            Summary: summary,
            VulnerabilityFlags: [],
            HumanApprovalRequired: false,
            HumanApprovalReason: "No vulnerability, decline or partial outcome — the drafts can be spot-checked before sending.",
            Approver: "Spot-check by Cara",
            Drafts: [welcome, policySummary, hannah, sms],
            PartnerNote: null,
            AdviserNote: null,
            Sources: [DocCorella, DocUnderwriting, DocSuperRules, DocTax, DocFsg, CaseDoc(r, "underwriting decision")]);
    }

    // ── 2. Grace — call script, reassurance letter, identity protection ───────
    private static DraftBundle Grace(CaseRecord r)
    {
        var summary = new CaseSummary(
            SentimentLabel: "Shocked · scam victim overseas",
            Sentiment:
                "Grace is in Florence and will be shocked to hear that someone pretended to be her — and claimed she has cancer. She's independent and careful, " +
                "so she'll want certainty that her cover is safe, a calm account of what happened and simple steps to protect herself. No jargon, and nothing that " +
                "suggests she did anything wrong.",
            WhatHappened:
                "On Mon 2 Nov 2026 a caller pretending to be Grace passed basic checks and changed her mobile and email (\"I've lost my phone\"), and a new device " +
                "logged in to her portal from Sydney. On Fri 6 Nov — about 93 hours later — an email from the new address made an \"urgent\" trauma claim for the full " +
                "$200,000, to be paid into a new account in another name. Felix found the oncologist's report was fake, the licence had been tampered with and the bank " +
                "statement was suspicious. Theo approved the block: no claim was lodged, Grace's verified contact details were restored, portal access was locked and " +
                "Lyrebird Life's fraud team was alerted.",
            WhatsNext:
            [
                "Cara calls Grace on her verified mobile 0491 570 157 — never the number or email added on 2 Nov — at 6pm Melbourne time (8am in Florence).",
                "After the call, the reassurance letter goes to grace.williams@example.com only — once Theo approves it.",
                "Portal access stays locked until Grace re-verifies; Amelia can help when she's home after 1 Dec.",
                "Her $500,000 life cover and $200,000 trauma cover are unchanged; Daniel and James remain her nominated beneficiaries.",
            ]);

        var callScript = new Draft(
            Id: "call-script",
            Audience: "call",
            AudienceLabel: "Call script — Cara calls Grace",
            Channel: "Phone call — verified mobile only",
            Recipient: "Grace Williams · 0491 570 157 (verified before 2 Nov) — never 0491 570 159",
            Subject: "Call Grace: reassure first, then explain",
            Body: """
                **Before you call**
                - Call **0491 570 157 only** — the number we've held for years. Never the number or email added on 2 November.
                - Grace is in Florence, ten hours behind Melbourne: 6pm here is 8am there.
                - Never ask for passwords, one-time codes or full bank details.

                **1. Introduce yourself and check it's Grace**
                "Hello, is that Grace Williams? It's Cara from Caldova Life Advice in Melbourne — I work with Amelia. Is now a good time to talk for a few minutes?"
                Check it's her with something only she and Amelia would know — for example, the travel dates she gave Amelia at her review on 28 September. Never use anything from the claim email.

                **2. Reassure her first**
                "Before anything else — your cover is safe, and no money has been paid to anyone."

                **3. Explain what happened, simply**
                "On Monday the 2nd, someone phoned us pretending to be you and changed your mobile number and email. This morning we received a claim in your name, asking for your trauma cover to be paid into a new bank account. A number of things didn't add up, so we stopped it straight away."
                Then ask gently: "Can I just check — you haven't asked anyone to make a claim for you?"
                If she asks what the claim said, tell her kindly that it named a serious illness, and that the documents with it were not genuine.

                **4. What we've done**
                - Stopped the claim and the bank change — nothing was sent to Lyrebird Life and nothing was paid.
                - Put back her own mobile and email, and removed the details that were added.
                - Locked online access to her Caldova portal until we can set her up again.
                - Let Lyrebird Life's fraud team know, so they're watching her policy too.
                - Her life cover ($500,000) and trauma cover ($200,000) haven't changed, and Daniel and James are still her beneficiaries.

                **5. Help her protect herself**
                - "Someone may have some of your personal details. IDCARE is a free service that helps people in exactly this situation — I'll put their details in my letter."
                - Suggest changing the passwords on her email and phone accounts, and letting her bank know.
                - "We'll never ask you for a password or a code."

                **6. Close**
                - "I'll email a summary to your usual address, grace.williams@example.com, today."
                - "When you're home after 1 December, Amelia would love to see you and set up your portal access again — or we can do it by phone sooner if you'd like."
                - "Thank you, Grace. You haven't done anything wrong — this happens to careful people too."

                **Don't**
                - Don't use words like "suspicious claim from you", "fraud on your policy" or anything that sounds like she's under suspicion.
                - Don't share the details of Felix's investigation, or read out the fake email address or bank account.
                """,
            Tone:
                "Calm and warm. Reassurance comes first, the explanation is short and blame-free, and the steps are simple. Built for a careful person " +
                "receiving unsettling news far from home.",
            Compliance:
            [
                Pass("Verified channel only", "0491 570 157, held before the change; the new number and email are never used."),
                Pass("Never implies Grace did anything wrong", "She's treated as the person we're protecting — no accusatory words."),
                Pass("Identity check", "Uses a detail from her annual review, not anything the impostor could know from the claim."),
                Pass("No codes or passwords requested", "Says so explicitly."),
                Pass("Facts match the case", "Dates, cover amounts and actions as approved by Theo."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a fraud victim — Cara reviews and approves the script before she calls.");

        var letter = new Draft(
            Id: "client-reassurance",
            Audience: "client",
            AudienceLabel: "Reassurance letter",
            Channel: "Email — verified address only",
            Recipient: $"{r.ClientName} <grace.williams@example.com> — never grace.w.claims@example.net",
            Subject: "Your cover is safe, Grace — what happened and what we've done",
            Body: $"""
                Dear Grace,

                Thank you for speaking with me today, and I'm sorry this news reached you while you're away. The most important thing first: **your cover is safe.** No claim was made on your Lyrebird Life policy, and no money has been paid to anyone.

                **What happened**
                On Monday 2 November someone phoned us pretending to be you and changed the mobile number and email address on your file. On Friday 6 November we received an email from that new address, making a claim on your trauma cover and asking for the money to go into a new bank account. A number of things didn't add up, so we stopped it before anything was sent to Lyrebird Life. When we spoke, you confirmed you hadn't asked for any of this. None of this happened because of anything you did.

                **What we've done**
                - Stopped the claim and the bank change — nothing was lodged and nothing was paid.
                - Put back your own mobile (0491 570 157) and this email address, and removed the details that were added.
                - Locked online access to your Caldova client portal. We'll help you set up new access whenever you're ready.
                - Told Lyrebird Life's fraud team, so they're watching your policy too.
                - From now on, we'll call you on 0491 570 157 before we change any contact or bank details on your file.

                **Your cover hasn't changed**
                - Lyrebird Life policy LB-19004417
                - Life cover: $500,000
                - Trauma cover: $200,000
                - Nominated beneficiaries: Daniel and James, 50% each

                **Steps to protect yourself**
                1. Contact {LifeRules.Idcare}. They can help you check whether your details are being misused elsewhere.
                2. Change the passwords on your email and phone accounts, and turn on multi-factor (two-step) sign-in wherever you can.
                3. Let your bank know that someone may have some of your personal details.
                4. Watch for unexpected messages, calls or logins — even ones that seem to come from us. We will never ask for your password or a one-time code, and this email contains no links.

                **What happens next**
                There's nothing you need to do with your policy. When you're back in Melbourne after 1 December, Amelia would be glad to see you and set up your portal access again — or we can do it by phone sooner if you'd prefer.

                **Questions?**
                Call me on {Phone} ({Hours}), or reply to this email and I'll call you back on 0491 570 157 at a time that suits you in Italy.

                {Complaints("reply to this email")}

                Warm regards,
                {CaraSignature}
                cc: Amelia, Senior Risk Adviser
                """,
            Tone:
                "Reassuring and calm. Leads with \"your cover is safe\", explains what happened in plain words without blame, lists what we've done, " +
                "and gives four simple identity-protection steps.",
            Compliance:
            [
                Pass("Verified channel only", "Sent to grace.williams@example.com; the changed details are never used."),
                Pass("Reassurance first", "Cover safe and nothing paid, in the first paragraph."),
                Pass("Never implies Grace did anything wrong", "\"None of this happened because of anything you did.\""),
                Pass("Identity-protection steps", "IDCARE (free), passwords, bank, scam-safe reminders."),
                Pass("No links or code requests", "Says so explicitly; no links in the email."),
                Pass("Complaint rights", $"Caldova first (within {LifeRules.IdrResponse}), then AFCA."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a fraud victim — Cara approves it before it is sent.");

        var sms = new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: "SMS — verified mobile only",
            Recipient: $"{r.ClientName} · 0491 570 157 — never 0491 570 159",
            Subject: null,
            Body: $"Caldova: Hi Grace, it's Cara. As we discussed, your cover is safe and nothing was paid. My letter is in your usual email. We never ask for codes. {Phone}",
            Tone: "Brief and reassuring; sent after the call, never instead of it. No links.",
            Compliance:
            [
                Pass("Verified channel only", "Her verified mobile — never the number added on 2 Nov."),
                Pass("No links", "Scam-safe wording; points to the email."),
                Pass("Non-sensitive", "No policy, bank or health details."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a fraud victim — Cara approves it before it is sent.");

        return new DraftBundle(
            Stage: "Claim blocked — Grace's cover secured",
            Outcome: "Blocked",
            Summary: summary,
            VulnerabilityFlags:
            [
                "Scam and identity-takeover victim — contact only on her verified details (0491 570 157, grace.williams@example.com).",
                "Travelling overseas (Italy until 1 Dec) — call at a time that suits her; be calm and unhurried.",
                "Never suggest Grace did anything wrong, and don't share the details of the investigation.",
            ],
            HumanApprovalRequired: true,
            HumanApprovalReason: "Every communication to a fraud victim is approved by a person before it is sent — Cara, for her letters (Matrix — authority, approvals and escalation).",
            Approver: CaraApproves,
            Drafts: [callScript, letter, sms],
            PartnerNote: "Grace is an existing client with no referral partner on this case, so there's no partner update. Lyrebird Life's fraud team was alerted by Felix with Theo's approval — that isn't a client communication.",
            AdviserNote: null,
            Sources: [DocFinCrime, DocLyrebird, DocMatrix, CaseDoc(r, "client activity log"), CaseDoc(r, "travel notice")]);
    }

    // ── 3. George & Harpreet — letters and an update to Marcus ────────────────
    private static DraftBundle George(CaseRecord r)
    {
        var summary = new CaseSummary(
            SentimentLabel: "Positive · practical",
            Sentiment:
                "George and Harpreet are practical business owners who finally have a succession plan in place. George may feel a little flat about the loading — " +
                "he'll want to hear that it isn't a judgement and that Northbank will review it. Both will want clear numbers and to know what's left to do.",
            WhatHappened:
                "Marcus Tan (Bayside Business Accountants) referred George and Harpreet on 9 Nov 2026 because Northcote Custom Joinery had no succession plan. " +
                "After Theo's second review (total cover $2,800,000), Amelia advised buy-sell life and TPD cover of $1,200,000 each — each partner owns their own policy — " +
                "and $400,000 of key person cover on George, owned by the company, all with Northbank Life. Harpreet was accepted at standard rates. George's medical results " +
                "led to a 25% loading on his life and TPD cover, which Northbank will review in two years; George accepted it with Amelia on 19 Nov. All three policies " +
                "have been in force since Fri 20 Nov 2026.",
            WhatsNext:
            [
                "Send separate letters to George and Harpreet — each sees only their own cover, and only George's letter mentions his loading.",
                "Update Marcus with their consent: cover in place and the lawyer finalising the buy-sell agreement — no health or underwriting details.",
                "Their lawyer drafts and finalises the buy-sell agreement — Caldova doesn't draft legal documents.",
                "Northbank will review George's loading in two years — note it on his file.",
            ]);

        var georgeLetter = new Draft(
            Id: "client-george",
            Audience: "client",
            AudienceLabel: "Letter to George",
            Channel: "Letter (by email)",
            Recipient: "George Papadopoulos <george@northcotejoinery.example.com.au>",
            Subject: "Your business protection is in place, George",
            Body: $"""
                Dear George,

                I'm pleased to confirm that the business protection we planned for Northcote Custom Joinery is now in place with Northbank Life, from **Friday 20 November 2026**. You and Harpreet now have the cover behind your succession plan.

                **Your cover**

                | Policy | What it's for | Cover | Owner | Premium a year |
                |---|---|---|---|---|
                | NB-26300418 | Buy-sell — life and TPD | $1,200,000 | You | $5,175 |
                | NB-26300420 | Key person — life and TPD | $400,000 | Northcote Custom Joinery Pty Ltd | $1,725, paid by the company |

                Premiums are stepped, so they'll usually rise each year as you get older.

                **About the 25% loading**
                Northbank looked at your medical results — your cholesterol and blood pressure readings — and added a 25% loading to the premiums for your life and TPD cover. A loading isn't a judgement about you. It's simply how an insurer prices a higher risk, and it changes the price of your cover, not what it covers. Northbank will review the loading on request after two years if your results improve, and we'll help you ask for that review. Thank you for talking it through with me on 19 November.

                **The buy-sell agreement**
                Your cover works hand in hand with a buy-sell agreement between you and Harpreet, which sets out how the insurance money is used if one of you dies or becomes totally and permanently disabled. Your lawyer is drafting it — Caldova doesn't prepare legal documents. If anything in the agreement changes the amount of cover you need, please let me know.

                **Tax**
                As the company's accountant, Marcus has confirmed how the key person cover is treated for tax. Please speak with him about anything tax-related.

                **If you change your mind**
                {LifeRules.CoolingOffPlain} Northbank's policy documents explain how to cancel.

                **If you ever need to claim**
                Call us first on {Phone} — Ben, our Benefit Claims Advocate, will help with every step.

                {Complaints("call me or reply to this email")}

                Kind regards,
                {AmeliaSignature}
                """,
            Tone:
                "Warm and businesslike. One table for the numbers; the loading is explained kindly and plainly — what it is, why, that it isn't a judgement, " +
                "and that it will be reviewed.",
            Compliance:
            [
                Pass("Loading explained kindly", "Why, what it changes (price, not cover), and the review in two years."),
                Pass("Health information", "George's own results, in his own letter only."),
                Pass("No legal or tax advice", "Buy-sell agreement with their lawyer; tax with Marcus, their accountant."),
                Pass("Required disclosures", $"Premiums in dollars (stepped), a cooling-off period of {LifeRules.CoolingOff} (Corporations Act s1019B), how to claim, complaint rights."),
                Pass("Facts match the policies", "NB-26300418 and NB-26300420 from 20 Nov 2026; premiums after loading."),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        var harpreetLetter = new Draft(
            Id: "client-harpreet",
            Audience: "client",
            AudienceLabel: "Letter to Harpreet",
            Channel: "Letter (by email)",
            Recipient: "Harpreet Kaur <harpreet@northcotejoinery.example.com.au>",
            Subject: "Your business protection is in place, Harpreet",
            Body: $"""
                Dear Harpreet,

                I'm pleased to confirm that your business protection cover is now in place with Northbank Life, from **Friday 20 November 2026**.

                **Your cover**

                | Policy | What it's for | Cover | Owner | Premium a year |
                |---|---|---|---|---|
                | NB-26300419 | Buy-sell — life and TPD | $1,200,000 | You | $3,100 |

                Northbank accepted your cover at standard rates. Premiums are stepped, so they'll usually rise each year as you get older.

                The company also now holds $400,000 of key person cover on George (policy NB-26300420), which protects the business's profit while a replacement is found and trained.

                **The buy-sell agreement**
                Your cover works hand in hand with the buy-sell agreement between you and George, which sets out how the insurance money is used if one of you dies or becomes totally and permanently disabled. Your lawyer is drafting it — Caldova doesn't prepare legal documents. If anything in the agreement changes the amount of cover you need, please let me know.

                **If you change your mind**
                {LifeRules.CoolingOffPlain} Northbank's policy documents explain how to cancel.

                **If you ever need to claim**
                Call us first on {Phone} — Ben, our Benefit Claims Advocate, will help with every step.

                {Complaints("call me or reply to this email")}

                Kind regards,
                {AmeliaSignature}
                """,
            Tone: "Clear and businesslike — Harpreet runs the numbers for the business and will appreciate a short, precise letter.",
            Compliance:
            [
                Pass("Health information", "Nothing about George's medical results or loading — only Harpreet's own cover, and the company's key person policy."),
                Pass("No legal advice", "The buy-sell agreement stays with their lawyer."),
                Pass("Required disclosures", $"Premium in dollars (stepped), a cooling-off period of {LifeRules.CoolingOff} (Corporations Act s1019B), how to claim, complaint rights."),
                Pass("Facts match the policy", "NB-26300419, $1,200,000, standard rates, $3,100 a year, from 20 Nov 2026."),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        var marcus = new Draft(
            Id: "partner-update",
            Audience: "partner",
            AudienceLabel: "Referral partner update — with consent",
            Channel: "Email",
            Recipient: "Marcus Tan <marcus.tan@baysideaccountants.example.com.au> · Bayside Business Accountants",
            Subject: "Northcote Custom Joinery — business protection in place",
            Body: $"""
                Hi Marcus,

                Thank you for introducing George and Harpreet. With their consent, a short update: the business protection for Northcote Custom Joinery is in place with Northbank Life from 20 November 2026.

                - **Buy-sell:** life and TPD cover of $1,200,000 each for George and Harpreet — 50% each of your $2,400,000 valuation at 30 June 2026 — with each partner owning the policy on their own life.
                - **Key person:** $400,000 of life and TPD cover on George, owned by the company for a revenue purpose, in line with your estimate of the profit at risk.
                - **Buy-sell agreement:** their lawyer is finalising it. It should reflect the sums insured and the ownership above.

                George and Harpreet have asked us to share the cover outcome only, so this note doesn't include any health or underwriting information. They're happy to talk to you directly about anything else.

                Kind regards,
                {AmeliaSignature}
                """,
            Tone: "Professional and concise — the facts an accountant needs for the succession plan, and nothing personal.",
            Compliance:
            [
                Pass("Consent scope", "George and Harpreet consented to Marcus receiving the cover outcome."),
                Pass("No health information", "No medical results, loadings or underwriting details."),
                Pass("No legal advice", "The agreement stays with their lawyer."),
                Pass("Facts match the case", "$2,400,000 valuation at 30 Jun 2026; $1,200,000 each; $400,000 key person; in force 20 Nov 2026."),
            ],
            ApprovalRequired: false,
            ApprovalReason: "Spot-check — confirm both partners' consent is on file before sending.");

        var sms = new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: "SMS",
            Recipient: $"George Papadopoulos · {r.ClientPhone}",
            Subject: null,
            Body: $"Caldova: Hi George, your Northbank Life business protection started on 20 Nov. Amelia's letter with the details is in your email. Questions? {Phone}",
            Tone: "Short and positive.",
            Compliance:
            [
                Pass("Non-sensitive", "No amounts, loading or health details by SMS."),
                Pass("Points to the full letter", "The letter carries the cover, premiums, cooling-off and complaint rights."),
            ],
            ApprovalRequired: false,
            ApprovalReason: SpotCheck);

        return new DraftBundle(
            Stage: "Cover in force from 20 Nov 2026",
            Outcome: "Completed",
            Summary: summary,
            VulnerabilityFlags: [],
            HumanApprovalRequired: false,
            HumanApprovalReason: "Positive outcome; the loading was accepted with Amelia — the drafts can be spot-checked before sending.",
            Approver: "Spot-check by Cara",
            Drafts: [georgeLetter, harpreetLetter, marcus, sms],
            PartnerNote: null,
            AdviserNote: null,
            Sources: [DocNorthbank, DocUnderwriting, DocAdviceRules, DocTax, CaseDoc(r, "underwriting decision")]);
    }

    // ── 4. Tom — Amelia calls first, then a kind letter ($5,600 vs $7,000) ────
    private static DraftBundle Tom(CaseRecord r)
    {
        var summary = new CaseSummary(
            SentimentLabel: "Worried · injured and under financial strain",
            Sentiment:
                "Tom is in pain after shoulder surgery, worried about the bills and frustrated that he can't work. He expected $7,000 a month, so $5,600 will be " +
                "a disappointment. He needs to hear it from Amelia first, then a kind, clear letter with the working, the date of his first payment and the reassurance " +
                "that Kylie can help him with anything.",
            WhatHappened:
                "Tom fell from a ladder on 14 Oct 2026, tore his right rotator cuff and had surgery on 21 Oct; his surgeon expects four to six months off work. He called " +
                "on 11 Nov with Kylie as his support person. Ben checked his Harbourline Life income protection policy (HL-23051877): it pays the lesser of the $7,000 " +
                "insured benefit and 70% of his average monthly earnings in the 12 months before the injury. His net business income for the 12 months to 30 Sep 2026 was " +
                "$96,000 — $8,000 a month — so 70% is $5,600. Felix confirmed the BAS, profit and loss and surgeon's report. Theo approved lodgement; Ben lodged the claim " +
                "on 12 Nov and Harbourline accepted it on 19 Nov at $5,600 a month.",
            WhatsNext:
            [
                "Amelia calls Tom (with Kylie, under his written authority) before anything is written — Theo's request.",
                "The benefit accrues from 13 Nov 2026 and is paid monthly in arrears — first payment about 13 Dec 2026.",
                "Payments are taxable income — Tom's accountant should know.",
                "Ben stays Tom's day-to-day claims contact while he's off work.",
            ]);

        var call = new Draft(
            Id: "call-talking-points",
            Audience: "call",
            AudienceLabel: "Call talking points — Amelia calls Tom first",
            Channel: "Phone call — before the letter",
            Recipient: $"{r.ClientName} · {r.ClientPhone} (Kylie may join as his support person — written authority on file)",
            Subject: "Tell Tom personally: claim accepted at $5,600 a month",
            Body: $"""
                **Purpose:** Tom hears from you, not from a letter, that Harbourline has accepted his claim — and why it's $5,600 a month.

                1. **Check in first.** "How's the shoulder? How are you and Kylie going?" Let him talk.
                2. **The good news.** "Harbourline has accepted your claim, so your payments will start soon."
                3. **The amount, simply.** "Your policy pays up to $7,000 a month, but it's also capped at 70% of what the business earned on average in the 12 months before your accident. Your BAS and profit and loss show $96,000 for the year to 30 September — that's $8,000 a month, and 70% of that is $5,600. So Harbourline will pay $5,600 a month."
                4. **When.** "It counts from 13 November, after your 30-day waiting period, and it's paid monthly in arrears — so the first payment should arrive around 13 December."
                5. **Tax.** "The payments are taxable income, so let your accountant know."
                6. **If he's disappointed.** "I know it's not what you were expecting, and I'm sorry." Don't argue the numbers. "If you think your earnings haven't been counted properly, Ben can ask Harbourline to look again — and you can make a complaint if you're still not happy."
                7. **Support.** "If money's tight before the first payment, the National Debt Helpline on 1800 007 007 offers free, confidential financial counselling."
                8. **Close.** "I'll send you a letter with all of this in writing. Ben looks after your claim day to day, and I'm here too."

                **Not on this call:** don't discuss changing his cover — that's for his next review (see the note for Amelia).
                """,
            Tone: "Personal and kind. Good news first, the reason in one breath, no defensiveness.",
            Compliance:
            [
                Pass("Partial outcome explained", "The 70% earnings limit with Tom's own figures."),
                Pass("No advice on the call", "Cover changes are left for his next review with Amelia."),
                Pass("Support person", "Kylie joins under Tom's written authority."),
                Pass("Free help", "National Debt Helpline offered."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Amelia calls Tom before anything is written — Theo's request after his vulnerable-client review.");

        var letter = new Draft(
            Id: "client-letter",
            Audience: "client",
            AudienceLabel: "Letter — claim accepted at $5,600 a month",
            Channel: "Letter (by email), after Amelia's call",
            Recipient: $"{r.ClientName} <{r.ClientEmail}> · Kylie Bradley may receive a copy under Tom's written authority",
            Subject: "Your income protection claim — accepted at $5,600 a month",
            Body: $"""
                Dear Tom,

                Thank you for talking with me today. I'm sorry you're going through this — a shoulder injury is hard on anyone who works with their hands, and harder when the bills keep coming. Here's everything we discussed, in writing.

                **Harbourline has accepted your claim**
                Harbourline Life has accepted your income protection claim (policy HL-23051877). You'll receive **$5,600 a month** for as long as your shoulder injury keeps you from working and you're under your doctor's care — your policy can pay up to age 65.

                **Why it's $5,600 and not $7,000**
                Your policy pays up to $7,000 a month. It also says the monthly benefit can't be more than 70% of your average monthly earnings in the 12 months before your injury. Harbourline pays the lower of the two.

                | | |
                |---|---|
                | Net business income, 12 months to 30 September 2026 (BAS and profit and loss) | $96,000 |
                | Average a month | $8,000 |
                | 70% of your average monthly earnings | **$5,600** |
                | Your insured benefit | $7,000 |
                | **What Harbourline pays each month** | **$5,600** |

                **When you'll be paid**
                - Your benefit counts from **13 November 2026**, after your 30-day waiting period.
                - It's paid monthly in arrears, so your first payment should arrive around **13 December 2026**, then each month after that.
                - Harbourline may ask for updates from your surgeon while you're off work. Ben will help you with these.

                **Tax**
                Income protection payments are taxable income, so please let your accountant know.

                **If money is tight**
                If things are tight before your first payment, {LifeRules.DebtHelpline} offers free, confidential financial counselling.

                **If you disagree with the amount**
                If you think your earnings haven't been counted properly, tell Ben and he'll ask Harbourline to review it with any extra information you have. You can also make a complaint to Harbourline Life, which must respond in writing within {LifeRules.IdrResponse}. If you're not satisfied with the response, you can contact the Australian Financial Complaints Authority (AFCA), a free and independent service: {LifeRules.AfcaContact}. If you're unhappy with anything we've done, please tell me.

                **Your contacts**
                Ben, our Benefit Claims Advocate, looks after your claim day to day, and I'm here too. Kylie is welcome to call us on your behalf — we have your written authority on file.

                Take care of that shoulder, and please call if you need anything.

                Kind regards,
                {AmeliaSignature}
                """,
            Tone:
                "Kind and respectful. Leads with the good news, explains the lower amount once with Tom's own figures in a simple table, then dates, tax, " +
                "free help and his right to disagree. Nothing defensive or legalistic.",
            Compliance:
            [
                Pass("Partial outcome explained", "Lesser of $7,000 and 70% of average monthly earnings in the 12 months before the injury."),
                Pass("Matches the policy", $"Benefit basis per {DocHarbourline} and {DocClaimsRules}."),
                Pass("Dates", "Accrues from 13 Nov 2026; paid monthly in arrears; first payment about 13 Dec 2026."),
                Pass("Tax stated carefully", "Taxable income — refers him to his accountant."),
                Pass("Complaint rights", $"Harbourline's IDR (within {LifeRules.IdrResponse}), then AFCA with full contact details."),
                Pass("Support person", "Kylie, under Tom's written authority."),
                Pass("No new advice", "Changing his cover isn't mentioned — that's Amelia's call at his next review."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Partial outcome for an injured client under financial stress — Cara approves it, and it goes only after Amelia's call.");

        var sms = new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: "SMS — after Amelia's call",
            Recipient: $"{r.ClientName} · {r.ClientPhone}",
            Subject: null,
            Body: $"Caldova: Hi Tom, it's Amelia. As we discussed, Harbourline accepted your claim at $5,600 a month, first payment about 13 Dec. Details by email. {Phone}",
            Tone: "Plain and kind; sent after the call, never instead of it.",
            Compliance:
            [
                Pass("Non-sensitive", "No health or bank details."),
                Pass("Points to the full letter", "The letter carries the working, tax and complaint rights."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Message to a vulnerable client — Cara approves it, after Amelia's call.");

        return new DraftBundle(
            Stage: "Claim accepted 19 Nov 2026 — $5,600 a month",
            Outcome: "Partially approved",
            Summary: summary,
            VulnerabilityFlags:
            [
                "Injury and financial stress — self-employed, with a young family; Amelia calls before anything is written.",
                "Partial outcome: $5,600 a month against $7,000 insured — the letter explains why and includes his right to complain.",
                "Kylie is Tom's support person under his written authority.",
            ],
            HumanApprovalRequired: true,
            HumanApprovalReason: "Messages to an injured client with a partial outcome are approved by a person before they're sent — Cara approves her letters, and Amelia calls first at Theo's request.",
            Approver: CaraApproves + ", after Amelia's call",
            Drafts: [call, letter, sms],
            PartnerNote: "Tom is an existing client (since 2023) with no referral partner on this case, so there's no partner update.",
            AdviserNote:
                "Not shared with Tom: his insured benefit ($7,000 a month) is more than his recent income supports " +
                "($5,600 a month at 70% of $8,000). At his next review Amelia may consider whether the benefit should change. The adviser decides.",
            Sources: [DocHarbourline, DocClaimsRules, DocTax, CaseDoc(r, "earnings evidence"), CaseDoc(r, "benefit calculation")]);
    }

    // ── 5. Robert — Amelia calls first, a gentle letter and a super checklist ─
    private static DraftBundle Robert(CaseRecord r)
    {
        var summary = new CaseSummary(
            SentimentLabel: "Grieving · bereaved family",
            Sentiment:
                "Robert lost his wife suddenly six weeks ago and is caring for Lily and Max on his own. He's organised but exhausted. He needs gentleness, " +
                "one familiar person (Amelia), clear confirmation that the money has arrived, and a simple list for the super claim — nothing he has to chase.",
            WhatHappened:
                "Robert emailed Amelia on 13 Nov 2026 with a certified copy of Mei's death certificate and his ID. Theo took compassionate oversight: Amelia is the " +
                "family's one named contact and Theo approves every message. Felix confirmed the documents. Ben confirmed Mei's Corella Life policy (CR-19033105, " +
                "life cover $1,000,000) was in force with premiums paid, no exclusions and a valid nomination of Robert (100%), so the benefit is paid directly to " +
                "him, not through Mei's estate. Ben lodged the claim on 16 Nov after Theo's approval; Corella accepted it on 20 Nov and paid $1,000,000 to Robert on " +
                "23 Nov 2026. Ben also found $180,000 of default death cover in Mei's Riverbend Super account — a separate claim with the fund's trustee.",
            WhatsNext:
            [
                "Amelia calls Robert first; the letter follows — Theo approves every message to the family.",
                "Riverbend Super claim ($180,000): Ben helps Robert with the fund's claim form and documents; the trustee pays it under the fund's rules.",
                "Nothing more is needed on the Corella claim — it's complete.",
            ]);

        var call = new Draft(
            Id: "call-talking-points",
            Audience: "call",
            AudienceLabel: "Call talking points — Amelia calls Robert first",
            Channel: "Phone call — before the letter",
            Recipient: $"{r.ClientName} · {r.ClientPhone}",
            Subject: "Let Robert know, gently, that Mei's cover has been paid",
            Body: $"""
                **Purpose:** Robert hears from you, gently, that Mei's life cover has been paid — and that the super claim is in hand.

                - **Open gently.** "Hi Robert, it's Amelia. Is now an OK time? There's no rush at all."
                - **The payment.** "I wanted you to hear it from me: Corella has paid Mei's life cover — $1,000,000 — into your account today."
                - **Why it came to you directly.** "Because Mei nominated you, it's paid to you directly. It doesn't have to wait for her estate."
                - **Tax.** "It isn't taxable."
                - **The super claim.** "Mei also had $180,000 of cover with Riverbend Super. That's a separate claim with the fund. Ben will help you with it, one step at a time — I'll send a short checklist so you don't have to remember anything from this call."
                - **Ask, don't assume.** "Is there anything you need from us right now?"
                - **Close.** "I'm your contact for anything at all. Call me any time on {Phone}."

                **Don't:** no jargon, no other products or reviews, and don't rush him. If he'd rather talk another day, offer to call back.
                """,
            Tone: "Gentle and unhurried. Short sentences; silence is fine.",
            Compliance:
            [
                Pass("One named contact", "Amelia."),
                Pass("Facts match the case", "$1,000,000 paid to Robert on 23 Nov 2026; Riverbend Super $180,000."),
                Pass("No sales or advice", "Nothing about other cover or products."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a bereaved family — Theo approves every message; Amelia makes the call.");

        var letter = new Draft(
            Id: "client-letter",
            Audience: "client",
            AudienceLabel: "Letter — Mei's life cover paid",
            Channel: "Letter (by email), after Amelia's call",
            Recipient: $"{r.ClientName} <{r.ClientEmail}>",
            Subject: "Mei's life cover — paid to you on 23 November",
            Body: $"""
                Dear Robert,

                I'm so sorry for the loss of Mei. Thank you for letting us know, and for sending what we needed at such a hard time. I wanted to confirm in writing what we talked about today.

                **Mei's life cover has been paid**
                Corella Life has paid Mei's life cover of **$1,000,000** into your account, on **Monday 23 November 2026**.
                - Because Mei nominated you as her beneficiary, it's paid to you directly — it doesn't go through her estate.
                - It isn't taxable.
                - There's nothing more you need to do for this claim.

                **Mei's super — a separate claim**
                Mei also had **$180,000** of death cover through her Riverbend Super account. This is a separate claim with Riverbend Super's trustee, who will pay it under the fund's rules. Because you were Mei's husband, a super death benefit paid to you as a lump sum is tax-free. Ben, from our Claims team, will help you with each step — there's no rush, and he'll do as much of the chasing as he can.

                Here's a short checklist. Riverbend Super will confirm exactly what it needs, and Ben will help you complete the form:
                - [ ] Riverbend Super's death benefit claim form — Ben will send it to you, with as much filled in as he can
                - [ ] A certified copy of Mei's death certificate — you already have this
                - [ ] Your certified ID — you already have this
                - [ ] Evidence of your relationship to Mei, such as your marriage certificate, if the fund asks for it
                - [ ] A bank account in your own name for the payment

                **Who to contact**
                I'm your contact for anything at all — please call me on {Phone} or reply to this email. Ben will look after the Riverbend Super claim with you.

                **If anything isn't right**
                If you're ever unhappy with how we've helped, please tell me and I'll put it right. We'll respond in writing within {LifeRules.IdrResponse}, and if you're still not satisfied you can contact the Australian Financial Complaints Authority (AFCA), a free and independent service: {LifeRules.AfcaContact}.

                With deepest sympathy,
                {AmeliaSignature}
                """,
            Tone:
                "Gentle and unhurried: condolences first, the payment confirmed in three short lines, then a simple checklist so nothing has to be remembered. " +
                "One name and one number. No process jargon.",
            Compliance:
            [
                Pass("Condolences first", "Opens with sympathy before any detail."),
                Pass("Facts match the case", "$1,000,000 paid 23 Nov 2026 to Robert as nominated beneficiary — not through the estate."),
                Pass("Tax stated carefully", $"Life cover paid outside super isn't taxable; a super lump sum to a spouse is tax-free (ITAA 1997 s302-60) — per {DocTax}."),
                Pass("Super claim explained", "Separate claim with Riverbend Super's trustee, under the fund's rules; checklist caveated."),
                Pass("One named contact", $"Amelia on {Phone}."),
                Pass("Complaint rights", "Given gently, at the end."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a bereaved family — Theo approves every word before it is sent.");

        var sms = new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: "SMS — after Amelia's call",
            Recipient: $"{r.ClientName} · {r.ClientPhone}",
            Subject: null,
            Body: $"Caldova: Robert, it's Amelia. As we discussed, Mei's life cover has been paid into your account. My letter is in your email. Call me anytime on {Phone}",
            Tone: "Gentle; sent after the call, never instead of it.",
            Compliance:
            [
                Pass("Non-sensitive", "No amounts or bank details by SMS."),
                Pass("One named contact", "Amelia."),
            ],
            ApprovalRequired: true,
            ApprovalReason: "Communication to a bereaved family — Theo approves it.");

        return new DraftBundle(
            Stage: "Claim paid 23 Nov 2026 — super claim under way",
            Outcome: "Completed",
            Summary: summary,
            VulnerabilityFlags:
            [
                "Bereavement — Mei died suddenly on 12 Oct 2026; Robert is caring for Lily (12) and Max (9).",
                "One named contact (Amelia); Theo approves every message to the family.",
                "Keep it simple: the super claim checklist means Robert has nothing to remember or chase.",
            ],
            HumanApprovalRequired: true,
            HumanApprovalReason: "Every communication to a bereaved family is approved by Theo before it is sent (Matrix — authority, approvals and escalation).",
            Approver: TheoApproves,
            Drafts: [call, letter, sms],
            PartnerNote: "Robert and Mei are existing clients (since 2019) with no referral partner on this case, so there's no partner update.",
            AdviserNote:
                "Corella must suggest independent financial advice for an accepted lump sum of $25,000 or more " +
                "(Life Code cl 5.61). No review or product conversation now — when Robert is ready, Amelia may offer to look at his own cover " +
                "and the family's needs. The adviser decides when, and whether.",
            Sources: [DocCorella, DocClaimsRules, DocTax, DocSuperRules, CaseDoc(r, "policy schedule and nomination"), CaseDoc(r, "super fund cover note")]);
    }

    // ── Request received — cases opened live with the Onboarding Agent ────────
    private static DraftBundle Generic(CaseRecord r)
    {
        var first = FirstName(r.ClientName);
        var type = string.IsNullOrWhiteSpace(r.RequestType) ? "request" : r.RequestType;
        var kind = KindOf(type);
        var hasPartner = HasReferralPartner(r);
        var isClaim = kind is "death" or "claim" or "claim-bank";

        var flags = new List<string>();
        if (kind == "death")
            flags.Add("Bereavement — condolences first; one named contact (Amelia); every message approved by Theo.");
        if (kind is "claim" or "claim-bank")
            flags.Add("Illness or injury — plain English and kindness; check for financial stress; messages approved by a person.");
        if (kind == "claim-bank")
            flags.Add("Bank-detail change with a claim — write only to the contact details verified before any recent change, and never confirm the new bank details.");
        if (string.Equals(r.Priority, "Urgent", StringComparison.OrdinalIgnoreCase))
            flags.Add($"Urgent case — {r.PriorityReason}");

        var approvalRequired = isClaim || flags.Count > 0;

        var opener = kind switch
        {
            "death" => $"Dear {first},\n\nI'm so sorry for your loss. Thank you for letting us know — we'll take care of this with you, one step at a time.",
            "claim" or "claim-bank" => $"Dear {first},\n\nI'm sorry you're going through this. Thank you for letting us know — we'll help you with your claim so you can focus on your health.",
            "business" => $"Dear {first},\n\nThank you for getting in touch about protecting your business. We're glad to help.",
            "advice" => $"Dear {first},\n\nThank you for getting in touch about protecting your family. We're glad to help.",
            _ => $"Dear {first},\n\nThank you for your request — it's with us and we're on it."
        };

        string[] steps = kind switch
        {
            "death" =>
            [
                "We'll check the documents we have — a certified death certificate and identification — and let you know straight away if we need anything else.",
                "We'll check the policy, who it's paid to, and whether there's other cover, such as cover inside super.",
                "We'll explain what happens next in plain English before anything is lodged — and I'll be your one contact the whole way through.",
            ],
            "claim" or "claim-bank" =>
            [
                "Ben, our Benefit Claims Advocate, will check your policy — the definition, any waiting period and what the insurer needs from you.",
                "If we need anything else, such as a report from your doctor or proof of earnings, we'll ask you once, clearly.",
                "We'll only lodge your claim with the insurer once a member of our team has approved it, and we'll keep you updated at every step.",
            ],
            "business" =>
            [
                "We'll start with a conversation about your business — its value, who owns it and who it depends on.",
                "We'll work out how much cover is needed and compare the insurers on our approved product list.",
                "Amelia, our Senior Risk Adviser, will give you her advice in a Statement of Advice, and nothing happens until you're comfortable.",
            ],
            "advice" =>
            [
                "We'll start with a fact find — your income, debts, family and any cover you already have, including cover inside super.",
                "We'll work out how much cover your family needs and compare the insurers on our approved product list.",
                "Amelia, our Senior Risk Adviser, will give you her advice in a Statement of Advice, and nothing happens until you're comfortable.",
            ],
            _ =>
            [
                "We'll review your request and let you know if we need anything else.",
                "If we need anything, we'll ask once, clearly, and explain why.",
                "We'll let you know the outcome in plain English.",
            ]
        };
        var stepsText = string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));

        var extra = kind switch
        {
            "claim-bank" =>
                "\n\n**Didn't make this request?**\n" +
                $"Please call us straight away on {Phone}. We'll never ask for a password or a one-time code, and we only pay benefits to the policy owner's verified account.",
            "advice" or "business" =>
                "\n\n**How we're paid**\n" +
                "Our Financial Services Guide explains our services and how we're paid, including any commission from insurers — we'll always show it to you in dollars before you decide.",
            _ => string.Empty
        };

        var summary = new CaseSummary(
            SentimentLabel: kind switch
            {
                "death" => "Grieving",
                "claim" or "claim-bank" => "Worried · unwell or injured",
                "business" or "advice" => "Positive · getting organised",
                _ => "Neutral"
            },
            Sentiment: kind switch
            {
                "death" => "A bereaved family member — be gentle, keep it simple and give one contact.",
                "claim" => "A client dealing with illness or injury — be kind and practical, and make the claim feel manageable.",
                "claim-bank" => "We can't yet be sure the policy owner made this request — write only to verified details and keep it factual and protective.",
                "business" => "Business owners planning ahead — practical, clear about the process and how we're paid.",
                "advice" => "A family planning ahead — warm, and clear about what happens next.",
                _ => "First contact on this request — keep it clear and set expectations."
            },
            WhatHappened: $"{r.ClientName}'s request ({type}, {r.CaseNumber}) was received on {PracticeDates.Display(r.ReceivedDate)} and opened by Olivia. {r.RequestSummary}",
            WhatsNext:
            [
                isClaim
                    ? "Ben checks the policy and evidence; Felix checks the documents (and any bank change) before anything is lodged."
                    : "Olivia completes the fact find and in-good-order checklist; Penny starts the needs analysis.",
                $"Cara acknowledges the request today by {(string.IsNullOrWhiteSpace(r.PreferredContact) ? "email" : r.PreferredContact)}" +
                    (hasPartner && !isClaim ? $", and updates {r.ReferredBy} with the client's consent." : "."),
                "If anything is missing, we ask once — clearly and with a reason.",
            ]);

        var drafts = new List<Draft>
        {
            new(
                Id: "client-acknowledgement",
                Audience: "client",
                AudienceLabel: "Acknowledgement — request received",
                Channel: "Email",
                Recipient: kind == "claim-bank"
                    ? $"{r.ClientName} <{r.ClientEmail}> — verified details on file only"
                    : $"{r.ClientName} <{r.ClientEmail}>",
                Subject: $"We've received your request — {r.CaseNumber}",
                Body: $"""
                    {opener}

                    **Your request**
                    - Reference: {r.CaseNumber}
                    - Type: {type}
                    - Client number: {r.ClientNumber}

                    **What happens next**
                    {stepsText}{extra}

                    **Questions?**
                    Reply to this email or call us on {Phone} ({Hours}).

                    {Complaints("reply to this email")}

                    Kind regards,
                    {(kind == "death" ? AmeliaSignature : CaraSignature)}
                    """,
                Tone: kind switch
                {
                    "death" => "Gentle and simple, with condolences first and one named contact.",
                    "claim" => "Kind and practical; makes the claim feel manageable.",
                    "claim-bank" => "Calm, factual and protective — nothing the requester could misuse.",
                    _ => "Clear and friendly; sets expectations."
                },
                Compliance: GenericCompliance(kind),
                ApprovalRequired: approvalRequired,
                ApprovalReason: approvalRequired
                    ? "Sensitive case — a person reviews the message before it is sent."
                    : SpotCheck),
        };

        if (hasPartner && !isClaim)
        {
            var partnerFirst = FirstName(r.ReferredBy);
            drafts.Add(new Draft(
                Id: "partner-update",
                Audience: "partner",
                AudienceLabel: "Referral partner update — with consent",
                Channel: "Email",
                Recipient: r.ReferredBy,
                Subject: $"Thank you for referring {r.ClientName}",
                Body: $"""
                    Hi {partnerFirst},

                    Thank you for referring {r.ClientName} to Caldova Life Advice. With {FirstName(r.ClientName)}'s consent, I'm letting you know we've received the referral, and I'll be in touch with {FirstName(r.ClientName)} shortly.

                    We'll only share what {FirstName(r.ClientName)} is happy for us to share — never health information.

                    Kind regards,
                    {AmeliaSignature}
                    """,
                Tone: "Brief and appreciative.",
                Compliance:
                [
                    Pass("Consent scope", "Receipt of the referral only."),
                    Pass("No health information", "Nothing about the client's circumstances or health."),
                ],
                ApprovalRequired: false,
                ApprovalReason: "Spot-check — confirm the client's consent is on file before sending."));
        }

        var smsBody = kind switch
        {
            "death" => $"Caldova: {first}, we're so sorry for your loss. We've received your message ({r.CaseNumber}) and emailed you what happens next. {Phone}",
            "claim-bank" => $"Caldova: We've received a claim request on your policy ({r.CaseNumber}). If this wasn't you, call us now on {Phone}.",
            _ => $"Caldova: Hi {first}, we've received your request ({r.CaseNumber}). Details are in your email. Questions? {Phone}"
        };

        drafts.Add(new Draft(
            Id: "sms",
            Audience: "sms",
            AudienceLabel: "SMS",
            Channel: kind == "claim-bank" ? "SMS — verified mobile only" : "SMS",
            Recipient: $"{r.ClientName} · {r.ClientPhone}",
            Subject: null,
            Body: smsBody,
            Tone: kind == "claim-bank" ? "Short and protective — invites a call if the client didn't make the request." : "Short acknowledgement.",
            Compliance:
            [
                Pass("Non-sensitive", "Reference number only — no health or bank details."),
                Pass("Points to the full message", "The email carries next steps and complaint rights."),
            ],
            ApprovalRequired: approvalRequired,
            ApprovalReason: approvalRequired ? "Sensitive case — a person reviews it before sending." : SpotCheck));

        var sources = isClaim
            ? new List<string> { DocClaimsRules, DocInGoodOrder, DocMatrix }
            : new List<string> { DocFsg, DocAdviceRules, DocInGoodOrder };
        if (kind == "claim-bank") sources.Add(DocFinCrime);

        return new DraftBundle(
            Stage: "Request received — with " + (isClaim ? "Claims" : "Onboarding & Requests"),
            Outcome: "In progress",
            Summary: summary,
            VulnerabilityFlags: flags,
            HumanApprovalRequired: approvalRequired,
            HumanApprovalReason: approvalRequired
                ? "Sensitive case — these drafts are reviewed by a person before anything is sent."
                : "Routine acknowledgement — the drafts can be spot-checked before sending.",
            Approver: !approvalRequired ? "Spot-check by Cara" : kind == "death" ? TheoApproves : CaraApproves,
            Drafts: drafts,
            PartnerNote: hasPartner
                ? (isClaim ? "Claims are never shared with a referral partner without the client's specific consent — no partner update." : null)
                : "No referral partner on this case, so there's no partner update.",
            AdviserNote: null,
            Sources: sources);
    }

    private static IReadOnlyList<ComplianceCheck> GenericCompliance(string kind)
    {
        var checks = new List<ComplianceCheck>
        {
            Pass("No personal advice", "Explains the process only — Amelia gives the advice."),
            Pass("Next steps", "What happens next, in order."),
            Warn("Timeframes", "No firm dates yet — confirm once the case is assessed."),
            Pass("Contact details", Phone),
            Pass("Complaint rights", $"Caldova first (within {LifeRules.IdrResponse}), then AFCA."),
        };
        if (kind is "advice" or "business")
            checks.Add(Pass("How we're paid", "Points to the FSG; commission shown in dollars before the client decides."));
        if (kind == "claim-bank")
            checks.Add(Pass("Verified channel only", "Sent to the details on file; invites a call if the client didn't make the request."));
        return checks;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Complaints(string howToTellUs) =>
        "**If you're unhappy**\n" +
        $"Please tell us first — {howToTellUs} — and we'll do our best to put it right. We'll respond in writing within {LifeRules.IdrResponse}. " +
        "If you're not satisfied with our response, or we haven't responded in time, you can contact the Australian Financial Complaints Authority (AFCA), " +
        $"a free and independent service: {LifeRules.AfcaContact}.";

    private static string CaseDoc(CaseRecord r, string document) => $"Case {r.CaseNumber} — {document}";

    private static ComplianceCheck Pass(string label, string note) => new(label, "pass", note);
    private static ComplianceCheck Warn(string label, string note) => new(label, "warn", note);

    /// <summary>True when a referral partner (not "Existing client") sent the case.</summary>
    internal static bool HasReferralPartner(CaseRecord r) =>
        !string.IsNullOrWhiteSpace(r.ReferredBy) &&
        !r.ReferredBy.TrimStart().StartsWith("Existing", StringComparison.OrdinalIgnoreCase) &&
        !r.ReferredBy.TrimStart().StartsWith("None", StringComparison.OrdinalIgnoreCase);

    private static string KindOf(string requestType)
    {
        var t = (requestType ?? string.Empty).ToLowerInvariant();
        if (t.Contains("death")) return "death";
        if (t.Contains("claim") && t.Contains("bank")) return "claim-bank";
        if (t.Contains("claim")) return "claim";
        if (t.Contains("business")) return "business";
        if (t.Contains("advice") || t.Contains("protection") || t.Contains("cover")) return "advice";
        return "general";
    }

    internal static string FirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "there";
        var first = fullName.Trim().Split(' ', 2)[0];
        return string.IsNullOrWhiteSpace(first) ? "there" : first;
    }
}
