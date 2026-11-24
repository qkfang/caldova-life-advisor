namespace LifeAdviceGuru.App.Services;

/// <summary>
/// Configuration values needed to construct the Life Advice Guru Foundry agents
/// from <c>src/agent</c>. These keys are read from the app's
/// <c>appsettings.json</c> / environment variables and mirror the keys used
/// by the standalone agent CLI.
/// </summary>
public class GuruAgentOptions
{
    public string? ProjectEndpoint { get; set; }
    public string? ModelDeploymentName { get; set; }
    public string? TenantId { get; set; }
    public string? SearchConnectionId { get; set; }
    public string? SearchIndexName { get; set; }

    /// <summary>
    /// Adds the Foundry web search tool to every agent. Added directly to an
    /// agent, web search is powered by Web IQ and needs no project connection.
    /// On unless <c>AZURE_AI_WEB_SEARCH_ENABLED</c> is <c>false</c>.
    /// </summary>
    public bool WebSearchEnabled { get; set; } = true;

    /// <summary>
    /// Base URL of the in-process MCP server exposed by this app
    /// (e.g. a dev tunnel URL). When set, agents that opt in register an MCP
    /// tool pointing at <c>{AppMcpUrl}/mcp</c> (server label
    /// <c>guru-ops-mcp</c>) — the Insurance Research Agent uses the
    /// <c>research_*</c> tools, the Underwriting Agent the
    /// <c>underwriting_*</c> tools and the Claims Agent the <c>claims_*</c>
    /// tools plus <c>teams_sendApprovalCard</c>.
    /// </summary>
    public string? AppMcpUrl { get; set; }

    /// <summary>
    /// Returns true when the minimum configuration required to instantiate a
    /// Foundry agent is present (project endpoint + model deployment name).
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectEndpoint) &&
        !string.IsNullOrWhiteSpace(ModelDeploymentName);
}
