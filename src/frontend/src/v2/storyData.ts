/**
 * The storybook pages of Caldova House, read from the top of the house down.
 * Keep the text in sync with docs/storybook.md, which is the source of truth.
 */
import type { StackMode } from "./types";

export type PageId = "cover" | "idea" | "people" | "agents" | "engine" | "finale";

export interface CameraShot {
  target: [number, number, number];
  radius: number;
  alpha: number;
  beta: number;
}

export interface StoryPage {
  id: PageId;
  chapter: string;
  /** Lift display label. */
  floor: string;
  kicker: string;
  title: string;
  body: string[];
  hint: string;
  accent: string;
  mode: StackMode;
  shot: CameraShot;
  /** The finale only: where it opens, at the front desk, while the client signs. */
  desk?: CameraShot;
}

export const PAGES: StoryPage[] = [
  {
    id: "cover",
    chapter: "Cover",
    floor: "⌂",
    kicker: "Advice Team in a Day · Caldova House",
    title: "One practice, two sides, four levels",
    body: [
      "Caldova Life Advice is a life insurance advice practice built for the agentic era. It has two sides, Protection Advice and Claims & Care, and runs both from Caldova House, a bank built in 1889 on a Melbourne city corner, with a futuristic office inside.",
      "Read it from the top down: the owner’s idea under the copper dome, two small teams, one group of AI agents in the old banking chamber and, in the old vault, Microsoft IQ and the Azure services that power it all. Out front, clients arrive by tram and sign in at the front desk under the verandah, where Amelia, Caldova’s adviser, gets them started. Up on the roof, Caldova throws a party for them.",
    ],
    hint: "Drag to look around. Press Begin, or take the lift on the right.",
    accent: "#ffc35a",
    mode: { kind: "house" },
    shot: { target: [-9, 7, -14], radius: 104, alpha: -1.0, beta: 1.15 },
  },
  {
    id: "idea",
    chapter: "Chapter 1",
    floor: "3",
    kicker: "Level 3 · The business owner’s office",
    title: "The idea",
    body: [
      "This is Maya Ashworth’s office, under the old copper dome. Maya is Caldova’s managing director and the business owner for both sides: Protection Advice, new advice for families and business owners, and Claims & Care, reviews, changes and claims for existing clients.",
      "Her idea for the agentic era: run each side with a small expert team, supported by one group of AI agents that are dedicated where the work is specialised and shared where it isn’t. Amelia gives the advice, and people approve every decision that matters. The brain under the dome holds the idea, one half for each side.",
    ],
    hint: "Click the brain to see the goals and guardrails, or click Maya to meet her.",
    accent: "#ffc35a",
    mode: { kind: "focus", level: "idea" },
    shot: { target: [-3.5, 15.2, -6.5], radius: 46, alpha: -1.08, beta: 0.86 },
  },
  {
    id: "people",
    chapter: "Chapter 2",
    floor: "2",
    kicker: "Level 2 · Two small teams",
    title: "The people",
    body: [
      "One floor down, inside the old brick walls, the idea becomes two small teams. On the left, the Claims & Care Team: Ben supports clients through their claims, and Cara and Felix look after client communications and financial crime for both sides. On the right, the Protection Advice Team: Penny, Ivy and Uma.",
      "In the middle, every request comes in through Olivia at the front door, and Theo leads both teams from behind her. People talk to clients, families and referral partners, weigh the evidence and make the calls, and both sides follow one lifecycle: Discover, Verify, Advise, Implement, Confirm.",
    ],
    hint: "Click a person to meet them and see what they are doing, then follow them down to their agent.",
    accent: "#ff8f6b",
    mode: { kind: "focus", level: "people" },
    shot: { target: [-1.6, 8.3, -5.4], radius: 52, alpha: -1.06, beta: 0.8 },
  },
  {
    id: "agents",
    chapter: "Chapter 3",
    floor: "1",
    kicker: "Level 1 · One group of agents",
    title: "The agents",
    body: [
      "Under the desks, in the old banking chamber, is one group of Microsoft Foundry agents that supports both teams. Each person has an agent, and each agent has two specialised assistants: 25 roles in all, counting Cara’s voice agent. They move about as they work, between their bays, the Client Case at the old teller counter, the Foundry uplink and the MCP tool bench.",
      "Some agents are dedicated to one side: Paraplanning, Insurance Research and Underwriting to Protection Advice, and Claims to Claims & Care. Onboarding, Financial Crime, Client Communications and the Team Leader agent are shared by both. They hand work to each other on one Client Case, and a person always decides.",
    ],
    hint: "Click an agent or one of its assistants to see what it does and what it is doing now, or follow it down to the engine room.",
    accent: "#5ce1ff",
    mode: { kind: "focus", level: "agents" },
    shot: { target: [-1.6, 1.8, -5.6], radius: 52, alpha: -1.08, beta: 0.78 },
  },
  {
    id: "engine",
    chapter: "Chapter 4",
    floor: "B",
    kicker: "B · Microsoft IQ and Azure in the vault",
    title: "The engine room",
    body: [
      "Down in the old bank vault is the platform. Microsoft Foundry is the core: it runs every agent on the gpt-5.4 model. On the raised deck behind it is Microsoft IQ, the context layer every agent shares: Work IQ, Fabric IQ, Foundry IQ and Web IQ.",
      "Around them are the Azure services. Along the front are the ones particular agents call: MCP tools, Teams approvals, Content Understanding and Voice Live. Behind, among the safe-deposit boxes, Entra ID, Key Vault, Storage, App Service and Application Insights keep it all secure, hosted and observable over HTTPS.",
    ],
    hint: "Click a component to see which agents use it, how it is secured and how the scenarios use it today.",
    accent: "#a78bfa",
    mode: { kind: "focus", level: "engine" },
    shot: { target: [-2.2, -4.4, -4.6], radius: 46, alpha: -1.06, beta: 0.74 },
  },
  {
    id: "finale",
    chapter: "Finale",
    floor: "⇅",
    kicker: "From the front desk, through all four levels",
    title: "One request, top to bottom",
    body: [
      "Now watch a real client request travel through the house. It starts at the front desk: the client arrives by tram and signs under the verandah, where Amelia links them with the agents that get them started and Olivia’s Fact Find Extraction agent comes out to read what they sign.",
      "Then the house pulls apart. Each step starts with a person, dips down to their agent and the engine room, and comes back up for a human decision. New advice runs through the Protection Advice side, claims through Claims & Care, and the shared agents work on every case. The result rises to Maya’s floor.",
    ],
    hint: "Pick a client to replay their request. Pause or skip at any time.",
    accent: "#ffc35a",
    mode: { kind: "exploded" },
    shot: { target: [-4, 17, -7], radius: 122, alpha: -0.98, beta: 1.12 },
    desk: { target: [5.5, 2.2, -17], radius: 34, alpha: -1.2, beta: 1.08 },
  },
];

export function pageIndex(id: PageId): number {
  return PAGES.findIndex((p) => p.id === id);
}

/** A closer look at the party on the roof, with the house together (from the party's card). */
export const PARTY_SHOT: CameraShot = { target: [4, 21, -2.5], radius: 56, alpha: -1.32, beta: 1.0 };
