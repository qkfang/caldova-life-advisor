/**
 * The v2 storybook HUD: the cover, the page card with each chapter's text
 * and navigation, the lift panel, the arrows that slide the view left or
 * right, the inspector card, the finale's request trace panel and the hover
 * tooltip. DOM only — main.ts drives the 3D.
 */
import { CLIENT_PERSONAS, type ScenarioId } from "../personaData";
import type { InspectorAction, InspectorView } from "./inspector";
import { LEVELS } from "./houseData";
import type { TraceStep } from "./requestTrace";
import { PAGES, pageIndex, type PageId, type StoryPage } from "./storyData";

export interface StorybookHooks {
  page(page: StoryPage, index: number): void;
  action(action: InspectorAction): void;
  closeInspector(): void;
  /** An arrow is pressed (-1 left, 1 right) or let go (0). */
  pan(dir: -1 | 0 | 1): void;
  scenario(id: ScenarioId): void;
  trace(cmd: "toggle" | "skip" | "speed" | "replay"): void;
  /** A panel has shrunk to its bar or grown back; `freed` is how much narrower it got, in pixels (negative when it grew). */
  layout(panel: PanelId, shrunk: boolean, freed: number): void;
}

/** The panels that shrink to a bar: the cover, the page card (with the finale's trace), the inspector card and the lift panel. */
export type PanelId = "cover" | "page" | "inspector" | "lift";
const PANELS: PanelId[] = ["cover", "page", "inspector", "lift"];
const PANEL_NAME: Record<PanelId, string> = {
  cover: "the cover",
  page: "the page card",
  inspector: "the card",
  lift: "the lift panel",
};
const MINUS = "\u2212";

export interface TraceState {
  scenario: ScenarioId;
  step: TraceStep | null;
  paused: boolean;
  speed: number;
  done: boolean;
}

const ESCAPES: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
const esc = (s: string): string => s.replace(/[&<>"']/g, (c) => ESCAPES[c]);

function byId<T extends HTMLElement>(id: string): T {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} is missing from index.html`);
  return el as T;
}

/** Short name for navigation buttons and the lift. */
function shortTitle(p: StoryPage): string {
  if (p.id === "cover") return "Guru House";
  if (p.id === "finale") return "Top to bottom";
  return LEVELS.find((l) => l.id === p.id)?.name ?? p.title;
}

/** A panel's shrink/expand button. */
function toggleHtml(id: PanelId, shrunk: boolean): string {
  const label = `${shrunk ? "Expand" : "Shrink"} ${PANEL_NAME[id]}`;
  return `<button type="button" class="panel-toggle" data-shrink="${id}" aria-controls="v2-${id}" aria-expanded="${!shrunk}" aria-label="${label}" title="${label}"><span aria-hidden="true">${shrunk ? "+" : MINUS}</span></button>`;
}

export class Storybook {
  private index = 0;
  private collapsed = false;
  /**
   * Which panels are shrunk to their bar. Each panel keeps its own state across page turns. Shrink
   * all and expand all set every panel, the inspector's included while it is closed, so a card
   * opened later opens the way the inspector was last left.
   */
  private readonly shrunk: Record<PanelId, boolean> = { cover: false, page: false, inspector: false, lift: false };
  /** The cover and the lift panel are centred up and down: their expanded heights, to keep their top edge put when they shrink. */
  private readonly tall: Partial<Record<PanelId, number>> = {};
  /** The last expanded right edge and height of each panel (the page card's for each page), to tell how much room shrinking it freed. */
  private readonly wide = new Map<string, { right: number; height: number }>();
  private wideKey(id: PanelId): string {
    return id === "page" ? `page:${PAGES[this.index].id}` : id;
  }
  private actions: InspectorAction[] = [];
  private links: InspectorAction[] = [];
  private trace: TraceState | null = null;
  private readonly cover = byId<HTMLElement>("v2-cover");
  private readonly card = byId<HTMLElement>("v2-page");
  private readonly lift = byId<HTMLElement>("v2-lift");
  private readonly pan = byId<HTMLElement>("v2-pan");
  private readonly inspector = byId<HTMLElement>("v2-inspector");
  private readonly tooltip = byId<HTMLElement>("v2-tooltip");
  private readonly pill = byId<HTMLButtonElement>("v2-pill");
  private readonly all = byId<HTMLButtonElement>("v2-shrink-all");

  constructor(private readonly hooks: StorybookHooks) {
    this.renderCover();
    this.renderLift();
    this.renderPan();
    this.card.addEventListener("click", (e) => this.onCardClick(e));
    this.inspector.addEventListener("click", (e) => {
      const el = (e.target as HTMLElement).closest<HTMLElement>("[data-insp]");
      if (!el) return;
      if (el.dataset.insp === "close") this.hooks.closeInspector();
      else if (el.dataset.insp === "act" || el.dataset.insp === "link") {
        const a = (el.dataset.insp === "act" ? this.actions : this.links)[Number(el.dataset.i)];
        if (a) this.hooks.action(a);
      }
    });
    // Every panel's own shrink/expand button.
    document.addEventListener("click", (e) => {
      const el = (e.target as HTMLElement).closest<HTMLElement>("[data-shrink]");
      const id = el?.dataset.shrink as PanelId | undefined;
      if (id && PANELS.includes(id)) this.setShrunk(id, !this.shrunk[id]);
    });
    this.all.addEventListener("click", () => {
      const open = this.anyOpen();
      for (const id of PANELS) this.setShrunk(id, open, false);
      this.updateAll();
    });
    this.pill.addEventListener("click", () => this.setCollapsed(false));
    window.addEventListener("resize", () => {
      for (const id of ["cover", "lift"] as const) if (this.shrunk[id]) this.pin(id);
    });
    window.addEventListener("keydown", (e) => {
      const t = e.target as HTMLElement | null;
      if (t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.isContentEditable)) return;
      if (e.altKey || e.ctrlKey || e.metaKey) return;
      if (e.key === "ArrowRight" || e.key === "ArrowDown" || e.key === "PageDown") {
        e.preventDefault();
        this.step(1);
      } else if (e.key === "ArrowLeft" || e.key === "ArrowUp" || e.key === "PageUp") {
        e.preventDefault();
        this.step(-1);
      } else if (e.key === "Home") {
        this.goTo(0);
      } else if (e.key === "End") {
        this.goTo(PAGES.length - 1);
      } else if (e.key === "Escape") {
        this.hooks.closeInspector();
      }
    });
    this.updateAll();
  }

  get page(): StoryPage {
    return PAGES[this.index];
  }

  goTo(target: number | PageId): void {
    const i = typeof target === "number" ? target : pageIndex(target);
    if (i < 0 || i >= PAGES.length) return;
    const from = this.index;
    this.index = i;
    this.renderPage();
    this.updateAll();
    this.updateLift(from);
    this.hooks.page(PAGES[i], i);
  }

  step(delta: number): void {
    const i = Math.max(0, Math.min(PAGES.length - 1, this.index + delta));
    if (i !== this.index) this.goTo(i);
  }

  setCollapsed(collapsed: boolean): void {
    this.collapsed = collapsed;
    this.renderPage();
    this.updateAll();
  }

  /** Shrinks a panel to its bar, or expands it again. */
  setShrunk(id: PanelId, on: boolean, update = true): void {
    if (this.shrunk[id] !== on) {
      const el = this.panelEl(id);
      const shown = this.visible(id);
      const before = shown ? el.getBoundingClientRect() : null;
      if (on && before) {
        if (id === "cover" || id === "lift") this.tall[id] = before.height;
        this.wide.set(this.wideKey(id), { right: before.right, height: before.height });
      }
      this.shrunk[id] = on;
      this.applyShrink(id);
      if (before) this.hooks.layout(id, on, before.width - el.getBoundingClientRect().width);
    }
    if (update) this.updateAll();
  }

  isShrunk(id: PanelId): boolean {
    return this.shrunk[id];
  }

  /**
   * How much of the screen's width a shrunk panel on the left has handed back, in CSS pixels: none
   * for a panel that only covered a corner (a chapter's page card), so the view only slides over
   * for panels that ran down the side of it (the cover, and the finale's page card).
   */
  freedWidth(id: "cover" | "page"): number {
    if (!this.shrunk[id] || !this.visible(id)) return 0;
    const el = this.panelEl(id);
    const now = el.getBoundingClientRect();
    const was = this.wide.get(this.wideKey(id)) ?? this.guessWide(id);
    return was.height > window.innerHeight * 0.6 ? Math.max(0, was.right - now.right) : 0;
  }

  /** The expanded size of a panel that has never been seen expanded, from its CSS width. */
  private guessWide(id: "cover" | "page"): { right: number; height: number } {
    const vw = window.innerWidth;
    if (id === "cover") return { right: 40 + Math.min(470, vw - 48), height: window.innerHeight * 0.75 };
    return { right: 20 + Math.min(560, vw - 280), height: this.index === PAGES.length - 1 ? window.innerHeight * 0.85 : 360 };
  }

  private panelEl(id: PanelId): HTMLElement {
    return id === "cover" ? this.cover : id === "page" ? this.card : id === "inspector" ? this.inspector : this.lift;
  }

  private visible(id: PanelId): boolean {
    switch (id) {
      case "cover":
        return !this.collapsed && this.index === 0;
      case "page":
        return !this.collapsed && this.index !== 0;
      case "inspector":
        return !this.inspector.classList.contains("hidden");
      default:
        return true;
    }
  }

  /** Whether any panel on screen is open (not shrunk). */
  private anyOpen(): boolean {
    return PANELS.some((id) => this.visible(id) && !this.shrunk[id]);
  }

  /** The shrink-all button shows − while any panel on screen is open, and + once they are all shrunk. */
  private updateAll(): void {
    const open = this.anyOpen();
    const label = open ? "Shrink all panels" : "Expand all panels";
    this.all.setAttribute("aria-expanded", String(open));
    this.all.setAttribute("aria-label", label);
    this.all.title = label;
    this.all.innerHTML = `<span aria-hidden="true">${open ? MINUS : "+"}</span>`;
  }

  /** Shows a panel's state: its class, and its button's glyph and label. */
  private applyShrink(id: PanelId): void {
    const el = this.panelEl(id);
    const on = this.shrunk[id];
    el.classList.toggle("shrunk", on);
    const b = el.querySelector<HTMLButtonElement>(`[data-shrink="${id}"]`);
    if (b) {
      const label = `${on ? "Expand" : "Shrink"} ${PANEL_NAME[id]}`;
      b.setAttribute("aria-expanded", String(!on));
      b.setAttribute("aria-label", label);
      b.title = label;
      b.innerHTML = `<span aria-hidden="true">${on ? "+" : MINUS}</span>`;
    }
    if (id === "cover" || id === "lift") {
      if (on) this.pin(id);
      else {
        el.style.top = "";
        el.style.transform = "";
      }
    }
  }

  /** The cover and the lift panel are centred up and down: shrunk, they keep their top edge where it was. */
  private pin(id: "cover" | "lift"): void {
    const el = this.panelEl(id);
    // On a narrow screen the cover sits at the bottom instead, and keeps to it.
    if (id === "cover" && window.innerWidth <= 720) return;
    const h = this.tall[id] ?? (id === "cover" ? 640 : 420);
    el.style.top = `${Math.max(76, Math.round((window.innerHeight - h) / 2))}px`;
    el.style.transform = "none";
  }

  showInspector(view: InspectorView): void {
    this.actions = view.actions;
    this.links = view.links;
    this.inspector.style.setProperty("--accent", view.accent);
    const buttons = view.actions
      .map((a, i) => {
        const cls = `btn ${a.primary ? "primary" : "ghost"}`;
        return a.href
          ? `<a class="${cls}" href="${esc(a.href)}" target="_blank" rel="noopener">${esc(a.label)}</a>`
          : `<button type="button" class="${cls}" data-insp="act" data-i="${i}">${esc(a.label)}</button>`;
      })
      .join("");
    this.inspector.innerHTML = `
      ${toggleHtml("inspector", this.shrunk.inspector)}
      <button type="button" class="insp-close" data-insp="close" aria-label="Close the card" title="Close the card">×</button>
      <div class="insp-body">${view.html}</div>
      <div class="insp-actions">${buttons}</div>`;
    this.inspector.classList.toggle("shrunk", this.shrunk.inspector);
    this.inspector.classList.remove("hidden");
    this.inspector.scrollTop = 0;
    document.body.classList.add("inspecting");
    this.updateAll();
  }

  hideInspector(): void {
    this.inspector.classList.add("hidden");
    document.body.classList.remove("inspecting");
    this.updateAll();
  }

  /** Refresh the open card's live "Now" line without re-rendering it. */
  setInspectorNow(text: string | null): void {
    if (!text || this.inspector.classList.contains("hidden")) return;
    const el = this.inspector.querySelector<HTMLElement>("[data-now]");
    if (el && el.textContent !== text) el.textContent = text;
  }

  showTooltip(text: string, x: number, y: number): void {
    this.tooltip.textContent = text;
    this.tooltip.style.transform = `translate(${Math.round(x + 16)}px, ${Math.round(y + 18)}px)`;
    this.tooltip.classList.remove("hidden");
  }

  hideTooltip(): void {
    this.tooltip.classList.add("hidden");
  }

  setTrace(state: TraceState | null): void {
    this.trace = state;
    this.renderTrace();
  }

  /* ---------------- rendering ---------------- */

  private renderCover(): void {
    const p = PAGES[0];
    const levels = LEVELS.map(
      (l) =>
        `<li style="--c:${l.accent}"><b>${esc(l.floor)}</b><span>${esc(l.name)}</span><em>${esc(l.caption)}</em></li>`,
    ).join("");
    this.cover.innerHTML = `
      <div class="cover-card">
        ${toggleHtml("cover", this.shrunk.cover)}
        <div class="cover-kicker">${esc(p.kicker)}</div>
        <h1>Guru House</h1>
        <p class="cover-title">${esc(p.title)}</p>
        ${p.body.map((b) => `<p class="cover-lede">${esc(b)}</p>`).join("")}
        <ol class="cover-levels">${levels}</ol>
        <div class="cover-actions">
          <button type="button" class="btn primary" data-cover="begin">Begin the story ›</button>
          <button type="button" class="btn ghost" data-cover="explore">Explore freely</button>
        </div>
        <p class="cover-hint">${esc(p.hint)}</p>
      </div>`;
    this.cover.addEventListener("click", (e) => {
      const el = (e.target as HTMLElement).closest<HTMLElement>("[data-cover]");
      if (el?.dataset.cover === "begin") this.goTo(1);
      else if (el?.dataset.cover === "explore") this.setCollapsed(true);
    });
  }

  private renderLift(): void {
    const buttons = PAGES.map((p, i) => {
      const label = shortTitle(p);
      return `<button type="button" class="lift-btn" data-page="${i}" style="--c:${p.accent}" title="${esc(label)}"><b>${esc(p.floor)}</b><span>${esc(label)}</span></button>`;
    }).join("");
    this.lift.innerHTML = `
      ${toggleHtml("lift", this.shrunk.lift)}
      <div class="lift-display" aria-hidden="true"><span class="lift-arrow"></span><span class="lift-floor">${esc(PAGES[0].floor)}</span></div>
      ${buttons}`;
    this.lift.addEventListener("click", (e) => {
      const el = (e.target as HTMLElement).closest<HTMLElement>("[data-page]");
      if (el) this.goTo(Number(el.dataset.page));
    });
    this.updateLift(0);
  }

  /** Two arrows that slide the view left or right: a press moves a step, and holding keeps it moving. */
  private renderPan(): void {
    const arrow = (dir: -1 | 1, label: string, path: string): string =>
      `<button type="button" class="pan-btn" data-pan="${dir}" title="${label}" aria-label="${label}"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="${path}"/></svg></button>`;
    this.pan.innerHTML = arrow(-1, "Move the view left", "M14.5 5 7.5 12l7 7") + arrow(1, "Move the view right", "M9.5 5l7 7-7 7");
    const button = (e: Event): HTMLElement | null => (e.target as HTMLElement).closest<HTMLElement>("[data-pan]");
    this.pan.addEventListener("pointerdown", (e) => {
      const b = button(e);
      if (!b || e.button !== 0) return;
      e.preventDefault();
      // Keep hold of the pointer, so letting go anywhere stops the glide.
      b.setPointerCapture(e.pointerId);
      this.hooks.pan(Number(b.dataset.pan) as -1 | 1);
    });
    for (const type of ["pointerup", "pointercancel", "lostpointercapture"]) {
      this.pan.addEventListener(type, () => this.hooks.pan(0));
    }
    // Enter or Space on an arrow moves one step (a mouse press is handled on pointerdown).
    this.pan.addEventListener("click", (e) => {
      const b = button(e);
      if (!b || e.detail !== 0) return;
      this.hooks.pan(Number(b.dataset.pan) as -1 | 1);
      this.hooks.pan(0);
    });
  }

  private updateLift(from: number): void {
    const p = PAGES[this.index];
    this.lift.style.setProperty("--accent", p.accent);
    this.pan.style.setProperty("--accent", p.accent);
    this.lift.querySelectorAll<HTMLElement>(".lift-btn").forEach((b) => {
      const on = Number(b.dataset.page) === this.index;
      b.classList.toggle("on", on);
      if (on) b.setAttribute("aria-current", "page");
      else b.removeAttribute("aria-current");
    });
    const floor = this.lift.querySelector<HTMLElement>(".lift-floor");
    if (floor) floor.textContent = p.floor;
    const arrow = this.lift.querySelector<HTMLElement>(".lift-arrow");
    if (arrow) {
      arrow.textContent = this.index > from ? "▼" : this.index < from ? "▲" : "";
      arrow.classList.remove("moving");
      void arrow.offsetWidth;
      arrow.classList.add("moving");
    }
  }

  private renderPage(): void {
    const p = PAGES[this.index];
    const onCover = this.index === 0;
    document.body.dataset.page = p.id;
    this.cover.classList.toggle("hidden", !onCover || this.collapsed);
    this.card.classList.toggle("hidden", onCover || this.collapsed);
    this.pill.classList.toggle("hidden", !this.collapsed);
    if (onCover) {
      this.applyShrink("cover");
      return;
    }
    const prev = PAGES[this.index - 1];
    const next = PAGES[this.index + 1];
    const dots = PAGES.map(
      (q, k) =>
        `<li><button type="button" data-pc="dot" data-i="${k}" class="${k === this.index ? "on" : ""}" aria-label="${esc(shortTitle(q))}"></button></li>`,
    ).join("");
    this.card.style.setProperty("--accent", p.accent);
    this.card.classList.toggle("shrunk", this.shrunk.page);
    this.card.innerHTML = `
      <div class="pc-head">
        <span class="pc-floor">${esc(p.floor)}</span>
        <div class="pc-titles">
          <div class="pc-kicker">${esc(p.chapter)} · ${esc(p.kicker)}</div>
          <h2>${esc(p.title)}</h2>
        </div>
        ${toggleHtml("page", this.shrunk.page)}
      </div>
      <div class="pc-body">${p.body.map((b) => `<p>${esc(b)}</p>`).join("")}</div>
      ${p.id === "finale" ? `<div class="pc-trace"></div>` : ""}
      <p class="pc-hint"><span aria-hidden="true">👆</span>${esc(p.hint)}</p>
      <nav class="pc-nav" aria-label="Storybook pages">
        <button type="button" class="btn ghost" data-pc="prev">‹ ${esc(prev ? shortTitle(prev) : "")}</button>
        <ol class="pc-dots">${dots}</ol>
        <button type="button" class="btn primary" data-pc="next">${next ? `${esc(shortTitle(next))} ›` : "Back to the cover ↺"}</button>
      </nav>`;
    this.renderTrace();
    if (!this.shrunk.page && !this.collapsed) {
      const r = this.card.getBoundingClientRect();
      this.wide.set(this.wideKey("page"), { right: r.right, height: r.height });
    }
  }

  private renderTrace(): void {
    const box = this.card.querySelector<HTMLElement>(".pc-trace");
    if (!box) return;
    const st = this.trace;
    const chips = CLIENT_PERSONAS.map(
      (c) =>
        `<button type="button" class="chip ${st?.scenario === c.id ? "on" : ""}" data-scenario="${c.id}" style="--c:${c.color}"><b>${esc(c.name.split(" ")[0])}</b><span>${esc(c.request_type)}</span></button>`,
    ).join("");
    const step = st?.step;
    const stepHtml = step
      ? `<div class="trace-step" style="--c:${step.color}">
          <div class="trace-progress">Step ${step.index + 1} of ${step.total}</div>
          <div class="trace-who">${esc(step.who)}</div>
          ${step.side ? `<span class="trace-side" style="--s:${step.side.color}">${esc(step.side.label)}</span>` : ""}
          <div class="trace-route">${step.route.map(esc).join("<i>→</i>")}</div>
          <p class="trace-text">${esc(step.text)}</p>
        </div>`
      : `<div class="trace-step idle"><p class="trace-text">The request is on its way to the house…</p></div>`;
    const play = st?.done ? "↺ Replay" : st?.paused ? "▶ Play" : "⏸ Pause";
    box.innerHTML = `
      <div class="trace-clients" role="group" aria-label="Choose a client">${chips}</div>
      ${stepHtml}
      <div class="trace-controls">
        <button type="button" class="btn ghost" data-trace="${st?.done ? "replay" : "toggle"}">${play}</button>
        <button type="button" class="btn ghost" data-trace="skip" ${st?.done ? "disabled" : ""}>⏭ Next step</button>
        <button type="button" class="btn ghost" data-trace="speed" title="Playback speed">x${st?.speed ?? 1}</button>
      </div>`;
  }

  private onCardClick(e: MouseEvent): void {
    const el = (e.target as HTMLElement).closest<HTMLElement>("[data-pc],[data-trace],[data-scenario]");
    if (!el) return;
    if (el.dataset.scenario) {
      this.hooks.scenario(el.dataset.scenario as ScenarioId);
      return;
    }
    if (el.dataset.trace) {
      this.hooks.trace(el.dataset.trace as "toggle" | "skip" | "speed" | "replay");
      return;
    }
    switch (el.dataset.pc) {
      case "prev":
        this.step(-1);
        break;
      case "next":
        if (this.index === PAGES.length - 1) this.goTo(0);
        else this.step(1);
        break;
      case "dot":
        this.goTo(Number(el.dataset.i));
        break;
    }
  }
}
