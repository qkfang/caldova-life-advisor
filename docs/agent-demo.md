# Presenter Click-Path

> Part of **Advice Team in a Day**, the AI advice practice demo for **Caldova Life Advice**.

This is a practical guide to presenting *Advice Team in a Day* live. It covers a **10-minute golden path**, a case cheat sheet, and a click path for each of the eight agent pages. For the full stories, see the `scenario-*.md` files. For the industry context and presenter notes, see [background.md](background.md).

---

## Before You Start

| Check | Detail |
|---|---|
| Backend running | <http://localhost:5212>, or the deployed `app-caldovacl-backend` |
| Frontend running | <http://localhost:5173>, or the deployed `app-caldovacl-frontend` |
| Azure | Signed in (`az login`) with access to the Foundry project (`aif-caldovacl`), and the `life_knowledge` index built (see [ai-search/README.md](../ai-search/README.md)) |
| MCP tools | `APP_MCP_URL` points to a public URL for the backend's `/mcp` endpoint, so Ivy's, Uma's and Ben's agents can call their tools |
| Microsoft Teams | Open in the channel where Ben's claim approval cards arrive |
| Microphone | Browser microphone permission allowed, for Cara's voice chat |
| Tabs | One browser tab on Caldova House (the frontend), one on the backend console |

Every **engage** step on an agent page shows five tabs:

> **Agent Narrative · Reference · Agent Prompt & Tools · Agent Input · Raw Output**

After each run, open **Reference** to show the citations from `life_knowledge`, and **Agent Prompt & Tools** to show the instructions and the MCP tool calls. That is where the Foundry story is.

---

## Case Cheat Sheet

The table includes the backend's scenario pages (`/scenarios/{id}`). Each lists the case's steps, and every step's **Run this step** link opens that agent's page for the case.

| # | Case | Client | Onboarding sample | Scenario page | Best for showing |
|---|---|---|---|---|---|
| 1 | `REQ-20261103-LIF001` | Priya Natarajan | `onboarding-priya` | `/scenarios/family-protection` | Fact find extraction, the Kestrel cover deadline, the needs analysis, research and the workbook, commission in dollars, an exclusion at underwriting |
| 2 | `REQ-20261106-LIF002` | Grace Williams | `onboarding-grace` | `/scenarios/fraudulent-claim` | Financial crime, Content Understanding, the risk timeline, a block |
| 3 | `REQ-20261109-LIF003` | George Papadopoulos, with Harpreet Kaur | `onboarding-george` | `/scenarios/business-protection` | Buy-sell and key person needs, the $2,000,000 second review, a loading |
| 4 | `REQ-20261111-LIF004` | Tom Bradley | `onboarding-tom` | `/scenarios/income-protection-claim` | The $5,600 benefit calculation, the Teams approval before lodgement, a kind letter |
| 5 | `REQ-20261113-LIF005` | Robert Chen | `onboarding-robert` | `/scenarios/death-claim` | Bereavement care, the policy and nomination checks, finding cover in super |

---

## The 10-Minute Golden Path

This path follows **Priya's family protection advice** (scenario 1) from the front desk to a policy in force. It then shows **Tom's claim** (scenario 4) for the Teams approval, and finishes with **Grace** (scenario 2) for financial crime.

| Time | Where | Click | What to show | What to say |
|---|---|---|---|---|
| 0:00–1:00 | **Caldova House** (frontend) | Press **End** for the finale and choose **Priya**. Use **x2** or **Next step** to keep to time | Priya steps off the tram and signs at the front desk under the verandah, where Amelia links her with the Onboarding and Paraplanning agents. The house opens up and her request travels person → agent → engine room and back up at each step: Olivia → Felix → Penny → Ivy → Penny → Uma → Cara. The result rises to Maya's idea under the copper dome | "Requests come in at the front desk. The adviser gives the advice, people make the decisions, and there's an AI agent under every desk, with the engine room in the old vault." |
| 1:00–2:00 | **Onboarding Agent** | Pick `onboarding-priya` (Hannah Lewis's referral and Priya's fact find) → **Engage agent** | The auto-filled Client Case, the in-good-order checklist, the Kestrel Retail Super deadline of 30 Nov 2026, priority High, and a case opened and routed | "Olivia's agent reads the referral and the fact find, so Olivia checks the work instead of keying it in. And it caught cover that was about to stop." |
| 2:00–3:30 | **Paraplanning** | Pick `REQ-20261103-LIF001` → review → **Engage** | The needs analysis: a $1,010,000 life need, less $45,000 of savings and $210,000 of super cover, leaves $755,000, so $750,000. TPD $600,000, trauma $150,000 and income protection of $6,125 a month. The structure inside and outside super. The SoA draft for Amelia, with commission in dollars: $1,908 upfront and $636 a year. Then open the **Reference** tab | "Every number shows its working and cites its source. Penny's agent drafts; Amelia gives the advice and signs." |
| 3:30–5:00 | **Insurance Research** | `REQ-20261103-LIF001` → **Engage** → then ask in chat: "What would level premiums cost with Corella Life?" | Corella Life $3,180, Harbourline Life $3,412 and Lyrebird Life $3,690 in year 1. Own-occupation TPD for physiotherapists. Level premiums of $4,050 in year 1. The chart and the workbook download. In **Agent Prompt & Tools**, the `research_analysePolicyDocument`, `research_compareInsurers` and `research_generateComparisonWorkbook` calls | "Cheapest isn't best if the definition won't pay. Ivy's agent compares what would actually pay, and leaves the choice to Amelia." |
| 5:00–6:00 | **Underwriting** | `REQ-20261103-LIF001` → **Engage** | The duty to take reasonable care explained first. The requirements. The decision: standard rates, with a back and spine exclusion on income protection. The application summary PDF. In **Agent Prompt & Tools**, `underwriting_lookupInsurer`, `underwriting_checkRequirements` and `underwriting_generateApplicationSummaryPdf` | "Uma's agent explains the exclusion in plain English. Accepting it is Priya's decision, with Amelia." |
| 6:00–7:30 | **Claims** (Tom) | `REQ-20261111-LIF004` → confirm the inputs → **Engage** → approve the card in **Teams** → return to the page | The calculation: $7,000 insured, but 70% of $8,000 a month of earnings is $5,600. The adaptive card in Teams. `claims_lodgeClaim` refuses until the card is approved, then lodges | "The agent can't lodge a claim on its own. Theo approves in Teams, where he already works." |
| 7:30–8:30 | **Client Communications** | `REQ-20261103-LIF001` → **Engage** → **Voice chat with Cara** → ask "When does my cover start?" | The welcome letter and policy summary. The update to Hannah, with no health details. A spoken answer: Wednesday 18 November 2026 | "Same facts, different audiences, and Hannah only hears what Priya agreed to share." |
| 8:30–9:15 | **Team Leader** | `REQ-20261109-LIF003` (George) → **Engage** → open **Group chat** | The practice dashboard and George's $2,800,000 second review. Theo's agent coordinates the seven specialists on one case and ends with a consolidated plan | "This isn't one chatbot. It's a team, with a team leader." |
| 9:15–10:00 | **Financial Crime** (Grace) | Optional: in the Caldova House finale, choose **Grace**. Then go to the Financial Crime page → pick `REQ-20261106-LIF002` → scan the documents → **Engage** | The fake oncologist's report, the tampered licence and the suspicious bank statement. The timeline from the contact change to the claim, about 93 hours. Risk **HIGH**, with a block, restored contact details and a call-back on 0491 570 157 recommended. Theo decides | "$200,000 protected, and Grace is treated as the victim, not a suspect." |

The Onboarding page creates a fresh `REQ-…` number. For the rest of the golden path, pick the seeded cases.

---

## Agent-by-Agent Click Path

### 1. Onboarding Agent (Olivia) · `/agents/onboarding`

| | |
|---|---|
| **Try it out** | 1. Pick a sample inbound email with its fact find or claim notification. 2. **Engage agent**. 3. Review the auto-filled Client Case form, the in-good-order checklist and the priority. 4. The case is opened (`REQ-…`) and routed |
| **Pick** | `onboarding-priya` for the best story: the Kestrel Retail Super deadline. `onboarding-tom` for a claim notification with a support person. `onboarding-grace` for a claim, a bank change and a recent contact change in one email |
| **Look for** | Fields pulled from the email and fact find. Health details marked sensitive. The existing-cover flag. A new case number |
| **Foundry on show** | A declarative agent (`Azure.AI.Projects`) doing structured extraction, with Azure AI Search grounding over `life_knowledge` |
| **Talking point** | "Requests that aren't in good order are where work stops. The agent gets it right the first time, and Olivia confirms." |

### 2. Paraplanning Agent (Penny) · `/agents/paraplanning`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. Review the case. 3. **Engage agent**. 4. See the needs analysis with its working, the ownership structure, and the SoA draft for Amelia |
| **Pick** | `REQ-20261103-LIF001` (Priya) for the family needs analysis and commission in dollars. `REQ-20261109-LIF003` (George) for buy-sell and key person needs and the $2,000,000 second-review flag |
| **Look for** | Every step of the working. The cited rule documents. The commission of $1,908 upfront and $636 a year, and the consent wording. The second-review flag on George's $2,800,000 |
| **Foundry on show** | Azure AI Search grounding with citations, shown in the **Reference** tab |
| **Talking point** | "The agent drafts the advice file. The adviser gives the advice. That line never moves." |

### 3. Insurance Research Agent (Ivy) · `/agents/insurance-research`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. Review. 3. **Engage agent**: the MCP tools turn the insurers' product sheets into a comparison table, a chart and an Excel workbook. 4. Chat with the agent |
| **Pick** | `REQ-20261103-LIF001` (Priya) for three insurers, stepped and level premiums. `REQ-20261109-LIF003` (George) for business protection: Northbank Life $8,620, Corella Life $9,180 and Harbourline Life $9,410 |
| **Look for** | The definitions that matter, such as own-occupation TPD for physiotherapists. Stepped against level premiums. The workbook download (`/research/download/{fileName}`) and the chart |
| **Foundry on show** | MCP tools `research_analysePolicyDocument`, `research_compareInsurers` and `research_generateComparisonWorkbook`, served by the backend (`caldova-ops-mcp`) |
| **Talking point** | "Ivy's agent shortlists. Amelia recommends." |

### 4. Financial Crime Agent (Felix) · `/agents/financial-crime`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. Review. 3. Scan the documents on the case: Content Understanding checks each one. 4. **Engage agent**. 5. See the risk summary and the recommended actions |
| **Pick** | `REQ-20261106-LIF002` (Grace) for a fake specialist report and a tampered licence. For contrast, `REQ-20261113-LIF005` (Robert): a genuine death certificate and low risk, or `REQ-20261109-LIF003` (George) for KYC on a company and its owners |
| **Look for** | A verdict for each document (legit, suspicious, tampered or fake) with the failed checks. The timeline: the contact change on 2 Nov at 10:41, the Sydney login at 10:58, and the claim on 6 Nov at 07:52, about 93 hours later. A HIGH rating, with a block, restored contact details, locked portal access, a call to 0491 570 157, and an alert to Lyrebird Life's fraud team for Theo to decide |
| **Foundry on show** | Azure Content Understanding, plus Azure AI Search over the financial crime red flags guide |
| **Talking point** | "It's explainable, not a black box, and it never accuses the client." |

### 5. Underwriting Agent (Uma) · `/agents/underwriting`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. **Engage agent**: the MCP tools look up the insurer, check the requirements and generate the application summary PDF. 3. Review the requirements and the decision |
| **Pick** | `REQ-20261103-LIF001` (Priya) for a tele-interview and the back and spine exclusion. `REQ-20261109-LIF003` (George) for medical and financial evidence and the 25% loading, which takes the premium from $8,620 to $10,000 a year |
| **Look for** | The duty to take reasonable care explained before any health question, and no questions about genetic tests. The requirements for each life insured. The decision explained in plain English. The PDF |
| **Foundry on show** | MCP tools `underwriting_lookupInsurer`, `underwriting_checkRequirements` and `underwriting_generateApplicationSummaryPdf` |
| **Talking point** | "Answer every question carefully — it protects your claim later." |

### 6. Claims Agent (Ben) · `/agents/claims`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. Confirm the claim inputs. 3. **Engage agent**: the MCP claim tools run, a Teams approval card is sent, and the claim is lodged only after approval |
| **Pick** | `REQ-20261111-LIF004` (Tom) for $5,600 a month against the $7,000 insured. `REQ-20261113-LIF005` (Robert) for a $1,000,000 death claim, the nomination check and the super cover found. `REQ-20261106-LIF002` (Grace) for a policy check that stops at Financial Crime, with no claim lodged |
| **Look for** | The tool sequence in **Agent Prompt & Tools**: `claims_validateClaimant` → `claims_checkPolicy` → `claims_calculateBenefit` → `claims_checkApprovalRules` → `claims_requestApproval` → `claims_getApprovalStatus` → `claims_lodgeClaim`. Then the adaptive card in Teams, and the insurer's Code of Practice timeframes |
| **Foundry on show** | MCP tools plus a Microsoft Teams adaptive-card approval (human in the loop) |
| **Talking point** | "`claims_lodgeClaim` simply refuses until a person approves." |

### 7. Client Communications Agent (Cara) · `/agents/client-communications`

| | |
|---|---|
| **Try it out** | 1. Select a case. 2. **Engage agent**. 3. Read Cara's case summary. 4. Review the drafts: the client letter, the referral-partner update and the SMS. Then open the **Voice chat** tab |
| **Pick** | `REQ-20261113-LIF005` (Robert) for tone. `REQ-20261103-LIF001` (Priya) for the outcome-only update to Hannah and the voice question. `REQ-20261111-LIF004` (Tom) for the letter that follows Amelia's call. `REQ-20261106-LIF002` (Grace) for the reassurance letter |
| **Look for** | A different tone for each audience. No health details for referral partners. The compliance points: no advice, and IDR and AFCA rights. Voice answers grounded in the case |
| **Foundry on show** | A declarative agent plus Voice Live (`client-communications-voice-agent`) |
| **Talking point** | "The hardest conversations get the most care, and a person approves every one." |

### 8. Team Leader Agent (Theo) · `/agents/team-leader`

| | |
|---|---|
| **Try it out** | 1. Pick a case. 2. **Engage agent** to see the practice dashboard, the clocks and the approvals. Then open **Group chat**, where Theo's agent coordinates the seven specialists on one case |
| **Pick** | `REQ-20261109-LIF003` (George) for the second review. `REQ-20261106-LIF002` (Grace) for the block and the insurer alert. `REQ-20261113-LIF005` (Robert) for compassionate oversight |
| **Look for** | The approval queue (SoAs, claims, blocks and second reviews). The IDR clocks and the insurers' Code of Practice timeframes. A turn from each specialist in the group chat |
| **Foundry on show** | Multi-agent group chat orchestration |
| **Talking point** | "Nobody waits without knowing why." |

---

## Talking Points to Have Ready

- **No advice from agents.** Agents never give personal advice. They gather facts, check, calculate, compare, draft and recommend. Amelia gives the advice and signs every Statement of Advice.
- **People approve:**
  - every Statement of Advice and Record of Advice, which the adviser signs
  - replacing or cancelling any existing cover
  - accepting a loading, an exclusion or a decline at underwriting
  - lodging any claim, and every death or terminal illness claim
  - blocking a request on financial-crime grounds, and referring suspected fraud to an insurer
  - any change to policy ownership, beneficiaries or bank details
  - advice with total cover of $2,000,000 or more on one case, which gets a second review
  - complaint responses
  - any communication to a fraud victim, a seriously ill client or a bereaved family
- **Fictional people, real rules.** All people, insurers, super funds and businesses are fictional. The rules and numbers are real FY2026-27 settings (see [background.md](background.md)).
- **Grounded answers.** Every answer is grounded and cited. Show the **Reference** tab.
- **Privacy.** Health information is treated as sensitive, and referral partners only hear what the client agreed to share.

## If Something Goes Wrong

- **Foundry isn't reachable:** the agent pages fall back to **offline** results for the five scenario cases. The figures match the scenario docs, so the story still works. Say plainly that the run is offline.
- **Voice isn't working:** show the drafts and the **Agent Prompt & Tools** tab, and describe Voice Live.
- **The Teams card doesn't arrive:** show the `claims_requestApproval` call and the pending status. The point is that lodgement is refused until a person approves.
- **The MCP tools don't respond:** check that `APP_MCP_URL` is reachable from Foundry. Meanwhile, show the tool definitions in **Agent Prompt & Tools** and the offline result.
- **A run is slow:** open **Agent Prompt & Tools** or **Agent Input** while it streams, and narrate what the agent was given.
