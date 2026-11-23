using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;
using LifeAdviceGuru.Agents;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// Factory for the Life Advice Guru Foundry agents defined in the
/// <c>agent</c> project. The Blazor app references <c>src/agent</c> as a .NET library and
/// uses this factory to invoke the same agents the CLI host exposes —
/// analogous to how
/// <see href="https://github.com/qkfang/quant-agent/tree/main/src/quantapi">quantapi</see>
/// instantiates <c>QuantAgent</c> subclasses from <c>quantlib</c>.
/// </summary>
public class GuruAgentFactory
{
    /// <summary>
    /// How long to skip Foundry after it couldn't be reached (DNS, network or
    /// sign-in failure), so every page falls straight back to its offline
    /// result instead of waiting for the client's retries on each click.
    /// </summary>
    private static readonly TimeSpan UnreachableWindow = TimeSpan.FromMinutes(5);

    private readonly GuruAgentOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GuruAgentFactory> _logger;
    private readonly Lazy<AIProjectClient> _aiProjectClient;
    private long _unreachableUntilTicks;

    public GuruAgentFactory(GuruAgentOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GuruAgentFactory>();
        _aiProjectClient = new Lazy<AIProjectClient>(CreateAIProjectClient);
    }

    /// <summary>True when the underlying Foundry connection is configured.</summary>
    public bool IsConfigured => _options.IsConfigured;

    /// <summary>
    /// Create an agent for the given role key. Keys match the CLI flags in
    /// <c>src/agent</c> (<c>onboarding</c>, <c>paraplanning</c>,
    /// <c>research</c>, <c>fincrime</c>, <c>underwriting</c>, <c>claims</c>,
    /// <c>communications</c>, <c>communications-voice</c>,
    /// <c>team-leader</c>); the page route ids (e.g. <c>insurance-research</c>,
    /// <c>financial-crime</c>, <c>client-communications</c>) are accepted as
    /// aliases.
    /// </summary>
    /// <param name="role">Kebab-case role key or route id.</param>
    /// <param name="extraTools">Optional Foundry response tools (e.g. an
    /// MCP tool surface) to wire onto the agent in addition to the default
    /// Azure AI Search and Bing tools.</param>
    public GuruAgent Create(string role, IList<ResponseTool>? extraTools = null)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Life Advice Guru agent factory is not configured. Set AZURE_AI_PROJECT_ENDPOINT and " +
                "AZURE_AI_MODEL_DEPLOYMENT_NAME in appsettings.json or environment variables.");
        }

        var unreachableUntil = new DateTimeOffset(Interlocked.Read(ref _unreachableUntilTicks), TimeSpan.Zero);
        if (DateTimeOffset.UtcNow < unreachableUntil)
        {
            throw new InvalidOperationException(
                $"Foundry is unreachable — retrying after {unreachableUntil.ToLocalTime():HH:mm}");
        }

        // A project host that doesn't resolve (e.g. the Azure resources aren't
        // deployed) fails in milliseconds here instead of after the client's
        // sign-in and network retries.
        if (Uri.TryCreate(_options.ProjectEndpoint, UriKind.Absolute, out var endpoint) && HostCannotBeResolved(endpoint))
        {
            MarkUnreachable();
            throw new InvalidOperationException($"Foundry is unreachable — {endpoint.Host} can't be found");
        }

        try
        {
            return CreateAgent(role, extraTools);
        }
        catch (Exception ex) when (IsConnectivityFailure(ex))
        {
            MarkUnreachable();
            throw;
        }
    }

    private void MarkUnreachable()
    {
        Interlocked.Exchange(ref _unreachableUntilTicks, DateTimeOffset.UtcNow.Add(UnreachableWindow).UtcTicks);
        _logger.LogWarning("Foundry project {Endpoint} couldn't be reached; agents use their offline results for the next {Minutes} minutes",
            _options.ProjectEndpoint, UnreachableWindow.TotalMinutes);
    }

    /// <summary>
    /// True when the endpoint's host name definitely doesn't exist. Skipped
    /// when a proxy handles the connection (the proxy resolves names), and any
    /// other DNS problem is left to the client to report.
    /// </summary>
    private static bool HostCannotBeResolved(Uri endpoint)
    {
        try
        {
            var proxy = System.Net.Http.HttpClient.DefaultProxy.GetProxy(endpoint);
            if (proxy is not null && proxy != endpoint) return false;
            System.Net.Dns.GetHostAddresses(endpoint.DnsSafeHost);
            return false;
        }
        catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// True for failures that mean Foundry can't be reached at all (host not
    /// found, network down, sign-in unavailable) rather than a problem with one
    /// request.
    /// </summary>
    private static bool IsConnectivityFailure(Exception ex) => ex switch
    {
        System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException => true,
        AuthenticationFailedException => true,
        AggregateException agg => agg.InnerExceptions.Any(IsConnectivityFailure),
        _ => ex.InnerException is not null && IsConnectivityFailure(ex.InnerException),
    };

    private GuruAgent CreateAgent(string role, IList<ResponseTool>? extraTools)
    {
        var client = _aiProjectClient.Value;
        var deployment = _options.ModelDeploymentName!;
        var search = _options.SearchConnectionId;
        var index = _options.SearchIndexName;
        var bing = _options.BingConnectionId;
        var mcpUri = string.IsNullOrWhiteSpace(_options.AppMcpUrl)
            ? null
            : $"{_options.AppMcpUrl!.TrimEnd('/')}/mcp";

        return role.ToLowerInvariant() switch
        {
            "onboarding" or "intake"
                => new OnboardingAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<OnboardingAgent>()),
            "paraplanning"
                => new ParaplanningAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<ParaplanningAgent>()),
            "research" or "insurance-research"
                => new InsuranceResearchAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<InsuranceResearchAgent>(), mcpUri),
            "fincrime" or "financial-crime"
                => new FinancialCrimeAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<FinancialCrimeAgent>()),
            "underwriting"
                => new UnderwritingAgent(client, deployment, search, index, bing, mcpUri, _loggerFactory.CreateLogger<UnderwritingAgent>()),
            "claims"
                => new ClaimsAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<ClaimsAgent>(), extraTools),
            "communications" or "client-communications"
                => new ClientCommunicationsAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<ClientCommunicationsAgent>()),
            "communications-voice" or "client-communications-voice"
                => new ClientCommunicationsVoiceAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<ClientCommunicationsVoiceAgent>()),
            "team-leader" or "leader"
                => new TeamLeaderAgent(client, deployment, search, index, bing, _loggerFactory.CreateLogger<TeamLeaderAgent>()),
            _ => throw new ArgumentException($"Unknown Life Advice Guru agent role: '{role}'.", nameof(role))
        };
    }

    private AIProjectClient CreateAIProjectClient()
    {
        var credentialOptions = new Azure.Identity.DefaultAzureCredentialOptions
        {
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeSharedTokenCacheCredential = true
        };
        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            credentialOptions.TenantId = _options.TenantId;
        }
        var credential = new Azure.Identity.DefaultAzureCredential(credentialOptions);
        return new AIProjectClient(new Uri(_options.ProjectEndpoint!), credential);
    }
}
