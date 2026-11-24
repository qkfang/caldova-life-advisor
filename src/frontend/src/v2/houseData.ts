/**
 * Content for "Guru House", the storybook at `/` (see docs/storybook.md).
 *
 * Life Advice Guru is a life insurance advice practice built for the
 * agentic era. It has two sides, Protection Advice and Claims & Care, and
 * works from Guru House, a heritage bank building from 1889 on a Melbourne
 * city corner with a futuristic office inside. The house is read from the top
 * down:
 *   Level 3 · The Idea         Maya Ashworth, the business owner for both sides, and her idea,
 *                              under the copper dome
 *   Level 2 · The People       two small teams, one per side, with Olivia at the front door and
 *                              Theo leading both
 *   Level 1 · The Agents       one group of agents in the old banking chamber, each with its
 *                              specialised assistants (the roles): dedicated to a side, or shared
 *   B · The Engine Room        Microsoft IQ and the Azure services, in the old bank vault
 *   The front desk             under the verandah, where visitors arrive by tram to sign in
 *   The roof                   a party for the clients, under a big LIFE ADVICE GURU sign
 *
 * Staff, assistants and scenarios come from the shared canon (personaData.ts,
 * agentStages.ts and scenarios/*.json). This file adds what the storybook needs on top.
 */
import { PALETTES, type VoxelCharacterPalette } from "../characterPalettes";
import { CLIENT_PERSONAS, STAFF_PERSONAS, type ScenarioId } from "../personaData";
import { STAGE_CONFIG } from "../agentStages";

export type LevelId = "idea" | "people" | "agents" | "engine";

export interface LevelInfo {
  id: LevelId;
  /** Lift button label. */
  floor: string;
  name: string;
  caption: string;
  accent: string;
}

/** Top to bottom. */
export const LEVELS: readonly LevelInfo[] = [
  { id: "idea", floor: "3", name: "The Idea", caption: "The owner’s office under the dome", accent: "#ffc35a" },
  { id: "people", floor: "2", name: "The People", caption: "Two small teams", accent: "#ff8f6b" },
  { id: "agents", floor: "1", name: "The Agents", caption: "In the old banking chamber", accent: "#5ce1ff" },
  { id: "engine", floor: "B", name: "The Engine Room", caption: "Microsoft IQ and Azure in the vault", accent: "#a78bfa" },
];

export function levelInfo(id: LevelId): LevelInfo {
  const l = LEVELS.find((x) => x.id === id);
  if (!l) throw new Error(`Unknown level: ${id}`);
  return l;
}

/* ------------------------------------------------------------------------ */
/* The two sides of the practice                                             */
/* ------------------------------------------------------------------------ */

export type SideId = "claims" | "protection" | "shared";

export interface SideInfo {
  id: SideId;
  name: string;
  team: string;
  color: string;
  /** What the side looks after, for its sign on Level 2. */
  covers: string;
  blurb: string;
}

export const SIDES: Record<SideId, SideInfo> = {
  claims: {
    id: "claims",
    name: "Claims & Care",
    team: "Claims & Care Team",
    color: "#c9a14a",
    covers: "Reviews · changes · claims",
    blurb: "Looks after existing clients: annual reviews, changes to their cover, and support through every claim.",
  },
  protection: {
    id: "protection",
    name: "Protection Advice",
    team: "Protection Advice Team",
    color: "#14b8a6",
    covers: "Personal · family · business",
    blurb: "New advice for individuals, families and business owners: life, TPD, trauma, income and business protection.",
  },
  shared: {
    id: "shared",
    name: "Both sides",
    team: "Practice Leadership",
    color: "#8b9cf7",
    covers: "The front door · leadership",
    blurb: "What both sides share: every request comes in through Olivia, and Theo leads both teams.",
  },
};

/**
 * The side of the floor each person's desk is on (Level 2 wings, left to right): Claims & Care
 * on the left, Olivia at the front door and Theo in the middle, Protection Advice on the right.
 */
const STAFF_SIDE: Record<string, SideId> = {
  "claims-1": "claims",
  "comms-1": "claims",
  "fincrime-1": "claims",
  "onboarding-1": "shared",
  "lead-1": "shared",
  "paraplanning-1": "protection",
  "research-1": "protection",
  "underwriting-1": "protection",
};

export function staffSide(staffId: string): SideId {
  return STAFF_SIDE[staffId] ?? "shared";
}

/** Where someone works, for their card: their team, or their place in the middle of the floor. */
export function staffPlace(staffId: string): string {
  if (staffId === "onboarding-1") return "The front door for both teams";
  if (staffId === "lead-1") return "Leads both teams";
  return SIDES[staffSide(staffId)].team;
}

/** Badge text for an agent's side. */
export function sideBadge(side: SideId): string {
  return side === "shared" ? "Shared · both sides" : `Dedicated · ${SIDES[side].name}`;
}

/** The side each scenario belongs to. */
export const SCENARIO_SIDE: Record<ScenarioId, SideId> = {
  family: "protection",
  fraud: "claims",
  business: "protection",
  income: "claims",
  bereavement: "claims",
};

/* ------------------------------------------------------------------------ */
/* Level 3 — the business owner and the idea                                 */
/* ------------------------------------------------------------------------ */

export const OWNER = {
  id: "owner",
  name: "Maya Ashworth",
  firstName: "Maya",
  role: "Managing Director, Life Advice Guru",
  badge: "Business owner for both sides",
  color: "#ffc35a",
  personality: "Curious, practical and people-first.",
  typical_line: "If a family has to chase us at the worst moment of their lives, we’ve already let them down.",
  summary:
    "Maya is the business owner for both sides of Life Advice Guru: Protection Advice and Claims & Care. She sets the goals, decides where to invest, and holds the line on one rule: people approve every decision that matters.",
  owns: [
    "The goals both sides work to",
    "The guardrails: when a person must approve, and what agents may never do",
    "Investment in the two teams, the agents and the platform",
    "The results clients, families and referral partners feel",
  ],
  // Smart-casual for the office under the dome: cream knit, slate trousers, a gold scarf.
  palette: {
    skin: "#b27b56",
    hair: "#2a1a12",
    shirt: "#f1ebe0",
    pants: "#3b4a5e",
    shoes: "#2a2420",
    accent: "#c9a14a",
    hairStyle: "long",
    glasses: true,
  } satisfies VoxelCharacterPalette,
};

/** The idea that lives in "the brain" above Maya's table: one hemisphere for each side. */
export const IDEA = {
  name: "Advice Team in a Day",
  era: "An advice practice for the agentic era",
  statement:
    "Run both sides of Life Advice Guru with small expert teams supported by one group of AI agents: dedicated where the work is specialised, shared where it isn’t, and all grounded in the same IQ layer on Azure. Requests become clearer, faster and more consistent. The adviser gives the advice, and people approve every decision that matters.",
  goals: [
    {
      tile: "CLEARER",
      title: "Clearer",
      text: "Clients, families and referral partners always know where their request is and what happens next.",
    },
    {
      tile: "FASTER",
      title: "Faster",
      text: "Agents do the reading, checking, calculating and drafting in minutes, so nobody waits in a queue.",
    },
    {
      tile: "CONSISTENT",
      title: "More consistent",
      text: "Both sides follow one lifecycle, and every check cites the rule it relied on.",
    },
    {
      tile: "PEOPLE APPROVE",
      title: "People approve",
      text: "Agents recommend. Amelia gives the advice, and a named person makes every decision that matters.",
    },
  ],
  lifecycle: ["Discover", "Verify", "Advise", "Implement", "Confirm"],
  /** Human approval is always required for these (AGENTS.md, "Advice and approvals"). */
  guardrails: [
    "Every Statement of Advice and Record of Advice, signed by the adviser",
    "Replacing or cancelling any existing cover",
    "Accepting a loading, an exclusion or a decline at underwriting",
    "Lodging any claim, and every death or terminal illness claim",
    "Blocking a request on financial-crime grounds, and referring suspected fraud to an insurer",
    "Any change to policy ownership, beneficiaries or bank details",
    "Advice with total cover of $2,000,000 or more on one case, which gets a second review",
    "Complaint responses",
    "Any communication to a fraud victim, a seriously ill client or a bereaved family",
  ],
  never: "Agents never give personal advice. Only Amelia, the adviser, gives advice and signs the Statement of Advice.",
};

/* ------------------------------------------------------------------------ */
/* Level 1 — one group of agents for both teams                              */
/* ------------------------------------------------------------------------ */

export type InfraId =
  | "workiq"
  | "fabriciq"
  | "foundryiq"
  | "webiq"
  | "foundry"
  | "mcp"
  | "teams"
  | "content"
  | "voice"
  | "apps"
  | "identity"
  | "vault"
  | "storage"
  | "monitor"
  | "network";

export interface AgentInfo {
  /** Foundry agent id (docs/foundry_agents.md). */
  foundryId: string;
  /** Cara also has a separate voice agent. */
  voiceId?: string;
  /** Which side of the practice the agent is dedicated to, or "shared" by both. */
  side: SideId;
  /** Why it is dedicated or shared. */
  sideNote: string;
  /** Engine-room components this agent calls while it works. */
  uses: InfraId[];
  /** The component that makes this agent different, used by the request trace. */
  signature: InfraId;
  signatureLine: string;
}

/** Every agent runs on Foundry and inherits the same IQ layer through the GuruAgent base class. */
const SHARED: InfraId[] = ["foundry", "workiq", "fabriciq", "foundryiq", "webiq"];

/** Keyed by staff id (personaData.ts). */
export const AGENTS: Record<string, AgentInfo> = {
  "onboarding-1": {
    foundryId: "onboarding-agent",
    side: "shared",
    sideNote: "Every request from both sides comes in here: new advice, reviews, changes and claim notifications.",
    uses: SHARED,
    signature: "foundryiq",
    signatureLine: "Runs the in-good-order checklist against the guides in the life_knowledge index.",
  },
  "paraplanning-1": {
    foundryId: "paraplanning-agent",
    side: "protection",
    sideNote: "Works out the cover each new client needs and drafts the Statement of Advice for Amelia to sign.",
    uses: SHARED,
    signature: "foundryiq",
    signatureLine: "Cites a rule from the life_knowledge index for every step of the advice.",
  },
  "research-1": {
    foundryId: "insurance-research-agent",
    side: "protection",
    sideNote: "Compares the insurers on the approved product list for every piece of new advice.",
    uses: [...SHARED, "mcp"],
    signature: "mcp",
    signatureLine: "Uses MCP tools to read product sheets, compare the APL insurers and build the comparison workbook.",
  },
  "fincrime-1": {
    foundryId: "financial-crime-agent",
    side: "shared",
    sideNote: "Protects clients on both sides from fraud, scams and identity takeover.",
    uses: [...SHARED, "content"],
    signature: "content",
    signatureLine: "Uses Azure Content Understanding to check documents for signs of tampering.",
  },
  "underwriting-1": {
    foundryId: "underwriting-agent",
    side: "protection",
    sideNote: "Takes each new application through underwriting with the insurer until the policy is issued.",
    uses: [...SHARED, "mcp"],
    signature: "mcp",
    signatureLine: "Uses MCP tools to look up insurers, check the evidence needed and generate the application summary PDF.",
  },
  "claims-1": {
    foundryId: "claims-agent",
    side: "claims",
    sideNote: "Supports existing clients and their families through every claim.",
    uses: [...SHARED, "mcp", "teams"],
    signature: "teams",
    signatureLine: "Uses MCP tools to check the policy and calculate the benefit, then waits for a Microsoft Teams approval before it lodges.",
  },
  "comms-1": {
    foundryId: "client-communications-agent",
    voiceId: "client-communications-voice-agent",
    side: "shared",
    sideNote: "Writes to clients, families and referral partners on both sides.",
    uses: [...SHARED, "voice"],
    signature: "voice",
    signatureLine: "Has a voice agent on Azure Voice Live, so clients can talk to Cara about their case.",
  },
  "lead-1": {
    foundryId: "team-leader-agent",
    side: "shared",
    sideNote: "Watches both teams’ queues and coordinates both sides’ agents on one case.",
    uses: SHARED,
    signature: "foundry",
    signatureLine: "Runs the group chat that coordinates the other seven agents on one case.",
  },
};

/* ------------------------------------------------------------------------ */
/* Level 1 — the roles: each agent and its two specialised assistants        */
/* ------------------------------------------------------------------------ */

export type RoleKind = "lead" | "assistant" | "voice";

/** What a role's robot carries, so its job reads at a glance. */
export type RoleProp =
  | "beacon"
  | "doc"
  | "clipboard"
  | "calculator"
  | "quill"
  | "book"
  | "chart"
  | "magnifier"
  | "timeline"
  | "form"
  | "stethoscope"
  | "folder"
  | "stamp"
  | "envelope"
  | "heart"
  | "clock"
  | "badge"
  | "headset";

export interface AgentRole {
  /** `<staffId>:lead`, `<staffId>:1`, `<staffId>:2`, or `comms-1:voice`. */
  id: string;
  staffId: string;
  kind: RoleKind;
  /** Full name, such as "Agent Olivia #1 — Fact Find Extraction". */
  name: string;
  /** Short name, such as "Fact Find Extraction". */
  short: string;
  description: string;
  prop: RoleProp;
  /** What it is seen doing at its own bay. */
  tasks: string[];
}

interface RoleExtra {
  props: [RoleProp, RoleProp];
  lead: string[];
  tasks: [string[], string[]];
}

/** Props and everyday tasks for each agent's roles; names and descriptions come from agentStages.ts. */
const ROLE_EXTRAS: Record<string, RoleExtra> = {
  "onboarding-1": {
    props: ["doc", "clipboard"],
    lead: ["Opening a Client Case", "Setting the case priority", "Routing a case"],
    tasks: [
      ["Reading a referral email", "Extracting the fact find", "Filling in the Client Case"],
      ["Running the in-good-order checklist", "Checking privacy consent", "Flagging a vulnerable client"],
    ],
  },
  "paraplanning-1": {
    props: ["calculator", "quill"],
    lead: ["Planning a Statement of Advice", "Citing the best interests duty", "Checking a replacement"],
    tasks: [
      ["Calculating life cover needs", "Working out TPD cover", "Showing the working"],
      ["Drafting the SoA scope", "Showing commission in dollars", "Writing the replacement analysis"],
    ],
  },
  "research-1": {
    props: ["book", "chart"],
    lead: ["Comparing the APL insurers", "Reading a PDS extract", "Building the comparison workbook"],
    tasks: [
      ["Comparing TPD definitions", "Checking an exclusion", "Reading the fine print"],
      ["Comparing stepped and level premiums", "Charting premiums over time", "Updating the workbook"],
    ],
  },
  "fincrime-1": {
    props: ["magnifier", "timeline"],
    lead: ["Scoring the case risk", "Recommending a call-back", "Screening PEP and sanctions"],
    tasks: [
      ["Checking a medical report", "Checking an ID for tampering", "Checking a bank statement"],
      ["Building a risk timeline", "Flagging a new device", "Reviewing a bank-detail change"],
    ],
  },
  "underwriting-1": {
    props: ["form", "stethoscope"],
    lead: ["Lodging an application", "Chasing the insurer", "Explaining a loading"],
    tasks: [
      ["Preparing an application", "Explaining the duty of care", "Checking a disclosure"],
      ["Tracking a GP report", "Checking blood test results", "Generating the application summary"],
    ],
  },
  "claims-1": {
    props: ["folder", "stamp"],
    lead: ["Building a claim pack", "Following up an insurer", "Checking a claim timeframe"],
    tasks: [
      ["Checking a definition", "Checking a waiting period", "Collecting the evidence"],
      ["Calculating a benefit", "Raising a Teams approval", "Lodging an approved claim"],
    ],
  },
  "comms-1": {
    props: ["envelope", "heart"],
    lead: ["Drafting a welcome letter", "Updating a referral partner", "Writing a claim update"],
    tasks: [
      ["Writing in plain English", "Summarising an SoA", "Drafting an SMS"],
      ["Checking the tone", "Adding IDR and AFCA rights", "Checking a disclosure"],
    ],
  },
  "lead-1": {
    props: ["clock", "badge"],
    lead: ["Running the group chat", "Coordinating the agents", "Summarising a case"],
    tasks: [
      ["Watching the claim clocks", "Checking IDR deadlines", "Tracking the advice pipeline"],
      ["Preparing an approval", "Auditing an advice file", "Flagging a vulnerable client"],
    ],
  },
};

function buildRoles(): AgentRole[] {
  const roles: AgentRole[] = [];
  for (const p of STAFF_PERSONAS) {
    const extra = ROLE_EXTRAS[p.id];
    const agent = AGENTS[p.id];
    if (!extra || !agent) continue;
    roles.push({
      id: `${p.id}:lead`,
      staffId: p.id,
      kind: "lead",
      name: p.agent_name,
      short: p.agent_name,
      description: p.agent_tagline,
      prop: "beacon",
      tasks: extra.lead,
    });
    const assistants = Object.values(STAGE_CONFIG).find((s) => s.role === p.role)?.agents ?? [];
    assistants.slice(0, 2).forEach((a, i) => {
      roles.push({
        id: `${p.id}:${i + 1}`,
        staffId: p.id,
        kind: "assistant",
        name: a.name,
        short: a.name.split("—")[1]?.trim() ?? a.name,
        description: a.description,
        prop: extra.props[i],
        tasks: extra.tasks[i],
      });
    });
    if (agent.voiceId) {
      roles.push({
        id: `${p.id}:voice`,
        staffId: p.id,
        kind: "voice",
        name: "Client Communications Voice Agent",
        short: "Voice Agent",
        description:
          "Takes calls from clients, families and referral partners about a case on Azure Voice Live, using only facts from the Client Case, and offers a call-back from a person for anything sensitive.",
        prop: "headset",
        tasks: ["Taking a client call", "Explaining next steps", "Offering a call-back"],
      });
    }
  }
  return roles;
}

/** Every role on the agent floor: 8 agents, their 16 assistants and Cara's voice agent. */
export const ROLES: AgentRole[] = buildRoles();

export function roleInfo(id: string): AgentRole | undefined {
  return ROLES.find((r) => r.id === id);
}

export function rolesOf(staffId: string): AgentRole[] {
  return ROLES.filter((r) => r.staffId === staffId);
}

/** The role that works the front desk: Olivia's Fact Find Extraction assistant. */
export const DESK_ROLE = "onboarding-1:1";

/**
 * Who each agent hands work to (docs/foundry_agents.md, "Suggested agent
 * handoff rules"). Any agent can also escalate to Theo's, and Theo's agent
 * coordinates the specialists on one case.
 */
export const HANDOFFS: Record<string, string[]> = {
  "onboarding-1": ["paraplanning-1", "claims-1", "fincrime-1"],
  "paraplanning-1": ["research-1", "underwriting-1"],
  "research-1": ["paraplanning-1"],
  "underwriting-1": ["comms-1"],
  "claims-1": ["fincrime-1", "comms-1"],
  "fincrime-1": ["lead-1"],
  "comms-1": [],
  "lead-1": ["onboarding-1", "paraplanning-1", "claims-1", "fincrime-1"],
};

/* ------------------------------------------------------------------------ */
/* The engine room — Microsoft IQ and the Azure services                     */
/* ------------------------------------------------------------------------ */

export interface InfraInfo {
  id: InfraId;
  name: string;
  /** Short label painted in the engine room. */
  sign: string;
  /** One-line subtitle under the sign. */
  tag: string;
  /** "iq": the Microsoft IQ layer on the raised deck; "azure": the services around it. */
  tier: TierId;
  kind: string;
  color: string;
  /** "all" when every agent depends on it. */
  scope: "all" | "some" | "platform";
  summary: string;
  details: string[];
  security: string;
  /** How the scenarios use it today, shown on its card under "In the scenarios". */
  inScenarios?: string;
}

export type TierId = "iq" | "azure";

/** The two tiers of the engine room. */
export const TIERS: Record<TierId, { name: string; tagline: string; color: string }> = {
  iq: { name: "Microsoft IQ", tagline: "the context layer every agent shares", color: "#e879f9" },
  azure: { name: "Azure services", tagline: "the platform underneath", color: "#60a5fa" },
};

export const INFRA: InfraInfo[] = [
  {
    id: "workiq",
    name: "Work IQ",
    sign: "WORK IQ",
    tag: "how people work",
    tier: "iq",
    kind: "Microsoft IQ · work context",
    color: "#fb7185",
    scope: "all",
    summary:
      "Context on how people work: the documents, conversations, meetings, collaboration and past decisions in Microsoft 365.",
    details: ["Referral emails, client calls and the team’s conversations about a case", "Part of Microsoft IQ"],
    security: "Permission-trimmed: an agent only sees the Microsoft 365 content its user is allowed to see.",
    inScenarios:
      "Not wired up yet. Referral emails and claim notifications arrive as case documents, and approvals go to Microsoft Teams through an MCP tool.",
  },
  {
    id: "fabriciq",
    name: "Fabric IQ",
    sign: "FABRIC IQ",
    tag: "the state of the business",
    tier: "iq",
    kind: "Microsoft IQ · business data",
    color: "#4ade80",
    scope: "all",
    summary:
      "The state of the business through governed data, business entities, relationships and rules: clients, policies, cover, premiums and claims across both sides.",
    details: ["A business model of entities and relationships that agents can reason over", "Built on Microsoft Fabric", "Part of Microsoft IQ"],
    security: "Governed data: people and agents work under the same rules and permissions.",
    inScenarios: "Not wired up yet. The backend’s Client Case store and the case files stand in for it.",
  },
  {
    id: "foundryiq",
    name: "Foundry IQ · life_knowledge",
    sign: "FOUNDRY IQ",
    tag: "policies and documents",
    tier: "iq",
    kind: "Microsoft IQ · knowledge",
    color: "#c4b5fd",
    scope: "all",
    summary:
      "Curated knowledge: the insurers’ PDS extracts, Life Advice Guru’s guides, the advice, claims and tax rules, and the case files in the life_knowledge index, returned with citations.",
    details: [
      "Foundry IQ is the new name for Azure AI Search",
      "Semantic search enabled",
      "Built and loaded with ai-search/setup-index.ps1 and ingest-documents.ps1",
    ],
    security:
      "Foundry’s managed identities read the index with Search Index Data Reader; the web apps hold Search Index Data Contributor.",
    inScenarios: "Live: every agent grounds on, and cites, the life_knowledge index.",
  },
  {
    id: "webiq",
    name: "Web IQ",
    sign: "WEB IQ",
    tag: "the web, right now",
    tier: "iq",
    kind: "Microsoft IQ · web context",
    color: "#38bdf8",
    scope: "all",
    summary: "Timely, citation-ready context from the public web, so agents can check up-to-date reference information.",
    details: [
      "The Foundry web search tool on the eight staff agents, powered by Web IQ",
      "Searches from Melbourne, Victoria, and cites each source",
      "Part of Microsoft IQ",
    ],
    security:
      "Added directly to each agent, web search needs no connection or keys. Agents send search queries only, never client details.",
    inScenarios:
      "Live: the eight staff agents can search the public web through Web IQ, for example to confirm a current super or tax rule. Cara’s voice agent doesn’t, so a call never reads out links.",
  },
  {
    id: "foundry",
    name: "Microsoft Foundry",
    sign: "MICROSOFT FOUNDRY",
    tag: "9 agents · gpt-5.4",
    tier: "azure",
    kind: "Azure · agent platform and model",
    color: "#a78bfa",
    scope: "all",
    summary:
      "Hosts the nine agents (eight staff agents plus Cara’s voice agent) and the gpt-5.4 model they reason with.",
    details: [
      "Declarative agents created from code with Azure.AI.Projects, on a shared GuruAgent base class",
      "Model deployment gpt-5.4 (DataZoneStandard) with the default responsible-AI content filters",
      "Theo’s group chat coordinates the specialist agents on a single case",
    ],
    security:
      "The backend calls the project endpoint with its managed identity, which holds the Azure AI User, Azure AI Developer and Cognitive Services OpenAI User roles.",
  },
  {
    id: "mcp",
    name: "MCP server · guru-ops-mcp",
    sign: "MCP TOOLS",
    tag: "guru-ops-mcp at /mcp",
    tier: "azure",
    kind: "Azure · tools that act on systems",
    color: "#f472b6",
    scope: "some",
    summary:
      "The backend serves Model Context Protocol tools at /mcp, and Foundry calls them while an agent works. This is how agents act on systems, not just talk about them.",
    details: [
      "Insurance Research: read a product sheet, compare the APL insurers, build the comparison workbook",
      "Underwriting: look up an insurer, check the evidence needed, generate the application summary PDF",
      "Claims: check the policy, calculate the benefit, check the approval rules, request approval and lodge the claim",
      "Teams: send the approval card",
    ],
    security: "Foundry reaches the backend’s /mcp endpoint over HTTPS at the public URL set in APP_MCP_URL.",
  },
  {
    id: "teams",
    name: "Microsoft Teams approvals",
    sign: "TEAMS APPROVALS",
    tag: "human approvals",
    tier: "azure",
    kind: "Microsoft 365 · human in the loop",
    color: "#818cf8",
    scope: "some",
    summary:
      "Carries approvals to people. A claim waits until a person approves the adaptive card in Microsoft Teams, and only then is it lodged.",
    details: [
      "Sent by the teams_sendApprovalCard MCP tool",
      "Ben’s agent raises the card, and Theo approves it",
      "In the scenarios, delivery to the Teams channel is simulated",
    ],
    security: "The card shows masked details only, and nothing is lodged until a person approves.",
  },
  {
    id: "content",
    name: "Azure Content Understanding",
    sign: "CONTENT UNDERSTANDING",
    tag: "document checks",
    tier: "azure",
    kind: "Azure · document analysis",
    color: "#fb923c",
    scope: "some",
    summary:
      "Checks IDs, medical reports, death certificates, payslips, BAS and bank statements, and returns a legit, suspicious or fake verdict with the reasons.",
    details: ["Used by Felix’s Financial Crime Agent, for both sides"],
    security: "Signs in with DefaultAzureCredential, or an API key when one is configured.",
  },
  {
    id: "voice",
    name: "Azure Voice Live",
    sign: "VOICE LIVE",
    tag: "Cara’s voice agent",
    tier: "azure",
    kind: "Azure · real-time voice",
    color: "#ec4899",
    scope: "some",
    summary: "Lets clients, families and referral partners talk to Cara’s voice agent about a case, in plain English.",
    details: ["Runs client-communications-voice-agent", "The backend proxies the Voice Live WebSocket"],
    security: "The backend’s managed identity signs each Voice Live connection.",
  },
  {
    id: "apps",
    name: "Azure App Service",
    sign: "APP SERVICE",
    tag: "frontend + backend",
    tier: "azure",
    kind: "Azure · hosting",
    color: "#60a5fa",
    scope: "platform",
    summary:
      "Two Linux web apps on one plan: the frontend (this voxel site) and the backend (the Blazor agent console, its APIs, the Client Case store and the MCP endpoint).",
    details: ["Frontend: Node 20 serving the static Vite build", "Backend: .NET 10", "Deployed by GitHub Actions"],
    security:
      "Both web apps are HTTPS-only with TLS 1.2, FTP is disabled, and each runs with a system-assigned managed identity.",
  },
  {
    id: "identity",
    name: "Microsoft Entra ID",
    sign: "ENTRA ID",
    tag: "managed identities",
    tier: "azure",
    kind: "Azure · identity and access",
    color: "#facc15",
    scope: "platform",
    summary:
      "No keys in code: the web apps and Foundry sign in with managed identities and DefaultAzureCredential, and role assignments limit what each one can do.",
    details: [
      "Azure AI User, Azure AI Developer and Cognitive Services OpenAI User on Foundry",
      "Search Index Data Reader, Search Index Data Contributor and Search Service Contributor on Foundry IQ (Azure AI Search)",
    ],
    security: "Every call carries an Entra ID token scoped by these roles.",
  },
  {
    id: "vault",
    name: "Azure Key Vault",
    sign: "KEY VAULT",
    tag: "secrets and keys",
    tier: "azure",
    kind: "Azure · secrets",
    color: "#94a3b8",
    scope: "platform",
    summary: "Provisioned with the platform as the place for secrets and keys, so they never live in code or config.",
    details: ["Standard tier"],
    security: "The agents and apps sign in with managed identities rather than stored keys.",
  },
  {
    id: "storage",
    name: "Azure Storage",
    sign: "STORAGE",
    tag: "StorageV2",
    tier: "azure",
    kind: "Azure · files",
    color: "#34d399",
    scope: "platform",
    summary: "A general-purpose StorageV2 account deployed with the platform, ready for document uploads and generated files.",
    details: ["Standard locally redundant storage", "Seven-day soft delete for blobs"],
    security: "HTTPS only with TLS 1.2, and no anonymous access to blobs.",
  },
  {
    id: "monitor",
    name: "Application Insights · Log Analytics",
    sign: "APP INSIGHTS",
    tag: "telemetry",
    tier: "azure",
    kind: "Azure · observability",
    color: "#f59e0b",
    scope: "platform",
    summary:
      "Both web apps send telemetry to Application Insights, backed by a Log Analytics workspace. The Foundry project is connected too, so every agent run is traced.",
    details: [
      "The connection string is set on both App Service apps",
      "Foundry traces each agent run (model calls and tool calls) to the same Application Insights",
      "30-day retention in Log Analytics",
    ],
    security: "Workspace-based Application Insights, so telemetry lands in the Log Analytics workspace.",
  },
  {
    id: "network",
    name: "Network and wiring",
    sign: "NETWORK",
    tag: "HTTPS · WebSocket",
    tier: "azure",
    kind: "Azure · how it all connects",
    color: "#22d3ee",
    scope: "platform",
    summary:
      "Every hop is HTTPS: the browser to App Service, the backend to the Foundry project endpoint, Foundry to Foundry IQ, the web and the backend’s /mcp tools, plus a WebSocket for Voice Live.",
    details: [
      "Public endpoints today, secured with Entra ID tokens and role assignments",
      "For production, add private endpoints and virtual network integration",
    ],
    security: "TLS on every connection, with identity checked on every call.",
  },
];

export function infraInfo(id: InfraId): InfraInfo {
  const i = INFRA.find((x) => x.id === id);
  if (!i) throw new Error(`Unknown engine-room component: ${id}`);
  return i;
}

const INFRA_SHORT: Partial<Record<InfraId, string>> = {
  teams: "Teams approvals",
  content: "Content Understanding",
  voice: "Voice Live",
};

/** A component's name, short enough for a chip: "Foundry IQ", "MCP server", "Teams approvals". */
export function infraShort(id: InfraId): string {
  return INFRA_SHORT[id] ?? infraInfo(id).name.split(" · ")[0];
}

/** Staff ids whose agents depend on a component ("all" and "platform" mean every agent). */
export function agentsUsing(id: InfraId): string[] {
  const scope = infraInfo(id).scope;
  return Object.entries(AGENTS)
    .filter(([, a]) => scope !== "some" || a.uses.includes(id))
    .map(([staffId]) => staffId);
}

/* ------------------------------------------------------------------------ */
/* The front desk — visitors arrive by tram and sign in under the verandah   */
/* ------------------------------------------------------------------------ */

export const FRONT_DESK = {
  name: "The front desk",
  summary:
    "Clients, families and referral partners arrive by tram and sign in at the front desk under the verandah: a fact find, a referral or a claim notification. The greeter welcomes them, Amelia, Life Advice Guru’s adviser, gets them started with the right agents, and Olivia’s Fact Find Extraction agent reads what they sign straight into a new Client Case.",
  inScenarios:
    "Requests arrive as referral emails, enquiries, calls and claim notifications. The front desk is the storybook’s way of showing them coming in.",
};

/** An agent Amelia links visitors with, and what it does to get them started. */
export interface AdviserLink {
  /** Staff id of the person the agent works for (personaData.ts). */
  id: string;
  why: string;
}

/**
 * Amelia, Life Advice Guru's senior risk adviser at the front desk. She gives the advice (agents never
 * do) and signs every Statement of Advice, then links each visitor with the agents that get them
 * started. Like the staff, her name starts with the first letter of her role.
 */
export const ADVISER = {
  /** Her character id, for picking. */
  id: "amelia",
  name: "Amelia",
  role: "Senior Risk Adviser",
  line: "Gives the advice, signs every Statement of Advice, and links each visitor with the agents that get them started.",
  typical_line: "Hi Priya! Hannah sent your details ahead — let’s look after your family.",
  color: "#ffc35a",
  palette: {
    skin: "#b97a56",
    hair: "#1d1410",
    shirt: "#8b2f4b",
    pants: "#2b2f3a",
    shoes: "#1a1a1a",
    accent: "#c9a14a",
    hairStyle: "ponytail",
    shirtLogo: true,
  } as VoxelCharacterPalette,
  /** What she is doing between visitors. */
  tasks: ["Reviewing a Statement of Advice", "Signing an SoA", "Preparing an annual review", "Reading an insurer’s PDS"],
  links: [
    { id: "onboarding-1", why: "Opens the Client Case and checks it is in good order" },
    { id: "paraplanning-1", why: "Works out the cover needed for new advice" },
    { id: "research-1", why: "Compares the insurers on the approved product list" },
    { id: "claims-1", why: "Supports clients and families through a claim" },
    { id: "comms-1", why: "Keeps everyone updated in plain English" },
  ] as readonly AdviserLink[],
};

/** Someone who comes along with a visitor: a business partner or a support person. */
export interface Companion {
  id: string;
  name: string;
  palette: VoxelCharacterPalette;
}

/** How each finale client arrives at the front desk. */
export interface Arrival {
  /** The client signs, or someone signs in their name (the fraud scenario). */
  visitor: "client" | "impostor";
  /** By tram (the default), or down from the party on the roof in the glass lift. */
  by?: "tram" | "lift";
  /** Who comes with them. */
  companion?: Companion;
  /** Painted on the paper they sign. */
  doc: string;
  /** Opening line of the finale's first step. */
  line: string;
  /** Amelia's welcome as they walk up. */
  welcome: string;
  /** The agents Amelia links them with while they sign, by staff id. */
  links: readonly string[];
}

export const ARRIVALS: Record<ScenarioId, Arrival> = {
  family: {
    visitor: "client",
    by: "lift",
    doc: "Fact find · family protection",
    line: "Priya comes down from the party on the roof in the glass lift, and signs her fact find at the front desk.",
    welcome: "Hi Priya! Hannah sent your details ahead",
    links: ["onboarding-1", "paraplanning-1"],
  },
  fraud: {
    visitor: "impostor",
    doc: "Trauma claim and bank change · LB-19004417",
    line: "Someone in a cap and dark glasses arrives by tram and signs at the front desk in Grace’s name. Grace is in Italy.",
    welcome: "Welcome to Life Advice Guru. Let’s get this lodged",
    links: ["onboarding-1"],
  },
  business: {
    visitor: "client",
    by: "lift",
    companion: { id: "harpreet", name: "Harpreet Kaur", palette: PALETTES.clientHarpreet },
    doc: "Fact find · business protection",
    line: "George and Harpreet come down from the party on the roof in the glass lift to start their business protection advice.",
    welcome: "Hi George, hi Harpreet! Marcus sent it all",
    links: ["onboarding-1", "paraplanning-1"],
  },
  income: {
    visitor: "client",
    companion: { id: "kylie", name: "Kylie Bradley", palette: PALETTES.clientKylie },
    doc: "Claim notification · income protection",
    line: "Tom arrives by tram with Kylie, his support person, and signs his claim notification at the front desk.",
    welcome: "Hi Tom, hi Kylie. We’ll help you through this",
    links: ["onboarding-1", "claims-1"],
  },
  bereavement: {
    visitor: "client",
    doc: "Death claim notification",
    line: "Robert arrives by tram with Mei’s paperwork, and signs at the front desk.",
    welcome: "Hi Robert. Take all the time you need",
    links: ["onboarding-1", "claims-1"],
  },
};

/** Someone signing in another client's name: a cap pulled low and dark glasses. */
export const IMPOSTOR_PALETTE: VoxelCharacterPalette = {
  skin: "#d6a982",
  hair: "#2b2f36",
  shirt: "#3a4150",
  pants: "#1d2129",
  shoes: "#14161a",
  accent: "#1d2129",
  hairStyle: "cap",
  glasses: true,
};

/** Everyday visitors who drop by the front desk between the finale's requests. */
export interface DropIn {
  id: string;
  who: string;
  side: SideId;
  doc: string;
  summary: string;
  palette: VoxelCharacterPalette;
  companion?: Companion;
  /** Amelia's welcome as they walk up. */
  welcome: string;
  /** The agents Amelia links them with while they sign, by staff id. */
  links: readonly string[];
}

export const DROP_INS: DropIn[] = [
  {
    id: "young-couple",
    who: "A young couple",
    side: "protection",
    doc: "Fact find · our first home",
    summary: "Have just bought their first home, and want advice on protecting it and each other.",
    welcome: "Welcome! Let’s look after your new home",
    links: ["onboarding-1", "paraplanning-1"],
    palette: {
      skin: "#e3b48f",
      hair: "#4a2c1d",
      shirt: "#14b8a6",
      pants: "#334155",
      shoes: "#1f2937",
      accent: "#f8fafc",
      hairStyle: "bun",
    },
    companion: {
      id: "partner",
      name: "Her partner",
      palette: { skin: "#c48c64", hair: "#2b1a12", shirt: "#f59e0b", pants: "#2a3344", shoes: "#f4f0e6", hairStyle: "short" },
    },
  },
  {
    id: "business-owner",
    who: "A business owner",
    side: "protection",
    doc: "Fact find · business protection",
    summary: "Runs a small design studio and wants to protect it if something happens to her.",
    welcome: "Welcome! Let’s protect your business",
    links: ["onboarding-1", "research-1"],
    palette: {
      skin: "#f1c7a1",
      hair: "#6b4423",
      shirt: "#1f3a5f",
      pants: "#2a2f3a",
      shoes: "#1a1a1a",
      accent: "#c9a14a",
      hairStyle: "long",
      glasses: true,
      shirtLogo: true,
    },
  },
  {
    id: "annual-review",
    who: "A client at his annual review",
    side: "claims",
    doc: "Annual review · update",
    summary: "Here for his annual review with Amelia, to check his cover still fits his life.",
    welcome: "Good to see you! Time for your review",
    links: ["onboarding-1", "comms-1"],
    palette: {
      skin: "#c48c64",
      hair: "#cfcfd4",
      shirt: "#7c5a3a",
      pants: "#d6cfc2",
      shoes: "#3a2a20",
      hairStyle: "short",
      facial: "stubble",
      glasses: true,
    },
  },
  {
    id: "broker",
    who: "A mortgage broker",
    side: "shared",
    doc: "Referral · new home loan client",
    summary: "Drops off a referral, with her client’s consent, for a family who has just bought a home.",
    welcome: "Thanks! We’ll take good care of them",
    links: ["onboarding-1"],
    palette: {
      skin: "#8d5a3b",
      hair: "#1a1410",
      shirt: "#334155",
      pants: "#334155",
      shoes: "#1a1a1a",
      accent: "#14b8a6",
      hairStyle: "ponytail",
      lowerBody: "skirt",
    },
  },
  {
    id: "claim",
    who: "A client making a claim",
    side: "claims",
    doc: "Claim notification · trauma",
    summary: "Has come in to start a trauma claim, and Amelia will help her through it.",
    welcome: "We’re here for you. Let’s start your claim",
    links: ["onboarding-1", "claims-1"],
    palette: {
      skin: "#f3d1b5",
      hair: "#b8862e",
      shirt: "#9b6ad6",
      pants: "#2f3a52",
      shoes: "#3a2a20",
      hairStyle: "long",
    },
  },
];

/* ------------------------------------------------------------------------ */
/* The roof — a party for the clients                                        */
/* ------------------------------------------------------------------------ */

export const PARTY = {
  name: "The rooftop celebration",
  summary:
    "Life Advice Guru throws a party on the roof for its clients and referral partners: the people the whole house works for.",
  /** Printed on the front of the DJ booth. */
  tagline: "Celebrating our clients and referral partners",
  guests:
    "Priya and Arjun with Asha, George and Helen with Harpreet, the referral partners Hannah Lewis and Marcus Tan, and clients from both sides",
};

/** The big marquee sign along the back of the roof. */
export const PARTY_SIGN = "LIFE ADVICE GURU";

export interface PartyGuest {
  id: string;
  name: string;
  /** Under the name on the card. */
  role: string;
  /** The card's one plain-English line. */
  line: string;
  /** The side they are with. Not set for family members and referral partners. */
  side?: SideId;
  palette: VoxelCharacterPalette;
  /** Drawn smaller: a toddler. */
  child?: boolean;
  /** Their request in the finale, if they have one. */
  scenario?: ScenarioId;
  /** The card action that replays it. */
  story?: string;
  /** Goes down to the front desk when this scenario plays (with the client, or as the client). */
  leavesFor?: ScenarioId;
  /** A labelled fact for the card: who they came with, or their clients here. */
  with?: [label: string, names: string];
  /** What they do at their home spot. */
  home: string[];
}

const clientName = (id: ScenarioId): string => CLIENT_PERSONAS.find((c) => c.id === id)?.name ?? "";

/**
 * The guests are clients with happy news, their families and the referral partners who sent
 * them. The other finale clients' stories (a scam, an injury and a bereavement) are not party
 * stories, so they are not on the roof.
 */
export const PARTY_GUESTS: PartyGuest[] = [
  {
    id: "priya",
    name: clientName("family"),
    role: "A new Protection Advice client",
    line: "Her family’s cover started this week, and she’s celebrating with Arjun and Asha.",
    side: "protection",
    palette: PALETTES.clientFamily,
    scenario: "family",
    story: "Watch her request ›",
    leavesFor: "family",
    with: ["Here with", "Arjun and Asha"],
    home: ["Telling Hannah about the new house", "Catching up with Hannah"],
  },
  {
    id: "arjun",
    name: "Arjun Mehta",
    role: "Priya’s partner",
    line: "Here with Priya and Asha to celebrate the new house, and the cover that protects it.",
    palette: PALETTES.clientArjun,
    with: ["Here with", "Priya and Asha"],
    home: ["Chatting with Hannah", "Keeping an eye on Asha"],
  },
  {
    id: "asha",
    name: "Asha",
    role: "Priya and Arjun’s daughter",
    line: "Two years old, and the first one on the dance floor.",
    palette: PALETTES.clientAsha,
    child: true,
    with: ["Here with", "Priya and Arjun"],
    home: ["Playing with a balloon", "Waving at the DJ"],
  },
  {
    id: "george",
    name: clientName("business"),
    role: "A Protection Advice client",
    line: "Celebrating the succession plan for Northcote Custom Joinery with Helen and Harpreet.",
    side: "protection",
    palette: PALETTES.clientBusiness,
    scenario: "business",
    story: "Watch his request ›",
    leavesFor: "business",
    with: ["Here with", "Helen and Harpreet"],
    home: ["Telling Marcus about the business", "Chatting with Marcus and Helen"],
  },
  {
    id: "helen",
    name: "Helen Papadopoulos",
    role: "George’s wife",
    line: "Here to celebrate the succession plan with George and Harpreet.",
    palette: PALETTES.clientHelen,
    with: ["Here with", "George and Harpreet"],
    home: ["Chatting with George and Marcus", "Toasting the succession plan"],
  },
  {
    id: "harpreet",
    name: "Harpreet Kaur",
    role: "George’s business partner",
    line: "Co-owner of Northcote Custom Joinery, celebrating the buy-sell cover that protects them both.",
    side: "protection",
    palette: PALETTES.clientHarpreet,
    leavesFor: "business",
    with: ["Here with", "George and Helen"],
    home: ["Talking business with Marcus", "Chatting with Helen"],
  },
  {
    id: "hannah",
    name: "Hannah Lewis",
    role: "Referral partner · Yarra Home Loans",
    line: "The mortgage broker who referred Priya, here to celebrate with her clients.",
    palette: {
      skin: "#f2c9a8",
      hair: "#9a3f1c",
      shirt: "#1f2f4d",
      pants: "#1f2f4d",
      shoes: "#1a1a1a",
      accent: "#c9a14a",
      hairStyle: "long",
      lowerBody: "skirt",
    },
    with: ["Client here", "Priya"],
    home: ["Catching up with Priya", "Hearing about the new house"],
  },
  {
    id: "marcus",
    name: "Marcus Tan",
    role: "Referral partner · Bayside Business Accountants",
    line: "The accountant who referred George and Harpreet, here to toast their succession plan.",
    palette: {
      skin: "#e5b98f",
      hair: "#141414",
      shirt: "#2f6f6a",
      pants: "#2a2f3a",
      shoes: "#3a2a20",
      accent: "#f8fafc",
      hairStyle: "short",
      glasses: true,
    },
    with: ["Clients here", "George and Harpreet"],
    home: ["Catching up with George", "Talking numbers with Harpreet"],
  },
  {
    id: "backatwork",
    name: "A client back at work",
    role: "A Claims & Care client",
    line: "Back at work after an income protection claim saw her family through her recovery.",
    side: "claims",
    palette: {
      skin: "#7a4e33",
      hair: "#1d1d1d",
      shirt: "#c9a14a",
      pants: "#2a3344",
      shoes: "#1a1a1a",
      hairStyle: "afro",
      lowerBody: "skirt",
    },
    home: ["Relaxing in the lounge", "Chatting in the lounge"],
  },
  {
    id: "retired-1",
    name: "A retired client",
    role: "A long-time Life Advice Guru client",
    line: "A client for years, here with his wife to celebrate with the team.",
    side: "claims",
    palette: {
      skin: "#e8b896",
      hair: "#f0f0f0",
      shirt: "#14b8a6",
      pants: "#d6cfc2",
      shoes: "#5a3a25",
      accent: "#f8fafc",
      hairStyle: "hat",
      glasses: true,
    },
    with: ["Here with", "his wife"],
    home: ["Relaxing in the lounge", "Telling a story in the lounge"],
  },
  {
    id: "retired-2",
    name: "A retired client",
    role: "A long-time Life Advice Guru client",
    line: "A client for years, here with her husband to celebrate with the team.",
    side: "claims",
    palette: {
      skin: "#f3d1b5",
      hair: "#e8e4de",
      shirt: "#9b6ad6",
      pants: "#ece6da",
      shoes: "#d9a03a",
      accent: "#f8fafc",
      hairStyle: "bun",
    },
    with: ["Here with", "her husband"],
    home: ["Laughing in the lounge", "Chatting in the lounge"],
  },
];

/** The DJ behind the decks. */
export const PARTY_DJ_PALETTE: VoxelCharacterPalette = {
  skin: "#a26f4c",
  hair: "#111111",
  shirt: "#111827",
  pants: "#1f2937",
  shoes: "#f4f0e6",
  accent: "#5ce1ff",
  hairStyle: "cap",
  glasses: true,
  shirtLogo: true,
};
