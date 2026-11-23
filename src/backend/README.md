# Advice Team in a Day — Agent Console (`src/backend`)

A .NET 10 Blazor Server web app for **Life Advice Guru**, a fictional
Melbourne life insurance advice practice. It is the agent console behind
*Advice Team in a Day*: every Microsoft Foundry agent in
[`src/agent/Agents`](../agent/Agents) gets its own page that opens straight
into a live **Try it out** flow against a real Client Case (Client
Communications adds a **Voice chat** tab and Team Leader a **Group Chat** tab).
The same app hosts the five scenario pages, the "Ask AI" assistant, the
in-process **MCP server** the Foundry agents call back into, and the Voice Live
proxy for Cara.

Every request follows one lifecycle — **Discover → Verify → Advise → Implement
→ Confirm** — and the agents gather facts, check, calculate, compare and draft.
Amelia, Life Advice Guru's senior risk adviser, gives the advice, and people approve
every decision that matters.

## Run

```bash
dotnet run --project src/backend/guru-backend.csproj
```

Then open <http://localhost:5212> (the `life-advice-guru` launch profile).
Without a reachable Foundry project every Try-it-out flow still completes
**offline**, using deterministic results that match the five scenarios exactly.
Configure Foundry (below) to see the real agents stream their reasoning, cite
the `life_knowledge` index and call MCP tools.

To build or run several copies side by side (for example while other changes
are being built), give each its own output folders and port:

```bash
dotnet build src/backend/guru-backend.csproj -p:IntermediateOutputPath=obj/mine/ -p:OutputPath=bin/mine/
dotnet run --project src/backend/guru-backend.csproj -p:IntermediateOutputPath=obj/mine/ -p:OutputPath=bin/mine/ --urls http://localhost:5299
```

If a previous run is still holding the build outputs, `./kill-sessions.ps1`
stops the `guru-backend` / `guru-agent` processes.

## Routes

| Route | What it is |
| --- | --- |
| `/` | Agent console — the five Client Cases, the lifecycle strip, the two sides of the practice, one card per agent and the human-approval list |
| `/agents/{id}` | Agent page with the live Try it out flow. `?caseNumber=REQ-…` pre-selects a case; on Onboarding, `?sample=onboarding-priya` (etc.) pre-selects an inbound request |
| `/scenarios/{id}` | Scenario page — key facts, who approves what, and the case journey with a **Run this step** link to each agent |
| `/account` | "Ask AI" assistant (general information only) with related-agent references |
| `/mcp` | MCP server, label `guru-ops-mcp` (research, underwriting, claims and Teams tools) |

### Agent pages

| Route id | Agent (staff) | Side | Endpoints | Foundry capability on show |
| --- | --- | --- | --- | --- |
| `onboarding` | Onboarding Agent (Olivia) | Shared | `/onboarding/*` | Reads emails, call notes and attachments → Client Case, fact find, in-good-order checklist, priority and routing |
| `paraplanning` | Paraplanning Agent (Penny) | Protection Advice | `/paraplanning/*` | Needs analysis with the working shown and the SoA draft for Amelia, grounded in cited rules |
| `insurance-research` | Insurance Research Agent (Ivy) | Protection Advice | `/research/*` | MCP tools: policy documents → insurer comparison, premium chart and Excel workbook |
| `financial-crime` | Financial Crime Agent (Felix) | Shared | `/fincrime/*` | Azure Content Understanding document checks + explainable risk timeline |
| `underwriting` | Underwriting Agent (Uma) | Protection Advice | `/underwriting/*` | MCP tools: insurer directory, evidence requirements, application summary PDF |
| `claims` | Claims Agent (Ben) | Claims & Care | `/claims/*` | MCP claim tools + Microsoft Teams approval card; lodgement only after approval |
| `client-communications` | Client Communications Agent (Cara) | Shared | `/communications/*` | Tone-matched letters and updates + Voice Live calls |
| `team-leader` | Team Leader Agent (Theo) | Shared | `/team-leader/*` | Practice dashboard + multi-agent group chat on a case |

Each department follows the same pattern: `GET /{prefix}/cases` lists the
Client Cases, `GET /{prefix}/cases/{caseNumber}` loads one, and
`POST /{prefix}/process` engages the agent (send `Accept: text/event-stream`
for a live Server-Sent Events stream: `delta` events, then one `done` event
with the full result). Department-specific endpoints include the claim
approvals under `/claims/approvals/*`, the document checks under
`/fincrime/*`, the group chat under `/team-leader/groupchat/*`, the Voice Live
session under `/communications/voice-live/*`, and the comparison workbook
download at `/research/download/{fileName}`.

Every engage step shows five sub-tabs — **Agent Narrative**, **Reference**,
**Agent Prompt & Tools** (from `GET /agents/{id}/metadata`; the voice agent is
`client-communications-voice`), **Agent Input** and **Raw Output** — so the
audience can see exactly what the agent saw, which tools it called and what it
produced.

### Onboarding endpoints

| Endpoint | Purpose |
| --- | --- |
| `GET /onboarding/samples` | The five inbound requests (one per scenario) |
| `GET /onboarding/samples/{id}` | One request: the email or call note, the attachment text and what Life Advice Guru holds on file |
| `POST /onboarding/process` | `{ "sampleId": "onboarding-priya" }` → Client Case fields, fact find (or claim details), in-good-order checklist, priority, vulnerability flags, routing and the agent narrative |
| `GET /onboarding/routing?requestType=…` | Where a case of that request type is routed |
| `POST /onboarding/submit` | Olivia confirms the (edited) Client Case → mints `REQ-YYYYMMDD-XXXXXX` and returns the routing |
| `GET /onboarding/cases/{caseNumber}` | Read one Client Case |

Sample ids: `onboarding-priya`, `onboarding-grace`, `onboarding-george`,
`onboarding-tom`, `onboarding-robert`.

### MCP tools (`/mcp`, label `guru-ops-mcp`)

| Agent | Tools |
| --- | --- |
| Insurance Research (Ivy) | `research_analysePolicyDocument`, `research_compareInsurers`, `research_generateComparisonWorkbook` |
| Underwriting (Uma) | `underwriting_lookupInsurer`, `underwriting_checkRequirements`, `underwriting_generateApplicationSummaryPdf` |
| Claims (Ben) | `claims_validateClaimant`, `claims_checkPolicy`, `claims_calculateBenefit`, `claims_checkApprovalRules`, `claims_requestApproval`, `claims_getApprovalStatus`, `claims_lodgeClaim` (refuses unless approved), plus `teams_sendApprovalCard` |

### Seeded Client Cases

`CaseStore.SeedDefaults()` creates one case per scenario at start-up, so every
page has something to work on immediately (cases opened live from the
Onboarding page are added alongside them; live case numbers never contain an
"I", so they can't clash with the `LIF` tag):

| Case | Client | Request | Outcome |
| --- | --- | --- | --- |
| `REQ-20261103-LIF001` | Priya Natarajan | New family protection advice (referred by Hannah Lewis, Yarra Home Loans) | Completed |
| `REQ-20261106-LIF002` | Grace Williams | Identity takeover and a fake trauma claim | Blocked |
| `REQ-20261109-LIF003` | George Papadopoulos & Harpreet Kaur | Business protection: buy-sell and key person cover (referred by Marcus Tan, Bayside Business Accountants) | Completed |
| `REQ-20261111-LIF004` | Tom Bradley | Income protection claim after an injury | Partially approved |
| `REQ-20261113-LIF005` | Robert Chen | Death claim after the sudden death of his wife Mei | Completed |

## Configuration

The app references the [`agent`](../agent) project as a .NET library and uses
the same Foundry configuration keys as the CLI host. Set them in
`appsettings.json`, `appsettings.Development.json`, or as environment variables:

| Key | Purpose |
| --- | --- |
| `AZURE_AI_PROJECT_ENDPOINT` | Microsoft Foundry project endpoint URL (`aif-lifeguru` / `aif-lifeguru-project`) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Chat model deployment (e.g. `gpt-5.4`) |
| `AZURE_TENANT_ID` | Optional tenant ID for `DefaultAzureCredential` |
| `AZURE_AI_SEARCH_CONNECTION_ID` | Foundry connection ID for Azure AI Search |
| `AZURE_AI_SEARCH_INDEX_NAME` | Knowledge index (`life_knowledge` — see [`ai-search/`](../../ai-search)) |
| `AZURE_BING_CONNECTION_ID` | Foundry Bing grounding connection ID |
| `APP_MCP_URL` | Public base URL of this app (e.g. a dev tunnel or `https://app-lifeguru-backend.azurewebsites.net`) so Foundry can reach `/mcp`. Leave it unset for purely local runs — otherwise agents call the MCP tools on that host. |
| `FoundryAgent:EndpointUrl` | Optional. Agent endpoint for the "Ask AI" assistant; without it the assistant answers offline |
| `AZURE_CONTENT_UNDERSTANDING_ENDPOINT` | Optional. Azure Content Understanding endpoint for the Financial Crime document checks (falls back to the manifest's expected verdicts when unset) |
| `AZURE_CONTENT_UNDERSTANDING_KEY` | Optional API key (otherwise `DefaultAzureCredential` is used) |
| `AZURE_CONTENT_UNDERSTANDING_ANALYZER_ID` | Optional analyzer id (created on first use) |
| `AZURE_CONTENT_UNDERSTANDING_COMPLETION_MODEL` / `_EMBEDDING_MODEL` | Optional model deployments for the analyzer |

When the endpoint and model are set, `GuruAgentFactory.IsConfigured` is
`true` and pages invoke the real agents (`factory.Create("onboarding")`,
`factory.Create("claims")`, …). If the call fails or Foundry isn't configured,
the offline results are used and the page says so.

## Financial Crime — document checks

Felix's Try-it-out tab runs a per-document authenticity pass over the
documents attached to each case (driver licences, medical reports, death
certificates, payslips, BAS and bank statements). Sample documents live under
[`wwwroot/fincrime/samples/`](wwwroot/fincrime/samples/) with a
[`manifest.json`](wwwroot/fincrime/samples/manifest.json); they are visibly
marked as specimens and were generated by
[`tools/generate_fincrime_samples.py`](../../tools/generate_fincrime_samples.py).
`FinCrimeDocumentVerifier` sends each document to Azure Content Understanding
and folds the result into a small deterministic checks layer, giving each
document a verdict (`legit` / `suspicious` / `fake`) with the reasons. Without
Content Understanding it falls back to the manifest's expected verdicts, so
the story still plays end to end. Felix's agent never accuses a client — it
explains each red flag, and a person decides.

## Claims — human approval in Microsoft Teams

Ben's agent calls the `claims_*` MCP tools to confirm the claimant, check the
policy and evidence, calculate the expected benefit and check which approvals
the claim needs. `claims_requestApproval` posts an Adaptive Card to Microsoft
Teams through `teams_sendApprovalCard`, and `claims_lodgeClaim` refuses to run
until a person has approved. The page also offers an in-page approve/reject
fallback when Teams isn't connected.
