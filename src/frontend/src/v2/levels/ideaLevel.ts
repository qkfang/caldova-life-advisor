/**
 * Level 3 · The Idea — Maya Ashworth's office on the top floor of Guru
 * House, and the roof above it.
 *
 * Maya is the business owner for both sides of Life Advice Guru. The voxel brain
 * floating above the round table is "the idea": one hemisphere for Claims &
 * Care, one for Protection Advice, joined by what they share. It floats under
 * the old copper dome, which the dollhouse cut opens like everything else on
 * the front of the house: half a dome, copper outside and coffered gold
 * inside, with a glass oculus at the top. Goal tiles orbit the brain, a vision
 * board on the back wall spells out two small teams and one group of agents,
 * and a cast-iron balcony looks over the tram line.
 */
import { Color3, Mesh, MeshBuilder, StandardMaterial, Texture, TransformNode, Vector3 } from "@babylonjs/core";
import { VoxelCharacter } from "../../voxelCharacter";
import { balustrade, heritageMats, lace, type HeritageMats } from "../heritage";
import { IDEA, OWNER, SIDES, levelInfo } from "../houseData";
import { Batch, type Kit, rng } from "../kit";
import { CHARACTER_SCALE, CORE, DOME, FLOOR_Y, HOUSE, INSIDE, LIFT, ROOF_Y, SLAB, faceAngle } from "../layout";
import { SIDE_BAYS, buildShell } from "../shell";
import type { Anchor, Highlights, LevelBuild } from "../types";
import { SpotBook, Wanderer, type Outing, type Spot } from "../wander";

const GOLD = Color3.FromHexString("#ffc35a");
/** The line Maya walks along across her office, behind the dome. */
const CZ = -4.2;
/** The brain's table, in the rotunda under the dome. */
const TABLE = { x: DOME.x, z: DOME.z + 2.6 };
/** The goal tiles orbit the brain on an ellipse that stays inside the rotunda's columns. */
const ORBIT = { rx: 2.7, rz: 1.4 };
/** The balcony over the street, at the front right. */
const BALCONY = { x0: 7, x1: 17.5, z0: HOUSE.z0 - 2.4 };
/**
 * The office floor: pale oak. Darker in hex than it looks, because the sun and sky lift every top
 * face by about a third: on screen it reads as a light, warm oak that the desks and Maya stand out on.
 */
const PALE_OAK = "#b0aca4";

export interface IdeaLevelBuild extends LevelBuild {
  /** What Maya is doing right now, for the inspector. */
  ownerTask(): string | null;
}

/**
 * Half the copper dome: the back half of a shell, standing-seam copper outside and coffered cream
 * and gold inside, with a gold rim along the cut, an oculus and a small lantern on top, all on a
 * drum with round windows. The front of the house cuts it, so the inside shows.
 */
function buildDome(kit: Kit, parent: TransformNode, m: HeritageMats): void {
  const { x: cx, z: cz, r, drumY, drumH } = DOME;
  const base = drumY + drumH;
  const scene = kit.scene;
  /** Where the oculus opens, as an angle up from the springing line. */
  const TOP = 1.3;
  /** Thickness of the shell. */
  const T = 0.34;
  const arcOf = (radius: number, from: number, to: number, n: number): Vector3[] => {
    const pts: Vector3[] = [];
    for (let i = 0; i <= n; i++) {
      const phi = from + ((to - from) * i) / n;
      pts.push(new Vector3(Math.cos(phi) * radius, Math.sin(phi) * radius, 0));
    }
    return pts;
  };
  // The back half (+z) of a surface of revolution, open along the cut.
  const shell = (name: string, radius: number, mat: StandardMaterial): Mesh => {
    const s = MeshBuilder.CreateLathe(
      name,
      { shape: arcOf(radius, 0, TOP, 18), arc: 0.5, tessellation: 40, closed: false, sideOrientation: Mesh.DOUBLESIDE },
      scene,
    );
    s.position.set(cx, base, cz);
    return kit.finish(s, parent, mat, true);
  };
  // Outside: copper sheets with darker standing seams that meet at the top. (u runs up the
  // dome and v around it, so the seams are rows of the canvas.)
  const seams = kit.canvas("v2_domeCopper", 64, 64, (ctx, w, h) => {
    ctx.fillStyle = "#b8743c";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#c98a4e";
    ctx.fillRect(0, 10, w, 22);
    ctx.fillStyle = "#7c4526";
    ctx.fillRect(0, h - 8, w, 8);
  });
  seams.wrapU = Texture.WRAP_ADDRESSMODE;
  seams.wrapV = Texture.WRAP_ADDRESSMODE;
  seams.vScale = 14;
  const copper = new StandardMaterial("v2_domeCopperMat", scene);
  copper.diffuseTexture = seams;
  copper.specularColor = new Color3(0.28, 0.22, 0.16);
  copper.emissiveColor = new Color3(0.12, 0.07, 0.04);
  shell("v2_domeOuter", r, copper);
  // Inside: lit coffers, cream panels in a gold frame with a rosette, shrinking toward the oculus.
  const coffers = kit.canvas("v2_domeCoffers", 64, 64, (ctx, w, h) => {
    ctx.fillStyle = "#c9a14a";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#e4cf9f";
    ctx.fillRect(6, 6, w - 12, h - 12);
    ctx.fillStyle = "#f7eedb";
    ctx.fillRect(13, 13, w - 26, h - 26);
    ctx.fillStyle = "#d4a94e";
    ctx.beginPath();
    ctx.arc(w / 2, h / 2, 6, 0, Math.PI * 2);
    ctx.fill();
  });
  coffers.wrapU = Texture.WRAP_ADDRESSMODE;
  coffers.wrapV = Texture.WRAP_ADDRESSMODE;
  coffers.uScale = 5;
  coffers.vScale = 12;
  shell("v2_domeInner", r - T, kit.screen(coffers));
  // Gold along the cut and round the oculus, so the section reads as one clean shell.
  const tube = (name: string, path: Vector3[], radius: number): void => {
    const t = MeshBuilder.CreateTube(name, { path, radius, tessellation: 10, cap: Mesh.CAP_ALL }, scene);
    kit.finish(t, parent, m.gold, true);
  };
  const mid = r - T / 2;
  for (const side of [1, -1]) {
    tube(
      "v2_domeRim",
      arcOf(mid, 0, TOP, 18).map((p) => new Vector3(cx + side * p.x, base + p.y, cz)),
      T / 2 + 0.05,
    );
  }
  const ring = mid * Math.cos(TOP);
  const ringY = base + mid * Math.sin(TOP);
  const ringPath: Vector3[] = [];
  for (let i = 0; i <= 20; i++) {
    const a = (Math.PI * i) / 20;
    ringPath.push(new Vector3(cx + Math.cos(a) * ring, ringY, cz + Math.sin(a) * ring));
  }
  tube("v2_oculusRing", ringPath, T / 2 + 0.05);
  // The lantern over the oculus: render posts with glass between, a copper cap and a gold finial.
  const lr = ring - 0.1;
  const lh = 1.1;
  for (let k = 0; k <= 6; k++) {
    const a = (Math.PI * k) / 6;
    kit.block(parent, cx + Math.cos(a) * lr, ringY, cz + Math.sin(a) * lr, 0.3, lh, 0.3, m.render, { rotY: Math.PI / 2 - a });
    if (k < 6) {
      const b = a + Math.PI / 12;
      kit.block(parent, cx + Math.cos(b) * lr, ringY + 0.1, cz + Math.sin(b) * lr, 0.75, lh - 0.2, 0.08, m.pane, { rotY: Math.PI / 2 - b });
    }
  }
  const capR = lr + 0.25;
  const cap = MeshBuilder.CreateLathe(
    "v2_lanternCap",
    { shape: arcOf(capR, 0, Math.PI / 2, 8), arc: 0.5, tessellation: 20, closed: false, sideOrientation: Mesh.DOUBLESIDE },
    scene,
  );
  cap.position.set(cx, ringY + lh, cz);
  cap.scaling.y = 0.7;
  kit.finish(cap, parent, copper, true);
  const finialY = ringY + lh + capR * 0.7;
  kit.cyl(parent, cx, finialY - 0.1, cz + 0.1, 0.16, 1.2, m.gold, { tess: 8, dynamic: true });
  const ball = MeshBuilder.CreateSphere("v2_finial", { diameter: 0.42, segments: 8 }, scene);
  ball.position.set(cx, finialY + 1.15, cz + 0.1);
  kit.finish(ball, parent, m.gold, true);
  // The drum: a half ring of render with round windows, and a cornice where the dome springs.
  const rm = r - 0.25;
  const segs = Math.ceil((Math.PI * rm) / 0.5);
  for (let k = 0; k < segs; k++) {
    const a = ((k + 0.5) / segs) * Math.PI;
    const x = cx + Math.cos(a) * rm;
    const z = cz + Math.sin(a) * rm;
    const rot = Math.PI / 2 - a;
    kit.block(parent, x, drumY, z, 0.56, drumH, 0.5, m.render, { rotY: rot });
    kit.block(parent, cx + Math.cos(a) * (r + 0.05), base - 0.25, cz + Math.sin(a) * (r + 0.05), 0.6, 0.25, 0.4, m.stone, { rotY: rot });
    if (k % 3 === 1) {
      kit.block(parent, cx + Math.cos(a) * (r + 0.02), drumY + 0.4, cz + Math.sin(a) * (r + 0.02), 0.42, 0.6, 0.1, m.pane, { rotY: rot });
      kit.block(parent, cx + Math.cos(a) * (rm - 0.27), drumY + 0.4, cz + Math.sin(a) * (rm - 0.27), 0.42, 0.6, 0.06, m.daylight, { rotY: rot });
    }
  }
}

export function buildIdeaLevel(kit: Kit): IdeaLevelBuild {
  const scene = kit.scene;
  const root = new TransformNode("v2_lvl_idea", scene);
  const batch = new Batch(root, "idea");
  kit.useBatch(batch);
  const y = FLOOR_Y.idea;
  const info = levelInfo("idea");
  const r = rng(3);
  const hm = heritageMats(kit);

  const walnut = kit.mat("#ffffff", { texture: "walnut" });
  const white = kit.mat("#f4f1ea");
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  const leaf = kit.mat("#5ea347");
  const leafDark = kit.mat("#3f7f3a");
  const pot = kit.mat("#d8d2c6");
  const { tag } = buildShell(kit, {
    root,
    level: info,
    floorY: y,
    storey: "upper",
    // Pale oak in long, calm boards.
    floor: kit.mat(PALE_OAK, { texture: "paleOak" }),
    floorTile: 4,
    windows: { left: [-10.6, -6.2, -1.8], right: SIDE_BAYS, back: [-14.6, 1.2, 8.6, 15.4] },
    cove: true,
    rail: { skip: [[BALCONY.x0, BALCONY.x1]] },
  });

  const plant = (x: number, z: number, s = 1): void => {
    kit.block(root, x, y, z, 0.9 * s, 0.8 * s, 0.9 * s, pot);
    kit.block(root, x, y + 0.8 * s, z, 1.2 * s, 1.0 * s, 1.2 * s, leaf);
    kit.block(root, x + 0.15, y + 1.7 * s, z - 0.1, 0.7 * s, 0.7 * s, 0.7 * s, leafDark);
  };

  /* ---------------- The dome and the rotunda under it ---------------- */
  buildDome(kit, root, hm);
  // A half-round rug under the dome, in a mid-tone gold so the table and Maya read on it, with a
  // gold inlay and three columns carrying the dome's drum.
  const rug = MeshBuilder.CreateCylinder("v2_rotundaRug", { diameter: 11.2, height: 0.05, tessellation: 40, arc: 0.5, enclose: true }, scene);
  // A cylinder's arc runs round the -z side, so turn it to lie under the dome.
  rug.rotation.y = Math.PI;
  rug.position.set(DOME.x, y + 0.025, DOME.z);
  kit.finish(rug, root, kit.mat("#a5884f"));
  const inlayMat = kit.mat("#ffc35a", { unlit: true });
  for (let a = 0.05; a < Math.PI; a += Math.PI / 30) {
    kit.block(root, DOME.x + Math.cos(a) * 5.0, y + 0.05, DOME.z + Math.sin(a) * 5.0, 0.5, 0.03, 0.16, inlayMat, { rotY: Math.PI / 2 - a });
  }
  for (const deg of [25, 90, 155]) {
    const a = (deg * Math.PI) / 180;
    const cx = DOME.x + Math.cos(a) * (DOME.r - 0.45);
    const cz = DOME.z + Math.sin(a) * (DOME.r - 0.45);
    kit.block(root, cx, y, cz, 0.8, 0.4, 0.8, hm.stone);
    kit.cyl(root, cx, y + 0.4, cz, 0.56, ROOF_Y - y - 0.9, hm.render, { tess: 12 });
    kit.cyl(root, cx, y + 2.4, cz, 0.62, 0.12, kit.mat("#ffc35a", { unlit: true }), { tess: 12 });
    kit.block(root, cx, ROOF_Y - 0.5, cz, 0.85, 0.5, 0.85, hm.stone);
  }

  /* ---------------- The idea table and the brain ---------------- */
  const tableX = TABLE.x;
  const tableZ = TABLE.z;
  kit.cyl(root, tableX, y, tableZ, 1.2, 1.0, white, { tess: 20 });
  kit.cyl(root, tableX, y + 1.0, tableZ, 4.6, 0.16, kit.mat("#f8f6f1", { specular: 0.3 }), { tess: 32 });
  const projector = MeshBuilder.CreateTorus("v2_projector", { diameter: 3.3, thickness: 0.12, tessellation: 40 }, scene);
  projector.position.set(tableX, y + 1.24, tableZ);
  kit.finish(projector, root, kit.mat("#ffc35a", { unlit: true }), true);
  const beamMat = kit.mat("#ffd98a", { unlit: true, alpha: 0.09 });
  kit.cyl(root, tableX, y + 1.2, tableZ, 3.1, 1.0, beamMat, { top: 2.1, tess: 32, dynamic: true });

  const brain = new TransformNode("v2_brain", scene);
  const brainBatch = new Batch(brain, "brain");
  const prev = kit.useBatch(brainBatch);
  // One idea, two sides: a Claims & Care hemisphere and a Protection Advice hemisphere, joined by what they share.
  const hemisphere = (hexes: string[]) => hexes.map((hex) => kit.mat(hex, { emissive: 0.42 }));
  const claimsMats = hemisphere(["#f2b84b", "#d99a2b", "#ffe1a1"]);
  const protectionMats = hemisphere(["#2dd4bf", "#14a796", "#a7f3e7"]);
  const sharedMat = kit.mat(SIDES.shared.color, { emissive: 0.5 });
  const s = 0.3;
  for (let i = -6; i <= 6; i++) {
    for (let j = -4; j <= 4; j++) {
      for (let k = -5; k <= 5; k++) {
        const bx = i * s;
        const by = j * s;
        const bz = k * s;
        const q = (bx * bx) / (1.45 * 1.45) + (by * by) / (1.05 * 1.05) + (bz * bz) / (1.2 * 1.2);
        if (q > 1 || q < 0.6) continue;
        if (i === 0 && j > -2) continue; // the fissure between the hemispheres
        const tone = Math.abs(i * 7 + j * 13 + k * 5) % 3;
        const mat = i === 0 ? sharedMat : i < 0 ? claimsMats[tone] : protectionMats[tone];
        kit.block(brain, bx, by - s / 2, bz, s * 0.9, s * 0.9, s * 0.9, mat);
      }
    }
  }
  kit.useBatch(prev);
  brainBatch.flush();
  brain.parent = root;
  brain.position.set(tableX, y + 3.25, tableZ);
  kit.hitbox(root, tableX, y + 1.0, tableZ, 3.6, 3.6, 3.6, { type: "brain" });

  const sparks: Mesh[] = [];
  const sparkMat = kit.mat("#fff4c2", { unlit: true });
  for (let i = 0; i < 8; i++) {
    sparks.push(kit.block(brain, 0, 0, 0, 0.16, 0.16, 0.16, sparkMat, { dynamic: true }));
  }

  // Goal tiles orbiting the brain.
  const tiles = new TransformNode("v2_ideaTiles", scene);
  tiles.parent = root;
  tiles.position.set(tableX, y + 3.2, tableZ);
  const tileMeshes = IDEA.goals.map((g, i) => {
    const mm = kit.label(tiles, {
      lines: [{ text: g.tile, size: 54, weight: 800, color: "#ffd98a" }],
      width: 3.1,
      px: 420,
      pxHeight: 96,
      bg: "rgba(20,16,40,0.94)",
    });
    mm.billboardMode = Mesh.BILLBOARDMODE_ALL;
    return mm;
  });

  const brainRing = MeshBuilder.CreateTorus("v2_brainRing", { diameter: 5.2, thickness: 0.2, tessellation: 48 }, scene);
  brainRing.position.set(tableX, y + 0.12, tableZ);
  kit.finish(brainRing, root, kit.mat("#ffc35a", { unlit: true }), true);
  brainRing.setEnabled(false);

  /* ---------------- Maya ---------------- */
  const maya = new VoxelCharacter(scene, OWNER.id, OWNER.palette);
  maya.root.parent = root;
  maya.root.scaling.setAll(CHARACTER_SCALE);
  // Her home spot is just outside the rotunda, in the open office, so she reads beside her idea.
  const mayaAt: [number, number] = [DOME.x + DOME.r + 1.0, tableZ + 1.2];
  maya.root.position.set(mayaAt[0], y, mayaAt[1]);
  const mayaFacing = faceAngle(tableX - mayaAt[0], tableZ - mayaAt[1]);
  maya.root.rotation.y = mayaFacing;

  /* ---------------- Maya's desk and the North Star screen ---------------- */
  const deskX = 4.5;
  const deskZ = 2.4;
  kit.block(root, deskX, y + 1.08, deskZ, 4.4, 0.12, 1.9, walnut);
  kit.block(root, deskX - 2.0, y, deskZ, 0.14, 1.08, 1.7, steel);
  kit.block(root, deskX + 2.0, y, deskZ, 0.14, 1.08, 1.7, steel);
  kit.block(root, deskX, y + 1.2, deskZ - 0.96, 4.4, 0.06, 0.04, kit.mat("#ffc35a", { unlit: true }));
  const dash = kit.canvas("v2_northStar", 512, 300, (ctx, w, h) => {
    ctx.fillStyle = "#0f1a2e";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#ffc35a";
    ctx.font = '800 34px "Segoe UI", sans-serif';
    ctx.fillText("NORTH STAR", 26, 50);
    const bars = [0.86, 0.72, 0.93, 1];
    IDEA.goals.forEach((g, i) => {
      const by = 88 + i * 52;
      ctx.fillStyle = "#c6d2e6";
      ctx.font = '600 24px "Segoe UI", sans-serif';
      ctx.fillText(g.title, 26, by + 20);
      ctx.fillStyle = "rgba(255,255,255,0.12)";
      ctx.fillRect(220, by, 260, 26);
      ctx.fillStyle = ["#5ce1ff", "#34d399", "#a78bfa", "#ffc35a"][i];
      ctx.fillRect(220, by, 260 * bars[i], 26);
    });
  });
  const monitor = MeshBuilder.CreatePlane("v2_northStarScreen", { width: 2.3, height: 1.35 }, scene);
  monitor.position.set(deskX, y + 2.05, deskZ - 0.35);
  kit.finish(monitor, root, kit.screen(dash));
  kit.block(root, deskX, y + 1.2, deskZ - 0.3, 2.45, 1.55, 0.06, steel);
  kit.block(root, deskX + 1.5, y + 1.2, deskZ - 0.2, 0.3, 0.32, 0.3, kit.mat("#ffffff"));
  const chairLeather = kit.mat("#1f2d4a");
  kit.block(root, deskX, y + 0.55, deskZ + 1.45, 1.3, 0.25, 1.2, chairLeather);
  kit.block(root, deskX, y + 0.8, deskZ + 2.0, 1.3, 1.4, 0.2, chairLeather);
  kit.cyl(root, deskX, y, deskZ + 1.45, 0.2, 0.55, steel, { tess: 8 });

  /* ---------------- Vision board, bookshelf, lounge and the boardroom ---------------- */
  const board = kit.canvas("v2_visionBoard", 1024, 440, (ctx, w, h) => {
    ctx.fillStyle = "#fbfaf6";
    ctx.fillRect(0, 0, w, h);
    ctx.strokeStyle = "#c9a14a";
    ctx.lineWidth = 10;
    ctx.strokeRect(5, 5, w - 10, h - 10);
    ctx.fillStyle = "#0f1a2e";
    ctx.font = '700 30px "Segoe UI", sans-serif';
    ctx.fillText(`THE IDEA · ${IDEA.era.toUpperCase()}`, 48, 68, w - 96);
    ctx.font = '800 72px "Iowan Old Style", "Palatino Linotype", Georgia, serif';
    ctx.fillText(IDEA.name, 48, 150);
    // Two sides and the one group of agents between them, left to right as on the floors below.
    const cols = [
      { title: SIDES.claims.name.toUpperCase(), sub: "a small expert team", color: SIDES.claims.color },
      { title: "ONE GROUP OF AGENTS", sub: "dedicated + shared", color: SIDES.shared.color },
      { title: SIDES.protection.name.toUpperCase(), sub: "a small expert team", color: SIDES.protection.color },
    ];
    const colW = (w - 96 - 2 * 24) / 3;
    cols.forEach((c, i) => {
      const x = 48 + i * (colW + 24);
      ctx.fillStyle = c.color;
      ctx.fillRect(x, 196, colW, 132);
      ctx.fillStyle = "rgba(255,255,255,0.9)";
      ctx.fillRect(x + 8, 204, colW - 16, 116);
      ctx.fillStyle = "#0f1a2e";
      ctx.font = '800 30px "Segoe UI", sans-serif';
      ctx.fillText(c.title, x + 22, 252, colW - 40);
      ctx.fillStyle = "#53607a";
      ctx.font = '600 26px "Segoe UI", sans-serif';
      ctx.fillText(c.sub, x + 22, 296, colW - 40);
    });
    ctx.fillStyle = "#53607a";
    ctx.font = '600 28px "Segoe UI", sans-serif';
    ctx.fillText("Grounded in Microsoft IQ on Azure · agents recommend, people approve", 48, 392, w - 96);
  });
  const boardPlane = MeshBuilder.CreatePlane("v2_visionBoardPlane", { width: 7.2, height: 3.1 }, scene);
  boardPlane.position.set(-6.4, y + 2.75, INSIDE.z1 - 0.06);
  kit.finish(boardPlane, root, kit.screen(board));

  // The bookshelf on the back wall, beside the lift.
  kit.block(root, -13.4, y, INSIDE.z1 - 0.35, 6.2, 3.8, 0.7, walnut);
  const bookTones = ["#c0392b", "#2e86de", "#f1c40f", "#27ae60", "#8e44ad", "#e67e22", "#ecf0f1", "#16a085"].map((hex) => kit.mat(hex));
  for (let shelf = 0; shelf < 4; shelf++) {
    let bx = -16.3;
    while (bx < -10.6) {
      const bw = 0.18 + r() * 0.16;
      const bh = 0.5 + r() * 0.25;
      kit.block(root, bx + bw / 2, y + 0.18 + shelf * 0.9, INSIDE.z1 - 0.72, bw, bh, 0.5, bookTones[Math.floor(r() * bookTones.length)]);
      bx += bw + 0.03;
    }
  }

  // The lounge corner: a sofa, an armchair and a floor lamp, by the laneway windows.
  const sofa = kit.mat("#d9cbb3");
  const cushion = kit.mat("#c9a14a");
  kit.block(root, -17.0, y, -8.6, 6.0, 0.06, 6.4, kit.mat("#8a6f4f"));
  kit.block(root, -19.4, y, -8.6, 1.5, 0.75, 5.8, sofa);
  kit.block(root, -20.0, y + 0.75, -8.6, 0.4, 0.95, 5.8, sofa);
  kit.block(root, -19.2, y + 0.75, -7.0, 0.6, 0.6, 1.0, cushion);
  kit.block(root, -16.0, y, -10.8, 1.6, 0.75, 1.5, sofa);
  kit.block(root, -16.0, y + 0.75, -11.4, 1.6, 0.95, 0.35, sofa);
  kit.block(root, -16.8, y, -8.2, 1.6, 0.6, 1.4, walnut);
  kit.block(root, -16.8, y + 0.6, -8.0, 0.5, 0.06, 0.7, kit.mat("#ffffff"));
  kit.block(root, -19.6, y, -4.6, 0.14, 2.8, 0.14, steel);
  kit.block(root, -19.6, y + 2.6, -4.6, 0.8, 0.5, 0.8, kit.mat("#ffe7b0", { unlit: true }));

  // The boardroom table, with a screen on the side street wall.
  kit.block(root, 14.2, y + 1.0, 3.6, 6.0, 0.12, 2.2, walnut);
  for (const dx of [-2.6, 2.6]) kit.block(root, 14.2 + dx, y, 3.6, 0.2, 1.0, 1.6, steel);
  for (const dx of [-1.8, 0, 1.8]) {
    for (const dz of [-1.6, 1.6]) {
      kit.block(root, 14.2 + dx, y + 0.5, 3.6 + dz, 0.8, 0.14, 0.8, chairLeather);
      kit.block(root, 14.2 + dx, y + 0.64, 3.6 + dz * 1.22, 0.8, 0.8, 0.12, chairLeather);
    }
  }
  const boardScreen = kit.canvas("v2_boardroomScreen", 512, 300, (ctx, w, h) => {
    ctx.fillStyle = "#0f1a2e";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#ffc35a";
    ctx.font = '800 34px "Segoe UI", sans-serif';
    ctx.fillText("BOTH SIDES, THIS WEEK", 26, 52);
    const rows: Array<[string, string, number]> = [
      ["Protection Advice", SIDES.protection.color, 0.78],
      ["Claims & Care", SIDES.claims.color, 0.64],
      ["Approved by people", "#ffc35a", 1],
    ];
    rows.forEach(([label, color, f], i) => {
      const by = 96 + i * 62;
      ctx.fillStyle = "#c6d2e6";
      ctx.font = '600 26px "Segoe UI", sans-serif';
      ctx.fillText(label, 26, by + 22);
      ctx.fillStyle = "rgba(255,255,255,0.12)";
      ctx.fillRect(270, by, 210, 28);
      ctx.fillStyle = color;
      ctx.fillRect(270, by, 210 * f, 28);
    });
  });
  const boardMesh = MeshBuilder.CreatePlane("v2_boardroomScreenPlane", { width: 3.6, height: 2.1 }, scene);
  boardMesh.rotation.y = Math.PI / 2;
  boardMesh.position.set(INSIDE.x1 - 0.08, y + 2.7, 3.6);
  kit.finish(boardMesh, root, kit.screen(boardScreen));
  kit.block(root, INSIDE.x1 - 0.06, y + 1.55, 3.6, 0.08, 2.3, 3.8, steel);

  // A scale model of Guru House on a plinth, on a rug with two reading chairs, by the balcony.
  const model = { x: 7.6, z: -7.4 };
  kit.block(root, 12.0, y, -7.2, 11.6, 0.05, 6.6, kit.mat("#4a5a78"));
  for (const [x, z, w, d] of [
    [12.0, -10.45, 11.6, 0.18],
    [12.0, -3.95, 11.6, 0.18],
    [6.25, -7.2, 0.18, 6.6],
    [17.75, -7.2, 0.18, 6.6],
  ] as const) {
    kit.block(root, x, y + 0.01, z, w, 0.05, d, kit.mat("#c9a14a"));
  }
  kit.block(root, model.x, y, model.z, 2.8, 0.95, 2.1, hm.render);
  kit.block(root, model.x, y + 0.95, model.z, 3.0, 0.1, 2.3, hm.stone);
  kit.block(root, model.x, y + 1.05, model.z, 2.4, 0.12, 1.6, hm.bluestone);
  kit.block(root, model.x, y + 1.17, model.z + 0.1, 2.2, 1.2, 1.3, hm.brick);
  for (const yy of [1.55, 1.95, 2.37]) kit.block(root, model.x, y + yy, model.z + 0.1, 2.28, 0.07, 1.38, hm.render);
  for (let i = -2; i <= 2; i++) kit.block(root, model.x + i * 0.4, y + 1.62, model.z - 0.56, 0.18, 0.26, 0.04, hm.pane);
  const mini = MeshBuilder.CreateSphere("v2_modelDome", { diameter: 0.9, segments: 10, slice: 0.5 }, scene);
  mini.position.set(model.x - 0.5, y + 2.44, model.z + 0.2);
  kit.finish(mini, root, hm.copper);
  const modelTag = kit.label(root, {
    lines: [
      { text: "GURU HOUSE · 1889", size: 40, weight: 800, color: "#7a5a12", serif: true },
      { text: "the bank, and now the practice", size: 26, weight: 600, color: "#5b6476" },
    ],
    width: 2.4,
    px: 480,
    bg: "rgba(255,251,242,0.97)",
  });
  modelTag.position.set(model.x, y + 0.5, model.z - 1.07);
  const chairFabric = kit.mat("#c9a14a");
  const chairDark = kit.mat("#8a6a2c");
  for (const [cx, dir] of [
    [12.2, 1],
    [16.4, -1],
  ] as const) {
    kit.block(root, cx, y, -7.0, 1.5, 0.6, 1.5, chairFabric);
    kit.block(root, cx - dir * 0.62, y + 0.6, -7.0, 0.26, 0.9, 1.5, chairDark);
    for (const dz of [-0.68, 0.68]) kit.block(root, cx, y + 0.6, -7.0 + dz, 1.5, 0.35, 0.16, chairDark);
  }
  kit.cyl(root, 14.3, y, -7.0, 1.3, 0.55, walnut, { tess: 16 });
  kit.block(root, 14.1, y + 0.55, -7.1, 0.5, 0.08, 0.38, kit.mat("#f4f1ea"));
  kit.block(root, 18.8, y, -9.2, 0.14, 2.8, 0.14, steel);
  kit.block(root, 18.8, y + 2.6, -9.2, 0.8, 0.5, 0.8, kit.mat("#ffe7b0", { unlit: true }));
  plant(-11.2, 5.6, 1.1);
  plant(10.4, 7.4, 1.0);
  plant(19.4, -11.6, 1.1);
  plant(1.6, -11.6, 0.9);

  // Biscuit, the office dog, asleep on her bed.
  const dogFur = kit.mat("#d9a35b");
  const dogDark = kit.mat("#b07a3a");
  const dogX = -17.0;
  const dogZ = -2.6;
  kit.cyl(root, dogX, y, dogZ, 2.0, 0.26, kit.mat("#1f3a5f"), { tess: 20 });
  kit.block(root, dogX - 0.1, y + 0.26, dogZ, 1.35, 0.55, 0.62, dogFur);
  kit.block(root, dogX + 0.8, y + 0.3, dogZ, 0.55, 0.5, 0.56, dogFur);
  kit.block(root, dogX + 1.2, y + 0.32, dogZ, 0.3, 0.24, 0.32, kit.mat("#e9c48a"));
  kit.block(root, dogX + 0.75, y + 0.6, dogZ - 0.28, 0.18, 0.3, 0.1, dogDark);
  kit.block(root, dogX + 0.75, y + 0.6, dogZ + 0.28, 0.18, 0.3, 0.1, dogDark);
  const tail = new TransformNode("v2_dogTail", scene);
  tail.parent = root;
  tail.position.set(dogX - 0.8, y + 0.55, dogZ);
  kit.block(tail, -0.32, -0.06, 0, 0.6, 0.12, 0.12, dogFur, { dynamic: true });

  /* ---------------- The balcony over the tram line ---------------- */
  const bw = BALCONY.x1 - BALCONY.x0;
  const bcx = (BALCONY.x0 + BALCONY.x1) / 2;
  const bcz = (BALCONY.z0 + HOUSE.z0) / 2;
  kit.block(root, bcx, y - 0.35, bcz, bw, 0.35, HOUSE.z0 - BALCONY.z0, hm.stone);
  kit.block(root, bcx, y - 0.02, bcz, bw - 0.1, 0.04, HOUSE.z0 - BALCONY.z0 - 0.1, kit.mat(PALE_OAK, { texture: "paleOak" }), { tile: 4 });
  for (let x = BALCONY.x0 + 0.5; x < BALCONY.x1; x += 1.4) {
    kit.block(root, x, y - 0.65, HOUSE.z0 - 0.4, 0.3, 0.3, 0.8, hm.stone);
  }
  // Cast-iron lace railing on three sides.
  kit.block(root, bcx, y + 1.08, BALCONY.z0 + 0.06, bw, 0.1, 0.12, hm.iron);
  for (const x of [BALCONY.x0 + 0.06, BALCONY.x1 - 0.06]) kit.block(root, x, y + 1.08, bcz, 0.12, 0.1, HOUSE.z0 - BALCONY.z0, hm.iron);
  for (const x of [BALCONY.x0 + 0.06, BALCONY.x1 - 0.06]) kit.block(root, x, y, BALCONY.z0 + 0.06, 0.14, 1.18, 0.14, hm.iron);
  const front = lace(kit, root, "balconyFront", bw - 0.2, 1.0, Math.round(bw / 1.1));
  front.position.set(bcx, y + 0.56, BALCONY.z0 + 0.06);
  for (const x of [BALCONY.x0 + 0.06, BALCONY.x1 - 0.06]) {
    const side = lace(kit, root, `balconySide${x}`, HOUSE.z0 - BALCONY.z0 - 0.1, 1.0, 2);
    side.rotation.y = Math.PI / 2;
    side.position.set(x, y + 0.56, bcz);
  }
  // A telescope pointed at the tram line, and a bistro table for two.
  kit.block(root, 15.2, y, BALCONY.z0 + 0.9, 0.1, 1.5, 0.1, steel, { rotY: 0.4 });
  kit.block(root, 14.9, y, BALCONY.z0 + 1.2, 0.1, 1.5, 0.1, steel, { rotY: -0.4 });
  const scope = kit.cyl(root, 15.0, y + 1.5, BALCONY.z0 + 0.8, 0.32, 1.6, kit.mat("#c9a14a", { specular: 0.5 }), { tess: 10 });
  scope.rotation.x = -1.2;
  kit.cyl(root, 10.2, y, BALCONY.z0 + 1.2, 0.12, 1.0, hm.iron, { tess: 8 });
  kit.cyl(root, 10.2, y + 1.0, BALCONY.z0 + 1.2, 1.0, 0.06, white, { tess: 14 });
  for (const dx of [-0.95, 0.95]) {
    kit.block(root, 10.2 + dx, y, BALCONY.z0 + 1.2, 0.6, 0.55, 0.6, hm.iron);
    kit.block(root, 10.2 + dx * 1.18, y + 0.55, BALCONY.z0 + 1.2, 0.12, 0.7, 0.6, hm.iron);
  }

  kit.useBatch(null);
  batch.flush();

  // Maya strolls between her table, the vision board, the bookshelf, her desk, Biscuit, the
  // boardroom and the balcony.
  const book = new SpotBook();
  const mayaHome: Spot = { at: mayaAt, face: mayaFacing, via: [[-2.0, CZ], [-2.0, mayaAt[1]]] };
  const mayaPlaces: Array<{ spot: Spot; task: string; where: string; weight: number }> = [
    { spot: { at: [-6.4, 7.0], face: 0, via: [[-6.4, CZ]] }, task: "Reviewing the vision board", where: "the vision board", weight: 2 },
    { spot: { at: [-12.6, 7.0], face: 0, via: [[-12.6, CZ]] }, task: "Picking out a book", where: "the bookshelf", weight: 1 },
    { spot: { at: [6.0, 0.6], face: 0, via: [[6.0, CZ]] }, task: "Checking the North Star", where: "her desk", weight: 2 },
    { spot: { at: [-15.4, -1.4], face: faceAngle(-1.4, -1.0), via: [[-15.4, CZ]] }, task: "Saying hello to Biscuit", where: "Biscuit", weight: 1 },
    { spot: { at: [10.4, 3.6], face: Math.PI / 2, via: [[10.4, CZ]] }, task: "Reading the board pack", where: "the boardroom", weight: 1 },
    { spot: { at: [model.x + 1.9, model.z + 0.2], face: -Math.PI / 2, via: [[model.x + 1.9, CZ]] }, task: "Looking over the model of the old bank", where: "the model of Guru House", weight: 1 },
    {
      spot: { at: [14.0, BALCONY.z0 + 0.9], face: Math.PI, via: [[8.0, CZ], [8.0, BALCONY.z0 + 0.9]] },
      task: "Watching the trams come in",
      where: "the balcony",
      weight: 2,
    },
  ];
  const mayaWeight = mayaPlaces.reduce((sum, p) => sum + p.weight, 0);
  const mayaWalker = new Wanderer({
    body: { root: maya.root, walk: (on) => maya.setWalking(on) },
    home: mayaHome,
    homeName: "the table",
    plan: (): Outing | null => {
      let roll = r() * mayaWeight;
      const place = mayaPlaces.find((p) => (roll -= p.weight) <= 0) ?? mayaPlaces[0];
      return { spot: place.spot, task: place.task, where: place.where, dwell: 5 + r() * 4 };
    },
    homeTask: () => ["Thinking about both sides", "Planning the next quarter", "Reading the teams’ updates"][Math.floor(r() * 3)],
    homeDwell: [12, 24],
    speed: 2.0,
    book,
    rand: r,
    bubble: kit.bubble(maya.root, 2.45, OWNER.color, CHARACTER_SCALE),
    sway: 0.12,
  });

  const anchors = new Map<string, Anchor>([
    ["owner", { node: maya.root, group: "idea", dy: 2.4 }],
    ["brain", { node: brain, group: "idea", dy: 0 }],
  ]);

  let highlight = { owner: false, brain: false };
  let introT = -1;
  return {
    id: "idea",
    root,
    tag,
    anchors,
    update(dt: number, t: number): void {
      mayaWalker.update(dt);
      maya.update(dt);
      const lit = highlight.brain || introT >= 0;
      if (introT >= 0) {
        introT += dt;
        if (introT > 2.4) introT = -1;
      }
      const pulse = 1 + Math.sin(t * 2.2) * 0.025 + (lit ? 0.07 + Math.sin(t * 6) * 0.03 : 0);
      brain.rotation.y += dt * (lit ? 0.9 : 0.35);
      brain.scaling.setAll(pulse);
      brain.position.y = y + 3.25 + Math.sin(t * 1.1) * 0.12;
      sparks.forEach((sp, i) => {
        const a = t * (1.6 + i * 0.17) + i * 0.8;
        sp.position.set(Math.cos(a) * 0.9, Math.sin(a * 1.3) * 0.55, Math.sin(a) * 0.75);
      });
      tileMeshes.forEach((mm, i) => {
        const a = (i / tileMeshes.length) * Math.PI * 2 + t * 0.25;
        mm.position.set(Math.cos(a) * ORBIT.rx, Math.sin(t * 1.4 + i * 1.6) * 0.25, Math.sin(a) * ORBIT.rz);
      });
      projector.scaling.setAll(1 + Math.sin(t * 3) * 0.04);
      if (brainRing.isEnabled()) brainRing.scaling.setAll(1 + Math.sin(t * 4) * 0.06);
      tail.rotation.y = Math.sin(t * 5) * 0.5;
    },
    setHighlights(h: Highlights): void {
      highlight = { owner: h.owner, brain: h.brain };
      maya.setHighlight(h.owner, GOLD);
      mayaWalker.hold(h.owner);
      brainRing.setEnabled(h.brain);
    },
    intro(): void {
      introT = 0;
    },
    ownerTask(): string | null {
      return mayaWalker.task;
    },
  };
}

/**
 * The roof: the main cornice and the parapet, with GURU HOUSE · 1889 over
 * the street, a gallery rail round the dome, solar panels and the lift up
 * from below. Life Advice Guru's party for its clients fills the rest of it (the deck,
 * the sign and the party are built in party.ts).
 */
export function buildRoof(kit: Kit): TransformNode {
  const scene = kit.scene;
  const root = new TransformNode("v2_roof", scene);
  const batch = new Batch(root, "roof");
  kit.useBatch(batch);
  const hm = heritageMats(kit);
  const y = ROOF_Y;
  const top = y + SLAB;
  const concrete = kit.mat("#ffffff", { texture: "concrete" });
  // The slab, with a half-round notch at the front where the dome's drum comes up through it.
  const notch = DOME.r + 0.05;
  const cz = (HOUSE.z0 + HOUSE.z1) / 2;
  kit.block(root, (HOUSE.x0 + DOME.x - notch) / 2, y, cz, DOME.x - notch - HOUSE.x0, SLAB, HOUSE.z1 - HOUSE.z0, concrete);
  kit.block(root, (DOME.x + notch + HOUSE.x1) / 2, y, cz, HOUSE.x1 - DOME.x - notch, SLAB, HOUSE.z1 - HOUSE.z0, concrete);
  const strip = 0.5;
  for (let x = DOME.x - notch; x < DOME.x + notch - 0.01; x += strip) {
    const mid = x + strip / 2 - DOME.x;
    const z0 = HOUSE.z0 + Math.sqrt(Math.max(0, notch * notch - mid * mid));
    kit.block(root, x + strip / 2, y, (z0 + HOUSE.z1) / 2, strip, SLAB, HOUSE.z1 - z0, concrete);
  }
  // The main cornice and dentils on every outside face, and a gold edge strip along the front.
  const faces = [
    { axis: "x" as const, at: HOUSE.z0, out: -1 as const, from: HOUSE.x0, to: HOUSE.x1 },
    { axis: "x" as const, at: HOUSE.z1, out: 1 as const, from: HOUSE.x0, to: HOUSE.x1 },
    { axis: "z" as const, at: HOUSE.x0, out: -1 as const, from: HOUSE.z0, to: HOUSE.z1 },
    { axis: "z" as const, at: HOUSE.x1, out: 1 as const, from: HOUSE.z0, to: HOUSE.z1 },
  ];
  for (const f of faces) {
    const len = f.to - f.from;
    const mid = (f.from + f.to) / 2;
    const place = (yy: number, h: number, d: number, mat = hm.render): void => {
      const c = f.at + (f.out * d) / 2;
      if (f.axis === "x") kit.block(root, mid, yy, c, len + 0.8, h, d, mat);
      else kit.block(root, c, yy, mid, d, h, len + 0.8, mat);
    };
    if (f.at === HOUSE.z0) {
      // The front runs either side of the dome.
      for (const [a, b] of [
        [HOUSE.x0, DOME.x - notch],
        [DOME.x + notch, HOUSE.x1],
      ] as const) {
        kit.block(root, (a + b) / 2, y - 0.05, HOUSE.z0 - 0.25, b - a, 0.6, 0.5, hm.render);
        kit.block(root, (a + b) / 2, y + 0.18, HOUSE.z0 - 0.52, b - a, 0.12, 0.06, kit.mat("#ffc35a", { unlit: true }));
      }
      continue;
    }
    place(y - 0.05, 0.6, 0.5);
    place(y - 0.4, 0.35, 0.25, hm.stone);
  }
  // The parapet: balustrades round the roof, with piers and urns at the corners.
  const rail = { rail: hm.render, baluster: hm.render };
  const inset = 0.32;
  balustrade(kit, root, HOUSE.x0 + inset, HOUSE.z1 - inset, HOUSE.x1 - inset, HOUSE.z1 - inset, top, rail);
  balustrade(kit, root, HOUSE.x0 + inset, HOUSE.z0 + inset, HOUSE.x0 + inset, HOUSE.z1 - inset, top, rail);
  balustrade(kit, root, HOUSE.x1 - inset, HOUSE.z0 + inset, HOUSE.x1 - inset, HOUSE.z1 - inset, top, rail);
  // Along the front, a gap at the corner just wide enough for the bridge from the glass lift.
  balustrade(kit, root, LIFT.x + 1.25, HOUSE.z0 + inset, DOME.x - notch - 0.3, HOUSE.z0 + inset, top, rail);
  kit.block(root, (HOUSE.x0 + inset + LIFT.x - 1.25) / 2, top, HOUSE.z0 + inset, LIFT.x - 1.25 - HOUSE.x0 - inset, 1.15, 0.46, hm.render);
  // Over the street, the name: GURU HOUSE · 1889 on a raised panel under a pediment.
  const panel = { x0: DOME.x + notch + 0.6, x1: HOUSE.x1 - inset };
  const pw = panel.x1 - panel.x0;
  const pcx = (panel.x0 + panel.x1) / 2;
  kit.block(root, pcx, top, HOUSE.z0 + 0.3, pw, 1.5, 0.5, hm.render);
  kit.block(root, pcx, top + 1.5, HOUSE.z0 + 0.3, pw + 0.3, 0.22, 0.7, hm.stone);
  for (let k = 0; k < 4; k++) {
    const w = 9.4 - k * 2.3;
    kit.block(root, pcx, top + 1.72 + k * 0.32, HOUSE.z0 + 0.3, w, 0.32, 0.5, k === 3 ? hm.stone : hm.render);
  }
  for (const x of [panel.x0, panel.x1, HOUSE.x0 + inset, DOME.x - notch - 0.3]) {
    kit.block(root, x, top, HOUSE.z0 + 0.3, 0.7, 1.8, 0.7, hm.render);
    kit.cyl(root, x, top + 1.8, HOUSE.z0 + 0.3, 0.6, 0.5, hm.stone, { top: 0.36, tess: 10 });
    kit.cyl(root, x, top + 2.3, HOUSE.z0 + 0.3, 0.2, 0.3, hm.stone, { tess: 8 });
  }
  for (const [x, z] of [
    [HOUSE.x0 + inset, HOUSE.z1 - inset],
    [HOUSE.x1 - inset, HOUSE.z1 - inset],
  ] as const) {
    kit.cyl(root, x, top + 1.27, z, 0.6, 0.5, hm.stone, { top: 0.36, tess: 10 });
  }
  const name = kit.canvas("v2_houseName", 2048, 150, (ctx, w, h) => {
    ctx.clearRect(0, 0, w, h);
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.font = '700 108px "Iowan Old Style", "Palatino Linotype", Georgia, serif';
    const text = "GURU HOUSE · 1889";
    ctx.fillStyle = "rgba(255,248,230,0.95)";
    ctx.fillText(text, w / 2 + 4, h / 2 + 5, w - 40);
    ctx.fillStyle = "#6e5320";
    ctx.fillText(text, w / 2, h / 2, w - 40);
  });
  // Sized to sit inside the 1.5-high panel.
  const nameH = 1.4;
  const nameMesh = MeshBuilder.CreatePlane("v2_houseNamePlane", { width: (nameH * 2048) / 150, height: nameH }, scene);
  nameMesh.position.set(pcx, top + 0.75, HOUSE.z0 + 0.03);
  kit.finish(nameMesh, root, kit.screen(name, true), true);
  // A gallery rail along the dome's cut, so the guests can look down at the brain.
  const segs = 10;
  for (let k = 0; k < segs; k++) {
    const a0 = (k / segs) * Math.PI;
    const a1 = ((k + 1) / segs) * Math.PI;
    const rr = DOME.r + 0.45;
    const p0 = [DOME.x + Math.cos(a0) * rr, HOUSE.z0 + Math.sin(a0) * rr] as const;
    const p1 = [DOME.x + Math.cos(a1) * rr, HOUSE.z0 + Math.sin(a1) * rr] as const;
    if (k === 0 || k === segs - 1) continue;
    const len = Math.hypot(p1[0] - p0[0], p1[1] - p0[1]);
    const rot = Math.atan2(p1[0] - p0[0], p1[1] - p0[1]);
    kit.block(root, (p0[0] + p1[0]) / 2, top + 1.0, (p0[1] + p1[1]) / 2, 0.1, 0.1, len, hm.iron, { rotY: rot });
    kit.block(root, p0[0], top, p0[1], 0.1, 1.1, 0.1, hm.iron);
  }
  // Solar panels on low frames at the back, clear of the lift and the sign.
  const panelMat = kit.mat("#1e3a8a", { specular: 0.7 });
  const frame = kit.mat("#cbd5e1");
  for (const [px, pz] of [
    [-15.6, 2.4],
    [-13.4, 2.4],
    [-11.2, 2.4],
    [-15.6, 5.6],
    [-13.4, 5.6],
    [-11.2, 5.6],
  ] as const) {
    kit.block(root, px, top + 0.18, pz + 0.6, 0.1, 0.42, 0.1, frame);
    const p = kit.block(root, px, top + 0.48, pz, 2.0, 0.08, 1.4, panelMat);
    p.rotation.x = -0.32;
  }
  // The staff lift comes up through the roof at the back: its overrun, with brass cage doors.
  const ccx = (CORE.x0 + CORE.x1) / 2;
  const ccz = (CORE.z0 + CORE.z1) / 2;
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  kit.block(root, ccx, top, ccz, CORE.x1 - CORE.x0, 2.9, CORE.z1 - CORE.z0, hm.render);
  kit.block(root, ccx, top + 2.9, ccz, CORE.x1 - CORE.x0 + 0.3, 0.16, CORE.z1 - CORE.z0 + 0.3, hm.stone);
  const brass = kit.mat("#c9a14a", { specular: 0.5 });
  kit.block(root, CORE.x1 + 0.02, top + 0.18, ccz, 0.04, 2.3, 1.9, kit.mat("#1d2433"));
  for (let i = 0; i <= 7; i++) kit.block(root, CORE.x1 + 0.06, top + 0.18, ccz - 0.95 + i * 0.27, 0.05, 2.3, 0.05, brass);
  kit.block(root, CORE.x1 + 0.06, top + 2.48, ccz, 0.08, 0.14, 2.1, steel);
  kit.useBatch(null);
  batch.flush();
  return root;
}
