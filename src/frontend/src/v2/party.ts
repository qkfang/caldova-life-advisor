/**
 * The roof: Life Advice Guru's party for its clients.
 *
 * A timber party deck round the copper dome, under a big LIFE ADVICE GURU
 * marquee sign along the back. Clients, their families and the referral
 * partners who sent them dance to the DJ, get a drink at the bar, try the
 * welcome cake, take in the city and peek down at the brain through the
 * dome's cut. Every so often the DJ calls a toast (confetti over the dance
 * floor), and fireworks go up behind the sign. Guests come and go by tram
 * and the glass lift on the front-left corner (partyTrips.ts). A storybook
 * touch: see "The roof" in docs/storybook.md.
 */
import { type AbstractMesh, Color3, type Mesh, Quaternion, type StandardMaterial, TransformNode, Vector3 } from "@babylonjs/core";
import type { ScenarioId } from "../personaData";
import { VoxelCharacter } from "../voxelCharacter";
import { PARTY, PARTY_DJ_PALETTE, PARTY_GUESTS, PARTY_SIGN, SIDES, type PartyGuest } from "./houseData";
import { Batch, type Bubble, type Kit, rng } from "./kit";
import { CHARACTER_SCALE, DOME, HOUSE, INSIDE, LIFT, ROOF_Y, SLAB } from "./layout";
import type { Highlights } from "./types";
import { SpotBook, Wanderer, type Outing, type Pt, type Spot } from "./wander";

/** Top of the roof slab, where the deck and the sign's legs stand. */
const SLAB_TOP = ROOF_Y + SLAB;
/** The deck, where everyone stands. */
const FLOOR = SLAB_TOP + 0.18;
/** The walkway across the party, behind the dome and in front of the dance floor. */
const CZ = -4.0;
const BPM = 120;
const GOLD = Color3.FromHexString("#ffc35a");
/** The sides' colours and the levels' accents. */
const PARTY_COLOURS = ["#ffc35a", "#14b8a6", "#8b9cf7", "#ff8f6b", "#5ce1ff"];

/** A 5 × 7 voxel font for the sign, top row first. */
const FONT: Record<string, string[]> = {
  C: [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
  A: [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
  L: ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
  D: ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
  O: [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
  V: ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
  I: ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
  F: ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
  E: ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
  G: [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
  U: ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
  R: ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
};
/** The sign: voxel size, centre, bottom edge (on legs above the deck) and depth position. */
const SIGN = { v: 0.29, x: 4.2, y: SLAB_TOP + 3.4, z: INSIDE.z1 - 0.5, margin: 0.6 };

/* ---------------- Where the guests go ---------------- */

/** A spot reached straight off the walkway (or by `via`, walkway first). */
const at = (x: number, z: number, face: number, key?: string, via?: Pt[]): Spot => ({ at: [x, z], face, via: via ?? [[x, CZ]], key });
const towards = (x: number, z: number, tx: number, tz: number): number => Math.atan2(tx - x, tz - z);

const DANCE_FLOOR = { x: 4.2, z: 0.9, cols: 6, rows: 5, tile: 0.95 };
const DJ_AT = { x: 4.2, z: 6.0 };
const TABLES = [
  { x: 2.0, z: -6.6 },
  { x: 12.0, z: -6.6 },
];
const CAKE_AT = { x: -10.4, z: -1.2 };
/** The lounge sits back from the front-left corner, clear of the way in from the glass lift. */
const LOUNGE = { x: -16.0, z: -7.8 };
const BAR_AT = { x: 16.4, z: -1.2 };
/** The way down the left side of the deck to the glass lift's landing on the roof, past the lounge. */
const LIFT_LANE = LIFT.x - 0.8;

/** Front row faces the DJ, back row faces the front row. */
const DANCE: Spot[] = [
  at(2.05, -0.9, 0, "dance:0"),
  at(4.2, -1.1, 0, "dance:1"),
  at(6.35, -0.9, 0, "dance:2"),
  at(3.15, 1.4, Math.PI, "dance:3"),
  at(5.25, 1.4, Math.PI, "dance:4"),
];
const BAR: Spot[] = [at(15.8, -2.4, 0, "bar:0"), at(17.0, -2.4, 0, "bar:1")];
const CAKE: Spot[] = [
  at(CAKE_AT.x, -2.5, 0, "cake:0"),
  at(-9.0, -2.2, towards(-9.0, -2.2, CAKE_AT.x, CAKE_AT.z), "cake:1"),
];
const VIEW: Spot[] = [
  at(INSIDE.x1 - 1.0, -9.0, Math.PI / 2, "view:0"),
  at(INSIDE.x1 - 1.0, 2.2, Math.PI / 2, "view:1"),
  // At the front parapet beside the lift's bridge, down the lane and round in front of the lounge.
  at(-15.4, -11.9, Math.PI, "view:2", [
    [LIFT_LANE, CZ],
    [LIFT_LANE, -10.9],
    [-15.4, -10.9],
  ]),
];
/** At the gallery rail round the dome's cut, looking down at the brain. */
const DOME_PEEK: Spot[] = [40, 140].map((deg, i) => {
  const a = (deg * Math.PI) / 180;
  const x = DOME.x + Math.cos(a) * (DOME.r + 1.1);
  const z = DOME.z + Math.sin(a) * (DOME.r + 1.1);
  return at(x, z, towards(x, z, DOME.x, DOME.z), `peek:${i}`);
});

/** Each guest's home spot: a cocktail table with their party, or the lounge. */
const HOMES: Record<string, [Spot, string]> = {
  priya: [at(TABLES[0].x - 0.85, TABLES[0].z, Math.PI / 2, "home:priya"), "their table"],
  hannah: [at(TABLES[0].x + 0.85, TABLES[0].z, -Math.PI / 2, "home:hannah"), "their table"],
  arjun: [at(TABLES[0].x, TABLES[0].z + 0.85, Math.PI, "home:arjun"), "their table"],
  asha: [at(TABLES[0].x - 0.5, TABLES[0].z - 1.0, 0.4, "home:asha"), "their table"],
  george: [at(TABLES[1].x - 0.85, TABLES[1].z, Math.PI / 2, "home:george"), "their table"],
  marcus: [at(TABLES[1].x + 0.85, TABLES[1].z, -Math.PI / 2, "home:marcus"), "their table"],
  helen: [at(TABLES[1].x, TABLES[1].z + 0.85, Math.PI, "home:helen"), "their table"],
  harpreet: [at(TABLES[1].x + 0.2, TABLES[1].z - 0.9, 0, "home:harpreet"), "their table"],
  backatwork: [
    at(LOUNGE.x + 1.9, LOUNGE.z - 1.0, towards(LOUNGE.x + 1.9, LOUNGE.z - 1.0, LOUNGE.x, LOUNGE.z), "home:backatwork", [
      [LOUNGE.x + 2.6, CZ],
      [LOUNGE.x + 2.6, LOUNGE.z - 1.0],
    ]),
    "the lounge",
  ],
  "retired-1": [
    at(LOUNGE.x - 1.2, LOUNGE.z - 1.3, towards(LOUNGE.x - 1.2, LOUNGE.z - 1.3, LOUNGE.x, LOUNGE.z), "home:retired-1", [
      [LOUNGE.x - 2.9, CZ],
      [LOUNGE.x - 2.9, LOUNGE.z - 1.3],
    ]),
    "the lounge",
  ],
  "retired-2": [at(LOUNGE.x - 1.4, LOUNGE.z + 1.2, towards(LOUNGE.x - 1.4, LOUNGE.z + 1.2, LOUNGE.x, LOUNGE.z), "home:retired-2"), "the lounge"],
};

const DJ_LINES = ["Next one’s a request!", "Make some noise!", "Hands up for Life Advice Guru!", "This one’s for our clients!"];

/** A guest's body, for their trips down to the tram and back up (partyTrips.ts). */
export interface GuestBody {
  id: string;
  name: string;
  /** Moves the guest about; it rides in the lift's car. */
  node: TransformNode;
  ch: VoxelCharacter;
  bubble: Bubble;
}

/** How the party lets guests go home by tram and the glass lift, and come back. */
export interface PartyTravel {
  /** These guests are at the party, at their spot and free to leave together. */
  free(ids: readonly string[]): boolean;
  /** Send them to the lift's landing on the roof, one spot each, saying goodbye. */
  goToLift(ids: readonly string[], spots: readonly Pt[], line: string): void;
  /** They are all standing at their spots on the landing. */
  atLift(ids: readonly string[]): boolean;
  /** Off the roof and on their way: the party stops moving them, and their card shows `status`. */
  take(id: string, status: string): GuestBody;
  /** What a guest on a trip is doing, for their card. */
  setStatus(id: string, status: string): void;
  /** Back on the roof, stepping out of the lift at `at`: they walk back to their spot. */
  giveBack(id: string, at: Pt): void;
  /** Straight back to their spot, without the walk. */
  snapBack(id: string): void;
}

export interface Party {
  readonly root: TransformNode;
  update(dt: number): void;
  setHighlights(h: Highlights): void;
  /** The finale's client is down at the front desk, so they are not up here as well. */
  setAway(id: ScenarioId | null): void;
  /** Keep this finale client's guests off the roof until `comeBack` (they are riding the lift back up). */
  holdAway(id: ScenarioId): void;
  /** They are back: stepping out of the lift at `at` (the client first), or straight back at their spot. */
  comeBack(id: ScenarioId, at?: ReadonlyArray<Pt>): void;
  /** Guests who go home by tram and the glass lift, and come back (partyTrips.ts). */
  readonly travel: PartyTravel;
  /** What the party is up to right now, for the card. */
  now(): string;
  /** What a guest is doing right now, for their card. */
  guestTask(id: string): string | null;
}

interface Guest {
  info: PartyGuest;
  mover: TransformNode;
  ch: VoxelCharacter;
  w: Wanderer;
  bubble: Bubble;
  /** Keeps the drink upright whatever the arm is doing. */
  glass: TransformNode;
  /** The shoulder the drink hangs from. */
  shoulder: TransformNode;
  style: number;
  phase: number;
  away: boolean;
  /** On a trip down to the tram and back: what they are doing, for their card. */
  trip: string | null;
  /** Their spot on the lift's landing while they wait to go down. */
  liftSpot: Spot | null;
  left: number;
  right: number;
}

interface Burst {
  rocket: Mesh;
  sparks: Mesh[];
  dirs: Vector3[];
  mat: StandardMaterial;
  state: "idle" | "rising" | "bursting";
  t: number;
  peak: number;
  centre: Vector3;
}

interface Confetti {
  m: Mesh;
  v: Vector3;
  spin: Vector3;
  rest: number;
}

export function buildParty(kit: Kit, roof: TransformNode): Party {
  const scene = kit.scene;
  const root = new TransformNode("v2_party", scene);
  root.parent = roof;
  const r = rng(77);
  const pick = <T>(a: readonly T[]): T => a[Math.floor(r() * a.length)];

  const batch = new Batch(root, "party");
  const prev = kit.useBatch(batch);
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  const oak = kit.mat("#ffffff", { texture: "oak" });
  const white = kit.mat("#f1eee6", { specular: 0.3 });

  /* ---------------- The deck ---------------- */
  // Timber decking over the roof, round the dome's cut.
  const notch = DOME.r + 0.6;
  for (const [x0, x1] of [
    [INSIDE.x0, DOME.x - notch],
    [DOME.x + notch, INSIDE.x1],
  ] as const) {
    kit.block(root, (x0 + x1) / 2, SLAB_TOP, (HOUSE.z0 + 0.6 + INSIDE.z1) / 2, x1 - x0, FLOOR - SLAB_TOP, INSIDE.z1 - HOUSE.z0 - 0.6, oak);
  }
  for (let x = DOME.x - notch; x < DOME.x + notch - 0.01; x += 0.5) {
    const mid = x + 0.25 - DOME.x;
    const z0 = Math.max(HOUSE.z0 + 0.6, DOME.z + Math.sqrt(Math.max(0, notch * notch - mid * mid)));
    kit.block(root, x + 0.25, SLAB_TOP, (z0 + INSIDE.z1) / 2, 0.5, FLOOR - SLAB_TOP, INSIDE.z1 - z0, oak);
  }

  /* ---------------- The sign ---------------- */
  // Glowing voxel letters on a navy board, framed by chasing marquee bulbs.
  const cells: Array<[number, number]> = [];
  let cursor = 0;
  for (const ch of PARTY_SIGN) {
    const rows = FONT[ch];
    if (!rows) {
      cursor += 2;
      continue;
    }
    rows.forEach((row, ri) => [...row].forEach((c, ci) => c === "#" && cells.push([cursor + ci, 6 - ri])));
    cursor += 6;
  }
  const v = SIGN.v;
  const textW = (cursor - 1) * v;
  const boardW = textW + SIGN.margin * 2;
  const boardH = 7 * v + 1.0;
  const boardFront = SIGN.z - 0.12;
  kit.block(root, SIGN.x, SIGN.y, SIGN.z, boardW, boardH, 0.24, kit.mat("#10213d", { specular: 0.2 }));
  kit.block(root, SIGN.x, SIGN.y + boardH, SIGN.z, boardW + 0.12, 0.1, 0.32, steel);
  const letters = kit.mat("#ffd36b", { unlit: true });
  const lx0 = SIGN.x - textW / 2;
  for (const [cx, cy] of cells) {
    kit.block(root, lx0 + (cx + 0.5) * v, SIGN.y + 0.5 + cy * v + v * 0.05, boardFront - 0.08, v * 0.9, v * 0.9, 0.16, letters);
  }
  // Marquee bulbs round the edge, in two sets that take turns.
  const bulbA = kit.mat("#fff1c2", { unlit: true, unique: true });
  const bulbB = kit.mat("#fff1c2", { unlit: true, unique: true });
  const bulbAt: Array<[number, number]> = [];
  const left = SIGN.x - boardW / 2 + 0.24;
  const right = SIGN.x + boardW / 2 - 0.24;
  const bottom = SIGN.y + 0.22;
  const top = SIGN.y + boardH - 0.22;
  const nx = Math.round((right - left) / 0.45);
  const ny = Math.round((top - bottom) / 0.45);
  for (let i = 0; i < nx; i++) bulbAt.push([left + ((right - left) * i) / nx, top]);
  for (let i = 0; i < ny; i++) bulbAt.push([right, top - ((top - bottom) * i) / ny]);
  for (let i = 0; i < nx; i++) bulbAt.push([right - ((right - left) * i) / nx, bottom]);
  for (let i = 0; i < ny; i++) bulbAt.push([left, bottom + ((top - bottom) * i) / ny]);
  bulbAt.forEach(([bx, by], i) => kit.block(root, bx, by - 0.075, boardFront - 0.06, 0.15, 0.15, 0.12, i % 2 ? bulbB : bulbA));
  // Steel legs with cross-bracing.
  const legs = [-10.6, -5.3, 0, 5.3, 10.6].map((dx) => SIGN.x + dx);
  for (const x of legs) kit.block(root, x, SLAB_TOP, SIGN.z + 0.1, 0.22, SIGN.y - SLAB_TOP, 0.22, steel);
  const beam = (ax: number, ay: number, az: number, bx: number, by: number, bz: number, t: number, mat: StandardMaterial): void => {
    const dx = bx - ax;
    const dy = by - ay;
    const dz = bz - az;
    const len = Math.hypot(dx, dy, dz);
    const m = kit.block(root, (ax + bx) / 2, (ay + by) / 2 - t / 2, (az + bz) / 2, t, t, len, mat);
    m.rotation.y = Math.atan2(dx, dz);
    m.rotation.x = -Math.asin(dy / len);
  };
  for (let i = 0; i < legs.length - 1; i++) {
    beam(legs[i], SLAB_TOP + 0.4, SIGN.z + 0.12, legs[i + 1], SIGN.y - 0.3, SIGN.z + 0.12, 0.07, steel);
    beam(legs[i + 1], SLAB_TOP + 0.4, SIGN.z + 0.12, legs[i], SIGN.y - 0.3, SIGN.z + 0.12, 0.07, steel);
  }
  kit.hitbox(root, SIGN.x, SIGN.y, SIGN.z - 0.1, boardW, boardH, 0.6, { type: "party" });

  /* ---------------- String lights and bunting ---------------- */
  const POSTS = [-0.6, 4.4, 9.4, 14.4, 19.6];
  const POST_Z = HOUSE.z0 + 1.6;
  const POST_TOP = SIGN.y - 0.2;
  for (const x of POSTS) kit.block(root, x, SLAB_TOP, POST_Z, 0.14, POST_TOP - SLAB_TOP, 0.14, steel);
  const lights = kit.mat("#ffe6a8", { unlit: true, unique: true });
  const strand = (x0: number, z0: number, y0: number, x1: number, z1: number, y1: number, sag = 0.55): void => {
    const len = Math.hypot(x1 - x0, z1 - z0);
    const n = Math.max(2, Math.round(len / 0.75));
    for (let i = 1; i < n; i++) {
      const f = i / n;
      const yy = y0 + (y1 - y0) * f - Math.sin(Math.PI * f) * sag;
      kit.block(root, x0 + (x1 - x0) * f, yy - 0.16, z0 + (z1 - z0) * f, 0.16, 0.16, 0.16, lights);
    }
  };
  // From the front posts back to the bottom of the sign, and along the front.
  for (const x of POSTS.slice(0, 4)) strand(x, POST_Z, POST_TOP - 0.1, Math.min(x + 1, SIGN.x + boardW / 2 - 0.5), boardFront - 0.05, SIGN.y);
  for (let i = 0; i < POSTS.length - 1; i++) strand(POSTS[i], POST_Z, POST_TOP - 0.1, POSTS[i + 1], POST_Z, POST_TOP - 0.1, 0.4);
  // Bunting in the party colours along the front.
  const flags = PARTY_COLOURS.map((hex) => kit.mat(hex));
  const cord = kit.mat("#e5e7eb");
  let flag = 0;
  const flagY = POST_TOP - 0.9;
  for (let i = 0; i < POSTS.length - 1; i++) {
    const a = POSTS[i];
    const b = POSTS[i + 1];
    const n = Math.max(2, Math.round((b - a) / 0.6));
    let px = a;
    let py = flagY;
    for (let j = 1; j <= n; j++) {
      const f = j / n;
      const x = a + (b - a) * f;
      const yy = flagY - Math.sin(Math.PI * f) * 0.25;
      beam(px, py, POST_Z, x, yy, POST_Z, 0.03, cord);
      if (j < n) {
        const mm = flags[flag++ % flags.length];
        kit.block(root, x, yy - 0.2, POST_Z, 0.34, 0.18, 0.03, mm);
        kit.block(root, x, yy - 0.36, POST_Z, 0.16, 0.16, 0.03, mm);
      }
      px = x;
      py = yy;
    }
  }

  /* ---------------- The dance floor ---------------- */
  const df = DANCE_FLOOR;
  kit.block(root, df.x, FLOOR, df.z, df.cols * df.tile + 0.2, 0.03, df.rows * df.tile + 0.2, kit.mat("#0b1220"));
  const tileMats = PARTY_COLOURS.map((hex) => kit.mat(hex, { unlit: true, unique: true }));
  for (let i = 0; i < df.cols; i++) {
    for (let j = 0; j < df.rows; j++) {
      const x = df.x + (i - (df.cols - 1) / 2) * df.tile;
      const z = df.z + (j - (df.rows - 1) / 2) * df.tile;
      kit.block(root, x, FLOOR, z, df.tile - 0.06, 0.05, df.tile - 0.06, tileMats[(i + j) % tileMats.length]);
    }
  }
  kit.hitbox(root, df.x, FLOOR, df.z, df.cols * df.tile, 0.3, df.rows * df.tile, { type: "party" });
  // Confetti cannons at the front corners, aimed in over the floor.
  const cannons = [
    { x: df.x - (df.cols * df.tile) / 2 - 0.35, z: df.z - (df.rows * df.tile) / 2 - 0.2, tilt: -0.45 },
    { x: df.x + (df.cols * df.tile) / 2 + 0.35, z: df.z - (df.rows * df.tile) / 2 - 0.2, tilt: 0.45 },
  ];
  for (const c of cannons) {
    kit.block(root, c.x, FLOOR, c.z, 0.4, 0.2, 0.4, steel);
    kit.cyl(root, c.x, FLOOR + 0.2, c.z, 0.26, 0.7, kit.mat("#c9a14a", { specular: 0.6 }), { tess: 10 }).rotation.z = c.tilt;
  }

  /* ---------------- The DJ booth ---------------- */
  const dark = kit.mat("#1f2937");
  const black = kit.mat("#111827");
  const boothZ = DJ_AT.z - 1.15;
  kit.block(root, DJ_AT.x, FLOOR, boothZ, 3.6, 1.05, 1.0, dark);
  kit.block(root, DJ_AT.x, FLOOR + 1.05, boothZ, 3.7, 0.06, 1.08, black);
  kit.block(root, DJ_AT.x, FLOOR + 0.92, boothZ - 0.51, 3.6, 0.06, 0.02, kit.mat("#5ce1ff", { unlit: true }));
  kit.block(root, DJ_AT.x, FLOOR + 1.11, boothZ - 0.05, 0.62, 0.08, 0.5, kit.mat("#374151"));
  for (const [i, hex] of ["#ffc35a", "#14b8a6", "#ff8f6b"].entries()) {
    kit.block(root, DJ_AT.x - 0.18 + i * 0.18, FLOOR + 1.19, boothZ - 0.18, 0.08, 0.05, 0.08, kit.mat(hex, { unlit: true }));
  }
  const decks: Mesh[] = [];
  for (const dx of [-0.9, 0.9]) {
    const deck = kit.cyl(root, DJ_AT.x + dx, FLOOR + 1.11, boothZ - 0.05, 0.66, 0.05, black, { tess: 18, dynamic: true });
    kit.block(deck, 0.2, 0.025, 0, 0.12, 0.02, 0.08, kit.mat("#ffc35a"), { dynamic: true });
    decks.push(deck);
  }
  const tagline = kit.label(root, {
    lines: [{ text: PARTY.tagline, size: 30, weight: 700, color: "#0f1a2e" }],
    width: 3.3,
    px: 820,
    bg: "rgba(255,251,242,0.96)",
    accent: "#c9a14a",
    bar: "top",
  });
  tagline.position.set(DJ_AT.x, FLOOR + 0.48, boothZ - 0.53);
  // Speakers either side, with a woofer that kicks on the beat.
  const woofers: Mesh[] = [];
  for (const dx of [-2.5, 2.5]) {
    const sx = DJ_AT.x + dx;
    kit.block(root, sx, FLOOR, boothZ, 0.9, 1.9, 0.8, black);
    kit.block(root, sx, FLOOR + 1.4, boothZ - 0.42, 0.3, 0.3, 0.04, dark);
    woofers.push(kit.block(root, sx, FLOOR + 0.32, boothZ - 0.42, 0.6, 0.6, 0.05, kit.mat("#374151"), { dynamic: true }));
  }
  // A lighting truss with four coloured lamps.
  const trussY = SIGN.y - 1.1;
  for (const dx of [-3.4, 3.4]) kit.block(root, DJ_AT.x + dx, FLOOR, DJ_AT.z - 0.15, 0.16, trussY - FLOOR, 0.16, steel);
  kit.block(root, DJ_AT.x, trussY, DJ_AT.z - 0.15, 6.96, 0.16, 0.16, steel);
  const lampMats = PARTY_COLOURS.slice(0, 4).map((hex) => kit.mat(hex, { unlit: true, unique: true }));
  lampMats.forEach((m, i) => {
    kit.block(root, DJ_AT.x - 1.8 + i * 1.2, trussY, DJ_AT.z - 0.15, 0.06, 0.12, 0.06, steel);
    const lamp = kit.block(root, DJ_AT.x - 1.8 + i * 1.2, trussY - 0.4, DJ_AT.z - 0.15, 0.28, 0.34, 0.28, m, { dynamic: true });
    lamp.rotation.x = 0.45;
  });
  kit.hitbox(root, DJ_AT.x, FLOOR, boothZ + 0.1, 6.2, 2.0, 1.4, { type: "party" });

  /* ---------------- The bar, the tables, the cake and the lounge ---------------- */
  const walnut = kit.mat("#ffffff", { texture: "walnut" });
  kit.block(root, BAR_AT.x, FLOOR, BAR_AT.z, 2.4, 1.15, 0.8, walnut);
  kit.block(root, BAR_AT.x, FLOOR + 1.15, BAR_AT.z, 2.56, 0.07, 0.94, white);
  kit.block(root, BAR_AT.x, FLOOR + 0.95, BAR_AT.z - 0.41, 2.4, 0.05, 0.02, kit.mat("#c9a14a", { unlit: true }));
  for (const [i, hex] of ["#14532d", "#b45309", "#cbd5e1", "#14532d", "#b45309"].entries()) {
    kit.cyl(root, BAR_AT.x - 0.9 + i * 0.32, FLOOR + 1.22, BAR_AT.z + 0.18, 0.14, 0.44, kit.mat(hex, { specular: 0.6 }), { tess: 8 });
  }
  kit.cyl(root, BAR_AT.x + 0.85, FLOOR + 1.22, BAR_AT.z - 0.1, 0.38, 0.3, kit.mat("#cbd5e1", { specular: 0.8 }), { tess: 10 });
  const flute = kit.mat("#f6e7b0", { specular: 0.6 });
  for (let i = 0; i < 4; i++) kit.block(root, BAR_AT.x - 0.7 + i * 0.3, FLOOR + 1.22, BAR_AT.z - 0.22, 0.1, 0.22, 0.1, flute);
  const barSign = kit.label(root, {
    lines: [{ text: "BAR", size: 64, weight: 800, color: "#ffd36b" }],
    width: 0.9,
    px: 256,
    pxHeight: 96,
    bg: "rgba(16,33,61,0.95)",
  });
  barSign.position.set(BAR_AT.x, FLOOR + 0.6, BAR_AT.z - 0.42);
  kit.hitbox(root, BAR_AT.x, FLOOR, BAR_AT.z, 2.6, 1.4, 1.0, { type: "party" });
  for (const t of TABLES) {
    kit.cyl(root, t.x, FLOOR, t.z, 0.5, 0.04, steel, { tess: 12 });
    kit.cyl(root, t.x, FLOOR, t.z, 0.12, 1.0, steel, { tess: 8 });
    kit.cyl(root, t.x, FLOOR + 1.0, t.z, 0.9, 0.05, white, { tess: 14 });
    kit.block(root, t.x - 0.15, FLOOR + 1.05, t.z + 0.1, 0.1, 0.22, 0.1, flute);
  }
  // The welcome cake.
  kit.cyl(root, CAKE_AT.x, FLOOR, CAKE_AT.z, 1.4, 0.75, kit.mat("#f8fafc"), { tess: 16 });
  for (const [d, h, yy, hex] of [
    [0.9, 0.28, 0.75, "#fff7ed"],
    [0.66, 0.25, 1.03, "#99f6e4"],
    [0.42, 0.22, 1.28, "#fff7ed"],
  ] as const) {
    kit.cyl(root, CAKE_AT.x, FLOOR + yy, CAKE_AT.z, d, h, kit.mat(hex), { tess: 14 });
  }
  for (const dx of [-0.1, 0, 0.1]) {
    kit.block(root, CAKE_AT.x + dx, FLOOR + 1.5, CAKE_AT.z, 0.04, 0.16, 0.04, kit.mat("#f8fafc"));
    kit.block(root, CAKE_AT.x + dx, FLOOR + 1.66, CAKE_AT.z, 0.05, 0.07, 0.05, kit.mat("#ffb347", { unlit: true }));
  }
  const cakeSign = kit.label(root, {
    lines: [
      { text: "Welcome to the", size: 34, weight: 600, color: "#3b4557" },
      { text: "Life Advice Guru family", size: 40, weight: 700, color: "#0f1a2e" },
    ],
    width: 1.9,
    px: 680,
    bg: "rgba(255,251,242,0.96)",
    accent: "#c9a14a",
    bar: "top",
  });
  cakeSign.position.set(CAKE_AT.x, FLOOR + 0.45, CAKE_AT.z - 0.72);
  kit.hitbox(root, CAKE_AT.x, FLOOR, CAKE_AT.z, 1.6, 1.8, 1.6, { type: "party" });
  // The lounge: a low table and three beanbags, by the front parapet.
  kit.cyl(root, LOUNGE.x, FLOOR, LOUNGE.z, 1.2, 0.42, walnut, { tess: 14 });
  for (const [x, z, hex] of [
    [LOUNGE.x - 1.4, LOUNGE.z - 0.1, "#ff8f6b"],
    [LOUNGE.x + 1.4, LOUNGE.z + 0.2, "#14b8a6"],
    [LOUNGE.x, LOUNGE.z + 1.4, "#ffc35a"],
  ] as const) {
    const bag = kit.mat(hex);
    kit.block(root, x, FLOOR, z, 1.0, 0.42, 1.0, bag);
    kit.block(root, x, FLOOR + 0.42, z + 0.05, 0.8, 0.18, 0.8, bag);
  }
  kit.hitbox(root, LOUNGE.x, FLOOR, LOUNGE.z + 0.3, 4.0, 1.0, 3.0, { type: "party" });
  // Planters along the back of the deck.
  for (const x of [-4.6, 18.6]) {
    kit.block(root, x, FLOOR, INSIDE.z1 - 0.7, 1.2, 0.6, 0.6, kit.mat("#d8d2c6"));
    kit.block(root, x, FLOOR + 0.6, INSIDE.z1 - 0.7, 1.0, 0.4, 0.45, kit.mat("#5ea347"));
  }
  kit.useBatch(prev);
  batch.flush();

  /* ---------------- Balloons ---------------- */
  // Each bunch is merged by colour while its node is at the origin, then moved into place.
  const bunches: TransformNode[] = [];
  for (const [i, [x, z]] of [
    [-1.2, -2.6],
    [9.6, 2.6],
    [-20.0, -2.2],
    [19.6, -11.8],
  ].entries()) {
    const node = new TransformNode("v2_balloons", scene);
    node.parent = root;
    const bb = new Batch(node, "balloons");
    const p = kit.useBatch(bb);
    kit.block(node, 0, 0, 0, 0.3, 0.2, 0.3, steel);
    const colours = [PARTY_COLOURS[i % 5], PARTY_COLOURS[(i + 1) % 5], PARTY_COLOURS[(i + 2) % 5]];
    colours.forEach((hex, k) => {
      const a = (k / colours.length) * Math.PI * 2 + i;
      const bx = Math.cos(a) * 0.34;
      const bz = Math.sin(a) * 0.34;
      const by = 2.3 + (k % 2) * 0.4;
      kit.block(node, bx * 0.5, 0.2, bz * 0.5, 0.03, by - 0.2, 0.03, kit.mat("#e5e7eb"));
      kit.block(node, bx, by, bz, 0.62, 0.72, 0.62, kit.mat(hex, { specular: 0.6 }));
    });
    kit.useBatch(p);
    bb.flush();
    node.position.set(x, FLOOR, z);
    bunches.push(node);
  }

  /* ---------------- The guests and the DJ ---------------- */
  const book = new SpotBook();
  const freeOf = (spots: Spot[]): Spot | null => {
    const free = spots.filter((s) => book.free(s.key));
    return free.length ? pick(free) : null;
  };
  const go = (spot: Spot | null, task: string, where: string, dwell: [number, number]): Outing | null =>
    spot ? { spot, task, where, dwell: dwell[0] + (dwell[1] - dwell[0]) * r() } : null;
  const guests = new Map<string, Guest>();
  const champagne = kit.mat("#f6e7b0", { specular: 0.6 });
  PARTY_GUESTS.forEach((info, i) => {
    const [home, homeName] = HOMES[info.id];
    const mover = new TransformNode(`v2_guest_${info.id}`, scene);
    mover.parent = root;
    mover.position.y = FLOOR;
    const scale = info.child ? CHARACTER_SCALE * 0.6 : CHARACTER_SCALE;
    const ch = new VoxelCharacter(scene, `guest:${info.id}`, info.palette);
    ch.root.parent = mover;
    ch.root.scaling.setAll(scale);
    const glass = new TransformNode(`v2_glass_${info.id}`, scene);
    glass.parent = ch.getHandAnchor();
    glass.rotationQuaternion = new Quaternion();
    if (!info.child) kit.block(glass, 0, -0.04, 0.1, 0.13, 0.24, 0.13, champagne, { dynamic: true });
    const bubble = kit.bubble(ch.root, 2.45, info.side ? SIDES[info.side].color : "#c9a14a", scale);
    const choices: Array<[number, () => Outing | null]> = info.child
      ? [
          [3.5, () => go(freeOf(DANCE), pick(["Dancing", "Twirling round and round"]), "the dance floor", [10, 18])],
          [1.0, () => go(freeOf(CAKE), "Looking at the cake", "the cake table", [4, 7])],
        ]
      : [
          [3.2, () => go(freeOf(DANCE), pick(["Dancing", "Dancing to the DJ", "Showing off some moves"]), "the dance floor", [12, 22])],
          [1.8, () => go(freeOf(BAR), pick(["Grabbing a drink", "Topping up the bubbles", "Ordering a mocktail"]), "the bar", [5, 9])],
          [1.0, () => go(freeOf(CAKE), pick(["Having some cake", "Trying the welcome cake"]), "the cake table", [6, 10])],
          [1.0, () => go(freeOf(VIEW), pick(["Watching the trams", "Taking in the city"]), "the parapet", [6, 10])],
          [0.6, () => go(freeOf(DOME_PEEK), "Peeking down at the brain", "the dome", [5, 8])],
        ];
    const total = choices.reduce((s, [w]) => s + w, 0);
    const w = new Wanderer({
      body: {
        root: mover,
        walk: (on) => {
          ch.setWalking(on);
          if (on) {
            ch.root.position.set(0, 0, 0);
            ch.root.rotation.y = 0;
          }
        },
      },
      home,
      homeName,
      plan: () => {
        let roll = r() * total;
        for (const [weight, make] of choices) {
          roll -= weight;
          if (roll <= 0) return make();
        }
        return null;
      },
      homeTask: () => pick(info.home),
      homeDwell: [8, 16],
      speed: info.child ? 1.6 : 2.0,
      book,
      rand: r,
      bubble,
      sway: 0.12,
    });
    guests.set(info.id, {
      info,
      mover,
      ch,
      w,
      bubble,
      glass,
      shoulder: ch.getHandAnchor().parent as TransformNode,
      style: i % 3,
      phase: r() * Math.PI * 2,
      away: false,
      trip: null,
      liftSpot: null,
      left: 0,
      right: info.child ? 0 : 0.55,
    });
  });
  const guestList = [...guests.values()];
  // Each guest's meshes and their own visibility (0 for the click target), so a guest who leaves the
  // roof or comes back to it always shows as themselves, whatever the roof's fade last set.
  const baseVis = new Map<string, Array<[Mesh | AbstractMesh, number]>>(
    guestList.map((g) => [g.info.id, g.mover.getChildMeshes(false).map((m) => [m, m.visibility] as [AbstractMesh, number])]),
  );
  const restore = (id: string): void => {
    for (const [m, v] of baseVis.get(id) ?? []) m.visibility = v;
  };

  const dj = new VoxelCharacter(scene, "party:dj", PARTY_DJ_PALETTE);
  dj.root.parent = root;
  dj.root.scaling.setAll(CHARACTER_SCALE);
  dj.root.position.set(DJ_AT.x, FLOOR, DJ_AT.z);
  dj.root.rotation.y = Math.PI;
  const djBubble: Bubble = kit.bubble(dj.root, 2.45, "#5ce1ff", CHARACTER_SCALE);

  /* ---------------- Confetti and fireworks ---------------- */
  const confetti: Confetti[] = [];
  const confettiMats = PARTY_COLOURS.map((hex) => kit.mat(hex, { unlit: true }));
  for (let i = 0; i < 40; i++) {
    const m = kit.block(root, 0, 0, 0, 0.3, 0.03, 0.2, confettiMats[i % confettiMats.length], { dynamic: true });
    m.setEnabled(false);
    confetti.push({ m, v: new Vector3(), spin: new Vector3(), rest: 0 });
  }
  const fireConfetti = (): void => {
    confetti.forEach((c, i) => {
      const from = cannons[i % 2];
      const toX = df.x + (r() - 0.5) * 3;
      const toZ = df.z + (r() - 0.5) * 3;
      const dx = toX - from.x;
      const dz = toZ - from.z;
      const d = Math.hypot(dx, dz) || 1;
      const speed = 1.2 + r() * 1.6;
      c.m.position.set(from.x, FLOOR + 0.9, from.z);
      c.v.set((dx / d) * speed, 6.4 + r() * 2.4, (dz / d) * speed);
      c.spin.set(r() * 8 - 4, r() * 8 - 4, r() * 8 - 4);
      c.rest = 0;
      c.m.setEnabled(true);
    });
  };

  const SPARKS = 24;
  const sparkDirs: Vector3[] = [];
  for (let i = 0; i < SPARKS; i++) {
    // Spread evenly over a sphere.
    const yy = 1 - ((i + 0.5) / SPARKS) * 2;
    const rad = Math.sqrt(1 - yy * yy);
    const a = i * 2.39996;
    sparkDirs.push(new Vector3(Math.cos(a) * rad, yy, Math.sin(a) * rad));
  }
  const bursts: Burst[] = [0, 1].map(() => {
    const mat = kit.mat("#ffc35a", { unlit: true, unique: true });
    const rocket = kit.block(root, 0, 0, 0, 0.2, 0.4, 0.2, kit.mat("#fff1c2", { unlit: true }), { dynamic: true });
    rocket.setEnabled(false);
    const sparks = sparkDirs.map(() => {
      const s = kit.block(root, 0, 0, 0, 0.45, 0.45, 0.45, mat, { dynamic: true });
      s.setEnabled(false);
      return s;
    });
    return { rocket, sparks, dirs: sparkDirs, mat, state: "idle" as const, t: 0, peak: 0, centre: new Vector3() };
  });
  const launch = (): void => {
    const b = bursts.find((x) => x.state === "idle");
    if (!b) return;
    b.state = "rising";
    b.t = 0;
    b.peak = SIGN.y + 9 + r() * 4;
    b.rocket.position.set(SIGN.x + (r() - 0.5) * 18, SIGN.y + 1, SIGN.z + 3.2);
    b.rocket.setEnabled(true);
    const c = Color3.FromHexString(pick(PARTY_COLOURS));
    b.mat.emissiveColor.copyFrom(c);
    b.mat.diffuseColor.copyFrom(c);
  };

  /* ---------------- The party's moments ---------------- */
  let clock = 0;
  let lastBeat = -1;
  let chase = 0;
  let chaseOn = false;
  let selected = false;
  let toastIn = 10;
  let toastFor = 0;
  let cheered = false;
  let toastLine = "";
  let toastNow = "";
  let fireIn = 4;
  let fireFor = 0;
  const launches: number[] = [];
  let djIn = 6;
  let djSays = 0;
  const here = (id: string): boolean => {
    const g = guests.get(id);
    return !!g && !g.away && !g.trip;
  };
  const djSay = (text: string): void => {
    djBubble.say(text);
    djSays = 3.4;
  };
  const cheering = (): boolean => toastFor > 0 && cheered;
  const bright = Color3.FromHexString("#fff1c2");
  const dim = bright.scale(0.22);
  const lightBase = Color3.FromHexString("#ffe6a8");
  const setColour = (m: StandardMaterial, c: Color3): void => {
    m.emissiveColor.copyFrom(c);
    m.diffuseColor.copyFrom(c);
  };
  /** The toasts, in turn: to whoever is here with happy news, and to everyone. */
  const TOASTS: Array<{ who?: string; line: string; now: string }> = [
    { who: "george", line: "To George and Harpreet’s succession plan!", now: "A toast to George and Harpreet" },
    { who: "priya", line: "To Priya and Arjun’s new home!", now: "A toast to Priya and Arjun" },
    { line: "To all our clients and referral partners!", now: "A toast to the clients" },
  ];
  let toastIndex = 0;

  const pose = (g: Guest, ph: number, dt: number): void => {
    const body = g.ch.root;
    if (g.w.walking) {
      // The stride swings the arms; afterwards the pose eases up from the sides.
      g.left = 0;
      g.right = 0;
    } else {
      const dancing = !!g.w.here?.key?.startsWith("dance:");
      let lft = 0;
      let rgt = g.info.child ? 0 : 0.55;
      let spread = 0;
      if (dancing) {
        const p = ph + g.phase;
        const hop = Math.abs(Math.sin(p));
        if (g.style === 0) {
          body.position.set(0, 0.05 + hop * 0.2, 0);
          body.rotation.y = Math.sin(p * 0.5) * 0.35;
          lft = 0.5;
          rgt = 2.6 + Math.sin(p) * 0.35;
        } else if (g.style === 1) {
          body.position.set(0, 0.05 + hop * 0.14, 0);
          body.rotation.y = Math.sin(p * 0.5) * 0.25;
          lft = 2.5;
          rgt = 2.5;
          spread = 0.4 + Math.sin(p) * 0.15;
        } else {
          body.position.set(Math.sin(p * 0.5) * 0.18, 0.05 + hop * 0.1, 0);
          body.rotation.y = Math.sin(p * 0.5) * 0.15;
          lft = 0.9 + Math.sin(p) * 0.5;
          rgt = 0.9 - Math.sin(p) * 0.5;
          spread = 0.25;
        }
      } else {
        body.position.set(0, 0, 0);
        body.rotation.y *= Math.exp(-dt * 6);
      }
      // Glasses up for the toast.
      if (cheering() && !g.info.child) rgt = Math.max(rgt, 2.5);
      const k = Math.min(1, dt * 10);
      g.left += (lft - g.left) * k;
      g.right += (rgt - g.right) * k;
      g.ch.setArmPose(g.left, g.right, spread);
    }
    // Undo the shoulder's turn, so the drink stays upright whatever the arm is doing.
    const s = g.shoulder.rotation;
    const q = g.glass.rotationQuaternion;
    if (q) Quaternion.RotationYawPitchRollToRef(s.y, s.x, s.z, q).conjugateInPlace();
  };

  /* ---------------- Coming and going ---------------- */
  let selectedGuests: ReadonlySet<string> = new Set();
  /** The finale's client (and whoever came with them) is down at the front desk. */
  let awayFor: ScenarioId | null = null;
  /** Finale clients riding the lift back up: their places are kept until they step out. */
  const holding = new Set<ScenarioId>();
  /** Where guests coming back up the lift step out, for the next refresh. */
  const arriving = new Map<string, Pt>();
  /** A spot on the lift's landing, reached down the left side of the deck. */
  const landing = (p: Pt, key?: string): Spot => ({ at: p, face: Math.PI, via: [[LIFT_LANE, CZ], [LIFT_LANE, p[1] + 0.9]], key });
  const refresh = (): void => {
    for (const g of guestList) {
      const lf = g.info.leavesFor;
      const away = !!lf && (lf === awayFor || holding.has(lf));
      if (away === g.away) continue;
      g.away = away;
      g.ch.root.position.set(0, 0, 0);
      g.ch.root.rotation.y = 0;
      const at = arriving.get(g.info.id);
      if (!away && at) g.w.enterAt(landing(at));
      else g.w.snapHome();
      g.mover.setEnabled(!away);
    }
    arriving.clear();
  };
  const travel: PartyTravel = {
    free: (ids) =>
      ids.every((id) => {
        const g = guests.get(id);
        return !!g && !g.away && !g.trip && !g.liftSpot && g.w.isHome && !selectedGuests.has(id);
      }),
    goToLift: (ids, spots, line) =>
      ids.forEach((id, i) => {
        const g = guests.get(id);
        if (!g) return;
        g.liftSpot = landing(spots[i], `lift:${i}`);
        g.w.pin({ spot: g.liftSpot, task: "Heading for the glass lift", where: "the glass lift", dwell: Infinity });
        if (i === 0) g.w.say(line, 3.2);
      }),
    atLift: (ids) =>
      ids.every((id) => {
        const g = guests.get(id);
        return !!g?.liftSpot && !g.w.walking && g.w.here === g.liftSpot;
      }),
    take: (id, status) => {
      const g = guests.get(id);
      if (!g) throw new Error(`No party guest ${id}`);
      g.trip = status;
      g.liftSpot = null;
      g.w.park();
      g.ch.root.position.set(0, 0, 0);
      g.ch.root.rotation.y = 0;
      g.ch.setWalking(false);
      // They put their drink down on the way out.
      g.glass.setEnabled(false);
      restore(id);
      return { id, name: g.info.name, node: g.mover, ch: g.ch, bubble: g.bubble };
    },
    setStatus: (id, status) => {
      const g = guests.get(id);
      if (g?.trip) g.trip = status;
    },
    giveBack: (id, at) => {
      const g = guests.get(id);
      if (!g) return;
      g.trip = null;
      g.glass.setEnabled(true);
      g.mover.parent = root;
      g.mover.position.set(at[0], FLOOR, at[1]);
      g.mover.setEnabled(!g.away);
      restore(id);
      g.w.enterAt(landing(at));
    },
    snapBack: (id) => {
      const g = guests.get(id);
      if (!g) return;
      g.trip = null;
      g.liftSpot = null;
      g.glass.setEnabled(true);
      g.mover.parent = root;
      g.mover.position.y = FLOOR;
      g.mover.setEnabled(!g.away);
      restore(id);
      g.w.snapHome();
    },
  };

  return {
    root,
    update(dt: number): void {
      clock += dt;
      const beats = (clock * BPM) / 60;
      const beat = Math.floor(beats);
      const ph = beats * Math.PI;
      const kick = Math.pow(1 - (beats - beat), 4);
      if (beat !== lastBeat) {
        lastBeat = beat;
        tileMats.forEach((m, i) => setColour(m, Color3.FromHexString(PARTY_COLOURS[(i + beat) % PARTY_COLOURS.length])));
        lampMats.forEach((m, i) => setColour(m, Color3.FromHexString(PARTY_COLOURS[(i + beat * 3) % PARTY_COLOURS.length])));
      }
      for (const wf of woofers) wf.scaling.x = wf.scaling.y = 1 + kick * 0.14;
      for (const d of decks) d.rotation.y += dt * 3.4;
      chase += dt;
      if (chase >= (selected || toastFor > 0 ? 0.14 : 0.32)) {
        chase = 0;
        chaseOn = !chaseOn;
        setColour(bulbA, chaseOn ? bright : dim);
        setColour(bulbB, chaseOn ? dim : bright);
      }
      setColour(lights, lightBase.scale(0.8 + 0.2 * Math.sin(clock * 2.1)));
      bunches.forEach((b, i) => {
        b.position.y = FLOOR + Math.sin(clock * 1.2 + i * 1.7) * 0.08;
        b.rotation.z = Math.sin(clock * 0.8 + i) * 0.05;
      });

      // The DJ, and the toasts they call.
      toastIn -= dt;
      if (toastIn <= 0 && toastFor <= 0) {
        toastFor = 4.4;
        toastIn = 34 + r() * 10;
        cheered = false;
        // The next toast whose guests are here.
        for (let k = 0; k < TOASTS.length; k++) {
          const t = TOASTS[(toastIndex + k) % TOASTS.length];
          if (!t.who || here(t.who)) {
            toastLine = t.line;
            toastNow = t.now;
            toastIndex = (toastIndex + k + 1) % TOASTS.length;
            break;
          }
        }
        djSay(toastLine);
      }
      if (toastFor > 0) {
        toastFor -= dt;
        if (!cheered && toastFor < 3.6) {
          cheered = true;
          for (const g of guestList) if (!g.away && !g.trip && !g.liftSpot && !g.w.walking) g.w.say("Cheers!", 3.2);
          fireConfetti();
        }
      }
      djIn -= dt;
      if (djIn <= 0 && toastFor <= 0) {
        djSay(pick(DJ_LINES));
        djIn = 9 + r() * 8;
      }
      if (djSays > 0) {
        djSays -= dt;
        if (djSays <= 0) djBubble.hide();
      }
      dj.update(dt);
      dj.root.position.y = FLOOR + Math.abs(Math.sin(ph)) * 0.08;
      if (cheering()) dj.setArmPose(2.7, 2.7, 0.35);
      else dj.setArmPose(0.95 + Math.sin(ph * 0.5) * 0.18, 0.95 + Math.cos(ph * 0.5) * 0.18, 0.1);

      for (const g of guestList) {
        if (g.away || g.trip) continue;
        g.w.update(dt);
        g.ch.update(dt);
        pose(g, ph, dt);
      }

      for (const c of confetti) {
        if (!c.m.isEnabled()) continue;
        const p = c.m.position;
        // Rest just above the dance floor's tiles.
        const ground = FLOOR + 0.08;
        if (p.y <= ground) {
          c.rest += dt;
          if (c.rest > 1.4) c.m.setEnabled(false);
          continue;
        }
        c.v.y = Math.max(c.v.y - 7 * dt, -1.3);
        const drag = Math.exp(-0.8 * dt);
        c.v.x *= drag;
        c.v.z *= drag;
        p.x += (c.v.x + Math.sin(clock * 5 + c.spin.x) * 0.3) * dt;
        p.y = Math.max(ground, p.y + c.v.y * dt);
        p.z += c.v.z * dt;
        if (p.y > ground) {
          c.m.rotation.x += c.spin.x * dt;
          c.m.rotation.y += c.spin.y * dt;
          c.m.rotation.z += c.spin.z * dt;
        } else {
          // Landed: lie flat.
          c.m.rotation.x = 0;
          c.m.rotation.z = 0;
        }
      }

      // Fireworks behind the sign.
      fireIn -= dt;
      if (fireIn <= 0) {
        fireIn = 15 + r() * 9;
        fireFor = 4.5;
        launches.push(0, 0.9);
        if (r() > 0.5) launches.push(1.8);
      }
      if (fireFor > 0) fireFor -= dt;
      for (let i = launches.length - 1; i >= 0; i--) {
        launches[i] -= dt;
        if (launches[i] <= 0) {
          launches.splice(i, 1);
          launch();
        }
      }
      for (const b of bursts) {
        if (b.state === "rising") {
          b.rocket.position.y += dt * 15;
          b.rocket.position.x += Math.sin(clock * 3) * dt * 0.4;
          if (b.rocket.position.y >= b.peak) {
            b.centre.copyFrom(b.rocket.position);
            b.rocket.setEnabled(false);
            b.state = "bursting";
            b.t = 0;
            for (const s of b.sparks) s.setEnabled(true);
          }
        } else if (b.state === "bursting") {
          b.t += dt;
          const f = Math.min(1, b.t / 2.0);
          const out = 5.6 * (1 - Math.pow(1 - f, 2.2));
          b.sparks.forEach((s, i) => {
            const d = b.dirs[i];
            s.position.set(b.centre.x + d.x * out, b.centre.y + d.y * out - 1.8 * f * f, b.centre.z + d.z * out);
            s.scaling.setAll(Math.max(0.01, 1 - f));
          });
          if (f >= 1) {
            b.state = "idle";
            for (const s of b.sparks) s.setEnabled(false);
          }
        }
      }
    },
    setHighlights(h: Highlights): void {
      selected = h.party;
      selectedGuests = h.guests;
      for (const g of guestList) g.ch.setHighlight(h.party || h.guests.has(g.info.id), GOLD);
      dj.setHighlight(h.party, GOLD);
    },
    setAway(id: ScenarioId | null): void {
      awayFor = id;
      refresh();
    },
    holdAway(id: ScenarioId): void {
      holding.add(id);
      refresh();
    },
    comeBack(id: ScenarioId, at?: ReadonlyArray<Pt>): void {
      holding.delete(id);
      if (at) {
        // The client steps out first, then whoever came with them.
        const list = guestList.filter((g) => g.info.leavesFor === id);
        list.sort((a, b) => Number(b.info.scenario === id) - Number(a.info.scenario === id));
        list.forEach((g, i) => {
          if (at[i]) arriving.set(g.info.id, at[i]);
        });
      }
      refresh();
    },
    travel,
    now(): string {
      if (toastFor > 0) return toastNow;
      if (fireFor > 0) return "Fireworks over the city";
      return "Dancing to the DJ";
    },
    guestTask(id: string): string | null {
      const g = guests.get(id);
      if (!g) return null;
      if (g.away) return g.info.leavesFor && holding.has(g.info.leavesFor) ? "Riding the glass lift back up to the party" : "Down at the front desk";
      if (g.trip) return g.trip;
      if (cheering() && !g.w.walking) return g.info.child ? "Clapping along" : "Raising a glass";
      return g.w.task;
    },
  };
}
