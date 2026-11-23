# Azure AI Search — Advice Team in a Day

This folder provisions the Azure AI Search indexes behind **Advice Team in a
Day** for **Life Advice Guru** and seeds them with fictional knowledge, so
every staff-character agent — Onboarding, Paraplanning, Insurance Research,
Financial Crime, Underwriting, Claims, Client Communications and Team Leader —
can ground its answers in the insurers' PDS extracts, Life Advice Guru's guides, the
rules and the client case files, and **cite each document by its title**.

The pattern mirrors the
[quant-agent ai-search](https://github.com/qkfang/quant-agent/tree/main/ai-search)
reference: each `GuruAgent` is wired up with the Foundry **Azure AI
Search** tool pointing at the project-level connection created in
`bicep/foundry.bicep`, querying the `life_knowledge` index.

## Indexes

| Index | Contents | Used by |
| --- | --- | --- |
| `life_knowledge` | Client case files and practice guides from `life_knowledge/`, **plus a copy of every product & rule document** from `life_rules/`, projected onto the knowledge schema. | Every Foundry agent (the index named in `AZURE_AI_SEARCH_INDEX_NAME`). |
| `life_rules` | The product & rule documents from `life_rules/` in their structured form (summary / rules / limits / evidence required / sources). | Structured lookups. The agents don't query it directly. |

The agents' search tool is bound to a single index, so both scripts upload
each rules document twice: unchanged into `life_rules`, and into
`life_knowledge` with the same `id` and `title`, `category` = `Product &
Rules`, `requestType` = `All`, `severity` = `reference`, `department` = the
document owner, and a `content` field built from summary + rules + limits +
evidence required + sources + product/version/owner.

Both indexes have a semantic configuration and a vector field (with an Azure
OpenAI vectorizer), so they are ready for hybrid retrieval. The agents use the
simple keyword query path by default (`AzureAISearchQueryType.Simple`).

## Documents

All people, insurers, super funds, businesses, policy numbers, premiums and
product features are fictional, and so is Life Advice Guru. The rule
documents summarise the FY2026-27 rules (from 1 July 2026) and list their
legal references and official source URLs in `sources`. They are general
information for the agents to cite, not legal or tax advice.

### `life_rules/` — product & rule documents (12)

| File | Title | Owner |
| --- | --- | --- |
| `pds-harbourline-life-protection.json` | Harbourline Life — Protection PDS extract | Insurance Research |
| `pds-corella-life-protection.json` | Corella Life — Protection PDS extract | Insurance Research |
| `pds-lyrebird-life-protection.json` | Lyrebird Life — Protection PDS extract | Insurance Research |
| `pds-northbank-life-business-protection.json` | Northbank Life — Business Protection PDS extract | Insurance Research |
| `fsg-life-advice-guru-and-remuneration.json` | Life Advice Guru — Financial Services Guide and remuneration | Team Leader Office |
| `apl-life-advice-guru-and-research-method.json` | Life Advice Guru — Approved product list and research method | Insurance Research |
| `rules-advice-best-interests-soa-and-replacement.json` | Advice rules — best interests duty, SoA and replacement advice | Paraplanning |
| `rules-insurance-inside-super.json` | Insurance inside super — permitted cover, Protecting Your Super and opt-in rules | Paraplanning |
| `rules-underwriting-and-duty-of-reasonable-care.json` | Underwriting and the duty to take reasonable care | Underwriting & New Business |
| `rules-claims-code-of-practice-and-income-protection.json` | Claims rules — Life Insurance Code of Practice and income protection benefits | Claims |
| `rules-tax-on-life-insurance.json` | Tax on life insurance — premiums and benefits | Paraplanning |
| `rules-commission-lif-caps-clawback-and-consent.json` | Commission rules — LIF caps, clawback and consent | Team Leader Office |

### `life_knowledge/` — practice guides (4)

| File | Title | Owner |
| --- | --- | --- |
| `guide-in-good-order-checklist.json` | Guide — in-good-order checklist for advice and claims | Onboarding & Requests |
| `guide-financial-crime-red-flags-and-controls.json` | Guide — financial crime red flags and controls for life insurance | Financial Crime |
| `matrix-authority-approvals-and-escalation.json` | Matrix — authority, approvals and escalation | Team Leader Office |
| `directory-apl-insurers.json` | Directory — insurers on the approved product list | Underwriting & New Business |

### `life_knowledge/` — client case files (21)

Case files are named `case-<case number>-<document>.json` and titled
`Case <case number> — <document>`, for example
`case-req-20261111-lif004-benefit-calculation.json` is
*Case REQ-20261111-LIF004 — benefit calculation*.

| Scenario | Case and client | File | Title |
| --- | --- | --- | --- |
| 1 · New family protection advice — Priya Natarajan · ✅ Completed | `REQ-20261103-LIF001` · `CL-104582` | `case-req-20261103-lif001-referral-and-fact-find.json` | Case REQ-20261103-LIF001 — referral and fact find |
| | | `case-req-20261103-lif001-needs-analysis.json` | Case REQ-20261103-LIF001 — needs analysis |
| | | `case-req-20261103-lif001-insurer-quotes.json` | Case REQ-20261103-LIF001 — insurer quotes |
| | | `case-req-20261103-lif001-statement-of-advice-summary.json` | Case REQ-20261103-LIF001 — Statement of Advice summary |
| | | `case-req-20261103-lif001-underwriting-decision.json` | Case REQ-20261103-LIF001 — underwriting decision |
| 2 · Identity takeover and a fake trauma claim — Grace Williams · 🛡️ Blocked | `REQ-20261106-LIF002` · `CL-100377` | `case-req-20261106-lif002-claim-email-and-attachments.json` | Case REQ-20261106-LIF002 — claim email and attachments |
| | | `case-req-20261106-lif002-client-activity-log.json` | Case REQ-20261106-LIF002 — client activity log |
| | | `case-req-20261106-lif002-document-checks.json` | Case REQ-20261106-LIF002 — document checks |
| | | `case-req-20261106-lif002-travel-notice.json` | Case REQ-20261106-LIF002 — travel notice |
| 3 · Business protection: buy-sell and key person — George Papadopoulos (with Harpreet Kaur, `CL-104612`) · ✅ Completed | `REQ-20261109-LIF003` · `CL-104611` | `case-req-20261109-lif003-referral-and-business-details.json` | Case REQ-20261109-LIF003 — referral and business details |
| | | `case-req-20261109-lif003-valuation-summary.json` | Case REQ-20261109-LIF003 — valuation summary |
| | | `case-req-20261109-lif003-insurer-quotes.json` | Case REQ-20261109-LIF003 — insurer quotes |
| | | `case-req-20261109-lif003-underwriting-decision.json` | Case REQ-20261109-LIF003 — underwriting decision |
| 4 · Income protection claim — Tom Bradley · 🟡 Partially approved | `REQ-20261111-LIF004` · `CL-102265` | `case-req-20261111-lif004-claim-notification.json` | Case REQ-20261111-LIF004 — claim notification |
| | | `case-req-20261111-lif004-policy-schedule.json` | Case REQ-20261111-LIF004 — policy schedule |
| | | `case-req-20261111-lif004-earnings-evidence.json` | Case REQ-20261111-LIF004 — earnings evidence |
| | | `case-req-20261111-lif004-benefit-calculation.json` | Case REQ-20261111-LIF004 — benefit calculation |
| 5 · Death claim — Robert Chen (for his wife Mei Chen, `CL-101934`) · ✅ Completed | `REQ-20261113-LIF005` · `CL-101935` | `case-req-20261113-lif005-death-notification.json` | Case REQ-20261113-LIF005 — death notification |
| | | `case-req-20261113-lif005-death-certificate.json` | Case REQ-20261113-LIF005 — death certificate |
| | | `case-req-20261113-lif005-policy-schedule-and-nomination.json` | Case REQ-20261113-LIF005 — policy schedule and nomination |
| | | `case-req-20261113-lif005-super-fund-cover-note.json` | Case REQ-20261113-LIF005 — super fund cover note |

## Document schemas

Each file holds one JSON object, and its `id` must equal the file name without
`.json`. `contentVector` and `summaryVector` are index-only vector fields.

`life_knowledge`:

| Field | Notes |
| --- | --- |
| `id` | Document key. |
| `title` | Human-readable title — what the agents cite. |
| `category` | `Case File`, `Case Evidence`, `Practice Guide` or `Directory` (`Product & Rules` for projected rules documents). |
| `requestType` | `New protection advice`, `Trauma claim and bank change`, `Business protection advice`, `Income protection claim`, `Death claim`, or `All`. |
| `caseNumber` | e.g. `REQ-20261111-LIF004`; empty for guides. |
| `clientNumber` | Life Advice Guru client number, e.g. `CL-102265`; empty for guides. |
| `content` | Body text. |
| `severity` | `low`, `medium` or `high` (`reference` for projected rules documents). |
| `department` | Owning department, e.g. `Financial Crime`. |
| `tags` | Keywords. |
| `lastUpdated` | ISO 8601 date-time in UTC. |

`life_rules`:

| Field | Notes |
| --- | --- |
| `id` | Document key. |
| `title` | Document title — what the agents cite. |
| `product` | The insurer's product, the Life Advice Guru document, or the area of law. |
| `version` | Version and effective date. |
| `summary` | What the document covers. |
| `rules` | The definitions, features, rules or worked examples. |
| `limits` | Limits and exceptions — what the product or rule does not allow. |
| `evidenceRequired` | Evidence the rule needs (list). |
| `sources` | Legal references and official source URLs (list; empty for the fictional insurer PDS extracts). |
| `owner` | Owning department. |
| `tags`, `lastUpdated` | As above. |

## Files

| File | What it does |
| --- | --- |
| `setup-index.ps1` | Creates / updates both indexes and uploads every document (including the rules copy in `life_knowledge`). Run once after `bicep/main.bicep` has provisioned the search service. |
| `ingest-documents.ps1` | Re-uploads the documents without recreating the index schemas. Validates each file first (JSON parse, required fields, `id` equals the file name) and prints a per-index summary. Use after editing, adding, renaming or removing a JSON file: it also deletes any indexed document whose JSON file no longer exists, since an upload can only add or replace documents. |
| `life_knowledge/*.json` | Client case files and practice guides. |
| `life_rules/*.json` | Product & rule documents. |

## Prerequisites

* `az login` with an account that has the **Search Index Data Contributor**
  role on the search service, to upload documents (the `principals` array
  passed to `bicep/main.bicep` grants this). `setup-index.ps1` also creates
  index definitions, which needs **Search Service Contributor**.
* PowerShell 7+ (`pwsh`).
* Check the variables at the top of each script: `$SearchServiceName` must
  match the `aiSearchName` output of `main.bicep` (`search-<baseName>`, e.g.
  `search-lifeguru`), and `$FoundryEndpoint` in `setup-index.ps1` must match the
  `foundryAccountEndpoint` output (e.g. `https://aif-lifeguru.cognitiveservices.azure.com`).
* Index fields can't be removed or renamed in place. If an index with these
  names already exists with a different schema, delete it before running
  `setup-index.ps1`. Indexes left over from earlier versions of this
  repository (with a different prefix) are no longer used and can be deleted
  from the search service.

## Running

```pwsh
cd ai-search
./setup-index.ps1       # first run: create life_knowledge + life_rules and upload docs
./ingest-documents.ps1  # later runs: re-upload docs only (and remove deleted ones)
```

Once the indexes are populated, the backend Web App reads
`AZURE_AI_SEARCH_CONNECTION_ID` and `AZURE_AI_SEARCH_INDEX_NAME` (set to
`life_knowledge`) from app settings (set by `bicep/appservice.bicep`) and
wires the connection into every Foundry agent created by
`GuruAgentFactory`.
