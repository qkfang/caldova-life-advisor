# Advice Team in a Day — Foundry Agents (`src/agent`)

This project hosts the **.NET 10** Microsoft Foundry agents behind
**Caldova Life Advice**, a fictional Melbourne life insurance advice practice
(*Advice Team in a Day*). Each staff character described in
[`docs/characters.md`](../../docs/characters.md) is supported by a specialised
Foundry declarative agent, following the role and tooling design in
[`docs/foundry_agents.md`](../../docs/foundry_agents.md).

The C# pattern is modelled on
[`qkfang/quant-agent` → `src/quantlib`](https://github.com/qkfang/quant-agent/tree/main/src/quantlib):

- `Agents/BaseAgent.cs` — wraps a Foundry declarative agent and the project
  Responses client, with MCP auto-approval, streaming, tracing and citation
  extraction.
- `Agents/CaldovaAgent.cs` — shared base for every staff agent; wires up
  Azure AI Search grounding over the `life_knowledge` index (insurer PDS
  extracts, Caldova's guides, advice, underwriting, claims, tax and commission
  rules, and client case files), Bing grounding, and an optional MCP tool
  surface (server label `caldova-ops-mcp`).
- One class per staff character, each with its own Foundry agent id, persona,
  instructions and structured output format.

## Agents

| Staff character | Role | Department | Class | Foundry agent id | CLI flag | Extra tools |
| --- | --- | --- | --- | --- | --- | --- |
| Olivia | Onboarding & Requests Officer | Onboarding & Requests | `OnboardingAgent` | `onboarding-agent` | `--onboarding` | — |
| Penny | Paraplanner | Paraplanning | `ParaplanningAgent` | `paraplanning-agent` | `--paraplanning` | — |
| Ivy | Insurance Research Analyst | Insurance Research | `InsuranceResearchAgent` | `insurance-research-agent` | `--research` | MCP: `research_analysePolicyDocument`, `research_compareInsurers`, `research_generateComparisonWorkbook` |
| Felix | Financial Crime Investigator | Financial Crime | `FinancialCrimeAgent` | `financial-crime-agent` | `--fincrime` | — (the backend adds Content Understanding document checks) |
| Uma | Underwriting Coordinator | Underwriting & New Business | `UnderwritingAgent` | `underwriting-agent` | `--underwriting` | MCP: `underwriting_lookupInsurer`, `underwriting_checkRequirements`, `underwriting_generateApplicationSummaryPdf` |
| Ben | Benefit Claims Advocate | Claims | `ClaimsAgent` | `claims-agent` | `--claims` | MCP: `claims_*` + `teams_sendApprovalCard` |
| Cara | Client Communications Specialist | Client Communications | `ClientCommunicationsAgent` | `client-communications-agent` | `--communications` | — |
| Cara (voice) | Client Communications Specialist (Voice) | Client Communications | `ClientCommunicationsVoiceAgent` | `client-communications-voice-agent` | — (used by Voice Live) | — |
| Theo | Practice Team Leader | Team Leader Office | `TeamLeaderAgent` | `team-leader-agent` | `--team-leader` | — (coordinates the multi-agent group chat) |

Every agent supports — never replaces — human judgement. Agents gather facts,
explain rules, calculate needs and benefits, compare products, draft documents
and recommend. They never give personal advice: Amelia, Caldova's senior risk
adviser, gives the advice and signs every Statement of Advice. People approve
replacing or cancelling cover, loadings, exclusions and declines, every claim
lodgement, financial-crime blocks, changes to ownership, beneficiaries or bank
details, $2,000,000+ second reviews, complaint responses, and any message to a
fraud victim, a seriously ill client or a bereaved family.

## Configuration

Set the following keys in `appsettings.json` (or as environment variables):

| Key | Purpose |
| --- | --- |
| `AZURE_AI_PROJECT_ENDPOINT` | Microsoft Foundry project endpoint URL (e.g. `https://aif-caldovacl.services.ai.azure.com/api/projects/aif-caldovacl-project`) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Chat model deployment (e.g. `gpt-5.4`) |
| `AZURE_TENANT_ID` | Optional tenant ID for `DefaultAzureCredential` |
| `AZURE_AI_SEARCH_CONNECTION_ID` | Foundry connection ID for the Azure AI Search service |
| `AZURE_AI_SEARCH_INDEX_NAME` | Knowledge index name (`life_knowledge`) |
| `AZURE_BING_CONNECTION_ID` | Foundry Bing grounding connection ID |

Authentication uses `DefaultAzureCredential`, so any locally configured
identity (Azure CLI, Managed Identity, etc.) will work. The CLI creates the
agents without the MCP tool surface; the web app in [`src/backend`](../backend)
adds it when `APP_MCP_URL` is set.

## Running an agent

```bash
cd src/agent
dotnet run -- --onboarding "Referral from Hannah Lewis (Yarra Home Loans): Priya Natarajan, 34, physiotherapist, new \$680,000 mortgage, old Kestrel Retail Super account with no contributions since 30 Jul 2025"
dotnet run -- --paraplanning "Needs analysis for Priya: \$680,000 mortgage, two children, \$105,000 salary, \$45,000 savings, \$210,000 death and TPD cover in super"
dotnet run -- --research "Compare Corella, Harbourline and Lyrebird for a 34-year-old non-smoking physio: life \$750,000, TPD \$600,000, trauma \$150,000, IP \$6,125 a month"
dotnet run -- --fincrime "Urgent \$200,000 trauma claim to a new account in another name, 93 hours after a phone change of mobile and email"
dotnet run -- --underwriting "What evidence will Northbank Life need for George, 52: buy-sell life and TPD \$1.2m plus key person \$400,000?"
dotnet run -- --claims "Tom Bradley's income protection claim: insured \$7,000 a month, net business income \$96,000 in the 12 months before his injury"
dotnet run -- --communications "Draft a gentle letter to Robert Chen confirming the \$1,000,000 death benefit for his wife Mei"
dotnet run -- --team-leader "Summarise today's approvals, claim clocks and IDR clocks"
```

Run with no arguments to print the full usage list.

## Used as a .NET library

The project is a `Microsoft.NET.Sdk` Exe assembly (`cl-agent.csproj`, the same
shape as [`quantlib`](https://github.com/qkfang/quant-agent/tree/main/src/quantlib)),
so it can run as a command-line host **and** be referenced as a library.

The Blazor agent console in [`src/backend`](../backend) takes a
`ProjectReference` on `cl-agent.csproj` and resolves a `CaldovaAgentFactory`
from DI to invoke the same `OnboardingAgent`, `ParaplanningAgent`, … classes
the CLI exposes. Factory role keys match the CLI flags (`onboarding`,
`paraplanning`, `research`, `fincrime`, `underwriting`, `claims`,
`communications`, `communications-voice`, `team-leader`), and the agent page
route ids are accepted too.

## Adding a new agent

1. Create `Agents/<Name>Agent.cs` that extends `CaldovaAgent`.
2. Define a unique Foundry agent id (kebab-case, ending in `-agent`).
3. Provide a public `Instructions` constant that follows the structure in
   `docs/foundry_agents.md`: role and persona, job steps, the rules it applies
   (citing `life_knowledge` documents by title), tools, constraints and human
   approvals, and an output format.
4. Add a CLI flag in `Program.cs` and a role key in
   `src/backend/Services/CaldovaAgentFactory.cs`.
