/**
 * Guru House's surroundings: a floating voxel city block on a Melbourne
 * corner.
 *
 *  - In front, the house's own forecourt (it is set back from the street),
 *    the footpath and bluestone kerb, and the main street with the tram line.
 *    The tram stop has its own platform out from the kerb on the left, with a
 *    shelter and bench, the next-tram display and the stop sign, clear of the
 *    verandah and the front desk. The street in front of the house (with the
 *    earth below it) is a group of its own, `front`, that fades away when the
 *    story looks into the old bank vault, like a section cut through the ground.
 *  - Down the left side, a bluestone laneway with street art, neon and
 *    festoon lights, beside a heritage building with the corner café.
 *  - On the right, the cross street at the corner, a pocket park and London
 *    plane trees, and in the back, low warehouses and two modern towers.
 *  - The things that move: clouds below the block and a delivery drone.
 */
import { type AbstractMesh, Mesh, MeshBuilder, type StandardMaterial, TransformNode } from "@babylonjs/core";
import { Batch, type Kit, rng } from "./kit";
import { heritageMats, onFace, type Face } from "./heritage";
import {
  BLOCK,
  CROSS,
  FLOOR_Y,
  FORECOURT_Z,
  HOUSE,
  KERB_Z,
  LANE,
  PLATFORM,
  PLATFORM_EDGE,
  ROAD,
  ROOF_Y,
  TRACK_Z,
  TRAM_DOORS,
  TRAM_STOP_X,
} from "./layout";

export interface Street {
  /** The street in front of the house (footpath, road, tram line and the earth below), which the front desk builds on. */
  front: TransformNode;
  /** Whether the street in front is showing (it hides when the story looks into the vault). */
  readonly frontVisible: boolean;
  /** How far the street in front has faded in (0 hidden, 1 showing). */
  readonly frontAlpha: number;
  update(dt: number, t: number): void;
  /** Fade the street in front of the house out (it hides the front of the vault) or back in. */
  setFrontVisible(visible: boolean): void;
}

/** Earth layers under the street, top to bottom: road base, dirt, clay and rock. */
const LAYERS: Array<[top: number, bottom: number, key: "base" | "dirt" | "clay" | "stone"]> = [
  [0, -0.6, "base"],
  [-0.6, -3.4, "dirt"],
  [-3.4, -7.2, "clay"],
  [-7.2, BLOCK.bottom, "stone"],
];

/** The footpath's surface sits a little above the road. */
const PATH_Y = 0.15;

export function buildStreet(kit: Kit): Street {
  const scene = kit.scene;
  const root = new TransformNode("v2_street", scene);
  const batch = new Batch(root, "street");
  kit.useBatch(batch);
  const r = rng(20261123);
  const m = heritageMats(kit);

  const earth: Record<"base" | "dirt" | "clay" | "stone", StandardMaterial> = {
    base: kit.mat("#4a4d52"),
    dirt: kit.mat("#ffffff", { texture: "dirt" }),
    clay: kit.mat("#ffffff", { texture: "clay" }),
    stone: kit.mat("#ffffff", { texture: "stone" }),
  };
  const asphalt = kit.mat("#ffffff", { texture: "asphalt" });
  const paving = kit.mat("#ffffff", { texture: "paving" });
  const grass = kit.mat("#ffffff", { texture: "grass" });
  const concrete = kit.mat("#ffffff", { texture: "concrete" });
  const deepStone = kit.mat("#a3a8b0", { texture: "stone" });
  const white = kit.mat("#f4f1ea");
  const lineWhite = kit.mat("#f1f1ec");
  const steel = kit.mat("#8b939e", { specular: 0.5 });
  const darkSteel = kit.mat("#2b3240", { specular: 0.25 });
  const warmGlow = kit.mat("#ffd27a", { unlit: true });

  /** A chunk of the block: its surface on top and the earth layers below. */
  const chunk = (parent: TransformNode, x0: number, x1: number, z0: number, z1: number, top?: StandardMaterial): void => {
    const w = x1 - x0;
    const d = z1 - z0;
    const cx = (x0 + x1) / 2;
    const cz = (z0 + z1) / 2;
    for (const [t, b, key] of LAYERS) kit.block(parent, cx, b, cz, w, t - b, d, earth[key]);
    if (top) kit.block(parent, cx, -0.02, cz, w, 0.04, d, top);
  };
  /** A footpath slab on top of the block. */
  const path = (parent: TransformNode, x0: number, x1: number, z0: number, z1: number, mat = paving): void => {
    kit.block(parent, (x0 + x1) / 2, 0, (z0 + z1) / 2, x1 - x0, PATH_Y, z1 - z0, mat);
  };
  /** A bluestone kerb along x or z. */
  const kerb = (parent: TransformNode, x0: number, x1: number, z0: number, z1: number): void => {
    kit.block(parent, (x0 + x1) / 2, 0, (z0 + z1) / 2, Math.max(0.3, x1 - x0), PATH_Y + 0.02, Math.max(0.3, z1 - z0), m.bluestone);
  };

  /* ---------------- The block behind the street ---------------- */
  // Left of the house (the café building, the laneway), right of it (the cross street and the
  // park) and behind it. The house's basement fills the gap in the middle.
  chunk(root, BLOCK.x0, HOUSE.x0, HOUSE.z0, BLOCK.z1);
  chunk(root, HOUSE.x1, BLOCK.x1, HOUSE.z0, BLOCK.z1);
  chunk(root, HOUSE.x0, HOUSE.x1, HOUSE.z1, BLOCK.z1);
  // Rock under the vault.
  kit.block(root, 0, BLOCK.bottom, (HOUSE.z0 + HOUSE.z1) / 2, HOUSE.x1 - HOUSE.x0, FLOOR_Y.engine - 0.5 - BLOCK.bottom, HOUSE.z1 - HOUSE.z0, earth.stone);
  // The block's stepped underside, so it reads as a floating diorama.
  const bcz = (BLOCK.z0 + BLOCK.z1) / 2;
  const bd = BLOCK.z1 - BLOCK.z0;
  kit.block(root, 0, -15, bcz, 84, 3, bd - 8, deepStone);
  kit.block(root, 0, -18, bcz, 58, 3, bd - 30, deepStone);
  kit.block(root, 0, -20.5, bcz + 1, 30, 2.5, 16, deepStone);

  // The laneway: bluestone setts with a channel down the middle.
  kit.block(root, (LANE.x0 + LANE.x1) / 2, 0, (HOUSE.z0 + BLOCK.z1) / 2, LANE.x1 - LANE.x0, 0.08, BLOCK.z1 - HOUSE.z0, m.bluestone, { tile: 1.6 });
  kit.block(root, (LANE.x0 + LANE.x1) / 2, 0.08, (HOUSE.z0 + BLOCK.z1) / 2, 0.5, 0.01, BLOCK.z1 - HOUSE.z0, kit.mat("#3a404a"));
  // The cross street on the right, its footpaths and the back lane behind the house.
  path(root, HOUSE.x1, CROSS.x0, HOUSE.z0, BLOCK.z1);
  kit.block(root, (CROSS.x0 + CROSS.x1) / 2, -0.02, (HOUSE.z0 + BLOCK.z1) / 2, CROSS.x1 - CROSS.x0, 0.04, BLOCK.z1 - HOUSE.z0, asphalt);
  kerb(root, CROSS.x0 - 0.15, CROSS.x0 + 0.15, HOUSE.z0, BLOCK.z1);
  kerb(root, CROSS.x1 - 0.15, CROSS.x1 + 0.15, HOUSE.z0, BLOCK.z1);
  path(root, CROSS.x1, 33.4, HOUSE.z0, BLOCK.z1);
  for (let z = HOUSE.z0 + 2; z < BLOCK.z1 - 1; z += 4) kit.block(root, (CROSS.x0 + CROSS.x1) / 2, 0.02, z, 0.18, 0.01, 2, lineWhite);
  kit.block(root, 0, 0, (HOUSE.z1 + 13) / 2, HOUSE.x1 - HOUSE.x0, 0.08, 13 - HOUSE.z1, m.bluestone, { tile: 1.6 });

  /* ---------------- The street in front ---------------- */
  const front = new TransformNode("v2_streetFront", scene);
  front.parent = root;
  const frontBatch = new Batch(front, "streetFront");
  kit.useBatch(frontBatch);
  chunk(front, BLOCK.x0, BLOCK.x1, BLOCK.z0, HOUSE.z0, asphalt);
  // Footpaths, broken by the cross street at the corner. In front of the house, its own forecourt
  // in warm sandstone flags with a bluestone edge, then the public footpath out to the kerb.
  path(front, BLOCK.x0, HOUSE.x0, KERB_Z, HOUSE.z0);
  path(front, HOUSE.x0, HOUSE.x1, KERB_Z, FORECOURT_Z);
  path(front, HOUSE.x0, HOUSE.x1, FORECOURT_Z, HOUSE.z0, kit.mat("#fff1dc", { texture: "paving" }));
  kit.block(front, 0, 0, FORECOURT_Z + 0.2, HOUSE.x1 - HOUSE.x0, PATH_Y + 0.02, 0.4, m.bluestone, { tile: 1.2 });
  path(front, HOUSE.x1, CROSS.x0, KERB_Z, HOUSE.z0);
  path(front, CROSS.x1, BLOCK.x1, KERB_Z, HOUSE.z0);
  path(front, BLOCK.x0, BLOCK.x1, BLOCK.z0 + 0.5, ROAD.z0);
  kerb(front, BLOCK.x0, CROSS.x0, KERB_Z - 0.15, KERB_Z + 0.15);
  kerb(front, CROSS.x1, BLOCK.x1, KERB_Z - 0.15, KERB_Z + 0.15);
  kerb(front, BLOCK.x0, BLOCK.x1, ROAD.z0 - 0.15, ROAD.z0 + 0.15);
  kerb(front, CROSS.x0 - 0.15, CROSS.x0 + 0.15, KERB_Z, HOUSE.z0);
  kerb(front, CROSS.x1 - 0.15, CROSS.x1 + 0.15, KERB_Z, HOUSE.z0);
  // The tram line: two pairs of rails set into the road, and lane lines either side.
  for (const tz of [TRACK_Z.near, TRACK_Z.far]) {
    kit.block(front, 0, 0, tz, BLOCK.x1 - BLOCK.x0, 0.03, 2.2, kit.mat("#56595f"));
    for (const dz of [-0.72, 0.72]) kit.block(front, 0, 0.03, tz + dz, BLOCK.x1 - BLOCK.x0, 0.05, 0.14, steel);
  }
  const centreLine = (TRACK_Z.near + TRACK_Z.far) / 2;
  for (const lz of [centreLine, KERB_Z - 0.9, ROAD.z0 + 0.9]) {
    for (let x = BLOCK.x0 + 1; x < BLOCK.x1 - 1; x += 3.2) {
      if (lz !== centreLine && x > CROSS.x0 - 1 && x < CROSS.x1 + 1) continue;
      kit.block(front, x, 0.02, lz, 1.6, 0.01, 0.16, lineWhite);
    }
  }
  // A pedestrian crossing over the cross street at the corner.
  for (let x = CROSS.x0 + 0.5; x < CROSS.x1 - 0.2; x += 1.0) kit.block(front, x, 0.02, KERB_Z + 2.3, 0.55, 0.01, 3.4, lineWhite);

  /* ---------------- The tram stop ---------------- */
  // Its own level-access platform out from the kerb, on the left of the forecourt and clear of the
  // verandah: rails at both ends, a shelter with a bench, the next-tram display and the stop sign.
  const P = PLATFORM;
  const stopX0 = TRAM_STOP_X - P.half;
  const stopX1 = TRAM_STOP_X + P.half;
  const stopTop = P.y;
  kit.block(front, TRAM_STOP_X, 0, (PLATFORM_EDGE + KERB_Z) / 2, P.half * 2, stopTop, KERB_Z - PLATFORM_EDGE, concrete);
  kit.block(front, TRAM_STOP_X, stopTop, PLATFORM_EDGE + 0.22, P.half * 2, 0.02, 0.36, kit.mat("#facc15"));
  kit.block(front, TRAM_STOP_X, 0, PLATFORM_EDGE - 0.04, P.half * 2, stopTop + 0.01, 0.08, m.bluestone);
  // Safety rails across both ends of the platform.
  for (const x of [stopX0 + 0.12, stopX1 - 0.12]) {
    for (const z of [KERB_Z - 0.2, PLATFORM_EDGE + 0.5]) kit.block(front, x, stopTop, z, 0.1, 1.1, 0.1, darkSteel);
    kit.block(front, x, stopTop + 1.0, (KERB_Z - 0.2 + PLATFORM_EDGE + 0.5) / 2, 0.12, 0.1, KERB_Z - PLATFORM_EDGE - 0.6, steel);
    kit.block(front, x, stopTop + 0.5, (KERB_Z - 0.2 + PLATFORM_EDGE + 0.5) / 2, 0.06, 0.06, KERB_Z - PLATFORM_EDGE - 0.6, steel);
  }
  // The shelter, between the tram's first and middle doors: a canopy on two posts, a bench and a map panel.
  const shelterX = TRAM_STOP_X - 2.4;
  const shelterZ = KERB_Z - 0.85;
  for (const dx of [-2.0, 2.0]) kit.block(front, shelterX + dx, stopTop, KERB_Z - 0.3, 0.14, 2.9, 0.14, darkSteel);
  kit.block(front, shelterX, stopTop + 2.9, shelterZ, 4.6, 0.16, 1.7, kit.mat("#e9edf2"));
  kit.block(front, shelterX, stopTop + 2.86, shelterZ - 0.85, 4.6, 0.12, 0.06, kit.mat("#14b8a6", { unlit: true }));
  kit.block(front, shelterX, stopTop, KERB_Z - 0.45, 3.4, 0.5, 0.5, kit.mat("#ffffff", { texture: "walnut" }));
  kit.block(front, shelterX - 0.9, stopTop + 0.5, KERB_Z - 0.22, 1.2, 0.5, 0.06, darkSteel);
  kit.block(front, shelterX + 0.9, stopTop + 0.5, KERB_Z - 0.22, 1.2, 0.5, 0.06, darkSteel);
  kit.block(front, shelterX + 2.0, stopTop + 0.6, KERB_Z - 0.34, 0.08, 2.0, 1.0, darkSteel);
  kit.block(front, shelterX + 2.05, stopTop + 0.7, KERB_Z - 0.34, 0.02, 1.8, 0.86, kit.mat("#dbeafe", { unlit: true }));
  // The next-tram display on a slim totem at the left end of the platform.
  const totemX = stopX0 + 1.3;
  kit.block(front, totemX, stopTop, KERB_Z - 0.45, 0.5, 2.5, 0.3, darkSteel);
  const nextTram = kit.label(front, {
    lines: [
      { text: "NEXT TRAM", size: 30, weight: 700, color: "#9fb3c8" },
      { text: "CITY · 2 min", size: 44, weight: 800, color: "#ffb547" },
    ],
    width: 1.15,
    px: 300,
    bg: "rgba(10,12,18,1)",
  });
  nextTram.position.set(totemX, stopTop + 1.9, KERB_Z - 0.61);
  // The stop sign at the right end of the platform.
  const signX = stopX1 - 0.8;
  kit.block(front, signX, stopTop, KERB_Z - 0.5, 0.14, 3.6, 0.14, darkSteel);
  const stopSign = kit.label(front, {
    lines: [
      { text: "TRAM STOP", size: 54, weight: 800, color: "#ffffff" },
      { text: "Guru House", size: 30, weight: 600, color: "#d1fae5" },
    ],
    width: 2.2,
    px: 420,
    bg: "rgba(15,118,110,0.97)",
  });
  stopSign.position.set(signX, stopTop + 3.25, KERB_Z - 0.6);
  const stopSignBack = stopSign.clone("v2_stopSignBack");
  stopSignBack.rotation.y = Math.PI;
  stopSignBack.position.z += 0.04;
  // Where the tram's doors open, marked on the platform's edge.
  for (const d of TRAM_DOORS) kit.block(front, TRAM_STOP_X + d, stopTop, PLATFORM_EDGE + 0.62, 1.4, 0.02, 0.22, kit.mat("#f4f1ea"));

  /* ---------------- The corner café and its building (left) ---------------- */
  kit.useBatch(batch);
  // Flat roofs in a membrane inside a stone parapet, with plant on top, so the neighbours' roofs
  // stay quiet beside and behind the house.
  const membrane = kit.mat("#7d8591");
  const plantMat = kit.mat("#b8c0ca");
  const roofUnit = (x: number, y: number, z: number): void => {
    kit.block(root, x, y, z, 1.6, 0.9, 1.2, plantMat);
    kit.block(root, x, y + 0.9, z, 0.9, 0.06, 0.9, darkSteel);
  };
  const flatRoof = (cx: number, y: number, cz: number, w: number, d: number, units: Array<[number, number]>): void => {
    kit.block(root, cx, y, cz, w + 0.4, 0.5, d + 0.4, m.stone);
    kit.block(root, cx, y + 0.5, cz, w - 0.5, 0.04, d - 0.5, membrane);
    for (const [ux, uz] of units) roofUnit(cx + ux, y + 0.54, cz + uz);
  };
  const cafe = { x0: BLOCK.x0 + 0.5, x1: LANE.x0, z0: HOUSE.z0, z1: 8, h: 10.6 };
  const cafeW = cafe.x1 - cafe.x0;
  const cafeCx = (cafe.x0 + cafe.x1) / 2;
  const cafeCz = (cafe.z0 + cafe.z1) / 2;
  const cafeD = cafe.z1 - cafe.z0;
  kit.block(root, cafeCx, 0, cafeCz, cafeW, cafe.h, cafeD, m.brick);
  // A plain flat roof: one calm membrane over the whole top, inside a low stone parapet on the
  // laneway, side and back (the front's render parapet stands proud of the street face), with two
  // small plant units.
  kit.block(root, cafeCx, cafe.h, cafeCz, cafeW - 0.02, 0.05, cafeD - 0.02, membrane);
  const coping = 0.5;
  for (const x of [cafe.x0 + coping / 2 - 0.1, cafe.x1 - coping / 2 + 0.1]) kit.block(root, x, cafe.h, cafeCz, coping, 0.6, cafeD - 0.8, m.stone);
  kit.block(root, cafeCx, cafe.h, cafe.z1 - coping / 2 + 0.1, cafeW + 0.2, 0.6, coping, m.stone);
  kit.block(root, cafeCx, cafe.h, cafe.z0 + 0.2, cafeW, 0.8, 0.4, m.render);
  roofUnit(cafeCx - 5.5, cafe.h + 0.05, cafe.z1 - 4.5);
  roofUnit(cafeCx + 4.0, cafe.h + 0.05, cafe.z1 - 3.2);
  const cafeFront: Face = { axis: "x", at: cafe.z0, out: -1 };
  // Render front with a cornice and parapet.
  onFace(kit, root, cafeFront, cafeCx, 4.4, cafeW, cafe.h - 4.4, 0.08, m.render);
  onFace(kit, root, cafeFront, cafeCx, cafe.h - 0.9, cafeW + 0.4, 0.5, 0.4, m.stone);
  onFace(kit, root, cafeFront, cafeCx, cafe.h - 0.4, cafeW, 1.2, 0.2, m.render);
  onFace(kit, root, cafeFront, cafeCx, 4.1, cafeW + 0.2, 0.36, 0.3, m.stone);
  for (const x of [-44, -39.5, -35, -30.5]) {
    onFace(kit, root, cafeFront, x, 5.6, 1.7, 2.6, 0.12, m.bar);
    onFace(kit, root, cafeFront, x, 5.75, 1.4, 2.3, 0.16, m.pane);
    onFace(kit, root, cafeFront, x, 6.9, 1.4, 0.08, 0.18, m.bar);
    onFace(kit, root, cafeFront, x, 5.45, 2.0, 0.18, 0.24, m.stone);
  }
  // Shopfronts: the café on the corner, and a florist.
  const shop = (x0: number, x1: number, awning: [string, string], sign: string, ink: string, z = cafe.z0): void => {
    const w = x1 - x0;
    const cx = (x0 + x1) / 2;
    onFace(kit, root, cafeFront, cx, 0, w, 4.1, 0.06, kit.mat("#1f2a37"));
    onFace(kit, root, cafeFront, cx, 0.5, w - 0.6, 3.1, 0.12, m.pane);
    onFace(kit, root, cafeFront, cx, 0, w - 0.4, 0.5, 0.14, m.iron);
    for (let x = x0 + 0.3; x <= x1 - 0.2; x += 2.1) onFace(kit, root, cafeFront, x, 0.5, 0.12, 3.1, 0.16, m.iron);
    // A striped awning over the footpath.
    const stripes = Math.round(w / 0.8);
    for (let i = 0; i < stripes; i++) {
      const sx = x0 + (i + 0.5) * (w / stripes);
      const mat = kit.mat(awning[i % 2]);
      kit.block(front, sx, 3.5, z - 1.25, w / stripes, 0.12, 2.5, mat).rotation.x = -0.32;
      kit.block(front, sx, 2.86, z - 2.42, w / stripes, 0.5, 0.06, mat);
    }
    const label = kit.label(root, {
      lines: [{ text: sign, size: 60, weight: 800, color: ink, serif: true }],
      width: Math.min(w - 1, 5.2),
      px: 640,
      pxHeight: 110,
      bg: "rgba(255,250,240,0.96)",
    });
    label.position.set(cx, 4.7, z - 0.12);
  };
  shop(-34.2, cafe.x1, ["#1f5d4c", "#f3ead7"], "CAFÉ · ESPRESSO", "#1f5d4c");
  shop(cafe.x0, -34.6, ["#b45309", "#f3ead7"], "FLOWERS", "#9a3412");
  // Outdoor tables on the footpath, and buckets of flowers.
  kit.useBatch(frontBatch);
  for (const x of [-32.6, -29.6]) {
    kit.cyl(front, x, PATH_Y, -15.9, 0.12, 1.1, darkSteel, { tess: 8 });
    kit.cyl(front, x, PATH_Y + 1.1, -15.9, 1.1, 0.08, white, { tess: 14 });
    kit.block(front, x - 0.12, PATH_Y + 1.18, -15.8, 0.14, 0.18, 0.14, kit.mat("#ffffff"));
    for (const dx of [-0.95, 0.95]) {
      kit.block(front, x + dx, PATH_Y, -15.9, 0.6, 0.62, 0.6, kit.mat("#1f5d4c"));
      kit.block(front, x + dx * 1.15, PATH_Y + 0.62, -15.9, 0.12, 0.7, 0.6, kit.mat("#1f5d4c"));
    }
  }
  const blooms = ["#f472b6", "#facc15", "#fb7185", "#a78bfa", "#ffffff"].map((hex) => kit.mat(hex));
  for (let i = 0; i < 5; i++) {
    const x = -45.4 + i * 1.1;
    kit.block(front, x, PATH_Y, -13.8, 0.8, 0.6, 0.8, kit.mat("#6b7280"));
    kit.block(front, x, PATH_Y + 0.6, -13.8, 0.7, 0.45, 0.7, kit.mat("#4f8a33"));
    kit.block(front, x + 0.1, PATH_Y + 1.0, -13.9, 0.35, 0.25, 0.35, blooms[i % blooms.length]);
  }
  kit.useBatch(batch);

  // The laneway wall of the café building: a mural and neon, with festoon lights over the lane.
  const mural = kit.canvas("v2_mural", 1024, 512, (ctx, w, h) => {
    ctx.fillStyle = "#24364f";
    ctx.fillRect(0, 0, w, h);
    const blobs: Array<[number, number, number, string]> = [
      [140, 330, 190, "#14b8a6"],
      [420, 160, 210, "#f472b6"],
      [700, 360, 230, "#facc15"],
      [930, 150, 170, "#8b9cf7"],
      [300, 470, 140, "#fb923c"],
    ];
    for (const [x, y, rr, c] of blobs) {
      ctx.fillStyle = c;
      ctx.beginPath();
      ctx.arc(x, y, rr, 0, Math.PI * 2);
      ctx.fill();
    }
    // A big bird in flight, in bold strokes.
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 26;
    ctx.lineCap = "round";
    ctx.beginPath();
    ctx.moveTo(260, 250);
    ctx.quadraticCurveTo(420, 90, 560, 240);
    ctx.quadraticCurveTo(700, 90, 860, 230);
    ctx.stroke();
    ctx.fillStyle = "#ffffff";
    const rr = rng(404);
    for (let i = 0; i < 60; i++) ctx.fillRect(rr() * w, rr() * h, 8, 8);
    ctx.fillStyle = "#f8fafc";
    ctx.font = '900 96px "Segoe UI", sans-serif';
    ctx.fillText("HELLO", 60, 140);
  });
  const muralPlane = MeshBuilder.CreatePlane("v2_muralPlane", { width: 12, height: 6 }, scene);
  muralPlane.rotation.y = -Math.PI / 2;
  muralPlane.position.set(LANE.x0 + 0.03, 4.4, -6.6);
  kit.finish(muralPlane, root, kit.screen(mural));
  const neon = (text: string, color: string, z: number, y: number, width: number): void => {
    const sign = kit.label(root, {
      lines: [{ text, size: 76, weight: 800, color }],
      width,
      px: 512,
      pxHeight: 112,
    });
    sign.rotation.y = -Math.PI / 2;
    sign.position.set(LANE.x0 + 0.08, y, z);
  };
  neon("OPEN LATE", "#ff5fa2", -11.4, 8.4, 3.2);
  neon("♥", "#ff3b6b", -1.6, 8.6, 1.4);
  neon("☕ COFFEE", "#5ce1ff", -0.6, 2.3, 2.8);
  // Wheelie bins and a bike against the wall.
  for (const [z, hex] of [
    [1.8, "#166534"],
    [3.0, "#b91c1c"],
  ] as const) {
    kit.block(root, LANE.x0 + 0.6, 0.08, z, 0.9, 1.3, 0.9, kit.mat(hex));
    kit.block(root, LANE.x0 + 0.6, 1.38, z, 1.0, 0.1, 1.0, kit.mat(hex));
  }
  // Festoon lights across the laneway.
  const bulbs = kit.mat("#ffe6a8", { unlit: true });
  for (const z of [-11.5, -6.5, -1.5, 3.5]) {
    const n = 9;
    for (let i = 1; i < n; i++) {
      const f = i / n;
      const x = LANE.x0 + (LANE.x1 - LANE.x0) * f;
      kit.block(root, x, 7.2 - Math.sin(Math.PI * f) * 0.7, z, 0.2, 0.2, 0.2, bulbs);
    }
  }

  /* ---------------- The right: the corner park, a terrace row and the towers ---------------- */
  const park = { x0: 33.4, x1: BLOCK.x1 - 0.5, z0: HOUSE.z0, z1: 10 };
  kit.block(root, (park.x0 + park.x1) / 2, 0, (park.z0 + park.z1) / 2, park.x1 - park.x0, 0.12, park.z1 - park.z0, grass);
  kit.block(root, (park.x0 + park.x1) / 2, 0.12, -2, park.x1 - park.x0, 0.02, 1.8, kit.mat("#d6cdb8"));
  for (const x of [37, 42.5]) {
    kit.block(root, x, 0.12, 0.6, 2.4, 0.5, 0.8, kit.mat("#ffffff", { texture: "walnut" }));
    kit.block(root, x, 0.62, 1.0, 2.4, 0.7, 0.14, kit.mat("#ffffff", { texture: "walnut" }));
    for (const dx of [-1, 1]) kit.block(root, x + dx, 0.12, 0.6, 0.12, 0.5, 0.8, darkSteel);
  }
  kit.cyl(root, 45.5, 0.12, -8, 0.5, 1.1, darkSteel, { tess: 8 });
  // A two-storey heritage terrace row behind the park.
  const terrace = { x0: 33.4, x1: BLOCK.x1 - 0.5, z0: 11, z1: 19 };
  kit.block(root, (terrace.x0 + terrace.x1) / 2, 0, (terrace.z0 + terrace.z1) / 2, terrace.x1 - terrace.x0, 9, terrace.z1 - terrace.z0, m.render);
  for (let x = terrace.x0 + 1.6; x < terrace.x1 - 1; x += 3.6) {
    onFace(kit, root, { axis: "x", at: terrace.z0, out: -1 }, x, 1.0, 1.4, 2.6, 0.1, m.pane);
    onFace(kit, root, { axis: "x", at: terrace.z0, out: -1 }, x, 5.4, 1.4, 2.3, 0.1, m.pane);
    onFace(kit, root, { axis: "x", at: terrace.z0, out: -1 }, x + 1.8, 0, 0.3, 9, 0.12, m.stone);
  }
  kit.block(root, (terrace.x0 + terrace.x1) / 2, 9, (terrace.z0 + terrace.z1) / 2, terrace.x1 - terrace.x0 + 0.3, 0.6, terrace.z1 - terrace.z0 + 0.3, m.stone);
  kit.block(root, (terrace.x0 + terrace.x1) / 2, 9.6, (terrace.z0 + terrace.z1) / 2, terrace.x1 - terrace.x0 - 0.6, 0.04, terrace.z1 - terrace.z0 - 0.6, kit.mat("#8a6f5e"));

  // Low buildings behind the house, and two modern towers at the back corners.
  const towerTex = kit.canvas("v2_towerGrid", 64, 64, (ctx, w, h) => {
    ctx.fillStyle = "#8ea3b8";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#4f6378";
    for (let y = 4; y < h; y += 16) for (let x = 4; x < w; x += 16) ctx.fillRect(x, y, 10, 9);
  });
  towerTex.wrapU = 1;
  towerTex.wrapV = 1;
  const towerMat = kit.mat("#ffffff");
  towerMat.diffuseTexture = towerTex;
  const tower = (cx: number, cz: number, w: number, d: number, h: number): void => {
    kit.block(root, cx, 0, cz, w, h, d, towerMat, { tile: 4 });
    kit.block(root, cx, h, cz, w + 0.4, 0.6, d + 0.4, kit.mat("#e2e8f0"));
    kit.block(root, cx, h + 0.6, cz + d * 0.2, w * 0.4, 2.2, d * 0.35, kit.mat("#cbd5e1"));
  };
  tower(cafeCx, 20, 16, 15, 30);
  tower(40.5, 25, 12, 8, 25);
  kit.block(root, -9, 0, 20.5, 22, 11, 13, m.brick);
  flatRoof(-9, 11, 20.5, 22, 13, [
    [-6, 2],
    [4, -1],
  ]);
  for (let x = -18; x < 1; x += 3.2) onFace(kit, root, { axis: "x", at: 14, out: -1 }, x, 3.4, 1.6, 2.4, 0.1, m.pane);
  kit.block(root, 11.5, 0, 21.5, 17, 8, 11, m.render);
  for (let x = 4.5; x < 19; x += 3.4) onFace(kit, root, { axis: "x", at: 16, out: -1 }, x, 2.2, 1.8, 3.2, 0.1, m.pane);
  flatRoof(11.5, 8, 21.5, 17, 11, [[3, 1]]);

  /* ---------------- London plane trees, lamps, bikes and an e-scooter ---------------- */
  const bark = [kit.mat("#d8cfb4"), kit.mat("#8f8a62"), kit.mat("#7a6a55")];
  const leaves = [kit.mat("#6fa64a"), kit.mat("#5d9440"), kit.mat("#83b85a")];
  /** A London plane tree: a mottled trunk and a broad, layered canopy. */
  const plane = (parent: TransformNode, x: number, z: number, s: number, y = PATH_Y): void => {
    for (let k = 0; k < 4; k++) kit.block(parent, x, y + k * 0.9 * s, z, 0.62 * s, 0.9 * s, 0.62 * s, bark[k % 3]);
    kit.block(parent, x + 0.5 * s, y + 2.8 * s, z, 1.6 * s, 0.35 * s, 0.35 * s, bark[1]);
    kit.block(parent, x, y + 3.2 * s, z, 4.4 * s, 1.6 * s, 4.0 * s, leaves[0]);
    kit.block(parent, x + 0.6 * s, y + 4.6 * s, z - 0.4 * s, 3.4 * s, 1.3 * s, 3.2 * s, leaves[1]);
    kit.block(parent, x - 1.3 * s, y + 3.8 * s, z + 0.6 * s, 1.8 * s, 1.4 * s, 2.0 * s, leaves[2]);
    kit.block(parent, x + 1.1 * s, y + 3.5 * s, z + 1.2 * s, 1.6 * s, 1.0 * s, 1.4 * s, leaves[2]);
  };
  // Along the cross street, trees stand only past the house and across the road, so they never
  // hide the side street facade.
  for (const [z, s] of [
    [13, 1.15],
    [22, 1.2],
  ] as const) {
    plane(root, CROSS.x0 - 1.6, z, s);
  }
  for (const [z, s] of [
    [-6, 1.1],
    [3, 1.15],
    [12, 1.05],
    [21, 1.15],
  ] as const) {
    plane(root, CROSS.x1 + 1.5, z, s);
  }
  for (const [x, z, s] of [
    [37, -9, 1.25],
    [44, -4, 1.1],
    [39.5, 5, 1.3],
  ] as const) {
    plane(root, x, z, s, 0.12);
  }
  // In front, trees only beside the house, so they never hide a floor.
  kit.useBatch(frontBatch);
  for (const [x, z, s] of [
    [-38, BLOCK.z0 + 2.6, 1.2],
    [-25.5, BLOCK.z0 + 2.6, 1.1],
    [27.5, BLOCK.z0 + 2.6, 1.15],
    [40, BLOCK.z0 + 2.6, 1.2],
    [-42.5, KERB_Z + 1.4, 1.05],
    [36, KERB_Z + 1.4, 1.1],
  ] as const) {
    plane(front, x, z, s);
  }
  /** A heritage lamp post: a fluted green post, a scrolled arm and a lantern that glows at dusk. */
  const lamp = (parent: TransformNode, x: number, z: number, y = PATH_Y): void => {
    kit.block(parent, x, y, z, 0.5, 0.6, 0.5, m.iron);
    kit.block(parent, x, y + 0.6, z, 0.22, 4.4, 0.22, m.iron);
    kit.block(parent, x, y + 2.4, z, 0.34, 0.14, 0.34, m.gold);
    kit.block(parent, x, y + 5.0, z, 0.62, 0.12, 0.62, m.iron);
    kit.block(parent, x, y + 5.12, z, 0.46, 0.66, 0.46, warmGlow);
    kit.block(parent, x, y + 5.78, z, 0.66, 0.16, 0.66, m.iron);
    kit.block(parent, x, y + 5.94, z, 0.2, 0.3, 0.2, m.iron);
  };
  // Along the kerb, clear of the tram stop's platform, the glass lift and the level tags beside the house.
  for (const x of [-44, -37, 2.6, 19.5, 33, 44.5]) lamp(front, x, KERB_Z + 0.5);
  for (const x of [-40, -20, 0, 20, 40]) lamp(front, x, ROAD.z0 - 0.6);
  kit.useBatch(batch);
  for (const z of [-4, 6, 16, 25]) {
    lamp(root, CROSS.x0 - 0.5, z);
    lamp(root, CROSS.x1 + 0.5, z - 5);
  }

  /** A bicycle: two wheels, a frame and handlebars. */
  const bike = (parent: TransformNode, x: number, z: number, rot: number, frameHex: string): void => {
    const node = new TransformNode("v2_bike", scene);
    node.parent = parent;
    node.position.set(x, PATH_Y, z);
    node.rotation.y = rot;
    const tyre = kit.mat("#1a1a1f");
    for (const dx of [-0.65, 0.65]) {
      const wheel = MeshBuilder.CreateTorus("v2_wheel", { diameter: 0.95, thickness: 0.1, tessellation: 16 }, scene);
      wheel.rotation.x = Math.PI / 2;
      wheel.position.set(dx, 0.48, 0);
      kit.finish(wheel, node, tyre);
    }
    const frame = kit.mat(frameHex);
    kit.block(node, 0, 0.5, 0, 1.3, 0.08, 0.08, frame);
    kit.block(node, -0.2, 0.5, 0, 0.08, 0.55, 0.08, frame).rotation.z = -0.3;
    kit.block(node, 0.55, 0.5, 0, 0.08, 0.6, 0.08, frame).rotation.z = 0.25;
    kit.block(node, -0.3, 1.05, 0, 0.4, 0.08, 0.2, kit.mat("#111827"));
    kit.block(node, 0.62, 1.1, 0, 0.08, 0.08, 0.5, tyre);
  };
  kit.useBatch(frontBatch);
  // Two bike hoops: voxel inverted U's.
  for (const dx of [0, 1.4]) {
    const hx = -24.2 + dx;
    kit.block(front, hx - 0.45, PATH_Y + 0.4, -14.4, 0.1, 0.8, 0.1, darkSteel);
    kit.block(front, hx + 0.45, PATH_Y + 0.4, -14.4, 0.1, 0.8, 0.1, darkSteel);
    kit.block(front, hx, PATH_Y + 0.85, -14.4, 1.0, 0.1, 0.1, darkSteel);
  }
  bike(front, -24.2, -14.9, 0.05, "#0f766e");
  bike(front, -22.8, -14.9, -0.04, "#f59e0b");
  // A share e-scooter parked near the corner.
  {
    const s = new TransformNode("v2_scooter", scene);
    s.parent = front;
    s.position.set(20.6, PATH_Y, -16.6);
    s.rotation.y = 0.9;
    kit.block(s, 0, 0.12, 0, 1.2, 0.1, 0.32, kit.mat("#14b8a6"));
    kit.block(s, 0.55, 0.2, 0, 0.1, 1.3, 0.1, darkSteel);
    kit.block(s, 0.55, 1.5, 0, 0.1, 0.1, 0.6, darkSteel);
    for (const dx of [-0.55, 0.55]) kit.cyl(s, dx, 0, 0, 0.26, 0.24, kit.mat("#1a1a1f"), { tess: 10 }).rotation.x = Math.PI / 2;
  }
  // Rubbish bins and bollards along the far footpath.
  for (const x of [-34, -6, 16, 34]) kit.block(front, x, PATH_Y, ROAD.z0 - 1.6, 0.7, 1.1, 0.7, m.iron);
  kit.useBatch(batch);
  bike(root, LANE.x0 + 0.5, -9.4, Math.PI / 2, "#be123c");

  kit.useBatch(null);
  batch.flush();
  frontBatch.flush();

  // Meshes added to the street later (the front desk, the tram) are picked up the next time it
  // fades, and each mesh's own visibility (0 for click targets) is kept.
  const baseVisibility = new WeakMap<AbstractMesh, number>();
  let frontMeshes: AbstractMesh[] = [];
  const collectFront = (): void => {
    frontMeshes = front.getChildMeshes(false);
    for (const mm of frontMeshes) if (!baseVisibility.has(mm)) baseVisibility.set(mm, mm.visibility);
  };
  let frontAlpha = 1;
  let frontTarget = 1;

  /* ---------------- Things that move ---------------- */
  const cloudMat = kit.mat("#ffffff", { emissive: 0.45 });
  const clouds: TransformNode[] = [];
  for (let i = 0; i < 9; i++) {
    const node = new TransformNode("v2_cloud", scene);
    node.parent = root;
    const n = 3 + Math.floor(r() * 3);
    for (let j = 0; j < n; j++) {
      const w = 3 + r() * 4;
      kit.block(node, (j - n / 2) * 2.4 + r() * 1.2, r() * 1, (r() - 0.5) * 2.4, w, 1.2 + r() * 1.2, 2.4 + r() * 2.4, cloudMat, {
        dynamic: true,
      });
    }
    // Drift well below and beyond the block so the clouds frame it without covering it.
    const side = i % 2 === 0 ? 1 : -1;
    node.position.set(-110 + r() * 220, -34 + r() * 12, side * (60 + r() * 45));
    clouds.push(node);
  }

  const drone = new TransformNode("v2_drone", scene);
  drone.parent = root;
  const droneBody = kit.mat("#1f2937", { specular: 0.4 });
  kit.block(drone, 0, 0, 0, 0.9, 0.28, 0.9, droneBody, { dynamic: true });
  kit.block(drone, 0, 0.02, 0, 2.3, 0.08, 0.14, droneBody, { dynamic: true, rotY: Math.PI / 4 });
  kit.block(drone, 0, 0.02, 0, 2.3, 0.08, 0.14, droneBody, { dynamic: true, rotY: -Math.PI / 4 });
  kit.block(drone, 0, -0.62, 0, 0.6, 0.5, 0.6, kit.mat("#c89b6d"), { dynamic: true });
  const rotors: Mesh[] = [];
  for (const [x, z] of [[0.8, 0.8], [-0.8, 0.8], [0.8, -0.8], [-0.8, -0.8]] as const) {
    rotors.push(kit.block(drone, x, 0.12, z, 0.9, 0.03, 0.12, kit.mat("#e5e7eb"), { dynamic: true }));
  }
  kit.block(drone, 0, -0.06, 0.48, 0.3, 0.1, 0.06, kit.mat("#5ce1ff", { unlit: true }), { dynamic: true });

  return {
    front,
    get frontVisible(): boolean {
      return frontTarget > 0;
    },
    get frontAlpha(): number {
      return frontAlpha;
    },
    setFrontVisible(visible: boolean): void {
      frontTarget = visible ? 1 : 0;
      collectFront();
      if (visible) front.setEnabled(true);
    },
    update(dt: number, t: number): void {
      if (frontAlpha !== frontTarget) {
        frontAlpha += (frontTarget - frontAlpha) * (1 - Math.exp(-dt * 5));
        if (Math.abs(frontTarget - frontAlpha) < 0.01) frontAlpha = frontTarget;
        for (const mm of frontMeshes) mm.visibility = (baseVisibility.get(mm) ?? 1) * frontAlpha;
        front.setEnabled(frontAlpha > 0.01);
      }
      for (const c of clouds) {
        c.position.x += dt * 0.9;
        if (c.position.x > 120) c.position.x = -120;
      }
      // The drone does slow loops high over the roof, clear of the dome and the sign.
      const da = t * 0.2;
      drone.position.set(2 + Math.cos(da) * 19, ROOF_Y + 11 + Math.sin(t * 1.7) * 0.25, -2 + Math.sin(da) * 13);
      drone.rotation.y = Math.atan2(-Math.sin(da) * 19, Math.cos(da) * 13);
      drone.rotation.z = 0.08;
      for (const rot of rotors) rot.rotation.y += dt * 40;
    },
  };
}
