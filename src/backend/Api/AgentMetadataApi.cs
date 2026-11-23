using CaldovaLife.Agents;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

/// <summary>
/// Lightweight read-only endpoint that powers the "Agent Prompt &amp; Tools"
/// sub-tab on every agent page. Returns the system instructions the
/// Foundry agent is created with, plus the list of tools the agent has
/// access to in the current environment. Does not require Foundry to be
/// configured — instructions are static <c>const</c> strings on each
/// agent class.
/// </summary>
public static class AgentMetadataApi
{
    private record RoleMetadata(
        string RoleId,
        string Name,
        string Role,
        string Department,
        string AgentId,
        string Instructions,
        string[] DefaultTools,
        string[] McpTools);

    // Shared base tools every Caldova agent gets. Surfaced both in the static
    // view (defaultTools) and the runtime view (configuredTools) when the
    // corresponding Foundry connection ids are present in configuration.
    private const string SearchToolDescription =
        "Azure AI Search — Caldova life knowledge base (life_knowledge: insurer PDS extracts, Caldova's FSG, APL and guides, advice, underwriting, claims, tax and commission rules, client case files)";
    private const string BingToolDescription =
        "Bing Grounding — open-web grounding for current public context (never client details)";

    private static readonly string[] DefaultTools =
    [
        SearchToolDescription,
        BingToolDescription
    ];

    // Per-role MCP tool surfaces served by this app at /mcp (server label
    // caldova-ops-mcp). Each entry is the label shown in the "Tools
    // available" list. They mirror the [McpServerTool] methods in
    // src/backend/Mcp/*.cs and are appended to the runtime configuredTools
    // list whenever APP_MCP_URL is set.
    private static readonly string[] ResearchMcpTools =
    [
        "MCP · research_analysePolicyDocument — read an APL insurer's product sheet or PDS extract (definitions, features, exclusions)",
        "MCP · research_compareInsurers — compare the APL insurers for the client's cover: stepped vs level premiums, definitions and features",
        "MCP · research_generateComparisonWorkbook — write the Excel comparison workbook with a premium chart and return its download link"
    ];

    private static readonly string[] UnderwritingMcpTools =
    [
        "MCP · underwriting_lookupInsurer — APL insurer directory: contacts, service levels and underwriting thresholds",
        "MCP · underwriting_checkRequirements — medical and financial evidence the insurer needs for the client's age and sums insured",
        "MCP · underwriting_generateApplicationSummaryPdf — generate the application and disclosure summary PDF and return its download link"
    ];

    private static readonly string[] ClaimsMcpTools =
    [
        "MCP · claims_validateClaimant — confirm the claimant is the policy owner or nominated beneficiary, and their payment details",
        "MCP · claims_checkPolicy — policy in force, definition, waiting period, exclusions and the evidence needed",
        "MCP · claims_calculateBenefit — expected benefit with the working shown",
        "MCP · claims_checkApprovalRules — which human approvals the claim needs before lodgement",
        "MCP · claims_requestApproval — raise the approval and post the Adaptive Card to Microsoft Teams (uses teams_sendApprovalCard)",
        "MCP · claims_getApprovalStatus — poll the approval state",
        "MCP · claims_lodgeClaim — lodge the claim with the insurer (refuses unless a person has approved)",
        "MCP · teams_sendApprovalCard — deliver the claim-lodgement approval card to the approver in Microsoft Teams"
    ];

    private static readonly string[] NoMcpTools = Array.Empty<string>();

    // Keyed by the agent page route id (/agents/{id}); the voice agent has
    // its own key for Cara's Voice chat tab.
    private static readonly Dictionary<string, RoleMetadata> _catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["onboarding"] = new(
            "onboarding", "Olivia", "Onboarding & Requests Officer", "Onboarding & Requests",
            "onboarding-agent", OnboardingAgent.Instructions, DefaultTools, NoMcpTools),
        ["paraplanning"] = new(
            "paraplanning", "Penny", "Paraplanner", "Paraplanning",
            "paraplanning-agent", ParaplanningAgent.Instructions, DefaultTools, NoMcpTools),
        ["insurance-research"] = new(
            "insurance-research", "Ivy", "Insurance Research Analyst", "Insurance Research",
            "insurance-research-agent", InsuranceResearchAgent.Instructions, DefaultTools.Concat(ResearchMcpTools).ToArray(), ResearchMcpTools),
        ["financial-crime"] = new(
            "financial-crime", "Felix", "Financial Crime Investigator", "Financial Crime",
            "financial-crime-agent", FinancialCrimeAgent.Instructions, DefaultTools, NoMcpTools),
        ["underwriting"] = new(
            "underwriting", "Uma", "Underwriting Coordinator", "Underwriting & New Business",
            "underwriting-agent", UnderwritingAgent.Instructions, DefaultTools.Concat(UnderwritingMcpTools).ToArray(), UnderwritingMcpTools),
        ["claims"] = new(
            "claims", "Ben", "Benefit Claims Advocate", "Claims",
            "claims-agent", ClaimsAgent.Instructions, DefaultTools.Concat(ClaimsMcpTools).ToArray(), ClaimsMcpTools),
        ["client-communications"] = new(
            "client-communications", "Cara", "Client Communications Specialist", "Client Communications",
            "client-communications-agent", ClientCommunicationsAgent.Instructions, DefaultTools, NoMcpTools),
        ["client-communications-voice"] = new(
            "client-communications-voice", "Cara", "Client Communications Specialist (Voice)", "Client Communications",
            "client-communications-voice-agent", ClientCommunicationsVoiceAgent.Instructions, DefaultTools, NoMcpTools),
        ["team-leader"] = new(
            "team-leader", "Theo", "Practice Team Leader", "Team Leader Office",
            "team-leader-agent", TeamLeaderAgent.Instructions, DefaultTools, NoMcpTools),
    };

    public static void MapAgentMetadataEndpoints(this WebApplication app, CaldovaAgentOptions options)
    {
        app.MapGet("/agents/{role}/metadata", (string role) =>
        {
            if (!_catalog.TryGetValue(role, out var m))
                return Results.NotFound(new { error = $"Unknown agent role '{role}'." });

            // Live runtime view of which tools the agent has wired up given
            // the current configuration. When APP_MCP_URL is set, the role's
            // MCP tool surface is also appended.
            var configuredTools = new List<string>();
            if (!string.IsNullOrWhiteSpace(options.SearchConnectionId) && !string.IsNullOrWhiteSpace(options.SearchIndexName))
                configuredTools.Add($"Azure AI Search — index '{options.SearchIndexName}'");
            if (!string.IsNullOrWhiteSpace(options.BingConnectionId))
                configuredTools.Add("Bing Grounding (web)");
            if (!string.IsNullOrWhiteSpace(options.AppMcpUrl) && m.McpTools.Length > 0)
                configuredTools.AddRange(m.McpTools);

            return Results.Ok(new
            {
                roleId = m.RoleId,
                name = m.Name,
                role = m.Role,
                department = m.Department,
                agentId = m.AgentId,
                modelDeployment = options.ModelDeploymentName ?? string.Empty,
                instructions = m.Instructions,
                defaultTools = m.DefaultTools,
                configuredTools = configuredTools,
                mcpTools = m.McpTools,
                mcpConfigured = !string.IsNullOrWhiteSpace(options.AppMcpUrl) && m.McpTools.Length > 0,
                isConfigured = options.IsConfigured
            });
        });
    }
}
