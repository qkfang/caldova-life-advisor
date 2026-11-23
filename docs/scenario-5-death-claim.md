# Scenario 5: Death Claim After the Sudden Death of a Wife and Mother

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Scenario Summary

A quiet, careful scenario. A husband tells Life Advice Guru that his wife has died suddenly, leaving him with two young children. The scenario shows:

- a compassionate process with one named contact, Life Advice Guru's adviser
- light-touch document checks, so the family isn't asked for anything twice
- a policy check that confirms the cover, the disclosure position and the beneficiary nomination
- a second source of cover found in the wife's super fund
- a **Microsoft Teams approval** before the claim is lodged
- gentle letters to the family, every one approved by a person

The agents step back and slow down. People approve every message to the family and every step of the claim.

## At a Glance

| Item | Detail |
|---|---|
| Case number | `REQ-20261113-LIF005` |
| Request type | Death claim |
| Received | Fri 13 Nov 2026, by email from Robert to Amelia |
| Priority | High: bereavement, flagged as a vulnerable client |
| Life insured | Mei Chen (DOB 05/06/1984), a pharmacist, who died suddenly of a brain aneurysm on Mon 12 Oct 2026, aged 42. `CL-101934` |
| Claimant | Robert Chen (DOB 30/01/1982, age 44), Mei's husband and nominated beneficiary. `CL-101935` |
| Children | Lily (12) and Max (9) |
| Adviser | Amelia, Senior Risk Adviser, who advised Mei on this policy in March 2019 |
| Policy | Corella Life `CR-19033105` (outside super): life cover of $1,000,000. Policy owner and life insured: Mei. Nominated beneficiary: Robert, 100% |
| Other cover found | Mei's Riverbend Super account: default death cover of $180,000 (a separate claim with the fund's trustee) |
| Outcome | ✅ **Completed**: $1,000,000 paid to Robert on Mon 23 Nov 2026, and the super claim under way |

---

## Family

**Robert Chen** is a secondary school teacher in Box Hill.

- He is 44. His wife, Mei, was a pharmacist. She died suddenly on 12 Oct 2026, aged 42.
- Their children are Lily (12) and Max (9).
- Robert and Mei have been Life Advice Guru clients since 2019.
- Contact: robert.chen@example.com · 0491 570 313.
- He is quiet and organised, and grieving. His focus is the children.

> "Mei always said she'd sorted the insurance. I just want to do this properly, for Lily and Max."

## Life Event

Mei Chen died suddenly of a brain aneurysm on Mon 12 Oct 2026. In March 2019 Amelia had advised her on a Corella Life policy with $1,000,000 of life cover, owned by Mei on her own life, with Robert as the nominated beneficiary.

On Fri 13 Nov 2026 Robert emails Amelia to let her know. He attaches a certified copy of the death certificate and his own identification.

---

## Request Journey

### 1. Notification Received — Onboarding Agent (Olivia)

- The **Fact Find Extraction** assistant recognises a **death notification**. It extracts Mei's details, the date of death, the policy number, the certified death certificate, Robert's identification and his contact preferences.
- The **In-Good-Order Checklist** assistant marks the case as **bereavement: vulnerable client**:
  - one named contact for the family: Amelia
  - gentle language throughout
  - no routine surveys or marketing
- It sets the priority to **High**.
- It opens case `REQ-20261113-LIF005`. It routes the case to the Team Leader for oversight and to Claims, and sends the documents to Financial Crime.

> Olivia asks Amelia to call Robert the same day. Nothing is sent to the family without a person's approval.

---

### 2. Compassionate Oversight — Team Leader Agent (Theo)

- **Theo takes oversight** of the case. The **Pipeline & SLAs** assistant keeps the claim's progress and the insurer's timeframes in view, so the family never has to chase.
- **Amelia is the family's one named contact.**
- **Theo approves every message to the family** before it is sent.

---

### 3. Document Checks — Financial Crime Agent (Felix)

- The **Document Authenticity** assistant checks the certified death certificate and Robert's identification with Azure Content Understanding:

| Document | Verdict |
|---|---|
| Death certificate (certified copy) | ✓ Legit |
| Robert's identification | ✓ Legit |

- Risk: **low**. No extra checks are needed, so the family isn't asked for anything more.

---

### 4. Policy Check and Lodgement — Claims Agent (Ben)

- The **Policy & Evidence Check** assistant checks the claimant and the policy (`claims_validateClaimant`, `claims_checkPolicy`), citing *Corella Life — Protection PDS extract*, *Underwriting and the duty to take reasonable care* and *Claims rules — Life Insurance Code of Practice and income protection benefits*:

| Check | Finding | Result |
|---|---|---|
| Policy in force | `CR-19033105`, life cover of $1,000,000, in force since March 2019 | ✓ |
| Premiums | Paid | ✓ |
| Exclusions | None | ✓ |
| Disclosure | The policy has been in force for over seven years. Under the Insurance Contracts Act, an insurer can avoid a life policy, or reduce the sum insured, for a non-fraudulent misrepresentation only within three years of the contract starting (s29(3) and s29(10)) | ✓ That window closed long ago |
| Beneficiary nomination | Robert, 100%. The nomination is valid | ✓ Paid directly to Robert, not through Mei's estate |
| Claimant | Robert's identity is verified | ✓ |

- **Other cover found.** The assistant checks the other cover on Mei's file and finds that her **Riverbend Super** account also had **default death cover of $180,000**. That is a separate claim with the fund's trustee, paid under the fund's rules, and the fund pays it to Robert. A lump-sum death benefit from super paid to a spouse is tax-free, because a spouse is a death-benefits dependant (ITAA 1997 s302-60 and s302-195).
- The **Benefit & Lodgement** assistant confirms the benefit (`claims_calculateBenefit`): **$1,000,000**, paid to Robert. Life cover proceeds paid outside super are generally not taxable (ITAA 1997 s118-300), citing *Tax on life insurance — premiums and benefits*.
- `claims_checkApprovalRules`: every death claim needs human approval. `claims_requestApproval` posts a **Microsoft Teams** approval card.
- **Theo approves in Teams.** `claims_getApprovalStatus` returns **approved**, and `claims_lodgeClaim` lodges the claim with Corella Life on **Mon 16 Nov 2026**.
- The assistant tracks Corella Life's timeframes under the Life Insurance Code of Practice. For a lump-sum claim, the insurer has up to six months to decide, and must update the claimant at least every 20 business days until it does.
- **Corella Life accepts the claim on Fri 20 Nov 2026 and pays $1,000,000 to Robert on Mon 23 Nov 2026.**
- Because the accepted benefit is $25,000 or more, the Code requires Corella Life to suggest that Robert gets independent financial advice. Amelia lets Robert know she's there when he's ready, with no rush.

> Ben keeps Amelia up to date at every step, so she can tell Robert before he has to ask.

---

### 5. Gentle Letters — Client Communications Agent (Cara)

- The **Letters & Updates** assistant drafts gentle letters to Robert:
  - that the claim has been lodged, and later that it has been accepted and paid
  - the amount, the date, and that the benefit isn't taxable
  - a simple **checklist for the Riverbend Super claim**: what the fund's trustee will ask for, and that Ben and Amelia will help
  - one name and one number to call: Amelia
  - his complaint rights: internal dispute resolution first, then AFCA
- The **Tone & Compliance** assistant checks the tone for bereavement: short sentences, no jargon, nothing that asks Robert to repeat himself, and no personal advice.
- **Theo approves each letter before it goes.**

> Amelia calls Robert before each letter arrives, so he never hears news from a letter first.

---

## Outcome

✅ **Completed.**

- Corella Life paid **$1,000,000** to Robert on Mon 23 Nov 2026, directly under Mei's nomination and not through her estate.
- Mei's **$180,000** super claim with Riverbend Super is under way, with a checklist and Life Advice Guru's help.
- Robert dealt with one person, Amelia, from start to finish.

## Human Approvals in This Scenario

| Decision | Who decided | When |
|---|---|---|
| One named contact for the family | Theo, with Amelia as the contact | Fri 13 Nov 2026 |
| Every message to the family | Theo | Before each message was sent |
| Lodge the death claim with Corella Life | Theo, in Microsoft Teams | Before lodgement on Mon 16 Nov 2026 |

## AI Agents Featured

1. **Onboarding Agent:** the death notification and the bereavement flag
2. **Team Leader Agent:** compassionate oversight, one named contact, and approval of every message and of the claim
3. **Financial Crime Agent:** light-touch checks of the death certificate and Robert's identification
4. **Claims Agent:** the policy, disclosure and nomination checks, finding the super cover, the Teams approval and the lodgement
5. **Client Communications Agent:** gentle letters and a checklist for the super claim

## Knowledge Cited

From the `life_knowledge` index:

- *Corella Life — Protection PDS extract*
- *Underwriting and the duty to take reasonable care*
- *Claims rules — Life Insurance Code of Practice and income protection benefits*
- *Tax on life insurance — premiums and benefits*
- *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules*
- *Matrix — authority, approvals and escalation*
- Case files: *Case REQ-20261113-LIF005 — death notification*, *— death certificate*, *— policy schedule and nomination* and *— super fund cover note*

## Scenario Highlight

Shows that AI in an advice practice is not only about speed but about care. The same agents that move quickly elsewhere work quietly in the background here: they confirm the policy, the nomination and the tax, find cover the family didn't know about, and keep everything moving. Robert deals with one person he already knows, and a person approves every word sent to the family.
