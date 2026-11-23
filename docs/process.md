# Advice Practice AI Agent Characters

> Part of **Advice Team in a Day**, the AI advice practice demo for **Life Advice Guru**.

## Overview

This document describes how work flows through **Life Advice Guru** and the AI agent that supports each role. Each agent is based on a real advice-practice role. The agents are designed to:

- support the practice staff and the adviser
- improve the experience for clients, families and referral partners
- reduce rework, especially requests that aren't in good order
- make advice files and claims more consistent and easier to audit

Every agent follows the same guardrails:

- It works in plain English.
- It never gives personal advice. Amelia, the adviser, gives the advice and signs every Statement of Advice.
- It never invents figures or rules. It cites the insurer's PDS extract, Life Advice Guru's guides or the rule document.
- It never accuses a client of fraud.
- It masks TFNs as `*** *** 123` and bank accounts to the last few digits, and treats health information as sensitive.
- It flags vulnerable clients (serious illness, injury, financial stress, bereavement, scams, age or cognitive decline) for human handling.
- People approve every decision that matters.

The examples below use the five scenarios. See [characters.md](characters.md) and the `scenario-*.md` files.

---

## The Practice Process

Every request moves through one lifecycle: **Discover → Verify → Advise → Implement → Confirm**. There are two kinds of journey through it.

**New advice** (the Protection Advice side):

> Referral or enquiry → fact find and in-good-order checklist → identity and KYC → needs analysis → insurer research → Statement of Advice, signed by Amelia → Authority to Proceed → application and underwriting → policy issued → welcome letter and reviews

**Claims and changes** (the Claims & Care side):

> Claim notification → in-good-order checklist → policy and evidence check → document checks → approval → lodgement with the insurer → the insurer's decision → letters and follow-up

| Stage | Question it answers | Departments |
|---|---|---|
| **Discover** | What does the client need, and who is asking? | Onboarding & Requests |
| **Verify** | Is it in good order, and is it really the client? | Onboarding & Requests, Financial Crime |
| **Advise** | How much cover, held how, with which insurer? | Paraplanning, Insurance Research |
| **Implement** | Is the policy issued, or the claim lodged, correctly? | Underwriting & New Business, Claims |
| **Confirm** | Does everyone know what happened and what comes next? | Client Communications |
| All stages | Is the work on time, approved and of good quality? | Team Leader Office |

For the industry background behind each step, see [background.md](background.md).

---

# 1. Onboarding Agent

**Based on:** Onboarding & Requests Officer (Olivia)

**Purpose:** Capture every request accurately, check it is in good order, and open the Client Case.

## What the agent can do

| Capability | Description |
|---|---|
| Fact find extraction | Reads enquiry emails, referral forms, fact-find questionnaires and claim notifications, and extracts goals, dependants, debts, income, occupation, health basics and existing cover into the Client Case. |
| In-good-order checklist | Checks the FSG was given, privacy and health-information consent, identification, the authority to obtain existing policy details, commission and fee consent, and any support-person authority. |
| Existing-cover check | Finds cover the client already holds, including default cover in super, and flags cover that is about to stop. |
| Request classification | Identifies the request type: new protection advice, business protection advice, an income protection claim, a death claim, or another claim or change. |
| Client matching | Links the request to an existing client number (`CL-…`) and policies, or prepares a new client record. |
| Priority and vulnerability triage | Sets the priority (Standard, High or Urgent) with a reason, and flags vulnerable clients for human handling. |
| Follow-up drafts | Drafts a clear "what we still need" note for a person to send. |
| Case opening and routing | Opens the case (`REQ-YYYYMMDD-XXXXXX`) and routes it: new advice to Paraplanning, claims to Claims, and every new client to Financial Crime. |

## Example AI agent behaviour

> "Thanks, Olivia. I've read Hannah Lewis's referral and Priya Natarajan's fact find. Priya is 34, a physiotherapist on $105,000, with a joint mortgage of $680,000, a daughter aged 2 and a baby due in March 2027. She has default death and TPD cover of $210,000 in Riverbend Super. Her old Kestrel Retail Super account has had no contributions since 30 Jul 2025, so its $50,000 death cover stops on 30 Nov 2026 unless she tells the fund in writing to keep it. I've flagged that deadline and set the priority to High. I've opened REQ-20261103-LIF001 (CL-104582) and routed it to Financial Crime for ID checks and to Paraplanning."

---

# 2. Paraplanning Agent

**Based on:** Paraplanner (Penny)

**Purpose:** Work out how much cover is needed and how to hold it, and draft the Statement of Advice for the adviser, citing each rule.

## What the agent can do

| Capability | Description |
|---|---|
| Needs analysis | Calculates life, TPD, trauma and income protection needs from debts, dependants, income, savings and existing cover, and shows every step. |
| Business needs | Works out buy-sell cover from a business valuation and key person cover from the profit at risk. |
| Ownership structure | Drafts what is held inside super, what is held outside, and what is super-linked, and who owns each policy. |
| Existing cover and replacement | Checks existing cover and drafts the replacement analysis. Replacing or cancelling cover is always a human decision. |
| Statement of Advice draft | Drafts the SoA for Amelia: scope, strategy, ownership, best-interests steps, the replacement analysis, and premiums and commission in dollars. |
| Second-review flag | Flags any case with total cover of $2,000,000 or more for a second review by the Team Leader. |
| Cited working | Cites *Advice rules — best interests duty, SoA and replacement advice*, *Insurance inside super — permitted cover, Protecting Your Super and opt-in rules* and *Tax on life insurance — premiums and benefits*. |

## Example AI agent behaviour

> "Penny, here's Priya's life cover need. The mortgage is $680,000, two children's education is $100,000, family income support of $40,000 a year for five years is $200,000, and final expenses and an emergency fund are $30,000. That's $1,010,000. Less her $45,000 of savings and the $210,000 of death cover in Riverbend Super, the shortfall is $755,000, so I've drafted $750,000 for Amelia's review. I've also drafted a recommendation that Priya keeps her Kestrel Retail Super cover until the new cover is in force. Whether to keep or cancel it is a decision for Priya and Amelia."

---

# 3. Insurance Research Agent

**Based on:** Insurance Research Analyst (Ivy)

**Purpose:** Compare the insurers on Life Advice Guru's approved product list on definitions, features and premiums, so the adviser can recommend with confidence.

## What the agent can do

| Capability | Description |
|---|---|
| Policy document analysis | Reads an insurer's product sheet or PDS extract: definitions, features and exclusions (`research_analysePolicyDocument`). |
| Insurer comparison | Compares the APL insurers for the client's cover: premiums, definitions and features (`research_compareInsurers`). |
| Definitions that matter | Checks the definitions that decide whether a claim would pay, such as own-occupation TPD for the client's occupation. |
| Stepped and level premiums | Compares stepped and level premiums now and over time. |
| Comparison workbook | Builds an Excel workbook with a premium chart and a download link (`research_generateComparisonWorkbook`). |
| APL knowledge | Answers questions about Harbourline Life, Corella Life, Lyrebird Life and Northbank Life from their PDS extracts and *Life Advice Guru — Approved product list and research method*. |

## Example AI agent behaviour

> "I've compared the three APL insurers for Priya's full package. Year-1 stepped premiums are Corella Life $3,180, Harbourline Life $3,412 and Lyrebird Life $3,690. Corella Life is the lowest cost, and its super-linked own-occupation TPD extension covers physiotherapists. Its level-premium option is $4,050 in year 1. Split by where the cover is held, it's $858 a year inside super and $2,322 a year outside super, or $193.50 a month from cash flow, within Priya's $300 budget. The workbook and chart are ready for Amelia. The recommendation is hers."

---

# 4. Financial Crime Agent

**Based on:** Financial Crime Investigator (Felix)

**Purpose:** Protect clients from fraud, scams and identity takeover, verify identity, and recommend controls.

## What the agent can do

| Capability | Description |
|---|---|
| Document authenticity | Uses Azure Content Understanding to check IDs, medical reports, death certificates, payslips, BAS and bank statements. Each gets a verdict of legit, suspicious, tampered or fake, with the reasons. |
| Risk timeline | Builds one timeline of contact and bank-detail changes, devices, locations and claims. |
| Claim red flags | Flags a claim soon after a contact change, a new bank account in another name, urgency, and medical evidence that doesn't hold up. |
| KYC and screening | Verifies identity for every new client, including companies and their beneficial owners, and screens for politically exposed persons (PEPs) and sanctions. |
| Explainable risk rating | Gives a low, medium or high rating, with each indicator matched to *Guide — financial crime red flags and controls for life insurance*. It is never a black-box score. |
| Recommended controls | Recommends a hold, restoring verified details, locking portal access, a call-back on the verified number on file, and alerting the insurer's fraud team. People decide. |

## Example AI agent behaviour

> "Felix, I've rated REQ-20261106-LIF002 as high risk. Grace's mobile and email were changed by phone on 2 Nov at 10:41 after basic checks only, and the portal was accessed from a new device on a Sydney IP 17 minutes later, while her travel notice says she's in Italy. About 93 hours after the change, a trauma claim arrived for the full $200,000, marked urgent, to be paid into a new account in a company name. The oncologist's report is fake, the licence photo has been edited, and the bank account was opened on 3 Nov. The request doesn't appear to have come from Grace. I recommend blocking the claim and the bank change, restoring her verified details, and calling her on 0491 570 157. Alerting Lyrebird Life's fraud team is Theo's decision."

---

# 5. Underwriting Agent

**Based on:** Underwriting Coordinator (Uma)

**Purpose:** Take each application through underwriting with the insurer, explain the client's duty first, and get the policy issued.

## What the agent can do

| Capability | Description |
|---|---|
| Application preparation | Prepares the application from the Client Case and the signed SoA, after the Authority to Proceed. |
| Duty to take reasonable care | Explains the client's duty in plain English before any health question, citing *Underwriting and the duty to take reasonable care*. |
| Genetic information | Never asks for or uses genetic test results. From 8 Oct 2026, insurers can't ask for or use protected genetic information in life insurance underwriting. |
| Insurer lookup | Finds the insurer's contacts, service levels and underwriting thresholds on the APL directory (`underwriting_lookupInsurer`). |
| Requirements check | Lists the medical and financial evidence needed for the client's age and sums insured (`underwriting_checkRequirements`). |
| Tracking and chasing | Tracks medicals, blood tests, GP reports and financial evidence, and drafts chasers to the insurer. |
| Decisions explained | Explains standard rates, loadings and exclusions in plain English. Accepting a loading, an exclusion or a decline is the client's decision with the adviser. |
| Application summary | Generates the application and disclosure summary PDF (`underwriting_generateApplicationSummaryPdf`). |
| Policy issue | Confirms the policy number, the start date and the cooling-off period, and hands over to Client Communications. |

## Example AI agent behaviour

> "Northbank Life's results are in. Harpreet is accepted at standard rates. George's cholesterol is 7.2 mmol/L and his blood pressure is 150/95, so Northbank Life has offered his life and TPD cover with a 25% loading, which it will review in two years. The loading doesn't reduce his cover; it adds 25% to his premiums. George's buy-sell premium goes from $4,140 to $5,175 and the key person premium from $1,380 to $1,725, so the total is $10,000 a year. Accepting the loading is George's decision with Amelia."

---

# 6. Claims Agent

**Based on:** Benefit Claims Advocate (Ben)

**Purpose:** Support clients and families through a claim: check the policy, build the claim pack, calculate the benefit, and lodge the claim only after approval.

## What the agent can do

| Capability | Description |
|---|---|
| Claimant check | Confirms who is claiming and that they can claim: the policy owner, a nominated beneficiary, or an authorised support person (`claims_validateClaimant`). |
| Policy check | Checks that the policy is in force, the definition, the waiting period, exclusions and who is paid (`claims_checkPolicy`). |
| Evidence and claim pack | Checks the evidence against the definition and builds the claim pack. |
| Benefit calculation | Calculates the expected benefit and explains it in plain English (`claims_calculateBenefit`). |
| Approval rules | Checks which approvals the claim needs (`claims_checkApprovalRules`). Lodging any claim needs human approval, and so does every death or terminal illness claim. |
| Teams approval | Posts a Microsoft Teams adaptive card to the approver (`claims_requestApproval`, through `teams_sendApprovalCard`) and tracks it (`claims_getApprovalStatus`). |
| Controlled lodgement | Lodges the claim with the insurer only after approval (`claims_lodgeClaim` refuses otherwise). |
| Code of Practice tracking | Tracks the insurer's timeframes under the Life Insurance Code of Practice and chases on the client's behalf. |
| Other cover | Looks for other cover the client may hold, such as default cover in super. |

## Example AI agent behaviour

> "Ben, Tom's Harbourline Life policy is in force and the 30-day waiting period ends on 13 Nov 2026. His insured benefit is $7,000 a month, but the policy pays the lesser of that and 70% of his average monthly earnings in the 12 months before the injury. His BAS and profit and loss show $96,000, which is $8,000 a month, so the expected benefit is $5,600 a month, paid monthly in arrears from about 13 Dec 2026. I've built the claim pack. Lodging needs approval, and Tom is a vulnerable client, so I've sent Theo a Teams card. I won't lodge until he approves."

---

# 7. Client Communications Agent

**Based on:** Client Communications Specialist (Cara)

**Purpose:** Keep clients, families and referral partners informed with clear, empathetic and compliant communication.

## What the agent can do

| Capability | Description |
|---|---|
| Client letters | Drafts welcome letters, policy summaries, underwriting updates, claim updates, and reassurance and condolence letters. |
| SoA summaries | Explains a Statement of Advice in plain English, without adding advice. |
| Referral-partner updates | Updates brokers and accountants with only what the client agreed to share, and never health details without consent. |
| Multi-channel drafts | Drafts emails, SMS, portal messages and call scripts. |
| Tone matching | Adjusts the tone for new cover, loadings and exclusions, injury, serious illness, scams and bereavement. |
| Compliance check | Checks for no personal advice, the required disclosures, and IDR and AFCA rights. |
| Voice | Cara's voice agent takes calls about a case on Azure Voice Live. |

## Example AI agent behaviour

> "I've drafted Robert's letter. It confirms that Corella Life accepted the claim and paid $1,000,000 to him on 23 Nov 2026, says in one sentence that the benefit isn't taxable, and includes a short checklist for Mei's Riverbend Super claim. It names Amelia as his one contact and includes his complaint rights. Because this is a bereaved family, it's waiting for Theo's approval, and Amelia plans to call Robert before it arrives."

---

# 8. Team Leader Agent

**Based on:** Practice Team Leader (Theo)

**Purpose:** Lead the practice: workload, service levels, complaint clocks, approvals, file quality and vulnerable clients.

## What the agent can do

| Capability | Description |
|---|---|
| Pipeline and SLA dashboard | Tracks open cases by stage, age, priority and owner, across the advice pipeline, underwriting and claims. |
| Clocks | Tracks IDR clocks and the insurers' Code of Practice timeframes on open claims. |
| Approval queue | Collects every item that needs a person: SoAs, replacements, loadings and exclusions, claim lodgements, blocks, insurer fraud referrals, and messages to vulnerable clients. |
| Second reviews | Prepares a second-review brief for any advice with total cover of $2,000,000 or more. |
| File quality | Audits files against the best interests duty and flags missing reasoning or disclosures. |
| Vulnerable-client oversight | Surfaces cases involving serious illness, injury, financial stress, bereavement or scams for human attention. |
| Group chat orchestration | Coordinates the seven specialist agents on a single case in a multi-agent group chat. |

## Example AI agent behaviour

> "Theo, four cases need you today. REQ-20261106-LIF002 (Grace): approve the block, restoring her verified details, and alerting Lyrebird Life's fraud team. REQ-20261109-LIF003 (George and Harpreet): a second review, because total cover is $2,800,000. REQ-20261111-LIF004 (Tom): a vulnerable-client review and the claim lodgement in Teams. REQ-20261113-LIF005 (Robert): every message to the family needs your approval, and the death claim lodgement is waiting in Teams."

---

# Suggested AI Agent Ecosystem

A realistic AI setup for a life insurance advice practice could look like this:

1. The **Onboarding Agent** captures the request, runs the fact find and the in-good-order checklist, and opens the Client Case.
2. The **Financial Crime Agent** verifies every new client and checks documents, contact changes and bank details whenever something doesn't fit.
3. The **Paraplanning Agent** works out the needs and the structure and drafts the Statement of Advice for the adviser.
4. The **Insurance Research Agent** compares the APL insurers on definitions, features and premiums.
5. The **Underwriting Agent** takes the application through underwriting and gets the policy issued.
6. The **Claims Agent** checks the policy and evidence, calculates the benefit, and lodges claims after approval.
7. The **Client Communications Agent** keeps clients, families and referral partners informed. Its voice agent takes calls.
8. The **Team Leader Agent** watches the pipeline and the clocks, prepares approvals and second reviews, and coordinates all seven agents in group chat.

The stages follow the request lifecycle: **Discover → Verify → Advise → Implement → Confirm**.

---

# Summary

Together, these AI agents form a realistic digital advice practice. Each agent has a specific role, clear responsibilities and governed tools, and each supports a named person rather than replacing the whole process with one generic chatbot. The agents do the reading, checking, calculating, comparing and drafting. Amelia gives the advice, and people make the decisions that matter.
