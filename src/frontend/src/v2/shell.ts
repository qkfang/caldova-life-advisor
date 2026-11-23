/**
 * The building shell shared by every level of Caldova House: the floor slab,
 * the 1889 brick walls on three sides, the lift core and the level tag.
 *
 * The front of the house is cut away, dollhouse style, so every level reads
 * from the street. Along the cut, each floor slab shows a cream render band
 * with dentils and a slim edge strip in the level's accent colour: the new
 * floors inside the old shell. The side street facade (right) and the back
 * and laneway walls carry arched windows with keystones, pilasters, a
 * bluestone base on the ground floor and render quoins at the corners.
 * Inside, the walls are exposed brick and the windows glow with daylight.
 */
import type { Mesh, StandardMaterial, TransformNode } from "@babylonjs/core";
import type { Kit } from "./kit";
import type { LevelInfo } from "./houseData";
import { archedWindow, cornice, dentils, heritageMats, onFace, type Face } from "./heritage";
import { CORE, HOUSE, INSIDE, SLAB, STOREY, WALL_T } from "./layout";

export type Storey = "basement" | "ground" | "upper";

export interface ShellSpec {
  root: TransformNode;
  level: LevelInfo;
  floorY: number;
  storey: Storey;
  /** Floor finish on top of the slab. */
  floor: StandardMaterial;
  /** Inside finish of the walls (default: the exposed brick). */
  inner?: StandardMaterial;
  /** Window centres along each wall: x for the back wall, z for the side walls. */
  windows?: { back?: number[]; left?: number[]; right?: number[] };
  /** A slim strip of the level's accent along the top of the inside walls. */
  cove?: boolean;
  /** A slim iron guard rail along the cut, on raised floors, leaving gaps over these x ranges. */
  rail?: { skip?: Array<[number, number]> };
  core?: boolean;
  /** Where the floating level tag goes (default: beside the slab's front-left corner). */
  tagAt?: [number, number, number];
}

/** Bays along the side walls (z) and the back wall (x). */
export const SIDE_BAYS = [-10.6, -6.2, -1.8, 2.6, 7.0];
const PILASTERS_Z = [-8.4, -4.0, 0.4, 4.8];

export function buildShell(kit: Kit, s: ShellSpec): { tag: Mesh } {
  const { root, floorY: y, level } = s;
  const h = STOREY[level.id];
  const m = heritageMats(kit);
  const concrete = kit.mat("#ffffff", { texture: "concrete" });
  const led = kit.mat(level.accent, { unlit: true });
  const outerWall = s.storey === "basement" ? m.bluestone : m.brick;
  const inner = s.inner ?? outerWall;

  // Slab, floor finish and, along the cut, a render band with the level's edge strip.
  const sw = HOUSE.x1 - HOUSE.x0;
  const sd = HOUSE.z1 - HOUSE.z0;
  const scx = (HOUSE.x0 + HOUSE.x1) / 2;
  const scz = (HOUSE.z0 + HOUSE.z1) / 2;
  kit.block(root, scx, y - SLAB, scz, sw, SLAB - 0.06, sd, concrete);
  const fw = INSIDE.x1 - INSIDE.x0;
  const fd = INSIDE.z1 - INSIDE.z0;
  kit.block(root, scx, y - 0.06, (INSIDE.z0 + INSIDE.z1) / 2, fw - 0.04, 0.06, fd - 0.04, s.floor);
  const front: Face = { axis: "x", at: HOUSE.z0, out: -1 };
  if (s.storey !== "basement") {
    cornice(kit, root, front, HOUSE.x0, HOUSE.x1, y - SLAB, SLAB, 0.12, m.render);
    dentils(kit, root, front, HOUSE.x0 + 0.4, HOUSE.x1 - 0.4, y - SLAB - 0.2, 0.22, m.stone);
  }
  onFace(kit, root, front, scx, y - SLAB * 0.62, sw + 0.1, 0.13, 0.2, led);

  // The brick shell: the laneway wall (left), the back wall and the side street facade (right).
  const left = { x0: HOUSE.x0, x1: HOUSE.x0 + WALL_T };
  const right = { x0: HOUSE.x1 - WALL_T, x1: HOUSE.x1 };
  kit.block(root, (left.x0 + left.x1) / 2, y, scz, WALL_T, h, sd, inner);
  kit.block(root, (right.x0 + right.x1) / 2, y, scz, WALL_T, h, sd, inner);
  kit.block(root, scx, y, HOUSE.z1 - WALL_T / 2, sw - WALL_T * 2, h, WALL_T, inner);
  if (inner !== outerWall) {
    // A skin of the outer material on the outside faces.
    onFace(kit, root, { axis: "z", at: HOUSE.x0, out: -1 }, scz, y, sd, h, 0.04, outerWall);
    onFace(kit, root, { axis: "z", at: HOUSE.x1, out: 1 }, scz, y, sd, h, 0.04, outerWall);
    onFace(kit, root, { axis: "x", at: HOUSE.z1, out: 1 }, scx, y, sw, h, 0.04, outerWall);
  }

  const outside: Record<"left" | "right" | "back", Face> = {
    left: { axis: "z", at: HOUSE.x0, out: -1 },
    right: { axis: "z", at: HOUSE.x1, out: 1 },
    back: { axis: "x", at: HOUSE.z1, out: 1 },
  };
  const insideFace: Record<"left" | "right" | "back", Face> = {
    left: { axis: "z", at: INSIDE.x0, out: 1 },
    right: { axis: "z", at: INSIDE.x1, out: -1 },
    back: { axis: "x", at: INSIDE.z1, out: -1 },
  };

  if (s.storey !== "basement") {
    // The facades: a bluestone base on the ground floor, the floor-line cornice with dentils,
    // pilasters between the bays and render quoins at the corners.
    const ground = s.storey === "ground";
    const sill = y + (ground ? 1.55 : 1.0);
    const open = ground ? 3.0 : 2.45;
    const winW = ground ? 2.3 : 2.1;
    for (const side of ["left", "right", "back"] as const) {
      const f = outside[side];
      const along = side === "back" ? [HOUSE.x0, HOUSE.x1] : [HOUSE.z0, HOUSE.z1];
      if (ground) {
        cornice(kit, root, f, along[0], along[1], y - SLAB, 1.4 + SLAB, 0.16, m.bluestone);
        cornice(kit, root, f, along[0], along[1], y + 0.9, 0.16, 0.24, m.stone);
      } else {
        cornice(kit, root, f, along[0], along[1], y - SLAB, SLAB, 0.3, m.render);
        dentils(kit, root, f, along[0] + 0.5, along[1] - 0.5, y - SLAB - 0.22, 0.22, m.stone);
      }
      // A string course under the windows.
      cornice(kit, root, f, along[0], along[1], sill - 0.42, 0.14, 0.14, m.render);
      // Quoins: alternating long and short render blocks up each corner.
      for (const end of [along[0], along[1]]) {
        const dir = end === along[0] ? 1 : -1;
        for (let k = 0; y + 0.1 + k * 0.62 < y + h - 0.2; k++) {
          const long = k % 2 === 0;
          const qw = long ? 1.0 : 0.6;
          onFace(kit, root, f, end + dir * (qw / 2), y + 0.1 + k * 0.62, qw, 0.54, 0.1, m.render);
        }
      }
    }
    // Pilasters on the side street facade, with capitals under the next cornice.
    for (const z of PILASTERS_Z) {
      onFace(kit, root, outside.right, z, y, 0.6, h, 0.16, m.render);
      onFace(kit, root, outside.right, z, y + h - 0.4, 0.9, 0.4, 0.26, m.stone);
    }
    // The windows, outside and in.
    const wins = s.windows ?? {};
    for (const side of ["left", "right", "back"] as const) {
      for (const u of wins[side] ?? []) {
        archedWindow(kit, root, outside[side], { u, sill, w: winW, h: open, side: "outside" }, m);
        archedWindow(kit, root, insideFace[side], { u, sill, w: winW, h: open, side: "inside" }, m);
      }
    }
    // The cut ends of the side walls: render, so the section reads clearly.
    for (const x of [(left.x0 + left.x1) / 2, (right.x0 + right.x1) / 2]) {
      kit.block(root, x, y, HOUSE.z0 - 0.03, WALL_T + 0.04, h, 0.06, m.render);
    }
  }

  if (s.cove) {
    // A slim neon strip along the top of the inside walls: the new interior in the old shell.
    kit.block(root, INSIDE.x0 + 0.08, y + h - 0.35, scz, 0.08, 0.1, sd, led);
    kit.block(root, scx, y + h - 0.35, INSIDE.z1 - 0.08, fw, 0.1, 0.08, led);
  }

  if (s.rail) {
    // Runs of rail between the gaps, each with posts about every 2.4 units.
    const skips = [...(s.rail.skip ?? [])].sort((a, b) => a[0] - b[0]);
    const runs: Array<[number, number]> = [];
    let from = INSIDE.x0 + 0.1;
    for (const [a, b] of skips) {
      if (a > from) runs.push([from, a]);
      from = Math.max(from, b);
    }
    if (INSIDE.x1 - 0.1 > from) runs.push([from, INSIDE.x1 - 0.1]);
    const z = HOUSE.z0 + 0.14;
    for (const [a, b] of runs) {
      const len = b - a;
      kit.block(root, (a + b) / 2, y + 1.02, z, len, 0.08, 0.1, m.iron);
      kit.block(root, (a + b) / 2, y + 0.52, z, len, 0.05, 0.05, m.iron);
      const n = Math.max(1, Math.round(len / 2.4));
      for (let i = 0; i <= n; i++) kit.block(root, a + (len * i) / n, y, z, 0.1, 1.1, 0.1, m.iron);
    }
  }

  // Lift core with brass cage doors facing into the room, and the floor number above them.
  if (s.core !== false) {
    const cw = CORE.x1 - CORE.x0;
    const cd = CORE.z1 - CORE.z0;
    const ccz = (CORE.z0 + CORE.z1) / 2;
    kit.block(root, (CORE.x0 + CORE.x1) / 2, y, ccz, cw, h, cd, kit.mat("#f2efe8"));
    const brass = kit.mat("#c9a14a", { specular: 0.5 });
    const shaft = kit.mat("#1d2433");
    kit.block(root, CORE.x1 + 0.02, y, ccz, 0.04, 3.0, 2.0, shaft);
    for (let i = 0; i <= 8; i++) kit.block(root, CORE.x1 + 0.06, y, ccz - 1.0 + i * 0.25, 0.05, 3.0, 0.05, brass);
    kit.block(root, CORE.x1 + 0.06, y + 1.45, ccz, 0.05, 0.06, 2.0, brass);
    kit.block(root, CORE.x1 + 0.06, y + 3.0, ccz, 0.1, 0.18, 2.3, brass);
    const sign = kit.label(root, {
      lines: [{ text: level.floor, size: 120, weight: 800, color: level.accent }],
      width: 0.9,
      px: 160,
      pxHeight: 160,
      bg: "rgba(14,22,38,0.92)",
    });
    sign.position.set(CORE.x1 + 0.1, y + 3.7, ccz);
    sign.rotation.y = -Math.PI / 2;
  }

  // Floating level tag beside the building.
  const tag = kit.label(root, {
    lines: [
      { text: level.floor === "B" ? "LOWER LEVEL" : `LEVEL ${level.floor}`, size: 34, color: level.accent, weight: 700 },
      { text: level.name.toUpperCase(), size: 64, weight: 800, serif: true },
      { text: level.caption, size: 30, color: "#c6d2e6", weight: 500 },
    ],
    width: 8.4,
    px: 640,
    bg: "rgba(12,20,36,0.94)",
    accent: level.accent,
    bar: "top",
    billboard: true,
  });
  if (s.tagAt) tag.position.set(...s.tagAt);
  // Out over the footpath, in front of the café's awning and clear of the lamp posts.
  else tag.position.set(HOUSE.x0 - 5.4, y + 2.6, HOUSE.z0 - 3.4);
  return { tag };
}
