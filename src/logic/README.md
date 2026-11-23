# `src/logic` — Client Request Email Logic App workflows

This folder contains the Azure **Logic Apps Standard** workflow set used by **Advice Team in a Day** for **Caldova Life Advice**. It is the email front door of the practice: when a client, a family member or a referral partner emails the practice mailbox, these workflows pick the email up, decide which sub-flow should handle it, call a Foundry agent to capture and route the request as a **Client Case** using MCP tools, log each step to OneLake (Microsoft Fabric), and escalate to the human **Practice Team Leader** (Theo) via Teams when the agent cannot complete the request.

The workflows here were ported from the reference implementation in
[`qkfang/tfn-processing/apps/logicapp-tfn`](https://github.com/qkfang/tfn-processing/tree/main/apps/logicapp-tfn) and re-pointed at Caldova Life Advice resources, agents and terminology.

## Files

| File | Type | Purpose |
| --- | --- | --- |
| `monitor-email-processing.json` | Stateful workflow | Trigger on a new email arriving in the practice mailbox (Office 365 connector, Inbox folder). An orchestrator Agent action reads the sender, subject and body and dispatches to either `client-request-processing` (new requests from clients, families and referral partners, or new information for an existing case) or `return-email-processing` (acknowledgements and outbound status emails). |
| `client-request-processing.json` | Stateful workflow | Main client-request pipeline. Uploads attachments to blob, builds the Foundry conversation, calls the `client-request-agent`, records each step to a OneLake JSON log via `fabric-file-log`, and engages the Practice Team Leader via Teams (human in the loop) when the agent reports `processed_status=false`. |
| `return-email-processing.json` | Stateful workflow | Mirror pipeline for acknowledgement and status-update emails to clients, families and referral partners, using the `return-email-agent`. Same Fabric log and human-in-the-loop escalation pattern. |
| `fabric-file-log.json` | Stateful workflow | Helper workflow that PUTs a JSON file into the `LH_Practice.Lakehouse/Files/` folder of the `Caldova_Life_Advice` Fabric workspace using the workflow's user-assigned managed identity. Called by both processing workflows at each major step. |
| `Analyze_Document_for_Prebuilt_or_Custom_models_v4_x_API.json` | Stateful workflow | Wrapper around the Document Intelligence v4 connector. Exposed as the `Analyze_Document_for_Prebuilt_or_Custom_models_v4_x_API` tool inside `document-int-mcp`; reads the attachment from the `email-attachment` blob container (or a URL). |
| `client-request-agent.yaml` | Foundry agent prompt | Instructions for the **Onboarding & Requests Officer** (Olivia) agent: read the email and attachments, extract the Client Case fields and the fact-find basics, run the in-good-order checklist, flag financial crime indicators for Financial Crime, and create or update the case through the `caldova_ops_data_mcp` MCP server. |
| `return-email-agent.yaml` | Foundry agent prompt | Mock agent that returns a fixed JSON envelope, used to simulate a successful acknowledgement or status email. |

## How it fits Advice Team in a Day

This workflow set is the **email front door** of Caldova Life Advice — the digital side of Olivia's Onboarding & Requests desk:

1. A **client, a family member or a referral partner** emails `requests@caldova.example` (fictional) — for example Hannah Lewis (mortgage broker, Yarra Home Loans) referring a family who has just bought a home, Marcus Tan (accountant, Bayside Business Accountants) referring business owners with no succession plan, a completed fact find or a signed Authority to Proceed, an income protection claim notification after an injury, or a family member notifying a death.
2. `monitor-email-processing` fires on the new mail and an orchestrator agent decides which sub-flow to run: new requests (or new information for an existing `REQ-YYYYMMDD-XXXXXX` case) go to `client-request-processing`; acknowledgements and outbound status emails go to `return-email-processing`.
3. `client-request-processing` invokes the **Onboarding & Requests Officer** Foundry agent (`client-request-agent`). It reads the attachments through `document-int-mcp`, extracts the Client Case fields and the fact-find basics (goals, dependants, debts, income, occupation, existing cover), runs the in-good-order checklist — Financial Services Guide given, privacy and health-information consent, ID, authority to obtain existing policy details, commission and fee consent in dollars, support-person authority, and the supporting documents each claim needs — and flags financial crime indicators (a recent change of contact or bank details, an urgent claim for the full sum insured, payment to a new account in another name, altered documents) for Financial Crime. It then uses the `caldova_ops_data_mcp` tools to look up the client and the policy and create or update the case, returning a JSON envelope (`case_number`, `client_number`, `request_type`, `priority`, `in_good_order_checklist`, `financial_crime_flags`, `vulnerability_flags`, `route_to`, `processed_status`, `processing_note`, …).
4. Every step is appended to a per-run JSON log (`client_request_<timestamp>.json` or `return_email_<timestamp>.json`) in the `LH_Practice` Fabric Lakehouse for observability and audit (for example for the Team Leader's queue and service-level dashboard).
5. If the agent cannot complete the request (`processed_status=false`), the **Tool_HIL** branch posts an actionable card in Teams to the **Practice Team Leader** for manual review, and the reviewer's name, comments and time are logged back into the same Fabric file.

This shows that client requests don't disappear into a chatbot: every email becomes a tracked, auditable Client Case the team can pick up. The agent captures, checks and routes. Advice stays with Amelia, Caldova's Senior Risk Adviser, who signs every Statement of Advice; claims, underwriting decisions and holds on suspected fraud stay with the specialist teams and their human approvers.

## External resources referenced (to be provisioned)

The workflow definitions reference the following resources by name. Environment-specific values (subscription id, tenant) need to be substituted at deploy time:

- **Foundry project** — `https://aif-caldovacl.services.ai.azure.com/api/projects/aif-caldovacl-project`
- **User-assigned managed identity** — `/subscriptions/<sub>/resourceGroups/rg-caldova-life/providers/Microsoft.ManagedIdentity/userAssignedIdentities/caldova-ops-logic-uid` (granted access to Foundry, OneLake and Blob storage). `return-email-processing` calls Foundry with the Logic App's system-assigned identity, so grant that identity Foundry access too.
- **Fabric Lakehouse** — workspace `Caldova_Life_Advice`, lakehouse `LH_Practice.Lakehouse`
- **Blob container** — `email-attachment` on the Logic App's storage account
- **Mailboxes (fictional)** — `requests@caldova.example` (practice inbox watched through `conn-office365`) and `practice-team-leader@caldova.example` (Teams recipient of the human-in-the-loop card)
- **MCP server (practice data)** — `caldova_ops_data_mcp` at `https://app-caldovacl-backend.azurewebsites.net/api/mcp` (Foundry project connection `caldova-ops-data-mcp`) exposing `lookup_client`, `lookup_policy`, `lookup_case`, `create_case`, `update_case` and `search_cases`. This is separate from the specialist-agent MCP tools the backend serves at `/mcp` (`caldova-ops-mcp`).
- **MCP server (document intelligence)** — `https://logic-dev.azurewebsites.net/api/mcpservers/document-int-mcp/mcp`
- **API connections** used by the workflows (must exist in the Logic App):
  - `conn-office365` — for the email trigger
  - `conn-teams` — for the human-in-the-loop card
  - `conn-azuread` — to resolve the responder identity
  - `conversionservice` / `conn-conversionservice` — html-to-text
  - `formrecognizer` — Document Intelligence (used by the Analyze workflow)
  - `agent` — Foundry agent model connection used by inline `Agent` actions
  - `AzureBlob` service provider — attachment upload / read

## Deploying

These files are workflow definitions only (no `host.json`, `connections.json` or `parameters.json`). To run them, drop each `*.json` into a Logic App Standard project as `<workflow-name>/workflow.json` — the folder names must match the workflow names the files refer to (`client-request-processing`, `return-email-processing`, `fabric-file-log`, and `Analyze_Document_for_Prebuilt_or_Custom_models_v4_x_API` for the `document-int-mcp` tool) — define the connections listed above, and upload the two `*.yaml` agent prompts to the Foundry project as agents named `client-request-agent` and `return-email-agent`. `client-request-agent` is the email-channel counterpart of the backend's `onboarding-agent` (same Olivia role) and is kept as a separate Foundry agent so the two definitions don't overwrite each other.

## Provenance

Ported from `qkfang/tfn-processing/apps/logicapp-tfn` (commit `12d5673`) and adapted through earlier versions of this repository (see git history for the files `client-request-agent.yaml` and `client-request-processing.json` were renamed from). The TFN-update domain logic was replaced with Caldova Life Advice client-request logic (Client Case capture, fact-find extraction, in-good-order checklist, financial crime flags); the Logic App orchestration, human-in-the-loop pattern and Fabric logging structure are preserved.
