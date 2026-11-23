# AGENTS.md

## Project Name

The project is called **Advice Team in a Day**. It is built for **Caldova Life Advice** (short name **Caldova**), a fictional life insurance advice practice. Caldova is headquartered in Melbourne and helps families and business owners protect their income, their family and their business. Use these names consistently across all UI, documentation, and demo narration.

## Demo Purpose

**Advice Team in a Day** shows how an AI-powered advice practice could work at **Caldova Life Advice**, inside Australia's life insurance advice industry. The goal is a realistic, easy-to-understand practice where each staff role is supported by a specialised AI agent.

The demo shows that the practice team is the engine room of good advice, not a separate business. Something starts each piece of work: a mortgage broker refers a family who has just bought a home, an accountant refers business owners with no succession plan, or a client has to make a claim after an injury, an illness or a death in the family. The practice turns that into completed, compliant work. It runs the fact find, works out how much cover is needed, researches the insurers, prepares the Statement of Advice, takes the application through underwriting, supports clients and families through claims, and keeps everyone informed.

Caldova's advisers recommend cover from the fictional insurers on Caldova's **approved product list (APL)**:

- **Harbourline Life**
- **Corella Life**
- **Lyrebird Life**
- **Northbank Life**

The cover Caldova advises on:

- **life cover**, with a terminal illness benefit
- **TPD cover** (total and permanent disability)
- **trauma cover** (critical illness)
- **income protection**
- **business protection**: key person and buy-sell cover

For the industry background, see [docs/background.md](docs/background.md).

---

## Core Concept

The demo shows Caldova Life Advice as **Caldova House**: a heritage building on a Melbourne city corner, built in 1889 as a bank, with a super modern, futuristic office inside. It is a voxel diorama, read from the top down.

- **Level 3, the idea:** the business owner's office under the old copper dome, and her idea for running an advice practice in the agentic era.
- **Level 2, the people:** two small teams, one for each side of the practice (**Protection Advice** and **Claims & Care**), with one team leader between them. Each role is a real advice-practice function.
- **Level 1, the agents:** one group of AI agents that supports both teams, working in the old banking chamber at street level. Each staff member is supported by an AI agent that drafts, checks, calculates, summarises and recommends. The adviser gives the advice, and people approve.
- **The engine room:** Microsoft IQ and the Azure services, in the old bank vault in the basement.

Out front, clients, families and referral partners arrive by tram at the front desk under the heritage verandah, where requests come in. Every request follows one lifecycle:

> **Discover → Verify → Advise → Implement → Confirm**

For the setting, the cast, every page and the visual direction, see [docs/storybook.md](docs/storybook.md).

---

## Target Story

The demo should communicate this story:

> A family, a business owner, or a client who needs to claim comes to Caldova. Inside the practice, specialist agents take the request from the first conversation through the fact find, identity checks, needs analysis, research, the Statement of Advice, underwriting or the claim, and communication. The adviser gives the advice, and people approve every decision that matters.

The experience should make life insurance advice feel tangible and understandable.

---

## Key Request Journey

The demo should broadly follow this flow:

1. A client, a family member or a referral partner makes a request.
2. **Onboarding & Requests** captures it, runs the fact find and the in-good-order checklist, and opens the Client Case.
3. **Paraplanning** works out how much cover is needed and how to hold it, and drafts the Statement of Advice for the adviser, citing each rule.
4. **Insurance Research** compares the insurers on the APL on definitions, features and premiums.
5. **Financial Crime** verifies identity and documents, and screens for fraud, scams and identity takeover.
6. **Underwriting & New Business** takes the application through underwriting with the insurer and gets the policy issued.
7. **Claims** supports clients and families through a claim: it checks the policy, builds the claim pack, calculates the benefit and lodges the claim after approval.
8. **Client Communications** keeps clients, families and referral partners informed in plain English.
9. **Team Leader Office** monitors workload, service levels, complaint (IDR) clocks, approvals and file quality.

The eight departments and their canonical names:

| # | Department (UI) | Staff and role | Agent | Route id | Foundry agent id |
|---|---|---|---|---|---|
| 1 | Onboarding & Requests | Olivia, Onboarding & Requests Officer | Onboarding Agent | `onboarding` | `onboarding-agent` |
| 2 | Paraplanning | Penny, Paraplanner | Paraplanning Agent | `paraplanning` | `paraplanning-agent` |
| 3 | Insurance Research | Ivy, Insurance Research Analyst | Insurance Research Agent | `insurance-research` | `insurance-research-agent` |
| 4 | Financial Crime | Felix, Financial Crime Investigator | Financial Crime Agent | `financial-crime` | `financial-crime-agent` |
| 5 | Underwriting & New Business | Uma, Underwriting Coordinator | Underwriting Agent | `underwriting` | `underwriting-agent` |
| 6 | Claims | Ben, Benefit Claims Advocate | Claims Agent | `claims` | `claims-agent` |
| 7 | Client Communications | Cara, Client Communications Specialist | Client Communications Agent | `client-communications` | `client-communications-agent` |
| 8 | Team Leader Office | Theo, Practice Team Leader | Team Leader Agent | `team-leader` | `team-leader-agent` |

Cara also has a voice agent, `client-communications-voice-agent`, built on Voice Live. Theo runs a group chat that coordinates the other seven agents on one case.

How the departments map to the lifecycle:

| Stage | Departments |
|---|---|
| **Discover** | Onboarding & Requests |
| **Verify** | Onboarding & Requests (in-good-order checklist), Financial Crime |
| **Advise** | Paraplanning, Insurance Research |
| **Implement** | Underwriting & New Business, Claims |
| **Confirm** | Client Communications |
| All stages | Team Leader Office |

The two sides of the practice:

| Side | What it covers | Team | Dedicated agents |
|---|---|---|---|
| **Protection Advice** | New advice: personal, family and business protection | Penny, Ivy and Uma | Paraplanning, Insurance Research and Underwriting agents |
| **Claims & Care** | Existing clients: reviews, changes and claims | Ben, Cara and Felix | Claims Agent |
| **Shared by both** | Every request comes in through Olivia, and Theo leads both teams | Olivia and Theo | Onboarding, Financial Crime, Client Communications and Team Leader agents |

For detailed AI agent capabilities, tools and approval rules, see [docs/foundry_agents.md](docs/foundry_agents.md).

---

## Staff and Client Characters

**Staff.** The demo has eight staff characters. Each name starts with the same letter as a keyword in the role:

- **O**livia: **O**nboarding & Requests Officer
- **P**enny: **P**araplanner
- **I**vy: **I**nsurance Research Analyst
- **F**elix: **F**inancial Crime Investigator
- **U**ma: **U**nderwriting Coordinator
- **B**en: **B**enefit Claims Advocate
- **C**ara: Client **C**ommunications Specialist
- **T**heo: Practice **T**eam Leader

**Owner and adviser.** **Maya Ashworth** is the **M**anaging director and business owner. **Amelia** is Caldova's senior risk **A**dviser. She works at the front desk, gives the advice and signs every Statement of Advice.

**Clients.** There are five client personas, one for each scenario:

| Client | Scenario |
|---|---|
| Priya Natarajan | New family protection advice after buying a home |
| Grace Williams | Identity takeover and a fake trauma claim, blocked |
| George Papadopoulos | Business protection: buy-sell and key person cover |
| Tom Bradley | Income protection claim after an injury |
| Robert Chen | Death claim after the sudden death of his wife |

**Referral partners.** Two fictional referral partners send clients to Caldova:

- Hannah Lewis, mortgage broker, Yarra Home Loans
- Marcus Tan, accountant, Bayside Business Accountants

For full character profiles, personalities, situations and example dialogue, see [docs/characters.md](docs/characters.md). For the scenarios, see the `docs/scenario-*.md` files listed under Reference Documents.

---

## Product Message

The demo should show that AI agents are not one generic chatbot. The system works like a coordinated digital advice practice: each agent has a clear role, supports a named human, and covers a specific part of the request lifecycle.

The intended message is:

> Life insurance advice is complex and heavily regulated, but it can be made clearer, faster and more consistent by assigning specialised AI agents to each stage of the request lifecycle, with the adviser giving the advice and people approving every decision that matters.

---

## Tone

Use language that is:

- Clear
- Practical
- Business-friendly
- Realistic
- Empathetic, especially for illness, injury, scams and bereavement
- Not overly technical
- Suitable for an Australian life insurance, financial advice and licensee audience

---

## GitHub Copilot Guidance

When you generate code, content, UI components, or documentation for this demo, follow these rules.

**Metaphor and roles**

- Preserve the Caldova House metaphor (the idea, the people, the agents and the engine room, read from the top down), and keep the **Discover → Verify → Advise → Implement → Confirm** lifecycle visible.
- Represent each AI agent as a specialised role that supports a named human staff member.
- Show AI supporting, triaging, drafting, summarising, checking, calculating and recommending. Humans approve, override and finalise. Never make the system appear to replace human judgement.

**Advice and approvals**

- Agents **never give personal financial advice**. That is the adviser's job. Agents gather facts, explain rules, calculate needs and benefits, compare products, draft documents and recommend for human approval. Only a human adviser gives advice and signs the Statement of Advice.
- Always require human approval for:
  - every Statement of Advice and Record of Advice, which the adviser signs
  - replacing or cancelling any existing cover
  - accepting a loading, an exclusion or a decline at underwriting
  - lodging any claim, and every death or terminal illness claim
  - blocking a request on financial-crime grounds, and referring suspected fraud to an insurer
  - any change to policy ownership, beneficiaries or bank details
  - advice with total cover of $2,000,000 or more on one case, which gets a second review
  - complaint responses
  - any communication to a fraud victim, a seriously ill client or a bereaved family

**Terminology**

- Use realistic Australian life insurance and advice terminology. Examples: fact find, needs analysis, Statement of Advice (SoA), Record of Advice (RoA), best interests duty, Authority to Proceed, approved product list (APL), sum insured, stepped and level premiums, loading, exclusion, underwriting, duty to take reasonable care, cooling-off period, terminal illness benefit, TPD (own and any occupation), trauma cover, income protection (waiting period, benefit period), insurance inside super, Protecting Your Super, super-linking, key person cover, buy-sell agreement, Life Insurance Framework (LIF) commission caps, commission consent, Life Insurance Code of Practice, KYC, nominated beneficiaries, IDR and AFCA.
- Use this core vocabulary:

| Concept | Use |
|---|---|
| Unit of work | A **request** becomes a **case**, the **Client Case**, numbered `REQ-YYYYMMDD-XXXXXX` |
| People | **client** for the person advised; **life insured**, **policy owner**, **beneficiary** and **claimant** where the difference matters; **adviser** for Caldova's human adviser; **referral partner** for brokers and accountants |
| Cover | **policy**, **cover**, **sum insured**, **premium** and **benefit**. Caldova client numbers are `CL-…`. Policy numbers carry the insurer's prefix: `HL-` Harbourline, `CR-` Corella, `LB-` Lyrebird, `NB-` Northbank |
| Other terms | **needs analysis**, **underwriting**, **claim**, **insurer**, **financial crime** |
| Outcomes | **completed**, **declined**, **blocked** or **partially approved** |

- Don't use general-insurance language: excess, loss adjuster, repairer, supplier or settlement. Life insurance words such as policy, claim, cover, premium and benefit are correct here.
- Never call anything on screen a "demo": not in labels, buttons, status lines, letters or case numbers. Say "scenario" or "use case" instead, or "offline" for the scripted fallback when Foundry isn't configured. The five scenario cases are numbered `REQ-YYYYMMDD-LIF001` to `LIF005`.
- Use the department names exactly as listed above, consistently across UI, code and documentation.
- Use Australian English (organise, licence, colour, judgement) and Australian conventions: AUD amounts, Australian date formats, and the FY2026-27 rules and numbers in [docs/background.md](docs/background.md).

**Accuracy and care**

- Never invent figures or rules. Cite the insurer's PDS, Caldova's guides or the rule document from the `life_knowledge` Azure AI Search index by its title.
- Never accuse a client of fraud.
- Protect privacy: mask TFNs as `*** *** 123`, and treat health information as sensitive, shared only with the people who need it.
- Flag vulnerable clients (serious illness, injury, financial stress, bereavement, scams, age, cognitive decline) for human handling.
- Keep client-facing language empathetic and plain English.

**Presentation**

- Keep visual elements aligned with Caldova House in [docs/storybook.md](docs/storybook.md): sharp and readable, one accent colour per level and one colour per side of the practice.
- Prefer structured layouts with departments, cards, panels, checklists or workflow stages.
- Make the demo feel like an enterprise advice-practice product, not a consumer chatbot.

**Fiction and sources**

- All people, practices, insurers, super funds, businesses and Caldova itself are fictional. Don't use real insurer, licensee, fund, adviser or business names, logos, product names or addresses in the UI or docs. The only exception is the presenter notes in [docs/background.md](docs/background.md).
- Reference the docs files for character details, scenarios, rules and agent capabilities instead of duplicating that content.

---

## Reference Documents

- [docs/background.md](docs/background.md): primer on life insurance advice, covering the cover types, the advice process, the claims process, FY2026-27 rules and numbers, regulators, and presenter notes.
- [docs/characters.md](docs/characters.md): staff and client character profiles, supporting AI agents, and dialogue.
- [docs/process.md](docs/process.md): practice process detail and AI agent capabilities for each role.
- [docs/foundry_agents.md](docs/foundry_agents.md): Foundry agent design, covering the Client Case, inputs, tools, outputs, handoffs and human approval.
- [docs/agent-demo.md](docs/agent-demo.md): the presenter click-path and 10-minute golden path.
- [docs/storybook.md](docs/storybook.md): the Caldova House storybook, the frontend at `/`. Caldova is shown as an advice practice for the agentic era with two sides, Protection Advice and Claims & Care. The house is read from the top down: the business owner's idea, two small teams, one group of agents (dedicated to a side or shared by both, with each agent's specialised assistants as their own roles), and Microsoft IQ and the Azure services in the engine room. Out front, visitors arrive by tram and sign at the front desk under the verandah, where Amelia, Caldova's adviser, links them with the agents that get them started, and on the roof Caldova throws a party for its clients. The doc covers the setting, the business owner and adviser characters, the visual direction and every page.
- Scenarios:
  - [docs/scenario-1-family-protection.md](docs/scenario-1-family-protection.md) (Priya)
  - [docs/scenario-2-fraudulent-claim.md](docs/scenario-2-fraudulent-claim.md) (Grace)
  - [docs/scenario-3-business-protection.md](docs/scenario-3-business-protection.md) (George)
  - [docs/scenario-4-income-protection-claim.md](docs/scenario-4-income-protection-claim.md) (Tom)
  - [docs/scenario-5-death-claim.md](docs/scenario-5-death-claim.md) (Robert)

---

## Project Name

The demo is called **Advice Team in a Day**. The fictional business it represents is **Caldova Life Advice**. Always refer to the demo by this name. Don't introduce alternative or legacy names.

---

## Summary

**Advice Team in a Day** presents **Caldova Life Advice** as Caldova House, a heritage building with a futuristic office inside. Its two small teams work on one floor, and each staff member is supported by a specialised AI agent on the floor below. The purpose is to explain and visualise how AI can assist with:

- onboarding and requests
- paraplanning and needs analysis
- insurance research
- financial crime
- underwriting and new business
- claims
- client communications
- team leadership

The final experience should help viewers quickly understand how a life insurance advice practice works, and how a coordinated set of AI agents can support the full request lifecycle: **Discover → Verify → Advise → Implement → Confirm**.

For all detailed character, process, rule and visual content, see the [docs/](docs/) folder.
