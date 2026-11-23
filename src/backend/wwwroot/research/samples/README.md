# Insurance Research — APL insurer product sheets

One product sheet per insurer on Caldova Life Advice's approved product list
(APL), used by the Insurance Research Agent (Ivy) on `/agents/insurance-research`.
The insurers and products are fictional. Each sheet summarises the insurer's
PDS extract in the `life_knowledge` index — read and cite the extract by its
title before relying on any wording.

| File | Insurer | PDS extract to cite | On the APL for |
| --- | --- | --- | --- |
| `harbourline-life.json` | Harbourline Life (`HL-`) | Harbourline Life — Protection PDS extract | Personal and business protection |
| `corella-life.json` | Corella Life (`CR-`) | Corella Life — Protection PDS extract | Personal and business protection |
| `lyrebird-life.json` | Lyrebird Life (`LB-`) | Lyrebird Life — Protection PDS extract | Personal protection |
| `northbank-life.json` | Northbank Life (`NB-`) | Northbank Life — Business Protection PDS extract | Business protection |

Each sheet holds the cover types, key definitions (terminal illness, TPD own and
any occupation, super-linking, trauma, income protection benefit basis and
periods, business cover), features, exclusions, premium styles and the
insurer's quotes for the two advice cases — with the terms quoted for the
client and an illustrative 10-year stepped vs level projection.

The MCP tools in `Mcp/ResearchMcpTools.cs` read them:

1. `research_analysePolicyDocument(insurerOrUrl, caseNumber?)` — one insurer's
   definitions, features and exclusions, plus fit notes for an advice case or
   the wording that applies to a claim.
2. `research_compareInsurers(caseNumber, insurers?)` — definitions first, price
   second: year-1 stepped and level premiums, 10-year totals, the definitions
   that matter and the recommendation rationale for Amelia.
3. `research_generateComparisonWorkbook(caseNumber, insurers?, notesForAdviser?)`
   — the `.xlsx` workbook with charts, written to `../output/` and served from
   `/research/download/{fileName}`.

## Quotes (year 1, stepped, standard rates before underwriting)

| Case | Corella Life | Harbourline Life | Lyrebird Life | Northbank Life |
| --- | --: | --: | --: | --: |
| `REQ-20261103-LIF001` — Priya Natarajan, family protection | **$3,180** (level $4,050) | $3,412 | $3,690 | not quoted (business protection only) |
| `REQ-20261109-LIF003` — George Papadopoulos and Harpreet Kaur, business protection | $9,180 | $9,410 | not quoted (no business protection) | **$8,620** |

Corella split for Priya: inside super life $468 + TPD (any occupation) $390 =
$858; outside super own-occupation TPD extension $222 + trauma $640 + income
protection $1,460 = $2,322 ($193.50 a month). Northbank split: George buy-sell
$4,140, George key person $1,380, Harpreet buy-sell $3,100.

Projections are illustrative only: stepped premiums rise with age and level
premiums start higher and rise more slowly (1% a year in these sheets), with
no CPI increases. They aren't quotes for future years. Claim cases (Grace,
Tom, Robert) use the same sheets for a policy wording lookup — there's nothing
to compare on a claim.
