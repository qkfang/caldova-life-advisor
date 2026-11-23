/**
 * The glass lift on the front-left corner of Guru House, beside the tram stop: an open frame of
 * dark steel with brass bands, from the forecourt up to the party on the roof. A car with lit
 * gold bands and brass rails rides up and down inside it. Brass cage doors, like the ones on the
 * lift inside, open to the street at the bottom and onto a short bridge to the roof deck at the
 * top, under a copper cap. A sign over the street doors reads ROOF PARTY ↑.
 *
 * Party guests ride it up from the tram and back down to catch the tram home (partyTrips.ts), and
 * in the finale Priya, and George with Harpreet, ride it down to the front desk (frontDesk.ts).
 *
 * The frame is built in four parts, one beside each storey and one on the roof, that lift away and
 * fade with their levels, so the lift is cut like the house in every chapter. The car hides while
 * it is in a part that has lifted away. There are no glass panes and no glow: an open frame keeps
 * it crisp, and the riders always show.
 */
import { Color3, type Mesh, MeshBuilder, TransformNode } from "@babylonjs/core";
import type { VoxelCharacter } from "../voxelCharacter";
import { heritageMats } from "./heritage";
import { Batch, type Bubble, type Kit } from "./kit";
import type { LevelStack } from "./levelStack";
import { FLOOR_Y, HOUSE, LIFT, ROOF_Y, SLAB } from "./layout";
import type { GroupId } from "./types";

export type LiftLevel = "street" | "roof";
type Pt = readonly [number, number];

/** Floor heights of the two landings: the forecourt, and the party deck on the roof. */
export const LIFT_FLOOR: Record<LiftLevel, number> = { street: 0.16, roof: ROOF_Y + SLAB + 0.18 };

const X0 = LIFT.x - LIFT.w / 2;
const X1 = LIFT.x + LIFT.w / 2;
const Z0 = LIFT.z0;
const Z1 = LIFT.z1;
const CZ = (Z0 + Z1) / 2;
const CAR_H = 3.3;
/** Top of the frame, under the copper cap. */
const TOP = LIFT_FLOOR.roof + CAR_H + 1.0;
/** The car: top speed (quick enough that the ride up the tall house still takes about five seconds), acceleration, and how long the doors take. */
const MAX_V = 6;
const ACCEL = 3;
const DOOR_TIME = 0.7;
const WALK = 2.0;
const GOLD = Color3.FromHexString("#ffc35a");

/** Someone riding the lift. */
export interface LiftRider {
  /** The node that moves the rider about; it rides in the car. */
  node: TransformNode;
  ch: VoxelCharacter;
  name: string;
  bubble?: Bubble;
}

export interface LiftTrip {
  from: LiftLevel;
  riders: LiftRider[];
  /** Said by the first rider as they step in. */
  say?: string;
  /** Said by the first rider as they step out. */
  sayOut?: string;
  /** The riders are already in the car, with its doors open at `from` (the finale's client). */
  aboard?: boolean;
  /** Ride before anyone else waiting. */
  first?: boolean;
  /** Playback speed for this trip (the finale's x1, x2, x4; 0 holds it). */
  speed?: () => number;
  /** The riders have stepped out at the other landing (on the street, or the roof deck). */
  done(): void;
}

export interface Lift {
  readonly root: TransformNode;
  /** A point on the lift for beams and the card's framing. */
  readonly anchor: TransformNode;
  /** Where rider `i` waits for the car at a landing (world x, z), facing the doors. */
  waitAt(level: LiftLevel, i: number): Pt;
  /** Where rider `i` steps out to at a landing. */
  exitAt(level: LiftLevel, i: number): Pt;
  /** Facing (rotation.y) for someone waiting at a landing. */
  waitFace(level: LiftLevel): number;
  call(trip: LiftTrip): void;
  /** Drop a trip: its riders are left where they are, for their owner to deal with. */
  cancel(trip: LiftTrip): void;
  /** Finish every trip at once, with the riders put down at their exits. */
  flush(): void;
  /** Freeze the lift while the street in front is hidden. */
  setActive(on: boolean): void;
  update(dt: number): void;
  setHighlight(on: boolean): void;
  /** Who is riding right now. */
  riders(): string[];
  /** What the lift is doing right now, for its card. */
  now(): string;
}

type Stage = "fetch" | "open-in" | "board" | "close-in" | "travel" | "open-out" | "alight" | "close-out";

interface Walker {
  rider: LiftRider;
  slot: Pt;
  route: Pt[];
  y: number;
}

interface Part {
  group: GroupId;
  node: TransformNode;
  meshes: Mesh[];
  y0: number;
  y1: number;
  alpha: number;
}

const names = (riders: LiftRider[]): string => {
  const n = riders.map((r) => r.name).filter(Boolean);
  return n.length > 1 ? `${n.slice(0, -1).join(", ")} and ${n[n.length - 1]}` : (n[0] ?? "");
};

/** Places a node under `parent` at house-frame coordinates (every parent here sits at the origin except the car). */
function put(node: TransformNode, parent: TransformNode, x: number, y: number, z: number, origin = { x: 0, y: 0, z: 0 }): void {
  node.parent = parent;
  node.position.set(x - origin.x, y - origin.y, z - origin.z);
}

export function buildLift(kit: Kit, parent: TransformNode, roofGround: TransformNode, stack: LevelStack, streetAlpha: () => number): Lift {
  const scene = kit.scene;
  const root = new TransformNode("v2_lift", scene);
  root.parent = parent;
  const hm = heritageMats(kit);
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  const brass = kit.mat("#c9a14a", { specular: 0.5 });
  const pale = kit.mat("#e9edf2");

  /* ---------------- The frame, in four parts ---------------- */
  const bounds: Array<[GroupId, number, number]> = [
    ["agents", 0, FLOOR_Y.people - SLAB],
    ["people", FLOOR_Y.people - SLAB, FLOOR_Y.idea - SLAB],
    ["idea", FLOOR_Y.idea - SLAB, ROOF_Y],
    ["roof", ROOF_Y, TOP + 2],
  ];
  const parts: Part[] = [];
  const ring = (node: TransformNode, y: number, t: number, mat: typeof steel, skipFront = false): void => {
    if (!skipFront) kit.block(node, LIFT.x, y, Z0, LIFT.w, t, t, mat);
    kit.block(node, LIFT.x, y, Z1, LIFT.w, t, t, mat);
    kit.block(node, X0, y, CZ, t, t, Z1 - Z0, mat);
    kit.block(node, X1, y, CZ, t, t, Z1 - Z0, mat);
  };
  for (const [group, y0, y1] of bounds) {
    const node = new TransformNode(`v2_lift_${group}`, scene);
    node.parent = root;
    const batch = new Batch(node, `lift_${group}`);
    const prev = kit.useBatch(batch);
    const top = group === "roof" ? TOP : y1;
    // Corner columns, and brass bands where the floors are, tied back to the house.
    for (const x of [X0, X1]) for (const z of [Z0, Z1]) kit.block(node, x, y0, z, 0.24, top - y0, 0.24, steel);
    if (group !== "roof") {
      ring(node, y1 - 0.16, 0.16, brass);
      for (const x of [X0 + 0.3, X1 - 0.3]) kit.block(node, x, y1 - 0.16, (Z1 + HOUSE.z0) / 2, 0.16, 0.16, HOUSE.z0 - Z1, steel);
    }
    if (group === "people" || group === "idea") ring(node, (y0 + y1) / 2, 0.1, steel);
    // Thin mullions down the middle of each side.
    for (const x of [X0, X1]) kit.block(node, x, y0, CZ, 0.06, top - y0, 0.06, steel);
    if (group === "agents") {
      // The street landing: bluestone footings, a stone sill, steel cladding beside brass jambs, and a canopy over the doors.
      for (const x of [X0, X1]) for (const z of [Z0, Z1]) kit.block(node, x, 0, z, 0.5, 0.32, 0.5, hm.bluestone);
      kit.block(node, LIFT.x, 0.15, Z0 - 0.35, 2.4, 0.02, 0.5, hm.stone);
      for (const side of [-1, 1]) {
        kit.block(node, LIFT.x + side * 1.28, LIFT_FLOOR.street, Z0 + 0.03, 0.44, 3.55, 0.1, steel);
        kit.block(node, LIFT.x + side * 1.04, LIFT_FLOOR.street, Z0 - 0.04, 0.1, 3.55, 0.14, brass);
      }
      kit.block(node, LIFT.x, LIFT_FLOOR.street + 3.55, Z0 - 0.04, LIFT.w, 0.2, 0.16, brass);
      kit.block(node, LIFT.x, LIFT_FLOOR.street + 3.75, Z0 - 0.45, LIFT.w + 0.3, 0.14, 0.95, steel);
      kit.block(node, LIFT.x, LIFT_FLOOR.street + 3.73, Z0 - 0.93, LIFT.w + 0.3, 0.06, 0.06, kit.mat("#ffc35a", { unlit: true }));
    }
    if (group === "roof") {
      const deck = LIFT_FLOOR.roof;
      // The roof landing: cladding and brass jambs on the back, the bridge to the deck, and the copper cap.
      for (const side of [-1, 1]) {
        kit.block(node, LIFT.x + side * 1.28, deck, Z1 - 0.03, 0.44, 3.55, 0.1, steel);
        kit.block(node, LIFT.x + side * 1.04, deck, Z1 + 0.04, 0.1, 3.55, 0.14, brass);
      }
      kit.block(node, LIFT.x, deck + 3.55, Z1 + 0.04, LIFT.w, 0.2, 0.16, brass);
      const bridgeZ1 = HOUSE.z0 + 0.75;
      kit.block(node, LIFT.x, deck - 0.2, (Z1 + bridgeZ1) / 2, 2.3, 0.2, bridgeZ1 - Z1, steel);
      kit.block(node, LIFT.x, deck - 0.02, (Z1 + bridgeZ1) / 2, 2.2, 0.02, bridgeZ1 - Z1, kit.mat("#ffffff", { texture: "oak" }));
      for (const side of [-1, 1]) {
        const x = LIFT.x + side * 1.1;
        for (const z of [Z1 + 0.1, bridgeZ1 - 0.1]) kit.block(node, x, deck, z, 0.1, 1.1, 0.1, hm.iron);
        kit.block(node, x, deck + 1.0, (Z1 + bridgeZ1) / 2, 0.1, 0.1, bridgeZ1 - Z1, hm.iron);
      }
      ring(node, TOP - 0.3, 0.3, steel);
      kit.block(node, LIFT.x, TOP, CZ, LIFT.w + 0.3, 0.16, Z1 - Z0 + 0.3, brass);
      const cap = kit.cyl(node, LIFT.x, TOP + 0.16, CZ, (LIFT.w + 0.2) * Math.SQRT2, 1.1, hm.copper, { top: 0.01, tess: 4 });
      cap.rotation.y = Math.PI / 4;
      kit.cyl(node, LIFT.x, TOP + 1.2, CZ, 0.12, 0.5, hm.gold, { tess: 8 });
    }
    kit.useBatch(prev);
    const meshes = batch.flush();
    parts.push({ group, node, meshes, y0, y1, alpha: 1 });
  }
  const street = parts[0];
  const roofPart = parts[3];

  // The sign over the street doors.
  const sign = kit.label(street.node, {
    lines: [
      { text: "ROOF PARTY ↑", size: 62, weight: 800, color: "#0f1a2e" },
      { text: "Glass lift to the roof", size: 30, weight: 600, color: "#5b6476" },
    ],
    width: 3.3,
    px: 600,
    bg: "rgba(255,251,242,0.98)",
    accent: "#c9a14a",
    bar: "top",
  });
  sign.position.set(LIFT.x, LIFT_FLOOR.street + 4.55, Z0 - 0.94);

  /** A pair of brass cage doors on a landing, sliding apart along x. */
  const doorsAt = (node: TransformNode, y: number, z: number): Mesh[] => {
    const leaves: Mesh[] = [];
    for (const side of [-1, 1]) {
      const leaf = kit.block(node, LIFT.x + side * 0.5, y, z, 0.98, 0.08, 0.05, brass, { dynamic: true });
      kit.block(leaf, 0, 3.32, 0, 0.98, 0.08, 0.05, brass, { dynamic: true });
      for (let i = 0; i < 5; i++) kit.block(leaf, -0.44 + i * 0.22, 0.08, 0, 0.05, 3.24, 0.05, brass, { dynamic: true });
      for (const h of [1.15, 2.3]) kit.block(leaf, 0, h, 0, 0.98, 0.05, 0.05, brass, { dynamic: true });
      leaves.push(leaf);
    }
    return leaves;
  };
  const doors: Record<LiftLevel, { leaves: Mesh[]; open: number }> = {
    street: { leaves: doorsAt(street.node, LIFT_FLOOR.street, Z0 - 0.12), open: 0 },
    roof: { leaves: doorsAt(roofPart.node, LIFT_FLOOR.roof, Z1 + 0.12), open: 0 },
  };
  for (const p of parts) p.meshes = p.node.getChildMeshes(false) as Mesh[];

  /* ---------------- The car ---------------- */
  const car = new TransformNode("v2_liftCar", scene);
  car.parent = root;
  const carBatch = new Batch(car, "liftCar");
  const prevBatch = kit.useBatch(carBatch);
  const cw = LIFT.w - 0.5;
  const glow = kit.mat("#ffd27a", { unlit: true });
  kit.block(car, 0, -0.16, 0, cw, 0.16, cw, steel);
  kit.block(car, 0, -0.02, 0, cw - 0.1, 0.03, cw - 0.1, kit.mat("#5b6476"));
  // A lit band round the floor and the roof, so the car reads as it rides.
  for (const yy of [-0.12, CAR_H + 0.16]) {
    for (const z of [-cw / 2, cw / 2]) kit.block(car, 0, yy, z, cw + 0.12, 0.1, 0.08, glow);
    for (const x of [-cw / 2, cw / 2]) kit.block(car, x, yy, 0, 0.08, 0.1, cw + 0.12, glow);
  }
  kit.block(car, 0, CAR_H, 0, cw + 0.1, 0.16, cw + 0.1, pale);
  kit.block(car, 0, CAR_H - 0.04, 0, cw - 0.6, 0.04, 0.3, kit.mat("#fff6dd", { unlit: true }));
  for (const x of [-cw / 2, cw / 2]) {
    for (const z of [-cw / 2, cw / 2]) kit.block(car, x, 0, z, 0.14, CAR_H, 0.14, brass);
    // The car's sides: brass rails and a handrail, open to see the riders through.
    for (const h of [1.0, 2.2]) kit.block(car, x, h, 0, 0.07, 0.07, cw, brass);
    kit.block(car, x * 0.94, 1.05, 0, 0.08, 0.08, cw - 0.3, kit.mat("#f4f1ea"));
    kit.block(car, x, 0, 0, 0.05, CAR_H, 0.05, brass);
  }
  kit.useBatch(prevBatch);
  carBatch.flush();
  let carY = LIFT_FLOOR.street;
  let carV = 0;
  car.position.set(LIFT.x, carY, CZ);

  // The selection ring on the street landing, and the click target round the whole frame.
  const selRing = MeshBuilder.CreateTorus("v2_liftRing", { diameter: 4.2, thickness: 0.14, tessellation: 36 }, scene);
  selRing.position.set(LIFT.x, LIFT_FLOOR.street + 0.06, Z0 - 0.9);
  kit.finish(selRing, root, kit.mat("#ffe08a", { unlit: true }), true);
  selRing.setEnabled(false);
  const hit = kit.hitbox(root, LIFT.x, 0, CZ - 0.3, LIFT.w + 0.8, TOP + 1.4, Z1 - Z0 + 1.2, { type: "lift" });
  const anchor = new TransformNode("v2_liftAnchor", scene);
  anchor.parent = root;
  anchor.position.set(LIFT.x, 0, CZ);

  /* ---------------- Trips ---------------- */
  const queue: LiftTrip[] = [];
  let trip: LiftTrip | null = null;
  let stage: Stage = "fetch";
  let stageT = 0;
  let walkers: Walker[] = [];
  let active = true;
  let highlight = false;
  let where: LiftLevel = "street";
  const other = (l: LiftLevel): LiftLevel => (l === "street" ? "roof" : "street");
  const ground = (l: LiftLevel): TransformNode => (l === "street" ? root : roofGround);
  /** The doorway's threshold on each landing, in front of a car slot. */
  const doorway = (l: LiftLevel, sx: number): Pt => [LIFT.x + sx, l === "street" ? Z0 - 0.35 : Z1 + 0.35];
  const slotsFor = (n: number): Pt[] => (n > 1 ? [[-0.55, 0.1], [0.55, -0.1]] : [[0, 0]]);
  const exitAt = (l: LiftLevel, i: number): Pt => [LIFT.x + (i % 2 ? 0.6 : -0.6), l === "street" ? Z0 - 1.5 : HOUSE.z0 + 1.35];
  const waitAt = (l: LiftLevel, i: number): Pt => [LIFT.x + (i % 2 ? 0.6 : -0.6), l === "street" ? Z0 - 1.0 : HOUSE.z0 + 1.15];
  const waitFace = (l: LiftLevel): number => (l === "street" ? 0 : Math.PI);
  const carOrigin = () => ({ x: LIFT.x, y: carY, z: CZ });
  const lightRiders = (riders: LiftRider[], on: boolean): void => {
    for (const r of riders) r.ch.setHighlight(on, GOLD);
  };
  let talk: { bubble: Bubble; t: number } | null = null;
  const say = (bubble: Bubble | undefined, text: string | undefined): void => {
    if (!bubble || !text) return;
    talk?.bubble.hide();
    bubble.say(text);
    talk = { bubble, t: 3.2 };
  };

  const startStage = (s: Stage): void => {
    stage = s;
    stageT = 0;
    if (!trip) return;
    const from = trip.from;
    const to = other(from);
    const slots = slotsFor(trip.riders.length);
    if (s === "board") {
      walkers = trip.riders.map((rider, i) => {
        const [sx, sz] = slots[i];
        return { rider, slot: [sx, sz], route: [doorway(from, sx), [LIFT.x + sx, CZ + sz]], y: LIFT_FLOOR[from] };
      });
      for (const w of walkers) {
        // Walk in from wherever they are standing on the landing.
        const p = w.rider.node.position;
        const o = w.rider.node.parent === car ? carOrigin() : { x: 0, y: 0, z: 0 };
        put(w.rider.node, ground(from), p.x + o.x, w.y, p.z + o.z);
        w.rider.ch.setWalking(true);
      }
      if (trip.say) say(trip.riders[0]?.bubble, trip.say);
    } else if (s === "alight") {
      walkers = trip.riders.map((rider, i) => {
        const [sx, sz] = slots[i];
        const out = exitAt(to, i);
        return { rider, slot: [sx, sz], route: [doorway(to, sx), out], y: LIFT_FLOOR[to] };
      });
      for (const w of walkers) {
        put(w.rider.node, ground(to), LIFT.x + w.slot[0], w.y, CZ + w.slot[1]);
        w.rider.ch.setWalking(true);
      }
      say(trip.riders[0]?.bubble, trip.sayOut);
    }
  };

  const seat = (t: LiftTrip): void => {
    // Into the car, facing the doors they will leave by.
    slotsFor(t.riders.length).forEach(([sx, sz], i) => {
      const r = t.riders[i];
      put(r.node, car, LIFT.x + sx, carY, CZ + sz, carOrigin());
      r.node.rotation.y = t.from === "street" ? 0 : Math.PI;
      r.ch.setWalking(false);
    });
  };

  const begin = (t: LiftTrip): void => {
    trip = t;
    lightRiders(t.riders, highlight);
    if (t.aboard) {
      carY = LIFT_FLOOR[t.from];
      carV = 0;
      where = t.from;
      doors.street.open = t.from === "street" ? 1 : 0;
      doors.roof.open = t.from === "roof" ? 1 : 0;
      seat(t);
      say(t.riders[0]?.bubble, t.say);
      startStage("close-in");
      stageT = -1.2;
      return;
    }
    startStage(where === t.from && Math.abs(carY - LIFT_FLOOR[t.from]) < 0.01 ? "open-in" : "fetch");
  };

  const finish = (): void => {
    const t = trip;
    trip = null;
    walkers = [];
    if (!t) return;
    lightRiders(t.riders, false);
    t.done();
  };

  /** Moves the car toward a landing; true once it is there. */
  const drive = (to: LiftLevel, dt: number): boolean => {
    const target = LIFT_FLOOR[to];
    const dist = target - carY;
    if (Math.abs(dist) < 0.002) {
      carY = target;
      carV = 0;
      where = to;
      return true;
    }
    const want = Math.min(MAX_V, Math.sqrt(2 * ACCEL * Math.abs(dist)));
    carV += Math.sign(want - carV) * Math.min(Math.abs(want - carV), ACCEL * 2 * dt);
    carY += Math.sign(dist) * Math.min(Math.abs(dist), Math.max(0.08, carV) * dt);
    return false;
  };

  /** Walks the riders along their routes; true once they are all there. */
  const walkAll = (dt: number): boolean => {
    let all = true;
    for (const w of walkers) {
      if (!w.route.length) continue;
      const node = w.rider.node;
      let step = WALK * dt;
      while (step > 0 && w.route.length) {
        const [tx, tz] = w.route[0];
        const dx = tx - node.position.x;
        const dz = tz - node.position.z;
        const d = Math.hypot(dx, dz);
        if (d <= step) {
          node.position.x = tx;
          node.position.z = tz;
          step -= d;
          w.route.shift();
        } else {
          node.position.x += (dx / d) * step;
          node.position.z += (dz / d) * step;
          node.rotation.y = Math.atan2(dx, dz);
          step = 0;
        }
      }
      node.position.y = w.y;
      w.rider.ch.update(dt);
      if (w.route.length) all = false;
      else w.rider.ch.setWalking(false);
    }
    return all;
  };

  const step = (dt: number): void => {
    if (!trip) {
      const next = queue.shift();
      if (next) begin(next);
      // Idle doors drift shut.
      for (const d of Object.values(doors)) d.open = Math.max(0, d.open - dt / DOOR_TIME);
      return;
    }
    const t = trip;
    const sdt = dt * (t.speed?.() ?? 1);
    if (sdt <= 0) return;
    stageT += sdt;
    const from = t.from;
    const to = other(from);
    for (const r of t.riders) r.ch.update(sdt);
    switch (stage) {
      case "fetch":
        for (const d of Object.values(doors)) d.open = Math.max(0, d.open - sdt / DOOR_TIME);
        if (doors.street.open + doors.roof.open === 0 && drive(from, sdt)) startStage("open-in");
        return;
      case "open-in":
        doors[from].open = Math.min(1, doors[from].open + sdt / DOOR_TIME);
        if (doors[from].open >= 1) startStage("board");
        return;
      case "board":
        if (walkAll(sdt)) {
          seat(t);
          startStage("close-in");
        }
        return;
      case "close-in":
        if (stageT < 0) return;
        doors[from].open = Math.max(0, doors[from].open - sdt / DOOR_TIME);
        if (doors[from].open <= 0) startStage("travel");
        return;
      case "travel":
        if (drive(to, sdt)) startStage("open-out");
        return;
      case "open-out":
        doors[to].open = Math.min(1, doors[to].open + sdt / DOOR_TIME);
        if (doors[to].open >= 1) startStage("alight");
        return;
      case "alight":
        if (walkAll(sdt)) {
          for (const w of walkers) w.rider.node.rotation.y = to === "street" ? Math.PI : 0;
          startStage("close-out");
          finish();
        }
        return;
      case "close-out":
        return;
    }
  };

  /** Shows each part of the frame as its level shows, and the car with whichever part it is in. */
  const fade = (): void => {
    const s = streetAlpha();
    let visibleTop = 0;
    for (const p of parts) {
      // Written every frame: the street's own fade writes these meshes too, and this has the last word.
      const a = stack.alpha(p.group) * s;
      p.node.position.y = stack.offset(p.group);
      p.alpha = a;
      for (const m of p.meshes) m.visibility = a;
      p.node.setEnabled(a > 0.01);
      if (a > 0.5 && stack.offset(p.group) < 0.5) visibleTop = Math.max(visibleTop, p.group === "roof" ? TOP + 1.4 : p.y1);
    }
    const mid = carY + CAR_H / 2;
    const at = parts.find((p) => mid >= p.y0 && mid < p.y1) ?? street;
    car.setEnabled(at.alpha > 0.5 && stack.offset(at.group) < 0.5);
    car.position.y = carY;
    for (const d of Object.values(doors)) {
      const e = d.open * d.open * (3 - 2 * d.open);
      d.leaves.forEach((leaf, i) => (leaf.position.x = LIFT.x + (i ? 1 : -1) * (0.5 + e * 0.92)));
    }
    hit.scaling.y = Math.max(0.05, visibleTop / (TOP + 1.4));
    hit.position.y = (TOP + 1.4) * hit.scaling.y * 0.5;
  };
  fade();

  return {
    root,
    anchor,
    waitAt,
    exitAt,
    waitFace,
    call(t: LiftTrip): void {
      if (t.first) queue.unshift(t);
      else queue.push(t);
    },
    cancel(t: LiftTrip): void {
      const i = queue.indexOf(t);
      if (i >= 0) queue.splice(i, 1);
      if (trip !== t) return;
      for (const r of t.riders) {
        if (r.node.parent === car) {
          const p = r.node.position;
          put(r.node, root, p.x + LIFT.x, p.y + carY, p.z + CZ);
        }
        r.ch.setWalking(false);
        r.ch.setHighlight(false, GOLD);
      }
      trip = null;
      walkers = [];
    },
    flush(): void {
      const all = [...(trip ? [trip] : []), ...queue.splice(0)];
      trip = null;
      walkers = [];
      for (const t of all) {
        const to = other(t.from);
        t.riders.forEach((r, i) => {
          const [x, z] = exitAt(to, i);
          put(r.node, ground(to), x, LIFT_FLOOR[to], z);
          r.node.rotation.y = to === "street" ? Math.PI : 0;
          r.ch.setWalking(false);
          r.ch.setHighlight(false, GOLD);
        });
        carY = LIFT_FLOOR[to];
        carV = 0;
        where = to;
        t.done();
      }
      doors.street.open = 0;
      doors.roof.open = 0;
      fade();
    },
    setActive(on: boolean): void {
      active = on;
    },
    update(dt: number): void {
      if (active) {
        step(dt);
        if (talk && (talk.t -= dt) <= 0) {
          talk.bubble.hide();
          talk = null;
        }
      }
      fade();
      if (selRing.isEnabled()) selRing.scaling.setAll(1 + Math.sin(performance.now() / 160) * 0.05);
    },
    setHighlight(on: boolean): void {
      highlight = on;
      selRing.setEnabled(on);
      if (trip) lightRiders(trip.riders, on);
    },
    riders(): string[] {
      return trip && stage !== "fetch" && stage !== "close-out" ? trip.riders.map((r) => r.name).filter(Boolean) : [];
    },
    now(): string {
      if (!trip) return queue.length ? `Coming for ${names(queue[0].riders)}` : `Waiting ${where === "street" ? "at the street" : "on the roof"}`;
      const who = names(trip.riders);
      const up = trip.from === "street";
      switch (stage) {
        case "fetch":
          return `On its way ${up ? "down" : "up"} for ${who}`;
        case "open-in":
        case "board":
          return `${who} stepping in`;
        case "close-in":
        case "travel":
          return `Going ${up ? "up" : "down"} with ${who}`;
        default:
          return `${who} stepping out ${up ? "on the roof" : "at the street"}`;
      }
    },
  };
}
