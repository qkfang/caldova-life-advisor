using System.Collections.Concurrent;
using CaldovaLife.App.Models;

namespace CaldovaLife.App.Services;

/// <summary>
/// One Client Case — the shared record every agent at Caldova Life Advice
/// reads from. Opened by the Onboarding Agent's "Try it out" flow (or seeded
/// at start-up) and looked up by case number on every other agent page.
/// </summary>
/// <param name="CaseNumber">Case number, <c>REQ-YYYYMMDD-XXXXXX</c>.</param>
/// <param name="SampleId">Id of the <see cref="RequestSample"/> the case came from.</param>
/// <param name="ClientName">Client name (for a death claim, the claimant).</param>
/// <param name="ClientEmail">Verified email for the client or claimant.</param>
/// <param name="ClientPhone">Verified phone for the client or claimant.</param>
/// <param name="ClientNumber">Caldova client number(s), e.g. <c>CL-104582</c>.</param>
/// <param name="Cover">Existing or requested cover, on one line.</param>
/// <param name="RequestType">One of the canonical request types (see <see cref="RequestTypes"/>).</param>
/// <param name="ReceivedDate">Date the request was received (<c>yyyy-MM-dd</c>).</param>
/// <param name="AdviserName">Always Caldova's adviser, "Amelia, Senior Risk Adviser".</param>
/// <param name="ReferredBy">Referral partner and firm, or "Existing client".</param>
/// <param name="RequestSummary">One-paragraph plain-English summary of the request.</param>
/// <param name="Amount">Headline figure in AUD (e.g. "$680,000 joint mortgage to protect").</param>
/// <param name="PreferredContact">How (and through whom) the client wants to be contacted.</param>
/// <param name="Priority">"Standard", "High" or "Urgent".</param>
/// <param name="PriorityReason">Why the case has that priority, including any vulnerability.</param>
public record CaseRecord(
    string CaseNumber,
    string SampleId,
    string ClientName,
    string ClientEmail,
    string ClientPhone,
    string ClientNumber,
    string Cover,
    string RequestType,
    string ReceivedDate,
    string AdviserName,
    string ReferredBy,
    string RequestSummary,
    string Amount,
    string PreferredContact,
    string Priority,
    string PriorityReason,
    DateTimeOffset CreatedAt);

/// <summary>Canonical request types used across the practice (one per scenario).</summary>
public static class RequestTypes
{
    public const string NewProtectionAdvice = "New protection advice";
    public const string TraumaClaimBankChange = "Trauma claim and bank change";
    public const string BusinessProtectionAdvice = "Business protection advice";
    public const string IncomeProtectionClaim = "Income protection claim";
    public const string DeathClaim = "Death claim";
}

/// <summary>
/// In-memory store of Client Cases. The Onboarding Agent's "Try it out" flow
/// mints a fresh case number on every <see cref="Add"/> call; later agents
/// look the case up by case number.
/// </summary>
public class CaseStore
{
    /// <summary>
    /// Suffix tag of the five seeded scenario cases (<c>REQ-yyyyMMdd-LIFnnn</c>).
    /// Live case numbers never contain an "I" (see <see cref="GenerateCaseNumber"/>),
    /// so the tag can't clash with a case opened live.
    /// </summary>
    private const string ScenarioTag = "LIF";

    /// <summary>True for the five seeded scenario cases, false for cases opened live.</summary>
    public static bool IsScenarioCaseNumber(string caseNumber) =>
        caseNumber.Contains($"-{ScenarioTag}", StringComparison.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CaseRecord> _records =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mint a fresh case number, persist the supplied details, and return
    /// the resulting <see cref="CaseRecord"/>.
    /// </summary>
    public CaseRecord Add(
        string sampleId,
        RequestExtractedFields fields,
        string priority,
        string priorityReason)
    {
        var caseNumber = GenerateCaseNumber();
        var record = FromFields(caseNumber, sampleId, fields, priority, priorityReason, DateTimeOffset.UtcNow);
        _records[caseNumber] = record;
        return record;
    }

    public CaseRecord? Get(string caseNumber) =>
        !string.IsNullOrWhiteSpace(caseNumber) && _records.TryGetValue(caseNumber, out var r)
            ? r : null;

    public IReadOnlyList<CaseRecord> All() =>
        _records.Values.OrderByDescending(r => r.CreatedAt).ToList();

    /// <summary>
    /// Pre-populate the store with one case per sample in
    /// <see cref="RequestSampleCatalog"/> (the five canonical scenarios) so
    /// every downstream agent page has cases to pick from the moment the app
    /// starts. Case numbers are deterministic —
    /// <c>REQ-{received yyyyMMdd}-LIF{nnn}</c> — and match the case ids used by
    /// the Caldova House frontend scenarios (e.g. <c>REQ-20261103-LIF001</c>).
    /// No-op if the store already contains records.
    /// </summary>
    public void SeedDefaults()
    {
        if (!_records.IsEmpty) return;

        var now = DateTimeOffset.UtcNow;
        var index = 0;

        foreach (var sample in RequestSampleCatalog.All)
        {
            index++;
            var stamp = sample.ExpectedFields.ReceivedDate.Replace("-", string.Empty);
            if (stamp.Length != 8)
            {
                stamp = now.ToString("yyyyMMdd");
            }
            var caseNumber = $"REQ-{stamp}-{ScenarioTag}{index:000}";

            // Stagger CreatedAt so All() lists scenario 1 (Priya) first.
            _records[caseNumber] = FromFields(
                caseNumber,
                sample.Id,
                sample.ExpectedFields,
                sample.ExpectedPriority,
                sample.ExpectedPriorityReason,
                now.AddMinutes(-index));
        }
    }

    private static CaseRecord FromFields(
        string caseNumber,
        string sampleId,
        RequestExtractedFields f,
        string priority,
        string priorityReason,
        DateTimeOffset createdAt) =>
        new(
            CaseNumber: caseNumber,
            SampleId: sampleId,
            ClientName: f.ClientName,
            ClientEmail: f.ClientEmail,
            ClientPhone: f.ClientPhone,
            ClientNumber: f.ClientNumber,
            Cover: f.Cover,
            RequestType: f.RequestType,
            ReceivedDate: f.ReceivedDate,
            AdviserName: f.AdviserName,
            ReferredBy: f.ReferredBy,
            RequestSummary: f.RequestSummary,
            Amount: f.Amount,
            PreferredContact: f.PreferredContact,
            Priority: priority,
            PriorityReason: priorityReason,
            CreatedAt: createdAt);

    /// <summary>
    /// Generate a case number of the form <c>REQ-YYYYMMDD-XXXXXX</c>, dated in
    /// Melbourne time (Caldova's home). The 6-character suffix uses
    /// cryptographically-strong randomness so two near-simultaneous submissions
    /// cannot collide.
    /// </summary>
    private static string GenerateCaseNumber()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1
        Span<byte> buf = stackalloc byte[6];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buf);
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < buf.Length; i++)
        {
            chars[i] = alphabet[buf[i] % alphabet.Length];
        }
        var stamp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Melbourne).ToString("yyyyMMdd");
        return $"REQ-{stamp}-{new string(chars)}";
    }

    private static readonly TimeZoneInfo Melbourne = FindMelbourne();

    private static TimeZoneInfo FindMelbourne()
    {
        foreach (var id in new[] { "Australia/Melbourne", "AUS Eastern Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        }
        return TimeZoneInfo.Utc;
    }
}
