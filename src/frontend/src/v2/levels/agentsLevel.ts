/**
 * Level 1 · The Agents — one group of Microsoft Foundry agents that supports
 * both teams, in the old banking chamber at street level: tall ceilings, a
 * colonnade along the cut, arched windows, and the old teller counter at the
 * front, now the Client Case hub.
 *
 * The floor is split into three tinted zones: agents dedicated to Claims &
 * Care, agents shared by both sides, and agents dedicated to Protection
 * Advice. Each person's agent has a bay directly under their desk on Level 2
 * and works there with its two specialised assistants (named in
 * agentStages.ts); Cara's bay also has her voice agent. Every role is its own
 * small voxel robot, carrying something that says what it does, and the
 * bay's frame glows in the colour of its side.
 *
 * The roles move about the floor: to the Client Case at the teller counter,
 * to the Foundry uplink (their line down to Microsoft Foundry and Microsoft
 * IQ), to the MCP tool bench, and to the next agent's bay to hand work on.
 * Olivia's Fact Find Extraction agent also walks out to the front desk under
 * the verandah whenever a visitor signs.
 */
import { Color3, Mesh, MeshBuilder, StandardMaterial, TransformNode, Vector3 } from "@babylonjs/core";
import { STAFF_PERSONAS, findStaffById } from "../../personaData";
import { heritageMats } from "../heritage";
import {
  AGENTS,
  DESK_ROLE,
  HANDOFFS,
  SIDES,
  levelInfo,
  rolesOf,
  sideBadge,
  type AgentRole,
  type RoleProp,
  type SideId,
} from "../houseData";
import { Batch, type Kit, mixHex, rng } from "../kit";
import { FLOOR_Y, FRONT_DESK, HOUSE, HUB, INSIDE, LIFT, SPARE_CELLS, STATIONS, STOREY, ZONES } from "../layout";
import { SIDE_BAYS, buildShell } from "../shell";
import type { Anchor, Highlights, LevelBuild } from "../types";
import { SpotBook, Wanderer, type Outing, type Pt, type Spot } from "../wander";

const BUS_Z = -1.25;
/** The line the robots walk along between the two rows of bays. */
const CZ = -1.0;
const POD_W = 5.6;
const POD_D = 4.6;
/** The robots' line down to Microsoft Foundry and Microsoft IQ, where the hub's trunk meets the bus. */
const UPLINK = { x: 0, z: BUS_Z };
/** The MCP tool bench in the back-right cell: Insurance Research, Underwriting, Claims and Teams. */
const BENCH_Z = 7.0;
const BENCH_XS = [13.2, 14.6, 16.0, 17.4];
const BENCH_COLORS = ["#f59e0b", "#10b981", "#6366f1", "#818cf8"];
/** The colonnade along the cut, one column in front of each outer bay. */
const COLUMNS_X = [-15, -7.5, 7.5, 15];
const COLUMN_Z = HOUSE.z0 + 0.55;

/** Lettering for each zone, in a lighter tint of the side colour so it reads on the dark signs. */
const ZONE_TEXT: Record<SideId, { text: string; color: string }> = {
  claims: { text: "DEDICATED · CLAIMS & CARE", color: "#e9c877" },
  shared: { text: "SHARED BY BOTH SIDES", color: "#b9c3ff" },
  protection: { text: "DEDICATED · PROTECTION ADVICE", color: "#5eead4" },
};

/**
 * The floor: a light, cool grey (darker in hex than it looks, because the sun and sky lift every
 * top face by about a third), and each zone a soft pastel of its side's colour on it.
 */
const L1_FLOOR = "#a1afc8";
const zoneTint = (side: SideId): string => mixHex(L1_FLOOR, SIDES[side].color, 0.25);

/** Where each role stands in its bay: the agent behind, its assistants in front. */
const HOME_OFFSETS: Record<number, Array<[number, number]>> = {
  3: [
    [0, 0.9],
    [-1.6, -0.7],
    [1.6, -0.7],
  ],
  4: [
    [-1.0, 0.9],
    [-1.9, -0.9],
    [0.2, -0.9],
    [1.5, 0.9],
  ],
};

/** What each agent does at the MCP tool bench (the tool families in guru-ops-mcp). */
const TOOL_WORK: Record<string, { bench: number; tasks: string[] }> = {
  "research-1": { bench: 0, tasks: ["Reading a product sheet", "Comparing the APL insurers", "Building the comparison workbook"] },
  "underwriting-1": { bench: 1, tasks: ["Looking up an insurer", "Checking the evidence needed", "Generating the application summary"] },
  "claims-1": { bench: 2, tasks: ["Checking a policy", "Calculating a benefit", "Sending a Teams approval card"] },
};

interface Robot {
  role: AgentRole;
  node: TransformNode;
  tip: Mesh;
  ring: Mesh;
  scale: number;
  phase: number;
  walking: boolean;
  wanderer: Wanderer;
}

interface Pod {
  id: string;
  node: TransformNode;
  ring: Mesh;
  pad: StandardMaterial;
  /** The pad's pastel at rest, stronger while lit, and the bare zone it powers up from. */
  padBase: Color3;
  padLit: Color3;
  zoneBase: Color3;
  power: number;
}

interface Packet {
  mesh: Mesh;
  path: Vector3[];
  length: number;
  speed: number;
  offset: number;
}

export interface AgentsLevelBuild extends LevelBuild {
  /** Send Olivia's Fact Find Extraction agent out to the front desk. */
  callToDesk(): void;
  releaseDesk(): void;
  /** The Fact Find Extraction agent is standing at the front desk, ready to read. */
  deskReady(): boolean;
  /** What a role is doing right now, for the inspector. */
  roleTask(id: string): string | null;
  /** Light up the bays Amelia has linked the visitor at the front desk with, by staff id. */
  setLinked(ids: ReadonlySet<string>): void;
}

/** What a role's robot carries, built in robot space (it faces -z). */
function addProp(kit: Kit, node: TransformNode, prop: RoleProp): void {
  const z = -0.42;
  const b = (x: number, y: number, zz: number, w: number, h: number, d: number, hex: string, glow = false): Mesh =>
    kit.block(node, x, y, zz, w, h, d, kit.mat(hex, glow ? { unlit: true } : {}));
  const disc = (y: number, d: number, hex: string): Mesh => {
    const m = kit.cyl(node, 0, y, z, d, 0.06, kit.mat(hex), { tess: 20 });
    m.rotation.x = Math.PI / 2;
    return m;
  };
  switch (prop) {
    case "doc":
      b(0, 0.4, z, 0.46, 0.6, 0.04, "#ffffff");
      for (const [ly, w] of [
        [0.86, 0.32],
        [0.74, 0.32],
        [0.62, 0.2],
      ] as const)
        b(0, ly, z - 0.03, w, 0.04, 0.02, "#94a3b8");
      break;
    case "clipboard":
      b(0, 0.38, z, 0.5, 0.66, 0.05, "#8b5e3c");
      b(0, 0.42, z - 0.03, 0.4, 0.5, 0.02, "#ffffff");
      b(0, 1.02, z - 0.01, 0.22, 0.06, 0.04, "#cbd5e1");
      b(-0.08, 0.72, z - 0.05, 0.1, 0.05, 0.02, "#16a34a");
      b(-0.08, 0.56, z - 0.05, 0.1, 0.05, 0.02, "#16a34a");
      break;
    case "calculator":
      b(0, 0.34, z, 0.42, 0.58, 0.08, "#334155");
      b(0, 0.76, z - 0.05, 0.32, 0.1, 0.02, "#86efac", true);
      for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) b(-0.12 + i * 0.12, 0.42 + j * 0.1, z - 0.05, 0.08, 0.06, 0.02, "#e2e8f0");
      break;
    case "quill":
      // A signed document and a quill: the Statement of Advice.
      b(0, 0.36, z, 0.48, 0.62, 0.04, "#fffaf0");
      b(-0.06, 0.82, z - 0.03, 0.3, 0.04, 0.02, "#94a3b8");
      b(-0.06, 0.7, z - 0.03, 0.3, 0.04, 0.02, "#94a3b8");
      b(-0.04, 0.5, z - 0.03, 0.26, 0.03, 0.02, "#1d4ed8");
      b(0.24, 0.5, z - 0.08, 0.06, 0.62, 0.04, "#f8fafc").rotation.z = -0.5;
      break;
    case "book":
      b(0, 0.38, z, 0.52, 0.64, 0.2, "#0d9488");
      b(0, 0.84, z - 0.11, 0.3, 0.06, 0.02, "#fcd34d");
      break;
    case "chart":
      b(0, 0.32, z, 0.5, 0.05, 0.2, "#334155");
      [0.22, 0.36, 0.5].forEach((h, i) => b(-0.15 + i * 0.15, 0.37, z, 0.11, h, 0.11, i === 2 ? "#f59e0b" : "#fcd34d"));
      break;
    case "magnifier": {
      const rim = MeshBuilder.CreateTorus("v2_propLens", { diameter: 0.44, thickness: 0.07, tessellation: 18 }, kit.scene);
      rim.rotation.x = Math.PI / 2;
      rim.position.set(0.05, 0.74, z);
      kit.finish(rim, node, kit.mat("#cbd5e1", { specular: 0.5 }));
      disc(0.71, 0.36, "#bfe6ff").position.x = 0.05;
      b(-0.14, 0.32, z, 0.08, 0.32, 0.08, "#7c3a12").rotation.z = 0.6;
      break;
    }
    case "timeline":
      b(0, 0.66, z, 0.64, 0.06, 0.04, "#e2e8f0");
      ["#22c55e", "#f59e0b", "#ef4444"].forEach((hex, i) => b(-0.22 + i * 0.22, 0.62, z - 0.03, 0.12, 0.14, 0.04, hex));
      break;
    case "form":
      // An application and a pen.
      b(0, 0.34, z, 0.46, 0.64, 0.04, "#ffffff");
      for (let i = 0; i < 4; i++) {
        b(-0.12, 0.84 - i * 0.13, z - 0.03, 0.08, 0.06, 0.02, "#10b981");
        b(0.08, 0.84 - i * 0.13, z - 0.03, 0.22, 0.04, 0.02, "#94a3b8");
      }
      b(0.26, 0.4, z - 0.06, 0.05, 0.44, 0.05, "#1e293b").rotation.z = 0.4;
      break;
    case "stethoscope": {
      // A heartbeat on a little monitor: the medical evidence.
      b(0, 0.34, z, 0.56, 0.42, 0.08, "#1f2937");
      b(-0.18, 0.5, z - 0.05, 0.1, 0.04, 0.02, "#34d399", true);
      b(-0.06, 0.48, z - 0.05, 0.05, 0.18, 0.02, "#34d399", true);
      b(0.04, 0.5, z - 0.05, 0.1, 0.04, 0.02, "#34d399", true);
      b(0.16, 0.54, z - 0.05, 0.05, 0.1, 0.02, "#34d399", true);
      const tube = MeshBuilder.CreateTorus("v2_propTube", { diameter: 0.4, thickness: 0.05, tessellation: 14 }, kit.scene);
      tube.position.set(0, 0.88, z);
      kit.finish(tube, node, kit.mat("#94a3b8"));
      break;
    }
    case "folder":
      // A claim pack: a folder with tabs.
      b(0, 0.34, z, 0.56, 0.62, 0.08, "#6366f1");
      b(-0.16, 0.96, z, 0.18, 0.08, 0.06, "#facc15");
      b(0.06, 0.96, z, 0.18, 0.08, 0.06, "#34d399");
      b(0, 0.5, z - 0.05, 0.3, 0.14, 0.02, "#e0e7ff");
      break;
    case "stamp":
      b(0, 0.4, z, 0.4, 0.12, 0.34, "#334155");
      b(0, 0.52, z, 0.14, 0.32, 0.14, "#dc2626");
      b(0, 0.84, z, 0.26, 0.12, 0.26, "#dc2626");
      break;
    case "envelope":
      b(0, 0.42, z, 0.52, 0.36, 0.04, "#ffffff");
      b(-0.12, 0.64, z - 0.03, 0.3, 0.04, 0.02, "#ec4899").rotation.z = -0.55;
      b(0.12, 0.64, z - 0.03, 0.3, 0.04, 0.02, "#ec4899").rotation.z = 0.55;
      break;
    case "heart":
      b(0, 0.46, z, 0.5, 0.36, 0.06, "#f9a8d4");
      b(-0.14, 0.38, z, 0.1, 0.1, 0.06, "#f9a8d4");
      for (const x of [-0.12, 0, 0.12]) b(x, 0.6, z - 0.04, 0.06, 0.06, 0.02, "#be185d");
      break;
    case "clock":
      disc(0.66, 0.5, "#f8fafc");
      b(0, 0.69, z - 0.05, 0.04, 0.18, 0.02, "#111827");
      b(0.06, 0.67, z - 0.05, 0.14, 0.04, 0.02, "#111827");
      break;
    case "badge":
      disc(0.66, 0.48, "#16a34a");
      b(-0.07, 0.63, z - 0.05, 0.12, 0.05, 0.02, "#ffffff").rotation.z = -0.8;
      b(0.05, 0.67, z - 0.05, 0.2, 0.05, 0.02, "#ffffff").rotation.z = 0.9;
      break;
    case "headset":
      for (const x of [-0.37, 0.37]) b(x, 1.32, 0, 0.12, 0.26, 0.28, "#ec4899");
      b(0, 1.77, 0, 0.84, 0.07, 0.12, "#1f2937");
      b(0.27, 1.2, -0.22, 0.05, 0.05, 0.32, "#1f2937");
      b(0.24, 1.16, -0.4, 0.1, 0.1, 0.1, "#ec4899", true);
      break;
    case "beacon":
      break;
  }
}

/** One role as a small voxel robot, merged into a few meshes. Leads are bigger, with a sash in their side's colour. */
function buildRobot(kit: Kit, role: AgentRole, accent: string, sideColor: string): Omit<Robot, "wanderer" | "phase" | "walking" | "role"> {
  const node = new TransformNode(`v2_robot_${role.id}`, kit.scene);
  const rb = new Batch(node, `robot_${role.id}`);
  const prev = kit.useBatch(rb);
  const lead = role.kind === "lead";
  const metal = kit.mat("#dfeaf7", { emissive: 0.25 });
  const dark = kit.mat("#16213a");
  const body = kit.mat(accent, { emissive: 0.35 });
  const glow = kit.mat("#6ec1ff", { unlit: true });
  kit.block(node, -0.18, 0, 0, 0.28, 0.55, 0.3, dark);
  kit.block(node, 0.18, 0, 0, 0.28, 0.55, 0.3, dark);
  kit.block(node, 0, 0.55, 0, 0.8, 0.75, 0.48, body);
  kit.block(node, 0, 0.8, -0.26, 0.32, 0.28, 0.06, glow);
  kit.block(node, -0.52, 0.6, 0, 0.2, 0.62, 0.22, metal);
  kit.block(node, 0.52, 0.6, 0, 0.2, 0.62, 0.22, metal);
  kit.block(node, 0, 1.27, 0, 0.62, 0.5, 0.52, metal);
  kit.block(node, 0, 1.42, -0.27, 0.5, 0.2, 0.06, dark);
  kit.block(node, -0.12, 1.46, -0.31, 0.1, 0.08, 0.04, glow);
  kit.block(node, 0.12, 1.46, -0.31, 0.1, 0.08, 0.04, glow);
  kit.block(node, 0, 1.77, 0, 0.06, lead ? 0.45 : 0.3, 0.06, metal);
  if (lead) kit.block(node, 0, 1.02, 0, 0.84, 0.12, 0.52, kit.mat(sideColor, { unlit: true }));
  addProp(kit, node, role.prop);
  kit.useBatch(prev);
  rb.flush();
  const tipSize = lead ? 0.24 : 0.16;
  const tip = kit.block(node, 0, lead ? 2.2 : 2.03, 0, tipSize, tipSize, tipSize, lead ? kit.mat(sideColor, { unlit: true }) : glow, {
    dynamic: true,
  });
  const ring = MeshBuilder.CreateTorus(`v2_roleRing_${role.id}`, { diameter: 1.7, thickness: 0.1, tessellation: 32 }, kit.scene);
  ring.position.y = 0.05;
  kit.finish(ring, node, kit.mat("#ffe08a", { unlit: true }), true);
  ring.setEnabled(false);
  return { node, tip, ring, scale: lead ? 1.4 : 1.12 };
}

/** Drops repeated points so no segment of a packet path has zero length. */
function dedupe(points: Vector3[]): Vector3[] {
  return points.filter((p, i) => i === 0 || Vector3.Distance(p, points[i - 1]) > 0.01);
}

export function buildAgentsLevel(kit: Kit): AgentsLevelBuild {
  const scene = kit.scene;
  const root = new TransformNode("v2_lvl_agents", scene);
  const batch = new Batch(root, "agents");
  kit.useBatch(batch);
  const y = FLOOR_Y.agents;
  const h = STOREY.agents;
  const info = levelInfo("agents");
  const r = rng(29);
  const pick = <T>(list: readonly T[]): T => list[Math.floor(r() * list.length)];
  const between = (a: number, b: number): number => a + (b - a) * r();
  const hm = heritageMats(kit);

  // A light, cool grey tiled floor: the new floor laid in the old banking chamber.
  const floorMat = kit.mat(L1_FLOOR, { texture: "tiles" });
  const { tag } = buildShell(kit, {
    root,
    level: info,
    floorY: y,
    storey: "ground",
    floor: floorMat,
    inner: hm.render,
    windows: { left: [-10.6, -6.2], right: SIDE_BAYS },
    cove: true,
  });

  /* ---------------- The banking chamber: columns and pilasters ---------------- */
  const neon = kit.mat("#5ce1ff", { unlit: true });
  const column = (x: number, z: number): void => {
    kit.block(root, x, y, z, 1.0, 0.5, 1.0, hm.stone);
    kit.cyl(root, x, y + 0.5, z, 0.66, h - 1.2, hm.render, { tess: 12 });
    kit.cyl(root, x, y + 2.2, z, 0.72, 0.1, neon, { tess: 12 });
    kit.block(root, x, y + h - 0.7, z, 0.95, 0.3, 0.95, hm.stone);
    kit.block(root, x, y + h - 0.4, z, 1.15, 0.4, 1.15, hm.render);
  };
  for (const x of COLUMNS_X) column(x, COLUMN_Z);
  // Engaged pilasters on the back wall, at the edges of the bays.
  for (const x of [-11.25, -3.75, 3.75, 11.25]) {
    kit.block(root, x, y, INSIDE.z1 - 0.15, 0.8, h, 0.3, hm.render);
    kit.block(root, x, y + h - 0.6, INSIDE.z1 - 0.25, 1.1, 0.5, 0.5, hm.stone);
    kit.block(root, x, y + 2.2, INSIDE.z1 - 0.31, 0.82, 0.08, 0.04, neon);
  }

  /* ---------------- The zones ---------------- */
  // Each zone is a soft pastel of its side's colour on the grey, with a strip of the full colour
  // along the front and subtle dashed lines where the zones meet.
  const z0 = HOUSE.z0 + 0.1;
  const z1 = INSIDE.z1 - 0.3;
  for (const side of ["claims", "shared", "protection"] as const) {
    const zone = ZONES[side];
    const cx = (zone.x0 + zone.x1) / 2;
    const w = zone.x1 - zone.x0 - 0.3;
    kit.block(root, cx, y + 0.004, (z0 + z1) / 2, w, 0.012, z1 - z0, kit.mat(zoneTint(side), { texture: "tiles" }));
    kit.block(root, cx, y + 0.01, z0 + 0.12, w, 0.04, 0.16, kit.mat(SIDES[side].color, { unlit: true }));
    // A sign for each zone along the top of the colonnade, like the old bank's counter signs, so
    // it reads from the street and is never hidden behind the agents' cards. The Claims & Care
    // sign starts just clear of the glass lift on the corner, so the lift never hides it.
    const width = side === "claims" ? 7.6 : 8.6;
    const banner = kit.label(root, {
      lines: [{ text: ZONE_TEXT[side].text, size: 60, weight: 800, color: ZONE_TEXT[side].color }],
      width,
      px: 1100,
      // Same height as the other zone signs; the text is just a little smaller.
      pxHeight: Math.round((110 * 8.6) / width),
      bg: "rgba(10,20,40,0.95)",
      accent: SIDES[side].color,
      bar: "left",
    });
    const bx = side === "claims" ? LIFT.x + LIFT.w / 2 + 0.25 + width / 2 : cx;
    banner.position.set(bx, y + h - 0.75, HOUSE.z0 - 0.08);
  }
  // Dashed lines where the zones meet.
  const dash = kit.mat("#8a95aa");
  for (const x of [ZONES.claims.x1, ZONES.protection.x0]) {
    for (let z = z0 + 0.6; z < z1 - 0.4; z += 1.5) kit.block(root, x, y + 0.01, z + 0.4, 0.12, 0.04, 0.8, dash);
  }

  // The floor's sign, on the laneway wall.
  const back = kit.label(root, {
    lines: [
      { text: "ONE GROUP OF AGENTS", size: 60, weight: 800, color: "#5ce1ff" },
      { text: "Each agent with its specialised roles", size: 30, weight: 500, color: "#c6d2e6" },
    ],
    width: 6.0,
    px: 768,
    bg: "rgba(10,20,40,0.94)",
  });
  back.rotation.y = -Math.PI / 2;
  back.position.set(INSIDE.x0 + 0.08, y + 3.9, 0.9);

  const traceMat = kit.mat("#2e7fbf", { emissive: 0.6 });
  const darkSteel = kit.mat("#1e2433", { specular: 0.3 });
  const pods: Pod[] = [];
  const robots: Robot[] = [];
  const anchors = new Map<string, Anchor>();
  const book = new SpotBook();

  /* ---------------- Places the roles visit ---------------- */
  const hubSpots: Spot[] = [
    { at: [HUB.x - 3.2, HUB.z + 0.2], face: Math.PI / 2, via: [[-3.75, CZ], [-3.75, HUB.z + 0.2]], key: "hub:w" },
    { at: [HUB.x + 3.2, HUB.z + 0.2], face: -Math.PI / 2, via: [[3.75, CZ], [3.75, HUB.z + 0.2]], key: "hub:e" },
  ];
  const uplinkSpots: Spot[] = [
    { at: [UPLINK.x - 1.5, UPLINK.z - 0.3], face: Math.PI / 2, via: [[UPLINK.x - 1.5, CZ]], key: "uplink:w" },
    { at: [UPLINK.x + 1.5, UPLINK.z - 0.3], face: -Math.PI / 2, via: [[UPLINK.x + 1.5, CZ]], key: "uplink:e" },
  ];
  const benchSpots: Spot[] = BENCH_XS.map((x, i) => ({
    at: [x, BENCH_Z - 1.15] as Pt,
    face: 0,
    via: [[11.25, CZ], [11.25, BENCH_Z - 2.2], [x, BENCH_Z - 2.2]] as Pt[],
    key: `bench:${i}`,
  }));
  const lookoutSpots: Spot[] = [
    { at: [-11.25, HOUSE.z0 + 1.1], face: Math.PI, via: [[-11.25, CZ]], key: "look:w" },
    { at: [19.0, HOUSE.z0 + 1.1], face: Math.PI, via: [[19.0, CZ]], key: "look:e" },
  ];
  const handoffSpot = (staffId: string): Spot => {
    const st = STATIONS[staffId];
    return st.z < 0
      ? { at: [st.x + 1.2, -3.0], face: Math.PI, via: [[st.x + 1.2, CZ]], key: `handoff:${staffId}` }
      : { at: [st.x - 1.2, 0.9], face: 0, via: [[st.x - 1.2, CZ]], key: `handoff:${staffId}` };
  };
  // Out through the colonnade and under the verandah, to the end of the front desk.
  const deskSpot: Spot = {
    at: [FRONT_DESK.x - 2.0, FRONT_DESK.z - 0.1],
    face: Math.PI / 2,
    via: [
      [3.75, CZ],
      [3.75, HOUSE.z0 + 0.4],
      [6.4, HOUSE.z0 + 0.4],
      [6.4, FRONT_DESK.z - 0.1],
    ],
  };
  const freeOf = (spots: Spot[]): Spot | null => {
    const free = spots.filter((s) => book.free(s.key));
    return free.length ? pick(free) : null;
  };

  /* ---------------- Bays and their roles ---------------- */
  for (const p of STAFF_PERSONAS) {
    const st = STATIONS[p.id];
    const agent = AGENTS[p.id];
    if (!st || !agent) continue;
    const side = SIDES[agent.side];
    const cz = st.z + 0.2;
    // A light pad: a pastel of the person's colour on the zone, framed in the side's colour, a
    // touch darker than the floor so the white robots stand out on it.
    const zoneBase = Color3.FromHexString(zoneTint(agent.side));
    const padBase = Color3.FromHexString(mixHex(zoneTint(agent.side), p.color, 0.3)).scale(0.94);
    const padLit = Color3.FromHexString(mixHex(zoneTint(agent.side), p.color, 0.55)).scale(0.94);
    const padMat = new StandardMaterial(`v2_pad_${p.id}`, scene);
    padMat.diffuseColor = padBase.clone();
    padMat.specularColor = Color3.Black();
    kit.block(root, st.x, y, cz, POD_W, 0.06, POD_D, padMat);
    const frameMat = kit.mat(side.color, { unlit: true });
    for (const [fw, fd, fx, fz] of [
      [POD_W, 0.22, 0, -POD_D / 2],
      [POD_W, 0.22, 0, POD_D / 2],
      [0.22, POD_D, -POD_W / 2, 0],
      [0.22, POD_D, POD_W / 2, 0],
    ] as const) {
      kit.block(root, st.x + fx, y + 0.02, cz + fz, fw, 0.08, fd, frameMat);
    }
    // Trace from the bus to the bay's edge.
    const edge = st.z < 0 ? cz + POD_D / 2 : cz - POD_D / 2;
    kit.block(root, st.x, y + 0.01, (BUS_Z + edge) / 2, 0.14, 0.04, Math.abs(edge - BUS_Z), traceMat);

    const node = new TransformNode(`v2_pod_${p.id}`, scene);
    node.parent = root;
    node.position.set(st.x, y, cz);
    anchors.set(`agent:${p.id}`, { node, group: "agents", dy: 2.1 });

    const roles = rolesOf(p.id);
    const assistants = roles.filter((rl) => rl.kind !== "lead").map((rl) => rl.short);
    const panel = kit.label(node, {
      lines: [
        { text: p.agent_name, size: 50, weight: 800, color: p.color },
        { text: sideBadge(agent.side).toUpperCase(), size: 30, weight: 800, color: ZONE_TEXT[agent.side].color },
        { text: agent.foundryId, size: 34, weight: 600, color: "#9fd4ff" },
        { text: `+ ${assistants.join(" · ")}`, size: 28, weight: 600, color: "#e2e8f0" },
        { text: `works for ${p.name}`, size: 30, weight: 500, color: "#c6d2e6" },
      ],
      width: 5.2,
      px: 640,
      bg: "rgba(10,20,40,0.94)",
      accent: side.color,
      bar: "left",
      align: "left",
    });
    panel.billboardMode = Mesh.BILLBOARDMODE_Y;
    panel.position.set(0, 4.35, 1.7);

    const ring = MeshBuilder.CreateTorus(`v2_podRing_${p.id}`, { diameter: 6.8, thickness: 0.2, tessellation: 48 }, scene);
    ring.position.set(st.x, y + 0.14, cz);
    kit.finish(ring, root, kit.mat("#ffe08a", { unlit: true }), true);
    ring.setEnabled(false);
    // A low click target over the pad, so the roles standing on it can be clicked on their own.
    kit.hitbox(root, st.x, y, cz, POD_W, 0.5, POD_D, { type: "agent", id: p.id });
    pods.push({ id: p.id, node, ring, pad: padMat, padBase, padLit, zoneBase, power: 1 });

    const offsets = HOME_OFFSETS[roles.length] ?? HOME_OFFSETS[3];
    const handoffs = [...(HANDOFFS[p.id] ?? []), ...(p.id === "lead-1" ? [] : ["lead-1"])];
    const tools = TOOL_WORK[p.id];
    roles.forEach((role, i) => {
      const [ox, oz] = offsets[i] ?? [0, 0];
      const parts = buildRobot(kit, role, p.color, side.color);
      parts.node.parent = root;
      parts.node.scaling.setAll(parts.scale);
      parts.node.position.y = y;
      kit.hitbox(parts.node, 0, 0, 0, 1.2, 2.3, 1.0, role.kind === "lead" ? { type: "agent", id: p.id } : { type: "role", id: role.id });
      const bubble = kit.bubble(parts.node, 2.55, side.color, parts.scale);
      const home: Spot = { at: [st.x + ox, cz + oz], face: Math.PI, via: [[st.x + ox, CZ]] };
      const robot: Robot = { role, ...parts, phase: r() * 6, walking: false, wanderer: null as unknown as Wanderer };
      const lead = role.kind === "lead";
      const voice = role.kind === "voice";
      const outing = (spot: Spot | null, task: string, where: string, dwell: [number, number]): Outing | null =>
        spot ? { spot, task, where, dwell: between(...dwell) } : null;
      const choices: Array<[number, () => Outing | null]> = [
        [voice ? 1 : 3, () => outing(freeOf(hubSpots), pick(["Updating the Client Case", "Reading the Client Case"]), "the Client Case", [4, 7])],
        [2, () => outing(freeOf(uplinkSpots), pick(["Grounding in Foundry IQ", "Checking a PDS extract", "Looking up a rule"]), "the Foundry uplink", [3, 5])],
        [
          voice ? 0 : 2,
          () => {
            const target = pick(handoffs);
            const t = findStaffById(target);
            return outing(book.free(handoffSpot(target).key) ? handoffSpot(target) : null, `Handing over to the ${t?.agent_name ?? "next agent"}`, `${t?.name ?? "the next"}’s bay`, [3, 5]);
          },
        ],
        [tools ? 2 : 0, () => (tools ? outing(book.free(benchSpots[tools.bench].key) ? benchSpots[tools.bench] : null, pick(tools.tasks), "the MCP tool bench", [4, 7]) : null)],
        // The front of the chamber looks onto the street, so only while this floor is in place.
        [0.4, () => (Math.abs(root.position.y) > 0.5 ? null : outing(freeOf(lookoutSpots), "Watching the trams go by", "the colonnade", [4, 6]))],
      ];
      const total = choices.reduce((s, [w]) => s + w, 0);
      robot.wanderer = new Wanderer({
        body: {
          root: parts.node,
          walk: (on) => {
            robot.walking = on;
          },
        },
        home,
        homeName: "the bay",
        homeTask: () => pick(role.tasks),
        homeDwell: lead ? [14, 26] : voice ? [10, 20] : [7, 16],
        plan: () => {
          let roll = r() * total;
          for (const [w, make] of choices) {
            roll -= w;
            if (roll <= 0) return make();
          }
          return null;
        },
        speed: 2.6,
        book,
        rand: r,
        bubble,
        yaw: Math.PI,
      });
      robots.push(robot);
      anchors.set(`role:${role.id}`, { node: parts.node, group: "agents", dy: 2.2 });
    });
  }

  // The bus that links every bay to the Client Case, and the trunk out to the teller counter.
  const xs = Object.values(STATIONS).map((s) => s.x);
  const busX0 = Math.min(...xs);
  const busX1 = Math.max(...xs);
  kit.block(root, (busX0 + busX1) / 2, y + 0.01, BUS_Z, busX1 - busX0 + 0.14, 0.04, 0.14, traceMat);
  const hubBack = HUB.z + 0.7;
  kit.block(root, HUB.x, y + 0.01, (BUS_Z + hubBack) / 2, 0.14, 0.04, BUS_Z - hubBack, traceMat);

  /* ---------------- The Foundry uplink ---------------- */
  kit.cyl(root, UPLINK.x, y, UPLINK.z, 2.3, 0.04, kit.mat("#9f98c9"), { tess: 32 });
  kit.cyl(root, UPLINK.x, y + 0.01, UPLINK.z, 1.4, 0.05, kit.mat("#4c3a99", { unlit: true }), { tess: 24 });
  const uplinkRing = MeshBuilder.CreateTorus("v2_uplinkRing", { diameter: 2.1, thickness: 0.1, tessellation: 40 }, scene);
  uplinkRing.position.set(UPLINK.x, y + 0.06, UPLINK.z);
  kit.finish(uplinkRing, root, kit.mat("#a78bfa", { unlit: true }));
  const uplinkBeam = kit.cyl(root, UPLINK.x, y + 0.05, UPLINK.z, 1.1, 3.0, kit.mat("#c4b5fd", { unlit: true, alpha: 0.22 }), {
    top: 0.5,
    tess: 20,
    dynamic: true,
  });
  uplinkBeam.setEnabled(false);
  const uplinkText = kit.label(root, {
    lines: [{ text: "FOUNDRY UPLINK", size: 60, weight: 800, color: "#4c1d95" }],
    width: 2.6,
    px: 640,
    pxHeight: 96,
  });
  uplinkText.rotation.x = Math.PI / 2;
  uplinkText.position.set(UPLINK.x, y + 0.07, UPLINK.z + 1.5);
  kit.hitbox(root, UPLINK.x, y, UPLINK.z, 2.2, 0.4, 2.2, { type: "infra", id: "foundry" });

  /* ---------------- The MCP tool bench ---------------- */
  BENCH_XS.forEach((x, i) => {
    kit.block(root, x, y, BENCH_Z, 1.0, 1.0, 0.7, darkSteel);
    kit.block(root, x, y + 1.0, BENCH_Z + 0.06, 0.9, 0.62, 0.08, kit.mat("#0b1222"));
    kit.block(root, x, y + 1.08, BENCH_Z, 0.78, 0.46, 0.02, kit.mat(BENCH_COLORS[i], { unlit: true }));
  });
  const benchText = kit.label(root, {
    lines: [{ text: "MCP TOOL BENCH", size: 60, weight: 800, color: "#9d174d" }],
    width: 4.2,
    px: 768,
    pxHeight: 96,
  });
  benchText.rotation.x = Math.PI / 2;
  benchText.position.set((BENCH_XS[0] + BENCH_XS[3]) / 2, y + 0.07, SPARE_CELLS.right.z - 0.4);
  kit.hitbox(root, (BENCH_XS[0] + BENCH_XS[3]) / 2, y, BENCH_Z, 5.2, 1.7, 1.0, { type: "infra", id: "mcp" });

  /* ---------------- The old safe, from 1889 ---------------- */
  {
    const s = SPARE_CELLS.left;
    const sx = s.x;
    const sz = s.z + 2.0;
    const safeGreen = kit.mat("#1f4a3a", { specular: 0.4 });
    kit.block(root, sx, y, sz, 3.0, 0.3, 2.0, hm.iron);
    kit.block(root, sx, y + 0.3, sz, 2.8, 3.2, 1.8, safeGreen);
    kit.block(root, sx, y + 0.5, sz - 0.92, 2.4, 2.8, 0.06, hm.gold);
    kit.block(root, sx, y + 0.62, sz - 0.96, 2.2, 2.56, 0.04, safeGreen);
    const dial = kit.cyl(root, sx - 0.4, y + 2.0, sz - 1.02, 0.7, 0.1, hm.gold, { tess: 18 });
    dial.rotation.x = Math.PI / 2;
    kit.block(root, sx + 0.5, y + 1.6, sz - 1.04, 0.5, 0.12, 0.12, hm.gold);
    kit.block(root, sx + 0.5, y + 1.3, sz - 1.04, 0.12, 0.6, 0.12, hm.gold);
    const plate = kit.label(root, {
      lines: [
        { text: "THE OLD SAFE", size: 40, weight: 800, color: "#7a5a12", serif: true },
        { text: "Guru House · 1889", size: 28, weight: 600, color: "#5b6476" },
      ],
      width: 2.0,
      px: 420,
      bg: "rgba(255,251,242,0.97)",
    });
    plate.position.set(sx, y + 3.95, sz - 0.95);
  }

  /* ---------------- Cara's voice agent and Theo's group chat ---------------- */
  const cara = STATIONS["comms-1"];
  const caraZ = cara.z + 0.2;
  const eqBars: Mesh[] = [];
  const eqMat = kit.mat("#ec4899", { unlit: true });
  for (let i = 0; i < 4; i++) {
    eqBars.push(kit.block(root, cara.x + 2.05 + i * 0.17, y + 0.1, caraZ + 1.95, 0.12, 1, 0.12, eqMat, { dynamic: true }));
  }
  const voiceTag = kit.label(root, {
    lines: [{ text: "voice agent", size: 40, weight: 700, color: "#fbcfe8" }],
    width: 1.9,
    px: 320,
    pxHeight: 70,
    bg: "rgba(10,20,40,0.94)",
  });
  voiceTag.billboardMode = Mesh.BILLBOARDMODE_Y;
  voiceTag.position.set(cara.x + 2.3, y + 2.25, caraZ + 1.95);

  const theo = STATIONS["lead-1"];
  const chatRing = MeshBuilder.CreateTorus("v2_groupChat", { diameter: 5.4, thickness: 0.08, tessellation: 40 }, scene);
  chatRing.position.set(theo.x, y + 3.2, theo.z + 0.2);
  kit.finish(chatRing, root, kit.mat(SIDES.shared.color, { unlit: true }), true);
  const chatTag = kit.label(root, {
    lines: [{ text: "group chat", size: 40, weight: 700, color: "#c7d2fe" }],
    width: 1.9,
    px: 320,
    pxHeight: 70,
    bg: "rgba(10,20,40,0.94)",
  });
  chatTag.billboardMode = Mesh.BILLBOARDMODE_Y;
  chatTag.position.set(theo.x - 2.5, y + 3.4, theo.z + 0.2);

  /* ---------------- The Client Case hub: the old teller counter ---------------- */
  const walnut = kit.mat("#ffffff", { texture: "walnut" });
  const marble = kit.mat("#f1eee6", { specular: 0.3 });
  kit.block(root, HUB.x, y, HUB.z, 4.8, 1.15, 1.1, walnut);
  kit.block(root, HUB.x, y + 1.15, HUB.z, 5.0, 0.1, 1.3, marble);
  kit.block(root, HUB.x, y + 0.9, HUB.z - 0.56, 4.4, 0.06, 0.02, kit.mat("#c9a14a", { unlit: true }));
  // The old brass teller grilles, now framing the case.
  for (const dx of [-1.6, 1.6]) {
    kit.block(root, HUB.x + dx, y + 1.25, HUB.z + 0.2, 1.2, 0.08, 0.08, hm.gold);
    for (let i = 0; i < 5; i++) kit.block(root, HUB.x + dx - 0.48 + i * 0.24, y + 1.25, HUB.z + 0.2, 0.05, 1.0, 0.05, hm.gold);
    kit.block(root, HUB.x + dx, y + 2.25, HUB.z + 0.2, 1.2, 0.1, 0.1, hm.gold);
  }
  const hubRingFloor = MeshBuilder.CreateTorus("v2_hubFloorRing", { diameter: 2.0, thickness: 0.08, tessellation: 32 }, scene);
  hubRingFloor.position.set(HUB.x, y + 1.25, HUB.z);
  kit.finish(hubRingFloor, root, kit.mat("#ffc35a", { unlit: true }));
  const hub = new TransformNode("v2_clientCase", scene);
  hub.parent = root;
  hub.position.set(HUB.x, y + 2.6, HUB.z);
  const caseCube = kit.block(hub, 0, -0.5, 0, 1.0, 1.0, 1.0, kit.mat("#ffc35a", { emissive: 0.55 }), {
    dynamic: true,
  });
  kit.block(caseCube, 0.2, 0.5, 0, 0.45, 0.14, 0.8, kit.mat("#fff1c9", { emissive: 0.6 }), { dynamic: true });
  const hubRings = [1.8, 2.4].map((d, i) => {
    const ring = MeshBuilder.CreateTorus(`v2_hubRing${i}`, { diameter: d, thickness: 0.07, tessellation: 40 }, scene);
    kit.finish(ring, hub, kit.mat("#5ce1ff", { unlit: true }), true);
    return ring;
  });
  const hubTag = kit.label(root, {
    lines: [
      { text: "CLIENT CASE", size: 52, weight: 800, color: "#ffd98a" },
      { text: "the one record every agent shares", size: 30, weight: 500, color: "#c6d2e6" },
    ],
    width: 4.2,
    px: 560,
    bg: "rgba(10,20,40,0.94)",
  });
  hubTag.billboardMode = Mesh.BILLBOARDMODE_Y;
  hubTag.position.set(HUB.x, y + 4.35, HUB.z);
  anchors.set("hub", { node: hub, group: "agents", dy: 0 });

  /* ---------------- Data packets ---------------- */
  const packetMat = kit.mat("#0e7490", { unlit: true });
  const packets: Packet[] = [];
  for (const [i, p] of STAFF_PERSONAS.entries()) {
    const st = STATIONS[p.id];
    const cz = st.z + 0.2;
    const edge = st.z < 0 ? cz + POD_D / 2 : cz - POD_D / 2;
    const py = y + 0.2;
    const path = dedupe([
      new Vector3(HUB.x, py, hubBack),
      new Vector3(HUB.x, py, BUS_Z),
      new Vector3(st.x, py, BUS_Z),
      new Vector3(st.x, py, edge),
    ]);
    let length = 0;
    for (let k = 1; k < path.length; k++) length += Vector3.Distance(path[k - 1], path[k]);
    for (let j = 0; j < 2; j++) {
      const mesh = kit.block(root, 0, 0, 0, 0.28, 0.16, 0.28, packetMat, { dynamic: true });
      packets.push({ mesh, path, length, speed: 3 + (i % 3) * 0.6, offset: j * 0.5 + i * 0.13 });
    }
  }

  kit.useBatch(null);
  batch.flush();

  let selected: ReadonlySet<string> = new Set();
  let linked: ReadonlySet<string> = new Set();
  const lit = (id: string): boolean => selected.has(id) || linked.has(id);
  let introT = -1;
  const pointOnPath = (pk: Packet, f: number, out: Vector3): void => {
    // Ping-pong along the polyline so packets go out to the bay and come back.
    const u = f < 0.5 ? f * 2 : 2 - f * 2;
    let d = u * pk.length;
    for (let k = 1; k < pk.path.length; k++) {
      const seg = Vector3.Distance(pk.path[k - 1], pk.path[k]);
      if (d <= seg || k === pk.path.length - 1) {
        Vector3.LerpToRef(pk.path[k - 1], pk.path[k], seg > 0 ? Math.min(1, d / seg) : 1, out);
        return;
      }
      d -= seg;
    }
  };
  const tmp = new Vector3();
  const deskRobot = robots.find((rb) => rb.role.id === DESK_ROLE);
  const byRole = new Map(robots.map((rb) => [rb.role.id, rb]));
  // The front desk wants the Fact Find Extraction agent; `deskPinned` says whether it has been sent.
  let deskCalled = false;
  let deskPinned = false;
  const sendToDesk = (): void => {
    deskRobot?.wanderer.pin({ spot: deskSpot, task: "Reading forms at the front desk", where: "the front desk", dwell: Infinity });
    deskPinned = true;
  };

  return {
    id: "agents",
    root,
    tag,
    anchors,
    update(dt: number, t: number): void {
      if (introT >= 0) introT += dt;
      const lifted = Math.abs(root.position.y) > 0.5;
      // Called while the floor was out of the way: go now it is back in place.
      if (deskCalled && !deskPinned && !lifted) sendToDesk();
      pods.forEach((pod, i) => {
        const on = lit(pod.id);
        // Bays power up one after another during the chapter intro, from the bare zone to their colour.
        pod.power = introT >= 0 ? Math.min(1, Math.max(0, (introT - i * 0.18) / 0.5)) : 1;
        Color3.LerpToRef(pod.zoneBase, on ? pod.padLit : pod.padBase, pod.power, pod.pad.diffuseColor);
        if (on) pod.ring.scaling.setAll(1 + Math.sin(t * 5) * 0.05);
      });
      if (introT > 3) introT = -1;
      let uplinkBusy = false;
      for (const rb of robots) {
        // A level moved out of the way takes its robots with it, so call in any that are out front.
        if (lifted && rb.node.position.z < HOUSE.z0 - 0.2) {
          rb.wanderer.snapHome();
          if (rb === deskRobot) deskPinned = false;
        }
        rb.wanderer.update(dt);
        const working = selected.has(rb.role.staffId) && rb.wanderer.isHome;
        // Out under the verandah, the robot stands on the footpath, a step down from the floor.
        const ground = rb.node.position.z < HOUSE.z0 ? 0.15 : y;
        if (rb.walking) {
          rb.node.position.y = ground + 0.14 + Math.sin(t * 11 + rb.phase) * 0.04;
          rb.node.rotation.x = -0.1;
        } else {
          const amp = working ? 0.3 : 0.05;
          rb.node.position.y = ground + Math.abs(Math.sin(t * (working ? 5 : 1.8) + rb.phase)) * amp;
          rb.node.rotation.x = 0;
        }
        rb.tip.scaling.setAll(1 + 0.35 * Math.sin(t * 5 + rb.phase));
        if (rb.ring.isEnabled()) rb.ring.scaling.setAll(1 + Math.sin(t * 5) * 0.06);
        if (!rb.walking && Math.abs(rb.node.position.z - (UPLINK.z - 0.3)) < 0.4 && Math.abs(Math.abs(rb.node.position.x - UPLINK.x) - 1.5) < 0.2) {
          uplinkBusy = true;
        }
      }
      uplinkBeam.setEnabled(uplinkBusy);
      if (uplinkBusy) uplinkBeam.scaling.x = uplinkBeam.scaling.z = 1 + Math.sin(t * 6) * 0.08;
      uplinkRing.scaling.setAll(1 + Math.sin(t * 2) * 0.03);
      hub.rotation.y = t * 0.8;
      caseCube.rotation.y = t * 0.6;
      caseCube.position.y = Math.sin(t * 1.8) * 0.12;
      hubRings[0].rotation.x = t * 1.1;
      hubRings[1].rotation.z = t * 0.9;
      for (const pk of packets) {
        const f = ((t * pk.speed) / (pk.length * 2) + pk.offset) % 1;
        pointOnPath(pk, f, tmp);
        pk.mesh.position.copyFrom(tmp);
      }
      eqBars.forEach((bar, i) => {
        bar.scaling.y = 0.4 + Math.abs(Math.sin(t * (4 + i * 0.7) + i)) * 1.4;
        bar.position.y = y + 0.1 + bar.scaling.y / 2;
      });
      chatRing.rotation.y = t * 0.6;
      chatRing.position.y = y + 3.2 + Math.sin(t * 1.5) * 0.1;
    },
    setHighlights(hl: Highlights): void {
      selected = hl.agents;
      for (const pod of pods) pod.ring.setEnabled(lit(pod.id));
      for (const rb of robots) {
        rb.ring.setEnabled(hl.roles.has(rb.role.id));
        // A bay that is selected, or working on the finale's request, calls its team home to work.
        rb.wanderer.hold(selected.has(rb.role.staffId));
      }
    },
    intro(): void {
      introT = 0;
    },
    callToDesk(): void {
      deskCalled = true;
      if (Math.abs(root.position.y) <= 0.5) sendToDesk();
    },
    releaseDesk(): void {
      deskCalled = false;
      if (deskPinned) deskRobot?.wanderer.release();
      deskPinned = false;
    },
    deskReady(): boolean {
      if (!deskRobot) return true;
      const p = deskRobot.node.position;
      return !deskRobot.wanderer.walking && Math.hypot(p.x - deskSpot.at[0], p.z - deskSpot.at[1]) < 0.3;
    },
    roleTask(id: string): string | null {
      return byRole.get(id)?.wanderer.task ?? null;
    },
    setLinked(ids: ReadonlySet<string>): void {
      linked = ids;
      for (const pod of pods) pod.ring.setEnabled(lit(pod.id));
    },
  };
}
