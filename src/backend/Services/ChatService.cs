using System.Text;
using System.Text.Json;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// Backs the "Ask AI" assistant on the Account page (<c>/account</c>): general
/// information about Life Advice Guru and how life insurance advice and
/// claims work — never personal advice. When <c>FoundryAgent:EndpointUrl</c> is
/// configured the question is sent to that agent endpoint with
/// <see cref="SystemPrompt"/>; otherwise an offline answer is returned so the
/// assistant still works end to end.
/// Ported from forex-trading-agent/src/research-analytics/Services/ChatService.cs.
/// </summary>
public class ChatService
{
    /// <summary>Instructions for the client-facing assistant.</summary>
    public const string SystemPrompt = """
        You are the Ask AI assistant for Life Advice Guru, a Melbourne life
        insurance advice practice ("Advice Team in a Day"). You answer clients'
        general questions about life cover, TPD, trauma and income protection,
        insurance inside super, how Life Advice Guru's advice process works, how claims
        work, commission and the cooling-off period — in plain Australian English.

        - General information only. Never give personal advice or say what cover
          someone should have; that is Amelia's job, Life Advice Guru's senior risk adviser.
        - Explain how the practice works: Olivia's Onboarding Agent opens the
          Client Case, Penny's Paraplanning Agent works out needs, Ivy's Insurance
          Research Agent compares insurers, Felix's Financial Crime Agent checks
          identity, Uma's Underwriting Agent handles applications, Ben's Claims
          Agent supports claims, Cara's Client Communications Agent keeps people
          informed and Theo's Team Leader Agent oversees everything. People approve
          every decision that matters.
        - Ground rules and figures in the life_knowledge index and cite document
          titles. Never invent a rule or a figure.
        - Never ask for or repeat a TFN, bank details or health details in chat.
        - Be warm and careful with anyone who mentions illness, injury, a scam or a
          death in the family, and offer to put them in touch with Amelia.
        """;

    private readonly HttpClient _http;
    private readonly string? _agentEndpoint;
    private readonly ILogger<ChatService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ChatService(HttpClient http, IConfiguration config, ILogger<ChatService> logger)
    {
        _http = http;
        _agentEndpoint = config["FoundryAgent:EndpointUrl"]?.TrimEnd('/');
        _logger = logger;
    }

    public Task<string> SendMessageAsync(string userMessage, List<ChatTurn>? history = null) =>
        SendMessageWithOptionsAsync(userMessage, 0.7, history);

    public async Task<string> SendMessageWithOptionsAsync(string userMessage, double temperature, List<ChatTurn>? history = null)
    {
        if (string.IsNullOrWhiteSpace(_agentEndpoint))
        {
            return BuildOfflineReply(userMessage);
        }

        var payload = new { message = userMessage, temperature, instructions = SystemPrompt };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        _logger.LogInformation("Ask AI request ({Length} characters)", userMessage?.Length ?? 0);

        try
        {
            var response = await _http.PostAsync($"{_agentEndpoint}/insight", content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgentInsightResponse>(body, _jsonOptions);
            return result?.Response ?? "No response from the assistant.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get a response from the Ask AI agent");
            return "Sorry, the Life Advice Guru assistant is unavailable right now. Please try again later, or call us and Amelia will help.";
        }
    }

    private const string GeneralOnly =
        "\n\nThis is general information only. For advice about your own situation, Amelia, our senior risk adviser, will talk it through with you.";

    /// <summary>
    /// Offline answer used when no agent endpoint is configured: plain-English
    /// general information on the most common topics, built only from verified
    /// rules, so the assistant still works without Foundry.
    /// </summary>
    private static string BuildOfflineReply(string userMessage)
    {
        var trimmed = (userMessage ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Ask me about life cover, income protection, insurance inside super, how a claim works, commission or the cooling-off period.";
        }

        var q = trimmed.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(w => q.Contains(w));

        if (Has("scam", "fraud", "suspicious", "hacked", "identity", "didn't ask", "did not ask", "changed my", "unauthorised", "unauthorized"))
        {
            return "If something about your policy looks unusual — like a request to pay a benefit into a new account, or contact details that changed without you asking — Felix's Financial Crime Agent checks it before anything moves, and we only ever call you back on the contact details we already hold. If you're worried right now, call us and we'll put a hold on changes while we check." + GeneralOnly;
        }

        if (Has("died", "death", "passed away", "bereave", "funeral"))
        {
            return "We're so sorry if you've lost someone. When a client dies, Ben's Claims Agent prepares the claim so the family has as little to do as possible, and you'll have one named contact. Life cover paid outside super is generally not taxable. Under the Life Insurance Code of Practice, insurers decide lump-sum claims within 6 months and give you an update at least every 20 business days — most death claims are much quicker. A person at Life Advice Guru approves every death claim before it's lodged." + GeneralOnly;
        }

        if (Has("income protection", "can't work", "cannot work", "injur", "sick leave", "waiting period"))
        {
            return "Income protection pays a monthly benefit if illness or injury stops you working, after a waiting period. Policies issued from 1 October 2021 base the benefit on your earnings when you claim, and all benefits combined are capped at 90% of earnings for the first six months of a claim and 70% after that. Benefits that replace income are taxable, and premiums for cover held outside super that protects your income are generally tax-deductible. Under the Life Insurance Code of Practice, insurers decide income-related claims within 2 months of receiving the claim (or of the end of the waiting period, if later)." + GeneralOnly;
        }

        if (Has("claim"))
        {
            return "Here's how a claim works at Life Advice Guru: Olivia's Onboarding Agent opens your Client Case, Ben's Claims Agent checks your policy and the evidence, builds the claim pack and works out the expected benefit, and a person approves before anything is lodged with your insurer. Under the Life Insurance Code of Practice, insurers decide income-related claims within 2 months and lump-sum claims (death, TPD, trauma) within 6 months, and update you at least every 20 business days. You can bring a support person — insurers must recognise them." + GeneralOnly;
        }

        if (Has("super", "inactive", "lapse"))
        {
            return "Many people hold death and TPD cover through super. Super funds can't offer new trauma or own-occupation TPD cover, so those are held outside super. Under Protecting Your Super, if a super account receives no contributions or other amounts for 16 months in a row, the fund must stop its insurance unless you tell it in writing that you want to keep it — worth checking before you change jobs or consolidate accounts. Never cancel existing cover until new cover is in place." + GeneralOnly;
        }

        if (Has("commission", "fee", "paid", "cost of advice"))
        {
            return "Life Advice Guru is paid by commission from the insurer when you take out cover we recommend. Under the Life Insurance Framework, upfront commission is capped at 60% of the first year's premium and 20% of each later year's (excluding GST), and for cover issued from 9 July 2025 the law requires your informed consent to it. Your Statement of Advice always shows the commission in dollars." + GeneralOnly;
        }

        if (Has("cooling", "change my mind", "cancel"))
        {
            return "Most life insurance policies have a 14-day cooling-off period. It starts when you receive confirmation of the policy or at the end of the fifth business day after it's issued, whichever is earlier, and it isn't available once you've made a claim. Please don't cancel existing cover before talking to us — Amelia will check what you'd lose first." + GeneralOnly;
        }

        if (Has("underwrit", "medical", "health question", "disclose", "disclosure"))
        {
            return "When you apply, the insurer asks about your health, work and lifestyle, and you have a duty to take reasonable care to answer accurately. Uma's Underwriting Agent walks you through the questions before you start. The insurer may accept you at standard rates, or with a loading or an exclusion — you decide with Amelia whether to accept. Since 8 October 2026, insurers can't ask for or use protected genetic information when underwriting life cover." + GeneralOnly;
        }

        if (Has("business", "key person", "buy-sell", "buy sell", "partner"))
        {
            return "Business protection helps owners plan for the worst: buy-sell cover funds a buy-out if an owner dies or is seriously disabled, and key person cover helps the business through the loss of someone vital. Ownership and tax treatment matter, so we work with your accountant, and your lawyer drafts the buy-sell agreement. Total cover of $2,000,000 or more on one case gets a second review at Life Advice Guru." + GeneralOnly;
        }

        return
            "Thanks for your question. I'm the Life Advice Guru assistant (offline mode), so I can share general information about life cover, TPD, trauma and income protection, insurance inside super, how claims work, commission and the cooling-off period.\n\n" +
            "Behind the scenes, eight specialist agents support our team — Onboarding, Paraplanning, Insurance Research, Financial Crime, Underwriting, Claims, Client Communications and the Team Leader — and people approve every decision that matters." +
            GeneralOnly;
    }
}

public class ChatTurn
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class AgentInsightResponse
{
    public string? Response { get; set; }
}
