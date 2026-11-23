using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LifeAdviceGuru.Agents;

// Advice Team in a Day — Life Advice Guru — .NET 10 Foundry agents
//
// Each staff character at Life Advice Guru, a Melbourne life insurance
// advice practice, is supported by a specialised Azure AI Foundry declarative
// agent. This console host lets you drive any one agent from the command line
// to try a prompt or test its instructions.
//
// Usage:
//   dotnet run -- --onboarding      "Referral from Hannah Lewis (Yarra Home Loans): Priya Natarajan, 34, physiotherapist, new $680,000 mortgage, old Kestrel Retail Super account with no contributions since 30 Jul 2025"
//   dotnet run -- --paraplanning    "Needs analysis for Priya: $680,000 mortgage, two children, $105,000 salary, $45,000 savings, $210,000 death and TPD cover in super"
//   dotnet run -- --research        "Compare Corella, Harbourline and Lyrebird for a 34-year-old non-smoking physio: life $750,000, TPD $600,000, trauma $150,000, IP $6,125 a month"
//   dotnet run -- --fincrime        "Urgent $200,000 trauma claim to a new account in another name, 93 hours after a phone change of mobile and email; client on a travel notice"
//   dotnet run -- --underwriting    "What evidence will Northbank Life need for George, 52: buy-sell life and TPD $1.2m plus key person $400,000?"
//   dotnet run -- --claims          "Tom Bradley's income protection claim: insured $7,000 a month, net business income $96,000 in the 12 months before his injury"
//   dotnet run -- --communications  "Draft a gentle letter to Robert Chen confirming the $1,000,000 death benefit for his wife Mei"
//   dotnet run -- --team-leader     "Summarise today's approvals, claim clocks and IDR clocks"
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["AZURE_AI_PROJECT_ENDPOINT"];
if (string.IsNullOrWhiteSpace(endpoint))
    throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT is not set.");

var deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
if (string.IsNullOrWhiteSpace(deploymentName))
    throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set.");

var tenantId = config["AZURE_TENANT_ID"];

var credentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeVisualStudioCredential = true,
    ExcludeVisualStudioCodeCredential = true,
    ExcludeSharedTokenCacheCredential = true
};
if (!string.IsNullOrWhiteSpace(tenantId))
{
    credentialOptions.TenantId = tenantId;
}
var credential = new DefaultAzureCredential(credentialOptions);
AIProjectClient aiProjectClient = new(new Uri(endpoint), credential);

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

var searchConnectionId = config["AZURE_AI_SEARCH_CONNECTION_ID"];
var searchIndexName = config["AZURE_AI_SEARCH_INDEX_NAME"];
var bingConnectionId = config["AZURE_BING_CONNECTION_ID"];

string? flag = args.Length > 0 ? args[0] : null;
string request = args.Length > 1
    ? string.Join(" ", args.Skip(1))
    : "Walk me through how you would handle this request.";

GuruAgent? agent = flag switch
{
    "--onboarding"
        => new OnboardingAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<OnboardingAgent>()),
    "--paraplanning"
        => new ParaplanningAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<ParaplanningAgent>()),
    "--research"
        => new InsuranceResearchAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<InsuranceResearchAgent>()),
    "--fincrime"
        => new FinancialCrimeAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<FinancialCrimeAgent>()),
    "--underwriting"
        => new UnderwritingAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, mcpServerUri: null, logger: loggerFactory.CreateLogger<UnderwritingAgent>()),
    "--claims"
        => new ClaimsAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<ClaimsAgent>()),
    "--communications"
        => new ClientCommunicationsAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<ClientCommunicationsAgent>()),
    "--team-leader"
        => new TeamLeaderAgent(aiProjectClient, deploymentName, searchConnectionId, searchIndexName, bingConnectionId, loggerFactory.CreateLogger<TeamLeaderAgent>()),
    _ => null
};

if (agent is null)
{
    PrintUsage();
    return;
}

Console.WriteLine($"{agent.ConsoleColor}--- {agent.Name} ({agent.Role}) — {agent.Department} ---\u001b[0m");
Console.WriteLine($"Request: {request}");
Console.WriteLine();

await foreach (var chunk in agent.RunStreamingAsync(request))
{
    Console.Write(chunk);
}
Console.WriteLine();

static void PrintUsage()
{
    Console.WriteLine("Advice Team in a Day — Life Advice Guru — Foundry agents");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- --onboarding      \"<message>\"   Olivia — Onboarding & Requests");
    Console.WriteLine("  dotnet run -- --paraplanning    \"<message>\"   Penny  — Paraplanning");
    Console.WriteLine("  dotnet run -- --research        \"<message>\"   Ivy    — Insurance Research");
    Console.WriteLine("  dotnet run -- --fincrime        \"<message>\"   Felix  — Financial Crime");
    Console.WriteLine("  dotnet run -- --underwriting    \"<message>\"   Uma    — Underwriting & New Business");
    Console.WriteLine("  dotnet run -- --claims          \"<message>\"   Ben    — Claims");
    Console.WriteLine("  dotnet run -- --communications  \"<message>\"   Cara   — Client Communications");
    Console.WriteLine("  dotnet run -- --team-leader     \"<message>\"   Theo   — Team Leader Office");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run -- --claims \"Tom Bradley's income protection claim: insured $7,000 a month, net business income $96,000 in the 12 months before his injury\"");
    Console.WriteLine();
    Console.WriteLine("Agents gather facts, check, calculate and draft. Amelia gives the advice, and people approve every decision that matters.");
}