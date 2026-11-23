/**
 * JSON-driven scenario definitions: one `<id>.json` file per scenario in
 * this folder, mirroring `docs/scenario-N-*.md`. Each holds:
 *  - The case facts: case number, headline amounts and outcome.
 *  - Beat list: the narration for every step, in the order the case moves
 *    through the practice, starting with the request at the front desk.
 *  - The result: how the case ends.
 *
 * The Caldova House finale (`v2/requestTrace.ts`) turns each beat into a stop
 * on the request's route through the house, so one JSON edit changes the
 * finale's narration.
 */
import type { ScenarioId, StaffRole } from "../personaData";

import family from "./family.json";
import fraud from "./fraud.json";
import business from "./business.json";
import income from "./income.json";
import bereavement from "./bereavement.json";

/** `rejected` covers declined and blocked cases; `result.status` holds the label. */
export type ScenarioOutcome = "approved" | "rejected" | "partial";

export type BeatAction =
  | "lodge-at-front-desk"
  | "onboarding-pickup"
  | "onboarding-route"
  | "fincrime-review"
  | "paraplanning-needs"
  | "paraplanning-soa"
  | "research-compare"
  | "team-leader-review"
  | "underwriting"
  | "claims-assess"
  | "claims-lodge"
  | "claims-confirm"
  | "comms-notify";

/** Who works the beat: a staff role, or the client making the request. */
export type BeatFocus = StaffRole | "client";

export interface ScenarioBeatJson {
  at: BeatFocus;
  action: BeatAction;
  narration: string;
}

/** Case status label once the outcome is decided. */
export type CaseDecisionStatus = "completed" | "partially approved" | "declined" | "blocked";

/** How the case ends. The finale's last stop shows the icon, headline and summary. */
export interface ScenarioResultJson {
  status: CaseDecisionStatus;
  /** One-line outcome. */
  headline: string;
  /** The headline figure (e.g. "$5,600 a month"). */
  figure: string;
  icon: string;
  /** Outcome line with the full detail. */
  summary: string;
}

export interface ScenarioDefinition {
  id: ScenarioId;
  /** The N in `docs/scenario-N-*.md`. */
  scenarioNumber: number;
  /** Case number, matching the seeded entries in the backend `CaseStore`. */
  caseId: string;
  clientId: ScenarioId;
  /** The headline amount on the case: the cover recommended, claimed or insured. */
  amount: number;
  /** The cover put in force, or the benefit accepted or paid, at the end (0 when blocked). */
  outcomeAmount: number;
  outcome: ScenarioOutcome;
  beats: ScenarioBeatJson[];
  result: ScenarioResultJson;
}

/** Single source of truth for every scenario. */
export const SCENARIOS: Record<ScenarioId, ScenarioDefinition> = {
  family: family as ScenarioDefinition,
  fraud: fraud as ScenarioDefinition,
  business: business as ScenarioDefinition,
  income: income as ScenarioDefinition,
  bereavement: bereavement as ScenarioDefinition,
};

export function getScenario(id: ScenarioId): ScenarioDefinition {
  const s = SCENARIOS[id];
  if (!s) {
    const valid = Object.keys(SCENARIOS).join(", ");
    throw new Error(`Unknown scenario id: ${id}. Valid IDs are: ${valid}`);
  }
  return s;
}
