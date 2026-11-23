using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LifeAdviceGuru.App.Services;

/// <summary>
/// Lifecycle of a claim decision prepared by the Claims Agent (Ben, Benefit
/// Claims Advocate).
/// </summary>
public enum ClaimApprovalStatus
{
    /// <summary>Raised and waiting for the approver in Microsoft Teams.</summary>
    Pending,
    /// <summary>Lodgement approved by a person — the claim may now be lodged with the insurer.</summary>
    Approved,
    /// <summary>Rejected by the approver — nothing is lodged.</summary>
    Rejected,
    /// <summary>Lodged with the insurer.</summary>
    Lodged,
    /// <summary>The Team Leader approved the block — no claim is lodged.</summary>
    Blocked,
    /// <summary>Superseded by a newer approval raised for the same case.</summary>
    Cancelled
}

/// <summary>What the approver is asked to decide.</summary>
public static class ClaimDecisionKinds
{
    /// <summary>Approve lodging the claim with the insurer.</summary>
    public const string Lodgement = "lodgement";

    /// <summary>Approve blocking the claim and referring it to Financial Crime — nothing is lodged.</summary>
    public const string Block = "block";
}

/// <summary>
/// In-memory record of a claim decision prepared for human approval. Created
/// by the Claims desk (<c>/claims/process</c>) or the <c>claims_requestApproval</c>
/// MCP tool, decided through the Microsoft Teams card (or the
/// <c>/claims/approvals/{id}/approve|reject</c> endpoints as the in-page
/// fallback), and finalised by <c>claims_lodgeClaim</c> /
/// <c>/claims/approvals/{id}/lodge</c> — only ever after a person approved it.
/// </summary>
public class ClaimApprovalRecord
{
    public string ApprovalId { get; init; } = string.Empty;
    public string CaseNumber { get; init; } = string.Empty;

    /// <summary><see cref="ClaimDecisionKinds.Lodgement"/> or <see cref="ClaimDecisionKinds.Block"/>.</summary>
    public string Kind { get; init; } = ClaimDecisionKinds.Lodgement;

    public string ClientName { get; init; } = string.Empty;
    public string ClientNumber { get; init; } = string.Empty;

    /// <summary>Client line shown on the Teams card, e.g. "Tom Bradley · CL-102265".</summary>
    public string ClientDisplay { get; init; } = string.Empty;

    public string Insurer { get; init; } = string.Empty;

    /// <summary>Insurer policy-number prefix (HL, CR, LB, NB) — used for the lodgement reference.</summary>
    public string InsurerPrefix { get; init; } = string.Empty;

    public string PolicyNumber { get; init; } = string.Empty;

    /// <summary>One-line cover summary, e.g. "Income protection — $7,000 a month insured, 30-day waiting period".</summary>
    public string CoverDisplay { get; init; } = string.Empty;

    /// <summary>Claim type key (see <see cref="ClaimCoverTypes"/>).</summary>
    public string ClaimType { get; init; } = ClaimCoverTypes.IncomeProtection;

    public string ClaimantName { get; init; } = string.Empty;
    public string ClaimantRole { get; init; } = string.Empty;

    /// <summary>Name on the bank account the insurer will pay.</summary>
    public string PayeeName { get; init; } = string.Empty;

    /// <summary>Masked bank details, e.g. <c>BSB 033-*** ****5120</c>. Full numbers are never stored.</summary>
    public string PayeeAccount { get; init; } = string.Empty;

    public decimal ExpectedBenefit { get; init; }

    /// <summary>Display form, e.g. "$5,600 a month" or "$1,000,000 lump sum".</summary>
    public string ExpectedBenefitDisplay { get; init; } = string.Empty;

    /// <summary>Amount the claim asked for (shown as "protected" when a claim is blocked).</summary>
    public decimal? AmountClaimed { get; init; }

    /// <summary>One line per check, shown on the Teams card.</summary>
    public IReadOnlyList<string> Checks { get; init; } = Array.Empty<string>();

    public string Recommendation { get; init; } = string.Empty;

    /// <summary>Why approval is needed (one paragraph).</summary>
    public string? Reason { get; init; }

    public IReadOnlyList<string> ApprovalReasons { get; init; } = Array.Empty<string>();
    public string Approver { get; init; } = ClaimRules.TeamLeaderApprover;
    public string RaisedBy { get; init; } = ClaimRules.RaisedByDesk;

    /// <summary>Lodgement date for the scenario (e.g. Thu 12 Nov 2026); today when not set.</summary>
    public DateOnly? LodgementDate { get; init; }

    /// <summary>Claim update draft for Client Communications; <c>{{LODGEMENT_REFERENCE}}</c> is filled on lodgement.</summary>
    public string? ClaimUpdateDraft { get; init; }

    /// <summary>What happened after lodgement in the scenario (insurer decision, payments).</summary>
    public IReadOnlyList<ClaimTimelineEvent> InsurerOutcome { get; init; } = Array.Empty<ClaimTimelineEvent>();

    /// <summary>The insurer's Life Insurance Code of Practice timeframes for this claim.</summary>
    public IReadOnlyList<ClaimFact> Timeframes { get; init; } = Array.Empty<ClaimFact>();

    public string? OutcomeLabel { get; init; }
    public string? OutcomeSummary { get; init; }

    /// <summary>Shown when a block is approved, e.g. "Grace's $500,000 life cover and $200,000 trauma cover are safe."</summary>
    public string? ProtectedNote { get; init; }

    public bool TeamsSent { get; set; }
    public string? TeamsChannel { get; set; }
    public string? TeamsMessage { get; set; }
    public string? ApprovalUrl { get; set; }
    public ClaimApprovalStatus Status { get; set; } = ClaimApprovalStatus.Pending;
    public string? Decision { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionComment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? LodgementReference { get; set; }
    public DateTimeOffset? LodgedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Foundry response id captured at the end of the <c>/claims/process</c>
    /// run. The approval popup (<c>/claims/approvals/{id}/agent-approve</c>)
    /// resumes the same conversation with it so the human decision flows back
    /// to the Claims Agent, which then calls <c>claims_lodgeClaim</c>.
    /// </summary>
    public string? PreviousResponseId { get; set; }

    /// <summary>Agent narrative produced by the resume turn after approval.</summary>
    public string? AgentResumeNotes { get; set; }

    public string StatusKey => Status.ToString().ToLowerInvariant();
    public bool IsBlockDecision => Kind == ClaimDecisionKinds.Block;
}

/// <summary>Outcome of an approve / reject / lodge transition.</summary>
public record ClaimTransitionResult(bool Ok, ClaimApprovalRecord? Record, string? Error, int StatusCode, string? Note = null)
{
    public static ClaimTransitionResult Success(ClaimApprovalRecord record, string? note = null) =>
        new(true, record, null, 200, note);

    public static ClaimTransitionResult NotFound(string approvalId) =>
        new(false, null, $"approval '{approvalId}' not found", 404);

    public static ClaimTransitionResult Conflict(ClaimApprovalRecord record, string error) =>
        new(false, record, error, 409);
}

/// <summary>
/// Singleton in-memory store of the claim decisions prepared by the Claims
/// Agent. Every state change goes through <see cref="Approve"/>,
/// <see cref="Reject"/> and <see cref="Lodge"/>, so the "never lodge without
/// a person's approval" rule is enforced in one place for both the HTTP
/// endpoints and the MCP tools.
/// </summary>
public class ClaimApprovalStore
{
    private readonly ConcurrentDictionary<string, ClaimApprovalRecord> _records =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public static string NewApprovalId() =>
        "CLM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    public ClaimApprovalRecord Add(ClaimApprovalRecord record)
    {
        _records[record.ApprovalId] = record;
        return record;
    }

    public ClaimApprovalRecord? Get(string approvalId) =>
        !string.IsNullOrWhiteSpace(approvalId) && _records.TryGetValue(approvalId.Trim(), out var r) ? r : null;

    public IEnumerable<ClaimApprovalRecord> All() =>
        _records.Values.OrderByDescending(r => r.CreatedAt);

    public IEnumerable<ClaimApprovalRecord> ForCase(string caseNumber)
    {
        var key = (caseNumber ?? string.Empty).Trim();
        return _records.Values
            .Where(r => string.Equals(r.CaseNumber, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.CreatedAt);
    }

    /// <summary>Latest decision for the case that is still live (pending, approved, lodged or blocked).</summary>
    public ClaimApprovalRecord? CurrentForCase(string caseNumber) =>
        ForCase(caseNumber).FirstOrDefault(r => r.Status is ClaimApprovalStatus.Pending
            or ClaimApprovalStatus.Approved
            or ClaimApprovalStatus.Lodged
            or ClaimApprovalStatus.Blocked);

    /// <summary>
    /// Cancel every open decision for the case (pending, approved or blocked)
    /// so only the newest run can be approved and lodged. Lodged claims stay
    /// lodged.
    /// </summary>
    public int SupersedeOpenForCase(string caseNumber, string supersededBy)
    {
        var count = 0;
        lock (_gate)
        {
            foreach (var r in ForCase(caseNumber))
            {
                if (r.Status is ClaimApprovalStatus.Pending or ClaimApprovalStatus.Approved or ClaimApprovalStatus.Blocked)
                {
                    r.Status = ClaimApprovalStatus.Cancelled;
                    r.Decision = "superseded";
                    r.DecisionComment = $"Superseded by {supersededBy}.";
                    r.DecidedAt = DateTimeOffset.UtcNow;
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Record the approver's "Approve". For a lodgement decision the claim may
    /// then be lodged; for a block decision the block is in place and nothing
    /// is ever lodged.
    /// </summary>
    public ClaimTransitionResult Approve(string approvalId, string? decidedBy, string? comment = null)
    {
        lock (_gate)
        {
            var r = Get(approvalId);
            if (r is null) return ClaimTransitionResult.NotFound(approvalId);
            switch (r.Status)
            {
                case ClaimApprovalStatus.Approved:
                    return ClaimTransitionResult.Success(r, "already approved");
                case ClaimApprovalStatus.Blocked:
                    return ClaimTransitionResult.Success(r, "block already approved");
                case ClaimApprovalStatus.Lodged:
                    return ClaimTransitionResult.Conflict(r, "the claim has already been lodged");
                case ClaimApprovalStatus.Rejected:
                    return ClaimTransitionResult.Conflict(r, "the approver has already rejected this");
                case ClaimApprovalStatus.Cancelled:
                    return ClaimTransitionResult.Conflict(r, "this approval was superseded by a newer one for the case");
            }

            var block = r.IsBlockDecision;
            r.Status = block ? ClaimApprovalStatus.Blocked : ClaimApprovalStatus.Approved;
            r.Decision = block ? "block approved" : "approved";
            r.DecidedBy = string.IsNullOrWhiteSpace(decidedBy) ? r.Approver : decidedBy.Trim();
            r.DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            r.DecidedAt = DateTimeOffset.UtcNow;
            return ClaimTransitionResult.Success(r);
        }
    }

    public ClaimTransitionResult Reject(string approvalId, string? decidedBy, string? comment = null)
    {
        lock (_gate)
        {
            var r = Get(approvalId);
            if (r is null) return ClaimTransitionResult.NotFound(approvalId);
            switch (r.Status)
            {
                case ClaimApprovalStatus.Rejected:
                    return ClaimTransitionResult.Success(r, "already rejected");
                case ClaimApprovalStatus.Lodged:
                    return ClaimTransitionResult.Conflict(r, "the claim has already been lodged with the insurer");
                case ClaimApprovalStatus.Blocked:
                    return ClaimTransitionResult.Conflict(r, "the block is already approved — no claim was lodged");
                case ClaimApprovalStatus.Cancelled:
                    return ClaimTransitionResult.Conflict(r, "this approval was superseded by a newer one for the case");
            }

            r.Status = ClaimApprovalStatus.Rejected;
            r.Decision = "rejected";
            r.DecidedBy = string.IsNullOrWhiteSpace(decidedBy) ? r.Approver : decidedBy.Trim();
            r.DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            r.DecidedAt = DateTimeOffset.UtcNow;
            return ClaimTransitionResult.Success(r);
        }
    }

    /// <summary>
    /// Lodge an approved claim with the insurer. Refuses unless a lodgement
    /// decision is in the <see cref="ClaimApprovalStatus.Approved"/> state;
    /// a block decision can never be lodged.
    /// </summary>
    public ClaimTransitionResult Lodge(string approvalId)
    {
        lock (_gate)
        {
            var r = Get(approvalId);
            if (r is null) return ClaimTransitionResult.NotFound(approvalId);
            if (r.IsBlockDecision)
            {
                return ClaimTransitionResult.Conflict(r, r.Status == ClaimApprovalStatus.Blocked
                    ? "the claim is blocked and referred to Financial Crime — nothing is lodged"
                    : "the claimant checks failed and the claim is referred to Financial Crime — it can't be lodged");
            }
            switch (r.Status)
            {
                case ClaimApprovalStatus.Lodged:
                    return ClaimTransitionResult.Success(r, "already lodged");
                case ClaimApprovalStatus.Pending:
                    return ClaimTransitionResult.Conflict(r, "lodgement is still awaiting approval in Microsoft Teams — the claim can't be lodged yet");
                case ClaimApprovalStatus.Rejected:
                    return ClaimTransitionResult.Conflict(r, "the approver rejected lodgement — the claim will not be lodged");
                case ClaimApprovalStatus.Blocked:
                    return ClaimTransitionResult.Conflict(r, "the claim is blocked — nothing is lodged");
                case ClaimApprovalStatus.Cancelled:
                    return ClaimTransitionResult.Conflict(r, "this approval was superseded by a newer one for the case");
            }

            var date = r.LodgementDate ?? ClaimRules.MelbourneToday();
            var suffix = r.ApprovalId.Length >= 6 ? r.ApprovalId[^6..] : r.ApprovalId;
            var prefix = string.IsNullOrWhiteSpace(r.InsurerPrefix) ? "CL" : r.InsurerPrefix;
            r.Status = ClaimApprovalStatus.Lodged;
            r.LodgementReference = $"{prefix}-CLM-{date:yyyyMMdd}-{suffix}";
            r.LodgedAt = DateTimeOffset.UtcNow;
            return ClaimTransitionResult.Success(r);
        }
    }
}

/// <summary>A key + display label pair for the claim input drop-downs.</summary>
public record ClaimOption(string Key, string Label);

/// <summary>The types of claim Ben's desk handles.</summary>
public static class ClaimCoverTypes
{
    public const string IncomeProtection = "income-protection";
    public const string Death = "death";
    public const string TerminalIllness = "terminal-illness";
    public const string Trauma = "trauma";
    public const string Tpd = "tpd";

    public static readonly IReadOnlyList<ClaimOption> All =
    [
        new(IncomeProtection, "Income protection — monthly benefit"),
        new(Death, "Death — life cover"),
        new(TerminalIllness, "Terminal illness — life cover paid early"),
        new(Trauma, "Trauma — critical illness lump sum"),
        new(Tpd, "TPD — total and permanent disability lump sum"),
    ];

    public static string Label(string? key)
    {
        var k = Normalize(key);
        return All.First(o => o.Key == k).Label;
    }

    /// <summary>"Income protection claim", "Death claim", …</summary>
    public static string ClaimLabel(string? key) => Normalize(key) switch
    {
        Death => "Death claim",
        TerminalIllness => "Terminal illness claim",
        Trauma => "Trauma claim",
        Tpd => "TPD claim",
        _ => "Income protection claim",
    };

    /// <summary>"income protection", "life cover", … (lower case, for sentences).</summary>
    public static string CoverName(string? key) => Normalize(key) switch
    {
        Death or TerminalIllness => "life cover",
        Trauma => "trauma cover",
        Tpd => "TPD cover",
        _ => "income protection",
    };

    public static bool IsDeathOrTerminal(string? key) => Normalize(key) is Death or TerminalIllness;

    /// <summary>Accepts a key, a label or free text (as an agent might pass it).</summary>
    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var o in All)
        {
            if (v == o.Key || v == o.Label.ToLowerInvariant()) return o.Key;
        }
        if (v.Contains("terminal")) return TerminalIllness;
        if (v.Contains("death") || v.Contains("life cover") || v == "life") return Death;
        if (v.Contains("trauma") || v.Contains("critical") || v.Contains("cancer")) return Trauma;
        if (v.Contains("tpd") || v.Contains("total and permanent")) return Tpd;
        return IncomeProtection;
    }
}

/// <summary>How the claimant is connected to the policy.</summary>
public static class ClaimantRelationships
{
    public const string PolicyOwner = "policy-owner";
    public const string Beneficiary = "beneficiary";
    public const string Estate = "estate";
    public const string Other = "other";

    public static readonly IReadOnlyList<ClaimOption> All =
    [
        new(PolicyOwner, "Policy owner"),
        new(Beneficiary, "Nominated beneficiary"),
        new(Estate, "Executor or administrator of the estate"),
        new(Other, "Someone else (family member, business, …)"),
    ];

    public static string Label(string? key)
    {
        var k = Normalize(key);
        return All.First(o => o.Key == k).Label;
    }

    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var o in All)
        {
            if (v == o.Key || v == o.Label.ToLowerInvariant()) return o.Key;
        }
        if (v.Contains("benefic")) return Beneficiary;
        if (v.Contains("estate") || v.Contains("executor") || v.Contains("administrator") || v.Contains("personal representative")) return Estate;
        if (v.Contains("owner") || v.Contains("life insured") || v.Contains("insured") || v.Length == 0) return PolicyOwner;
        return Other;
    }
}

/// <summary>The paths a claim can take on Ben's desk.</summary>
public static class ClaimFlows
{
    /// <summary>Checks pass — raise the Teams approval, lodge only after a person approves.</summary>
    public const string Lodge = "lodge";

    /// <summary>Claimant checks raise financial-crime concerns — don't lodge; refer to Financial Crime; the block is the Team Leader's decision.</summary>
    public const string Refer = "refer";

    /// <summary>Not ready — the claimant isn't entitled or the cover isn't on the policy. Nothing is lodged.</summary>
    public const string Hold = "hold";

    /// <summary>A new advice case — there is nothing to claim.</summary>
    public const string NotAClaim = "not-a-claim";
}

public record ClaimLink(string Label, string Href);

public record ClaimBeneficiary(string Name, string Relationship, decimal SharePercent);

/// <summary>One document in the claim pack. Status: verified, received, prepared, later, to-come, missing or referred.</summary>
public record ClaimPackItem(string Document, string Status, string Note);

public record ClaimTimelineEvent(string When, string What);

/// <summary>A note shown with the result. Tone: info, adviser (for Amelia, not advice) or care.</summary>
public record ClaimNote(string Title, string Text, string Tone);

public record ClaimFact(string Label, string Value);

/// <summary>One check shown on the page. Status: pass, fail, warn or na.</summary>
public record ClaimCheckLine(string Key, string Label, string Status, string Value, string Detail, string? Source);

/// <summary>
/// Deterministic claim data for one Client Case, keyed off the five
/// scenario cases (by sample id or case number) with a generic fallback for
/// cases opened live by the Onboarding page.
/// </summary>
public sealed record ClaimCaseProfile
{
    public string ProfileId { get; init; } = "generic";

    /// <summary>Default path (see <see cref="ClaimFlows"/>); the claimant checks can still divert a run to refer or hold.</summary>
    public string Flow { get; init; } = ClaimFlows.Lodge;

    public string ClientName { get; init; } = string.Empty;
    public string ClientNumber { get; init; } = string.Empty;
    public string ClientDisplay { get; init; } = string.Empty;

    // ── New advice cases (nothing to claim) ─────────────────────────
    public string? NotAClaimNote { get; init; }
    public IReadOnlyList<ClaimLink> NotAClaimLinks { get; init; } = Array.Empty<ClaimLink>();

    // ── Policy ──────────────────────────────────────────────────────
    public string Insurer { get; init; } = string.Empty;
    public string InsurerPrefix { get; init; } = string.Empty;
    public string PolicyNumber { get; init; } = string.Empty;

    /// <summary>Knowledge-index title of the insurer's PDS extract.</summary>
    public string PdsTitle { get; init; } = string.Empty;

    public string PolicyOwner { get; init; } = string.Empty;
    public string LifeInsured { get; init; } = string.Empty;
    public DateOnly? PolicyStart { get; init; }
    public string PolicyStartDisplay { get; init; } = string.Empty;

    /// <summary>Plain-English time in force when the start date is only known to the month or year.</summary>
    public string? TimeInForceText { get; init; }

    public bool HeldOutsideSuper { get; init; } = true;
    public bool InForce { get; init; } = true;
    public string Premiums { get; init; } = string.Empty;
    public bool PremiumsPaid { get; init; } = true;

    /// <summary>False for cases opened live, where the policy schedule still has to be confirmed with the insurer.</summary>
    public bool PolicyConfirmed { get; init; } = true;

    /// <summary>Cover types held on the policy (see <see cref="ClaimCoverTypes"/>).</summary>
    public IReadOnlyList<string> CoverTypes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CoverLines { get; init; } = Array.Empty<string>();
    public string ClaimType { get; init; } = ClaimCoverTypes.IncomeProtection;

    /// <summary>Insured monthly benefit (income protection) or sum insured (lump sums).</summary>
    public decimal InsuredAmount { get; init; }

    public int? WaitingPeriodDays { get; init; }
    public string? BenefitPeriod { get; init; }

    /// <summary>The definition the claim relies on, in plain English.</summary>
    public string Definition { get; init; } = string.Empty;

    public bool DefinitionMet { get; init; } = true;
    public string DefinitionNote { get; init; } = string.Empty;
    public string Exclusions { get; init; } = string.Empty;
    public IReadOnlyList<ClaimBeneficiary> Beneficiaries { get; init; } = Array.Empty<ClaimBeneficiary>();

    // ── The event and the notification ──────────────────────────────
    public DateOnly? EventDate { get; init; }
    public string EventLabel { get; init; } = "Date of the event";
    public string EventDescription { get; init; } = string.Empty;
    public string Notification { get; init; } = string.Empty;

    // ── Claimant ────────────────────────────────────────────────────
    public string ClaimantName { get; init; } = string.Empty;
    public string ClaimantRelationship { get; init; } = ClaimantRelationships.PolicyOwner;
    public string? SupportPerson { get; init; }
    public string VerifiedContact { get; init; } = string.Empty;

    /// <summary>A recent change of contact details (a financial-crime red flag), if any.</summary>
    public string? RecentChange { get; init; }

    /// <summary>True when the claim form is signed by the verified claimant (for living benefits, the verified policy owner).</summary>
    public bool ClaimFormVerified { get; init; } = true;

    public string ClaimFormNote { get; init; } = string.Empty;

    // ── Bank account ────────────────────────────────────────────────
    public string AccountName { get; init; } = string.Empty;
    public string Bsb { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string? AccountOnFileName { get; init; }
    public string? AccountOnFileLast4 { get; init; }
    public string? AccountNote { get; init; }

    /// <summary>Who the insurer pays, and into which account (from the insurer's PDS extract).</summary>
    public string? PaymentRule { get; init; }

    /// <summary>The insurer's own claims service from its PDS extract (e.g. a named claims consultant within 2 business days).</summary>
    public string? InsurerService { get; init; }

    /// <summary>Other cover found while checking the client's records (e.g. default cover in super).</summary>
    public string? OtherCover { get; init; }

    // ── Income protection earnings ──────────────────────────────────
    public decimal? AnnualEarnings { get; init; }
    public string? EarningsPeriod { get; init; }
    public string? EarningsEvidence { get; init; }
    public decimal ReplacementRatePercent { get; init; } = ClaimRules.IncomeReplacementPercent;

    // ── Lump sums ───────────────────────────────────────────────────
    public decimal BeneficiarySharePercent { get; init; } = 100m;
    public decimal? AmountClaimed { get; init; }

    // ── Evidence, care and financial crime ──────────────────────────
    public IReadOnlyList<ClaimPackItem> ClaimPack { get; init; } = Array.Empty<ClaimPackItem>();
    public bool Vulnerable { get; init; }
    public string? VulnerabilityNote { get; init; }
    public string? VulnerabilityApprovalReason { get; init; }
    public string? FinancialCrimeNote { get; init; }
    public IReadOnlyList<ClaimTimelineEvent> ActivityTimeline { get; init; } = Array.Empty<ClaimTimelineEvent>();

    // ── After the decision ──────────────────────────────────────────
    public DateOnly? LodgementDate { get; init; }

    /// <summary>The scenario's expected benefit; the scenario outcome is shown only when a run reproduces it.</summary>
    public decimal? ScenarioBenefit { get; init; }

    public IReadOnlyList<ClaimTimelineEvent> InsurerOutcome { get; init; } = Array.Empty<ClaimTimelineEvent>();
    public string? OutcomeLabel { get; init; }
    public string? OutcomeSummary { get; init; }
    public string? ProtectedNote { get; init; }
    public string? ReferralNextStep { get; init; }
    public IReadOnlyList<ClaimNote> Notes { get; init; } = Array.Empty<ClaimNote>();

    /// <summary>Case-file titles in the <c>life_knowledge</c> index for this case.</summary>
    public IReadOnlyList<string> CaseFiles { get; init; } = Array.Empty<string>();

    public string InputsHint { get; init; } = string.Empty;
}

public record ClaimantCheckResult(
    string ClaimantName,
    string Relationship,
    string RelationshipLabel,
    bool Entitled,
    string EntitlementNote,
    bool ClaimFormVerified,
    bool ContactDetailsTrusted,
    string AccountName,
    string MaskedAccount,
    bool AccountInOwnName,
    bool? AccountMatchesFile,
    bool BankDetailChange,
    bool ReferToFinancialCrime,
    IReadOnlyList<string> RedFlags,
    IReadOnlyList<ClaimCheckLine> Checks);

public record ClaimPolicyCheckResult(
    string ClaimType,
    bool InForce,
    bool PremiumsPaid,
    bool CoverMatches,
    bool DefinitionMet,
    string TimeInForce,
    bool PastMisrepresentationWindow,
    IReadOnlyList<ClaimCheckLine> Checks,
    IReadOnlyList<ClaimPackItem> ClaimPack,
    string RuleCited);

/// <summary>One line of the benefit working. Kind: input, step, deduct, total or note.</summary>
public record ClaimBenefitStep(string Label, string Kind, decimal? Amount, string Display);

public record ClaimBenefitCalculation(
    string ClaimType,
    string Mode,
    string Title,
    IReadOnlyList<ClaimBenefitStep> Steps,
    decimal Benefit,
    string BenefitDisplay,
    decimal InsuredAmount,
    bool LimitedByEarnings,
    decimal? MonthlyEarnings,
    decimal? EarningsCap,
    DateOnly? AccrualStart,
    string? FirstPayment,
    string PaidTo,
    string TaxTreatment,
    IReadOnlyList<string> Notes,
    string RuleCited,
    string TaxRuleCited);

public record ClaimApprovalRulesResult(
    bool Required,
    IReadOnlyList<string> Reasons,
    string Approver,
    bool PersonFirst,
    bool DoNotLodge,
    string Recommendation,
    string Note,
    string RuleCited);

/// <summary>
/// Claim rules shared by the <c>/claims/*</c> endpoints and the
/// <c>claims_*</c> MCP tools: per-case deterministic data, the claimant and
/// policy checks, the benefit calculation, the insurer timeframes, the tax
/// treatment and the human-approval rules.
/// </summary>
public static partial class ClaimRules
{
    public const string TeamLeaderApprover = "Theo — Practice Team Leader";

    /// <summary>Decision raised by the Claims desk (the <c>/claims/process</c> run).</summary>
    public const string RaisedByDesk = "Claims desk";

    /// <summary>Decision raised directly by the agent through <c>claims_requestApproval</c>.</summary>
    public const string RaisedByAgent = "Claims Agent (MCP)";

    // Knowledge-index titles (life_knowledge) cited by the claims flow.
    public const string ClaimsRuleTitle = "Claims rules — Life Insurance Code of Practice and income protection benefits";
    public const string TaxRuleTitle = "Tax on life insurance — premiums and benefits";
    public const string DisclosureRuleTitle = "Underwriting and the duty to take reasonable care";
    public const string AuthorityRuleTitle = "Matrix — authority, approvals and escalation";
    public const string InGoodOrderTitle = "Guide — in-good-order checklist for advice and claims";
    public const string FinancialCrimeGuideTitle = "Guide — financial crime red flags and controls for life insurance";
    public const string HarbourlinePds = "Harbourline Life — Protection PDS extract";
    public const string CorellaPds = "Corella Life — Protection PDS extract";
    public const string LyrebirdPds = "Lyrebird Life — Protection PDS extract";
    public const string NorthbankPds = "Northbank Life — Business Protection PDS extract";

    /// <summary>Harbourline Life's income protection benefit basis: 70% of pre-disability earnings (policy term).</summary>
    public const decimal IncomeReplacementPercent = 70m;

    /// <summary>Placeholder in claim-update drafts, replaced by the lodgement reference.</summary>
    public const string ReferenceToken = "{{LODGEMENT_REFERENCE}}";

    public const string PriyaCase = "REQ-20261103-LIF001";
    public const string GraceCase = "REQ-20261106-LIF002";
    public const string GeorgeCase = "REQ-20261109-LIF003";
    public const string TomCase = "REQ-20261111-LIF004";
    public const string RobertCase = "REQ-20261113-LIF005";

    private static readonly CultureInfo AuCulture = CultureInfo.GetCultureInfo("en-AU");
    private static readonly Regex AmountRegex = new(@"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?", RegexOptions.Compiled);
    private static readonly Regex PolicyNumberRegex = new(@"\b(HL|CR|LB|NB)-\d{6,10}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Per-case deterministic data ─────────────────────────────────────

    public static ClaimCaseProfile ProfileFor(CaseRecord record) => ScenarioKey(record) switch
    {
        "tom" => IncomeProtectionTom(),
        "robert" => DeathClaimRobert(),
        "grace" => TraumaClaimGrace(),
        "priya" => AdvicePriya(),
        "george" => AdviceGeorge(),
        _ => Generic(record),
    };

    /// <summary>Scenario key from the sample id or the seeded case number.</summary>
    private static string ScenarioKey(CaseRecord r)
    {
        switch ((r.SampleId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "onboarding-tom": case "income-protection-claim": return "tom";
            case "onboarding-robert": case "death-claim": return "robert";
            case "onboarding-grace": case "fraudulent-claim": return "grace";
            case "onboarding-priya": case "family-protection": return "priya";
            case "onboarding-george": case "business-protection": return "george";
        }

        var caseNumber = (r.CaseNumber ?? string.Empty).Trim();
        if (caseNumber.Equals(TomCase, StringComparison.OrdinalIgnoreCase)) return "tom";
        if (caseNumber.Equals(RobertCase, StringComparison.OrdinalIgnoreCase)) return "robert";
        if (caseNumber.Equals(GraceCase, StringComparison.OrdinalIgnoreCase)) return "grace";
        if (caseNumber.Equals(PriyaCase, StringComparison.OrdinalIgnoreCase)) return "priya";
        if (caseNumber.Equals(GeorgeCase, StringComparison.OrdinalIgnoreCase)) return "george";
        return string.Empty;
    }

    private static ClaimCaseProfile IncomeProtectionTom() => new()
    {
        ProfileId = "income-protection-tom",
        Flow = ClaimFlows.Lodge,
        ClientName = "Tom Bradley",
        ClientNumber = "CL-102265",
        ClientDisplay = "Tom Bradley · CL-102265",
        Insurer = "Harbourline Life",
        InsurerPrefix = "HL",
        PolicyNumber = "HL-23051877",
        PdsTitle = HarbourlinePds,
        PolicyOwner = "Tom Bradley",
        LifeInsured = "Tom Bradley",
        PolicyStart = new DateOnly(2023, 3, 15),
        PolicyStartDisplay = "15 Mar 2023",
        Premiums = "$2,880 a year, paid to date",
        CoverTypes = [ClaimCoverTypes.IncomeProtection],
        CoverLines =
        [
            "Income protection — insured monthly benefit $7,000 (set from $120,000 a year at application)",
            "30-day waiting period · benefit period to age 65",
            "Held outside super · premiums $2,880 a year",
        ],
        ClaimType = ClaimCoverTypes.IncomeProtection,
        InsuredAmount = 7_000m,
        WaitingPeriodDays = 30,
        BenefitPeriod = "to age 65",
        Definition = "Totally disabled — because of an injury or illness, Tom can't do at least one income-producing duty of his occupation as an electrician, isn't working, and is under the regular care of a medical practitioner.",
        DefinitionMet = true,
        DefinitionNote = "Surgeon's report: torn rotator cuff (right shoulder), surgery on Wed 21 Oct 2026, four to six months off work expected.",
        Exclusions = "None on the policy schedule.",
        EventDate = new DateOnly(2026, 10, 14),
        EventLabel = "Date of injury",
        EventDescription = "Fell from a ladder on a job on Wed 14 Oct 2026 — torn rotator cuff (right shoulder); surgery on Wed 21 Oct 2026; the surgeon expects four to six months off work.",
        Notification = "Wed 11 Nov 2026 — Tom called; Kylie joined as his support person with his written authority.",
        ClaimantName = "Tom Bradley",
        ClaimantRelationship = ClaimantRelationships.PolicyOwner,
        SupportPerson = "Kylie Bradley (his wife) — support person under Tom's written authority",
        VerifiedContact = "0491 570 110 · tom.bradley@example.com — verified, no recent changes",
        ClaimFormVerified = true,
        ClaimFormNote = "Claim form signed by Tom on Wed 11 Nov 2026. His written authority lets Kylie act as his support person — under the Life Insurance Code of Practice (cl 6.13) the insurer recognises a support person in all reasonable ways.",
        AccountName = "Tom Bradley",
        Bsb = "033-***",
        Account = "****5120",
        AccountOnFileName = "Tom Bradley",
        AccountOnFileLast4 = "5120",
        AccountNote = "Tom's own account, already on file.",
        AnnualEarnings = 96_000m,
        EarningsPeriod = "12 months to 30 Sep 2026",
        EarningsEvidence = "BAS for the four quarters to 30 Sep 2026 and the profit and loss statement for Bradley Electrical (net business income)",
        ReplacementRatePercent = IncomeReplacementPercent,
        PaymentRule = "Harbourline pays benefits to the policy owner, only into an account in the payee's own name.",
        InsurerService = "Harbourline Life gives every claim a named claims consultant within 2 business days.",
        ClaimPack =
        [
            new("Claim form", "verified", "Signed by Tom, the policy owner, on Wed 11 Nov 2026 — Kylie helped him complete it."),
            new("Support-person authority", "verified", "Tom's written authority for Kylie Bradley to help with the claim and speak with us."),
            new("Surgeon's report", "verified", "Torn rotator cuff (right shoulder) from a fall on Wed 14 Oct 2026; surgery on Wed 21 Oct 2026; four to six months off work expected. Checked by Financial Crime — legitimate."),
            new("Earnings evidence", "verified", "BAS for the four quarters to 30 Sep 2026 and the profit and loss statement for Bradley Electrical: net business income $96,000. Checked by Financial Crime — legitimate."),
            new("Certified ID", "verified", "Tom's ID on file since 2023; contact details unchanged."),
            new("Bank details", "verified", "Tom's own account, already on file (BSB 033-*** ****5120)."),
        ],
        Vulnerable = true,
        VulnerabilityNote = "Tom is injured and off work, self-employed with two young children — financial stress was flagged when the case was opened. Theo reviews first and has asked Amelia to call Tom personally before the letter goes out.",
        VulnerabilityApprovalReason = "Vulnerable client — Tom is injured, off work and under financial stress, so a person reviews first: Theo approves, and Amelia calls Tom personally before the letter.",
        FinancialCrimeNote = "Financial Crime (Felix's agent) checked the BAS, the profit and loss and the surgeon's report — legitimate.",
        LodgementDate = new DateOnly(2026, 11, 12),
        ScenarioBenefit = 5_600m,
        InsurerOutcome =
        [
            new("Thu 12 Nov 2026", "Claim lodged with Harbourline Life after Theo's approval."),
            new("Fri 13 Nov 2026", "The 30-day waiting period from Wed 14 Oct 2026 ends, and the benefit starts to accrue."),
            new("Thu 19 Nov 2026", "Harbourline Life accepted the claim at $5,600 a month — not the $7,000 insured — because the benefit follows Tom's earnings in the 12 months before the injury. The decision came 5 business days after lodgement, well inside the Code's 2 months."),
            new("About 13 Dec 2026", "First monthly payment, paid in arrears."),
        ],
        OutcomeLabel = "Partially approved",
        OutcomeSummary = "Claim accepted at $5,600 a month, not the $7,000 insured, because the benefit follows Tom's earnings in the 12 months before the injury.",
        Notes =
        [
            new("Note for Amelia — not advice",
                "Tom is insured for $7,000 a month, but his earnings in the 12 months before the injury support $5,600 a month. At his next review, Amelia may consider whether the insured benefit should change. The adviser decides.",
                "adviser"),
        ],
        CaseFiles =
        [
            $"Case {TomCase} — claim notification",
            $"Case {TomCase} — policy schedule",
            $"Case {TomCase} — earnings evidence",
            $"Case {TomCase} — benefit calculation",
        ],
        InputsHint = "Tom is claiming on his Harbourline Life income protection. The benefit is the lesser of his insured $7,000 a month and 70% of his average monthly earnings in the 12 months before the injury — change the earnings or the insured benefit to see the working change.",
    };

    private static ClaimCaseProfile DeathClaimRobert() => new()
    {
        ProfileId = "death-claim-robert",
        Flow = ClaimFlows.Lodge,
        ClientName = "Robert Chen",
        ClientNumber = "CL-101935",
        ClientDisplay = "Robert Chen · CL-101935 — claiming on Mei Chen's policy (CL-101934)",
        Insurer = "Corella Life",
        InsurerPrefix = "CR",
        PolicyNumber = "CR-19033105",
        PdsTitle = CorellaPds,
        PolicyOwner = "Mei Chen",
        LifeInsured = "Mei Chen",
        PolicyStart = new DateOnly(2019, 3, 1),
        PolicyStartDisplay = "March 2019 (advised by Amelia)",
        TimeInForceText = "over seven years (since March 2019)",
        Premiums = "Paid to date — the policy was in force when Mei died",
        CoverTypes = [ClaimCoverTypes.Death, ClaimCoverTypes.TerminalIllness],
        CoverLines =
        [
            "Life cover — $1,000,000 (held outside super)",
            "Policy owner and life insured: Mei Chen",
            "Nominated beneficiary: Robert Chen, 100%",
        ],
        ClaimType = ClaimCoverTypes.Death,
        InsuredAmount = 1_000_000m,
        Definition = "Death of the life insured while the policy is in force.",
        DefinitionMet = true,
        DefinitionNote = "Certified copy of the death certificate: Mei died on Mon 12 Oct 2026, aged 42.",
        Exclusions = "None.",
        Beneficiaries = [new("Robert Chen", "husband", 100m)],
        EventDate = new DateOnly(2026, 10, 12),
        EventLabel = "Date of death",
        EventDescription = "Mei Chen died suddenly of a brain aneurysm on Mon 12 Oct 2026, aged 42.",
        Notification = "Fri 13 Nov 2026 — Robert emailed Amelia a certified copy of the death certificate and his ID.",
        ClaimantName = "Robert Chen",
        ClaimantRelationship = ClaimantRelationships.Beneficiary,
        VerifiedContact = "0491 570 313 · robert.chen@example.com — verified, no recent changes",
        ClaimFormVerified = true,
        ClaimFormNote = "Claimant's statement prepared from what Robert has already told Amelia, so he only needs to check and sign it; his ID is verified.",
        AccountName = "Robert Chen",
        Bsb = "083-***",
        Account = "****9012",
        AccountOnFileName = "Robert Chen",
        AccountOnFileLast4 = "9012",
        AccountNote = "Robert's own account, on his client file (CL-101935).",
        BeneficiarySharePercent = 100m,
        PaymentRule = "Mei owned the policy and nominated Robert, so Corella pays the life cover directly to him in the share nominated, not through her estate, into an account in his own name.",
        InsurerService = "Corella Life gives every death and terminal illness claim a dedicated case manager.",
        OtherCover = "Mei's Riverbend Super account had default death cover of $180,000 — a separate claim with the fund's trustee.",
        ClaimPack =
        [
            new("Certified copy of the death certificate", "verified", "Received Fri 13 Nov 2026. Checked by Financial Crime — legitimate."),
            new("Robert's certified ID", "verified", "Received Fri 13 Nov 2026. Checked by Financial Crime — legitimate."),
            new("Policy schedule and beneficiary nomination", "verified", "CR-19033105 — Robert Chen, 100%. The nomination is valid, so the benefit is paid directly to Robert, not through Mei's estate."),
            new("Corella Life claim form", "prepared", "Completed with Robert, with Ben's help, from what he has already told Amelia — he only needs to check and sign it."),
            new("Bank details", "verified", "Robert's own account, on his client file (BSB 083-*** ****9012)."),
        ],
        Vulnerable = true,
        VulnerabilityNote = "Robert is grieving and caring for Lily (12) and Max (9). Theo oversees the case with compassion: Amelia is Robert's one named contact, and Theo approves every message to the family.",
        VulnerabilityApprovalReason = "Bereaved family — Theo's compassionate oversight: Amelia is Robert's one named contact, and Theo approves every message to the family.",
        FinancialCrimeNote = "Financial Crime (Felix's agent) checked the death certificate and Robert's ID — legitimate.",
        LodgementDate = new DateOnly(2026, 11, 16),
        ScenarioBenefit = 1_000_000m,
        InsurerOutcome =
        [
            new("Mon 16 Nov 2026", "Claim lodged with Corella Life after Theo's approval."),
            new("Fri 20 Nov 2026", "Corella Life accepted the claim — well inside the Code's 6 months. Its letter suggested Robert consider independent financial advice (cl 5.61); Amelia will offer to meet when he's ready."),
            new("Mon 23 Nov 2026", "$1,000,000 paid to Robert — directly, not through Mei's estate."),
        ],
        OutcomeLabel = "Completed",
        OutcomeSummary = "$1,000,000 paid to Robert on Mon 23 Nov 2026, with Mei's $180,000 super claim under way.",
        Notes =
        [
            new("Also found — Mei's super cover", SuperCoverNoteRobert, "info"),
        ],
        CaseFiles =
        [
            $"Case {RobertCase} — death notification",
            $"Case {RobertCase} — death certificate",
            $"Case {RobertCase} — policy schedule and nomination",
            $"Case {RobertCase} — super fund cover note",
        ],
        InputsHint = "Robert is claiming on his late wife Mei's Corella Life policy as her nominated beneficiary (100%), so the benefit is paid directly to him, not through her estate.",
    };

    private static ClaimCaseProfile TraumaClaimGrace() => new()
    {
        ProfileId = "trauma-claim-grace",
        Flow = ClaimFlows.Refer,
        ClientName = "Grace Williams",
        ClientNumber = "CL-100377",
        ClientDisplay = "Grace Williams · CL-100377",
        Insurer = "Lyrebird Life",
        InsurerPrefix = "LB",
        PolicyNumber = "LB-19004417",
        PdsTitle = LyrebirdPds,
        PolicyOwner = "Grace Williams",
        LifeInsured = "Grace Williams",
        PolicyStart = new DateOnly(2019, 1, 1),
        PolicyStartDisplay = "2019",
        TimeInForceText = "about seven years (since 2019)",
        Premiums = "Stepped premiums, paid to date",
        CoverTypes = [ClaimCoverTypes.Death, ClaimCoverTypes.TerminalIllness, ClaimCoverTypes.Trauma],
        CoverLines =
        [
            "Life cover — $500,000",
            "Trauma cover — $200,000",
            "Held outside super · stepped premiums",
            "Nominated beneficiaries: Daniel 50%, James 50%",
        ],
        ClaimType = ClaimCoverTypes.Trauma,
        InsuredAmount = 200_000m,
        AmountClaimed = 200_000m,
        Definition = "Cancer — a malignant tumour, shown by the uncontrolled growth and spread of malignant cells and the invasion of tissue, confirmed by a specialist medical practitioner and supported by histopathology; a GP's letter or imaging alone isn't enough. Trauma benefits are paid to the policy owner.",
        DefinitionMet = false,
        DefinitionNote = "The claim includes an oncologist's report but no histopathology, so it doesn't show the definition is met.",
        Exclusions = "None relevant to this claim.",
        Beneficiaries = [new("Daniel Williams", "son", 50m), new("James Williams", "son", 50m)],
        EventLabel = "Diagnosis date on the claim",
        EventDescription = "A trauma claim for breast cancer, emailed on Fri 6 Nov 2026 at 07:52 — marked urgent, for the full $200,000, to be paid into a new account.",
        Notification = "Fri 6 Nov 2026 07:52 — email from grace.w.claims@example.net, the address set up on Mon 2 Nov, asking Life Advice Guru to use only the new mobile and email.",
        ClaimantName = "Grace Williams",
        ClaimantRelationship = ClaimantRelationships.PolicyOwner,
        VerifiedContact = "0491 570 157 · grace.williams@example.com — the details verified before Mon 2 Nov 2026",
        RecentChange = "Mon 2 Nov 2026 10:41 — a caller who passed basic checks (name, date of birth, address) changed the mobile to 0491 570 159 and the email to grace.w.claims@example.net (\"I've lost my phone\"); no code was sent to the old mobile. 10:58 — portal login from a new device on a Sydney IP.",
        ClaimFormVerified = false,
        ClaimFormNote = "The claim form is in Grace's name, but it isn't signed by the verified policy owner.",
        AccountName = "G Williams Consulting Pty Ltd",
        Bsb = "062-***",
        Account = "****4471",
        AccountOnFileName = "Grace Williams",
        AccountOnFileLast4 = "2207",
        AccountNote = "A new account, opened Tue 3 Nov 2026.",
        PaymentRule = "Lyrebird pays trauma benefits to the policy owner, only into an account in the policy owner's name — never to a third party, or to a company unless the company owns the policy.",
        InsurerService = "Lyrebird Life's Claims Integrity team investigates suspected fraud and identity takeover; advisers can alert it about a policy.",
        ClaimPack =
        [
            new("Claim form", "referred", "In Grace's name, but not signed by the verified policy owner."),
            new("Oncologist's report — \"Dr A. Moretti, Eastern Oncology Group\"", "referred", "No histopathology. Sent to Financial Crime for document checks."),
            new("Histopathology report", "missing", "Lyrebird Life's cancer definition needs a specialist diagnosis supported by histopathology."),
            new("Driver licence", "referred", "Sent to Financial Crime for document checks."),
            new("Bank statement", "referred", "Account opened Tue 3 Nov 2026 in the name \"G Williams Consulting Pty Ltd\" — not the policy owner."),
        ],
        Vulnerable = true,
        VulnerabilityNote = "Grace may be the victim of an identity takeover. At her review with Amelia on 28 Sep 2026 she said she'd be in Italy from 20 Oct to 1 Dec 2026. Contact her only on the details verified before 2 Nov — her mobile, 0491 570 157.",
        FinancialCrimeNote = "Financial Crime (Felix's agent) rated the request HIGH risk and explained each flag: the oncologist's report has a pasted letterhead, edited dates, a font mismatch, a provider number in the wrong format and no histopathology; the driver licence photo looks edited; the bank account was opened on 3 Nov in a name that isn't the policy owner's. Nobody is accused — Grace may be the victim.",
        ActivityTimeline =
        [
            new("Mon 28 Sep 2026", "Annual review with Amelia — Grace says she'll be in Italy from 20 Oct to 1 Dec 2026."),
            new("Mon 2 Nov 2026 10:41", "A caller passes basic checks and changes the mobile to 0491 570 159 and the email to grace.w.claims@example.net — no code sent to the old mobile."),
            new("Mon 2 Nov 2026 10:58", "Client portal login from a new device on a Sydney IP."),
            new("Tue 3 Nov 2026", "Bank account ending 4471 opened in the name \"G Williams Consulting Pty Ltd\"."),
            new("Fri 6 Nov 2026 07:52", "Trauma claim for the full $200,000, marked urgent — about 93 hours after the contact change."),
        ],
        ProtectedNote = "Grace's $500,000 life cover and $200,000 trauma cover are safe.",
        ReferralNextStep = "Felix's agent checks the documents and the account activity. Theo decides on the block, restores Grace's verified contact details, locks portal access and decides whether to alert Lyrebird Life's fraud team (Claims Integrity). Cara calls Grace on her verified mobile, 0491 570 157 — never the new details.",
        OutcomeLabel = "Blocked",
        OutcomeSummary = "No claim lodged — $200,000 protected.",
        CaseFiles =
        [
            $"Case {GraceCase} — claim email and attachments",
            $"Case {GraceCase} — client activity log",
            $"Case {GraceCase} — document checks",
            $"Case {GraceCase} — travel notice",
        ],
        InputsHint = "A trauma claim for the full $200,000 arrived from new contact details, asking for payment into a new account in a company's name. Ben's agent checks the claimant before anything goes to Lyrebird Life.",
    };

    private static ClaimCaseProfile AdvicePriya() => new()
    {
        ProfileId = "advice-priya",
        Flow = ClaimFlows.NotAClaim,
        ClientName = "Priya Natarajan",
        ClientNumber = "CL-104582",
        ClientDisplay = "Priya Natarajan · CL-104582",
        NotAClaimNote = "Priya's case is new family protection advice after buying a home — there's nothing to claim. Penny's Paraplanning Agent works out how much cover her family needs, Ivy's Insurance Research Agent compares the insurers, and Uma's Underwriting Agent takes the application through underwriting.",
        NotAClaimLinks = AdviceLinks,
    };

    private static ClaimCaseProfile AdviceGeorge() => new()
    {
        ProfileId = "advice-george",
        Flow = ClaimFlows.NotAClaim,
        ClientName = "George Papadopoulos",
        ClientNumber = "CL-104611",
        ClientDisplay = "George Papadopoulos · CL-104611 (with Harpreet Kaur · CL-104612)",
        NotAClaimNote = "George and Harpreet's case is new business protection advice — buy-sell and key person cover for Northcote Custom Joinery. There's nothing to claim. Penny's Paraplanning Agent works out the cover, Ivy's Insurance Research Agent compares the insurers, and Uma's Underwriting Agent takes the applications through underwriting.",
        NotAClaimLinks = AdviceLinks,
    };

    private static readonly IReadOnlyList<ClaimLink> AdviceLinks =
    [
        new("Paraplanning Agent — Penny", "/agents/paraplanning"),
        new("Insurance Research Agent — Ivy", "/agents/insurance-research"),
        new("Underwriting Agent — Uma", "/agents/underwriting"),
    ];

    /// <summary>Cases opened live on the Onboarding page that don't match a scenario.</summary>
    private static ClaimCaseProfile Generic(CaseRecord r)
    {
        var client = string.IsNullOrWhiteSpace(r.ClientName) ? "Client" : r.ClientName.Trim();
        var number = string.IsNullOrWhiteSpace(r.ClientNumber) ? "client number to confirm" : r.ClientNumber.Trim();
        var amount = ParseAmount(r.Amount) ?? 0m;
        var policy = PolicyNumberRegex.Match(r.Cover ?? string.Empty);
        var prefix = policy.Success ? policy.Groups[1].Value.ToUpperInvariant() : string.Empty;
        var (insurer, pds) = prefix switch
        {
            "HL" => ("Harbourline Life", HarbourlinePds),
            "CR" => ("Corella Life", CorellaPds),
            "LB" => ("Lyrebird Life", LyrebirdPds),
            "NB" => ("Northbank Life", NorthbankPds),
            _ => ("the insurer on the policy schedule", ClaimsRuleTitle),
        };

        if (r.RequestType is RequestTypes.NewProtectionAdvice or RequestTypes.BusinessProtectionAdvice)
        {
            return new ClaimCaseProfile
            {
                ProfileId = "generic-advice",
                Flow = ClaimFlows.NotAClaim,
                ClientName = client,
                ClientNumber = number,
                ClientDisplay = $"{client} · {number}",
                NotAClaimNote = $"{FirstName(client)}'s case is new protection advice — there's nothing to claim. Penny's Paraplanning Agent works out the cover, and Uma's Underwriting Agent takes the application through underwriting.",
                NotAClaimLinks = AdviceLinks,
            };
        }

        var type = r.RequestType switch
        {
            RequestTypes.DeathClaim => ClaimCoverTypes.Death,
            RequestTypes.TraumaClaimBankChange => ClaimCoverTypes.Trauma,
            _ => ClaimCoverTypes.IncomeProtection,
        };
        var death = type == ClaimCoverTypes.Death;
        var bankChange = r.RequestType == RequestTypes.TraumaClaimBankChange;

        return new ClaimCaseProfile
        {
            ProfileId = "generic",
            Flow = bankChange ? ClaimFlows.Refer : ClaimFlows.Lodge,
            ClientName = client,
            ClientNumber = number,
            ClientDisplay = $"{client} · {number}",
            Insurer = insurer,
            InsurerPrefix = prefix,
            PolicyNumber = policy.Success ? policy.Value.ToUpperInvariant() : "policy number to confirm",
            PdsTitle = pds,
            PolicyOwner = death ? "the life insured" : client,
            LifeInsured = death ? "the life insured" : client,
            PolicyStartDisplay = "to confirm from the policy schedule",
            TimeInForceText = "to confirm from the policy schedule",
            Premiums = "to confirm with the insurer",
            PolicyConfirmed = false,
            CoverTypes = [type],
            CoverLines = [string.IsNullOrWhiteSpace(r.Cover) ? "Cover to confirm from the policy schedule" : r.Cover.Trim()],
            ClaimType = type,
            InsuredAmount = amount,
            BenefitPeriod = type == ClaimCoverTypes.IncomeProtection ? "as shown on the policy schedule" : null,
            Definition = "As defined in the insurer's PDS — confirm against the policy schedule.",
            DefinitionNote = "Confirm the medical or death evidence against the definition before lodging.",
            Exclusions = "To confirm from the policy schedule.",
            Beneficiaries = death ? [new(client, "claimant — confirm the nomination", 100m)] : Array.Empty<ClaimBeneficiary>(),
            EventDescription = string.IsNullOrWhiteSpace(r.RequestSummary) ? "See the Client Case." : r.RequestSummary.Trim(),
            Notification = $"{r.ReceivedDate} — {r.RequestType}",
            ClaimantName = client,
            ClaimantRelationship = death ? ClaimantRelationships.Beneficiary : ClaimantRelationships.PolicyOwner,
            VerifiedContact = string.Join(" · ", new[] { r.ClientPhone, r.ClientEmail }.Where(s => !string.IsNullOrWhiteSpace(s))),
            RecentChange = bankChange ? "The request asks to change the bank details as part of the claim." : null,
            ClaimFormNote = "Claim form to be signed by the claimant.",
            AccountName = client,
            PaymentRule = "Benefits are paid only into an account in the payee's own name — never to a third party.",
            EarningsPeriod = "12 months before the disability",
            ClaimPack =
            [
                new("Claim form", "to-come", "Signed by the claimant."),
                new("Proof of identity", "to-come", "Checked by Financial Crime."),
                new(death ? "Certified copy of the death certificate" : "Medical evidence", "to-come", death ? "Certified copy." : "Treating doctor's or specialist's report."),
                new("Bank details", "to-come", "An account in the claimant's own name."),
            ],
            Vulnerable = true,
            VulnerabilityNote = "A claim usually follows an illness, an injury or a death — check whether the client needs a person first.",
            VulnerabilityApprovalReason = death ? "Bereaved family — a person approves every message to the family." : null,
            AmountClaimed = bankChange && amount > 0 ? amount : null,
            ReferralNextStep = bankChange ? "Financial Crime verifies the bank change on details we already trust before anything is lodged." : null,
            InputsHint = "Defaults are worked out from the case. Confirm the policy schedule, the claimant and the bank details before engaging the agent.",
        };
    }

    // ── Claimant: entitlement, identity, contact details, bank account ──

    public static ClaimantCheckResult ValidateClaimant(
        ClaimCaseProfile p,
        string? claimantName = null,
        string? relationship = null,
        string? accountName = null,
        string? bsb = null,
        string? account = null)
    {
        var name = Pick(claimantName, p.ClaimantName);
        var rel = ClaimantRelationships.Normalize(Pick(relationship, p.ClaimantRelationship));
        var acctName = Pick(accountName, p.AccountName);
        var masked = MaskBankDetails(Pick(bsb, p.Bsb), Pick(account, p.Account));
        var key = NormalizeName(name);
        var type = ClaimCoverTypes.Normalize(p.ClaimType);
        var coverName = ClaimCoverTypes.CoverName(type);
        var checks = new List<ClaimCheckLine>();
        var redFlags = new List<string>();

        // 1. Entitlement — living benefits go to the policy owner; a death
        //    benefit goes to the nominated beneficiaries (or, without a
        //    valid nomination, to the estate).
        bool entitled;
        string entitlementValue;
        string entitlementNote;
        if (type == ClaimCoverTypes.Death)
        {
            var beneficiary = key.Length == 0 ? null : p.Beneficiaries.FirstOrDefault(b => NormalizeName(b.Name) == key);
            var nominated = string.Join(", ", p.Beneficiaries.Select(b => $"{b.Name} {FormatPercent(b.SharePercent)}"));
            if (beneficiary is not null && rel != ClaimantRelationships.Estate)
            {
                entitled = true;
                entitlementValue = $"Nominated beneficiary · {FormatPercent(beneficiary.SharePercent)}";
                entitlementNote = $"{name} is the nominated beneficiary ({FormatPercent(beneficiary.SharePercent)}) on {p.PolicyNumber}, so the benefit is paid directly to {FirstName(name)} — not through {Possessive(FirstName(p.LifeInsured))} estate.";
            }
            else if (rel == ClaimantRelationships.Estate && p.Beneficiaries.Count == 0)
            {
                entitled = true;
                entitlementValue = "Estate";
                entitlementNote = "There's no nominated beneficiary, so the benefit is paid to the estate — the executor or administrator makes the claim.";
            }
            else
            {
                entitled = false;
                entitlementValue = "Not entitled";
                entitlementNote = p.Beneficiaries.Count > 0
                    ? $"{name} isn't a nominated beneficiary on {p.PolicyNumber} (nominated: {nominated}). The benefit is paid to the nominated beneficiaries directly{(rel == ClaimantRelationships.Estate ? ", not to the estate" : string.Empty)}."
                    : $"{name} isn't recorded on {p.PolicyNumber}. Without a nomination, the benefit is paid to the estate — the executor or administrator makes the claim.";
            }
        }
        else if (key.Length > 0 && key == NormalizeName(p.PolicyOwner))
        {
            entitled = true;
            entitlementValue = NormalizeName(p.PolicyOwner) == NormalizeName(p.LifeInsured) ? "Policy owner and life insured" : "Policy owner";
            entitlementNote = $"{name} owns {p.PolicyNumber}, and {coverName} benefits are paid to the policy owner."
                + (p.SupportPerson is null ? string.Empty : $" {p.SupportPerson} — the claim stays {Possessive(FirstName(name))}.");
        }
        else
        {
            entitled = false;
            entitlementValue = "Not the policy owner";
            entitlementNote = $"{(name.Length == 0 ? "The claimant" : name)} isn't the policy owner ({p.PolicyOwner}). {Capitalise(coverName)} benefits are paid to the policy owner — a support person can help, but the claim is {Possessive(FirstName(p.PolicyOwner))}.";
        }
        checks.Add(new("entitlement", "Entitled to claim", entitled ? "pass" : "fail", entitlementValue, entitlementNote, p.PdsTitle));

        // 2. Claim form signed by the verified claimant.
        if (!p.ClaimFormVerified)
            redFlags.Add("The claim form isn't signed by the verified policy owner.");
        checks.Add(new("claim-form", "Claim form and identity", p.ClaimFormVerified ? "pass" : "fail",
            p.ClaimFormVerified ? "Verified claimant" : "Not the verified policy owner",
            p.ClaimFormNote, InGoodOrderTitle));

        // 3. Contact details — a recent change is a red flag.
        var contactTrusted = p.RecentChange is null;
        if (!contactTrusted)
            redFlags.Add("The contact details were changed shortly before the claim arrived.");
        checks.Add(new("contact", "Contact details", contactTrusted ? "pass" : "fail",
            contactTrusted ? "Verified · unchanged" : "Changed just before the claim",
            contactTrusted
                ? p.VerifiedContact
                : $"{p.RecentChange} Details verified before the change: {p.VerifiedContact}. Basic checks alone aren't enough — a change is confirmed with a code to, or a call-back on, the verified mobile or email held before the change.",
            FinancialCrimeGuideTitle));

        // 4. Bank account in the claimant's own name.
        var acctKey = NormalizeName(acctName);
        var ownName = acctKey.Length > 0 && acctKey == key;
        var last4 = Last4Digits(masked);
        bool? matchesFile = p.AccountOnFileLast4 is { Length: > 0 } fileLast4 && last4 is not null
            ? last4 == fileLast4 && (p.AccountOnFileName is null || NormalizeName(p.AccountOnFileName) == acctKey)
            : null;
        var bankChange = matchesFile == false;
        var onFile = p.AccountOnFileLast4 is null ? null : $"{p.AccountOnFileName ?? p.ClientName} ****{p.AccountOnFileLast4}";
        string bankStatus;
        string bankValue;
        string bankDetail;
        if (!entitled)
        {
            bankStatus = "na";
            bankValue = "Confirm the claimant first";
            bankDetail = $"{acctName} — {masked}. The account must be in the entitled claimant's own name.";
        }
        else if (!ownName)
        {
            bankStatus = "fail";
            bankValue = "Not in the claimant's name";
            bankDetail = $"\"{acctName}\" — {masked}{(p.AccountNote is null || !bankChange ? string.Empty : $" ({Uncapitalise(p.AccountNote.TrimEnd('.'))})")}. "
                + (p.PaymentRule ?? "Benefits are paid only into an account in the payee's own name.")
                + (onFile is null ? string.Empty : $" The account on file is {onFile}.")
                + " A payment account in another name goes to Financial Crime, and any bank-detail change needs Theo's approval after Financial Crime confirms it on the verified contact details.";
            redFlags.Add($"The bank account is in the name \"{acctName}\", not the claimant's own name.");
        }
        else if (bankChange)
        {
            bankStatus = "warn";
            bankValue = "New account for this claim";
            bankDetail = $"{acctName} — {masked} is in {Possessive(FirstName(name))} own name, but it isn't the account on file ({onFile}). "
                + "Hold it and call the client back on the verified number held before any change; Theo approves the bank-detail change after Financial Crime confirms it.";
        }
        else
        {
            bankStatus = "pass";
            bankValue = "In the claimant's own name";
            bankDetail = $"{acctName} — {masked}." + (p.AccountNote is null ? string.Empty : $" {p.AccountNote}")
                + (p.PaymentRule is null ? string.Empty : $" {p.PaymentRule}");
        }
        checks.Add(new("bank", "Bank account", bankStatus, bankValue, bankDetail, p.PdsTitle.Length > 0 ? $"{p.PdsTitle} · {InGoodOrderTitle}" : InGoodOrderTitle));

        // 5. Supporting documents.
        var referred = p.ClaimPack.Where(i => i.Status == "referred").Select(i => i.Document).ToList();
        var outstanding = p.ClaimPack.Where(i => i.Status is "to-come" or "missing").Select(i => i.Document).ToList();
        if (referred.Count > 0)
            redFlags.Add("Supporting documents need Financial Crime's document checks.");
        checks.Add(new("documents", "Supporting documents",
            referred.Count > 0 ? "fail" : outstanding.Count > 0 ? "warn" : "pass",
            referred.Count > 0 ? "Referred to Financial Crime" : outstanding.Count > 0 ? "Still to come" : "Checked",
            referred.Count > 0
                ? $"Referred for document checks: {string.Join("; ", referred)}."
                : outstanding.Count > 0
                    ? $"Still needed: {string.Join("; ", outstanding)}."
                    : p.FinancialCrimeNote ?? "The claim pack is complete.",
            FinancialCrimeGuideTitle));

        var refer = !contactTrusted || !p.ClaimFormVerified || referred.Count > 0 || (entitled && !ownName);
        return new ClaimantCheckResult(
            name,
            rel,
            ClaimantRelationships.Label(rel),
            entitled,
            entitlementNote,
            p.ClaimFormVerified,
            contactTrusted,
            acctName,
            masked,
            ownName,
            matchesFile,
            bankChange || (entitled && !ownName),
            refer,
            redFlags,
            checks);
    }

    // ── Policy: in force, cover and definition, waiting period, exclusions ─

    public static ClaimPolicyCheckResult CheckPolicy(ClaimCaseProfile p, string? claimType = null, DateOnly? eventDate = null)
    {
        var type = ClaimCoverTypes.Normalize(Pick(claimType, p.ClaimType));
        var coverName = ClaimCoverTypes.CoverName(type);
        var coverMatches = p.CoverTypes.Count == 0 || p.CoverTypes.Contains(type);
        var checks = new List<ClaimCheckLine>();
        var confirmed = p.PolicyConfirmed;

        var inForce = p.InForce && p.PremiumsPaid;
        checks.Add(new("in-force", "Policy in force",
            !confirmed ? "warn" : inForce ? "pass" : "fail",
            !confirmed ? "To confirm with the insurer" : inForce ? "In force · premiums paid" : "Check the policy status",
            $"{p.Insurer} {p.PolicyNumber}, held {(p.HeldOutsideSuper ? "outside" : "inside")} super, since {p.PolicyStartDisplay}. Premiums: {p.Premiums}.",
            p.PdsTitle));

        var definitionMet = coverMatches && p.DefinitionMet;
        checks.Add(new("definition", "Cover and definition",
            coverMatches && !confirmed ? "warn" : definitionMet ? "pass" : "fail",
            !coverMatches ? $"No {coverName} on the policy" : !confirmed ? "To confirm against the PDS" : p.DefinitionMet ? "Definition met" : "Definition not shown",
            !coverMatches
                ? $"{p.PolicyNumber} doesn't hold {coverName} — check the claim type."
                : $"{p.Definition} {p.DefinitionNote}".Trim(),
            p.PdsTitle));

        if (type == ClaimCoverTypes.IncomeProtection)
        {
            var start = eventDate ?? p.EventDate;
            checks.Add(start is { } s && p.WaitingPeriodDays is { } days
                ? new ClaimCheckLine("waiting", "Waiting period", "pass", $"{days} days",
                    $"{days} days from {FormatDate(s)} — the benefit accrues from {FormatDate(s.AddDays(days))}. Benefit period {p.BenefitPeriod ?? "as scheduled"}.",
                    p.PdsTitle)
                : new ClaimCheckLine("waiting", "Waiting period", "warn",
                    p.WaitingPeriodDays is { } d ? $"{d} days" : "To confirm",
                    "Confirm the waiting period from the policy schedule and the date the disability started — the waiting period runs from then.", p.PdsTitle));
        }
        else
        {
            checks.Add(new("waiting", "Waiting period", "na", "Not applicable",
                $"A {ClaimCoverTypes.ClaimLabel(type).ToLowerInvariant()} is a lump sum — there's no waiting period to serve.", p.PdsTitle));
        }

        checks.Add(confirmed
            ? new ClaimCheckLine("exclusions", "Exclusions", "pass", "None apply", p.Exclusions, p.PdsTitle)
            : new ClaimCheckLine("exclusions", "Exclusions", "warn", "To confirm", p.Exclusions, p.PdsTitle));

        var asAt = p.LodgementDate ?? MelbourneToday();
        var timeInForce = p.TimeInForceText
            ?? (p.PolicyStart is { } ps ? DescribeDuration(ps, asAt) : "to confirm from the policy schedule");
        bool? past = p.PolicyStart is { } start2 ? start2.AddYears(MisrepresentationWindowYears) <= asAt : null;
        checks.Add(new("time-in-force", "Time in force", past == true ? "pass" : "warn",
            past switch { true => $"{timeInForce} — past the {MisrepresentationWindowYears}-year window", false => $"{timeInForce} — within {MisrepresentationWindowYears} years", _ => "To confirm" },
            MisrepresentationDetail(past, timeInForce, p.CoverTypes.Contains(ClaimCoverTypes.Death)),
            DisclosureRuleTitle));

        if (!string.IsNullOrWhiteSpace(p.OtherCover))
        {
            checks.Add(new("other-cover", "Other cover", "pass", "Found — tell the claimant",
                p.OtherCover!, InGoodOrderTitle));
        }

        return new ClaimPolicyCheckResult(
            type,
            p.InForce,
            p.PremiumsPaid,
            coverMatches,
            definitionMet,
            timeInForce,
            past == true,
            checks,
            p.ClaimPack,
            ClaimsRuleTitle);
    }

    // ── Benefit calculation ─────────────────────────────────────────────

    public static ClaimBenefitCalculation CalculateBenefit(
        ClaimCaseProfile p,
        string? claimType = null,
        decimal? insuredAmount = null,
        decimal? annualEarnings = null,
        int? waitingPeriodDays = null,
        DateOnly? eventDate = null,
        decimal? sharePercent = null,
        string? payeeName = null,
        string? maskedAccount = null)
    {
        var type = ClaimCoverTypes.Normalize(Pick(claimType, p.ClaimType));
        var insured = Round2(Math.Max(0m, insuredAmount ?? p.InsuredAmount));
        var payee = Pick(payeeName, p.ClaimantName);
        var account = Pick(maskedAccount, MaskBankDetails(p.Bsb, p.Account));
        var steps = new List<ClaimBenefitStep>();
        var notes = new List<string>();

        if (type == ClaimCoverTypes.IncomeProtection)
        {
            var rate = p.ReplacementRatePercent;
            var earnings = annualEarnings ?? p.AnnualEarnings;
            steps.Add(new("Insured monthly benefit (policy schedule)", "input", insured, FormatMoney(insured)));

            decimal benefit;
            decimal? monthly = null;
            decimal? cap = null;
            var limited = false;
            if (earnings is { } e && e > 0m)
            {
                monthly = Round2(e / 12m);
                cap = Round2(monthly.Value * rate / 100m);
                benefit = Math.Min(insured, cap.Value);
                limited = cap.Value < insured;
                steps.Add(new($"Earnings in the {p.EarningsPeriod ?? "12 months before the disability"}", "input", e, FormatMoney(e)));
                steps.Add(new("Average monthly earnings (÷ 12)", "step", monthly, FormatMoney(monthly.Value)));
                steps.Add(new($"{FormatPercent(rate)} of average monthly earnings", "step", cap, FormatMoney(cap.Value)));
                steps.Add(new("Monthly benefit — the lesser of the two", "total", benefit, $"{FormatMoney(benefit)} a month"));
            }
            else
            {
                benefit = insured;
                steps.Add(new("Earnings in the 12 months before the disability", "note", null, "To confirm — needed before the benefit is final"));
                steps.Add(new("Monthly benefit, before the earnings check", "total", benefit, $"{FormatMoney(benefit)} a month"));
                notes.Add($"The benefit can't be more than {FormatPercent(rate)} of average monthly earnings in the 12 months before the disability — collect the earnings evidence before lodging.");
            }

            var days = waitingPeriodDays ?? p.WaitingPeriodDays;
            var start = eventDate ?? p.EventDate;
            DateOnly? accrual = start is { } from && days is { } waiting ? from.AddDays(waiting) : null;
            string? firstPayment = accrual is { } a ? $"about {FormatDayMonthYear(a.AddMonths(1))}" : null;
            if (start is { } s && accrual is { } acc)
                steps.Add(new($"Waiting period — {days} days from {FormatDate(s)}", "note", null, $"Accrues from {FormatDate(acc)}"));
            else
                steps.Add(new("Waiting period", "note", null, "Confirm the waiting period and the date the disability started"));
            steps.Add(new("Paid", "note", null, firstPayment is null ? "Monthly in arrears" : $"Monthly in arrears — first payment {firstPayment}"));

            if (limited)
                notes.Add($"The benefit is {FormatMoney(insured - benefit)} a month less than the {FormatMoney(insured)} insured, because it follows {Possessive(FirstName(payee))} earnings in the 12 months before the disability.");
            if (!string.IsNullOrWhiteSpace(p.BenefitPeriod))
                notes.Add($"Paid while {FirstName(payee)} stays totally disabled, for up to the benefit period ({p.BenefitPeriod}).");

            return new ClaimBenefitCalculation(
                type, "income", "Income protection benefit", steps, benefit, $"{FormatMoney(benefit)} a month", insured,
                limited, monthly, cap, accrual, firstPayment,
                $"{payee} — monthly, into {account}",
                TaxTreatment(type, p.HeldOutsideSuper),
                notes,
                p.PdsTitle.Length > 0 ? $"{ClaimsRuleTitle} · {p.PdsTitle}" : ClaimsRuleTitle,
                TaxRuleTitle);
        }

        var share = Math.Clamp(sharePercent ?? p.BeneficiarySharePercent, 0m, 100m);
        var lumpSum = type == ClaimCoverTypes.Death ? Round2(insured * share / 100m) : insured;
        steps.Add(new($"Sum insured — {ClaimCoverTypes.CoverName(type)}", "input", insured, FormatMoney(insured)));
        if (type == ClaimCoverTypes.Death)
            steps.Add(new($"{Possessive(payee)} share as nominated beneficiary ({FormatPercent(share)})", "step", lumpSum, FormatMoney(lumpSum)));
        steps.Add(new("Expected benefit — one lump sum", "total", lumpSum, FormatMoney(lumpSum)));

        var paidTo = type == ClaimCoverTypes.Death
            ? $"{payee} directly — not through {Possessive(FirstName(p.LifeInsured))} estate — into {account}"
            : $"{payee}, the policy owner — into {account}";
        if (type == ClaimCoverTypes.Death && share < 100m)
            notes.Add($"The other {FormatPercent(100m - share)} is paid to the other nominated beneficiaries.");

        return new ClaimBenefitCalculation(
            type, "lump-sum", $"{Capitalise(ClaimCoverTypes.CoverName(type))} benefit", steps, lumpSum, $"{FormatMoney(lumpSum)} lump sum", insured,
            false, null, null, null, null,
            paidTo,
            TaxTreatment(type, p.HeldOutsideSuper),
            notes,
            p.PdsTitle.Length > 0 ? $"{ClaimsRuleTitle} · {p.PdsTitle}" : ClaimsRuleTitle,
            TaxRuleTitle);
    }

    // ── Which path the claim takes, and who must approve ────────────────

    public static string DecideFlow(ClaimCaseProfile p, ClaimantCheckResult claimant, ClaimPolicyCheckResult policy)
    {
        if (p.Flow == ClaimFlows.NotAClaim) return ClaimFlows.NotAClaim;
        if (claimant.ReferToFinancialCrime || p.Flow == ClaimFlows.Refer) return ClaimFlows.Refer;
        if (!claimant.Entitled || !policy.CoverMatches) return ClaimFlows.Hold;
        return ClaimFlows.Lodge;
    }

    public static ClaimApprovalRulesResult CheckApprovalRules(
        ClaimCaseProfile p,
        string? claimType,
        ClaimantCheckResult claimant,
        string flow,
        string? benefitDisplay = null,
        bool coverMatches = true)
    {
        var type = ClaimCoverTypes.Normalize(Pick(claimType, p.ClaimType));
        var reasons = new List<string>();
        string recommendation;
        string note;

        switch (flow)
        {
            case ClaimFlows.Refer:
                reasons.Add("Blocking a claim on financial-crime grounds is the Team Leader's decision — nothing is lodged while the claim is referred.");
                if (claimant.BankDetailChange)
                    reasons.Add("The claim asks for payment into an account that isn't on file in the claimant's own name — any change to bank details needs Theo's approval after Financial Crime confirms it on the verified contact details.");
                reasons.Add("Referring suspected fraud to an insurer also needs the Team Leader's approval — Financial Crime prepares it.");
                recommendation = "Don't lodge. Block the claim and the bank-detail change, and refer the case to Financial Crime (Felix). Contact the client only on the details verified before the change.";
                note = "Never accuse the client — they may be the victim. The block is a person's decision.";
                break;

            case ClaimFlows.Hold:
                if (!coverMatches)
                    reasons.Add($"The policy doesn't hold {ClaimCoverTypes.CoverName(type)} — confirm the claim type against the cover before anything is lodged.");
                if (!claimant.Entitled && coverMatches)
                    reasons.Add("The person claiming isn't entitled to the benefit — confirm who can claim with Amelia before anything is lodged.");
                if (reasons.Count == 0)
                    reasons.Add("The claim isn't ready to lodge — confirm the details with Amelia first.");
                recommendation = "Don't lodge yet. Ben confirms the details with Amelia and the client first.";
                note = "Nothing goes to the insurer until the claimant and the cover are confirmed.";
                break;

            default:
                reasons.Add("Every claim lodgement needs a person's approval before it goes to the insurer.");
                if (ClaimCoverTypes.IsDeathOrTerminal(type))
                    reasons.Add($"{ClaimCoverTypes.ClaimLabel(type)} — every death or terminal illness claim needs the Team Leader's approval.");
                if (claimant.BankDetailChange)
                    reasons.Add("Bank-detail change — the claim uses an account that isn't on file, so Theo approves it after Financial Crime confirms it on the client's verified contact details.");
                if (p.Vulnerable && !string.IsNullOrWhiteSpace(p.VulnerabilityApprovalReason))
                    reasons.Add(p.VulnerabilityApprovalReason!);
                recommendation = $"Lodge the {ClaimCoverTypes.ClaimLabel(type).ToLowerInvariant()} with {p.Insurer}"
                    + (string.IsNullOrWhiteSpace(benefitDisplay) ? string.Empty : $" — expected benefit {benefitDisplay}")
                    + ", once Theo approves.";
                note = "Ben's agent lodges only after a person approves in Microsoft Teams.";
                break;
        }

        return new ClaimApprovalRulesResult(
            Required: flow != ClaimFlows.Hold,
            Reasons: reasons,
            Approver: TeamLeaderApprover,
            PersonFirst: p.Vulnerable,
            DoNotLodge: flow != ClaimFlows.Lodge,
            Recommendation: recommendation,
            Note: note,
            RuleCited: AuthorityRuleTitle);
    }

    /// <summary>Fill the lodgement-reference token in a claim-update draft.</summary>
    public static string? RenderClaimUpdate(string? draft, ClaimApprovalRecord? record)
    {
        if (string.IsNullOrEmpty(draft)) return draft;
        var value = record is { Status: ClaimApprovalStatus.Lodged, LodgementReference: { Length: > 0 } reference }
            ? reference
            : "[added once the claim is lodged]";
        return draft.Replace(ReferenceToken, value, StringComparison.Ordinal);
    }

    // ── Verified rules (FY2026-27) ──────────────────────────────────────
    // Sources: Life Insurance Code of Practice (CALI edition in force from
    // 1 Mar 2025) cl 5.5–5.63 and 6.13; APRA's individual disability income
    // insurance measures (policies issued from 1 Oct 2021); Insurance
    // Contracts Act 1984 s29; ITAA 1997 s118-300, s118-37 and s302-60.

    public const int MisrepresentationWindowYears = 3;

    /// <summary>Accepted lump-sum claims at or above this amount: the insurer suggests independent financial advice (Code cl 5.61).</summary>
    public const decimal IndependentAdviceThreshold = 25_000m;

    /// <summary>
    /// The insurer's timeframes under the Code for the claim type (due dates
    /// counted from the date the insurer receives the claim), the insurer's
    /// own service level from its PDS, and Life Advice Guru's service standards.
    /// </summary>
    public static IReadOnlyList<ClaimFact> Timeframes(
        string? claimType,
        DateOnly? claimReceived = null,
        DateOnly? waitingPeriodEnds = null,
        decimal? benefit = null,
        ClaimCaseProfile? profile = null)
    {
        var type = ClaimCoverTypes.Normalize(claimType);
        var facts = new List<ClaimFact>();
        string Due(DateOnly? from, Func<DateOnly, DateOnly> add) => from is { } d ? $" — by {FormatDate(add(d))}" : string.Empty;

        facts.Add(new("First contact (cl 5.5)",
            "Within 10 business days of receiving the claim, the insurer explains the Code, the cover, any waiting period, the benefits and the claims process"
            + Due(claimReceived, d => AddBusinessDays(d, 10)) + "."));
        if (type == ClaimCoverTypes.IncomeProtection)
        {
            var start = claimReceived is { } r && waitingPeriodEnds is { } w ? (w > r ? w : r) : claimReceived ?? waitingPeriodEnds;
            facts.Add(new("Decision (cl 5.48)",
                "Income-related claims: within 2 months of the claim received date, or of the end of the waiting period if that's later"
                + Due(start, d => d.AddMonths(2)) + "."));
        }
        else
        {
            facts.Add(new("Decision (cl 5.49)",
                "Lump-sum claims (death, terminal illness, trauma, TPD): within 6 months of the claim received date"
                + Due(claimReceived, d => d.AddMonths(6)) + "."));
        }
        facts.Add(new("Written decision (cl 5.50)", "Within 15 business days of the insurer having all the information it needs."));
        facts.Add(new("Updates (cl 5.6, 5.7, 5.55)", "Progress updates at least every 20 business days until the decision; replies to requests for information within 10 business days; medical and financial reports asked for within 20 business days."));
        if (type == ClaimCoverTypes.IncomeProtection)
        {
            facts.Add(new("Payments (cl 5.63)", "Once accepted, income protection benefits are paid by the due date, or within 5 business days of the insurer receiving all the information."));
            if (profile?.Vulnerable == true)
                facts.Add(new("Urgent access (cl 6.25–6.27)", "If money is tight, the insurer responds to a request for urgent access to benefits within 5 business days of receiving the evidence."));
        }
        else if (benefit is null || benefit >= IndependentAdviceThreshold)
        {
            facts.Add(new("Independent advice (cl 5.61)", $"When a lump-sum claim of {FormatMoney(IndependentAdviceThreshold)} or more is accepted, the insurer suggests the claimant gets independent financial advice."));
        }
        facts.Add(new("Delays (cl 5.59, 5.60)", "If a delay is outside the insurer's control, it tells the claimant in writing before the deadline, updates them every 20 business days, and escalates past 12 months."));
        if (!string.IsNullOrWhiteSpace(profile?.InsurerService))
            facts.Add(new($"{profile!.Insurer} (PDS)", $"{profile.InsurerService} ({profile.PdsTitle})"));
        facts.Add(new("Life Advice Guru standards",
            "Approved claims lodged the same or next business day; the client or family updated at least every 10 business days while the claim is open; "
            + "Theo is told at once if the insurer misses a Code timeframe or its own service standard (Matrix — authority, approvals and escalation)."));
        return facts;
    }

    /// <summary>Plain-English tax treatment of the benefit, outside or inside super.</summary>
    public static string TaxTreatment(string? claimType, bool heldOutsideSuper)
    {
        var type = ClaimCoverTypes.Normalize(claimType);
        if (!heldOutsideSuper)
            return "Held inside super: the benefit is paid to the fund's trustee and taxed under the super rules when it's paid out — confirm with the fund.";
        return type switch
        {
            ClaimCoverTypes.IncomeProtection =>
                "Taxable — income protection benefits replace income, so they're assessable income, whether paid monthly or as a lump sum. Self-employed clients confirm the treatment with their accountant.",
            ClaimCoverTypes.Death =>
                "Generally not taxable — life cover paid outside super is exempt from capital gains tax for the original owner or someone who acquired their interest in the policy for no consideration, such as a nominated beneficiary (ITAA 1997 s118-300).",
            ClaimCoverTypes.TerminalIllness =>
                "Generally not taxable — a terminal illness benefit is treated as an early payment of the death benefit (TD 2007/4), so the life-policy exemption in ITAA 1997 s118-300 applies.",
            _ =>
                "Generally not taxable outside super — a lump sum for an illness or injury suffered personally by the life insured (or a relative) may be exempt under ITAA 1997 s118-37.",
        };
    }

    /// <summary>What the time in force means for a non-fraudulent misrepresentation at application (ICA s29).</summary>
    public static string MisrepresentationDetail(bool? pastWindow, string timeInForce, bool policyHasDeathCover)
    {
        return pastWindow switch
        {
            true when policyHasDeathCover =>
                $"In force {timeInForce}. More than 3 years after the contract was entered into, the insurer can't avoid the policy for a non-fraudulent misrepresentation at application (Insurance Contracts Act 1984 s29(3)), and on a policy with death cover it can't reduce the sum insured either (s29(10)). Only fraud would let it avoid the policy (s29(2)).",
            true =>
                $"In force {timeInForce}. More than 3 years after the contract was entered into, the insurer can't avoid the policy for a non-fraudulent misrepresentation at application (Insurance Contracts Act 1984 s29(3)). On income protection, trauma or TPD it could still vary the terms to where it would have been (s29(6)–(8)) if a non-disclosure came to light; none is recorded on the file.",
            false =>
                $"In force {timeInForce} — within 3 years of the contract being entered into. If a non-fraudulent misrepresentation came to light and the insurer wouldn't have offered the cover on any terms, it could avoid the policy (Insurance Contracts Act 1984 s29(3)); otherwise it may vary it (s29(4)–(8)). Ben's agent checks the application disclosures before lodging.",
            _ =>
                "Confirm the start date from the policy schedule — the 3-year window in Insurance Contracts Act 1984 s29(3) decides what the insurer can do about a non-fraudulent misrepresentation.",
        };
    }

    /// <summary>The income protection benefit basis: the policy term, and APRA's limits it sits within.</summary>
    public static string IncomeBenefitBasis(ClaimCaseProfile p) =>
        $"{p.Insurer}'s policy pays the lesser of the insured monthly benefit and {FormatPercent(p.ReplacementRatePercent)} of average monthly earnings in the 12 months before the disability ({p.PdsTitle}). "
        + "That sits within APRA's measures for income protection issued from 1 Oct 2021: the benefit is based on earnings at the time of claim (no more than 12 months old, averaged for variable incomes), "
        + "and all benefits combined can't exceed 90% of earnings for the first 6 months of a claim and 70% after that.";

    private const string SuperCoverNoteRobert =
        "Mei's Riverbend Super account also had default death cover of $180,000. That's a separate claim with the fund's trustee, paid under the fund's rules — here to Robert as her spouse. "
        + "A lump-sum death benefit from super paid to a death-benefits dependant, which includes a spouse, is tax-free (ITAA 1997 s302-60 and s302-195). "
        + "Ben's agent has prepared the checklist — the fund's claim form, the certified death certificate, Robert's certified ID, evidence of the relationship if the fund asks, and an account in Robert's own name — so Cara can send it and Ben can help him with the form.";

    // ── Formatting, dates, masking and parsing helpers ──────────────────

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>AUD with cents only when there are cents: "$5,600", "$1,000,000", "$2,333.33".</summary>
    public static string FormatMoney(decimal amount)
    {
        var rounded = Round2(amount);
        return rounded == decimal.Truncate(rounded)
            ? rounded.ToString("C0", AuCulture)
            : rounded.ToString("C2", AuCulture);
    }

    public static string FormatPercent(decimal percent) =>
        percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>"Thu 12 Nov 2026".</summary>
    public static string FormatDate(DateOnly date) =>
        date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"13 Dec 2026".</summary>
    public static string FormatDayMonthYear(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"12/11/2026".</summary>
    public static string FormatShortDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy", "yyyy-MM-dd",
        "d MMM yyyy", "ddd d MMM yyyy", "d MMMM yyyy", "ddd, d MMM yyyy",
    ];

    /// <summary>Australian dates ("14/10/2026", "14 Oct 2026") or ISO dates.</summary>
    public static DateOnly? ParseDate(string? text)
    {
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        return DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
            ? d
            : null;
    }

    /// <summary>Today in Melbourne.</summary>
    public static DateOnly MelbourneToday()
    {
        foreach (var id in new[] { "Australia/Melbourne", "AUS Eastern Standard Time" })
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Try the next id.
            }
        }
        return DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(10)).DateTime);
    }

    /// <summary>Adds business days (Monday to Friday).</summary>
    public static DateOnly AddBusinessDays(DateOnly date, int days)
    {
        var d = date;
        var added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) added++;
        }
        return d;
    }

    /// <summary>"3 years 7 months".</summary>
    public static string DescribeDuration(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        if (to.Day < from.Day) months--;
        months = Math.Max(0, months);
        var years = months / 12;
        var rest = months % 12;
        var parts = new List<string>();
        if (years > 0) parts.Add(years == 1 ? "1 year" : $"{years} years");
        if (rest > 0 || years == 0) parts.Add(rest == 1 ? "1 month" : $"{rest} months");
        return string.Join(" ", parts);
    }

    public static string DigitsOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static string? Last4Digits(string? account)
    {
        var digits = DigitsOnly(account);
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    /// <summary>"033123" or "033-***" → "033-***".</summary>
    public static string MaskBsb(string? bsb)
    {
        var raw = (bsb ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        var digits = DigitsOnly(raw);
        return digits.Length >= 3 ? $"{digits[..3]}-***" : "***-***";
    }

    /// <summary>"12345120" or "****5120" → "****5120".</summary>
    public static string MaskAccount(string? account)
    {
        var raw = (account ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        var last4 = Last4Digits(raw);
        return last4 is null ? "****" : "****" + last4;
    }

    /// <summary>"BSB 033-*** ****5120" — never the full numbers.</summary>
    public static string MaskBankDetails(string? bsb, string? account)
    {
        var b = MaskBsb(bsb);
        var a = MaskAccount(account);
        if (b.Length == 0 && a.Length == 0) return "account to confirm";
        return $"BSB {(b.Length == 0 ? "***-***" : b)} {(a.Length == 0 ? "****" : a)}";
    }

    /// <summary>Mask free-text bank details passed in by an agent; already-masked text passes through.</summary>
    public static string MaskAccountText(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0) return "(not provided)";
        if (raw.Contains('*') || !raw.Any(char.IsDigit)) return raw;
        var last4 = Last4Digits(raw);
        return last4 is null ? "****" : "****" + last4;
    }

    /// <summary>Lower-case letters and digits only, with "&amp;" read as "and".</summary>
    public static string NormalizeName(string? value)
    {
        var s = (value ?? string.Empty).ToLowerInvariant().Replace("&", " and ");
        return new string(s.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>First amount in a case's headline text, e.g. "$7,000 a month insured" → 7000.</summary>
    public static decimal? ParseAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = AmountRegex.Match(text);
        if (!match.Success) return null;
        return decimal.TryParse(match.Value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    public static string FirstName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return "the client";
        if (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) return trimmed;
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    public static string Possessive(string name) => $"{name}'s";

    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Uncapitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string Pick(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();
}

/// <summary>Claim details confirmed on the Claims page (or passed by the agent). Null fields fall back to the case.</summary>
public sealed record ClaimRunInputs
{
    public string? ClaimType { get; init; }
    public string? ClaimantName { get; init; }
    public string? ClaimantRelationship { get; init; }
    public string? AccountName { get; init; }
    public string? Bsb { get; init; }
    public string? Account { get; init; }
    public string? EventDate { get; init; }
    public decimal? InsuredAmount { get; init; }
    public decimal? AnnualEarnings { get; init; }
    public int? WaitingPeriodDays { get; init; }
    public decimal? BeneficiarySharePercent { get; init; }
}

/// <summary>One deterministic pass over a claim: every check, the benefit, the path and the approvals.</summary>
public sealed class ClaimRun
{
    public required CaseRecord Case { get; init; }
    public required ClaimCaseProfile Profile { get; init; }
    public required string ClaimType { get; init; }
    public required DateOnly? EventDate { get; init; }
    public required DateOnly LodgementDate { get; init; }
    public required ClaimantCheckResult Claimant { get; init; }
    public required ClaimPolicyCheckResult Policy { get; init; }
    public required ClaimBenefitCalculation Benefit { get; init; }
    public required string Flow { get; init; }
    public required ClaimApprovalRulesResult Approval { get; init; }
    public required IReadOnlyList<ClaimFact> Timeframes { get; init; }

    /// <summary>True when the run reproduces the scenario's figures, so the scenario's insurer outcome applies.</summary>
    public bool MatchesScenario => Flow == ClaimFlows.Lodge
        && Profile.ScenarioBenefit is { } expected
        && ClaimRules.Round2(Benefit.Benefit) == expected;

    /// <summary>Amount asked for on a claim that isn't lodged (Grace: $200,000).</summary>
    public decimal AmountAtStake => Profile.AmountClaimed ?? Benefit.InsuredAmount;
}

public static partial class ClaimRules
{
    public static ClaimRun BuildRun(CaseRecord record, ClaimRunInputs? inputs = null)
    {
        var profile = ProfileFor(record);
        var type = ClaimCoverTypes.Normalize(string.IsNullOrWhiteSpace(inputs?.ClaimType) ? profile.ClaimType : inputs.ClaimType);
        if (type != profile.ClaimType)
            profile = profile with { ClaimType = type };
        if (inputs?.WaitingPeriodDays is { } waitingDays && waitingDays != profile.WaitingPeriodDays)
            profile = profile with { WaitingPeriodDays = waitingDays };

        var eventDate = ParseDate(inputs?.EventDate) ?? profile.EventDate;
        var claimant = ValidateClaimant(profile, inputs?.ClaimantName, inputs?.ClaimantRelationship, inputs?.AccountName, inputs?.Bsb, inputs?.Account);
        var policy = CheckPolicy(profile, type, eventDate);
        var benefit = CalculateBenefit(
            profile,
            type,
            inputs?.InsuredAmount,
            inputs?.AnnualEarnings,
            inputs?.WaitingPeriodDays,
            eventDate,
            inputs?.BeneficiarySharePercent,
            claimant.ClaimantName,
            claimant.MaskedAccount);
        var flow = DecideFlow(profile, claimant, policy);
        var approval = CheckApprovalRules(profile, type, claimant, flow, benefit.BenefitDisplay, policy.CoverMatches);
        var lodgementDate = profile.LodgementDate ?? MelbourneToday();
        // The waiting period runs from the date of disablement and ends the
        // day the benefit starts to accrue (Tom: 14 Oct + 30 days = 13 Nov).
        var waitingEnds = type == ClaimCoverTypes.IncomeProtection && eventDate is { } e && profile.WaitingPeriodDays is { } waitingPeriod
            ? e.AddDays(Math.Max(0, waitingPeriod))
            : (DateOnly?)null;

        return new ClaimRun
        {
            Case = record,
            Profile = profile,
            ClaimType = type,
            EventDate = eventDate,
            LodgementDate = lodgementDate,
            Claimant = claimant,
            Policy = policy,
            Benefit = benefit,
            Flow = flow,
            Approval = approval,
            Timeframes = Timeframes(type, lodgementDate, waitingEnds, benefit.Benefit, profile),
        };
    }

    /// <summary>Short check lines for the Teams card: "✓ Entitled to claim — Policy owner and life insured".</summary>
    public static IReadOnlyList<string> CheckSummaryLines(ClaimRun run)
    {
        static string Glyph(string status) => status switch { "pass" => "✓", "fail" => "✕", "warn" => "!", _ => "–" };
        var keys = new[] { "entitlement", "claim-form", "contact", "bank", "documents" };
        var lines = run.Claimant.Checks
            .Where(c => keys.Contains(c.Key))
            .Select(c => $"{Glyph(c.Status)} {c.Label} — {c.Value}")
            .ToList();
        lines.AddRange(run.Policy.Checks
            .Where(c => c.Key is "in-force" or "definition" or "waiting" or "time-in-force" && c.Status != "na")
            .Select(c => $"{Glyph(c.Status)} {c.Label} — {c.Value}"));
        return lines;
    }

    /// <summary>The approval record for a run (lodgement, or a block decision when the claimant checks fail).</summary>
    public static ClaimApprovalRecord CreateApprovalRecord(
        ClaimRun run,
        string approvalId,
        string raisedBy,
        string? claimUpdateDraft = null,
        string? recommendation = null,
        string? reason = null)
    {
        var p = run.Profile;
        var block = run.Flow == ClaimFlows.Refer;
        var scenario = run.MatchesScenario;
        var cover = p.CoverLines.Count > 0 ? p.CoverLines[0] : ClaimCoverTypes.Label(run.ClaimType);

        return new ClaimApprovalRecord
        {
            ApprovalId = approvalId,
            CaseNumber = run.Case.CaseNumber,
            Kind = block ? ClaimDecisionKinds.Block : ClaimDecisionKinds.Lodgement,
            ClientName = p.ClientName.Length > 0 ? p.ClientName : run.Case.ClientName,
            ClientNumber = p.ClientNumber.Length > 0 ? p.ClientNumber : run.Case.ClientNumber,
            ClientDisplay = p.ClientDisplay.Length > 0 ? p.ClientDisplay : $"{run.Case.ClientName} · {run.Case.ClientNumber}",
            Insurer = p.Insurer,
            InsurerPrefix = p.InsurerPrefix,
            PolicyNumber = p.PolicyNumber,
            CoverDisplay = cover,
            ClaimType = run.ClaimType,
            ClaimantName = run.Claimant.ClaimantName,
            ClaimantRole = run.Claimant.RelationshipLabel,
            PayeeName = run.Claimant.AccountName,
            PayeeAccount = run.Claimant.MaskedAccount,
            ExpectedBenefit = block ? 0m : run.Benefit.Benefit,
            ExpectedBenefitDisplay = block
                ? $"None — {FormatMoney(run.AmountAtStake)} claimed, not lodged"
                : run.Benefit.BenefitDisplay,
            AmountClaimed = block ? run.AmountAtStake : p.AmountClaimed,
            Checks = CheckSummaryLines(run),
            Recommendation = string.IsNullOrWhiteSpace(recommendation) ? run.Approval.Recommendation : recommendation.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? string.Join(" ", run.Approval.Reasons) : reason.Trim(),
            ApprovalReasons = run.Approval.Reasons,
            Approver = run.Approval.Approver,
            RaisedBy = raisedBy,
            LodgementDate = block ? null : run.LodgementDate,
            ClaimUpdateDraft = claimUpdateDraft,
            InsurerOutcome = block || !scenario ? Array.Empty<ClaimTimelineEvent>() : p.InsurerOutcome,
            OutcomeLabel = block ? (p.OutcomeLabel ?? "Blocked") : scenario ? p.OutcomeLabel : null,
            OutcomeSummary = block
                ? p.OutcomeSummary ?? "No claim lodged — referred to Financial Crime until the claimant and the bank details are verified on details we already trust."
                : scenario ? p.OutcomeSummary : null,
            ProtectedNote = block ? p.ProtectedNote : null,
            Timeframes = run.Timeframes,
            Status = ClaimApprovalStatus.Pending,
        };
    }
}
