/**
 * Level 2 · The People — two small teams, one for each side of the practice,
 * at standing desks inside the old brick shell: the Claims & Care Team on the
 * left, the Protection Advice Team on the right, and in the middle Olivia at
 * the front door, where every request comes in, with Theo behind her leading
 * both teams. Every desk sits directly above that person's agent bay on
 * Level 1, so the thread between a person and their agent runs straight down
 * through the floor. Through the day people move about the floor: to the
 * huddle table where both teams meet, the quiet room for sensitive calls, the
 * coffee bar, the printer and the team boards, and to each other's desks.
 */
import { Color3, Mesh, MeshBuilder, TransformNode } from "@babylonjs/core";
import { PALETTES } from "../../characterPalettes";
import { STAFF_PERSONAS, findStaffById } from "../../personaData";
import { VoxelCharacter } from "../../voxelCharacter";
import { IDEA, SIDES, levelInfo } from "../houseData";
import { Batch, type Kit, rng } from "../kit";
import { CHARACTER_SCALE, DOME, FLOOR_Y, INSIDE, SPARE_CELLS, STATIONS, WINGS } from "../layout";
import { SIDE_BAYS, buildShell } from "../shell";
import type { Anchor, Highlights, LevelBuild } from "../types";
import { SpotBook, Wanderer, type Outing, type Pt, type Spot } from "../wander";

const CORAL = Color3.FromHexString("#ff8f6b");
/** The aisle between the two rows of desks. */
const CZ = -2.2;
/** The huddle table, between Olivia at the front door and Theo. */
const HUDDLE = { x: 0, z: 0.4 };

type Errand =
  | "coffee"
  | "huddle"
  | "printer"
  | "process"
  | "claimsBoard"
  | "protectionBoard"
  | "quiet"
  | "lookout"
  | `visit:${string}`;

/** Where each person likes to go, and how often (the visits are to their teammates, Olivia and Theo). */
const ERRANDS: Record<string, Array<[Errand, number]>> = {
  "onboarding-1": [["printer", 2], ["huddle", 2], ["coffee", 2], ["visit:paraplanning-1", 1], ["visit:claims-1", 1], ["visit:lead-1", 1], ["visit:fincrime-1", 1], ["lookout", 1]],
  "paraplanning-1": [["visit:research-1", 2], ["protectionBoard", 2], ["huddle", 1], ["coffee", 1], ["visit:underwriting-1", 1], ["printer", 1]],
  "research-1": [["visit:paraplanning-1", 2], ["protectionBoard", 1], ["coffee", 1], ["lookout", 1], ["huddle", 1]],
  "underwriting-1": [["visit:paraplanning-1", 1], ["coffee", 2], ["protectionBoard", 1], ["printer", 1], ["visit:comms-1", 1], ["lookout", 1]],
  "claims-1": [["quiet", 2], ["claimsBoard", 2], ["visit:fincrime-1", 1], ["visit:comms-1", 1], ["coffee", 1], ["huddle", 1]],
  "comms-1": [["quiet", 1], ["huddle", 2], ["visit:claims-1", 1], ["visit:onboarding-1", 1], ["coffee", 1], ["claimsBoard", 1]],
  "fincrime-1": [["visit:lead-1", 2], ["visit:onboarding-1", 1], ["visit:claims-1", 1], ["printer", 1], ["lookout", 1], ["coffee", 1]],
  "lead-1": [["huddle", 2], ["process", 2], ["visit:onboarding-1", 1], ["visit:claims-1", 1], ["visit:paraplanning-1", 1], ["visit:comms-1", 1], ["visit:underwriting-1", 1], ["coffee", 1], ["lookout", 1]],
};

export interface PeopleLevelBuild extends LevelBuild {
  /** What a person is doing right now, for the inspector. */
  task(id: string): string | null;
}

export function buildPeopleLevel(kit: Kit): PeopleLevelBuild {
  const scene = kit.scene;
  const root = new TransformNode("v2_lvl_people", scene);
  const batch = new Batch(root, "people");
  kit.useBatch(batch);
  const y = FLOOR_Y.people;
  const info = levelInfo("people");

  const oak = kit.mat("#fff4e2", { texture: "oak" });
  const walnut = kit.mat("#ffffff", { texture: "walnut" });
  const white = kit.mat("#f4f1ea");
  const steel = kit.mat("#2b3240", { specular: 0.25 });
  const leaf = kit.mat("#5ea347");
  const leafDark = kit.mat("#3f7f3a");
  const pot = kit.mat("#d8d2c6");
  const dark = kit.mat("#1f2937");
  const { tag } = buildShell(kit, {
    root,
    level: info,
    floorY: y,
    storey: "upper",
    floor: oak,
    windows: { left: [-10.6, -6.2, -1.8], right: SIDE_BAYS, back: [-6.25, 6.25, 17.6] },
    cove: true,
    rail: {},
  });

  /* ---------------- The two wings and the middle ---------------- */
  // Mid-tone rugs (each side's colour, about L* 64 once lit): pale ones wash out to white under the
  // sun and sky, and then the white desks and everything else on the floor disappears into them.
  const rugZ0 = -11.6;
  const rugZ1 = 7.6;
  const rug = (x0: number, x1: number, hex: string, edge: string): void => {
    kit.block(root, (x0 + x1) / 2, y, (rugZ0 + rugZ1) / 2, x1 - x0 - 0.3, 0.05, rugZ1 - rugZ0, kit.mat(hex));
    kit.block(root, (x0 + x1) / 2, y + 0.05, rugZ0 + 0.1, x1 - x0 - 0.3, 0.03, 0.12, kit.mat(edge, { unlit: true }));
  };
  rug(WINGS.claims.x0, WINGS.claims.x1, "#86734d", SIDES.claims.color);
  rug(WINGS.lead.x0, WINGS.lead.x1, "#707da7", SIDES.shared.color);
  rug(WINGS.protection.x0, WINGS.protection.x1, "#3a8787", SIDES.protection.color);
  const floorText = (text: string, x: number, z: number, width: number, color: string): void => {
    const decal = kit.label(root, {
      lines: [{ text, size: 60, weight: 800, color }],
      width,
      px: 1024,
      pxHeight: 100,
    });
    decal.rotation.x = Math.PI / 2;
    decal.position.set(x, y + 0.07, z);
  };
  const claimsX = (WINGS.claims.x0 + WINGS.claims.x1) / 2;
  const protectionX = (WINGS.protection.x0 + WINGS.protection.x1) / 2;
  floorText("CLAIMS & CARE TEAM", claimsX, -9.0, 11, "#3d2b06");
  floorText("PROTECTION ADVICE TEAM", protectionX, -9.0, 12, "#053b36");
  floorText("THE FRONT DOOR", 0, -9.0, 5.6, "#1c2550");
  floorText("LEADING BOTH TEAMS", 0, 6.7, 5.8, "#1c2550");

  // A sign for each side on the back wall: the side's name and what it looks after.
  const sideSign = (side: "claims" | "protection", x: number, ink: string): void => {
    const s = SIDES[side];
    const sign = kit.label(root, {
      lines: [
        { text: s.name.toUpperCase(), size: 60, weight: 800, color: ink },
        { text: s.covers, size: 32, weight: 600, color: "#3b4557" },
      ],
      width: 6.4,
      px: 768,
      bg: "rgba(255,251,242,0.96)",
      accent: s.color,
      bar: "top",
    });
    sign.position.set(x, y + 3.55, INSIDE.z1 - 0.06);
  };
  sideSign("claims", -12.1, "#8a6a1f");
  sideSign("protection", 12.1, "#0f766e");

  // One process for both sides, on the back wall behind Theo.
  const process = kit.canvas("v2_processBoard", 1024, 300, (ctx, w) => {
    ctx.fillStyle = "#0f1a2e";
    ctx.fillRect(0, 0, w, 300);
    ctx.fillStyle = "#ffc35a";
    ctx.font = '800 40px "Segoe UI", sans-serif';
    ctx.fillText("ONE PROCESS FOR BOTH SIDES", 40, 64);
    const colW = (w - 80) / IDEA.lifecycle.length;
    IDEA.lifecycle.forEach((step, i) => {
      const x = 40 + i * colW;
      ctx.fillStyle = "rgba(255,255,255,0.08)";
      ctx.fillRect(x, 104, colW - 18, 150);
      ctx.fillStyle = ["#0ea5e9", "#ef4444", "#a855f7", "#10b981", "#ec4899"][i];
      ctx.fillRect(x, 104, colW - 18, 10);
      ctx.fillStyle = "#ffffff";
      ctx.font = '800 64px "Segoe UI", sans-serif';
      ctx.fillText(String(i + 1), x + 18, 194);
      ctx.font = '700 32px "Segoe UI", sans-serif';
      ctx.fillText(step, x + 18, 238, colW - 40);
    });
  });
  const board = MeshBuilder.CreatePlane("v2_processBoardPlane", { width: 7.6, height: 2.2 }, scene);
  board.position.set(0, y + 3.2, INSIDE.z1 - 0.06);
  kit.finish(board, root, kit.screen(process));

  // A holo team board on a stand at the back of each wing.
  const teamBoard = (side: "claims" | "protection", x: number, cards: string[]): void => {
    const s = SIDES[side];
    const tex = kit.canvas(`v2_teamBoard_${side}`, 512, 320, (ctx, w, h) => {
      ctx.fillStyle = "#0b1426";
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = s.color;
      ctx.fillRect(0, 0, w, 10);
      ctx.fillStyle = "#e2e8f0";
      ctx.font = '800 30px "Segoe UI", sans-serif';
      ctx.fillText(`${s.name.toUpperCase()} · THIS WEEK`, 22, 52, w - 44);
      const cols = ["To do", "With an agent", "Waiting on a person"];
      cols.forEach((c, i) => {
        const cx = 22 + i * 162;
        ctx.fillStyle = "#94a3b8";
        ctx.font = '600 20px "Segoe UI", sans-serif';
        ctx.fillText(c, cx, 92, 150);
        for (let k = 0; k < 3 - (i === 2 ? 1 : 0); k++) {
          ctx.fillStyle = "rgba(255,255,255,0.1)";
          ctx.fillRect(cx, 108 + k * 64, 148, 54);
          ctx.fillStyle = s.color;
          ctx.fillRect(cx, 108 + k * 64, 6, 54);
          ctx.fillStyle = "#e2e8f0";
          ctx.font = '600 18px "Segoe UI", sans-serif';
          ctx.fillText(cards[(i * 3 + k) % cards.length], cx + 14, 140 + k * 64, 128);
        }
      });
    });
    kit.block(root, x - 1.5, y, 7.3, 0.1, 1.2, 0.1, steel);
    kit.block(root, x + 1.5, y, 7.3, 0.1, 1.2, 0.1, steel);
    kit.block(root, x, y + 1.2, 7.3, 3.4, 2.1, 0.1, dark);
    const plane = MeshBuilder.CreatePlane(`v2_teamBoardPlane_${side}`, { width: 3.2, height: 2.0 }, scene);
    plane.position.set(x, y + 2.25, 7.24);
    kit.finish(plane, root, kit.screen(tex));
    kit.block(root, x, y + 3.32, 7.3, 3.4, 0.06, 0.12, kit.mat(s.color, { unlit: true }));
  };
  teamBoard("claims", -11.0, ["Claim pack", "IP claim", "Annual review", "Bank change", "Death claim", "Cover change"]);
  teamBoard("protection", 11.0, ["Fact find", "Needs analysis", "Quotes", "SoA draft", "Underwriting", "Policy issued"]);

  /* ---------------- Desks and the two teams ---------------- */
  const deskTop = kit.mat("#f8f4ec");
  const laptop = kit.mat("#c7ccd6", { specular: 0.4 });
  const paper = kit.mat("#ffffff");
  const characters = new Map<string, VoxelCharacter>();
  const rings = new Map<string, Mesh>();
  const anchors = new Map<string, Anchor>();
  for (const p of STAFF_PERSONAS) {
    const st = STATIONS[p.id];
    if (!st) continue;
    const dz = st.z - 0.4;
    const accent = kit.mat(p.color);
    kit.block(root, st.x, y + 1.5, dz, 3.6, 0.12, 1.3, deskTop);
    kit.block(root, st.x - 1.65, y, dz, 0.14, 1.5, 1.2, white);
    kit.block(root, st.x + 1.65, y, dz, 0.14, 1.5, 1.2, white);
    kit.block(root, st.x, y + 0.3, dz - 0.62, 3.3, 1.1, 0.06, accent);
    // A neon edge along the desk's front, in the person's colour.
    kit.block(root, st.x, y + 1.44, dz - 0.66, 3.6, 0.06, 0.04, kit.mat(p.color, { unlit: true }));
    const nameTag = kit.label(root, {
      lines: [
        { text: p.name.toUpperCase(), size: 62, weight: 800 },
        { text: p.department, size: 30, weight: 600, color: "#e6ecf5" },
      ],
      width: 3.0,
      px: 512,
      bg: "rgba(10,18,34,0.94)",
      accent: p.color,
      bar: "top",
    });
    nameTag.position.set(st.x, y + 0.86, dz - 0.67);
    // Laptop: the lid faces the person, so the camera sees its back.
    const top = y + 1.62;
    kit.block(root, st.x - 0.3, top, dz + 0.1, 0.95, 0.05, 0.62, laptop);
    const lid = kit.block(root, st.x - 0.3, top, dz - 0.22, 0.95, 0.64, 0.05, laptop);
    lid.rotation.x = -0.22;
    // A desk prop that says something about the role.
    const px = st.x + 1.05;
    switch (p.id) {
      case "onboarding-1":
        // The inbox tray.
        kit.block(root, px, top, dz, 0.9, 0.12, 0.7, dark);
        for (let i = 0; i < 4; i++) kit.block(root, px, top + 0.12 + i * 0.05, dz, 0.75, 0.04, 0.55, paper);
        break;
      case "paraplanning-1":
        // A calculator beside a draft Statement of Advice.
        kit.block(root, px - 0.15, top, dz + 0.05, 0.55, 0.06, 0.72, paper);
        kit.block(root, px + 0.25, top, dz - 0.1, 0.36, 0.1, 0.5, kit.mat("#334155"));
        kit.block(root, px + 0.25, top + 0.1, dz - 0.25, 0.28, 0.02, 0.12, kit.mat("#86efac", { unlit: true }));
        break;
      case "research-1":
        // Product disclosure statements, stacked, and a little bar chart.
        ["#14b8a6", "#0ea5e9", "#f59e0b"].forEach((hex, i) => kit.block(root, px - 0.2, top + i * 0.16, dz, 0.6, 0.14, 0.75, kit.mat(hex)));
        [0.3, 0.5, 0.7].forEach((h, i) => kit.block(root, px + 0.32 + i * 0.14, top, dz - 0.1, 0.1, h, 0.2, kit.mat("#fcd34d")));
        break;
      case "fincrime-1":
        kit.block(root, px, top, dz, 0.7, 0.04, 0.9, paper);
        kit.cyl(root, px + 0.1, top + 0.06, dz - 0.1, 0.5, 0.06, kit.mat("#bfe6ff", { specular: 0.8 }), { tess: 16 });
        kit.block(root, px + 0.4, top + 0.06, dz + 0.22, 0.4, 0.08, 0.1, dark, { rotY: 0.7 });
        break;
      case "underwriting-1":
        // An application on a clipboard, and a heartbeat on a small screen.
        kit.block(root, px - 0.2, top, dz, 0.5, 0.05, 0.7, kit.mat("#8b5e3c"));
        kit.block(root, px - 0.2, top + 0.05, dz, 0.42, 0.02, 0.6, paper);
        kit.block(root, px + 0.35, top, dz + 0.1, 0.5, 0.45, 0.08, dark);
        kit.block(root, px + 0.35, top + 0.08, dz + 0.05, 0.42, 0.3, 0.02, kit.mat("#10b981", { unlit: true }));
        break;
      case "claims-1":
        // Claim packs in folders, and a stamp.
        ["#6366f1", "#818cf8", "#4f46e5"].forEach((hex, i) => kit.block(root, px - 0.15, top + i * 0.12, dz, 0.7, 0.1, 0.85, kit.mat(hex)));
        kit.block(root, px + 0.42, top, dz - 0.15, 0.22, 0.12, 0.22, kit.mat("#334155"));
        kit.block(root, px + 0.42, top + 0.12, dz - 0.15, 0.08, 0.2, 0.08, kit.mat("#dc2626"));
        break;
      case "comms-1":
        kit.block(root, px, top, dz, 0.12, 0.45, 0.12, steel);
        kit.block(root, px, top + 0.45, dz, 0.7, 0.12, 0.16, dark);
        kit.block(root, px - 0.32, top + 0.25, dz, 0.16, 0.28, 0.28, kit.mat("#ec4899"));
        kit.block(root, px + 0.32, top + 0.25, dz, 0.16, 0.28, 0.28, kit.mat("#ec4899"));
        break;
      case "lead-1":
        kit.block(root, st.x + 1.1, top, dz + 0.15, 0.14, 0.9, 0.14, steel);
        kit.block(root, st.x + 1.1, top + 0.9, dz + 0.1, 1.3, 0.8, 0.08, dark);
        kit.block(root, st.x + 1.1, top + 0.98, dz + 0.05, 1.12, 0.62, 0.02, kit.mat(p.color, { unlit: true }));
        break;
    }
    const ch = new VoxelCharacter(scene, p.id, PALETTES[p.palette]);
    ch.root.parent = root;
    ch.root.scaling.setAll(CHARACTER_SCALE);
    ch.root.position.set(st.x, y, st.z + 0.75);
    ch.root.rotation.y = Math.PI;
    characters.set(p.id, ch);
    anchors.set(`staff:${p.id}`, { node: ch.root, group: "people", dy: 2.5 });
    // The selection ring walks with the person.
    const ring = MeshBuilder.CreateTorus(`v2_staffRing_${p.id}`, { diameter: 1.9, thickness: 0.11, tessellation: 40 }, scene);
    ring.position.y = 0.08;
    kit.finish(ring, ch.root, kit.mat("#ffe08a", { unlit: true }), true);
    ring.setEnabled(false);
    rings.set(p.id, ring);
  }

  /* ---------------- The huddle table, where both teams meet ---------------- */
  const hx = HUDDLE.x;
  const hz = HUDDLE.z;
  kit.cyl(root, hx, y, hz, 0.5, 1.4, steel, { tess: 10 });
  kit.cyl(root, hx, y + 1.4, hz, 2.4, 0.12, white, { tess: 28 });
  const huddleRing = MeshBuilder.CreateTorus("v2_huddleRing", { diameter: 2.1, thickness: 0.06, tessellation: 36 }, scene);
  huddleRing.position.set(hx, y + 1.53, hz);
  kit.finish(huddleRing, root, kit.mat(SIDES.shared.color, { unlit: true }));
  kit.block(root, hx + 0.5, y + 1.52, hz - 0.3, 0.7, 0.04, 0.5, dark, { rotY: 0.3 });
  for (const [sx, sz] of [
    [-1.75, 0],
    [1.75, 0],
  ] as const) {
    kit.cyl(root, hx + sx, y, hz + sz, 0.12, 0.95, steel, { tess: 8 });
    kit.cyl(root, hx + sx, y + 0.95, hz + sz, 0.75, 0.12, kit.mat(sx < 0 ? SIDES.claims.color : SIDES.protection.color), { tess: 12 });
  }
  const huddleCase = kit.block(root, hx, y + 2.0, hz, 0.5, 0.5, 0.5, kit.mat("#ffc35a", { emissive: 0.55 }), {
    dynamic: true,
  });
  const huddleTag = kit.label(root, {
    lines: [{ text: "both teams meet here", size: 40, weight: 700, color: "#dfe5f7" }],
    width: 2.6,
    px: 420,
    pxHeight: 70,
    bg: "rgba(10,18,34,0.94)",
  });
  huddleTag.billboardMode = Mesh.BILLBOARDMODE_Y;
  huddleTag.position.set(hx, y + 3.0, hz);

  /* ---------------- The quiet room, for sensitive calls ---------------- */
  {
    const q = SPARE_CELLS.left;
    const qz = q.z - 0.4;
    // A curved white pod, open to the front, with a gold neon edge.
    const shell = MeshBuilder.CreateCylinder("v2_quietPod", { diameter: 4.6, height: 3.0, tessellation: 28, arc: 0.5, sideOrientation: Mesh.DOUBLESIDE }, scene);
    // A cylinder's arc runs round the -z side: turn it so the pod opens to the front.
    shell.rotation.y = Math.PI;
    shell.position.set(q.x, y + 1.5, qz);
    kit.finish(shell, root, kit.mat("#f4f6f8"));
    const edge = MeshBuilder.CreateCylinder("v2_quietPodEdge", { diameter: 4.7, height: 0.1, tessellation: 28, arc: 0.5, sideOrientation: Mesh.DOUBLESIDE }, scene);
    edge.rotation.y = Math.PI;
    edge.position.set(q.x, y + 3.0, qz);
    kit.finish(edge, root, kit.mat(SIDES.claims.color, { unlit: true }));
    kit.cyl(root, q.x, y, qz + 0.9, 0.9, 0.7, white, { tess: 14 });
    kit.block(root, q.x + 0.15, y + 0.7, qz + 0.9, 0.3, 0.12, 0.2, kit.mat("#f9a8d4"));
    for (const dx of [-1.25, 1.25]) {
      kit.block(root, q.x + dx, y, qz + 0.7, 1.0, 0.55, 1.0, kit.mat("#b08a5a"));
      kit.block(root, q.x + dx * 1.18, y + 0.55, qz + 0.75, 0.2, 0.8, 1.0, kit.mat("#b08a5a"));
    }
    const sign = kit.label(root, {
      lines: [
        { text: "QUIET ROOM", size: 44, weight: 800, color: "#7a5a12" },
        { text: "for sensitive calls", size: 28, weight: 600, color: "#5b6476" },
      ],
      width: 2.2,
      px: 420,
      bg: "rgba(255,251,242,0.96)",
      accent: SIDES.claims.color,
      bar: "top",
    });
    sign.position.set(q.x, y + 3.45, qz);
    sign.billboardMode = Mesh.BILLBOARDMODE_Y;
  }

  /* ---------------- Coffee bar, printer, lounge corner and plants ---------------- */
  {
    const c = SPARE_CELLS.right;
    kit.block(root, c.x, y, c.z + 0.2, 4.4, 1.2, 1.2, walnut);
    kit.block(root, c.x, y + 1.2, c.z + 0.2, 4.6, 0.1, 1.4, white);
    kit.block(root, c.x + 1.2, y + 1.3, c.z + 0.4, 0.9, 1.0, 0.7, dark);
    kit.block(root, c.x + 1.2, y + 1.8, c.z + 0.04, 0.5, 0.18, 0.04, kit.mat("#ffc35a", { unlit: true }));
    for (let i = 0; i < 4; i++) kit.cyl(root, c.x - 1.6 + i * 0.4, y + 1.3, c.z + 0.1, 0.2, 0.22, kit.mat("#ffffff"), { tess: 8 });
    const coffee = kit.label(root, {
      lines: [{ text: "COFFEE", size: 64, weight: 800, color: "#ff8f6b" }],
      width: 2.0,
      px: 360,
      pxHeight: 90,
      bg: "rgba(10,18,34,0.94)",
    });
    coffee.position.set(c.x, y + 3.4, c.z + 0.8);
    for (const dx of [-1.2, 0.2]) {
      kit.cyl(root, c.x + dx, y, c.z - 1.0, 0.14, 1.0, steel, { tess: 8 });
      kit.cyl(root, c.x + dx, y + 1.0, c.z - 1.0, 0.75, 0.14, kit.mat("#ff8f6b"), { tess: 12 });
    }
  }
  kit.block(root, -18.6, y, -1.6, 1.5, 0.8, 1.5, kit.mat("#ff8f6b"));
  kit.block(root, -18.8, y, 0.4, 1.5, 0.8, 1.5, kit.mat("#14b8a6"));
  // The printer by the laneway wall, with a paper tray.
  kit.block(root, -19.5, y, -6.6, 1.3, 1.1, 1.2, kit.mat("#e5e7eb"));
  kit.block(root, -19.5, y + 1.1, -6.6, 1.1, 0.2, 1.0, kit.mat("#9ca3af"));
  kit.block(root, -18.86, y + 0.8, -6.6, 0.14, 0.05, 0.7, kit.mat("#ffffff"));
  kit.block(root, -19.0, y + 1.32, -6.9, 0.2, 0.06, 0.2, kit.mat("#34d399", { unlit: true }));
  for (const [x, z] of [[-19.6, -12.0], [19.6, -12.0], [19.6, 7.6], [-15.6, 7.6]] as const) {
    kit.block(root, x, y, z, 0.9, 0.8, 0.9, pot);
    kit.block(root, x, y + 0.8, z, 1.2, 1.1, 1.2, leaf);
    kit.block(root, x + 0.15, y + 1.8, z - 0.1, 0.7, 0.7, 0.7, leafDark);
  }

  /* ---------------- The idea drops in (chapter intro) ---------------- */
  const sparkMat = kit.mat("#ffd98a", { unlit: true });
  const sparks: Mesh[] = STAFF_PERSONAS.map(() => {
    const mm = kit.block(root, 0, 0, 0, 0.42, 0.42, 0.42, sparkMat, { dynamic: true });
    mm.setEnabled(false);
    return mm;
  });

  kit.useBatch(null);
  batch.flush();

  /* ---------------- Moving about the floor ---------------- */
  const r = rng(47);
  const pick = <T>(list: readonly T[]): T => list[Math.floor(r() * list.length)];
  const between = (a: number, b: number): number => a + (b - a) * r();
  const book = new SpotBook();
  const homes = new Map<string, Spot>();
  for (const p of STAFF_PERSONAS) {
    const st = STATIONS[p.id];
    if (!st) continue;
    const at: Pt = [st.x, st.z + 0.75];
    // Back-row people leave by the side of their desk nearer the middle.
    const side = st.x > 0 ? -2.6 : 2.6;
    const via: Pt[] = st.z < 0 ? [[st.x, CZ]] : [[st.x + side, CZ], [st.x + side, at[1]]];
    homes.set(p.id, { at, face: Math.PI, via });
  }
  const spot = (at: Pt, face: number, via: Pt[], key: string): Spot => ({ at, face, via, key });
  const toward = (from: Pt, to: Pt): number => Math.atan2(to[0] - from[0], to[1] - from[1]);
  const cafe = SPARE_CELLS.right;
  const quiet = SPARE_CELLS.left;
  const places: Record<Exclude<Errand, `visit:${string}`>, { spots: Spot[]; where: string; tasks: string[] }> = {
    coffee: {
      spots: [spot([cafe.x - 1.6, cafe.z - 1.3], 0, [[cafe.x - 1.6, CZ]], "coffee:0"), spot([cafe.x + 0.9, cafe.z - 1.3], 0, [[cafe.x + 0.9, CZ]], "coffee:1")],
      where: "the coffee bar",
      tasks: ["Grabbing a flat white", "Coffee break"],
    },
    huddle: {
      spots: [
        spot([hx - 2.4, hz - 0.5], toward([hx - 2.4, hz - 0.5], [hx, hz]), [[hx - 2.4, CZ]], "huddle:w"),
        spot([hx + 2.4, hz - 0.5], toward([hx + 2.4, hz - 0.5], [hx, hz]), [[hx + 2.4, CZ]], "huddle:e"),
        spot([hx, hz - 1.5], 0, [[hx, CZ]], "huddle:s"),
      ],
      where: "the huddle table",
      tasks: ["Case huddle", "Talking through a case"],
    },
    printer: {
      spots: [spot([-17.6, -6.6], -Math.PI / 2, [[-17.6, CZ]], "printer")],
      where: "the printer",
      tasks: ["Collecting printouts", "Printing a welcome pack"],
    },
    process: {
      spots: [spot([-2.4, 7.1], 0, [[-3.75, CZ], [-3.75, 7.1]], "process")],
      where: "the process board",
      tasks: ["Checking the process board"],
    },
    claimsBoard: {
      spots: [spot([-11.0, 6.2], 0, [[-11.25, CZ], [-11.25, 6.2]], "claimsBoard")],
      where: "the team board",
      tasks: ["Checking the team board", "Moving a card on the board"],
    },
    protectionBoard: {
      spots: [spot([11.0, 6.2], 0, [[11.25, CZ], [11.25, 6.2]], "protectionBoard")],
      where: "the team board",
      tasks: ["Checking the team board", "Moving a card on the board"],
    },
    quiet: {
      spots: [spot([quiet.x - 0.7, quiet.z - 0.1], Math.PI / 2, [[quiet.x - 0.7, CZ]], "quiet:0")],
      where: "the quiet room",
      tasks: ["A quiet call with a family", "Calling a client about a claim"],
    },
    lookout: {
      spots: [spot([-11.25, -12.1], Math.PI, [[-11.25, CZ]], "look:w"), spot([3.75, -12.1], Math.PI, [[3.75, CZ]], "look:e")],
      where: "the front rail",
      tasks: ["Watching the trams go by", "A quick stretch"],
    },
  };
  /** Standing beside a colleague's desk, on the side nearer the middle. */
  const visitSpot = (id: string): Spot | null => {
    const home = homes.get(id);
    if (!home) return null;
    const [x, z] = home.at;
    const sx = x > 0 ? x - 2.4 : x < 0 ? x + 2.4 : -2.4;
    const at: Pt = [sx, z + 0.35];
    return spot(at, toward(at, home.at), [[sx, CZ]], `visit:${id}`);
  };
  const wanderers = new Map<string, Wanderer>();
  for (const p of STAFF_PERSONAS) {
    const ch = characters.get(p.id);
    const home = homes.get(p.id);
    if (!ch || !home) continue;
    const errands = ERRANDS[p.id] ?? [];
    const total = errands.reduce((sum, [, w]) => sum + w, 0);
    const plan = (): Outing | null => {
      let roll = r() * total;
      const errand = errands.find(([, w]) => (roll -= w) <= 0)?.[0];
      if (!errand) return null;
      if (errand.startsWith("visit:")) {
        const id = errand.slice(6);
        const s = visitSpot(id);
        const colleague = findStaffById(id);
        // Only drop by when they're at their desk.
        if (!s || !colleague || !wanderers.get(id)?.isHome) return null;
        return { spot: s, task: pick([`Checking in with ${colleague.name}`, `A quick question for ${colleague.name}`]), where: `${colleague.name}’s desk`, dwell: between(4, 8) };
      }
      const place = places[errand as keyof typeof places];
      const free = place.spots.filter((s) => book.free(s.key));
      if (!free.length) return null;
      return { spot: pick(free), task: pick(place.tasks), where: place.where, dwell: between(5, 9) };
    };
    wanderers.set(
      p.id,
      new Wanderer({
        body: { root: ch.root, walk: (on) => ch.setWalking(on) },
        home,
        homeName: "the desk",
        plan,
        homeTask: () => pick(p.ambient),
        homeDwell: [9, 22],
        speed: 2.2,
        book,
        rand: r,
        bubble: kit.bubble(ch.root, 2.45, p.color, CHARACTER_SCALE),
        sway: 0.14,
      }),
    );
  }

  let selected: ReadonlySet<string> = new Set();
  const flash = new Map<string, number>();
  let introT = -1;
  const ids = STAFF_PERSONAS.map((p) => p.id);
  // The idea drops in from the brain on the floor above.
  const fromX = DOME.x;
  const fromZ = DOME.z + 2.6;

  const applyHighlight = (id: string): void => {
    characters.get(id)?.setHighlight(selected.has(id) || (flash.get(id) ?? 0) > 0, CORAL);
    rings.get(id)?.setEnabled(selected.has(id));
  };

  return {
    id: "people",
    root,
    tag,
    anchors,
    update(dt: number, t: number): void {
      huddleCase.rotation.y = t * 0.9;
      huddleCase.position.y = y + 2.25 + Math.sin(t * 1.7) * 0.1;
      for (const id of selected) rings.get(id)?.scaling.setAll(1 + Math.sin(t * 5) * 0.06);
      ids.forEach((id) => {
        const ch = characters.get(id);
        if (!ch) return;
        wanderers.get(id)?.update(dt);
        ch.update(dt);
        const f = flash.get(id);
        if (f !== undefined) {
          const left = f - dt;
          if (left <= 0) flash.delete(id);
          else flash.set(id, left);
          applyHighlight(id);
        }
      });
      if (introT >= 0) {
        introT += dt;
        let running = false;
        ids.forEach((id, i) => {
          const spark = sparks[i];
          const ch = characters.get(id);
          const local = (introT - i * 0.14) / 1.3;
          if (!ch || local < 0 || local > 1) {
            if (local > 1 && spark.isEnabled()) {
              spark.setEnabled(false);
              flash.set(id, 1.1);
              applyHighlight(id);
            }
            if (local <= 1) running = true;
            return;
          }
          running = true;
          spark.setEnabled(true);
          const e = local * local * (3 - 2 * local);
          const tx = ch.root.position.x;
          const tz = ch.root.position.z;
          spark.position.set(fromX + (tx - fromX) * e, y + 5.2 - 2.4 * e + Math.sin(Math.PI * e) * 1.2, fromZ + (tz - fromZ) * e);
          spark.rotation.y = t * 4;
        });
        if (!running) introT = -1;
      }
    },
    setHighlights(h: Highlights): void {
      selected = h.staff;
      ids.forEach(applyHighlight);
      // Someone selected, or working on the finale's request, heads back to their desk.
      for (const [id, w] of wanderers) w.hold(selected.has(id));
    },
    intro(): void {
      introT = 0;
    },
    task(id: string): string | null {
      return wanderers.get(id)?.task ?? null;
    },
  };
}
