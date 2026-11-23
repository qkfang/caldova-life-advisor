/**
 * Single source of truth for staff and client persona data.
 *
 * All persona-level facts (name, role or situation, personality, typical
 * line, palette, request type) live here so that Guru House's cast, its
 * cards and the finale never disagree.
 *
 * Mirrors the Life Advice Guru canon (AGENTS.md, docs/characters.md and
 * docs/foundry_agents.md) and matches the palette keys defined in
 * `characterPalettes.ts`.
 */
import type { PALETTES } from "./characterPalettes";

export type PaletteKey = keyof typeof PALETTES;

export type StaffRole =
  | "Onboarding & Requests Officer"
  | "Paraplanner"
  | "Insurance Research Analyst"
  | "Financial Crime Investigator"
  | "Underwriting Coordinator"
  | "Benefit Claims Advocate"
  | "Client Communications Specialist"
  | "Practice Team Leader";

/**
 * The two small teams of Life Advice Guru, one for each side of the
 * practice, plus the leadership that runs both. Olivia is listed with the
 * Protection Advice Team, but every request from both sides comes in through
 * her, so she sits at the front door in the middle of Level 2.
 */
export type StaffTeam = "Protection Advice Team" | "Claims & Care Team" | "Practice Leadership";

export interface StaffPersona {
  /** Stable id used for click hitboxes and the house's anchors. */
  id: string;
  name: string;
  role: StaffRole;
  /** Team the staff member belongs to. */
  team: StaffTeam;
  /** Department name shown in the UI (e.g. "Paraplanning"). */
  department: string;
  /** Human-readable role and duties summary (one line). */
  role_short: string;
  /** The role in one short sentence, as in docs/characters.md ("**Role:**"), for compact cards. */
  role_line: string;
  palette: PaletteKey;
  /** Accent colour for their desk, their agent's bay and their cards. */
  color: string;
  personality: string;
  /** Bullet list of "what they do". */
  responsibilities: string[];
  /** Their signature line, as in docs/characters.md. */
  typical_line: string;
  /** Rotating status lines shown while the staff member is at their desk. */
  ambient: string[];
  /**
   * Id of the matching agent page in the companion `src/backend` Blazor site.
   * Mirrors an Id in `src/backend/Models/AgentCatalog.cs` and is used to link
   * to `/agents/{app_agent_id}`.
   */
  app_agent_id: string;
  /** Name of the AI agent working behind this staff member (as in the backend catalogue). */
  agent_name: string;
  /** One-line summary of what the agent does for them. */
  agent_tagline: string;
}

// Order matches docs/scenario-N-*.md:
//   1 family, 2 fraud, 3 business, 4 income, 5 bereavement.
export type ScenarioId = "family" | "fraud" | "business" | "income" | "bereavement";

export interface ClientPersona {
  id: ScenarioId;
  name: string;
  /** Short summary for cards. */
  situation: string;
  /** Long-form narrative. */
  situation_long: string;
  need: string[];
  concern: string;
  personality: string;
  typical_line: string;
  palette: PaletteKey;
  /** Headline request type (matches the backend RequestTypes values). */
  request_type: string;
  /** Scenario colour, used for the client's chip in the finale. */
  color: string;
}

/* ------------------------------------------------------------------------ */
/* Staff cast — the Life Advice Guru practice team                        */
/* ------------------------------------------------------------------------ */

export const STAFF_PERSONAS: StaffPersona[] = [
  {
    id: "onboarding-1",
    name: "Olivia",
    role: "Onboarding & Requests Officer",
    team: "Protection Advice Team",
    department: "Onboarding & Requests",
    role_short:
      "The front door of the practice: every enquiry, referral, review and claim notification starts with Olivia, from both sides of Life Advice Guru.",
    role_line: "The front door for every request, from both sides of the practice.",
    palette: "onboardingOfficer",
    color: "#0ea5e9",
    personality: "Calm, meticulous and welcoming.",
    responsibilities: [
      "Reads referral emails, enquiries, fact-find questionnaires and claim notifications",
      "Extracts the fact find into the Client Case",
      "Runs the in-good-order checklist: FSG, privacy and health-information consent, ID and authorities",
      "Opens the Client Case, sets its priority and flags vulnerable clients",
      "Routes the case to Paraplanning, Claims or Financial Crime",
    ],
    typical_line: "Let’s get your details right the first time.",
    ambient: [
      "Waiting for the next request",
      "Checking a privacy consent",
      "Logging a new referral",
      "Opening a Client Case",
      "Reading a fact-find questionnaire",
    ],
    app_agent_id: "onboarding",
    agent_name: "Onboarding Agent",
    agent_tagline: "Every request set up right the first time, from the fact find to the Client Case.",
  },
  {
    id: "paraplanning-1",
    name: "Penny",
    role: "Paraplanner",
    team: "Protection Advice Team",
    department: "Paraplanning",
    role_short:
      "Works out how much cover each client needs and how to hold it, and drafts the Statement of Advice for Amelia to review and sign.",
    role_line: "Works out the cover each client needs and drafts the Statement of Advice.",
    palette: "paraplanner",
    color: "#a855f7",
    personality: "Precise, thoughtful and explains numbers simply.",
    responsibilities: [
      "Runs the needs analysis for life, TPD, trauma and income protection",
      "Works out how to hold the cover, inside or outside super",
      "Drafts the Statement of Advice, citing each rule",
      "Shows premiums and commission in dollars",
      "Hands the Authority to Proceed to Underwriting",
    ],
    typical_line: "Let’s work out what your family actually needs, then check it twice.",
    ambient: [
      "Running a needs analysis",
      "Checking a cover shortfall",
      "Drafting a Statement of Advice",
      "Comparing inside and outside super",
      "Checking the working twice",
    ],
    app_agent_id: "paraplanning",
    agent_name: "Paraplanning Agent",
    agent_tagline: "Works out how much cover is needed, shows the working and drafts the SoA.",
  },
  {
    id: "research-1",
    name: "Ivy",
    role: "Insurance Research Analyst",
    team: "Protection Advice Team",
    department: "Insurance Research",
    role_short:
      "Compares the insurers on Life Advice Guru’s approved product list on definitions, features, exclusions and premiums, so the advice rests on the fine print.",
    role_line: "Compares the insurers on the approved product list.",
    palette: "researchAnalyst",
    color: "#f59e0b",
    personality: "Curious, analytical and loves the fine print.",
    responsibilities: [
      "Reads each insurer’s PDS extract: definitions, features and exclusions",
      "Compares the APL insurers for each client’s cover",
      "Compares stepped and level premiums over time",
      "Builds the comparison workbook and chart",
      "Keeps the approved product list research current",
    ],
    typical_line: "Cheapest isn’t best if the definition won’t pay.",
    ambient: [
      "Reading a PDS extract",
      "Comparing TPD definitions",
      "Charting stepped and level premiums",
      "Checking a trauma definition",
      "Updating the comparison workbook",
    ],
    app_agent_id: "insurance-research",
    agent_name: "Insurance Research Agent",
    agent_tagline: "Compares the insurers on the APL, definitions first and premiums second.",
  },
  {
    id: "fincrime-1",
    name: "Felix",
    role: "Financial Crime Investigator",
    team: "Claims & Care Team",
    department: "Financial Crime",
    role_short:
      "Protects clients on both sides from fraud, scams and identity takeover: checks identity and documents, and explains every red flag.",
    role_line: "Protects clients from fraud, scams and identity takeover.",
    palette: "finCrimeInvestigator",
    color: "#ef4444",
    personality: "Sharp, protective and never accusatory.",
    responsibilities: [
      "Verifies identity for every new client",
      "Checks IDs, medical reports, death certificates and bank statements for tampering",
      "Builds the timeline of contact and bank-detail changes",
      "Screens for PEP and sanctions matches",
      "Recommends holds and call-backs; a block is always a human decision",
    ],
    typical_line: "If it’s urgent and different, it’s worth a second look.",
    ambient: [
      "Running PEP and sanctions screening",
      "Reviewing a bank-detail change",
      "Checking a new-device login",
      "Verifying a driver licence",
      "Checking a medical report",
    ],
    app_agent_id: "financial-crime",
    agent_name: "Financial Crime Agent",
    agent_tagline: "Checks every document and explains every red flag, without accusing anyone.",
  },
  {
    id: "underwriting-1",
    name: "Uma",
    role: "Underwriting Coordinator",
    team: "Protection Advice Team",
    department: "Underwriting & New Business",
    role_short:
      "Takes each application through underwriting with the insurer, from the duty to take reasonable care to the policy being issued.",
    role_line: "Takes each application through underwriting until the policy is issued.",
    palette: "underwritingCoord",
    color: "#10b981",
    personality: "Organised, persistent and plain-spoken about health questions.",
    responsibilities: [
      "Prepares the application and explains the duty to take reasonable care",
      "Tracks medicals, blood tests, GP reports and financial evidence",
      "Chases the insurer and explains loadings and exclusions",
      "Brings every loading, exclusion or decline to the client and Amelia",
      "Confirms the policy is issued and in force",
    ],
    typical_line: "Answer every question carefully. It protects your claim later.",
    ambient: [
      "Chasing a GP report",
      "Checking underwriting requirements",
      "Preparing an application",
      "Explaining a loading",
      "Confirming a policy is in force",
    ],
    app_agent_id: "underwriting",
    agent_name: "Underwriting Agent",
    agent_tagline: "Takes the application through underwriting, from disclosure to the policy.",
  },
  {
    id: "claims-1",
    name: "Ben",
    role: "Benefit Claims Advocate",
    team: "Claims & Care Team",
    department: "Claims",
    role_short:
      "Supports clients and families through a claim: checks the policy, builds the claim pack, calculates the benefit and lodges the claim once a person approves.",
    role_line: "Supports clients and families through every claim.",
    palette: "claimsAdvocate",
    color: "#6366f1",
    personality: "Steady, compassionate and a tenacious advocate.",
    responsibilities: [
      "Checks the definition, waiting period, exclusions and evidence",
      "Builds the claim pack",
      "Calculates the expected benefit and explains it",
      "Raises the Microsoft Teams approval, and lodges only after approval",
      "Tracks the insurer’s Code of Practice timeframes",
    ],
    typical_line: "My job is to make the claim as easy as it can be for you.",
    ambient: [
      "Building a claim pack",
      "Checking a waiting period",
      "Calculating a benefit",
      "Chasing an insurer update",
      "Checking a definition",
    ],
    app_agent_id: "claims",
    agent_name: "Claims Agent",
    agent_tagline: "Builds the claim pack, works out the benefit and lodges it after approval.",
  },
  {
    id: "comms-1",
    name: "Cara",
    role: "Client Communications Specialist",
    team: "Claims & Care Team",
    department: "Client Communications",
    role_short:
      "Keeps clients, families and referral partners informed in plain English, with the right tone for illness, claims, scams and bereavement.",
    role_line: "Keeps clients, families and referral partners informed in plain English.",
    palette: "commsSpecialist",
    color: "#ec4899",
    personality: "Warm, clear and careful with words.",
    responsibilities: [
      "Drafts letters, SoA summaries, underwriting and claim updates, and SMS",
      "Updates referral partners with only what the client agreed to share",
      "Matches the tone for illness, claims, scams and bereavement",
      "Checks every message for required disclosures and IDR and AFCA rights",
      "Takes calls as Cara through the voice agent",
    ],
    typical_line: "Plain English, every time.",
    ambient: [
      "Drafting a welcome letter",
      "Writing a claim update",
      "Updating a referral partner",
      "Returning a client call",
      "Checking the tone of a letter",
    ],
    app_agent_id: "client-communications",
    agent_name: "Client Communications Agent",
    agent_tagline: "Plain-English letters and updates for clients, families and referral partners.",
  },
  {
    id: "lead-1",
    name: "Theo",
    role: "Practice Team Leader",
    team: "Practice Leadership",
    department: "Team Leader Office",
    role_short:
      "Leads both teams from the middle of the floor: workload, service levels, complaint clocks, approvals, file quality and vulnerable clients.",
    role_line: "Leads both teams.",
    palette: "teamLeader",
    color: "#f97316",
    personality: "Calm under pressure and people-first.",
    responsibilities: [
      "Watches the advice pipeline, underwriting and claim clocks, and IDR clocks",
      "Approves blocks, fraud referrals and claim lodgements",
      "Gives every case of $2,000,000 or more a second review",
      "Personally reviews cases for vulnerable clients",
      "Audits files against the best interests duty",
    ],
    typical_line: "Nobody waits without knowing why.",
    ambient: [
      "Reviewing the pipeline",
      "Checking the IDR clocks",
      "Approving a claim lodgement",
      "Auditing an advice file",
      "Coaching the team",
    ],
    app_agent_id: "team-leader",
    agent_name: "Team Leader Agent",
    agent_tagline: "Watches the pipeline, the clocks and the approvals for both teams.",
  },
];

/* ------------------------------------------------------------------------ */
/* Client cast — five Melbourne stories                                      */
/* ------------------------------------------------------------------------ */

export const CLIENT_PERSONAS: ClientPersona[] = [
  // Order matches docs/scenario-N-*.md:
  //   1. family   2. fraud   3. business   4. income   5. bereavement
  {
    id: "family",
    name: "Priya Natarajan",
    situation: "Has just bought a home in Coburg, with a second baby on the way, and wants her family protected.",
    situation_long:
      "Priya (34) is a physiotherapist in Brunswick. She lives in Coburg with her partner Arjun and their daughter Asha (2), and a second baby is due in March 2027. They bought a home in October 2026 with a $680,000 joint mortgage, and their mortgage broker, Hannah Lewis of Yarra Home Loans, referred Priya to Life Advice Guru.",
    need: [
      "Enough cover to clear the mortgage and look after the children",
      "Premiums that fit the budget: up to $300 a month",
      "Her old super cover kept until the new cover starts",
      "Plain answers about her health questions",
    ],
    concern: "She worries about what would happen to Arjun and the children if she couldn’t work.",
    personality: "Organised, busy and practical.",
    typical_line: "We’ve just bought a house and there’s a baby on the way. I want us to be okay, whatever happens.",
    palette: "clientFamily",
    request_type: "New protection advice",
    color: "#3a8fd6",
  },
  {
    id: "fraud",
    name: "Grace Williams",
    situation: "Travelling in Italy, while someone uses her name to claim her trauma cover.",
    situation_long:
      "Grace (61) is a semi-retired school principal from Hawthorn and a Life Advice Guru client since 2019. Her Lyrebird Life policy holds $500,000 of life cover and $200,000 of trauma cover. She told Amelia she would be in Italy until 1 December. While she is away, someone changes her contact details and claims her trauma cover in her name.",
    need: [
      "Her cover kept safe",
      "To be contacted on the number she already trusts",
      "Her portal access locked and her details restored",
      "Clear reassurance about what happens next",
    ],
    concern: "She is overseas and had no idea anything was happening.",
    personality: "Independent and careful, and rattled when she hears.",
    typical_line: "I’m in Florence and I’m perfectly well. I never asked for anything!",
    palette: "clientFraud",
    request_type: "Trauma claim and bank change",
    color: "#d9534f",
  },
  {
    id: "business",
    name: "George Papadopoulos",
    situation: "Co-owns a joinery business with Harpreet, and they have no succession plan.",
    situation_long:
      "George (52) is a master joiner and co-founder of Northcote Custom Joinery Pty Ltd, which he owns 50/50 with Harpreet Kaur (47), its operations and finance director. The business has 14 staff and is valued at $2,400,000. Their accountant, Marcus Tan of Bayside Business Accountants, referred them because they have no succession plan.",
    need: [
      "A way for the surviving partner to buy out the other",
      "Cover for the business if it loses George",
      "Clear numbers they can check with Marcus",
      "Help through the medicals and the paperwork",
    ],
    concern: "He doesn’t want Helen or Harpreet left to sort out the business on their own.",
    personality: "Proud, practical and a little wary of paperwork.",
    typical_line: "We built this from nothing. If something happens to one of us, I don’t want it to fall apart.",
    palette: "clientBusiness",
    request_type: "Business protection advice",
    color: "#e8a33d",
  },
  {
    id: "income",
    name: "Tom Bradley",
    situation: "A self-employed electrician who fell from a ladder and can’t work for months.",
    situation_long:
      "Tom (41) runs Bradley Electrical as a sole trader in Frankston. He lives with his wife Kylie and their two children. On 14 October 2026 he fell from a ladder on a job and tore his rotator cuff, and his surgeon expects four to six months off work. His Harbourline Life income protection policy insures $7,000 a month.",
    need: [
      "Income coming in while he recovers",
      "Kylie able to help as his support person",
      "A clear answer about how much will be paid, and when",
      "Less paperwork while he can’t use his right arm",
    ],
    concern: "The bills don’t stop because he’s injured.",
    personality: "Practical, proud and worried about money.",
    typical_line: "I’ve never missed a month of work. I just need to know the bills will be paid.",
    palette: "clientIncome",
    request_type: "Income protection claim",
    color: "#e07a2c",
  },
  {
    id: "bereavement",
    name: "Robert Chen",
    situation: "His wife Mei died suddenly, and he is claiming on her life cover.",
    situation_long:
      "Robert (44) is a secondary school teacher in Box Hill. His wife Mei, a pharmacist, died suddenly of a brain aneurysm on 12 October 2026, aged 42. They have two children, Lily (12) and Max (9). Mei’s Corella Life policy, advised by Amelia in 2019, holds $1,000,000 of life cover with Robert as her nominated beneficiary.",
    need: [
      "A gentle, clear process",
      "One person to talk to",
      "To know when the money will arrive",
      "Help with anything else Mei had in place",
    ],
    concern: "The paperwork feels overwhelming while the family is grieving.",
    personality: "Quiet, grieving and organised for the children.",
    typical_line: "Mei sorted all of this years ago. I just want to get it right for the kids.",
    palette: "clientBereavement",
    request_type: "Death claim",
    color: "#6e7a8a",
  },
];

// Scripted scenario beats live in src/scenarios/<id>.json
// (see src/scenarios/index.ts for the typed loader).

/* ------------------------------------------------------------------------ */
/* Helpers                                                                  */
/* ------------------------------------------------------------------------ */

export function findStaffByRole(role: StaffRole): StaffPersona {
  const p = STAFF_PERSONAS.find((x) => x.role === role);
  if (!p) throw new Error(`No staff persona for role: ${role}`);
  return p;
}

export function findStaffById(id: string): StaffPersona | undefined {
  return STAFF_PERSONAS.find((x) => x.id === id);
}
