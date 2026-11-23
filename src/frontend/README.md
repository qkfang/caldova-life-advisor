# Advice Team in a Day — Frontend (`src/frontend`)

The frontend for **Life Advice Guru**, a fictional Melbourne life insurance advice practice.
It is a voxel storybook, **Guru House**, that you read from the top of the house to the
bottom. Guru House is a bank built in 1889 on a Melbourne city corner, with a super modern,
futuristic office inside. Life Advice Guru is an advice practice for the agentic era with two sides,
Protection Advice and Claims & Care:

- **Level 3 · The Idea** is business owner Maya Ashworth's office under the copper dome, with her
  idea floating above the table as a voxel brain (one half for each side).
- **Level 2 · The People** holds two small teams: Claims & Care on the left and Protection Advice
  on the right, with Olivia at the front door and Theo leading both from the middle.
- **Level 1 · The Agents** is one group of Microsoft Foundry agents in the old banking chamber,
  with a bay under every desk, in three zones: dedicated to Claims & Care, shared by both sides,
  and dedicated to Protection Advice. Each agent and each of its two specialised assistants is
  its own robot (25 roles, with Cara's voice agent).
- **B · The Engine Room** is the old bank vault. Microsoft IQ (Work IQ, Fabric IQ, Foundry IQ and
  Web IQ) stands on a raised deck behind Microsoft Foundry, with the Azure services around it.

The front of the house is cut away, dollhouse style, so every floor reads from the street.
People and agents walk about their floors on errands, with a speech bubble saying what each is
doing. The house is set back behind its own forecourt. Out front, clients arrive by tram at the
tram stop on its own platform and sign at the front desk under the cast-iron verandah, where
Amelia, Life Advice Guru's adviser, links them with the agents that get them started. Up on the
roof Life Advice Guru throws a party for its clients under a big LIFE ADVICE GURU sign, and the
guests come and go by tram and the glass lift on the front-left corner. Each chapter lifts the
levels above it out of the way. Clicking anything follows its thread up or down the house. The
finale starts at the front desk with the client signing, then pulls the house apart and traces
one of the five scenarios from top to bottom. The story, the cast, the controls and every page
are in [docs/storybook.md](../../docs/storybook.md).

## Tech stack

- [Babylon.js](https://www.babylonjs.com/) (`@babylonjs/core`) for 3D rendering
- [Vite](https://vitejs.dev/) + TypeScript for the dev server and build
- A plain-CSS overlay for the storybook card, lift panel, view arrows, inspector and trace panel.
  Each panel has a − / + that shrinks it to a bar, and the − / + beside the brand bar shrinks or
  expands them all.

The house, the city block, the tram, characters and props are built at runtime from
`MeshBuilder` boxes (plus a few lathes and cylinders for the dome and columns), so there are no
model files to load. Characters walk with simple kinematic steering; no physics engine is shipped
to the browser.

## Running locally

```bash
cd src/frontend
npm install
npm run dev
```

Then open <http://localhost:5173>. Each page has a deep link:
`/?page=idea|people|agents|engine|finale`.

To produce a production build (type-check + bundle):

```bash
npm run build
npm run preview
```

The **Open the agent page ↗** links point at the companion Blazor site in `src/backend`
(`/agents/{route id}`) and open in a new tab. They sit at the bottom of the people cards and under
the name on the agent, assistant and voice-agent cards. Amelia's card links to the family
protection scenario (`/scenarios/family-protection`). Their base URL comes from
`VITE_APP_BASE_URL`, set in a local `.env` or `.env.local` (both git-ignored), or by
`.github/workflows/21_deploy_frontend.yml` for deployments. Without it, the cards leave those
links out.

In production, `pm2 serve --spa` answers every path with `index.html`, so old `/v2` links still
open the storybook, and the page tidies the address back to `/`.

## The team

| Staff | Side | Role | Department | Agent page |
|---|---|---|---|---|
| Olivia | Shared (the front door) | Onboarding & Requests Officer | Onboarding & Requests | `/agents/onboarding` |
| Penny | Protection Advice | Paraplanner | Paraplanning | `/agents/paraplanning` |
| Ivy | Protection Advice | Insurance Research Analyst | Insurance Research | `/agents/insurance-research` |
| Felix | Claims & Care team, shared agent | Financial Crime Investigator | Financial Crime | `/agents/financial-crime` |
| Uma | Protection Advice | Underwriting Coordinator | Underwriting & New Business | `/agents/underwriting` |
| Ben | Claims & Care | Benefit Claims Advocate | Claims | `/agents/claims` |
| Cara | Claims & Care team, shared agent | Client Communications Specialist | Client Communications | `/agents/client-communications` |
| Theo | Shared (leads both teams) | Practice Team Leader | Team Leader Office | `/agents/team-leader` |

Each person's agent works in a bay on Level 1, directly under their desk, with two specialised
assistants named in `src/agentStages.ts`. Some agents are dedicated to one side and some are
shared by both; [docs/storybook.md](../../docs/storybook.md) has the full split.

## Scenarios

Pick a client's chip on the finale page to trace one of the five client scenarios (dates are
November 2026):

| # | Client | Request | Case | Who works the case | Outcome |
|---|---|---|---|---|---|
| 1 | Priya Natarajan | New protection advice — life $750,000, TPD $600,000, trauma $150,000 and income protection | `REQ-20261103-LIF001` | Olivia → Felix → Penny → Ivy → Penny → Uma → Cara | Completed — covered from 18 Nov 2026, with a back exclusion on income protection |
| 2 | Grace Williams | Trauma claim and bank change — $200,000, made in her name | `REQ-20261106-LIF002` | Olivia → Ben → Felix → Theo → Ben → Cara | Blocked — no claim lodged, $200,000 protected |
| 3 | George Papadopoulos | Business protection advice — buy-sell and key person cover, $2,800,000 in all | `REQ-20261109-LIF003` | Olivia → Felix → Penny → Ivy → Theo → Uma → Cara | Completed — in force from 20 Nov 2026, with a 25% loading on George's cover |
| 4 | Tom Bradley | Income protection claim — $7,000 a month insured | `REQ-20261111-LIF004` | Olivia → Ben → Felix → Theo → Ben → Cara | Partially approved — accepted at $5,600 a month |
| 5 | Robert Chen | Death claim — his wife Mei's $1,000,000 life cover | `REQ-20261113-LIF005` | Olivia → Theo → Felix → Ben → Cara | Completed — $1,000,000 paid to Robert on 23 Nov 2026 |

The client arrives by tram, or rides the glass lift down from the party on the roof (Priya, and
George with Harpreet), and signs at the front desk. The request then sets off: each step
starts with a person on Level 2, dips down to their agent on Level 1 and the engine-room
component that agent leans on, then comes back up for the human decision. The result rises to
the brain on Level 3, where Maya's idea is measured.

### Editing a scenario

Each scenario is defined in `src/scenarios/<id>.json` (`family`, `fraud`, `business`, `income`
and `bereavement`) and mirrors `docs/scenario-N-*.md`. Case numbers match the cases seeded by
the backend `CaseStore`.

- `beats` — the narration for every step, in order. The `lodge-at-front-desk` beat (`at` is
  `client`) opens the trace at the front desk; every staff beat (`at` is the person's role)
  becomes a stop, and one person's consecutive beats merge into one stop. A `claims-confirm` beat
  is a short handover stop.
- `result` — `icon`, `headline` and `summary` make the last stop; `status` and `figure` record
  the outcome.
- `caseId`, `amount`, `outcomeAmount` and `outcome` — the case facts.

Personas (names, roles, situations and palettes) live in `src/personaData.ts`. Content only the
storybook needs (the sides, the agents' roles and props, the front desk, Amelia, the arrivals
and visitors, the party, Microsoft IQ and the Azure components) lives in `src/v2/houseData.ts`.

## File map

```
src/frontend
├── index.html                  # Page shell: canvas, brand bar with the shrink-all button, storybook HUD, lift panel and view arrows
├── package.json                # Vite + Babylon dependencies
├── tsconfig.json
├── vite.config.ts              # Single page; dev server on port 5173
├── public/                     # favicon.svg / favicon.png (Life Advice Guru "G")
└── src
    ├── personaData.ts          # Single source of truth for staff and client personas
    ├── agentStages.ts          # Each agent's specialised assistants, and the backend base URL
    ├── characterPalettes.ts    # Per-persona colour palettes and looks
    ├── characterFigure.ts      # SVG figure for the cards
    ├── voxelCharacter.ts       # Box-built voxel humanoid + walk animation
    ├── cameraDirector.ts       # Eased camera moves for page turns and selections
    ├── scenarios/              # Scenario JSON (family, fraud, business, income, bereavement)
    └── v2/                     # The Guru House storybook (docs/storybook.md)
        ├── main.ts             # Bootstrap, camera, picking, selection threads, page wiring
        ├── storyData.ts        # Storybook pages: text, camera shots, stack modes
        ├── houseData.ts        # The two sides, the business owner, the idea, agent sides and roles, handoffs, the front desk, Amelia, arrivals and visitors, the party, Microsoft IQ and Azure components
        ├── storybook.ts        # Cover, page card, lift panel, view arrows, inspector, trace panel, keyboard, and each panel's − / + shrink state
        ├── inspector.ts        # Card content for the owner, brain, people, agents, roles, components, the front desk, Amelia, visitors, the party and the glass lift
        ├── levelStack.ts       # House / focus / exploded arrangements of the levels
        ├── threads.ts          # Lines between levels: the finale's tree, the selection's beams, and Amelia's live links
        ├── callouts.ts         # Labels on the far ends of the selection's beams
        ├── requestTrace.ts     # Finale: a scenario's request, from the front desk through the house
        ├── wander.ts           # Walking: errands for people, Maya and the agents' roles, with speech bubbles
        ├── street.ts           # The city block: forecourt, street, tram stop, laneway, café, park, towers, trees, lamps, clouds, drone
        ├── frontDesk.ts        # The front desk: verandah, greeter, Amelia, the tram and the visitors who sign there (by tram, or down the glass lift in the finale)
        ├── lift.ts             # The glass lift on the front-left corner: frame, car, doors, ROOF PARTY sign and its rides
        ├── party.ts            # The roof: the clients' party, the LIFE ADVICE GURU sign, DJ, toasts and fireworks
        ├── partyTrips.ts       # Party guests who come and go by tram and the glass lift
        ├── heritage.ts         # Heritage pieces: brick, render, bluestone, arched windows, cornices, balustrades, cast-iron lace
        ├── shell.ts            # Each floor's slab, brick walls, facades, rails, brass lift doors and level tag
        ├── kit.ts              # Voxel kit: materials, textures, blocks, labels, batching
        ├── layout.ts           # World layout: floor heights, footprint, desk/bay stations, wings, zones, dome, forecourt, street, tram stop, glass lift and front desk
        ├── types.ts
        ├── v2.css              # Storybook HUD styling
        └── levels/             # ideaLevel (+ dome and roof), peopleLevel, agentsLevel, engineLevel
```
