# Insurance Research — generated comparison workbooks

This folder holds the Excel workbooks built at runtime by the
`research_generateComparisonWorkbook` MCP tool (see `Mcp/ResearchMcpTools.cs`)
and by `POST /research/workbook` on the Insurance Research Agent page. Each
workbook has four sheets — Summary (the recommendation rationale for Amelia and
the sources), Comparison (premiums, cash flow, 10-year totals, cover breakdown
and a premium chart), Definitions (each insurer's result and wording) and
Projection (10 years of stepped vs level premiums, with a chart).

Files are served from `/research/download/{fileName}` by the download endpoint
in `Program.cs` (`.xlsx` only, path-traversal guarded). Generated files aren't
tracked (see `.gitignore`); only the newest workbooks are kept.
