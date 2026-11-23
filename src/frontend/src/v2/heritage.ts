/**
 * The heritage kit for Caldova House: the 1889 bank's red brick, cream
 * render and bluestone, built as crisp voxel pieces on any wall face.
 *
 * - `onFace()` places a block on a wall face, so the same code dresses the
 *   side street facade, the back and the laneway wall, inside and out.
 * - `archedWindow()` builds an arched window with its render surround,
 *   keystone, sill and glazing bars. Outside, the panes are dark glass;
 *   inside, they are bright with daylight.
 * - `cornice()` and `dentils()` run the render bands along each floor line,
 *   and `balustrade()` the parapets, balconies and the dome's gallery.
 * - `lace()` paints the cast-iron lace for the verandah and the balcony.
 */
import { Material, Mesh, MeshBuilder, type StandardMaterial, type TransformNode } from "@babylonjs/core";
import type { Kit } from "./kit";

/** A wall face: the axis the wall runs along, where its face is on the other axis, and which way is out. */
export interface Face {
  axis: "x" | "z";
  at: number;
  out: 1 | -1;
}

export interface HeritageMats {
  brick: StandardMaterial;
  render: StandardMaterial;
  /** Darker cream, for keystones, dentils and capitals, so they read against the render. */
  stone: StandardMaterial;
  bluestone: StandardMaterial;
  /** Window glass seen from outside. */
  pane: StandardMaterial;
  /** Window glass seen from inside: daylight. */
  daylight: StandardMaterial;
  /** Glazing bars and window frames. */
  bar: StandardMaterial;
  /** Painted cast iron, heritage green. */
  iron: StandardMaterial;
  copper: StandardMaterial;
  copperDark: StandardMaterial;
  gold: StandardMaterial;
}

export function heritageMats(kit: Kit): HeritageMats {
  return {
    brick: kit.mat("#ffffff", { texture: "brick" }),
    render: kit.mat("#ffffff", { texture: "render" }),
    stone: kit.mat("#d9c7a3"),
    bluestone: kit.mat("#ffffff", { texture: "bluestone" }),
    pane: kit.mat("#30465f", { specular: 0.45 }),
    daylight: kit.mat("#d3ecfb", { unlit: true }),
    bar: kit.mat("#f3eee3"),
    iron: kit.mat("#1f3b31", { specular: 0.2 }),
    copper: kit.mat("#ffffff", { texture: "copper", specular: 0.25 }),
    copperDark: kit.mat("#7c4526", { specular: 0.2 }),
    gold: kit.mat("#d4a94e", { specular: 0.4 }),
  };
}

/**
 * A block on a wall face: `u` along the wall, `y` its bottom, `w` along the wall, `h` tall and
 * `d` out from the face (negative `d` goes into the wall).
 */
export function onFace(
  kit: Kit,
  parent: TransformNode,
  f: Face,
  u: number,
  y: number,
  w: number,
  h: number,
  d: number,
  mat: StandardMaterial,
): Mesh {
  const c = f.at + (f.out * d) / 2;
  return f.axis === "x" ? kit.block(parent, u, y, c, w, h, Math.abs(d), mat) : kit.block(parent, c, y, u, Math.abs(d), h, w, mat);
}

export interface WindowSpec {
  /** Centre along the wall. */
  u: number;
  /** Top of the sill. */
  sill: number;
  /** Opening width. */
  w: number;
  /** Height of the opening up to where the arch springs. */
  h: number;
  /** Outside (dark glass, render surround, keystone) or inside (daylight, plain frame). */
  side: "outside" | "inside";
}

/** Half-widths of the arch's voxel rows, bottom to top, for an arch of radius `r`. */
function archRows(r: number, rows = 3): Array<{ y: number; h: number; half: number }> {
  const out: Array<{ y: number; h: number; half: number }> = [];
  const h = r / rows;
  for (let k = 0; k < rows; k++) {
    const mid = (k + 0.5) * h;
    out.push({ y: k * h, h, half: Math.sqrt(Math.max(0, r * r - mid * mid)) });
  }
  return out;
}

/** An arched window on a wall face, with its surround, keystone, sill and glazing bars. */
export function archedWindow(kit: Kit, parent: TransformNode, f: Face, s: WindowSpec, m: HeritageMats): void {
  const r = s.w / 2;
  const top = s.sill + s.h;
  const outside = s.side === "outside";
  const glass = outside ? m.pane : m.daylight;
  const surround = outside ? m.render : m.bar;
  const rim = outside ? 0.26 : 0.16;
  // The surround: a slightly larger arched opening behind the glass.
  onFace(kit, parent, f, s.u, s.sill - 0.05, s.w + rim * 2, s.h + 0.05, 0.08, surround);
  for (const row of archRows(r + rim)) {
    onFace(kit, parent, f, s.u, top + row.y, row.half * 2, row.h, 0.08, surround);
  }
  // The glass.
  onFace(kit, parent, f, s.u, s.sill, s.w, s.h, 0.12, glass);
  for (const row of archRows(r)) onFace(kit, parent, f, s.u, top + row.y, row.half * 2, row.h, 0.12, glass);
  // Glazing bars: a mullion, a transom at the springing line and the fanlight's middle bar.
  onFace(kit, parent, f, s.u, s.sill, 0.09, s.h + r * 0.92, 0.15, m.bar);
  onFace(kit, parent, f, s.u, top - 0.05, s.w, 0.09, 0.15, m.bar);
  onFace(kit, parent, f, s.u, s.sill + s.h * 0.48, s.w, 0.07, 0.15, m.bar);
  if (outside) {
    // Keystone and sill, standing proud of the render.
    onFace(kit, parent, f, s.u, top + r - 0.12, 0.42, 0.56, 0.2, m.stone);
    onFace(kit, parent, f, s.u, s.sill - 0.2, s.w + 0.62, 0.2, 0.26, m.stone);
  } else {
    onFace(kit, parent, f, s.u, s.sill - 0.12, s.w + 0.4, 0.12, 0.2, m.bar);
  }
}

/** A render band along a wall face: a cornice or a string course, `d` deep. */
export function cornice(kit: Kit, parent: TransformNode, f: Face, u0: number, u1: number, y: number, h: number, d: number, mat: StandardMaterial): void {
  onFace(kit, parent, f, (u0 + u1) / 2, y, u1 - u0, h, d, mat);
}

/** A row of small blocks under a cornice. */
export function dentils(kit: Kit, parent: TransformNode, f: Face, u0: number, u1: number, y: number, d: number, mat: StandardMaterial, every = 0.55): void {
  const n = Math.floor((u1 - u0) / every);
  const start = u0 + ((u1 - u0) - (n - 1) * every) / 2;
  for (let i = 0; i < n; i++) onFace(kit, parent, f, start + i * every, y, 0.24, 0.22, d, mat);
}

/**
 * A balustrade from (x0, z0) to (x1, z1) standing on `y`: a base, turned balusters, a coping and
 * a pier at each end.
 */
export function balustrade(
  kit: Kit,
  parent: TransformNode,
  x0: number,
  z0: number,
  x1: number,
  z1: number,
  y: number,
  mats: { rail: StandardMaterial; baluster: StandardMaterial },
  h = 1.15,
): void {
  const len = Math.hypot(x1 - x0, z1 - z0);
  const alongX = Math.abs(x1 - x0) >= Math.abs(z1 - z0);
  const cx = (x0 + x1) / 2;
  const cz = (z0 + z1) / 2;
  const bar = (by: number, bh: number, t: number): Mesh =>
    kit.block(parent, cx, by, cz, alongX ? len : t, bh, alongX ? t : len, mats.rail);
  bar(y, 0.18, 0.42);
  bar(y + h - 0.18, 0.18, 0.46);
  const n = Math.max(2, Math.round(len / 0.42));
  for (let i = 1; i < n; i++) {
    const f = i / n;
    const bx = x0 + (x1 - x0) * f;
    const bz = z0 + (z1 - z0) * f;
    kit.block(parent, bx, y + 0.18, bz, 0.16, h - 0.36, 0.16, mats.baluster);
    kit.block(parent, bx, y + 0.42, bz, 0.24, 0.24, 0.24, mats.baluster);
  }
  for (const [px, pz] of [
    [x0, z0],
    [x1, z1],
  ] as const) {
    kit.block(parent, px, y, pz, 0.52, h + 0.12, 0.52, mats.rail);
  }
}

/**
 * Cast-iron lace on a transparent plane, painted once and shared: arcs, rings and fleurs in a
 * frieze. `cells` is how many repeats sit across the plane.
 */
export function lace(kit: Kit, parent: TransformNode, name: string, width: number, height: number, cells: number, color = "#1f3b31"): Mesh {
  const tex = kit.canvas(`v2_lace_${name}`, 256, 96, (ctx, w, h) => {
    ctx.clearRect(0, 0, w, h);
    ctx.strokeStyle = color;
    ctx.fillStyle = color;
    ctx.lineWidth = 7;
    // Top and bottom rails.
    ctx.fillRect(0, 0, w, 10);
    ctx.fillRect(0, h - 8, w, 8);
    // A scalloped arch with a ring and a fleur in each half.
    for (const cx of [w * 0.25, w * 0.75]) {
      ctx.beginPath();
      ctx.arc(cx, 10, w * 0.25 - 6, 0, Math.PI);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(cx, h * 0.58, 13, 0, Math.PI * 2);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(cx, h * 0.58 + 13);
      ctx.lineTo(cx, h - 8);
      ctx.stroke();
    }
    ctx.lineWidth = 5;
    for (const cx of [0, w * 0.5, w]) {
      ctx.beginPath();
      ctx.moveTo(cx, 10);
      ctx.lineTo(cx, h - 8);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(cx, h * 0.4, 9, 0, Math.PI * 2);
      ctx.fill();
    }
  });
  tex.wrapU = 1;
  tex.uScale = cells;
  const mat = kit.screen(tex, true);
  // Alpha-tested rather than blended, so the lace stays crisp and never sorts behind the scene.
  mat.transparencyMode = Material.MATERIAL_ALPHATEST;
  mat.alphaCutOff = 0.5;
  const plane = MeshBuilder.CreatePlane(`v2_lacePlane_${name}`, { width, height, sideOrientation: Mesh.DOUBLESIDE }, kit.scene);
  plane.material = mat;
  plane.parent = parent;
  plane.isPickable = false;
  return plane;
}
