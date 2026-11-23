namespace CaldovaLife.App.Models;

/// <summary>
/// The three groupings on the practice floor: the two sides of Caldova Life
/// Advice and the agents both sides share. Colours match Caldova House
/// (teal for Protection Advice, gold for Claims &amp; Care, lilac for shared).
/// </summary>
public static class PracticeSides
{
    public const string ProtectionAdvice = "Protection Advice";
    public const string ClaimsAndCare = "Claims & Care";
    public const string Shared = "Shared by both";

    public const string ProtectionAdviceColour = "#14b8a6";
    public const string ClaimsAndCareColour = "#c9a14a";
    public const string SharedColour = "#8b9cf7";

    public static string ColourFor(string side) => side switch
    {
        ProtectionAdvice => ProtectionAdviceColour,
        ClaimsAndCare => ClaimsAndCareColour,
        _ => SharedColour,
    };
}

/// <summary>
/// Lightweight metadata for one of the eight Caldova Life Advice agents. The
/// detailed page content is hand-written in
/// <c>Components/Pages/Agents/&lt;Agent&gt;.razor</c>; this catalog powers
/// navigation (Home, NavMenu), the scenario pages, the onboarding routing
/// cards and the "Ask AI" references surface.
/// </summary>
/// <param name="Id">Route id — the page lives at <c>/agents/{Id}</c>.</param>
/// <param name="Name">Agent name, e.g. "Onboarding Agent".</param>
/// <param name="Department">Department as shown in the UI.</param>
/// <param name="Persona">The human the agent supports, e.g. "Olivia — Onboarding &amp; Requests Officer".</param>
/// <param name="Side">One of <see cref="PracticeSides"/>.</param>
/// <param name="Stages">Lifecycle stages the department covers (Discover → Verify → Advise → Implement → Confirm).</param>
public record AgentDefinition(
    string Id,
    string Name,
    string Department,
    string Persona,
    string Tagline,
    string Purpose,
    IReadOnlyList<string> Responsibilities,
    string Icon,
    string Accent,
    string Side,
    IReadOnlyList<string> Stages);

/// <summary>
/// Static catalog of the eight Caldova Life Advice agents, in request-journey
/// order. Mirrors the agents in <c>src/agent/Agents</c> and the department
/// table in <c>AGENTS.md</c>.
/// </summary>
public static class AgentCatalog
{
    /// <summary>The request lifecycle every case follows.</summary>
    public static IReadOnlyList<string> Lifecycle { get; } =
        ["Discover", "Verify", "Advise", "Implement", "Confirm"];

    public static IReadOnlyList<AgentDefinition> All { get; } = new[]
    {
        new AgentDefinition(
            Id: "onboarding",
            Name: "Onboarding Agent",
            Department: "Onboarding & Requests",
            Persona: "Olivia — Onboarding & Requests Officer",
            Tagline: "Every enquiry, referral and claim, set up right the first time.",
            Purpose: "Reads enquiry and referral emails, fact-find questionnaires and claim notifications, extracts the fact find into the Client Case, runs the in-good-order checklist, sets the priority, flags vulnerable clients and routes the case to the right specialist.",
            Responsibilities: new[]
            {
                "Read enquiries, referral-partner emails, fact finds and claim notifications.",
                "Extract goals, dependants, debts, income, occupation, health basics and existing cover — including cover inside super.",
                "Run the in-good-order checklist: FSG, privacy and health-information consent, ID, authorities, commission consent, support-person authority and claim forms.",
                "Set the priority and flag vulnerable clients for a human touch.",
                "Open the Client Case and route it: Paraplanning for new advice, Claims for claims, Financial Crime for every new client.",
            },
            Icon: "📥",
            Accent: "#0ea5e9",
            Side: PracticeSides.Shared,
            Stages: ["Discover", "Verify"]),

        new AgentDefinition(
            Id: "paraplanning",
            Name: "Paraplanning Agent",
            Department: "Paraplanning",
            Persona: "Penny — Paraplanner",
            Tagline: "Works out what the family actually needs — then drafts the advice for Amelia.",
            Purpose: "Calculates life, TPD, trauma and income protection needs from debts, dependants, income and existing cover, works out how to hold the cover inside or outside super, and drafts the Statement of Advice for Amelia to review and sign.",
            Responsibilities: new[]
            {
                "Run the needs analysis and show the working, step by step.",
                "Work out how to hold the cover: inside super, outside super or super-linked.",
                "Check existing cover before anything is replaced or cancelled.",
                "Draft the Statement of Advice: scope, strategy, best-interests steps, premiums and commission in dollars.",
                "Hand over to Underwriting once the client signs the Authority to Proceed.",
            },
            Icon: "🧮",
            Accent: "#6366f1",
            Side: PracticeSides.ProtectionAdvice,
            Stages: ["Advise"]),

        new AgentDefinition(
            Id: "insurance-research",
            Name: "Insurance Research Agent",
            Department: "Insurance Research",
            Persona: "Ivy — Insurance Research Analyst",
            Tagline: "Cheapest isn't best if the definition won't pay.",
            Purpose: "Compares the insurers on Caldova's approved product list on definitions, features, exclusions and premiums — stepped and level, over time — and builds the comparison workbook and chart for the Statement of Advice.",
            Responsibilities: new[]
            {
                "Read each APL insurer's product sheet and PDS extract.",
                "Compare the definitions that matter for the client's occupation, such as own-occupation TPD.",
                "Compare stepped and level premiums over time.",
                "Build the comparison workbook and premium chart with MCP tools.",
                "Explain the recommended insurer in plain English for the SoA.",
            },
            Icon: "🔎",
            Accent: "#f59e0b",
            Side: PracticeSides.ProtectionAdvice,
            Stages: ["Advise"]),

        new AgentDefinition(
            Id: "financial-crime",
            Name: "Financial Crime Agent",
            Department: "Financial Crime",
            Persona: "Felix — Financial Crime Investigator",
            Tagline: "Protects clients from fraud, scams and identity takeover.",
            Purpose: "Runs KYC for every new client, checks identity and supporting documents for tampering, builds the contact and bank-change timeline, scores risk explainably and recommends holds and call-backs on verified details — a person decides.",
            Responsibilities: new[]
            {
                "Run KYC for every new client, including companies and their beneficial owners.",
                "Check IDs, medical reports, death certificates, payslips, BAS and bank statements for tampering.",
                "Build the timeline of contact changes, new devices and bank-detail changes.",
                "Score risk explainably — never accusing the client.",
                "Recommend holds, call-backs on verified details and referrals to the insurer's fraud team.",
            },
            Icon: "🛡️",
            Accent: "#ef4444",
            Side: PracticeSides.Shared,
            Stages: ["Verify"]),

        new AgentDefinition(
            Id: "underwriting",
            Name: "Underwriting Agent",
            Department: "Underwriting & New Business",
            Persona: "Uma — Underwriting Coordinator",
            Tagline: "Answer every question carefully — it protects your claim later.",
            Purpose: "Prepares the application, walks the client through the duty to take reasonable care, tracks medical and financial evidence with the insurer, explains loadings and exclusions, and gets the policy issued.",
            Responsibilities: new[]
            {
                "Prepare the application from the signed Authority to Proceed.",
                "Explain the duty to take reasonable care before any health questions.",
                "Look up the insurer's underwriting requirements for the client's age and sums insured.",
                "Track medicals, blood tests and financial evidence, and chase the insurer.",
                "Explain loadings and exclusions — the client decides with Amelia.",
            },
            Icon: "🩺",
            Accent: "#10b981",
            Side: PracticeSides.ProtectionAdvice,
            Stages: ["Implement"]),

        new AgentDefinition(
            Id: "claims",
            Name: "Claims Agent",
            Department: "Claims",
            Persona: "Ben — Benefit Claims Advocate",
            Tagline: "Makes a claim as easy as it can be, at the hardest time.",
            Purpose: "Checks the policy, definitions, waiting periods and evidence, builds the claim pack, calculates the expected benefit, raises a Microsoft Teams approval and lodges the claim with the insurer only after a person approves.",
            Responsibilities: new[]
            {
                "Check the policy is in force, the definition, the waiting period and any exclusions.",
                "Build the claim pack and the evidence list.",
                "Calculate the expected benefit and explain the working.",
                "Raise a Teams approval and lodge the claim only after a person approves.",
                "Track the insurer's timeframes under the Life Insurance Code of Practice.",
            },
            Icon: "🤝",
            Accent: "#a855f7",
            Side: PracticeSides.ClaimsAndCare,
            Stages: ["Implement"]),

        new AgentDefinition(
            Id: "client-communications",
            Name: "Client Communications Agent",
            Department: "Client Communications",
            Persona: "Cara — Client Communications Specialist",
            Tagline: "Plain English, every time.",
            Purpose: "Drafts letters, SoA and policy summaries, underwriting and claim updates, SMS and referral-partner updates in plain English — matching tone to illness, injury, scams and bereavement — and takes calls as Cara through Voice Live.",
            Responsibilities: new[]
            {
                "Draft client letters, SoA summaries and policy summaries.",
                "Send underwriting and claim updates at every stage.",
                "Update referral partners with only what the client agreed to share.",
                "Match tone to illness, injury, scams and bereavement.",
                "Take calls as Cara through Voice Live.",
            },
            Icon: "✉️",
            Accent: "#ec4899",
            Side: PracticeSides.Shared,
            Stages: ["Confirm"]),

        new AgentDefinition(
            Id: "team-leader",
            Name: "Team Leader Agent",
            Department: "Team Leader Office",
            Persona: "Theo — Practice Team Leader",
            Tagline: "Nobody waits without knowing why.",
            Purpose: "Watches the advice pipeline, underwriting and claim clocks and complaint (IDR) clocks, queues every human approval, checks advice files against the best interests duty and coordinates the other seven agents in a group chat on any case.",
            Responsibilities: new[]
            {
                "Track the advice pipeline, underwriting and claim clocks, and IDR clocks.",
                "Queue approvals: SoAs, replacements, claim lodgements, blocks and $2m+ second reviews.",
                "Make sure vulnerable clients get a human touch.",
                "Check advice files against the best interests duty.",
                "Coordinate the multi-agent group chat on a case.",
            },
            Icon: "🧭",
            Accent: "#14b8a6",
            Side: PracticeSides.Shared,
            Stages: ["Discover", "Verify", "Advise", "Implement", "Confirm"]),
    };

    public static AgentDefinition? FindById(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    public static AgentDefinition? FindByName(string name) =>
        All.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Agents grouped by side, in journey order.</summary>
    public static IReadOnlyList<AgentDefinition> OnSide(string side) =>
        All.Where(a => a.Side == side).ToList();
}
