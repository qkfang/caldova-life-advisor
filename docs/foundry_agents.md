# Mapping Practice Staff into Foundry Agents

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Core Design Idea

Each staff character at **Life Advice Guru** becomes a specialised **Microsoft Foundry** agent.

*Advice Team in a Day* doesn't build one generic "insurance chatbot". It models a digital advice practice, where every agent owns a specific part of the request lifecycle:

> **Discover → Verify → Advise → Implement → Confirm**

All agents read from and write to one shared object, the **Client Case**. Amelia, Life Advice Guru's adviser, gives the advice and signs every Statement of Advice. The agents gather facts, explain rules, calculate needs and benefits, compare products, draft documents and recommend, for human approval.

**How it's built**

- The agents are declarative Foundry agents created from code with `Azure.AI.Projects`.
- They share a `GuruAgent` base class, which wires up two grounding sources:
  - **Azure AI Search** over the `life_knowledge` index
  - **Bing**
- The agents that act on systems call **MCP tools** served by the backend at `/mcp` (server label `guru-ops-mcp`): Ivy's Insurance Research Agent, Uma's Underwriting Agent and Ben's Claims Agent.
- Every agent has its own page in the backend console at `/agents/{route id}`.

---

## Shared Client Case Object

All agents read from and write to a common case record. In the backend, the case header is the `CaseRecord` held by `CaseStore`. The store is seeded with the five scenario cases, and the Onboarding page creates new `REQ-…` numbers.

The example column shows Priya's case ([scenario 1](scenario-1-family-protection.md)).

| Field | Description | Example: `REQ-20261103-LIF001` |
|---|---|---|
| `CaseNumber` | Case reference, `REQ-YYYYMMDD-XXXXXX` | `REQ-20261103-LIF001` |
| `SampleId` | The onboarding sample the case came from | `onboarding-priya` |
| `ClientName` | The client: the person advised, or the claimant | Priya Natarajan |
| `ClientEmail` | Client email | priya.natarajan@example.com |
| `ClientPhone` | Client phone | 0491 570 156 |
| `ClientNumber` | Life Advice Guru client number, `CL-…` | `CL-104582` |
| `Cover` | Existing or requested cover, in one line | Riverbend Super death and TPD $210,000 (keep); Kestrel Retail Super death $50,000 (at risk 30 Nov 2026); nothing outside super |
| `RequestType` | New protection advice · Trauma claim and bank change · Business protection advice · Income protection claim · Death claim | New protection advice |
| `ReceivedDate` | Date the request arrived | Tue 3 Nov 2026 |
| `AdviserName` | Life Advice Guru's adviser, always "Amelia, Senior Risk Adviser" | Amelia, Senior Risk Adviser |
| `ReferredBy` | Referral partner and firm, or "Existing client" | Hannah Lewis, Yarra Home Loans |
| `RequestSummary` | One-paragraph summary of the request | New family protection advice after buying a home with a $680,000 joint mortgage |
| `Amount` | The headline figure | $680,000 joint mortgage to protect |
| `PreferredContact` | How the client wants to hear from Life Advice Guru, and what may be shared | Mobile or email. Updates to Hannah: the outcome only |
| `Priority` | Standard, High or Urgent | High |
| `PriorityReason` | Why | Existing cover at risk: the Kestrel Retail Super death cover stops on 30 Nov 2026 unless Priya elects in writing to keep it, and the family has a new $680,000 mortgage with no cover outside super |
| `CreatedAt` | When the case was opened | When Olivia's agent opened the case |

As the case moves through the practice, each agent adds its work to the case:

| What the agent adds | Added by | Example for Priya |
|---|---|---|
| Fact find and in-good-order checklist | Onboarding Agent | Goals, dependants, debts, income, health basics and existing cover. Commission consent to be collected with the SoA |
| Identity and risk | Financial Crime Agent | Driver licence and payslip legit, KYC complete, no PEP or sanctions match: clear |
| Needs analysis and structure | Paraplanning Agent | Life $750,000, TPD $600,000, trauma $150,000, income protection $6,125 a month. Life and any-occupation TPD inside super |
| Research shortlist | Insurance Research Agent | Corella Life $3,180, Harbourline Life $3,412, Lyrebird Life $3,690 (year 1, stepped) |
| Statement of Advice | Paraplanning Agent, then Amelia | Drafted 5 Nov, signed by Amelia 6 Nov, Authority to Proceed 7 Nov |
| Underwriting | Underwriting Agent | Standard rates, with a back and spine exclusion on income protection, accepted with Amelia on 17 Nov |
| Policy | Underwriting Agent | Corella Life `CR-26118204`, from 18 Nov 2026 |
| Claim | Claims Agent | Not applicable |
| Approvals | Every agent, decided by people | SoA signed by Amelia; exclusion accepted by Priya with Amelia |
| Communications | Client Communications Agent | Welcome letter and policy summary (Priya); outcome-only update (Hannah) |
| Next action | The agent working the case | Annual review with Amelia |

---

# Agent Mapping

## 1. Onboarding Agent

**Based on:** Onboarding & Requests Officer (Olivia)

**Foundry agent:** `onboarding-agent`

**Page:** `/agents/onboarding`

### Purpose

Capture every request accurately, run the fact find and the in-good-order checklist, open the Client Case and route it.

### Inputs

- An enquiry or referral email, or a claim notification by email or phone
- The fact-find questionnaire and attachments: ID, payslips, policy schedules, death certificates
- Existing client records and policies
- Contact details and preferences, and any consent to share with a referral partner

### Tools

- Azure AI Search over `life_knowledge`: *Guide — in-good-order checklist for advice and claims*, *Life Advice Guru — Financial Services Guide and remuneration*, *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules*, and the case files
- Bing grounding
- Structured field extraction into the Client Case
- Backend `/onboarding/*` endpoints, which open the case in `CaseStore` and route it

### Outputs

- The Client Case, with every extracted field, TFNs masked and health details marked sensitive
- The in-good-order checklist, with each item passed, missing or flagged
- Existing-cover flags, such as default cover in super that is about to stop
- The request type and priority, with a reason
- Vulnerable-client flags
- The routing decision and next action
- A draft "what we still need" note

### Example Instruction

> You are the Onboarding Agent for Life Advice Guru, supporting Olivia, the Onboarding & Requests Officer. Read the enquiry, referral or claim notification and its attachments. Extract the goals, dependants, debts, income, occupation, health basics and existing cover into the Client Case. Run the in-good-order checklist for this request type, classify the request, set a priority with a reason, and route the case: new advice to Paraplanning, claims to Claims, and every new client to Financial Crime. Mask TFNs and treat health information as sensitive. Flag vulnerable clients, including anyone who is seriously ill, injured, bereaved or in financial stress. Never give advice.

### Human Approval

Usually not required, unless:

- A vulnerable client is identified
- Identity can't be verified
- The request may not come from the client or someone they have authorised
- The request includes a change to policy ownership, beneficiaries or bank details
- The instructions conflict

---

## 2. Paraplanning Agent

**Based on:** Paraplanner (Penny)

**Foundry agent:** `paraplanning-agent`

**Page:** `/agents/paraplanning`

### Purpose

Work out how much cover is needed and how to hold it, and draft the Statement of Advice for Amelia, citing each rule.

### Inputs

- The Client Case and fact find
- Existing cover, including cover in super
- The client's budget for premiums
- For business protection: the business valuation and the profit at risk
- The research shortlist from Insurance Research

### Tools

- Azure AI Search over `life_knowledge`, using:
  - *Advice rules — best interests duty, SoA and replacement advice*
  - *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules*
  - *Tax on life insurance — premiums and benefits*
  - *Commission rules — LIF caps, clawback and consent*
  - *Life Advice Guru — Financial Services Guide and remuneration*
  - the insurers' PDS extracts
- Product and rule document texts used for citations (`ProductDocuments`)
- Bing grounding
- Backend `/paraplanning/*` endpoints

### Outputs

- The needs analysis for each cover, with every step of the working
- The ownership structure: inside super, outside super or super-linked, and who owns each policy
- The existing-cover and replacement analysis
- The Statement of Advice draft, with premiums and commission in dollars
- A second-review flag when total cover is $2,000,000 or more
- A list of anything still missing

### Example Instruction

> You are the Paraplanning Agent for Life Advice Guru, supporting Penny, the Paraplanner. From the Client Case, calculate life, TPD, trauma and income protection needs, showing every step and the existing cover you have taken into account. Draft the ownership structure and the Statement of Advice for Amelia: scope, strategy, ownership, best-interests steps, the replacement analysis, and premiums and commission in dollars. Cite the title of every rule document you rely on. Never invent figures or rules. You draft; Amelia gives the advice and signs. Flag any case with total cover of $2,000,000 or more for a second review, and never recommend cancelling existing cover without a human decision.

### Human Approval

Required for:

- Every Statement of Advice and Record of Advice, which Amelia signs
- Replacing or cancelling any existing cover
- Advice with total cover of $2,000,000 or more on one case, which gets a second review by Theo

---

## 3. Insurance Research Agent

**Based on:** Insurance Research Analyst (Ivy)

**Foundry agent:** `insurance-research-agent`

**Page:** `/agents/insurance-research`

### Purpose

Compare the insurers on Life Advice Guru's approved product list (APL) on definitions, features, exclusions and premiums, and build the comparison workbook.

### Inputs

- The Client Case and the needs analysis: cover types and sums insured
- The client's age, occupation and smoker status
- The insurers' product sheets and PDS extracts
- Life Advice Guru's APL and research method

### Tools

- MCP tools (`guru-ops-mcp`):
  - `research_analysePolicyDocument`: reads a product sheet or PDS extract for definitions, features and exclusions
  - `research_compareInsurers`: compares the APL insurers for the client's cover, including stepped and level premiums, definitions and features
  - `research_generateComparisonWorkbook`: builds an Excel workbook with a premium chart, downloadable from `/research/download/{fileName}`
- Azure AI Search over the insurers' PDS extracts and *Life Advice Guru — Approved product list and research method*
- Backend `/research/*` endpoints

### Outputs

- A comparison of definitions, features and exclusions that matter for the client
- Year-1 premiums across the APL insurers, stepped and level
- The comparison workbook and chart
- A shortlist and notes for Amelia

### Example Instruction

> You are the Insurance Research Agent for Life Advice Guru, supporting Ivy, the Insurance Research Analyst. Use the MCP tools to read each APL insurer's product sheet, compare the definitions, features, exclusions and premiums for the client's cover, and build the comparison workbook. Explain the definitions that decide whether a claim would pay, such as own-occupation TPD for the client's occupation. Compare stepped and level premiums. Shortlist with reasons, but never make a personal recommendation: the choice of insurer is Amelia's advice.

### Human Approval

Required before:

- Any insurer is recommended to a client (Amelia's advice)
- Any comparison is shared with a client (it goes through Amelia)

---

## 4. Financial Crime Agent

**Based on:** Financial Crime Investigator (Felix)

**Foundry agent:** `financial-crime-agent`

**Page:** `/agents/financial-crime`

### Purpose

Verify identity, check documents, and detect fraud, scams and identity takeover, then recommend controls.

### Inputs

- The Client Case and request
- The activity log: contact and bank-detail changes, devices, IP locations and portal logins
- Documents on the case: IDs, medical reports, death certificates, payslips, BAS, bank statements and company extracts
- Travel notices and file notes
- KYC, politically exposed person (PEP) and sanctions screening results

### Tools

- **Azure Content Understanding**, using a shared authenticity field schema: document type, issuer, holder name, document number, issue and expiry dates, tamper indicators, security features, and a consistency summary
- A deterministic checks layer that turns the extracted fields into a verdict: legit, suspicious, tampered or fake
- Azure AI Search over *Guide — financial crime red flags and controls for life insurance* and the case files
- Bing grounding
- Backend `/fincrime/*` endpoints

### Outputs

- An explainable risk rating (low, medium or high), with its indicators
- The risk timeline
- A verdict for each document, with the checks behind it and the reasons
- Recommended actions: a hold, restoring verified details, locking portal access, a call-back on the verified number, alerting the insurer's fraud team
- A one-page summary for the Team Leader

### Example Instruction

> You are the Financial Crime Agent for Life Advice Guru, supporting Felix, the Financial Crime Investigator. Verify identity for new clients, including companies and their beneficial owners. Review the activity log, documents and request for signs of fraud, scams or identity takeover. Explain every risk indicator in plain language and link it to a known pattern. Recommend controls such as a hold, or a call-back on the verified number on file, never a number in the request. Never accuse the client, who may be the victim. Decisions to block a request or refer suspected fraud to an insurer belong to people.

### Human Approval

Always required before:

- Blocking a request on financial-crime grounds
- Referring suspected fraud to an insurer
- Any change to policy ownership, beneficiaries or bank details
- Contacting a suspected fraud victim
- Closing an alert as a false positive

### Document Authenticity (Try it out)

The **Try it out** flow on `/agents/financial-crime` checks each document attached to a case:

1. **Scan.** Each document goes through Azure Content Understanding with the shared authenticity schema.
2. **Verdict.** A small deterministic rules layer gives it a verdict of *legit*, *suspicious*, *tampered* or *fake*. *Tampered* means the details pass the identity check but the image itself has been edited, as with Grace's driver licence.
3. **Findings.** The page lists every check performed and the reasons any check failed.
4. **Risk narrative.** The findings feed into Felix's risk narrative, so document red flags are cited by document.

The 12 sample documents cover all five scenarios: genuine IDs, a payslip, a company extract, a BAS and profit and loss summary, a surgeon's report and a certified death certificate, plus the fake oncologist's report, the tampered licence and the suspicious bank statement in Grace's case. Every sample is visibly marked "SPECIMEN — NOT A REAL DOCUMENT". See [`src/backend/README.md`](../src/backend/README.md) for the full flow and endpoint list.

---

## 5. Underwriting Agent

**Based on:** Underwriting Coordinator (Uma)

**Foundry agent:** `underwriting-agent`

**Page:** `/agents/underwriting`

### Purpose

Take each application through underwriting with the insurer, explain the client's duty first, and get the policy issued.

### Inputs

- The Client Case, the signed SoA and the Authority to Proceed
- The application answers and health disclosures
- The insurer's requirements for the client's age and sums insured
- The evidence: tele-interviews, blood results, medical examinations, GP reports and financial evidence

### Tools

- MCP tools (`guru-ops-mcp`):
  - `underwriting_lookupInsurer`: the APL directory, with contacts, service levels and underwriting thresholds
  - `underwriting_checkRequirements`: the medical and financial evidence needed for the client's age and sums insured
  - `underwriting_generateApplicationSummaryPdf`: the application and disclosure summary PDF
- Azure AI Search over *Underwriting and the duty to take reasonable care*, *Directory — insurers on the approved product list* and the insurers' PDS extracts
- Bing grounding
- Backend `/underwriting/*` endpoints

### Outputs

- The application, with the duty to take reasonable care explained first
- The requirements list and tracker, with chasers
- The underwriting decision explained in plain English
- The application summary PDF
- Confirmation of the policy number, the start date and the cooling-off period

### Example Instruction

> You are the Underwriting Agent for Life Advice Guru, supporting Uma, the Underwriting Coordinator. Before any health question, explain the duty to take reasonable care in plain English. Use the MCP tools to look up the insurer, check the medical and financial requirements for the client's age and sums insured, and generate the application summary. Track the requirements and explain each decision (standard rates, a loading or an exclusion) without jargon. Treat health information as sensitive, and never ask for or use genetic test results. Never accept a loading, an exclusion or a decline on the client's behalf: that is the client's decision with Amelia.

### Human Approval

Required for:

- Accepting a loading, an exclusion or a decline
- Replacing or cancelling any existing cover once the new cover is in force
- Any change to policy ownership or beneficiaries

---

## 6. Claims Agent

**Based on:** Benefit Claims Advocate (Ben)

**Foundry agent:** `claims-agent`

**Page:** `/agents/claims`

### Purpose

Support clients and families through a claim: check the policy and the evidence, build the claim pack, calculate the benefit, and lodge the claim only after approval.

### Inputs

- The Client Case and the claim notification
- The policy schedule and the insurer's PDS extract
- The evidence: medical reports, death certificates and earnings evidence
- The claimant's identity and any authority (support person or nominated beneficiary)
- The authority and escalation matrix

### Tools

- MCP tools (`guru-ops-mcp`):
  - `claims_validateClaimant`
  - `claims_checkPolicy`
  - `claims_calculateBenefit`
  - `claims_checkApprovalRules`
  - `claims_requestApproval`: posts a Microsoft Teams adaptive card through `teams_sendApprovalCard`
  - `claims_getApprovalStatus`
  - `claims_lodgeClaim`: refuses unless the claim is approved
- Azure AI Search over *Claims rules — Life Insurance Code of Practice and income protection benefits*, *Tax on life insurance — premiums and benefits*, *Matrix — authority, approvals and escalation* and the insurers' PDS extracts
- Backend `/claims/*` endpoints

### Outputs

- The claimant and policy check
- The claim pack
- The benefit calculation, explained in plain English
- The Teams approval request and its status
- The lodgement confirmation and the Code of Practice timeframe tracker
- Any other cover found, such as cover in super

### Example Instruction

> You are the Claims Agent for Life Advice Guru, supporting Ben, the Benefit Claims Advocate. Validate the claimant and check the policy: definition, waiting period, exclusions, who is paid and the evidence. Build the claim pack and calculate the expected benefit, showing your working and citing the PDS extract. Check the approval rules, request approval in Microsoft Teams, and lodge the claim only when it is approved. Track the insurer's Code of Practice timeframes. Be gentle with people who are ill, injured or bereaved. Never promise an outcome the insurer hasn't decided, and never accuse a claimant of fraud: refer concerns to Financial Crime.

### Human Approval

Required for:

- Lodging any claim, and every death or terminal illness claim
- Any change to bank details for a claim payment
- Referring suspected fraud to an insurer
- Any communication to a seriously ill client or a bereaved family

---

## 7. Client Communications Agent

**Based on:** Client Communications Specialist (Cara)

**Foundry agent:** `client-communications-agent`

**Page:** `/agents/client-communications`

### Purpose

Draft clear, empathetic and compliant communications for clients, families and referral partners.

### Inputs

- The Client Case and the outcome: a signed SoA, an underwriting decision or a claim outcome
- The audience (client, family or referral partner) and what the client agreed to share
- The situation: new cover, a loading or exclusion, an injury or illness, a scam or a bereavement
- Contact preferences

### Tools

- The drafting service (`ClientCommunicationsDrafter`)
- Azure AI Search over the case files and rule documents, for facts and required disclosures
- A compliance check: no personal advice, the required disclosures, and IDR and AFCA rights
- Bing grounding
- Backend `/communications/*` endpoints

### Outputs

- A case summary
- A client letter
- A referral-partner update, limited to what the client agreed to share
- An SMS
- A call script

### Example Instruction

> You are the Client Communications Agent for Life Advice Guru, supporting Cara, the Client Communications Specialist. Explain the case outcome in clear, warm, plain English for the client, and in concise professional terms for a referral partner, sharing only what the client agreed to share and never health details without consent. Match the tone to the situation. Include the required disclosures and complaint rights. Never give personal advice, invent facts, or promise outcomes that haven't been decided.

### Human Approval

Required for:

- Complaint responses
- Any communication to a fraud victim, a seriously ill client or a bereaved family
- Anything that could read as advice, which goes to Amelia

---

## 7a. Client Communications Voice Agent

**Based on:** Client Communications Specialist (Cara)

**Foundry agent:** `client-communications-voice-agent` (class `ClientCommunicationsVoiceAgent`, factory key `communications-voice`)

**Page:** the **Voice chat with Cara** panel on `/agents/client-communications`

### Purpose

Let clients, families and referral partners talk to Cara by voice about a case.

### Inputs

- The selected Client Case
- The caller's spoken questions

### Tools

- **Voice Live**, for real-time speech in and out, through the backend's Voice Live proxy (`CommunicationsVoiceLiveProxy`)
- The same `life_knowledge` grounding as the other agents

### Outputs

- Spoken answers about case status, timing and next steps
- An offer of a call-back from a person when needed

### Example Instruction

> You are Cara's voice assistant at Life Advice Guru. Speak warmly and simply. Explain case status and next steps using only facts from the Client Case. Don't make decisions, give advice, or confirm anything that hasn't been decided. Never discuss health details unless you are sure you are speaking with the client. For complaints, vulnerable callers or identity concerns, offer a call-back from a person.

### Human Approval

Not required to explain status that has already been decided. Hand over to a person for:

- complaints
- serious illness, injury or bereavement
- identity verification
- anything that needs advice

---

## 8. Team Leader Agent

**Based on:** Practice Team Leader (Theo)

**Foundry agent:** `team-leader-agent`

**Page:** `/agents/team-leader`

### Purpose

Lead the practice: monitor the pipeline, service levels and complaint clocks, prepare approvals and second reviews, audit file quality, and orchestrate the team on a case.

### Inputs

- All open cases
- Agent outputs and recommendations
- Underwriting, claim and IDR clock status
- Approval requests
- Complaints and escalations
- The authority and escalation matrix

### Tools

- Azure AI Search over *Matrix — authority, approvals and escalation*, *Claims rules — Life Insurance Code of Practice and income protection benefits*, *Advice rules — best interests duty, SoA and replacement advice* and the case files
- The practice dashboard, through `/team-leader/*`
- A multi-agent group chat (`TeamLeaderGroupChatApi`), in which Theo's agent coordinates the seven specialist agents
- Bing grounding

### Outputs

- A pipeline dashboard summary
- Alerts for service levels, Code of Practice timeframes and IDR clocks
- Approval briefs and second-review briefs
- File quality findings against the best interests duty
- A group chat transcript with a consolidated case plan

### Example Instruction

> You are the Team Leader Agent for Life Advice Guru, supporting Theo, the Practice Team Leader. Monitor the advice pipeline, underwriting requirements, claims and complaint clocks, and identify cases at risk. Summarise the items that need approval, prepare second-review briefs for advice with total cover of $2,000,000 or more, and audit files against the best interests duty. In group chat, coordinate the specialist agents on one case and produce a consolidated plan. Never approve on Theo's behalf, and never override an approval rule.

### Human Approval

Theo, the human team leader, approves:

- Second reviews of advice with total cover of $2,000,000 or more
- Blocking a request on financial-crime grounds, and referring suspected fraud to an insurer
- Claim lodgements in Microsoft Teams, including every death or terminal illness claim
- Changes to policy ownership, beneficiaries or bank details
- Complaint responses
- Communications to fraud victims, seriously ill clients and bereaved families

---

# Suggested Multi-Agent Workflow

## Workflow: Discover to Confirm

1. `onboarding-agent` (**Discover**)
   - Captures the request, runs the fact find and the in-good-order checklist.
   - Opens the Client Case and routes it.

2. `financial-crime-agent` (**Verify**)
   - Verifies every new client (KYC, PEP and sanctions).
   - Runs whenever documents, contact changes or bank details need checking.
   - Produces a risk summary and recommended controls.

3. `paraplanning-agent` (**Advise**)
   - Works out the needs and the structure.
   - Drafts the Statement of Advice for Amelia, who gives the advice and signs it.

4. `insurance-research-agent` (**Advise**)
   - Compares the APL insurers on definitions, features and premiums.
   - Builds the comparison workbook.

5. `underwriting-agent` (**Implement**)
   - Takes the application through underwriting after the Authority to Proceed.
   - Explains each decision, and confirms the policy is issued.

6. `claims-agent` (**Implement**)
   - Checks the policy and evidence, calculates the benefit, and requests approval in Microsoft Teams.
   - Lodges the claim only after approval, and tracks the insurer's timeframes.

7. `client-communications-agent` and `client-communications-voice-agent` (**Confirm**)
   - Keep the client, family and referral partner informed throughout.
   - Draft the final letters.

8. `team-leader-agent` (all stages)
   - Monitors the workflow, prepares approvals and second reviews.
   - Orchestrates the group chat on complex cases.

---

# Recommended Agent Architecture

## Option A: One Agent per Department

Use this when the story needs to match Guru House, where every person has an agent of their own. This is how the app is built: each department page engages its own agent.

| Department | Foundry agent id |
|---|---|
| Onboarding & Requests | `onboarding-agent` |
| Paraplanning | `paraplanning-agent` |
| Insurance Research | `insurance-research-agent` |
| Financial Crime | `financial-crime-agent` |
| Underwriting & New Business | `underwriting-agent` |
| Claims | `claims-agent` |
| Client Communications | `client-communications-agent`, plus the voice agent `client-communications-voice-agent` |
| Team Leader Office | `team-leader-agent` |

Best for:

- Storytelling
- A visual department layout
- Clear responsibilities
- Agent-to-agent handoffs

---

## Option B: Orchestrator plus Specialist Agents

Use this when you need stronger workflow control.

- **Orchestrator:** `team-leader-agent` decides which specialist works next and when a human approval gate is reached.
- **Specialists:** `onboarding-agent`, `paraplanning-agent`, `insurance-research-agent`, `financial-crime-agent`, `underwriting-agent`, `claims-agent` and `client-communications-agent`.

Theo's **group chat** on the Team Leader page shows this pattern on a single case.

Best for:

- A more realistic enterprise workflow
- Complex branching
- Human approval gates
- Easier observability

---

# Suggested Foundry Objects

| Object | Used by | Notes |
|---|---|---|
| **Client Case** | All agents | The central object |
| **Client** | Onboarding, Communications | The life insured, policy owner, beneficiary or claimant, with contact preferences and vulnerability flags |
| **Adviser** | Paraplanning, Communications, Team Leader | Amelia: every SoA and RoA she signs |
| **Referral partner** | Onboarding, Communications | The broker or accountant, and what the client agreed to share with them |
| **Fact find** | Onboarding, Paraplanning | Goals, dependants, debts, income, occupation, health basics and existing cover |
| **Needs analysis** | Paraplanning | Each cover, the working and the ownership structure |
| **Insurer** | Insurance Research, Underwriting, Claims | The APL insurers: Harbourline Life, Corella Life, Lyrebird Life and Northbank Life |
| **Policy** | Underwriting, Claims | Policy number with the insurer's prefix, cover, sums insured, premiums, owner and nominated beneficiaries |
| **Document** | Onboarding, Financial Crime, Claims | IDs, medical reports, death certificates, payslips, BAS and bank statements |
| **Rule** | Paraplanning, Underwriting, Claims | The PDS extracts, Life Advice Guru's guides and the rule documents in `life_knowledge` |
| **Application** | Underwriting | Answers, disclosures, requirements and the insurer's decision |
| **Claim** | Claims | The claim pack, benefit calculation, lodgement and the insurer's timeframes |
| **Approval** | Claims, Team Leader | Microsoft Teams adaptive cards and decisions |
| **Communication** | Client Communications | Letters, referral-partner updates, SMS and calls |
| **Task** | Team Leader, orchestration | Next actions and owners |
| **Escalation / Complaint** | Team Leader | IDR clocks and AFCA |

---

# Suggested Agent Handoff Rules

## Onboarding to Paraplanning

Trigger when:

- A request for new advice is in good order, or only non-blocking items remain

## Onboarding to Claims

Trigger when:

- A claim notification arrives

## Onboarding to Financial Crime

Trigger when:

- A new client is onboarded (every new client, including companies and their beneficial owners)
- Identity, a document or a bank-detail change needs checking

## Paraplanning and Insurance Research

Trigger when:

- The needs analysis is ready and the SoA needs research across the APL
- Research changes the structure or the premiums, and the needs analysis needs updating

## Paraplanning to Underwriting

Trigger when:

- Amelia has signed the SoA and the client has signed the Authority to Proceed

## Underwriting to Client Communications

Trigger when:

- The policy is issued

## Claims to Financial Crime

Trigger when:

- Claim documents need checking
- A claim comes with a bank-detail change, or from contact details that changed recently

## Claims to Client Communications

Trigger when:

- The claim is lodged, decided or paid, and the client or family needs an update

## Financial Crime to Team Leader

Trigger when:

- A block or a fraud referral to an insurer needs approval

## Any Agent to Team Leader

Trigger when:

- Human approval is required
- Advice has total cover of $2,000,000 or more and needs a second review
- A vulnerable client is identified
- A complaint is received, which starts the IDR clock
- A service level or an insurer's timeframe is at risk

Theo's agent coordinates the group chat when a case needs several agents at once.

---

# Human-in-the-Loop Design

Life insurance advice needs repeatable orchestration with firm approval gates. Agents recommend, draft, summarise, check and calculate. The adviser gives the advice, and people approve, override and finalise.

Human approval is **always** required for:

- every Statement of Advice and Record of Advice, which the adviser signs
- replacing or cancelling any existing cover
- accepting a loading, an exclusion or a decline at underwriting
- lodging any claim, and every death or terminal illness claim
- blocking a request on financial-crime grounds, and referring suspected fraud to an insurer
- any change to policy ownership, beneficiaries or bank details
- advice with total cover of $2,000,000 or more on one case, which gets a second review
- complaint responses
- any communication to a fraud victim, a seriously ill client or a bereaved family

How the app makes the human role visible:

- **Paraplanning** drafts the SoA for Amelia. It never issues advice.
- **Claims** won't lodge a claim until a person approves. `claims_requestApproval` posts a Microsoft Teams adaptive card, and `claims_lodgeClaim` refuses until the card is approved.
- **Team Leader** turns every approval and second review into a one-page brief. The group chat ends with a plan for Theo to sign off.
- **Every engage step** shows five tabs: Agent Narrative, Reference, Agent Prompt & Tools, Agent Input and Raw Output. Viewers can see what the agent was told, which tools it used, what it cited and what it produced.

---

# Presenter-Friendly Interaction Model

This walk-through follows Priya's family protection advice ([scenario 1](scenario-1-family-protection.md)). It is the core of the golden path in [agent-demo.md](agent-demo.md).

## Step 1: A referral arrives

Hannah Lewis of Yarra Home Loans emails Life Advice Guru on 3 Nov 2026:

> With her consent, I'm referring Priya Natarajan. Priya and her partner Arjun have just bought in Coburg with a $680,000 joint mortgage, and they'd like to protect the loan and the family. Priya's fact find is attached.

The `onboarding-agent` extracts the fact find, runs the in-good-order checklist and opens `REQ-20261103-LIF001`. It flags that the $50,000 of death cover in Priya's old Kestrel Retail Super account stops on 30 Nov 2026 unless she tells the fund to keep it.

## Step 2: Identity

The `financial-crime-agent` says:

> Priya's driver licence and payslip are legit, KYC is complete, and there's no PEP or sanctions match. Clear.

## Step 3: The needs analysis

The `paraplanning-agent` says:

> Priya's life cover need is $1,010,000: the $680,000 mortgage, $100,000 for the children's education, $200,000 of family income support and $30,000 for final expenses and an emergency fund. Less $45,000 of savings and $210,000 of existing death cover in Riverbend Super, the shortfall is $755,000, so I've drafted $750,000. TPD is $600,000, trauma is $150,000, and income protection is $6,125 a month, 70% of her income, with a 90-day waiting period to age 65.

## Step 4: The research

The `insurance-research-agent` says:

> For the full package, year-1 stepped premiums are Corella Life $3,180, Harbourline Life $3,412 and Lyrebird Life $3,690. Corella Life is the lowest cost and has own-occupation TPD for physiotherapists. Its level-premium option is $4,050 in year 1. The workbook is ready for Amelia.

## Step 5: The Statement of Advice

The `paraplanning-agent` drafts the SoA on 5 Nov, with no advice fee and the commission in dollars: $1,908 upfront and $636 a year from year 2. **Amelia reviews and signs it on 6 Nov**, and Priya signs the Authority to Proceed on 7 Nov.

## Step 6: Underwriting

The `underwriting-agent` explains the duty to take reasonable care, lodges the application with Corella Life on 9 Nov, and reports the decision on 16 Nov:

> Life, TPD and trauma are accepted at standard rates, and the asthma is accepted at standard. Income protection is accepted with an exclusion for back and spine conditions, which Corella Life will review after two symptom-free years. Accepting it is Priya's decision with Amelia.

Priya accepts with Amelia on 17 Nov. Policy `CR-26118204` starts on 18 Nov 2026.

## Step 7: Client communication

The `client-communications-agent` drafts a welcome letter and a plain-English policy summary for Priya, and an update for Hannah:

> Good news: Priya's cover is now in place. Thanks for the referral.

The update to Hannah has no health details, because Priya agreed to share the outcome only. If Priya calls, Cara's voice agent can answer questions such as "When does my cover start?". The answer is Wednesday 18 November 2026.

## Step 8: The team leader dashboard

The `team-leader-agent` shows the practice for the five scenario cases:

| Case | Client | Status |
|---|---|---|
| `REQ-20261103-LIF001` | Priya Natarajan | ✅ Completed. Corella Life policy `CR-26118204` in force from 18 Nov 2026, with a back and spine exclusion on income protection accepted with Amelia |
| `REQ-20261106-LIF002` | Grace Williams | 🛡️ Blocked. No claim lodged and $200,000 protected. Lyrebird Life's fraud team alerted with Theo's approval |
| `REQ-20261109-LIF003` | George Papadopoulos and Harpreet Kaur | ✅ Completed. Second review done. Three Northbank Life policies in force from 20 Nov 2026, with a 25% loading on George's cover |
| `REQ-20261111-LIF004` | Tom Bradley | 🟡 Partially approved. Accepted at $5,600 a month, not the $7,000 insured. First payment about 13 Dec 2026 |
| `REQ-20261113-LIF005` | Robert Chen (for Mei Chen) | ✅ Completed. $1,000,000 paid to Robert on 23 Nov 2026, and the $180,000 super claim under way |

---

# UI Mapping to Guru House

In the Guru House storybook ([storybook.md](storybook.md)), every person's card on Level 2 and every agent's, assistant's and voice agent's card on Level 1 has an **Open the agent page ↗** link that opens that agent's page (`/agents/{route id}`) in a new tab.

| Agent page | Shows |
|---|---|
| Onboarding (Olivia) | New requests, the extracted Client Case, the in-good-order checklist and existing-cover flags |
| Paraplanning (Penny) | The needs analysis with its working, the ownership structure and the SoA draft for Amelia |
| Insurance Research (Ivy) | The insurer comparison, stepped and level premiums, the chart and the workbook |
| Financial Crime (Felix) | Risk indicators, the risk timeline and document verdicts |
| Underwriting (Uma) | The duty to take reasonable care, requirements, the underwriting decision and the application summary PDF |
| Claims (Ben) | The policy and evidence check, the benefit calculation, the Teams approval status and the lodgement |
| Client Communications (Cara) | The message drafts and the voice chat with Cara |
| Team Leader (Theo) | The practice dashboard, service-level and IDR clocks, approvals and second reviews, and the multi-agent group chat on one case |

The house, read from the top down:

- **Level 3, the idea:** Maya's office under the copper dome, with her idea, "the brain".
- **Level 2, the people:** the open-plan office inside the old brick shell, with Claims & Care on the left, Protection Advice on the right, and Olivia and Theo in the middle.
- **Level 1, the agents:** the old banking chamber at street level. The old teller counter is now the **Client Case** hub, with a bay under every desk, the MCP tool bench for Ivy's, Uma's and Ben's agents, the group-chat ring at Theo's bay and the voice equaliser at Cara's.
- **B, the engine room:** the old bank vault in the basement, behind a huge round vault door. It holds the Microsoft Foundry core, Microsoft IQ (Work IQ, Fabric IQ, Foundry IQ and Web IQ) and the Azure services the agents run on. Foundry IQ holds the `life_knowledge` index: the records and rules the agents cite.
- **The front desk**, under the heritage verandah beside the tram stop, is where requests come in. Amelia links each visitor with the agents that get them started.

---

# Best Framing

The strongest framing is:

> Each staff member at Life Advice Guru becomes a Foundry agent with a specialised role, governed tools, cited rules and human approval points. Together, they operate as a coordinated digital advice practice. The adviser gives the advice, and people stay in charge of every decision that matters.

This makes the app feel enterprise-ready. It shows AI supporting advice professionals rather than replacing them, and the agents never give personal advice.
