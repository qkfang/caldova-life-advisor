# Scenario 1: New Family Protection Advice

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Scenario Summary

A mortgage broker refers a young family who have just bought a home. Priya wants to know that her partner and children could keep the house if something happened to her. The scenario follows new advice from the first conversation to a policy in force:

- a referral and fact find turned into a Client Case in one pass
- an existing-cover check that catches super cover about to stop
- a needs analysis with every step of the working shown
- research across the insurers on Life Advice Guru's approved product list (APL), with stepped and level premiums
- a Statement of Advice (SoA) drafted for Amelia, with the commission shown in dollars
- underwriting, with the duty to take reasonable care explained first, and an exclusion the client decides on with her adviser
- a referral-partner update that shares only what Priya agreed to share

Amelia gives the advice and signs the SoA. Priya makes every decision about her cover.

## At a Glance

| Item | Detail |
|---|---|
| Case number | `REQ-20261103-LIF001` |
| Request type | New protection advice |
| Received | Tue 3 Nov 2026, by email from the referral partner, with Priya's consent |
| Priority | High: existing cover at risk. Priya's Kestrel Retail Super cover stops on 30 Nov 2026 unless she elects in writing to keep it |
| Client | Priya Natarajan (DOB 14/02/1992, age 34), client number `CL-104582` |
| Adviser | Amelia, Senior Risk Adviser |
| Referred by | Hannah Lewis, mortgage broker, Yarra Home Loans |
| Trigger | Bought a home in October 2026 with a joint mortgage of $680,000 |
| Existing cover | Riverbend Super: default death and TPD cover of $210,000, kept. Kestrel Retail Super: default death cover of $50,000, due to stop on 30 Nov 2026, kept until the new cover started |
| Cover put in place | Life $750,000 · TPD $600,000 · trauma $150,000 · income protection $6,125 a month (90-day waiting period, benefit period to age 65) |
| Insurer and policy | Corella Life, policy `CR-26118204`, from Wed 18 Nov 2026 |
| Premium | $3,180 in year 1 (stepped): $858 a year inside super and $2,322 a year outside super |
| Outcome | ✅ **Completed**: covered from 18 Nov 2026 |

---

## Client and Family

**Priya Natarajan** is a new Life Advice Guru client.

- She is 34 and a physiotherapist at a private clinic in Brunswick. She earns $105,000 a year plus super, and she is a non-smoker.
- She lives in Coburg with her partner, Arjun Mehta (36, a software engineer), and their daughter Asha (2). Their second baby is due in March 2027.
- Arjun has default cover in his own super fund. His cover isn't part of this case.
- Contact: priya.natarajan@example.com · 0491 570 156.
- She is organised and practical, and short on time.

> "We've just taken on a big mortgage. If something happened to me, I want Arjun and the kids to be able to stay in the house."

## Life Event

In October 2026 Priya and Arjun bought a home in Coburg with a joint mortgage of $680,000. Their mortgage broker, Hannah Lewis of Yarra Home Loans, suggested they get advice on protecting the loan and the family. With Priya's consent, Hannah refers her to Life Advice Guru on Tue 3 Nov 2026.

Priya's fact find shows:

| Item | Detail |
|---|---|
| Goals | Keep the home and the family's lifestyle if she dies, is disabled, falls seriously ill or can't work. Up to $300 a month for premiums from cash flow |
| Dependants | Arjun (36), Asha (2), and a baby due in March 2027 |
| Debts | Joint mortgage of $680,000 |
| Savings | $45,000 |
| Income | $105,000 a year plus super |
| Super | **Riverbend Super:** balance $96,400, with default death and TPD cover of $210,000. **Kestrel Retail Super** (an old account): balance $8,200, with default death cover of $50,000 and no contributions since 30 Jul 2025 |
| Health | Non-smoker. Mild asthma, well controlled with a preventer inhaler. A lower back strain in 2023: six weeks of physiotherapy and a full recovery |

---

## Request Journey

### 1. Referral Received — Onboarding Agent (Olivia)

- The **Fact Find Extraction** assistant reads Hannah's referral email and Priya's fact-find questionnaire. It fills in the Client Case: goals, dependants, debts, income, occupation, health basics and existing cover. Health details are marked sensitive.
- The **In-Good-Order Checklist** assistant runs the checklist from *Guide — in-good-order checklist for advice and claims*:

| Check | Result |
|---|---|
| Financial Services Guide (FSG) given | ✓ |
| Privacy and health-information consent | ✓ Signed |
| Identification | ✓ Driver licence and payslip attached. Sent to Financial Crime |
| Authority to obtain existing policy details | ✓ Signed for Riverbend Super and Kestrel Retail Super |
| Referral consent | ✓ Priya agreed to the referral, and to Hannah hearing the outcome only |
| Commission and fee consent | ⏳ Collected with the SoA |
| Support-person authority | Not needed |
| Existing cover | ⚠ **Kestrel Retail Super** has had no contributions since 30 Jul 2025. Under Protecting Your Super, a fund can't keep insuring an account that has received nothing for 16 continuous months unless the account holder elects in writing to keep the cover. So its $50,000 death cover stops on **30 Nov 2026** unless Priya tells the fund in writing to keep it |

- It sets the priority to **High**, because existing cover is at risk: the Kestrel deadline is 27 days away, and the family has a new mortgage with no cover outside super.
- It opens case `REQ-20261103-LIF001` with client number `CL-104582`. It routes the case to Financial Crime, which checks every new client, and to Paraplanning for the new advice.

> Olivia checks the extraction against the fact find and makes sure the Kestrel deadline is at the top of Penny's list.

---

### 2. Identity and KYC — Financial Crime Agent (Felix)

- The **Document Authenticity** assistant scans Priya's documents with Azure Content Understanding:

| Document | Verdict |
|---|---|
| Driver licence | ✓ Legit |
| Payslip | ✓ Legit |

- KYC is complete. There is no politically exposed person (PEP) or sanctions match.
- The **Risk Timeline** assistant finds nothing unusual for a new client. Result: **clear**.

> Felix accepts the clear result. No further checks are needed.

---

### 3. Needs Analysis — Paraplanning Agent (Penny)

The **Needs Analysis** assistant works out how much cover Priya's family needs and shows every step for Penny to check.

**Life cover**

| Step | Amount |
|---|---|
| Pay off the mortgage | $680,000 |
| Children's education (2 × $50,000) | $100,000 |
| Family income support ($40,000 a year × 5 years) | $200,000 |
| Final expenses and emergency fund | $30,000 |
| **Total need** | **$1,010,000** |
| Less savings | −$45,000 |
| Less existing death cover in Riverbend Super | −$210,000 |
| **Shortfall** | **$755,000** |
| **Recommended for Amelia's review** | **$750,000** |

The Kestrel Retail Super cover isn't counted, because it is only kept until the new cover starts.

**TPD cover**

| Step | Amount |
|---|---|
| Pay off the mortgage | $680,000 |
| Rehabilitation and home modifications | $100,000 |
| Emergency fund | $30,000 |
| **Total need** | **$810,000** |
| Less existing TPD cover in Riverbend Super | −$210,000 |
| **Recommended** | **$600,000** |

**Trauma cover**

| Step | Amount |
|---|---|
| Six months' income (half of $105,000) | $52,500 |
| Medical costs and Arjun's time off work | $50,000 |
| Debt reduction | $50,000 |
| **Total need** | **$152,500** |
| **Recommended** | **$150,000** |

**Income protection**

| Step | Amount |
|---|---|
| 70% of $105,000 | $73,500 a year |
| **Monthly benefit** | **$6,125 a month** |
| Waiting period | 90 days |
| Benefit period | To age 65 |

For new income protection policies, APRA expects all benefits combined to be no more than 90% of earnings for the first six months of a claim, and 70% after that. The draft insures 70% of Priya's income.

**How to hold the cover.** The assistant drafts an ownership structure for Amelia, citing *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules* and *Tax on life insurance — premiums and benefits*:

| Cover | Held | Why |
|---|---|---|
| Life $750,000 | Inside super, in Corella Life's super plan | Premiums are funded by rollover from Riverbend Super, so they don't come out of the family's cash flow |
| TPD $600,000 (any occupation) | Inside super, in Corella Life's super plan | Super can only insure events that match a condition of release, such as permanent incapacity. Any-occupation TPD fits (SIS reg 4.07D) |
| Own-occupation TPD extension | Outside super, super-linked | New own-occupation TPD can't be held inside super (SIS reg 4.07D). The extension pays if Priya can't work as a physiotherapist again |
| Trauma $150,000 | Outside super | New trauma cover can't be held inside super (SIS reg 4.07D) |
| Income protection $6,125 a month | Outside super | Premiums for income protection that protects her salary are tax-deductible when paid outside super |

**The Kestrel Retail Super cover.** Penny's agent recommends that Priya tells Kestrel Retail Super to keep its $50,000 death cover until the new cover is in force, so she is never without cover. Cancelling or replacing existing cover is a human decision. Amelia talks it through with Priya, and Priya tells the fund in writing to keep the cover before 30 Nov 2026.

> Penny checks the working twice and agrees the numbers.

---

### 4. Research — Insurance Research Agent (Ivy)

- The **Policy Comparison** assistant reads each insurer's product sheet (`research_analysePolicyDocument`), citing *Corella Life — Protection PDS extract*, *Harbourline Life — Protection PDS extract* and *Lyrebird Life — Protection PDS extract*. It compares the definitions, features and exclusions that matter for Priya (`research_compareInsurers`). The key difference: Corella Life's super-linked own-occupation TPD extension covers physiotherapists.
- The **Premium Quotes** assistant compares year-1 stepped premiums for the full package, following *Life Advice Guru — Approved product list and research method*:

| Insurer | Year-1 premium (stepped, full package) | Notes |
|---|---|---|
| **Corella Life** | **$3,180** | Lowest cost, and own-occupation TPD for physiotherapists. Shortlisted for Amelia |
| Harbourline Life | $3,412 | |
| Lyrebird Life | $3,690 | |

- **Stepped or level.** Corella Life's level-premium option costs $4,050 in year 1. Stepped premiums start lower and rise as Priya gets older. Level premiums start higher and rise more slowly. The comparison workbook and chart show both over time (`research_generateComparisonWorkbook`).
- **The Corella Life package, split by where it is held:**

| Held | Cover | Year-1 premium |
|---|---|---|
| Inside super (Corella Life's super plan) | Life $750,000 | $468 |
| Inside super | TPD $600,000 (any occupation) | $390 |
| | **Inside super subtotal** | **$858 a year** |
| Outside super | Own-occupation TPD extension (super-linked) | $222 |
| Outside super | Trauma $150,000 | $640 |
| Outside super | Income protection $6,125 a month | $1,460 |
| | **Outside super subtotal** | **$2,322 a year ($193.50 a month)** |
| | **Total** | **$3,180** |

- $193.50 a month from cash flow is within Priya's $300 budget.

> Ivy's agent never picks the policy for Priya. It shortlists and shows the trade-offs. Amelia decides what to recommend.

---

### 5. Statement of Advice — Paraplanning Agent (Penny), signed by Amelia

The **SoA Drafting** assistant drafts the Statement of Advice for Amelia on Thu 5 Nov 2026, citing *Advice rules — best interests duty, SoA and replacement advice*:

- **Scope:** personal and family protection for Priya. Arjun's cover isn't in scope.
- **Strategy:** the four covers and sums insured from the needs analysis.
- **Ownership:** life and any-occupation TPD inside super, and the own-occupation TPD extension, trauma and income protection outside super.
- **Best-interests steps** (Corporations Act s961B): Priya's objectives, financial situation and needs from the fact find; reasonable inquiries, such as the existing-cover check; a reasonable investigation of the products on the APL; and judgements based on her circumstances, including why Corella Life suits her needs and budget.
- **Existing cover:** the Riverbend Super cover is kept, and the Kestrel Retail Super cover is kept until the new cover starts. No existing cover is replaced. Any later decision to cancel the Kestrel cover is Priya's, with Amelia.
- **Premiums and commission, in dollars**, as the law requires (Corporations Act s947B), citing *Life Advice Guru — Financial Services Guide and remuneration* and *Commission rules — LIF caps, clawback and consent*:

| Item | Amount |
|---|---|
| Advice fee | $0. Life Advice Guru charges no advice fee for protection-only advice |
| Year-1 premium | $3,180 |
| Upfront commission paid by Corella Life (60% of the first-year premium) | $1,908 |
| Ongoing commission (20% of the premium), from year 2 | $636 a year, on a premium of $3,180 |

The commission is within the Life Insurance Framework (LIF) caps: up to 60% of the first year's premium upfront, and up to 20% of the premium in each later year. The caps are tested on commission excluding GST. If the policy is cancelled in its first year, Life Advice Guru repays all of the upfront commission to Corella Life, and 60% of it if the policy is cancelled in the second year.

**Commission consent.** Because the cover is issued after 9 Jul 2025, the law requires Priya's informed consent to the commission (Corporations Act s963BB). Before she consents, the SoA shows her the insurer, the commission rates as a percentage of the premium, and how often and for how long they are paid. It also tells her that the law requires her consent, and that her consent can't be withdrawn once given.

- Total cover on the case is under $2,000,000, so no second review is needed.
- **Amelia reviews and signs the SoA on Fri 6 Nov 2026**, and talks it through with Priya. Priya gives her written consent to the commission and gets a copy.
- **Priya signs the Authority to Proceed on Sat 7 Nov 2026.**

> Penny's agent drafts. Amelia gives the advice.

---

### 6. Application and Underwriting — Underwriting Agent (Uma)

- Before any health question, the **Application & Disclosure** assistant explains the duty to take reasonable care in plain English, citing *Underwriting and the duty to take reasonable care*. Under the Insurance Contracts Act (s20B), Priya must take reasonable care not to make a misrepresentation when she answers the insurer's questions. So she answers every question carefully, including the asthma and the 2023 back strain, because it protects her claim later.
- The assistant never asks for genetic test results. From 8 Oct 2026, insurers can't ask for or use protected genetic information in life insurance underwriting, and not disclosing it isn't a misrepresentation.
- The **Underwriting Requirements** assistant looks up Corella Life's underwriting contacts and service levels (`underwriting_lookupInsurer`) and the evidence needed for Priya's age and sums insured (`underwriting_checkRequirements`).
- The application is lodged with Corella Life on Mon 9 Nov 2026. Priya's tele-interview with Corella Life is on Tue 10 Nov.
- Corella Life's decision arrives on Mon 16 Nov 2026:

| Cover | Decision |
|---|---|
| Life $750,000 | ✓ Standard rates |
| TPD $600,000 and the own-occupation extension | ✓ Standard rates |
| Trauma $150,000 | ✓ Standard rates |
| Asthma | ✓ Accepted at standard rates |
| Income protection $6,125 a month | ⚠ Accepted with an **exclusion for back and spine conditions**. Corella Life will review it after two symptom-free years |

- The assistant explains the exclusion plainly: income protection won't pay for time off work caused by a back or spine condition, but it still covers other illnesses and injuries. After two symptom-free years, Priya can ask Corella Life to review it.
- Accepting an exclusion is a human decision. **Priya accepts it with Amelia on Tue 17 Nov 2026.**
- It generates the application and disclosure summary PDF for the file (`underwriting_generateApplicationSummaryPdf`).
- **Policy `CR-26118204` starts on Wed 18 Nov 2026.** Priya has a 14-day cooling-off period (Corporations Act s1019B). It starts when she receives confirmation of the cover, or at the end of the fifth business day after it is issued, whichever is earlier.

---

### 7. Keeping Everyone Informed — Client Communications Agent (Cara)

- The **Letters & Updates** assistant drafts:
  - a **welcome letter and plain-English policy summary** for Priya. It covers each cover and sum insured, the premiums inside and outside super, what the back and spine exclusion means, the cooling-off period, how to make a claim, and a reminder that her Kestrel Retail Super cover stays in place until she decides with Amelia what to do with it
  - an **update to Hannah** that Priya's cover is in place. It has no health details, because Priya agreed to share the outcome only
- The **Tone & Compliance** assistant checks the drafts: plain English, no personal advice, the required disclosures, and Priya's complaint rights (internal dispute resolution, then AFCA).

> Cara reviews both drafts and sends them.

---

## Outcome

✅ **Completed.** From Wed 18 Nov 2026 Priya is covered with Corella Life under policy `CR-26118204`:

- life cover of $750,000
- TPD cover of $600,000 (any occupation inside super, with an own-occupation extension outside super)
- trauma cover of $150,000
- income protection of $6,125 a month, with a 90-day waiting period, a benefit period to age 65, and an exclusion for back and spine conditions

The year-1 premium is $3,180, with $193.50 a month from cash flow. Her old super cover was kept until the new cover started, so the family was never without cover.

## Human Approvals in This Scenario

| Decision | Who decided | When |
|---|---|---|
| Keep the Kestrel Retail Super cover until the new cover starts | Priya, with Amelia | Before 30 Nov 2026 |
| Sign the Statement of Advice | Amelia | Fri 6 Nov 2026 |
| Consent to the commission | Priya, in writing | With the SoA |
| Sign the Authority to Proceed | Priya | Sat 7 Nov 2026 |
| Accept the back and spine exclusion on income protection | Priya, with Amelia | Tue 17 Nov 2026 |

## AI Agents Featured

1. **Onboarding Agent:** referral and fact-find extraction, the in-good-order checklist, and the Kestrel cover deadline
2. **Financial Crime Agent:** ID and payslip checks, KYC, PEP and sanctions screening
3. **Paraplanning Agent:** needs analysis, the ownership structure, and the SoA draft with commission in dollars
4. **Insurance Research Agent:** policy comparison, stepped and level premiums, and the comparison workbook
5. **Underwriting Agent:** the duty to take reasonable care, requirements, the decision explained, and the application summary PDF
6. **Client Communications Agent:** the welcome letter, the policy summary and a consent-limited referral-partner update

## Knowledge Cited

From the `life_knowledge` index:

- *Corella Life — Protection PDS extract*, *Harbourline Life — Protection PDS extract* and *Lyrebird Life — Protection PDS extract*
- *Life Advice Guru — Financial Services Guide and remuneration*
- *Life Advice Guru — Approved product list and research method*
- *Advice rules — best interests duty, SoA and replacement advice*
- *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules*
- *Underwriting and the duty to take reasonable care*
- *Tax on life insurance — premiums and benefits*
- *Commission rules — LIF caps, clawback and consent*
- *Guide — in-good-order checklist for advice and claims*
- Case files: *Case REQ-20261103-LIF001 — referral and fact find*, *— needs analysis*, *— insurer quotes* and *— underwriting decision*

## Scenario Highlight

Shows good advice done right the first time. The agents catch super cover that was about to stop, show every number in the needs analysis, compare the insurers on what would actually pay, and make the commission visible in dollars. Amelia gives the advice, and Priya makes every decision about her cover, including the exclusion. The family is never without cover along the way.
