# Scenario 2: Identity Takeover and a Fake Trauma Claim, Blocked

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Scenario Summary

A trauma claim arrives for a long-standing client: breast cancer, the full $200,000, marked urgent, to be paid into a new bank account. It looks like a client in crisis. It is actually an identity takeover. The scenario shows:

- a claim check against the policy wording that spots what's missing
- document authenticity checks with Azure Content Understanding
- a risk timeline that connects a phone call, a device login and a claim four days apart
- an explainable risk rating, with every red flag in plain language
- a careful, client-first response through the contact details Life Advice Guru already trusts

No one is accused. People decide to block the request and to alert the insurer, and Grace is treated as the likely victim from start to finish.

## At a Glance

| Item | Detail |
|---|---|
| Case number | `REQ-20261106-LIF002` |
| Request type | Trauma claim and bank change (what the request asked for) |
| Received | Fri 6 Nov 2026, 07:52, by email from a recently changed address |
| Priority | Urgent |
| Client | Grace Williams (DOB 22/07/1965, age 61), client since 2019, `CL-100377` |
| Adviser | Amelia, Senior Risk Adviser |
| Policy | Lyrebird Life `LB-19004417` (outside super, stepped): life cover $500,000 and trauma cover $200,000 |
| Nominated beneficiaries | Her sons Daniel (50%) and James (50%) |
| Requested | The full $200,000 trauma benefit for breast cancer, paid into a new account ending 4471 in the name "G Williams Consulting Pty Ltd" |
| Outcome | 🛡️ **Blocked**: no claim lodged, and $200,000 protected |

---

## Client

**Grace Williams** is a semi-retired school principal from Hawthorn who still does some relief teaching.

- She is 61. She was widowed in 2018, and her sons are Daniel (32) and James (29).
- She has been a Life Advice Guru client since 2019.
- Verified contact details: grace.williams@example.com · mobile 0491 570 157.
- She is independent, careful and warm, and she is rattled when she hears what happened.

> "I'm in Florence and I'm perfectly well. I never asked anyone for anything!"

## Life Event: What Really Happened

| When | What happened |
|---|---|
| Mon 28 Sep 2026 | At her annual review, Grace tells Amelia she'll be in Italy from 20 Oct to 1 Dec 2026. The travel notice goes on file |
| Tue 20 Oct 2026 | Grace leaves for Italy |
| Mon 2 Nov 2026, 10:41 | A caller claiming to be Grace passes basic checks (name, date of birth and address). The caller says "I've lost my phone" and changes the mobile to 0491 570 159 and the email to grace.w.claims@example.net. No code is sent to the old mobile |
| Mon 2 Nov 2026, 10:58 | The client portal is accessed from a **new device** on a **Sydney** IP address |
| Fri 6 Nov 2026, 07:52 | An email from the new address asks for a trauma claim for breast cancer: the full **$200,000**, "urgent", paid into a **new account ending 4471** in the name "G Williams Consulting Pty Ltd". This is about **93 hours** after the contact details were changed |

Three documents are attached to the email: an oncologist's report from "Dr A. Moretti, Eastern Oncology Group", a driver licence, and a bank statement.

---

## Request Journey

### 1. Request Received — Onboarding Agent (Olivia)

- The **Fact Find Extraction** assistant reads the email and attachments. It extracts three things:
  - a **trauma claim**: breast cancer, the full $200,000 trauma benefit
  - a **bank-detail change**: a new account ending 4471 in the name "G Williams Consulting Pty Ltd"
  - a **recent contact change**: the mobile and email were changed on 2 Nov, four days earlier
- The **In-Good-Order Checklist** assistant notes that a bank-detail change always needs human approval, and that the request comes from contact details changed this week.
- It sets the priority to **Urgent**: a claim, a bank change and a recent contact change on the same policy.
- It opens case `REQ-20261106-LIF002` and routes it to Claims, with the bank change and the contact change flagged for Financial Crime.

> Olivia confirms the flags. She doesn't reply to the new email address.

---

### 2. Policy Check — Claims Agent (Ben)

The **Policy & Evidence Check** assistant checks the claim against the policy (`claims_validateClaimant`, `claims_checkPolicy`), citing *Lyrebird Life — Protection PDS extract*:

| Check | Finding | Result |
|---|---|---|
| Policy | `LB-19004417` is in force, with trauma cover of $200,000 | ✓ |
| Cancer definition | A cancer claim needs a confirmed diagnosis by a specialist, with histopathology. The report attached has no histopathology | ✗ Evidence doesn't meet the definition |
| Who is paid | Trauma benefits are paid to the policy owner, Grace Williams | ✗ The new account is in another name |
| Bank details | A new account ending 4471, in the name "G Williams Consulting Pty Ltd" | ✗ Can't be used. Any change to bank details needs human approval |
| Who is asking | The request came from contact details changed on 2 Nov | ⚠ Refer to Financial Crime |

- **Finding:** the claim can't go to Lyrebird Life as it stands. Ben's agent doesn't lodge anything and refers the case to Financial Crime.

> Ben agrees, and holds the claim until Felix has looked at it.

---

### 3. Financial Crime Review — Financial Crime Agent (Felix)

- The **Document Authenticity** assistant scans the three attachments with **Azure Content Understanding**:

| Document | Finding | Why |
|---|---|---|
| Oncologist's report, "Dr A. Moretti, Eastern Oncology Group" | ❌ **Fake** | Pasted letterhead, edited dates, a font mismatch, a provider number in the wrong format, and no histopathology |
| Driver licence | ❌ **Tampered** | The photo region has been edited |
| Bank statement | ⚠ **Suspicious** | The account was opened on 3 Nov, and the name doesn't match the policy owner |

- The **Risk Timeline** assistant lines up the activity and matches it against *Guide — financial crime red flags and controls for life insurance*:

| Red flag | What the agent found |
|---|---|
| Contact change just before a claim | The mobile and email were changed on 2 Nov at 10:41. The claim arrived on 6 Nov at 07:52, about 93 hours later |
| Weak verification on the change | The caller passed basic checks only, and no code was sent to the old mobile |
| New device, unexpected location | The portal was accessed from a new device on a Sydney IP at 10:58 on 2 Nov, while the travel notice says Grace is in Italy |
| Urgency and the full benefit | The email asks for the whole $200,000, urgently |
| Payment to someone else | The new account is in a company name, not the policy owner's |
| New account | The bank statement shows the account was opened on 3 Nov |
| Document problems | A fake specialist report and a tampered licence |

- **Risk rating: HIGH.** Every indicator is explained, so this is not a black-box score.
- **Recommended actions**, for a human decision:
  1. Block the claim and the bank change.
  2. Restore Grace's verified contact details (0491 570 157 and grace.williams@example.com) and lock portal access.
  3. Call Grace on her **verified mobile, 0491 570 157**, never the new number.
  4. Alert Lyrebird Life's fraud team.
  5. Help Grace protect her identity.
- The language is neutral throughout. The agent says the request "doesn't appear to have come from Grace". It never suggests Grace did anything wrong.

> Felix reviews the evidence and agrees: stop everything, and reach Grace on the number we already trust.

---

### 4. Approval — Team Leader Agent (Theo)

- The **Approvals & File Quality** assistant prepares a one-page brief for Theo: the red flags, the document findings, the recommended actions and what each one protects. It cites *Matrix — authority, approvals and escalation*.
- **Theo approves:**
  - blocking the claim and the bank change on financial-crime grounds
  - restoring Grace's verified contact details
  - locking her portal access
  - alerting Lyrebird Life's fraud team

---

### 5. No Claim Lodged — Claims Agent (Ben)

- The **Benefit & Lodgement** assistant confirms on the case that **no claim was lodged** with Lyrebird Life and the bank change was never applied.
- Grace's policy, her cover and her sons' nominations are unchanged.

---

### 6. Reaching Grace — Client Communications Agent (Cara)

- Cara calls Grace herself on her **verified mobile, 0491 570 157**. Grace is in Florence. She confirms she is well and never asked for anything.
- The **Letters & Updates** assistant drafts a calm **reassurance letter** to Grace's verified email:
  - what happened, and what Life Advice Guru did about it
  - that her cover is safe and no claim was made
  - identity-protection steps, such as changing her email password and watching for unexpected messages
  - how to reach a person at Life Advice Guru
- The **Tone & Compliance** assistant checks that the letter is calm, blame-free and in plain English.
- Every communication to a fraud victim needs human approval. Cara approves the letter before it goes.

> Cara asks Grace when suits her for a follow-up call from Amelia after she's home.

---

## Outcome

🛡️ **Blocked.**

- No claim was lodged, and the **$200,000** trauma benefit is protected.
- Grace's life cover of $500,000 and trauma cover of $200,000 are safe, and her sons are still her nominated beneficiaries.
- Her verified contact details are restored, and portal access is locked until she's back.
- Lyrebird Life's fraud team has been alerted, with Theo's approval.

## Human Approvals in This Scenario

| Decision | Who decided |
|---|---|
| Hold the claim until Financial Crime had reviewed it | Ben |
| Block the claim and the bank change on financial-crime grounds | Theo |
| Restore the verified contact details and lock portal access | Theo |
| Alert Lyrebird Life's fraud team | Theo |
| Call Grace and approve the reassurance letter | Cara |

## AI Agents Featured

1. **Onboarding Agent:** extracting a claim, a bank change and a recent contact change from one email
2. **Claims Agent:** the policy check that spots the missing histopathology and the wrong payee, and confirming no claim was lodged
3. **Financial Crime Agent:** document authenticity, the risk timeline, an explainable HIGH rating and recommended controls
4. **Team Leader Agent:** the approval brief for the block and the insurer alert
5. **Client Communications Agent:** a trusted-channel call and a reassurance letter

## Knowledge Cited

From the `life_knowledge` index:

- *Lyrebird Life — Protection PDS extract*
- *Guide — financial crime red flags and controls for life insurance*
- *Guide — in-good-order checklist for advice and claims*
- *Matrix — authority, approvals and escalation*
- Case files: *Case REQ-20261106-LIF002 — claim email and attachments*, *— client activity log*, *— document checks* and *— travel notice*

## Scenario Highlight

Shows how AI agents protect clients from identity takeover. In minutes, the agents connect a phone call, a device login, a new bank account, a fake specialist report and a tampered licence, and explain every red flag. People make the decisions to block the request and alert the insurer, and Grace is treated as the victim throughout.
