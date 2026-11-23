/**
 * The front desk: under the cast-iron verandah on the footpath in front of
 * Guru House, beside the tram stop. The greeter welcomes visitors off the
 * tram, Amelia, Life Advice Guru's adviser, links each one with the agents that get
 * them started (a gold beam runs from her to each agent's bay), and Olivia's
 * Fact Find Extraction agent walks out to read what they sign.
 *
 * Between the finale's requests, everyday visitors drop by, one tram at a
 * time. In the finale the chosen client arrives first (with their business
 * partner or support person, where they have one), signs at the front desk,
 * and the request sets off through the house (see requestTrace.ts).
 */
import { Color3, DynamicTexture, type Mesh, MeshBuilder, Quaternion, TransformNode } from "@babylonjs/core";
import { PALETTES, type VoxelCharacterPalette } from "../characterPalettes";
import { CLIENT_PERSONAS, findStaffById, type ScenarioId } from "../personaData";
import { VoxelCharacter } from "../voxelCharacter";
import { balustrade, heritageMats, lace } from "./heritage";
import {
  ADVISER,
  ARRIVALS,
  DROP_INS,
  IMPOSTOR_PALETTE,
  SCENARIO_SIDE,
  SIDES,
  type Companion,
  type SideId,
} from "./houseData";
import { Batch, type Bubble, type Kit, rng } from "./kit";
import { CHARACTER_SCALE, FRONT_DESK, KERB_Z, LIFT, PLATFORM, PLATFORM_EDGE, TRACK_Z, TRAM_STOP_X, VERANDAH } from "./layout";
import { LIFT_FLOOR, type Lift, type LiftRider, type LiftTrip } from "./lift";
import type { Anchor, Highlights } from "./types";

type Pt = readonly [number, number];

/** Height of the footpath, and of the tram stop's platform. */
const PATH_Y = 0.15;
const PLATFORM_Y = PLATFORM.y;
/** The tram comes in from the left and leaves to the right, appearing and fading at the block's edges. */
const TRAM_FROM = -58;
const TRAM_TO = 58;
const TRAM_FADE = 9;
const TRAM_MAX = 9;
const TRAM_BRAKE = 4.2;
/** The finale's client starts closer in, so the arrival doesn't drag. */
const SCRIPTED_FROM = TRAM_STOP_X - 32;
/** Where visitors step off, and their walk across the forecourt to the desk, in under the verandah. */
const DOOR: Pt = [TRAM_STOP_X, PLATFORM_EDGE - 0.1];
const OFF: Pt = [TRAM_STOP_X + 0.3, PLATFORM_EDGE + 0.7];
const SIGN_AT: Pt = [FRONT_DESK.x - 0.4, FRONT_DESK.z - 1.35];
/** In under the verandah between its first two posts, clear of the greeter. */
const UNDER: Pt[] = [
  [VERANDAH.x0 + 1.2, VERANDAH.z0 - 0.7],
  [VERANDAH.x0 + 2.0, VERANDAH.z0 + 0.8],
];
const WALK_IN: Pt[] = [OFF, [TRAM_STOP_X + 2.2, KERB_Z + 0.6], ...UNDER, SIGN_AT];
/** From the glass lift's street doors along the forecourt to the desk (the finale's clients from the party). */
const LIFT_OUT: Pt = [LIFT.x - 0.6, LIFT.z0 - 1.5];
const FROM_LIFT: Pt[] = [[LIFT.x + 2.6, LIFT.z0 - 1.6], ...UNDER, SIGN_AT];
const WALK_SPEED = 2.6;
/** A brisker walk over from the lift, which is further away than the tram stop. */
const LIFT_PACE = 3.3;
const GOLD = Color3.FromHexString("#ffc35a");
/** Amelia stands out front of the desk, just under the verandah's edge. */
const AMELIA_AT: Pt = [FRONT_DESK.x + 2.2, FRONT_DESK.z - 1.75];
/** The greeter waits just inside the verandah's edge, facing the tram stop. */
const GREETER_AT: Pt = [VERANDAH.x0 + 0.55, VERANDAH.z0 + 1.1];
/** Amelia faces the tram stop between visitors, and turns toward the visitor while she helps them. */
const FACE_IDLE = Math.PI + 0.75;
const FACE_HELP = Math.PI * 2 - 1.6;
/** Seconds between the agents she links. */
const LINK_EVERY = 1.6;
/** A hold on the tram lapses after this long, so nobody can keep it at the stop for ever. */
const HOLD_MAX = 8;

type Phase =
  | "away"
  | "riding-in"
  | "riding-down"
  | "stepping-off"
  | "walking-in"
  | "signing"
  | "signed"
  | "walking-out"
  | "waiting"
  | "boarding"
  | "riding-out";

const STATUS: Record<Phase, string> = {
  away: "",
  "riding-in": "On the tram",
  "riding-down": "Coming down from the party in the glass lift",
  "stepping-off": "Stepping off the tram",
  "walking-in": "Walking up to the front desk",
  signing: "Signing at the front desk",
  signed: "Signed",
  "walking-out": "Heading back to the tram stop",
  waiting: "Waiting for the tram",
  boarding: "Boarding the tram",
  "riding-out": "Heading home",
};

interface Person {
  ch: VoxelCharacter;
  bubble: Bubble;
}

interface Visitor extends Person {
  kind: "client" | "impostor" | "drop-in";
  palette: VoxelCharacterPalette;
  name: string;
  doc: string;
  side: SideId;
  summary: string;
  scenario?: ScenarioId;
  companion?: Person & { info: Companion };
  /** Amelia's welcome as they walk up. */
  welcome: string;
  /** The agents Amelia links them with while they sign, by staff id. */
  links: readonly string[];
}

export interface VisitorInfo {
  kind: Visitor["kind"];
  palette: VoxelCharacterPalette;
  name: string;
  doc: string;
  side: SideId;
  summary: string;
  scenario?: ScenarioId;
  /** Who came with them. */
  companion?: string;
  /** Where they are in their visit, in plain words. */
  status: string;
}

export interface FrontDeskHooks {
  /** A visitor is on the way: send the Fact Find Extraction agent out to the desk. */
  callAgent(): void;
  /** The form has been read: the agent can go back inside. */
  releaseAgent(): void;
  /** The agent is standing at the desk, ready to read the signed form. */
  agentReady(): boolean;
  /** Amelia has linked the visitor with these agents, by staff id (empty once they are on their way). */
  link(staffIds: string[]): void;
  /**
   * A finale client who came down from the party is back up on the roof (stepping out of the lift
   * at `at`, visitor first), or, with no `at`, straight back at their table.
   */
  backToParty(id: ScenarioId, at?: Array<readonly [number, number]>): void;
}

/** The tram stop, shared by the front desk's visitors and the party guests who come and go by tram. */
export interface TramStop {
  /** A tram is standing at the stop. */
  readonly standing: boolean;
  /** Its doors are open. */
  readonly open: boolean;
  /** Keep the tram at the stop, with its doors open, while `key` steps on or off. */
  hold(key: string, on: boolean): void;
}

export interface FrontDesk {
  readonly root: TransformNode;
  readonly anchors: Map<string, Anchor>;
  readonly tramStop: TramStop;
  /** The scripted visitor has signed (the finale's request can set off). */
  readonly signed: boolean;
  /** Speed of the scripted arrival (0 pauses it), set by the finale's controls. */
  timeScale: number;
  update(dt: number): void;
  /** The finale: the chosen client arrives by tram, or comes down from the party in the glass lift. */
  arrive(id: ScenarioId): void;
  /** Skip ahead to the signature. */
  fastForward(): void;
  /**
   * Back to everyday visitors. A client who came down from the party heads back up in the lift:
   * returns their scenario, so the party keeps their place until they are back.
   */
  ambient(): ScenarioId | null;
  /** Freeze the front desk (and hide the tram) while the street is hidden. */
  setActive(on: boolean): void;
  visitor(): VisitorInfo | null;
  /** What Amelia is doing right now, for her card. */
  adviserTask(): string;
  setHighlights(h: Highlights): void;
}

function wrapLines(ctx: CanvasRenderingContext2D, text: string, width: number): string[] {
  const words = text.split(" ");
  const lines: string[] = [];
  let line = "";
  for (const w of words) {
    const next = line ? `${line} ${w}` : w;
    if (ctx.measureText(next).width > width && line) {
      lines.push(line);
      line = w;
    } else line = next;
  }
  if (line) lines.push(line);
  return lines.slice(0, 2);
}

/* ---------------- The tram ---------------- */

interface Tram {
  node: TransformNode;
  meshes: Mesh[];
  doors: Array<{ leaf: Mesh; home: number; dir: number }>;
  x: number;
  v: number;
  /** Where it is heading: the stop, or off the edge. */
  target: number;
  /** 0 shut, 1 open. */
  open: number;
  wantOpen: boolean;
}

/** A generic modern low-floor tram in three sections, with no route number or livery. */
function buildTram(kit: Kit): Tram {
  const scene = kit.scene;
  const node = new TransformNode("v2_tram", scene);
  const b = new Batch(node, "tram");
  const prev = kit.useBatch(b);
  const body = kit.mat("#f2f4f7", { specular: 0.35 });
  const skirt = kit.mat("#2b3240");
  const glass = kit.mat("#1b2838", { specular: 0.6 });
  const stripe = kit.mat("#4b6584");
  const roofGrey = kit.mat("#c3cad3");
  const dark = kit.mat("#1a1d24");
  const lamp = kit.mat("#fff4d6", { unlit: true });
  const W = 2.5;
  const sections: Array<[x: number, len: number]> = [
    [-5.2, 5.0],
    [0, 4.6],
    [5.2, 5.0],
  ];
  for (const [x, len] of sections) {
    kit.block(node, x, 0.3, 0, len, 0.6, W, skirt);
    kit.block(node, x, 0.9, 0, len, 3.0, W, body);
    kit.block(node, x, 1.62, 0, len - 0.3, 1.5, W + 0.04, glass);
    for (let px = x - len / 2 + 1.2; px < x + len / 2 - 0.6; px += 1.25) kit.block(node, px, 1.62, 0, 0.16, 1.5, W + 0.06, body);
    kit.block(node, x, 1.28, 0, len, 0.16, W + 0.06, stripe);
    kit.block(node, x, 3.9, 0, len - 0.2, 0.18, W - 0.3, roofGrey);
    // Bogies.
    kit.block(node, x, 0.02, 0, len * 0.5, 0.3, W - 0.5, dark);
  }
  // Bellows between the sections.
  for (const x of [-2.55, 2.55]) kit.block(node, x, 0.5, 0, 0.3, 3.3, W - 0.3, dark);
  // The noses, with a windscreen, headlights and a destination display.
  for (const dir of [-1, 1]) {
    const nx = dir * 7.85;
    kit.block(node, nx, 0.3, 0, 0.5, 3.4, W - 0.3, body);
    kit.block(node, nx + dir * 0.18, 1.5, 0, 0.18, 1.7, W - 0.6, glass);
    kit.block(node, nx + dir * 0.24, 0.75, -0.7, 0.08, 0.22, 0.32, lamp);
    kit.block(node, nx + dir * 0.24, 0.75, 0.7, 0.08, 0.22, 0.32, lamp);
  }
  // Roof equipment and the pantograph on the middle section.
  kit.block(node, -5.2, 4.08, 0, 2.2, 0.4, 1.4, roofGrey);
  kit.block(node, 5.2, 4.08, 0, 2.2, 0.4, 1.4, roofGrey);
  kit.block(node, 0, 4.08, 0, 1.2, 0.3, 1.0, dark);
  const arm = kit.block(node, -0.4, 4.4, 0, 0.08, 1.2, 0.08, dark);
  arm.rotation.z = 0.7;
  const arm2 = kit.block(node, 0.35, 4.4, 0, 0.08, 1.1, 0.08, dark);
  arm2.rotation.z = -0.6;
  kit.block(node, 0, 5.3, 0, 0.12, 0.08, 1.4, dark);
  kit.useBatch(prev);
  const meshes = b.flush();
  // Destination displays at each end: just "CITY", no route number.
  for (const dir of [-1, 1]) {
    const sign = kit.label(node, {
      lines: [{ text: "CITY", size: 60, weight: 800, color: "#ffb547" }],
      width: 1.3,
      px: 256,
      pxHeight: 72,
      bg: "rgba(10,10,12,1)",
    });
    sign.position.set(dir * 8.12, 3.15, 0);
    sign.rotation.y = dir > 0 ? -Math.PI / 2 : Math.PI / 2;
    meshes.push(sign);
  }
  // Sliding doors on the footpath side (+z), one pair per section.
  const doors: Tram["doors"] = [];
  const doorMat = kit.mat("#cfd6de", { specular: 0.4 });
  for (const [x] of sections) {
    for (const side of [-1, 1]) {
      const leaf = kit.block(node, x + side * 0.42, 0.62, W / 2 + 0.04, 0.82, 2.6, 0.06, doorMat, { dynamic: true });
      kit.block(leaf, 0, -0.5, 0.03, 0.56, 1.3, 0.02, glass, { dynamic: true });
      doors.push({ leaf, home: x + side * 0.42, dir: side });
      meshes.push(leaf, ...(leaf.getChildMeshes() as Mesh[]));
    }
  }
  node.position.set(TRAM_FROM, 0, TRACK_Z.near);
  node.setEnabled(false);
  return { node, meshes, doors, x: TRAM_FROM, v: 0, target: TRAM_FROM, open: 0, wantOpen: false };
}

export function buildFrontDesk(kit: Kit, street: TransformNode, hooks: FrontDeskHooks, lift: Lift): FrontDesk {
  const scene = kit.scene;
  const root = new TransformNode("v2_frontDesk", scene);
  root.parent = street;
  const r = rng(91);
  const hm = heritageMats(kit);
  const dx = FRONT_DESK.x;
  const dz = FRONT_DESK.z;
  const y = PATH_Y;

  /* ---------------- The verandah ---------------- */
  const batch = new Batch(root, "frontDesk");
  const prev = kit.useBatch(batch);
  const V = VERANDAH;
  const cream = kit.mat("#efe7d4");
  const green = kit.mat("#2f5d4f");
  // Cast-iron posts along the kerb and against the house, with bases and capitals.
  const postXs = [V.x0 + 0.2, (V.x0 * 2 + V.x1) / 3, (V.x0 + V.x1 * 2) / 3, V.x1 - 0.2];
  for (const px of postXs) {
    for (const pz of [V.z0, V.z1 - 0.3]) {
      kit.block(root, px, y, pz, 0.42, 0.5, 0.42, hm.iron);
      kit.block(root, px, y + 0.5, pz, 0.2, V.y - y - 0.9, 0.2, hm.iron);
      kit.block(root, px, V.y - 0.42, pz, 0.36, 0.2, 0.36, hm.gold);
      kit.block(root, px, V.y - 0.22, pz, 0.46, 0.22, 0.46, hm.iron);
    }
  }
  kit.block(root, (V.x0 + V.x1) / 2, V.y - 0.12, V.z0, V.x1 - V.x0, 0.16, 0.22, hm.iron);
  kit.block(root, (V.x0 + V.x1) / 2, V.y - 0.12, V.z1 - 0.3, V.x1 - V.x0, 0.16, 0.22, hm.iron);
  // The striped corrugated roof, with a bullnose curve over the street.
  const stripes = Math.round((V.x1 - V.x0) / 0.7);
  const sw = (V.x1 - V.x0) / stripes;
  for (let i = 0; i < stripes; i++) {
    const sx = V.x0 + (i + 0.5) * sw;
    const mat = i % 2 ? green : cream;
    const flat = kit.block(root, sx, V.y + 0.05, (V.z1 + V.z0 + 1.4) / 2, sw, 0.12, V.z1 - V.z0 - 1.4, mat);
    flat.rotation.x = -0.05;
    kit.block(root, sx, V.y - 0.02, V.z0 + 0.55, sw, 0.12, 0.6, mat).rotation.x = 0.55;
    kit.block(root, sx, V.y - 0.35, V.z0 + 0.12, sw, 0.12, 0.42, mat).rotation.x = 1.15;
  }
  kit.useBatch(prev);
  batch.flush();
  // Lace frieze between the front posts, and lace brackets at their tops.
  const friezeH = 0.6;
  for (let i = 0; i < postXs.length - 1; i++) {
    const a = postXs[i];
    const b = postXs[i + 1];
    const f = lace(kit, root, `frieze${i}`, b - a - 0.2, friezeH, Math.round((b - a) / 1.2));
    f.position.set((a + b) / 2, V.y - 0.12 - friezeH / 2, V.z0 - 0.02);
  }
  for (const px of postXs) {
    for (const side of [-1, 1]) {
      if ((px === postXs[0] && side < 0) || (px === postXs[postXs.length - 1] && side > 0)) continue;
      const br = lace(kit, root, `bracket${px}${side}`, 0.9, 0.7, 1);
      br.position.set(px + side * 0.5, V.y - 0.12 - friezeH - 0.35, V.z0 - 0.02);
    }
  }
  // The sign stands on the verandah, over the street, like an old shop sign.
  const sign = kit.label(root, {
    lines: [
      { text: "FRONT DESK", size: 64, weight: 800, color: "#0f1a2e", serif: true },
      { text: "Welcome to Life Advice Guru", size: 32, weight: 600, color: "#5b6476" },
    ],
    width: 3.8,
    px: 640,
    bg: "rgba(255,251,242,0.98)",
    accent: "#c9a14a",
    bar: "top",
  });
  sign.position.set((postXs[1] + postXs[2]) / 2, V.y + 0.62, V.z0 + 0.05);
  balustrade(kit, root, postXs[1] - 0.3, V.z0 + 0.25, postXs[2] + 0.3, V.z0 + 0.25, V.y + 0.12, { rail: hm.iron, baluster: hm.iron }, 0.5);

  /* ---------------- The front desk ---------------- */
  const deskBatch = new Batch(root, "frontDeskCounter");
  kit.useBatch(deskBatch);
  const walnut = kit.mat("#ffffff", { texture: "walnut" });
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  const gold = kit.mat("#c9a14a", { unlit: true });
  kit.block(root, dx, y, dz, 3.0, 1.1, 0.9, walnut);
  kit.block(root, dx, y + 1.1, dz, 3.2, 0.08, 1.1, kit.mat("#f1eee6", { specular: 0.3 }));
  kit.block(root, dx, y + 0.84, dz - 0.46, 2.6, 0.06, 0.02, gold);
  // The signing tablet and a pot plant.
  kit.block(root, dx + 0.95, y + 1.18, dz + 0.1, 0.12, 0.28, 0.12, steel);
  kit.block(root, dx + 0.95, y + 1.4, dz + 0.1, 0.66, 0.44, 0.06, kit.mat("#1f2937"));
  kit.block(root, dx + 0.95, y + 1.45, dz + 0.06, 0.56, 0.34, 0.02, kit.mat("#5ce1ff", { unlit: true }));
  kit.block(root, dx - 1.25, y + 1.18, dz + 0.1, 0.36, 0.3, 0.36, kit.mat("#d8d2c6"));
  kit.block(root, dx - 1.25, y + 1.48, dz + 0.1, 0.5, 0.42, 0.5, kit.mat("#5ea347"));
  // A planter at the far end of the verandah.
  kit.block(root, V.x1 - 1.0, y, V.z1 - 1.0, 1.1, 0.8, 1.1, kit.mat("#d8d2c6"));
  kit.block(root, V.x1 - 1.0, y + 0.8, V.z1 - 1.0, 1.0, 1.4, 1.0, kit.mat("#4f8a33"));
  kit.useBatch(prev);
  deskBatch.flush();

  kit.hitbox(root, dx, y, dz + 0.3, 3.6, 3.0, 2.2, { type: "desk" });
  kit.hitbox(root, (V.x0 + V.x1) / 2, V.y - 0.8, (V.z0 + V.z1) / 2, V.x1 - V.x0, 1.6, V.z1 - V.z0, { type: "desk" });
  const ring = MeshBuilder.CreateTorus("v2_deskRing", { diameter: 4.6, thickness: 0.16, tessellation: 40 }, scene);
  ring.position.set(dx, y + 0.08, dz - 0.2);
  kit.finish(ring, root, kit.mat("#ffe08a", { unlit: true }), true);
  ring.setEnabled(false);

  // The paper being signed, the pen, the agent's scan and the "sent" note.
  const docTex = new DynamicTexture("v2_deskDocTex", { width: 256, height: 320 }, scene, true);
  const doc = MeshBuilder.CreatePlane("v2_deskDoc", { width: 0.62, height: 0.78 }, scene);
  doc.rotation.x = Math.PI / 2;
  doc.position.set(dx - 0.4, y + 1.19, dz - 0.1);
  kit.finish(doc, root, kit.screen(docTex), true);
  doc.setEnabled(false);
  const paintDoc = (title: string, side: SideId, signed: boolean): void => {
    const ctx = docTex.getContext() as unknown as CanvasRenderingContext2D;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, 256, 320);
    ctx.fillStyle = SIDES[side].color;
    ctx.fillRect(0, 0, 256, 44);
    ctx.fillStyle = "#ffffff";
    ctx.font = '800 20px "Segoe UI", sans-serif';
    ctx.fillText("LIFE ADVICE GURU", 16, 30);
    ctx.fillStyle = "#0f1a2e";
    ctx.font = '700 21px "Segoe UI", sans-serif';
    wrapLines(ctx, title, 224).forEach((line, i) => ctx.fillText(line, 16, 76 + i * 26));
    ctx.fillStyle = "#cbd5e1";
    for (let i = 0; i < 5; i++) ctx.fillRect(16, 136 + i * 22, i % 2 ? 170 : 224, 8);
    ctx.fillStyle = "#64748b";
    ctx.font = '600 16px "Segoe UI", sans-serif';
    ctx.fillText("Sign here", 16, 268);
    ctx.fillRect(96, 276, 140, 3);
    if (signed) {
      ctx.strokeStyle = "#1d4ed8";
      ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(100, 272);
      ctx.bezierCurveTo(122, 236, 136, 292, 156, 258);
      ctx.bezierCurveTo(170, 236, 186, 286, 226, 252);
      ctx.stroke();
      ctx.save();
      ctx.translate(176, 196);
      ctx.rotate(-0.22);
      ctx.strokeStyle = "#16a34a";
      ctx.lineWidth = 5;
      ctx.strokeRect(-62, -22, 124, 44);
      ctx.fillStyle = "#16a34a";
      ctx.font = '800 26px "Segoe UI", sans-serif';
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("SIGNED", 0, 1);
      ctx.restore();
    }
    docTex.update(true);
  };
  // A blank form up front, so the texture is ready before the first visitor.
  paintDoc("", "shared", false);
  const pen = kit.block(root, 0, 0, 0, 0.06, 0.06, 0.42, kit.mat("#111827"), { dynamic: true });
  pen.setEnabled(false);
  const beam = kit.block(root, dx - 0.4, y + 1.24, dz - 0.1, 0.74, 0.02, 0.06, kit.mat("#5ce1ff", { unlit: true }), {
    dynamic: true,
  });
  beam.setEnabled(false);
  const sent = kit.label(root, {
    lines: [{ text: "Signed · sent to Onboarding", size: 34, weight: 700, color: "#0f766e" }],
    width: 2.6,
    px: 520,
    pxHeight: 70,
    bg: "rgba(255,255,255,0.95)",
    billboard: true,
  });
  sent.position.set(dx - 0.4, y + 2.15, dz - 0.3);
  sent.setEnabled(false);

  const anchorNode = new TransformNode("v2_deskAnchor", scene);
  anchorNode.parent = root;
  anchorNode.position.set(dx - 0.4, y + 1.2, dz - 0.1);
  const anchors = new Map<string, Anchor>([["desk", { node: anchorNode, group: "agents", dy: 0.3 }]]);

  /* ---------------- The greeter and the visitors ---------------- */
  const person = (id: string, palette: VoxelCharacterPalette, accent: string, bubbleY = 2.45, scale = CHARACTER_SCALE): Person => {
    const ch = new VoxelCharacter(scene, id, palette);
    ch.root.parent = root;
    ch.root.scaling.setAll(scale);
    return { ch, bubble: kit.bubble(ch.root, bubbleY, accent, scale) };
  };
  const greeter = person("greeter", PALETTES.receptionGreeter, "#c9a14a");
  greeter.ch.root.position.set(GREETER_AT[0], y, GREETER_AT[1]);
  const GREETER_FACE = Math.atan2(TRAM_STOP_X - GREETER_AT[0], PLATFORM_EDGE - GREETER_AT[1]);
  greeter.ch.root.rotation.y = GREETER_FACE;

  /* ---------------- Amelia, Life Advice Guru's adviser ---------------- */
  // She gives the advice, then links each visitor with the agents that get them started. Her
  // tablet shows the agent she is linking, in its colour.
  const amelia = person(ADVISER.id, ADVISER.palette, ADVISER.color, 2.6);
  amelia.ch.root.position.set(AMELIA_AT[0], y, AMELIA_AT[1]);
  amelia.ch.root.rotation.y = FACE_IDLE;
  // A slimmer click target, so the desk behind her can still be clicked past her.
  amelia.ch.getHitbox().scaling.x = 0.75;
  const shoulder = amelia.ch.getHandAnchor().parent as TransformNode;
  const holder = new TransformNode("v2_ameliaTablet", scene);
  holder.parent = amelia.ch.getHandAnchor();
  holder.rotationQuaternion = new Quaternion();
  const tilt = new TransformNode("v2_ameliaTabletTilt", scene);
  tilt.parent = holder;
  tilt.position.set(0, 0.1, 0.04);
  tilt.rotation.x = -0.45;
  kit.block(tilt, 0, -0.18, 0, 0.52, 0.36, 0.05, kit.mat("#1f2937", { specular: 0.3 }), { dynamic: true });
  const screen = kit.block(tilt, 0, -0.15, 0.03, 0.44, 0.3, 0.012, kit.mat(ADVISER.color, { unlit: true }), { dynamic: true });
  let arm = 1.1;
  /** Holds the tablet out, higher while she shows the visitor who she is linking. */
  const pose = (dt: number, showing: boolean): void => {
    arm += ((showing ? 1.45 : 1.1) - arm) * Math.min(1, dt * 6);
    amelia.ch.setArmPose(0.12, arm);
    // Undo the shoulder's turn, so the tablet stays upright whatever the arm is doing.
    const s = shoulder.rotation;
    Quaternion.RotationYawPitchRollToRef(s.y, s.x, s.z, holder.rotationQuaternion!).conjugateInPlace();
  };
  pose(1, false);
  anchors.set("adviser", { node: amelia.ch.root, group: "agents", dy: 3.15 });

  const companionOf = (c: Companion | undefined, accent: string): Visitor["companion"] => {
    if (!c) return undefined;
    const p = person(`visitor:companion:${c.id}`, c.palette, accent);
    return { ...p, info: c };
  };
  const clients = new Map<ScenarioId, Visitor>();
  for (const c of CLIENT_PERSONAS) {
    const arrival = ARRIVALS[c.id];
    if (arrival.visitor !== "client") continue;
    const side = SCENARIO_SIDE[c.id];
    clients.set(c.id, {
      kind: "client",
      ...person(`visitor:${c.id}`, PALETTES[c.palette], SIDES[side].color),
      palette: PALETTES[c.palette],
      name: c.name,
      doc: arrival.doc,
      side,
      summary: c.situation,
      scenario: c.id,
      companion: companionOf(arrival.companion, SIDES[side].color),
      welcome: arrival.welcome,
      links: arrival.links,
    });
  }
  const grace = CLIENT_PERSONAS.find((c) => c.id === "fraud");
  const impostor: Visitor = {
    kind: "impostor",
    ...person("visitor:impostor", IMPOSTOR_PALETTE, SIDES.claims.color),
    palette: IMPOSTOR_PALETTE,
    name: `Someone signing in ${grace?.name.split(" ")[0] ?? "a client"}’s name`,
    doc: ARRIVALS.fraud.doc,
    side: SCENARIO_SIDE.fraud,
    summary: `${grace?.name ?? "The client"} is overseas. This request in her name is made by someone else, and Felix’s Financial Crime Agent will check it.`,
    scenario: "fraud",
    welcome: ARRIVALS.fraud.welcome,
    links: ARRIVALS.fraud.links,
  };
  const dropIns: Visitor[] = DROP_INS.map((d) => ({
    kind: "drop-in",
    ...person(`visitor:${d.id}`, d.palette, SIDES[d.side].color),
    palette: d.palette,
    name: d.who,
    doc: d.doc,
    side: d.side,
    summary: d.summary,
    companion: companionOf(d.companion, SIDES[d.side].color),
    welcome: d.welcome,
    links: d.links,
  }));
  for (const v of [...clients.values(), impostor, ...dropIns]) {
    v.ch.root.setEnabled(false);
    v.companion?.ch.root.setEnabled(false);
  }

  /* ---------------- The tram ---------------- */
  const tram = buildTram(kit);
  let tramShown = false;
  /** Who is keeping the tram at the stop (party guests stepping on or off), and since when. */
  const holds = new Map<string, number>();
  /** The front desk's visitor is clear of the doors: the tram leaves once nobody is holding it. */
  let leaving = false;
  const showTram = (on: boolean): void => {
    tramShown = on;
    tram.node.setEnabled(on && active);
  };
  /** Sends the tram to `target`, starting from `from` if given. */
  const runTram = (target: number, from?: number): void => {
    if (from !== undefined) {
      tram.x = from;
      tram.v = TRAM_MAX * 0.7;
    }
    tram.target = target;
    tram.wantOpen = false;
    leaving = false;
    showTram(true);
  };
  /** The tram moves on, as soon as anyone still stepping on or off is clear. */
  const leave = (): void => {
    if (holds.size) leaving = true;
    else runTram(TRAM_TO);
  };
  const updateTram = (dt: number): void => {
    if (!tramShown) return;
    for (const [key, since] of holds) if (clock - since > HOLD_MAX) holds.delete(key);
    if (tramAtStop() && holds.size) tram.wantOpen = true;
    if (leaving && !holds.size) runTram(TRAM_TO);
    const dist = tram.target - tram.x;
    if (Math.abs(dist) > 0.001) {
      const stopping = tram.target < TRAM_TO - 1;
      const want = stopping ? Math.min(TRAM_MAX, Math.sqrt(2 * TRAM_BRAKE * Math.abs(dist))) : TRAM_MAX;
      tram.v += Math.sign(want - tram.v) * Math.min(Math.abs(want - tram.v), 3.2 * dt);
      const step = Math.min(Math.abs(dist), Math.max(0.05, tram.v) * dt);
      tram.x += Math.sign(dist) * step;
    } else {
      tram.v = 0;
    }
    tram.open += ((tram.wantOpen ? 1 : 0) - tram.open) * Math.min(1, dt * 5);
    for (const d of tram.doors) d.leaf.position.x = d.home + d.dir * tram.open * 0.7;
    tram.node.position.x = tram.x;
    tram.node.position.y = Math.abs(tram.v) > 0.1 ? Math.sin(clock * 9) * 0.015 : 0;
    // Fade in and out at the edges of the block.
    const fade = Math.min(1, Math.max(0, Math.min(tram.x - TRAM_FROM, TRAM_TO - tram.x) / TRAM_FADE));
    for (const mm of tram.meshes) mm.visibility = fade;
    if (tram.x >= TRAM_TO - 0.01) showTram(false);
  };
  const tramAtStop = (): boolean => tramShown && Math.abs(tram.x - TRAM_STOP_X) < 0.02 && tram.v < 0.05;

  /* ---------------- The visit ---------------- */
  let phase: Phase = "away";
  let wait = 4;
  let scripted = false;
  let signedFlag = false;
  let active = true;
  let current: Visitor | null = null;
  let dropIndex = 0;
  let route: Pt[] = [];
  let hop = 0;
  let agentOut = false;
  /** Skipped ahead: sign without waiting for the agent to walk out. */
  let rushed = false;
  /** How long the signed form has waited for the agent. */
  let late = 0;
  let releaseIn = -1;
  let sentFor = 0;
  let greeterWait = 6;
  let greeterSays = 0;
  let clock = 0;
  /** The finale's client came down from the party in the glass lift, rather than by tram. */
  let byLift = false;
  let liftTrip: LiftTrip | null = null;
  let welcomed = false;
  /** A finale client walking back to the lift, and riding it up to the party. */
  let back: { v: Visitor; route: Pt[]; trip: LiftTrip | null } | null = null;
  const greet = (text: string): void => {
    greeter.bubble.say(text);
    greeterSays = 3.4;
  };
  let ameliaSays = 0;
  let taskIndex = 0;
  /** The greeter and Amelia take turns to say what they are doing between visitors. */
  let ameliaTurn = false;
  const advise = (text: string, time = 3.4): void => {
    amelia.bubble.say(text);
    ameliaSays = time;
  };
  /** The agents Amelia has linked the visitor with so far, and those still to come. */
  let linked: string[] = [];
  let toLink: string[] = [];
  let linkIn = 0;
  const agentName = (id: string): string => findStaffById(id)?.agent_name ?? "agent";
  const linkNext = (): void => {
    const id = toLink.shift();
    if (!id) return;
    linked = [...linked, id];
    hooks.link(linked);
    screen.material = kit.mat(findStaffById(id)?.color ?? ADVISER.color, { unlit: true });
    // One speaker at a time: the visitor has said they are signing, now Amelia talks.
    current?.bubble.hide();
    advise(linked.length === 1 ? `Linking the ${agentName(id)}` : `And the ${agentName(id)}`, LINK_EVERY + 0.3);
    linkIn = LINK_EVERY;
  };
  const clearLinks = (): void => {
    toLink = [];
    if (!linked.length) return;
    linked = [];
    hooks.link([]);
    screen.material = kit.mat(ADVISER.color, { unlit: true });
  };
  const timeScale = { value: 1 };

  /** Places the visitor's companion beside them, a step to their left and a little behind. */
  const followCompanion = (v: Visitor): void => {
    const c = v.companion;
    if (!c) return;
    const n = v.ch.root;
    const a = n.rotation.y;
    // Their left, and a little behind (the walker looks along +z when rotation.y is 0).
    const lx = Math.cos(a);
    const lz = -Math.sin(a);
    c.ch.root.position.set(n.position.x + lx * 1.15 - Math.sin(a) * 0.45, n.position.y, n.position.z + lz * 1.15 - Math.cos(a) * 0.45);
    c.ch.root.rotation.y = a;
  };
  const showVisitor = (v: Visitor, on: boolean): void => {
    v.ch.root.setEnabled(on);
    v.companion?.ch.root.setEnabled(on);
  };
  const setWalking = (v: Visitor, on: boolean): void => {
    v.ch.setWalking(on);
    v.companion?.ch.setWalking(on);
  };
  /** Walks the visitor (and their companion) along `path`; true once they are there. */
  const walkPath = (v: Visitor, path: Pt[], speed: number, dt: number): boolean => {
    const node = v.ch.root;
    let step = speed * dt;
    while (step > 0 && path.length) {
      const [tx, tz] = path[0];
      const ddx = tx - node.position.x;
      const ddz = tz - node.position.z;
      const dist = Math.hypot(ddx, ddz);
      if (dist <= step) {
        node.position.x = tx;
        node.position.z = tz;
        step -= dist;
        path.shift();
      } else {
        node.position.x += (ddx / dist) * step;
        node.position.z += (ddz / dist) * step;
        node.rotation.y = Math.atan2(ddx, ddz);
        step = 0;
      }
    }
    // Up on the platform, or down on the footpath.
    node.position.y = node.position.z < KERB_Z ? PLATFORM_Y : y;
    followCompanion(v);
    return path.length === 0;
  };
  const walk = (v: Visitor, dt: number): boolean => walkPath(v, route, byLift ? LIFT_PACE : WALK_SPEED, dt);
  /** The visitor and their companion, as riders for the glass lift. */
  const ridersOf = (v: Visitor): LiftRider[] => [
    { node: v.ch.root, ch: v.ch, name: v.name, bubble: v.bubble },
    ...(v.companion ? [{ node: v.companion.ch.root, ch: v.companion.ch, name: v.companion.info.name, bubble: v.companion.bubble }] : []),
  ];
  /** Back from the lift (or anywhere) into the front desk's own space. */
  const home = (v: Visitor): void => {
    for (const n of [v.ch.root, v.companion?.ch.root]) if (n && n.parent !== root) n.parent = root;
  };
  const hideVisitor = (v: Visitor): void => {
    setWalking(v, false);
    showVisitor(v, false);
    v.bubble.hide();
    v.companion?.bubble.hide();
    home(v);
  };
  /** Drops a finale client who was heading back up: straight back to their table. */
  const dropBack = (): void => {
    if (!back) return;
    if (back.trip) lift.cancel(back.trip);
    hideVisitor(back.v);
    if (back.v.scenario) hooks.backToParty(back.v.scenario);
    back = null;
  };

  const reset = (): void => {
    if (agentOut) hooks.releaseAgent();
    agentOut = false;
    releaseIn = -1;
    if (liftTrip) lift.cancel(liftTrip);
    liftTrip = null;
    if (current) hideVisitor(current);
    current = null;
    byLift = false;
    doc.setEnabled(false);
    pen.setEnabled(false);
    beam.setEnabled(false);
    sent.setEnabled(false);
    sentFor = 0;
    greeter.bubble.hide();
    amelia.bubble.hide();
    ameliaSays = 0;
    clearLinks();
    phase = "away";
  };
  const callAgent = (): void => {
    if (agentOut) return;
    hooks.callAgent();
    agentOut = true;
  };
  const begin = (v: Visitor, from: number): void => {
    current = v;
    rushed = false;
    runTram(TRAM_STOP_X, from);
    showVisitor(v, false);
    setWalking(v, false);
    paintDoc(v.doc, v.side, false);
    phase = "riding-in";
    // The finale's client is expected: the agent heads out while the tram is still on its way.
    if (scripted) callAgent();
  };
  /** The finale's client comes down from the party on the roof in the glass lift, already aboard. */
  const beginByLift = (v: Visitor): void => {
    current = v;
    rushed = false;
    byLift = true;
    welcomed = false;
    showVisitor(v, true);
    setWalking(v, false);
    paintDoc(v.doc, v.side, false);
    phase = "riding-down";
    callAgent();
    lift.flush();
    // Already in the car on the roof, with its doors open.
    v.ch.root.position.set(LIFT.x, LIFT_FLOOR.roof, (LIFT.z0 + LIFT.z1) / 2);
    followCompanion(v);
    liftTrip = {
      from: "roof",
      aboard: true,
      first: true,
      riders: ridersOf(v),
      say: v.companion ? "Down to the front desk!" : "Off to the front desk!",
      speed: () => timeScale.value,
      done: () => {
        liftTrip = null;
        home(v);
        route = [...FROM_LIFT];
        setWalking(v, true);
        greet("Welcome to Life Advice Guru!");
        phase = "walking-in";
      },
    };
    lift.call(liftTrip);
  };
  /** After the finale, a client who came down from the party walks back to the lift and rides up. */
  const returnUp = (v: Visitor): void => {
    back = { v, route: [...UNDER].reverse().concat([[LIFT.x + 2.6, LIFT.z0 - 1.6], lift.waitAt("street", 0)]), trip: null };
    setWalking(v, true);
    v.bubble.say("Back to the party!");
  };
  /** Steps the walk back to the lift and the ride up. */
  const stepBack = (dt: number): void => {
    const b = back;
    if (!b || b.trip) return;
    b.v.ch.update(dt);
    b.v.companion?.ch.update(dt);
    if (!walkPath(b.v, b.route, LIFT_PACE, dt)) return;
    setWalking(b.v, false);
    b.v.bubble.hide();
    b.v.ch.root.rotation.y = lift.waitFace("street");
    followCompanion(b.v);
    b.trip = {
      from: "street",
      riders: ridersOf(b.v),
      say: "Up to the party!",
      done: () => {
        const at = ridersOf(b.v).map((r) => [r.node.position.x, r.node.position.z] as const);
        hideVisitor(b.v);
        if (b.v.scenario) hooks.backToParty(b.v.scenario, at);
        if (back === b) back = null;
      },
    };
    lift.call(b.trip);
  };
  const startSigning = (v: Visitor, time: number): void => {
    setWalking(v, false);
    v.ch.root.rotation.y = 0;
    followCompanion(v);
    doc.setEnabled(true);
    pen.setEnabled(true);
    v.bubble.say("Signing here");
    // Then Amelia links them with the agents that get them started.
    toLink = [...v.links];
    linkIn = 1.2;
    wait = time;
    late = 0;
    phase = "signing";
  };

  const step = (dt: number): void => {
    clock += dt;
    updateTram(dt);
    stepBack(dt);
    greeter.ch.update(dt);
    const greeterFace = phase === "riding-in" || phase === "stepping-off" ? GREETER_FACE : GREETER_FACE + Math.sin(clock * 0.5) * 0.25;
    greeter.ch.root.rotation.y += (greeterFace - greeter.ch.root.rotation.y) * Math.min(1, dt * 3);
    amelia.ch.update(dt);
    const helping = !!current && (phase === "walking-in" || phase === "signing" || phase === "signed" || phase === "walking-out");
    const face = (helping ? FACE_HELP : FACE_IDLE) + Math.sin(clock * 0.4 + 1) * 0.1;
    amelia.ch.root.rotation.y += (face - amelia.ch.root.rotation.y) * Math.min(1, dt * 3);
    const linking = phase === "signing" || phase === "signed";
    pose(dt, linking && linked.length > 0);
    if (linking && toLink.length) {
      linkIn -= dt;
      if (linkIn <= 0) linkNext();
    }
    if (ameliaSays > 0) {
      ameliaSays -= dt;
      if (ameliaSays <= 0) amelia.bubble.hide();
    }
    if (sentFor > 0) {
      sentFor -= dt;
      sent.position.y = y + 2.15 + (2.5 - sentFor) * 0.12;
      if (sentFor <= 0) sent.setEnabled(false);
    }
    if (releaseIn > 0) {
      releaseIn -= dt;
      if (releaseIn <= 0 && agentOut) {
        hooks.releaseAgent();
        agentOut = false;
      }
    }
    if (!current) {
      greeterWait -= dt;
      if (greeterWait <= 0) {
        greeterWait = 10 + r() * 10;
        ameliaTurn = !ameliaTurn;
        if (ameliaTurn) {
          taskIndex = (taskIndex + 1) % ADVISER.tasks.length;
          advise(ADVISER.tasks[taskIndex]);
        } else greet(r() > 0.5 ? "Tidying the front desk" : "Checking the tram times");
      }
    }
    if (greeterSays > 0) {
      greeterSays -= dt;
      if (greeterSays <= 0) greeter.bubble.hide();
    }

    const v = current;
    switch (phase) {
      case "away":
        wait -= dt;
        if (wait <= 0 && !scripted) begin(dropIns[dropIndex++ % dropIns.length], TRAM_FROM);
        return;
      case "riding-in":
        if (!v) return;
        if (tramAtStop()) {
          tram.wantOpen = true;
          if (tram.open > 0.85) {
            hop = 0;
            v.ch.root.position.set(DOOR[0], PATH_Y + 0.2, DOOR[1]);
            v.ch.root.rotation.y = 0;
            showVisitor(v, true);
            followCompanion(v);
            phase = "stepping-off";
            greet("Welcome to Life Advice Guru!");
          }
        }
        return;
      case "riding-down":
        // The glass lift brings them down; its trip moves them on to "walking-in".
        return;
      case "stepping-off": {
        if (!v) return;
        hop = Math.min(1, hop + dt / 0.6);
        v.ch.root.position.set(
          DOOR[0] + (OFF[0] - DOOR[0]) * hop,
          PATH_Y + 0.2 + (PLATFORM_Y - PATH_Y - 0.2) * hop + Math.sin(Math.PI * hop) * 0.4,
          DOOR[1] + (OFF[1] - DOOR[1]) * hop,
        );
        followCompanion(v);
        if (hop >= 1) {
          route = WALK_IN.slice(1);
          setWalking(v, true);
          greeter.bubble.hide();
          advise(v.welcome, 3.6);
          callAgent();
          // The tram moves on once they are clear of the doors.
          leave();
          phase = "walking-in";
        }
        return;
      }
      case "walking-in":
        if (!v) return;
        v.ch.update(dt);
        v.companion?.ch.update(dt);
        // Down from the party: Amelia welcomes them once they are on their way across the forecourt.
        if (byLift && !welcomed && v.ch.root.position.x > LIFT.x + 10) {
          welcomed = true;
          greeter.bubble.hide();
          advise(v.welcome, 3.6);
        }
        if (walk(v, dt)) startSigning(v, 3.3);
        return;
      case "signing": {
        if (!v) return;
        if (wait > 0.9) {
          wait -= dt;
          pen.position.set(dx - 0.36 + Math.sin(clock * 9) * 0.1, y + 1.24, dz - 0.36 + Math.cos(clock * 7) * 0.03);
          pen.rotation.y = 0.6;
          return;
        }
        if (pen.isEnabled()) {
          pen.setEnabled(false);
          paintDoc(v.doc, v.side, true);
        }
        // The Fact Find Extraction agent scans the signed form once it is at the desk.
        if (!beam.isEnabled()) {
          if (!rushed && late < 6 && !hooks.agentReady()) {
            if (late === 0) greet("Our Fact Find agent is on its way");
            late += dt;
            return;
          }
          beam.setEnabled(true);
        }
        wait -= dt;
        beam.position.z = dz - 0.1 + Math.sin((0.9 - wait) * 7) * 0.3;
        if (wait <= 0) {
          beam.setEnabled(false);
          sent.setEnabled(true);
          sentFor = 2.5;
          releaseIn = 1.6;
          v.bubble.hide();
          greet(v.kind === "drop-in" ? "All done, thank you" : "Thank you, it’s on its way");
          signedFlag = true;
          wait = 2.2;
          phase = "signed";
        }
        return;
      }
      case "signed":
        if (!v || scripted) return;
        wait -= dt;
        if (wait <= 0) {
          greeter.bubble.hide();
          clearLinks();
          route = [...WALK_IN].reverse().slice(1);
          setWalking(v, true);
          // The next tram is on its way.
          runTram(TRAM_STOP_X, TRAM_FROM);
          phase = "walking-out";
        }
        return;
      case "walking-out":
        if (!v) return;
        v.ch.update(dt);
        v.companion?.ch.update(dt);
        if (walk(v, dt)) {
          setWalking(v, false);
          v.ch.root.rotation.y = Math.PI;
          followCompanion(v);
          doc.setEnabled(false);
          phase = "waiting";
        }
        return;
      case "waiting":
        if (!v) return;
        if (tramAtStop()) {
          tram.wantOpen = true;
          if (tram.open > 0.85) {
            hop = 0;
            phase = "boarding";
          }
        }
        return;
      case "boarding": {
        if (!v) return;
        hop = Math.min(1, hop + dt / 0.6);
        v.ch.root.position.set(
          OFF[0] + (DOOR[0] - OFF[0]) * hop,
          PLATFORM_Y + Math.sin(Math.PI * hop) * 0.4,
          OFF[1] + (DOOR[1] - OFF[1]) * hop,
        );
        followCompanion(v);
        if (hop >= 1) {
          showVisitor(v, false);
          leave();
          phase = "riding-out";
        }
        return;
      }
      case "riding-out":
        if (!tramShown || tram.x > TRAM_STOP_X + 20) {
          reset();
          wait = 5 + r() * 7;
        }
        return;
    }
  };

  return {
    root,
    anchors,
    tramStop: {
      get standing(): boolean {
        return tramAtStop();
      },
      get open(): boolean {
        return tramAtStop() && tram.open > 0.85;
      },
      hold(key: string, on: boolean): void {
        if (on && !holds.has(key)) holds.set(key, clock);
        else if (!on) holds.delete(key);
      },
    },
    get signed(): boolean {
      return signedFlag;
    },
    get timeScale(): number {
      return timeScale.value;
    },
    set timeScale(s: number) {
      timeScale.value = s;
    },
    update(dt: number): void {
      if (!active) return;
      step(scripted ? dt * timeScale.value : dt);
    },
    arrive(id: ScenarioId): void {
      dropBack();
      reset();
      scripted = true;
      signedFlag = false;
      timeScale.value = 1;
      const arrival = ARRIVALS[id];
      const v = arrival.visitor === "impostor" ? impostor : clients.get(id);
      if (!v) return;
      if (arrival.by === "lift") {
        // No tram for this one: the stop stays quiet while they come down from the party.
        showTram(false);
        beginByLift(v);
      } else begin(v, SCRIPTED_FROM);
    },
    fastForward(): void {
      const v = current;
      if (!v || !scripted) return;
      if (phase === "riding-down") lift.flush();
      if (phase === "riding-in" || phase === "stepping-off" || phase === "walking-in") {
        if (!byLift) {
          tram.x = TRAM_STOP_X;
          tram.v = 0;
          runTram(TRAM_TO);
        }
        route = [];
        const [sx, sz] = SIGN_AT;
        v.ch.root.position.set(sx, y, sz);
        showVisitor(v, true);
        callAgent();
        rushed = true;
        startSigning(v, 1.3);
      } else if (phase === "signing") {
        rushed = true;
        wait = Math.min(wait, 0.5);
      }
    },
    ambient(): ScenarioId | null {
      if (!scripted) return null;
      const v = current;
      // A client who came down from the party, and has made it to the street, goes back up the same way.
      const goingBack = byLift && v?.scenario && phase !== "riding-down" ? v : null;
      if (goingBack) {
        current = null;
        liftTrip = null;
      }
      reset();
      showTram(false);
      scripted = false;
      signedFlag = false;
      wait = 4;
      if (!goingBack?.scenario) return null;
      returnUp(goingBack);
      return goingBack.scenario;
    },
    setActive(on: boolean): void {
      active = on;
      tram.node.setEnabled(on && tramShown);
    },
    visitor(): VisitorInfo | null {
      if (!current || phase === "away") return null;
      const v = current;
      return {
        kind: v.kind,
        palette: v.palette,
        name: v.name,
        doc: v.doc,
        side: v.side,
        summary: v.summary,
        scenario: v.scenario,
        companion: v.companion?.info.name,
        status: phase === "signing" ? `Signing the ${v.doc}` : STATUS[phase],
      };
    },
    adviserTask(): string {
      const v = current;
      if (!v || phase === "away") return ADVISER.tasks[taskIndex];
      const who =
        v.kind === "client" ? v.name.split(" ")[0] : v.kind === "impostor" ? "the visitor" : v.name.charAt(0).toLowerCase() + v.name.slice(1);
      switch (phase) {
        case "riding-in":
        case "stepping-off":
          return "Watching for the tram";
        case "riding-down":
          return `Watching for ${who} to come down from the party`;
        case "walking-in":
          return `Welcoming ${who}`;
        case "signing":
        case "signed":
          return linked.length ? `Linking ${who} with the ${agentName(linked[linked.length - 1])}` : `Helping ${who} get started`;
        default:
          return `Seeing ${who} off`;
      }
    },
    setHighlights(h: Highlights): void {
      ring.setEnabled(h.desk);
      greeter.ch.setHighlight(h.desk, GOLD);
      amelia.ch.setHighlight(h.adviser || h.desk, GOLD);
    },
  };
}

