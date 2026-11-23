/**
 * Party guests who come and go. Every so often a guest (or the retired couple) says goodbye, rides
 * the glass lift down from the roof, walks over to the tram stop and catches the tram home. Later
 * they come back on another tram, step off at the first door, walk across the forecourt to the
 * lift and ride up to rejoin the party. A speech bubble says what they are doing, and so does
 * their card. At most two groups are out at once, and only one uses the lift at a time.
 *
 * Departures start only while the whole house is together, and the finale pauses the trips (with
 * everyone back at the party) so the street and the lift are clear for its own client.
 */
import type { TramStop } from "./frontDesk";
import { rng } from "./kit";
import { KERB_Z, LIFT, PLATFORM, PLATFORM_EDGE, TRAM_DOORS, TRAM_STOP_X } from "./layout";
import { LIFT_FLOOR, type Lift, type LiftRider, type LiftTrip } from "./lift";
import type { GuestBody, Party } from "./party";

type Pt = readonly [number, number];

const PATH_Y = 0.15;
const WALK = 2.2;
/** Party guests use the tram's first door, nearest the lift; the front desk's visitors use the middle one. */
const DOOR_X = TRAM_STOP_X + TRAM_DOORS[0];
/** Where they wait on the platform for the tram home, beside the shelter. */
const WAIT: Pt[] = [
  [DOOR_X - 0.5, PLATFORM_EDGE + 1.0],
  [DOOR_X + 0.7, PLATFORM_EDGE + 1.15],
];
/** Up off the platform, onto the forecourt, on the way between the stop and the lift. */
const CROSSING: Pt[] = [
  [DOOR_X - 1.6, KERB_Z + 1.0],
  [DOOR_X - 0.8, KERB_Z + 1.5],
];
/** Who comes and goes, and in what order. */
const GROUPS: string[][] = [["marcus"], ["retired-1", "retired-2"], ["hannah"], ["backatwork"]];
const GOODBYES = ["Time to head home", "What a night! Off home", "Thanks for a lovely party!"];

type Phase =
  | "party"
  | "to-lift"
  | "lift-down"
  | "to-stop"
  | "waiting"
  | "boarding"
  | "gone"
  | "riding-in"
  | "stepping-off"
  | "to-lift-up"
  | "lift-up";

/** The phases while a group is using the lift, or about to. */
const AT_LIFT: Phase[] = ["to-lift", "lift-down", "to-lift-up", "lift-up"];

interface Group {
  ids: string[];
  phase: Phase;
  bodies: GuestBody[];
  routes: Pt[][];
  t: number;
  trip: LiftTrip | null;
  key: string;
  talk: number;
}

export interface PartyTrips {
  update(dt: number): void;
  /** Freeze the trips while the street is hidden. */
  setActive(on: boolean): void;
  /** The finale plays: everyone out on a trip is back at the party at once, and nobody leaves until it ends. */
  setPaused(on: boolean): void;
}

export function buildPartyTrips(party: Party, lift: Lift, tram: TramStop, roofShown: () => boolean): PartyTrips {
  const r = rng(515);
  const T = party.travel;
  const groups: Group[] = GROUPS.map((ids, i) => ({ ids, phase: "party", bodies: [], routes: [], t: 0, trip: null, key: `party:${i}`, talk: 0 }));
  let active = true;
  let paused = false;
  let turn = 0;
  let nextOut = 30;

  const status = (g: Group, text: string): void => {
    for (const id of g.ids) T.setStatus(id, text);
  };
  const say = (g: Group, text: string): void => {
    g.bodies[0]?.bubble.say(text);
    g.talk = 3.2;
  };
  /** For the lift's card: a couple ride as one name. */
  const riders = (g: Group): LiftRider[] =>
    g.bodies.map((b, i) => ({
      node: b.node,
      ch: b.ch,
      name: g.ids.length > 1 ? (i === 0 ? "A retired couple" : "") : b.name,
      bubble: b.bubble,
    }));
  const walking = (g: Group, on: boolean): void => {
    for (const b of g.bodies) b.ch.setWalking(on);
  };
  /** Onto the street, at house-frame coordinates (the lift's root sits at the origin). */
  const place = (b: GuestBody, x: number, z: number): void => {
    b.node.parent = lift.root;
    b.node.position.set(x, z < KERB_Z ? PLATFORM.y : PATH_Y, z);
  };
  /** Walks each body along its route; true once they are all there. */
  const walk = (g: Group, dt: number): boolean => {
    let all = true;
    g.bodies.forEach((b, i) => {
      const route = g.routes[i];
      const node = b.node;
      let step = WALK * dt;
      while (step > 0 && route.length) {
        const [tx, tz] = route[0];
        const dx = tx - node.position.x;
        const dz = tz - node.position.z;
        const d = Math.hypot(dx, dz);
        if (d <= step) {
          node.position.x = tx;
          node.position.z = tz;
          step -= d;
          route.shift();
        } else {
          node.position.x += (dx / d) * step;
          node.position.z += (dz / d) * step;
          node.rotation.y = Math.atan2(dx, dz);
          step = 0;
        }
      }
      node.position.y = node.position.z < KERB_Z ? PLATFORM.y : PATH_Y;
      b.ch.update(dt);
      if (route.length) all = false;
      else b.ch.setWalking(false);
    });
    return all;
  };

  /** Off home: the group walks to the lift's landing on the roof. */
  const depart = (g: Group): void => {
    T.goToLift(g.ids, g.ids.map((_, i) => lift.waitAt("roof", i)), GOODBYES[Math.floor(r() * GOODBYES.length)]);
    g.phase = "to-lift";
    g.t = 0;
  };
  /** Back at the party at once, wherever they were. */
  const snapBack = (g: Group): void => {
    if (g.trip) lift.cancel(g.trip);
    g.trip = null;
    tram.hold(g.key, false);
    for (const b of g.bodies) {
      b.bubble.hide();
      b.ch.setWalking(false);
    }
    for (const id of g.ids) T.snapBack(id);
    g.bodies = [];
    g.phase = "party";
  };

  const stepGroup = (g: Group, dt: number): void => {
    if (g.talk > 0 && (g.talk -= dt) <= 0) g.bodies[0]?.bubble.hide();
    switch (g.phase) {
      case "party":
        return;
      case "to-lift":
        if (T.atLift(g.ids)) {
          g.bodies = g.ids.map((id) => T.take(id, "Riding the glass lift down"));
          g.trip = {
            from: "roof",
            riders: riders(g),
            say: "Going down!",
            done: () => {
              g.trip = null;
              g.routes = g.bodies.map((_, i) => [CROSSING[i], WAIT[i]]);
              walking(g, true);
              status(g, "Walking to the tram stop");
              g.phase = "to-stop";
            },
          };
          lift.call(g.trip);
          g.phase = "lift-down";
        } else if ((g.t += dt) > 60) snapBack(g);
        return;
      case "lift-down":
      case "lift-up":
        return;
      case "to-stop":
        if (walk(g, dt)) {
          for (const b of g.bodies) b.node.rotation.y = Math.PI;
          status(g, "Waiting for the tram home");
          say(g, "Waiting for the tram");
          g.phase = "waiting";
        }
        return;
      case "waiting":
        if (!tram.standing) return;
        tram.hold(g.key, true);
        if (tram.open) {
          g.t = 0;
          g.routes = g.bodies.map((b) => [[b.node.position.x, b.node.position.z]]);
          g.phase = "boarding";
        }
        return;
      case "boarding": {
        g.t += dt;
        let all = true;
        g.bodies.forEach((b, i) => {
          const f = Math.min(1, Math.max(0, (g.t - i * 0.3) / 0.6));
          const [fx, fz] = g.routes[i][0];
          b.node.position.set(fx + (DOOR_X + i * 0.3 - fx) * f, PLATFORM.y + Math.sin(Math.PI * f) * 0.4, fz + (PLATFORM_EDGE - 0.1 - fz) * f);
          if (f < 1) all = false;
          else b.node.setEnabled(false);
        });
        if (!all) return;
        tram.hold(g.key, false);
        for (const b of g.bodies) b.bubble.hide();
        status(g, "On the tram home");
        g.t = 25 + r() * 20;
        g.phase = "gone";
        return;
      }
      case "gone":
        if ((g.t -= dt) > 0) return;
        status(g, "On the tram, coming back to the party");
        g.phase = "riding-in";
        return;
      case "riding-in":
        if (!tram.standing) return;
        tram.hold(g.key, true);
        if (!tram.open) return;
        g.bodies.forEach((b, i) => {
          place(b, DOOR_X + i * 0.3, PLATFORM_EDGE - 0.1);
          b.node.rotation.y = 0;
          b.node.setEnabled(true);
        });
        g.t = 0;
        g.phase = "stepping-off";
        return;
      case "stepping-off": {
        g.t += dt;
        let all = true;
        g.bodies.forEach((b, i) => {
          const f = Math.min(1, Math.max(0, (g.t - i * 0.3) / 0.6));
          const x0 = DOOR_X + i * 0.3;
          const z0 = PLATFORM_EDGE - 0.1;
          b.node.position.set(x0 - 0.2 * f, PLATFORM.y + 0.2 * (1 - f) + Math.sin(Math.PI * f) * 0.4, z0 + 0.9 * f);
          if (f < 1) all = false;
        });
        if (!all) return;
        tram.hold(g.key, false);
        g.routes = g.bodies.map((_, i) => [CROSSING[i], [LIFT.x + 2.4 + i * 0.5, LIFT.z0 - 2.0], lift.waitAt("street", i)]);
        walking(g, true);
        say(g, "Up to the party!");
        status(g, "Walking over to the glass lift");
        g.phase = "to-lift-up";
        return;
      }
      case "to-lift-up":
        if (!walk(g, dt)) return;
        for (const b of g.bodies) b.node.rotation.y = lift.waitFace("street");
        g.trip = {
          from: "street",
          riders: riders(g),
          say: g.bodies.length > 1 ? "Back up we go!" : "Going up!",
          sayOut: "Hello again, everyone!",
          done: () => {
            g.trip = null;
            for (const b of g.bodies) T.giveBack(b.id, [b.node.position.x, b.node.position.z]);
            g.bodies = [];
            g.phase = "party";
          },
        };
        status(g, "Riding the glass lift up to the party");
        lift.call(g.trip);
        g.phase = "lift-up";
        return;
    }
  };

  // Marcus is already on his way by tram, so the first tram brings someone to the party.
  {
    const g = groups[0];
    g.bodies = g.ids.map((id) => T.take(id, "On the tram, coming to the party"));
    g.bodies.forEach((b, i) => {
      place(b, DOOR_X + i * 0.3, PLATFORM_EDGE - 0.1);
      b.node.position.y = LIFT_FLOOR.street;
      b.node.setEnabled(false);
    });
    g.phase = "riding-in";
    turn = 1;
  }

  return {
    update(dt: number): void {
      if (!active) return;
      for (const g of groups) stepGroup(g, dt);
      if (paused || !roofShown()) return;
      const out = groups.filter((g) => g.phase !== "party");
      if (out.length >= 2 || out.some((g) => AT_LIFT.includes(g.phase))) return;
      if ((nextOut -= dt) > 0) return;
      const g = groups[turn % groups.length];
      if (g.phase === "party" && T.free(g.ids)) {
        depart(g);
        turn++;
        nextOut = 20 + r() * 20;
      } else {
        turn++;
        nextOut = 3;
      }
    },
    setActive(on: boolean): void {
      active = on;
    },
    setPaused(on: boolean): void {
      if (on === paused) return;
      paused = on;
      if (on) for (const g of groups) if (g.phase !== "party") snapBack(g);
      nextOut = 20;
    },
  };
}
