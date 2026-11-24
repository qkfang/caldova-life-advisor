using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

namespace LifeAdviceGuru.Agents;

/// <summary>
/// Shared base for every staff-character agent at Life Advice Guru, the life
/// insurance advice practice in "Advice Team in a Day".
///
/// Each agent represents one specialist role from <c>docs/foundry_agents.md</c>
/// and is wired up with the same Foundry tools: Azure AI Search over the
/// <c>life_knowledge</c> index (insurer PDS extracts, Life Advice Guru's guides, advice,
/// underwriting, claims, tax and commission rules, and client case files) and
/// web search for current public context (the Foundry web search tool, which
/// is powered by Web IQ when added directly to an agent and needs no
/// connection). The Insurance Research,
/// Underwriting and Claims agents also get the MCP tool surface served by the
/// backend (server label <c>guru-ops-mcp</c>).
/// Analogous to <c>QuantAgent</c> in
/// https://github.com/qkfang/quant-agent/tree/main/src/quantlib/Agents/QuantAgent.cs.
/// </summary>
public class GuruAgent : BaseAgent
{
    /// <summary>Shared MCP server label for the backend's tool surface.</summary>
    public const string DefaultMcpServerLabel = "guru-ops-mcp";

    /// <summary>Display name of the staff character (e.g. "Olivia").</summary>
    public string Name { get; }

    /// <summary>Staff role (e.g. "Onboarding &amp; Requests Officer").</summary>
    public string Role { get; }

    /// <summary>Department this agent supports at Guru House (e.g. "Onboarding &amp; Requests").</summary>
    public string Department { get; }

    /// <summary>ANSI console colour used when streaming the agent's output.</summary>
    public string ConsoleColor { get; }

    /// <summary>
    /// Human-readable list of Foundry tools wired up for this agent based on
    /// the connections that were configured. Surfaced in the "Agent Prompt
    /// &amp; Tools" sub-tab on each agent page.
    /// </summary>
    public IReadOnlyList<string> ConfiguredTools { get; }

    public GuruAgent(
        AIProjectClient aiProjectClient,
        string agentId,
        string name,
        string role,
        string department,
        string consoleColor,
        string deploymentName,
        string instructions,
        string? searchConnectionId = null,
        string? searchIndexName = null,
        bool webSearch = false,
        ILogger? logger = null,
        string? mcpServerUri = null,
        string? mcpServerLabel = null,
        IList<ResponseTool>? extraTools = null)
        : base(aiProjectClient, agentId, deploymentName, instructions, null,
            agentDef =>
            {
                if (!string.IsNullOrWhiteSpace(searchConnectionId) && !string.IsNullOrWhiteSpace(searchIndexName))
                    agentDef.Tools.Add(new AzureAISearchTool(new AzureAISearchToolOptions([
                        new AzureAISearchToolIndex { ProjectConnectionId = searchConnectionId, IndexName = searchIndexName, QueryType = AzureAISearchQueryType.Simple, TopK = 5 }
                    ])));

                if (webSearch)
                    agentDef.Tools.Add(ResponseTool.CreateWebSearchTool(
                        userLocation: WebSearchToolLocation.CreateApproximateLocation(country: "AU", region: "Victoria", city: "Melbourne")));

                if (!string.IsNullOrWhiteSpace(mcpServerUri))
                {
                    var mcpTool = ResponseTool.CreateMcpTool(
                        serverLabel: mcpServerLabel ?? DefaultMcpServerLabel,
                        serverUri: new Uri(mcpServerUri),
                        toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));
                    agentDef.Tools.Add(mcpTool);
                }

                if (extraTools != null)
                {
                    foreach (var tool in extraTools)
                    {
                        if (tool != null) agentDef.Tools.Add(tool);
                    }
                }
            },
            logger)
    {
        Name = name;
        Role = role;
        Department = department;
        ConsoleColor = consoleColor;

        var tools = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchConnectionId) && !string.IsNullOrWhiteSpace(searchIndexName))
            tools.Add($"Azure AI Search — index '{searchIndexName}'");
        if (webSearch)
            tools.Add("Web search — Web IQ");
        if (!string.IsNullOrWhiteSpace(mcpServerUri))
            tools.Add($"MCP server '{mcpServerLabel ?? DefaultMcpServerLabel}' at {mcpServerUri}");
        if (extraTools != null && extraTools.Count > 0)
            tools.Add($"MCP tool surface ({extraTools.Count} tool{(extraTools.Count == 1 ? "" : "s")})");
        ConfiguredTools = tools;
    }
}
