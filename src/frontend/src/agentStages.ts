/**
 * Each staff member's step in a request: its title, the staff role that
 * owns it, the two specialised assistants of that person's agent (matching
 * docs/foundry_agents.md) and the matching `src/backend` agent page. Guru
 * House uses it for each agent's assistants on Level 1, and `APP_BASE_URL`
 * for links to the agent pages.
 */
import type { StaffRole } from "./personaData";

export type StageKey =
  | "onboarding"
  | "paraplanning"
  | "research"
  | "fincrime"
  | "underwriting"
  | "claims"
  | "comms"
  | "lead";

/**
 * Base URL for the companion `src/backend` Blazor site that hosts the
 * per-agent pages (`/agents/{id}`).
 *
 * Read from the `VITE_APP_BASE_URL` Vite app setting at build time. The
 * default value lives in `src/frontend/.env`; override it with an
 * `.env.local` for local development, or via the
 * `.github/workflows/21_deploy_frontend.yml` workflow for deployments.
 */
export const APP_BASE_URL: string = (
  (import.meta as unknown as { env?: Record<string, string> }).env?.VITE_APP_BASE_URL ?? ""
).replace(/\/+$/, "");

/** Stage configuration: title, staff role, AI assistants, and the matching
 * `src/backend` agent route id used to link the agent's page. */
export const STAGE_CONFIG: Record<
  StageKey,
  {
    title: string;
    /** Staff role that owns this step. */
    role: StaffRole;
    /** Matches an Id in `src/backend/Models/AgentCatalog.cs` (route `/agents/{id}`). */
    appAgentId: string;
    agents: { name: string; description: string }[];
  }
> = {
  onboarding: {
    title: "Onboarding & Requests",
    role: "Onboarding & Requests Officer",
    appAgentId: "onboarding",
    agents: [
      {
        name: "Agent Olivia #1 — Fact Find Extraction",
        description:
          "Reads enquiry emails, referral forms, fact-find questionnaires and claim notifications, and extracts goals, dependants, debts, income, occupation, health basics and existing cover into the Client Case.",
      },
      {
        name: "Agent Olivia #2 — In-Good-Order Checklist",
        description:
          "Checks the FSG was given, privacy and health-information consent, ID, the authority to obtain existing policy details, commission and fee consent, and any support-person authority, then sets the priority and flags vulnerability.",
      },
    ],
  },
  paraplanning: {
    title: "Paraplanning",
    role: "Paraplanner",
    appAgentId: "paraplanning",
    agents: [
      {
        name: "Agent Penny #1 — Needs Analysis",
        description:
          "Calculates life, TPD, trauma and income protection needs from debts, dependants, income and existing cover, and shows the working.",
      },
      {
        name: "Agent Penny #2 — SoA Drafting",
        description:
          "Drafts the Statement of Advice for Amelia: scope, strategy, ownership inside or outside super, best-interests steps, replacement analysis, and premiums and commission in dollars.",
      },
    ],
  },
  research: {
    title: "Insurance Research",
    role: "Insurance Research Analyst",
    appAgentId: "insurance-research",
    agents: [
      {
        name: "Agent Ivy #1 — Policy Comparison",
        description: "Compares the insurers on the approved product list on definitions, features and exclusions.",
      },
      {
        name: "Agent Ivy #2 — Premium Quotes",
        description:
          "Compares stepped and level premiums over time, and builds the comparison workbook and chart.",
      },
    ],
  },
  fincrime: {
    title: "Financial Crime",
    role: "Financial Crime Investigator",
    appAgentId: "financial-crime",
    agents: [
      {
        name: "Agent Felix #1 — Document Authenticity",
        description:
          "Scans IDs, medical reports, death certificates, payslips, BAS and bank statements for signs of tampering, using Azure Content Understanding.",
      },
      {
        name: "Agent Felix #2 — Risk Timeline",
        description:
          "Builds the timeline of contact and bank-detail changes, devices and claim patterns, explains the risk score, and recommends holds and call-backs.",
      },
    ],
  },
  underwriting: {
    title: "Underwriting & New Business",
    role: "Underwriting Coordinator",
    appAgentId: "underwriting",
    agents: [
      {
        name: "Agent Uma #1 — Application & Disclosure",
        description:
          "Prepares the application and walks the client through the duty to take reasonable care.",
      },
      {
        name: "Agent Uma #2 — Underwriting Requirements",
        description:
          "Tracks medicals, blood tests, GP reports and financial evidence, chases the insurer, explains loadings and exclusions, and generates the application summary PDF.",
      },
    ],
  },
  claims: {
    title: "Claims",
    role: "Benefit Claims Advocate",
    appAgentId: "claims",
    agents: [
      {
        name: "Agent Ben #1 — Policy & Evidence Check",
        description:
          "Checks the definition, the waiting period, the exclusions and the evidence, and builds the claim pack.",
      },
      {
        name: "Agent Ben #2 — Benefit & Lodgement",
        description:
          "Calculates the expected benefit, raises the Microsoft Teams approval and lodges the claim only after approval, then tracks the insurer’s Code of Practice timeframes.",
      },
    ],
  },
  comms: {
    title: "Client Communications",
    role: "Client Communications Specialist",
    appAgentId: "client-communications",
    agents: [
      {
        name: "Agent Cara #1 — Letters & Updates",
        description:
          "Drafts plain-English letters, SoA summaries, underwriting and claim updates, SMS and referral-partner updates, sharing only what the client agreed to.",
      },
      {
        name: "Agent Cara #2 — Tone & Compliance",
        description:
          "Checks the tone for illness, claims, scams and bereavement, and confirms required disclosures and IDR and AFCA rights. Never personal advice.",
      },
    ],
  },
  lead: {
    title: "Team Leader Office",
    role: "Practice Team Leader",
    appAgentId: "team-leader",
    agents: [
      {
        name: "Agent Theo #1 — Pipeline & SLAs",
        description: "Watches the advice pipeline, the underwriting and claim clocks, and the IDR clocks.",
      },
      {
        name: "Agent Theo #2 — Approvals & File Quality",
        description:
          "Prepares the approval queue (SoAs, replacements, claims, blocks and second reviews of $2,000,000 or more) and audits files against the best interests duty.",
      },
    ],
  },
};
