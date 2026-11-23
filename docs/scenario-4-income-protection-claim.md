# Scenario 4: Income Protection Claim After an Injury

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Scenario Summary

A self-employed electrician is hurt on a job and can't work for months. His income protection policy is the family's safety net. The scenario shows:

- a claim notification taken with care, with his wife as his authorised support person
- a policy and evidence check that builds the claim pack before anything goes to the insurer
- a benefit calculation that explains, in plain English, why the benefit is lower than the insured amount
- a vulnerable-client review and a **Microsoft Teams approval** before the claim is lodged
- the insurer's Code of Practice timeframes tracked, so the family never has to chase
- a personal call from the adviser before the letter goes out

The agents handle the policy wording and the numbers. People handle Tom.

## At a Glance

| Item | Detail |
|---|---|
| Case number | `REQ-20261111-LIF004` |
| Request type | Income protection claim |
| Received | Wed 11 Nov 2026, by phone from Tom, with Kylie joining as his support person |
| Priority | High: financial stress, flagged as a vulnerable client |
| Client | Tom Bradley (DOB 03/03/1985, age 41), client since 2023, `CL-102265` |
| Adviser | Amelia, Senior Risk Adviser |
| Policy | Harbourline Life income protection `HL-23051877` (outside super), started 15 Mar 2023 |
| Insured benefit | $7,000 a month (set from $120,000 a year at application), 30-day waiting period, benefit period to age 65 |
| Premium | $2,880 a year (tax-deductible) |
| Injury | Torn rotator cuff (right shoulder) after a fall from a ladder on Wed 14 Oct 2026. Surgery on 21 Oct. Four to six months off work expected |
| Earnings before the injury | Net business income of $96,000 in the 12 months to 30 Sep 2026 |
| Benefit accepted | **$5,600 a month** from 13 Nov 2026, paid monthly in arrears. First payment about 13 Dec 2026 |
| Outcome | 🟡 **Partially approved**: accepted at $5,600 a month, not the $7,000 insured |

---

## Client and Family

**Tom Bradley** is a self-employed electrician in Frankston. He trades as Bradley Electrical, a sole trader business.

- He is 41. His wife, Kylie (39), is a part-time bookkeeper and keeps Tom's books. They have two children, aged 8 and 5.
- He has been a Life Advice Guru client since 2023, when he took out his income protection through Life Advice Guru.
- Contact: tom.bradley@example.com · 0491 570 110.
- He is practical and proud, he hates paperwork, and right now he's stressed.

> "I've never been off the tools this long. I just need to know the money's coming."

## Life Event

On Wed 14 Oct 2026 Tom fell from a ladder on a job and tore the rotator cuff in his right shoulder. He had surgery on 21 Oct, and his surgeon expects him to be off work for four to six months. With no work coming in, money is tight.

On Wed 11 Nov 2026 Tom calls Life Advice Guru to make a claim. Kylie joins the call as his support person, with Tom's written authority.

---

## Request Journey

### 1. Claim Notification — Onboarding Agent (Olivia)

- The **Fact Find Extraction** assistant takes the claim notification from the call notes: the date and cause of the injury, the surgery, the surgeon's estimate of time off work, and the policy number.
- The **In-Good-Order Checklist** assistant runs the claims checklist from *Guide — in-good-order checklist for advice and claims*:

| Check | Result |
|---|---|
| Identification | ✓ Verified |
| Support-person authority | ✓ Tom's written authority for Kylie to speak for him |
| Privacy and health-information consent | ✓ Signed |
| Authority for Life Advice Guru to deal with Harbourline Life on the claim | ✓ Signed |
| Medical evidence | ✓ Surgeon's report |
| Earnings evidence | ✓ Business activity statements (BAS) and a profit and loss statement for the 12 months to 30 Sep 2026, sent by Kylie |

- It flags **financial stress** and marks Tom as a **vulnerable client** for human handling.
- It sets the priority to **High**.
- It opens case `REQ-20261111-LIF004` and routes it to Claims.

> Olivia confirms the flags and lets Tom know Ben will look after his claim.

---

### 2. Policy and Evidence Check — Claims Agent (Ben)

- The **Policy & Evidence Check** assistant checks the claimant and the policy (`claims_validateClaimant`, `claims_checkPolicy`), citing *Harbourline Life — Protection PDS extract* and *Claims rules — Life Insurance Code of Practice and income protection benefits*:

| Check | Finding | Result |
|---|---|---|
| Claimant | Tom is the policy owner and the life insured. Kylie is his authorised support person | ✓ |
| Policy | `HL-23051877` is in force, with a monthly benefit of $7,000 and a benefit period to age 65 | ✓ |
| Disability | The surgeon's report confirms Tom can't work as an electrician while he recovers | ✓ |
| Waiting period | 30 days from 14 Oct 2026, so the benefit accrues from 13 Nov 2026 | ✓ |
| Benefit basis | Harbourline Life's policy was issued in 2023, after APRA's income protection measures took effect on 1 Oct 2021. Its terms pay the lesser of the insured benefit and 70% of Tom's average monthly earnings in the 12 months before the disability | ℹ Calculated below |
| Earnings evidence | BAS and profit and loss for the 12 months to 30 Sep 2026: net business income of $96,000 | ✓ |

- It builds the **claim pack**: the claim form, the surgeon's report, the earnings evidence and the policy schedule.
- The **Benefit & Lodgement** assistant calculates the expected benefit (`claims_calculateBenefit`):

| Step | Working | Amount |
|---|---|---|
| Insured monthly benefit | Set from $120,000 a year at application | $7,000 |
| Average monthly earnings in the 12 months before the injury | $96,000 ÷ 12 | $8,000 |
| 70% of average monthly earnings | 70% × $8,000 | $5,600 |
| **Benefit payable** | The lesser of $7,000 and $5,600 | **$5,600 a month** |
| Starts | After the 30-day waiting period | Accrues from 13 Nov 2026 |
| Paid | Monthly in arrears | First payment about 13 Dec 2026 |
| Tax | Income protection benefits that replace income are assessable income | |

- It explains why in plain English: "Tom's policy pays the lower of his insured benefit and 70% of what he actually earned in the 12 months before the injury. He earned $96,000, or $8,000 a month, so the benefit is $5,600 a month."
- `claims_checkApprovalRules`: lodging any claim needs human approval, and Tom is a vulnerable client, so the case goes to Theo.
- It sends the documents to Financial Crime for the usual checks.

> Ben checks the calculation and the explanation before anything goes further.

---

### 3. Document Checks — Financial Crime Agent (Felix)

- The **Document Authenticity** assistant scans the documents with Azure Content Understanding:

| Document | Verdict |
|---|---|
| Business activity statements (BAS) | ✓ Legit |
| Profit and loss statement | ✓ Legit |
| Surgeon's report | ✓ Legit |

- The **Risk Timeline** assistant finds no contact or bank-detail changes. Risk: **low**. Tom isn't asked for anything more.

---

### 4. Vulnerable-Client Review and Approval — Team Leader Agent (Theo)

- The **Approvals & File Quality** assistant prepares a short brief for Theo: Tom's situation, the policy checks, why the benefit is $5,600 and not $7,000, and tone guidance.
- Ben's agent raises the approval in **Microsoft Teams** (`claims_requestApproval`, which posts the adaptive card through `teams_sendApprovalCard`).
- **Theo approves the lodgement in Teams.** He asks Amelia to call Tom personally before any letter goes out.

---

### 5. Lodgement — Claims Agent (Ben)

- `claims_getApprovalStatus` returns **approved**. Only then does `claims_lodgeClaim` lodge the claim with Harbourline Life, on **Thu 12 Nov 2026**. The tool refuses to lodge any claim without approval.
- The **Benefit & Lodgement** assistant tracks Harbourline Life's timeframes under the Life Insurance Code of Practice, citing *Claims rules — Life Insurance Code of Practice and income protection benefits*. For an income-related claim, the insurer must decide within two months of receiving the claim, or of the end of the waiting period if that is later: here, by 13 Jan 2027. It must update Tom at least every 20 business days until it decides, and give a written decision within 15 business days of having all the information it needs. The Code binds insurers, not advisers, so Ben's agent tracks it on Tom's behalf.
- **Harbourline Life accepts the claim on Thu 19 Nov 2026 at $5,600 a month.**

---

### 6. A Call First, Then a Letter — Client Communications Agent (Cara)

- **Amelia calls Tom** to tell him the claim is accepted, and to explain the amount in person.
- The **Letters & Updates** assistant then drafts a plain-English letter to Tom:
  - the claim is accepted: **$5,600 a month**
  - why it is $5,600 and not $7,000: the benefit follows what he earned in the 12 months before the injury
  - when the money comes: the benefit accrues from 13 Nov 2026 and is paid monthly in arrears, with the first payment about 13 Dec 2026
  - that the benefit is taxable income
  - that Ben stays in touch with Harbourline Life and will tell Tom if they need anything else
  - his right to complain if he disagrees with the insurer's decision: Harbourline Life's internal dispute resolution, then AFCA, and that Life Advice Guru will help him
- The **Tone & Compliance** assistant checks the letter is warm, clear and free of jargon, and gives no personal advice.

> Cara reviews the letter and sends it after Amelia's call.

---

## A Note for Amelia (Not Advice)

Ben's agent adds a note to the file for Amelia: Tom's insured benefit of $7,000 a month is more than his recent income supports. At his next review, Amelia may want to consider whether the benefit should change. The adviser decides. The agent doesn't recommend anything to Tom.

## Outcome

🟡 **Partially approved.**

- Harbourline Life accepted Tom's claim on Thu 19 Nov 2026 at **$5,600 a month**, not the $7,000 insured, because the benefit follows his earnings in the 12 months before the injury.
- The benefit accrues from 13 Nov 2026, and the first payment is due about 13 Dec 2026.
- Tom heard the news from Amelia first, then got a letter that explains the amount in plain English.

## Human Approvals in This Scenario

| Decision | Who decided | When |
|---|---|---|
| Kylie as Tom's support person | Tom, in writing | Wed 11 Nov 2026 |
| Vulnerable-client review | Theo | Before lodgement |
| Lodge the claim with Harbourline Life | Theo, in Microsoft Teams | Before lodgement on Thu 12 Nov 2026 |
| Call Tom before the letter | Amelia, at Theo's request | After Harbourline's decision |
| Send the letter | Cara | After Amelia's call |

## AI Agents Featured

1. **Onboarding Agent:** the claim notification, the support-person authority and the vulnerable-client flag
2. **Claims Agent:** the policy and evidence check, the claim pack, the $5,600 calculation, the Teams approval and the lodgement
3. **Financial Crime Agent:** checks on the BAS, the profit and loss statement and the surgeon's report
4. **Team Leader Agent:** the vulnerable-client review and the approval in Teams
5. **Client Communications Agent:** a plain-English letter after Amelia's call

## Knowledge Cited

From the `life_knowledge` index:

- *Harbourline Life — Protection PDS extract*
- *Claims rules — Life Insurance Code of Practice and income protection benefits*
- *Tax on life insurance — premiums and benefits*
- *Guide — in-good-order checklist for advice and claims*
- *Matrix — authority, approvals and escalation*
- Case files: *Case REQ-20261111-LIF004 — claim notification*, *— policy schedule*, *— earnings evidence* and *— benefit calculation*

## Scenario Highlight

Shows AI applying the policy wording exactly, including a benefit that is lower than the client expected, while people stay in charge of the human side. Ben's agent explains the $5,600 before anyone is surprised by it, Theo approves the claim in Teams, Amelia makes the call, and Tom never has to chase.
