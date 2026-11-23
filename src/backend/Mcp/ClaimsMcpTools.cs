using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Server;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Mcp;

/// <summary>
/// MCP tool surface for the Claims Agent (Ben, Benefit Claims Advocate):
/// validating the claimant, checking the policy, calculating the expected
/// benefit, checking the approval rules, raising the approval (posted to
/// Microsoft Teams), polling it and — only once a person has approved —
/// lodging the claim with the insurer.
///
/// Every tool is deterministic and side-effect free except
/// <see cref="RequestApproval"/> (creates an approval and posts the Teams
/// card) and <see cref="LodgeClaim"/> (moves an approved claim to lodged).
/// The static <c>*Payload</c> builders are shared with <c>ClaimsApi</c> so the
/// offline path reports exactly what the tools return.
/// </summary>
[McpServerToolType]
public class ClaimsMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly CaseStore _cases;
    private readonly ClaimApprovalStore _approvals;
    private readonly TeamsMcpTools _teams;
    private readonly ILogger<ClaimsMcpTools> _logger;

    public ClaimsMcpTools(
        CaseStore cases,
        ClaimApprovalStore approvals,
        TeamsMcpTools teams,
        ILogger<ClaimsMcpTools> logger)
    {
        _cases = cases;
        _approvals = approvals;
        _teams = teams;
        _logger = logger;
    }

    [McpServerTool(Name = "claims_validateClaimant"),
     Description("Check the claimant before anything goes to the insurer: entitled to the benefit (the policy owner for income protection, trauma, TPD and terminal illness; the nominated beneficiaries — or, without a valid nomination, the estate — for a death claim), claim form signed by the verified claimant, contact details not recently changed, bank account in the claimant's own name, and supporting documents. Any red flag means: don't lodge — refer to Financial Crime. Call this first.")]
    public string ValidateClaimant(
        [Description("Client Case number, e.g. REQ-20261111-LIF004.")] string caseNumber,
        [Description("Claimant's name (optional — defaults to the claimant on the case).")] string? claimantName = null,
        [Description("Claimant's relationship to the policy: policy-owner, beneficiary, estate or other (optional).")] string? relationship = null,
        [Description("Name on the bank account the benefit would be paid into (optional).")] string? accountName = null,
        [Description("BSB — masked is fine (e.g. '033-***').")] string? bsb = null,
        [Description("Account number — masked is fine (e.g. '****5120').")] string? accountNumber = null)
    {
        if (!TryGetCase(caseNumber, out var record, out var error)) return error;
        var run = ClaimRules.BuildRun(record, new ClaimRunInputs
        {
            ClaimantName = claimantName,
            ClaimantRelationship = relationship,
            AccountName = accountName,
            Bsb = bsb,
            Account = accountNumber,
        });
        if (run.Flow == ClaimFlows.NotAClaim) return NotAClaim(run);
        return ToJson(ValidateClaimantPayload(run));
    }

    [McpServerTool(Name = "claims_checkPolicy"),
     Description("Check the policy behind the claim: in force and premiums paid; the cover type and the definition the claim relies on; the waiting period; exclusions; and how long the policy has been in force — after 3 years the insurer can't avoid it for a non-fraudulent misrepresentation (Insurance Contracts Act 1984 s29(3)). Returns the claim pack (evidence verified, still to come, missing or referred to Financial Crime) and the insurer's Life Insurance Code of Practice timeframes.")]
    public string CheckPolicy(
        [Description("Client Case number.")] string caseNumber,
        [Description("Claim type: income-protection, death, terminal-illness, trauma or tpd (optional — defaults to the case).")] string? claimType = null,
        [Description("Date of the injury, illness or death, dd/MM/yyyy (optional).")] string? eventDate = null)
    {
        if (!TryGetCase(caseNumber, out var record, out var error)) return error;
        var run = ClaimRules.BuildRun(record, new ClaimRunInputs { ClaimType = claimType, EventDate = eventDate });
        if (run.Flow == ClaimFlows.NotAClaim) return NotAClaim(run);
        return ToJson(CheckPolicyPayload(run));
    }

    [McpServerTool(Name = "claims_calculateBenefit"),
     Description("Calculate the expected benefit step by step. Income protection: the lesser of the insured monthly benefit and the policy's share of average monthly earnings in the 12 months before the disability (Harbourline Life: 70%; APRA caps all benefits at 90% of earnings for the first 6 months of a claim and 70% after, for policies issued from 1 Oct 2021), accruing after the waiting period and paid monthly in arrears. Death: the sum insured times the nominated beneficiary's share, paid directly to them. Includes the tax treatment.")]
    public string CalculateBenefit(
        [Description("Client Case number.")] string caseNumber,
        [Description("Claim type (optional — defaults to the case).")] string? claimType = null,
        [Description("Insured monthly benefit (income protection) or sum insured (lump sums), AUD (optional).")] decimal? insuredAmount = null,
        [Description("Earnings in the 12 months before the disability, AUD (income protection, optional).")] decimal? annualEarnings = null,
        [Description("Waiting period in days (income protection, optional).")] int? waitingPeriodDays = null,
        [Description("Date the disability started, dd/MM/yyyy (optional).")] string? eventDate = null,
        [Description("Nominated beneficiary's share in % (death claims, optional).")] decimal? beneficiarySharePercent = null)
    {
        if (!TryGetCase(caseNumber, out var record, out var error)) return error;
        if (insuredAmount is < 0m || annualEarnings is < 0m) return Error("amounts can't be negative.");
        var run = ClaimRules.BuildRun(record, new ClaimRunInputs
        {
            ClaimType = claimType,
            InsuredAmount = insuredAmount,
            AnnualEarnings = annualEarnings,
            WaitingPeriodDays = waitingPeriodDays,
            EventDate = eventDate,
            BeneficiarySharePercent = beneficiarySharePercent,
        });
        if (run.Flow == ClaimFlows.NotAClaim) return NotAClaim(run);
        return ToJson(CalculateBenefitPayload(run));
    }

    [McpServerTool(Name = "claims_checkApprovalRules"),
     Description("Check which human approvals the claim needs: every claim lodgement; every death or terminal illness claim; any bank-detail change; a person first for vulnerable clients; and — when the claimant checks raise financial-crime concerns — the Team Leader's decision to block, with nothing lodged. Returns the recommendation (lodge, hold, or don't lodge and refer).")]
    public string CheckApprovalRules(
        [Description("Client Case number.")] string caseNumber,
        [Description("Claim type (optional).")] string? claimType = null,
        [Description("Claimant's name (optional).")] string? claimantName = null,
        [Description("Name on the bank account (optional).")] string? accountName = null,
        [Description("Account number — masked is fine (optional).")] string? accountNumber = null)
    {
        if (!TryGetCase(caseNumber, out var record, out var error)) return error;
        var run = ClaimRules.BuildRun(record, new ClaimRunInputs
        {
            ClaimType = claimType,
            ClaimantName = claimantName,
            AccountName = accountName,
            Account = accountNumber,
        });
        if (run.Flow == ClaimFlows.NotAClaim) return NotAClaim(run);
        return ToJson(CheckApprovalRulesPayload(run));
    }

    [McpServerTool(Name = "claims_requestApproval"),
     Description("Raise the approval and post the 'Caldova Life Advice — Claim lodgement approval' Adaptive Card to the Team Leader in Microsoft Teams (via teams_sendApprovalCard). Returns the approvalId. One live approval per case: if the Claims desk already raised one, that approvalId is returned. When the claimant checks fail, it raises a block decision instead — that can never be lodged. Nothing is raised when the claim isn't ready (claimant not entitled, cover not on the policy). MUST be called before claims_lodgeClaim.")]
    public async Task<string> RequestApproval(
        [Description("Client Case number.")] string caseNumber,
        [Description("Expected benefit in AUD, from claims_calculateBenefit (optional — checked against the case).")] decimal? expectedBenefit = null,
        [Description("Your recommendation for the approver (optional).")] string? recommendation = null,
        [Description("Why approval is needed — the rules triggered (optional).")] string? reason = null)
    {
        if (!TryGetCase(caseNumber, out var record, out var error)) return error;

        var existing = _approvals.CurrentForCase(record.CaseNumber);
        if (existing is not null)
            return ToJson(RequestApprovalPayload(existing, null, alreadyRaised: true));

        var run = ClaimRules.BuildRun(record);
        if (run.Flow == ClaimFlows.NotAClaim) return NotAClaim(run);
        if (run.Flow == ClaimFlows.Hold)
        {
            return ToJson(new
            {
                tool = "claims_requestApproval",
                caseNumber = record.CaseNumber,
                raised = false,
                flow = run.Flow,
                reasons = run.Approval.Reasons,
                note = "No approval raised — the claim isn't ready to lodge. " + run.Approval.Recommendation
            });
        }

        var approval = _approvals.Add(ClaimRules.CreateApprovalRecord(
            run,
            ClaimApprovalStore.NewApprovalId(),
            ClaimRules.RaisedByAgent,
            recommendation: recommendation,
            reason: reason));
        var teamsResult = await _teams.SendApprovalCardCoreAsync(BuildTeamsRequest(approval));
        approval.TeamsSent = teamsResult.Sent;
        approval.TeamsChannel = teamsResult.Channel;
        approval.TeamsMessage = teamsResult.Message;
        approval.ApprovalUrl = teamsResult.ApprovalUrl;

        _logger.LogInformation(
            "Claim approval requested via MCP: id={ApprovalId} case={CaseNumber} kind={Kind} teamsSent={TeamsSent}",
            Sanitize(approval.ApprovalId), Sanitize(approval.CaseNumber), approval.Kind, teamsResult.Sent);

        var payload = RequestApprovalPayload(approval, teamsResult, alreadyRaised: false);
        if (expectedBenefit is { } passed && approval.Kind == ClaimDecisionKinds.Lodgement
            && ClaimRules.Round2(passed) != ClaimRules.Round2(approval.ExpectedBenefit))
        {
            return ToJson(new
            {
                approval = payload,
                warning = $"The expected benefit you passed ({ClaimRules.FormatMoney(passed)}) differs from the case calculation ({approval.ExpectedBenefitDisplay}); the approval uses the case calculation."
            });
        }
        return ToJson(payload);
    }

    [McpServerTool(Name = "claims_getApprovalStatus"),
     Description("Current status of a claim approval: pending, approved, rejected, lodged, blocked or cancelled (superseded).")]
    public string GetApprovalStatus(
        [Description("Approval id returned by claims_requestApproval.")] string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return Error("approvalId is required.");
        var record = _approvals.Get(approvalId);
        if (record is null) return Error($"approval '{approvalId}' not found.");
        return ToJson(ApprovalStatusPayload(record));
    }

    [McpServerTool(Name = "claims_lodgeClaim"),
     Description("Lodge the claim with the insurer. Call ONLY after a person has approved in Microsoft Teams — the tool refuses while the approval is pending, rejected or superseded, and always refuses a block decision.")]
    public string LodgeClaim(
        [Description("Approval id returned by claims_requestApproval.")] string approvalId)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return Error("approvalId is required.");
        var result = _approvals.Lodge(approvalId);
        if (result.Record is null) return Error(result.Error ?? $"approval '{approvalId}' not found.");

        if (result.Ok)
        {
            _logger.LogInformation("Claim lodged via MCP: id={ApprovalId} ref={Reference}",
                Sanitize(result.Record.ApprovalId), Sanitize(result.Record.LodgementReference ?? string.Empty));
        }
        return ToJson(LodgePayload(result));
    }

    // ── Shared with ClaimsApi ───────────────────────────────────────────

    internal static TeamsApprovalCardRequest BuildTeamsRequest(ClaimApprovalRecord a) => new(
        ApprovalId: a.ApprovalId,
        CaseNumber: a.CaseNumber,
        ClientName: a.ClientDisplay,
        Policy: $"{a.Insurer} {a.PolicyNumber}".Trim(),
        Cover: a.CoverDisplay,
        ClaimType: $"{ClaimCoverTypes.ClaimLabel(a.ClaimType)} — claimant {a.ClaimantName} ({a.ClaimantRole.ToLowerInvariant()})",
        ExpectedBenefit: a.ExpectedBenefitDisplay,
        Checks: a.Checks,
        Recommendation: a.Recommendation,
        Reason: a.Reason,
        Kind: a.Kind,
        Payee: $"{a.PayeeName} — {a.PayeeAccount}",
        LodgementDate: a.LodgementDate is { } d ? $"{ClaimRules.FormatDate(d)}, after approval" : null,
        Approver: a.Approver);

    internal static object ValidateClaimantPayload(ClaimRun run)
    {
        var c = run.Claimant;
        var note = c.ReferToFinancialCrime
            ? "Don't lodge — refer to Financial Crime. Describe the facts; never accuse the client — they may be the victim."
            : !c.Entitled
                ? "Not entitled — confirm who can claim with the adviser before anything is lodged."
                : c.BankDetailChange
                    ? "Claimant checks passed, but the bank-detail change needs a person's approval and a call-back on details we already trust."
                    : "Claimant checks passed.";
        return new
        {
            tool = "claims_validateClaimant",
            caseNumber = run.Case.CaseNumber,
            client = run.Profile.ClientDisplay,
            policy = $"{run.Profile.Insurer} {run.Profile.PolicyNumber}",
            policyOwner = run.Profile.PolicyOwner,
            claimant = c.ClaimantName,
            relationship = c.RelationshipLabel,
            entitled = c.Entitled,
            entitlement = c.EntitlementNote,
            supportPerson = run.Profile.SupportPerson,
            claimFormVerified = c.ClaimFormVerified,
            contactDetailsTrusted = c.ContactDetailsTrusted,
            bankAccount = new
            {
                name = c.AccountName,
                account = c.MaskedAccount,
                inClaimantsOwnName = c.AccountInOwnName,
                matchesAccountOnFile = c.AccountMatchesFile,
                bankDetailChange = c.BankDetailChange
            },
            referToFinancialCrime = c.ReferToFinancialCrime,
            redFlags = c.RedFlags,
            checks = Checks(c.Checks),
            vulnerable = run.Profile.Vulnerable,
            note
        };
    }

    internal static object CheckPolicyPayload(ClaimRun run)
    {
        var p = run.Profile;
        var policy = run.Policy;
        return new
        {
            tool = "claims_checkPolicy",
            caseNumber = run.Case.CaseNumber,
            insurer = p.Insurer,
            policyNumber = p.PolicyNumber,
            policyOwner = p.PolicyOwner,
            lifeInsured = p.LifeInsured,
            heldOutsideSuper = p.HeldOutsideSuper,
            cover = p.CoverLines,
            claimType = ClaimCoverTypes.ClaimLabel(policy.ClaimType),
            inForce = policy.InForce,
            premiumsPaid = policy.PremiumsPaid,
            coverMatches = policy.CoverMatches,
            definition = p.Definition,
            definitionMet = policy.DefinitionMet,
            waitingPeriodDays = p.WaitingPeriodDays,
            benefitPeriod = p.BenefitPeriod,
            exclusions = p.Exclusions,
            timeInForce = policy.TimeInForce,
            pastMisrepresentationWindow = policy.PastMisrepresentationWindow,
            checks = Checks(policy.Checks),
            claimPack = policy.ClaimPack.Select(i => new { document = i.Document, status = i.Status, note = i.Note }),
            codeTimeframes = run.Timeframes.Select(f => new { label = f.Label, value = f.Value }),
            rulesCited = new[] { p.PdsTitle, ClaimRules.ClaimsRuleTitle, ClaimRules.DisclosureRuleTitle }.Where(t => t.Length > 0).Distinct()
        };
    }

    internal static object CalculateBenefitPayload(ClaimRun run)
    {
        var b = run.Benefit;
        return new
        {
            tool = "claims_calculateBenefit",
            caseNumber = run.Case.CaseNumber,
            claimType = ClaimCoverTypes.ClaimLabel(b.ClaimType),
            steps = b.Steps.Select(s => new { label = s.Label, kind = s.Kind, amount = s.Amount, display = s.Display }),
            insuredAmount = b.InsuredAmount,
            expectedBenefit = b.Benefit,
            expectedBenefitDisplay = b.BenefitDisplay,
            limitedByEarnings = b.LimitedByEarnings,
            averageMonthlyEarnings = b.MonthlyEarnings,
            earningsLimit = b.EarningsCap,
            accruesFrom = b.AccrualStart is { } a ? ClaimRules.FormatDate(a) : null,
            firstPayment = b.FirstPayment,
            paidTo = b.PaidTo,
            benefitBasis = b.ClaimType == ClaimCoverTypes.IncomeProtection ? ClaimRules.IncomeBenefitBasis(run.Profile) : null,
            taxTreatment = b.TaxTreatment,
            notes = b.Notes,
            ruleCited = b.RuleCited,
            taxRuleCited = b.TaxRuleCited
        };
    }

    internal static object CheckApprovalRulesPayload(ClaimRun run)
    {
        var a = run.Approval;
        return new
        {
            tool = "claims_checkApprovalRules",
            caseNumber = run.Case.CaseNumber,
            flow = run.Flow,
            humanApprovalRequired = a.Required,
            approver = a.Approver,
            reasons = a.Reasons,
            personFirst = a.PersonFirst,
            doNotLodge = a.DoNotLodge,
            recommendation = a.Recommendation,
            note = a.Note,
            ruleCited = a.RuleCited
        };
    }

    internal static object RequestApprovalPayload(ClaimApprovalRecord r, TeamsCardSendResult? teams, bool alreadyRaised)
    {
        var note = r.IsBlockDecision
            ? "Block decision raised with the Team Leader in Microsoft Teams. Nothing can be lodged — claims_lodgeClaim refuses a block decision."
            : r.Status == ClaimApprovalStatus.Lodged
                ? $"This claim has already been lodged (reference {r.LodgementReference})."
                : alreadyRaised
                    ? "An approval for this case is already with the approver in Microsoft Teams — use this approvalId. Call claims_lodgeClaim only after it's approved."
                    : "Awaiting a person's approval in Microsoft Teams. Call claims_lodgeClaim with this approvalId only after approval.";
        return new
        {
            tool = "claims_requestApproval",
            approvalId = r.ApprovalId,
            caseNumber = r.CaseNumber,
            kind = r.Kind,
            status = r.StatusKey,
            raised = true,
            alreadyRaised,
            insurer = r.Insurer,
            policyNumber = r.PolicyNumber,
            claimType = ClaimCoverTypes.ClaimLabel(r.ClaimType),
            expectedBenefit = r.ExpectedBenefitDisplay,
            recommendation = r.Recommendation,
            approver = r.Approver,
            reasons = r.ApprovalReasons,
            teams = new
            {
                sent = teams?.Sent ?? r.TeamsSent,
                channel = teams?.Channel ?? r.TeamsChannel,
                approvalUrl = teams?.ApprovalUrl ?? r.ApprovalUrl,
                message = teams?.Message ?? r.TeamsMessage
            },
            note
        };
    }

    internal static object ApprovalStatusPayload(ClaimApprovalRecord r) => new
    {
        tool = "claims_getApprovalStatus",
        approvalId = r.ApprovalId,
        caseNumber = r.CaseNumber,
        kind = r.Kind,
        status = r.StatusKey,
        decision = r.Decision,
        decidedBy = r.DecidedBy,
        decidedAt = r.DecidedAt,
        decisionComment = r.DecisionComment,
        lodgementReference = r.LodgementReference,
        lodgedAt = r.LodgedAt,
        canLodge = !r.IsBlockDecision && r.Status == ClaimApprovalStatus.Approved
    };

    internal static object LodgePayload(ClaimTransitionResult result)
    {
        var r = result.Record!;
        var lodged = result.Ok && r.Status == ClaimApprovalStatus.Lodged;
        var note = !result.Ok
            ? $"Not lodged: {result.Error}."
            : result.Note == "already lodged"
                ? "The claim had already been lodged."
                : $"Lodged with {r.Insurer} — reference {r.LodgementReference}. The insurer now works to the Life Insurance Code of Practice timeframes.";
        return new
        {
            tool = "claims_lodgeClaim",
            approvalId = r.ApprovalId,
            lodged,
            status = r.StatusKey,
            insurer = r.Insurer,
            policyNumber = r.PolicyNumber,
            lodgementReference = r.LodgementReference,
            lodgementDate = lodged && r.LodgementDate is { } d ? ClaimRules.FormatDate(d) : null,
            expectedBenefit = r.ExpectedBenefitDisplay,
            codeTimeframes = lodged ? r.Timeframes.Select(f => new { label = f.Label, value = f.Value }) : null,
            note
        };
    }

    internal static string ToJson(object payload) => JsonSerializer.Serialize(payload, JsonOptions);

    private static IEnumerable<object> Checks(IEnumerable<ClaimCheckLine> checks) =>
        checks.Select(c => new { check = c.Label, status = c.Status, value = c.Value, detail = c.Detail, source = c.Source });

    private bool TryGetCase(string caseNumber, out CaseRecord record, out string error)
    {
        record = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(caseNumber))
        {
            error = Error("caseNumber is required.");
            return false;
        }
        var found = _cases.Get(caseNumber.Trim());
        if (found is null)
        {
            error = Error($"case '{caseNumber}' not found.");
            return false;
        }
        record = found;
        return true;
    }

    private static string NotAClaim(ClaimRun run) => ToJson(new
    {
        caseNumber = run.Case.CaseNumber,
        flow = run.Flow,
        note = run.Profile.NotAClaimNote ?? "This case is new advice — there's nothing to claim.",
        seeAlso = run.Profile.NotAClaimLinks.Select(l => new { agent = l.Label, page = l.Href })
    });

    private static string Error(string message) => ToJson(new { error = message });

    private static string Sanitize(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
