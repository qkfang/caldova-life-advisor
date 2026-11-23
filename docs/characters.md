# Advice Practice Characters

> Part of **Advice Team in a Day**, the AI advice practice demo for **Caldova Life Advice**.

## Overview

This document describes the people at **Caldova Life Advice**, the life insurance advice practice featured in *Advice Team in a Day*. They are the **staff**, the **business owner**, Caldova's **adviser**, two **referral partners** and five **clients**. You can use these characters for journey mapping, product walkthroughs, AI agent design, training scenarios, service design or role-play.

Each staff member is supported by a specialised AI agent, and each agent has two specialised assistants. The agents take on the repeatable day-to-day work: reading fact finds and claim notifications, checking documents and policy wording, calculating needs and benefits, comparing insurers and drafting. They report their findings, drafts and recommendations back to the person they support, who keeps the judgement and the accountability.

Agents never give personal advice. **Amelia**, Caldova's adviser, gives the advice and signs every Statement of Advice.

To make the cast easy to remember, each name starts with the same letter as a keyword in the role:

| Name | Role keyword |
|---|---|
| **O**livia | **O**nboarding |
| **P**enny | **P**araplanner |
| **I**vy | **I**nsurance research |
| **F**elix | **F**inancial crime |
| **U**ma | **U**nderwriting |
| **B**en | **B**enefit claims |
| **C**ara | **C**ommunications |
| **T**heo | **T**eam leader |
| **M**aya | **M**anaging director |
| **A**melia | **A**dviser |

In the running app, one Foundry agent delivers both of a staff member's assistants. For example, `onboarding-agent` covers the Fact Find Extraction and In-Good-Order Checklist assistants. Cara's voice agent is a separate Foundry agent. See [foundry_agents.md](foundry_agents.md).

### The Two Sides of the Practice

| Side | What it covers | Team | Dedicated agents |
|---|---|---|---|
| **Protection Advice** | New advice: personal, family and business protection | Penny, Ivy and Uma | Paraplanning, Insurance Research and Underwriting agents |
| **Claims & Care** | Existing clients: reviews, changes and claims | Ben, Cara and Felix | Claims Agent |
| **Shared by both** | Every request comes in through Olivia, and Theo leads both teams | Olivia and Theo | Onboarding, Financial Crime, Client Communications and Team Leader agents |

Cara and Felix sit with the Claims & Care team, but their work spans both sides, so their agents are shared.

---

## Human–Agent Relationships

| Human (staff) | Role | AI agent | Assistant | What the assistant does |
|---|---|---|---|---|
| Olivia | Onboarding & Requests Officer | Onboarding Agent | Fact Find Extraction | Reads enquiry emails, referral forms, fact-find questionnaires and claim notifications, and extracts goals, dependants, debts, income, occupation, health basics and existing cover into the Client Case |
| Olivia | Onboarding & Requests Officer | Onboarding Agent | In-Good-Order Checklist | Checks the FSG was given, privacy and health-information consent, ID, the authority to obtain existing policy details, commission and fee consent, and any support-person authority. Sets the priority and flags vulnerability |
| Penny | Paraplanner | Paraplanning Agent | Needs Analysis | Calculates life, TPD, trauma and income protection needs from debts, dependants, income and existing cover, and shows the working |
| Penny | Paraplanner | Paraplanning Agent | SoA Drafting | Drafts the Statement of Advice for Amelia: scope, strategy, ownership inside or outside super, best-interests steps, replacement analysis, and premiums and commission in dollars |
| Ivy | Insurance Research Analyst | Insurance Research Agent | Policy Comparison | Compares the insurers on the approved product list (APL) on definitions, features and exclusions |
| Ivy | Insurance Research Analyst | Insurance Research Agent | Premium Quotes | Compares stepped and level premiums over time, and builds the comparison workbook and chart |
| Felix | Financial Crime Investigator | Financial Crime Agent | Document Authenticity | Scans IDs, medical reports, death certificates, payslips, BAS and bank statements for tampering, with Azure Content Understanding |
| Felix | Financial Crime Investigator | Financial Crime Agent | Risk Timeline | Lines up contact and bank-detail changes, devices and claim patterns, gives an explainable risk rating, and recommends holds and call-backs |
| Uma | Underwriting Coordinator | Underwriting Agent | Application & Disclosure | Prepares the application and walks the client through the duty to take reasonable care |
| Uma | Underwriting Coordinator | Underwriting Agent | Underwriting Requirements | Tracks medicals, blood tests, GP reports and financial evidence, chases the insurer, explains loadings and exclusions, and generates the application summary PDF |
| Ben | Benefit Claims Advocate | Claims Agent | Policy & Evidence Check | Checks the definition, waiting period, exclusions and evidence, and builds the claim pack |
| Ben | Benefit Claims Advocate | Claims Agent | Benefit & Lodgement | Calculates the expected benefit, raises the Microsoft Teams approval, lodges the claim only after approval, and tracks the insurer's Code of Practice timeframes |
| Cara | Client Communications Specialist | Client Communications Agent | Letters & Updates | Drafts plain-English letters, SoA summaries, underwriting and claim updates, SMS, and referral-partner updates that share only what the client agreed to share |
| Cara | Client Communications Specialist | Client Communications Agent | Tone & Compliance | Sets the tone for illness, claims, scams and bereavement, and checks the required disclosures, IDR and AFCA rights, and that nothing is personal advice |
| Cara | Client Communications Specialist | Client Communications Voice Agent | (voice) | Takes calls about a case on Azure Voice Live |
| Theo | Practice Team Leader | Team Leader Agent | Pipeline & SLAs | Watches the advice pipeline, underwriting and claim clocks, and IDR clocks |
| Theo | Practice Team Leader | Team Leader Agent | Approvals & File Quality | Runs the approval queue (SoAs, replacements, claims, blocks and second reviews of $2,000,000 or more) and audits files against the best interests duty. Theo's agent also coordinates the other seven agents in group chat |

---

# Staff Characters

## 1. Olivia — Onboarding & Requests Officer

**Role:** The front door for every enquiry, referral, review request and claim notification. Olivia is shared by both sides of the practice.

**What she does:**

- Receives enquiries from clients, referrals from mortgage brokers and accountants, and claim notifications from clients and families
- Runs the fact find and checks that each request is in good order before it goes any further
- Opens the Client Case and routes it to the right department
- Tells clients and referral partners exactly what is still needed
- Handles claim notifications, especially after a death, with care from the first contact

**Personality:** Calm, meticulous and welcoming.

**Typical line:**

> "Let's get your details right the first time."

**Look:** A sky-blue dress with a gold accent, and her dark hair in a ponytail. She sits at the front door of Level 2, in the middle of the floor.

### Supporting AI Agent: the Onboarding Agent

- **Fact Find Extraction assistant**
  - Reads enquiry emails, referral forms, fact-find questionnaires and claim notifications
  - Extracts goals, dependants, debts, income, occupation, health basics and existing cover into the Client Case, and marks health details as sensitive
  - Spots existing cover that is about to change, such as default cover in an inactive super account
  - Drafts the "what we still need" note for Olivia to approve

- **In-Good-Order Checklist assistant**
  - Checks the Financial Services Guide (FSG) was given, privacy and health-information consent, identification, the authority to obtain existing policy details, commission and fee consent, and any support-person authority
  - Classifies the request: new protection advice, business protection advice, an income protection claim, a death claim, or another claim or change
  - Sets the priority (Standard, High or Urgent) with a reason, and flags vulnerable clients for human handling
  - Opens the case (`REQ-YYYYMMDD-XXXXXX`), routes it, and reports back to Olivia

---

## 2. Penny — Paraplanner

**Role:** Turns the fact find into the numbers and the draft advice. Penny works on the Protection Advice side.

**What she does:**

- Works out how much cover a family or a business needs
- Works out how to hold it: inside or outside super, and who owns each policy
- Drafts the Statement of Advice (SoA) for Amelia to review and sign
- Checks the best-interests steps, the replacement analysis and the commission disclosure
- Flags any case with total cover of $2,000,000 or more for a second review

**Personality:** Precise and thoughtful, and she explains numbers simply.

**Typical line:**

> "Let's work out what your family actually needs, then check it twice."

**Look:** A purple top and skirt, long dark hair, glasses and a gold pen.

### Supporting AI Agent: the Paraplanning Agent

- **Needs Analysis assistant**
  - Calculates life, TPD, trauma and income protection needs from debts, dependants, income, savings and existing cover
  - Works out business needs from a valuation: buy-sell cover for each owner and key person cover for the business
  - Shows every step of the working for Penny to check
  - Drafts the ownership structure: what is held inside super, what is held outside, and what is super-linked

- **SoA Drafting assistant**
  - Drafts the Statement of Advice: scope, strategy, ownership, best-interests steps and the replacement analysis
  - Shows premiums and commission in dollars, as well as percentages
  - Cites the rule documents it relied on
  - Sends the draft to Penny, then to Amelia, who gives the advice and signs it

---

## 3. Ivy — Insurance Research Analyst

**Role:** Researches the insurers on Caldova's approved product list (APL): Harbourline Life, Corella Life, Lyrebird Life and Northbank Life. Ivy works on the Protection Advice side.

**What she does:**

- Compares insurers on the definitions, features and exclusions that matter for each client
- Compares stepped and level premiums, now and over time
- Keeps the APL research current
- Gives Amelia and Penny a clear shortlist and leaves the advice to Amelia

**Personality:** Curious and analytical, and she loves the fine print.

**Typical line:**

> "Cheapest isn't best if the definition won't pay."

**Look:** An amber cardigan, navy trousers, glasses, and her hair in a researcher's bun.

### Supporting AI Agent: the Insurance Research Agent

- **Policy Comparison assistant**
  - Reads each insurer's product sheet and PDS extract
  - Compares definitions (such as own-occupation and any-occupation TPD), features and exclusions for the client's occupation and needs
  - Reports the trade-offs to Ivy, never a personal recommendation

- **Premium Quotes assistant**
  - Compares year-1 premiums across the APL insurers
  - Compares stepped and level premiums over time
  - Builds the comparison workbook and chart for Amelia

---

## 4. Felix — Financial Crime Investigator

**Role:** Protects clients from fraud, scams and identity takeover. Felix sits with the Claims & Care team, and his agent is shared by both sides.

**What he does:**

- Verifies identity for every new client, including companies and their beneficial owners
- Checks documents for tampering: IDs, medical reports, death certificates, payslips, BAS and bank statements
- Reviews contact and bank-detail changes and unusual claim patterns
- Runs politically exposed person (PEP) and sanctions screening
- Recommends holds, call-backs and referrals to the insurer, for a human decision

**Personality:** Sharp and protective, and never accusatory.

**Typical line:**

> "If it's urgent and different, it's worth a second look."

**Look:** A red shirt, dark trousers, short black hair and stubble.

### Supporting AI Agent: the Financial Crime Agent

- **Document Authenticity assistant**
  - Uses Azure Content Understanding to check IDs, medical reports, death certificates, payslips, BAS and bank statements
  - Gives each document a verdict (legit, suspicious, tampered or fake) with the checks behind it
  - Reports results to Felix, citing each document

- **Risk Timeline assistant**
  - Builds one timeline of contact and bank-detail changes, devices, locations and claims
  - Matches activity against known red flags for life insurance
  - Gives an explainable risk rating: low, medium or high, with every indicator spelled out
  - Recommends controls, such as a hold or a call-back on the verified number on file. The decision stays with people

---

## 5. Uma — Underwriting Coordinator

**Role:** Takes each application through underwriting with the insurer, from the first health question to the policy being issued. Uma works on the Protection Advice side.

**What she does:**

- Prepares the application after the client signs the Authority to Proceed
- Explains the duty to take reasonable care before any health question is asked
- Tracks medicals, blood tests, GP reports and financial evidence, and chases the insurer
- Explains loadings and exclusions in plain English, so the client can decide with Amelia
- Confirms when the policy is issued and the cooling-off period starts

**Personality:** Organised and persistent, and plain-spoken about health questions.

**Typical line:**

> "Answer every question carefully — it protects your claim later."

**Look:** An emerald top with a white collar, and long black hair.

### Supporting AI Agent: the Underwriting Agent

- **Application & Disclosure assistant**
  - Prepares the application from the Client Case and the signed SoA
  - Walks the client through the duty to take reasonable care, in plain English
  - Never asks for or uses genetic test results
  - Checks the answers are complete and consistent with the fact find

- **Underwriting Requirements assistant**
  - Looks up the insurer's contacts, service levels and underwriting thresholds
  - Lists the medical and financial evidence needed for the client's age and sums insured, and tracks it
  - Explains each underwriting decision: standard rates, a loading or an exclusion
  - Generates the application and disclosure summary PDF

---

## 6. Ben — Benefit Claims Advocate

**Role:** Supports clients and families through a claim, and advocates for them with the insurer. Ben works on the Claims & Care side.

**What he does:**

- Checks the policy and the evidence before anything goes to the insurer
- Builds the claim pack and calculates the expected benefit
- Gets approval, then lodges the claim
- Tracks the insurer against the Life Insurance Code of Practice timeframes and chases on the client's behalf
- Finds other cover the client may not know about, such as cover in super

**Personality:** Steady and compassionate, and a tenacious advocate.

**Typical line:**

> "My job is to make the claim as easy as it can be for you."

**Look:** An indigo shirt, a lanyard, sandy hair, glasses and a beard.

### Supporting AI Agent: the Claims Agent

- **Policy & Evidence Check assistant**
  - Confirms who the claimant is and that they can claim
  - Checks the definition, the waiting period, exclusions and the evidence against the insurer's PDS extract
  - Builds the claim pack and lists anything still needed

- **Benefit & Lodgement assistant**
  - Calculates the expected benefit and explains it in plain English
  - Checks the approval rules, then raises a Microsoft Teams approval card
  - Lodges the claim only after a person approves
  - Tracks the insurer's Code of Practice timeframes

---

## 7. Cara — Client Communications Specialist

**Role:** Talks to clients, families and referral partners. Cara sits with the Claims & Care team, and her agent is shared by both sides.

**What she does:**

- Sends welcome letters, SoA summaries, underwriting and claim updates, and referral-partner updates
- Explains decisions and next steps in plain English
- Matches her tone to the moment: new cover, a loading, an injury, a scam or a bereavement
- Keeps communications compliant: no personal advice, the required disclosures and complaint rights
- Takes calls about a case

**Personality:** Warm and clear, and careful with words.

**Typical line:**

> "Plain English, every time."

**Look:** A pink blouse with a navy skirt and a gold accent, and an afro.

### Supporting AI Agents: the Client Communications Agent and Cara's Voice Agent

- **Letters & Updates assistant**
  - Drafts letters, SoA summaries, underwriting and claim updates, SMS and call scripts from the Client Case
  - Drafts referral-partner updates that share only what the client agreed to share, and never health details without consent
  - Sends drafts to Cara for review and sign-off

- **Tone & Compliance assistant**
  - Sets the tone for illness, claims, scams and bereavement
  - Checks the required disclosures and IDR and AFCA rights
  - Checks that nothing reads as personal advice

- **Voice agent**
  - Takes calls about a case on Azure Voice Live
  - Explains case status and next steps in plain English
  - Hands over to a person for complaints, vulnerable callers or identity concerns

---

## 8. Theo — Practice Team Leader

**Role:** Leads both teams. Theo sits behind Olivia in the middle of the floor.

**What he does:**

- Manages workload, service levels and complaint (IDR) clocks
- Approves the decisions that need a second person, including second reviews of advice with total cover of $2,000,000 or more
- Looks after vulnerable clients and sensitive cases
- Audits files against the best interests duty and handles escalations and complaints
- Coordinates the team on complex cases

**Personality:** Calm under pressure, and people-first.

**Typical line:**

> "Nobody waits without knowing why."

**Look:** A navy suit, an orange tie and a beard.

### Supporting AI Agent: the Team Leader Agent

- **Pipeline & SLAs assistant**
  - Watches the advice pipeline, underwriting requirements and claim clocks
  - Tracks IDR clocks and the insurers' Code of Practice timeframes
  - Surfaces vulnerable clients and cases at risk
  - Gives Theo a daily briefing

- **Approvals & File Quality assistant**
  - Runs the approval queue: SoAs, replacements, claims, blocks and second reviews
  - Prepares one-page approval briefs
  - Audits files against the best interests duty
  - Runs the group chat that coordinates the other seven agents on one case

---

# Business Owner

## Maya Ashworth — Managing Director (Business Owner)

**Role:** The business owner for both sides of Caldova Life Advice: Protection Advice and Claims & Care. Maya appears on Level 3 of the Caldova House storybook ([storybook.md](storybook.md)), in her office under the copper dome, next to her idea, "the brain". Her name follows the same rule as the staff: **M**aya, **M**anaging director.

**What she does:**

- Sets the goals both sides work to: clearer, faster and more consistent work, with people approving every decision that matters
- Holds the guardrails: when a person must approve, and what agents may never do
- Decides where to invest in the two teams, the agents and the practice
- Answers for the results that clients, families and referral partners feel

**Personality:** Curious, practical and people-first.

**Typical line:**

> "If a family has to chase us at the worst moment of their lives, we've already let them down."

Maya isn't part of the day-to-day practice team and has no AI agent of her own. Her idea is the product message for the agentic era: run each side of the practice with a small expert team, supported by one group of AI agents that are dedicated to a side where the work is specialised and shared where it isn't, with the adviser giving the advice and people approving every decision that matters.

---

# The Adviser

## Amelia — Senior Risk Adviser (Front Desk)

**Role:** Caldova's adviser. In the Caldova House storybook she works at the front desk under the heritage verandah, and she helps every visitor get started on either side of the practice. Her name follows the same rule as the staff: **A**melia, **A**dviser.

**What she does:**

- **Gives the advice**, which agents never do
- Reviews and signs every Statement of Advice and Record of Advice
- Talks clients through underwriting decisions, such as a loading or an exclusion, so they can decide
- Calls clients herself at the hard moments, and is the one named contact for bereaved families
- Welcomes clients, families and referral partners at the front desk, and links each visitor with the agents that get them started

**Personality:** Warm, unhurried and clear.

**Typical line:**

> "Hi Priya! Hannah sent your details ahead — let's look after your family."

**Look:** A burgundy blazer with a gold Caldova badge. She carries a tablet that lights up in the colour of the agent she links a visitor with.

Amelia isn't one of the eight practice staff and has no AI agent of her own. She works with all of them: Penny's Paraplanning Agent drafts her Statements of Advice, and every other agent works on her clients' cases.

---

# Referral Partners

Two fictional referral partners send clients to Caldova. They only hear what the client has agreed they can hear, and never health details without consent.

- **Hannah Lewis**, mortgage broker, **Yarra Home Loans** (hannah.lewis@yarrahomeloans.example.com.au). Hannah refers families who have just bought a home and want to protect the loan. She referred Priya.
- **Marcus Tan**, accountant, **Bayside Business Accountants** (marcus.tan@baysideaccountants.example.com.au). Marcus refers business owners, values their businesses and confirms the tax treatment of business cover. He referred George and Harpreet.

---

# Client Characters

The five clients are fictional, and each one anchors a scenario.

## 9. Priya — New Family Protection Advice

**Situation:** Priya Natarajan is 34 and a physiotherapist at a private clinic in Brunswick, earning $105,000 a year plus super. She lives in Coburg with her partner, Arjun Mehta (36, a software engineer), and their daughter Asha (2), and their second baby is due in March 2027. In October 2026 they bought a home with a joint mortgage of $680,000. Their mortgage broker, Hannah Lewis, referred Priya to Caldova with her consent.

**Needs:**

- Enough cover to keep the home and the family going if she dies, is disabled, falls seriously ill or can't work
- Premiums the family can afford: up to $300 a month from cash flow
- One clear recommendation she can trust

**Concerns:**
She doesn't know how much cover she needs, or what her super already gives her. She is worried the health questions about her asthma and an old back strain will count against her. She doesn't realise the cover in an old super account is about to stop.

**Personality:**
Organised and practical, and short on time.

**Typical line:**

> "We've just taken on a big mortgage. If something happened to me, I want Arjun and the kids to be able to stay in the house."

**Example dialogue:**

> **Amelia:** "Hi Priya! Hannah sent your details ahead — let's look after your family."
>
> **Priya:** "Thanks. I honestly don't know what my super already covers."
>
> **Amelia:** "That's the first thing we'll check. Then we'll work out what you need, and nothing changes until you've seen the numbers."

**Scenario:** [Scenario 1: New family protection advice](scenario-1-family-protection.md)

---

## 10. Grace — Client Targeted by Identity Takeover

**Situation:** Grace Williams is 61, a semi-retired school principal from Hawthorn who still does some relief teaching. She was widowed in 2018, and her sons are Daniel (32) and James (29). She has been a Caldova client since 2019, with a Lyrebird Life policy for $500,000 of life cover and $200,000 of trauma cover, and her sons as her nominated beneficiaries. At her annual review in September she told Amelia she'd be in Italy from 20 Oct to 1 Dec 2026. While she is away, someone impersonates her, changes her contact details and sends a fake trauma claim.

**Needs:**

- Her cover protected, and no claim made in her name
- Contact only through the details she trusts
- Her details restored, and help to protect her identity

**Concerns:**
Someone has stolen her identity, and she can't easily deal with it from overseas. She worries her sons' protection could be affected.

**Personality:**
Independent, careful and warm. She is rattled when she hears.

**Typical line:**

> "I'm in Florence and I'm perfectly well. I never asked anyone for anything!"

**Example dialogue:**

> **Cara:** "Hi Grace, it's Cara from Caldova. I'm calling on the number we've always had for you. Is now a good time?"
>
> **Grace:** "Yes, I'm in Florence. Is everything all right?"
>
> **Cara:** "You're safe, and so is your cover. Someone tried to make a claim in your name. We stopped it, and nothing was paid. I'll walk you through what we've done."

**Scenario:** [Scenario 2: Identity takeover and a fake trauma claim, blocked](scenario-2-fraudulent-claim.md)

---

## 11. George and Harpreet — Business Owners Protecting Their Business

**Situation:** George Papadopoulos is 52, a master joiner and the co-founder of Northcote Custom Joinery Pty Ltd, which he owns 50/50 with his business partner, Harpreet Kaur (47), the operations and finance director. The business was founded in 2011 and has 14 staff, revenue of $3.1m and net profit of $520,000. George lives in Thornbury with his wife, Helen. Their accountant, Marcus Tan, valued the business at $2,400,000 and referred them to Caldova because they have no succession plan.

**Needs:**

- If one of them dies or can't work again, the other can take over the business, and the family is paid fairly for their share
- The business can survive losing George's skills while a replacement is found and trained
- The right ownership and the right tax treatment, confirmed by their accountant

**Concerns:**
George worries about what would happen to Helen and to the business. He would rather be in the workshop than at the doctor, and isn't looking forward to the medical tests.

**Personality:**
George is a proud craftsman and practical. Harpreet is organised and direct, and wants the plan written down.

**Typical line:**

> "Harpreet and I built this together. If something happens to one of us, I don't want our families fighting over it."

**Example dialogue:**

> **Harpreet:** "Marcus says the business is worth $2.4 million. Is that the number we insure?"
>
> **Amelia:** "It's the starting point. Penny will show you the working, and your lawyer drafts the agreement that sits behind the cover."
>
> **George:** "And the medical?"
>
> **Amelia:** "Uma will tell you exactly what's needed, and why, before anything is booked."

**Scenario:** [Scenario 3: Business protection: buy-sell and key person cover](scenario-3-business-protection.md)

---

## 12. Tom — Income Protection Claim After an Injury

**Situation:** Tom Bradley is 41, a self-employed electrician in Frankston who trades as Bradley Electrical. His wife, Kylie (39), is a part-time bookkeeper, and they have two children, aged 8 and 5. He has been a Caldova client since 2023, with a Harbourline Life income protection policy with an insured benefit of $7,000 a month and a 30-day waiting period. On 14 Oct 2026 he fell from a ladder on a job and tore the rotator cuff in his right shoulder. He had surgery on 21 Oct and expects four to six months off work.

**Needs:**

- An income while he recovers
- A simple claim, with someone else doing the chasing
- To know how much he'll get and when

**Concerns:**
Money is tight with no work coming in. He's worried the claim will be a fight, and he hates paperwork.

**Personality:**
Practical and proud, and stressed.

**Typical line:**

> "I've never been off the tools this long. I just need to know the money's coming."

**Example dialogue:**

> **Tom:** "Kylie's on the call too. She does my books."
>
> **Ben:** "That's great. With your OK in writing, Kylie can talk to us for you. We'll build the claim and chase the insurer, so you can focus on your shoulder."

**Scenario:** [Scenario 4: Income protection claim after an injury](scenario-4-income-protection-claim.md)

---

## 13. Robert — Death Claim After the Sudden Death of His Wife

**Situation:** Robert Chen is 44, a secondary school teacher in Box Hill. His wife, Mei, a pharmacist, died suddenly of a brain aneurysm on 12 Oct 2026, aged 42. Their children are Lily (12) and Max (9). Robert and Mei have been Caldova clients since 2019. Mei's Corella Life policy has $1,000,000 of life cover with Robert as the nominated beneficiary, and her super account also had default death cover.

**Needs:**

- One person to deal with, and simple steps
- Certainty about money for the children
- Not to be asked for the same thing twice

**Concerns:**
Paperwork while grieving. He doesn't know what's needed, and he didn't know about the cover in Mei's super.

**Personality:**
Quiet, organised and grieving, and focused on the children.

**Typical line:**

> "Mei always said she'd sorted the insurance. I just want to do this properly, for Lily and Max."

**Example dialogue:**

> **Amelia:** "Hi Robert. Take all the time you need."
>
> **Robert:** "I've sent the certificate. I don't know what else you need."
>
> **Amelia:** "You've sent what we need for now. I'll be your one contact, and I'll call you before anything arrives in the post."

**Scenario:** [Scenario 5: Death claim after the sudden death of a wife and mother](scenario-5-death-claim.md)

---

# Summary

These characters represent a realistic life insurance advice practice, with practice staff, an adviser, referral partners and clients.

- **Staff** show how the practice works behind the advice. Each is paired with a specialised AI agent and its two assistants, which take on repeatable day-to-day work and report back to the human.
- **Amelia** gives the advice and signs every Statement of Advice. **Maya** sets the goals and guardrails for both sides.
- **Clients** show the life events behind every request (a new home, a business partnership, an injury, a scam and a bereavement) and the practical, financial and emotional pressures that come with them.
