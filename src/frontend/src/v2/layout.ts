/**
 * World layout for Caldova House. One unit is roughly 0.6 m; the voxel
 * characters stand about three units tall.
 *
 * Cross-section (front view; the street and the tram line run in front of the house):
 *
 *   y 17.8  ── roof · the party, the copper dome, the CALDOVA LIFE ADVICE sign
 *   y 12.3  Level 3 · The Idea           (5 units floor to ceiling)
 *   y  6.8  Level 2 · The People         (5)
 *   y  0.3  Level 1 · The Agents         (6: the old banking chamber) ── footpath · verandah · front desk ── tram
 *   y  0.0  ─── street ──────────────────────────────────────────────────────────────────────────
 *   y -6.0  B · The Engine Room          (the old bank vault)
 *
 * The front of the house (-z) is cut away, dollhouse style, so every level reads from the street.
 */
import type { LevelId } from "./houseData";

export interface Rect {
  x0: number;
  x1: number;
  z0: number;
  z1: number;
}

/** Top of each level's floor slab. */
export const FLOOR_Y: Record<LevelId, number> = {
  engine: -6,
  agents: 0.3,
  people: 6.8,
  idea: 12.3,
};

/** Floor-to-ceiling height of each storey. */
export const STOREY: Record<LevelId, number> = {
  engine: 5.8,
  agents: 6.0,
  people: 5.0,
  idea: 5.0,
};

/** Underside of the roof slab (the top of Level 3). */
export const ROOF_Y = 17.3;
/** Floor slab thickness. */
export const SLAB = 0.5;
/** Thickness of the brick shell. */
export const WALL_T = 0.6;

/** The outside faces of the heritage shell. The front (z0) is the cut. */
export const HOUSE: Rect = { x0: -21, x1: 21, z0: -13, z1: 9 };
/** The floor inside the shell. */
export const INSIDE: Rect = { x0: HOUSE.x0 + WALL_T, x1: HOUSE.x1 - WALL_T, z0: HOUSE.z0, z1: HOUSE.z1 - WALL_T };

/** Lift core, stacked through every level in the back-left corner, doors facing into the room. */
export const CORE: Rect = { x0: INSIDE.x0, x1: -17.2, z0: 4.6, z1: INSIDE.z1 };

/**
 * Desk positions on Level 2, on a grid of five columns and two rows. Each
 * agent bay on Level 1 sits directly underneath its person, so the thread
 * between them runs straight down.
 *
 *   Level 2   |  Claims & Care  | middle |  Protection Advice |
 *   front     |  Ben    Cara    | Olivia |  Penny     Ivy     |
 *   back      |  ·      Felix   | Theo   |  Uma       ·       |
 *   Level 1   | Dedicated |   Shared by both   |   Dedicated   |
 *
 * Cara and Felix sit with the Claims & Care Team, but their agents serve both
 * sides, so their bays are in the shared zone with Olivia's and Theo's.
 */
export const STATIONS: Record<string, { x: number; z: number }> = {
  "claims-1": { x: -15, z: -6 },
  "comms-1": { x: -7.5, z: -6 },
  "fincrime-1": { x: -7.5, z: 3.5 },
  "onboarding-1": { x: 0, z: -6 },
  "lead-1": { x: 0, z: 3.5 },
  "paraplanning-1": { x: 7.5, z: -6 },
  "research-1": { x: 15, z: -6 },
  "underwriting-1": { x: 7.5, z: 3.5 },
};

/** The two cells with no desk: Level 2's quiet room and coffee bar, Level 1's old safe and MCP tool bench. */
export const SPARE_CELLS = { left: { x: -15, z: 3.5 }, right: { x: 15, z: 3.5 } } as const;

/** Level 2 wings, left to right. */
export const WINGS = {
  claims: { x0: INSIDE.x0, x1: -3.75 },
  lead: { x0: -3.75, x1: 3.75 },
  protection: { x0: 3.75, x1: INSIDE.x1 },
} as const;

/** Level 1 zones, left to right: one group of agents, dedicated to a side or shared by both. */
export const ZONES = {
  claims: { x0: INSIDE.x0, x1: -11.25 },
  shared: { x0: -11.25, x1: 3.75 },
  protection: { x0: 3.75, x1: INSIDE.x1 },
} as const;

/** The old teller counter at the front of the banking chamber: the Client Case hub. */
export const HUB = { x: 0, z: -10.3 };

/**
 * The copper dome over Maya's office. It stands on a drum on top of Level 3, centred on the
 * front of the house, so the dollhouse cut takes off its front half and the brain floats in it.
 */
export const DOME = { x: -8, z: HOUSE.z0, r: 5.8, drumY: 17.3, drumH: 1.4 };

/* ---------------- The street ---------------- */

/** Footpath in front of the house, from the building line to the bluestone kerb. */
export const KERB_Z = -17.6;
/** The road, with the tram line down the middle. */
export const ROAD = { z0: -29.4, z1: KERB_Z };
/** Centre lines of the two tram tracks. */
export const TRACK_Z = { near: -20.4, far: -25.6 };
/** Where the tram stops: its door lines up with the platform. */
export const TRAM_STOP_X = 0;

/** The front desk under the verandah, where visitors sign in. */
export const FRONT_DESK = { x: 9.5, z: -15.3 };
/** The cast-iron verandah over the footpath at the entrance. */
export const VERANDAH = { x0: 4.4, x1: 15.6, z0: -17.2, z1: HOUSE.z0, y: 4.7 };

/** The floating city block: its extent and the bottom of its earth. */
export const BLOCK = { x0: -48, x1: 48, z0: -34, z1: 30, bottom: -12 };
/** The bluestone laneway down the left side of the house. */
export const LANE = { x0: -27.6, x1: HOUSE.x0 };
/** The cross street on the right, at the corner. */
export const CROSS = { x0: 24.4, x1: 30.4 };

/** Characters are drawn larger than life so they read from the orbit camera. */
export const CHARACTER_SCALE = 1.5;

/** Facing angle (rotation.y) for a voxel character so it looks along (dx, dz). */
export function faceAngle(dx: number, dz: number): number {
  return Math.atan2(dx, dz);
}
