using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Mcp;

/// <summary>
/// MCP tool surface that owns Microsoft Teams notifications for the claim
/// approvals raised by the Claims Agent (Ben, Benefit Claims Advocate). The
/// agent (or any in-process caller) invokes <c>teams_sendApprovalCard</c> and
/// gets back where the "Caldova Life Advice — Claim lodgement approval"
/// Adaptive Card was delivered.
///
/// Delivery is simulated in the scenarios: the Adaptive Card payload is built
/// and logged, and the tool returns <c>sent=true</c> so the page flow always
/// works end to end without external infrastructure. Swapping in a real Teams
/// sender later just means replacing the body of
/// <see cref="SendApprovalCardCoreAsync"/>.
/// </summary>
[McpServerToolType]
public class TeamsMcpTools
{
    private const string SimulatedChannel = "caldova-claim-approvals";
    private const string ApprovalPagePath = "/agents/claims";

    public const string LodgementCardTitle = "Caldova Life Advice — Claim lodgement approval";
    public const string BlockCardTitle = "Caldova Life Advice — Claim block approval";

    private static readonly Regex BsbRegex = new(@"(?<![\d-])(\d{3})-?\d{3}(?![\d-])", RegexOptions.Compiled);
    private static readonly Regex AccountRegex = new(@"(?<![\d-])\d{5,}(?![\d-])", RegexOptions.Compiled);

    private readonly ILogger<TeamsMcpTools> _logger;

    public TeamsMcpTools(ILogger<TeamsMcpTools> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Whether the Teams sender is considered configured. The simulated
    /// sender always reports configured so the page shows a delivered card
    /// rather than a configuration warning.
    /// </summary>
    public bool IsConfigured => true;

    [McpServerTool(Name = "teams_sendApprovalCard"),
     Description("Post the Microsoft Teams Adaptive Card 'Caldova Life Advice — Claim lodgement approval' to the approver (the Team Leader). The card shows the case, the client, the policy, the cover, the claim type, the expected benefit, the checks, the recommendation and why approval is needed, with Approve / Reject actions. Use decision='block' for a claim that must NOT be lodged (the card then asks the approver to confirm the block and the referral to Financial Crime). claims_requestApproval calls this for you after minting an approvalId.")]
    public async Task<string> SendApprovalCard(
        [Description("Approval id minted by claims_requestApproval (e.g. CLM-XXXXXXXXXX).")] string approvalId,
        [Description("Client Case number (e.g. REQ-20261111-LIF004).")] string caseNumber,
        [Description("Client (and client number) the claim is for.")] string clientName,
        [Description("Insurer and policy number, e.g. 'Harbourline Life HL-23051877'.")] string policy,
        [Description("Cover being claimed on, one line.")] string cover,
        [Description("Claim type, e.g. 'Income protection claim', 'Death claim'.")] string claimType,
        [Description("Expected benefit, e.g. '$5,600 a month' or '$1,000,000 lump sum'.")] string expectedBenefit,
        [Description("The checks completed, one per line (or separated by ';').")] string? checks = null,
        [Description("The agent's recommendation, e.g. 'Lodge with Harbourline Life'.")] string? recommendation = null,
        [Description("Why approval is needed (the approval rules triggered).")] string? reason = null,
        [Description("'lodge' (default) or 'block'.")] string? decision = null,
        [Description("Payee name and masked bank details, e.g. 'Tom Bradley — BSB 033-*** ****5120'. Unmasked numbers are masked automatically.")] string? payee = null,
        [Description("Planned lodgement date (optional).")] string? lodgementDate = null)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return Error("approvalId is required.");
        if (string.IsNullOrWhiteSpace(caseNumber)) return Error("caseNumber is required.");
        if (string.IsNullOrWhiteSpace(clientName)) return Error("clientName is required.");

        var kind = (decision ?? string.Empty).Trim().ToLowerInvariant() is "block" or "do-not-lodge" or "refer"
            ? ClaimDecisionKinds.Block
            : ClaimDecisionKinds.Lodgement;
        var checkLines = (checks ?? string.Empty)
            .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = await SendApprovalCardCoreAsync(new TeamsApprovalCardRequest(
            ApprovalId: approvalId.Trim(),
            CaseNumber: caseNumber.Trim(),
            ClientName: clientName.Trim(),
            Policy: policy ?? string.Empty,
            Cover: cover ?? string.Empty,
            ClaimType: claimType ?? string.Empty,
            ExpectedBenefit: expectedBenefit ?? string.Empty,
            Checks: checkLines,
            Recommendation: recommendation ?? string.Empty,
            Reason: reason,
            Kind: kind,
            Payee: payee,
            LodgementDate: lodgementDate));

        return Json(new
        {
            tool = "teams_sendApprovalCard",
            approvalId,
            caseNumber,
            kind,
            sent = result.Sent,
            channel = result.Channel,
            approvalUrl = result.ApprovalUrl,
            message = result.Message
        });
    }

    /// <summary>
    /// In-process entry point used by <c>ClaimsMcpTools</c> and the
    /// <c>ClaimsApi</c> HTTP layer, so the Adaptive Card is built in one place
    /// and the offline path reports the same result shape.
    /// </summary>
    public Task<TeamsCardSendResult> SendApprovalCardCoreAsync(
        TeamsApprovalCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var approvalUrl = BuildApprovalUrl(request.ApprovalId);
        var card = BuildAdaptiveCard(request, approvalUrl);
        var title = request.Kind == ClaimDecisionKinds.Block ? BlockCardTitle : LodgementCardTitle;

        // Simulated delivery: log the card and report it delivered to the
        // claim-approvals channel. A real implementation posts `card` here.
        _logger.LogInformation(
            "teams_sendApprovalCard: simulated delivery. approvalId={ApprovalId} case={CaseNumber} kind={Kind} channel={Channel}",
            Sanitize(request.ApprovalId), Sanitize(request.CaseNumber), request.Kind, SimulatedChannel);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "teams_sendApprovalCard payload for {ApprovalId}: {Payload}",
                Sanitize(request.ApprovalId),
                JsonSerializer.Serialize(card));
        }

        return Task.FromResult(new TeamsCardSendResult(
            Sent: true,
            Channel: SimulatedChannel,
            ApprovalUrl: approvalUrl,
            Message: $"Adaptive Card \"{title}\" posted to {SimulatedChannel} for approval {request.ApprovalId}.",
            Card: card));
    }

    /// <summary>
    /// Deep link back to the Claims page. <paramref name="decision"/>
    /// ("approve" / "reject") pre-selects the action in the approval popup;
    /// the person still confirms it there.
    /// </summary>
    public static string BuildApprovalUrl(string approvalId, string? decision = null)
    {
        var url = $"{ApprovalPagePath}?approvalId={Uri.EscapeDataString(approvalId ?? string.Empty)}";
        return string.IsNullOrWhiteSpace(decision) ? url : $"{url}&decision={Uri.EscapeDataString(decision)}";
    }

    /// <summary>Mask BSBs and account numbers in free text ("033-123 12345120" → "033-*** ****5120").</summary>
    public static string MaskBankText(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0) return raw;
        var masked = AccountRegex.Replace(raw, m => "****" + m.Value[^4..]);
        return BsbRegex.Replace(masked, m => m.Groups[1].Value + "-***");
    }

    private static Dictionary<string, object?> BuildAdaptiveCard(TeamsApprovalCardRequest request, string approvalUrl)
    {
        var block = request.Kind == ClaimDecisionKinds.Block;
        var facts = new List<object>
        {
            new { title = "Case", value = request.CaseNumber },
            new { title = "Client", value = request.ClientName },
            new { title = "Policy", value = request.Policy },
            new { title = "Cover", value = request.Cover },
            new { title = "Claim type", value = request.ClaimType },
            new { title = block ? "Amount claimed" : "Expected benefit", value = request.ExpectedBenefit },
        };
        if (!string.IsNullOrWhiteSpace(request.Payee))
            facts.Add(new { title = block ? "Payee on the claim" : "Payee", value = MaskBankText(request.Payee) });
        if (!block && !string.IsNullOrWhiteSpace(request.LodgementDate))
            facts.Add(new { title = "Lodgement", value = request.LodgementDate });
        facts.Add(new { title = "Approver", value = string.IsNullOrWhiteSpace(request.Approver) ? ClaimRules.TeamLeaderApprover : request.Approver });

        var body = new List<object>
        {
            new
            {
                type = "TextBlock",
                size = "Large",
                weight = "Bolder",
                wrap = true,
                text = block ? $"🛡️ {BlockCardTitle}" : $"🗂️ {LodgementCardTitle}"
            },
            new
            {
                type = "TextBlock",
                spacing = "None",
                isSubtle = true,
                wrap = true,
                text = $"Claims Agent (Ben) · {request.CaseNumber} · Approval id {request.ApprovalId}"
            },
            new { type = "FactSet", facts },
        };

        if (request.Checks.Count > 0)
        {
            body.Add(new { type = "TextBlock", weight = "Bolder", spacing = "Medium", wrap = true, text = "Checks" });
            body.Add(new { type = "TextBlock", wrap = true, text = string.Join("\r\n", request.Checks.Select(c => $"- {c}")) });
        }

        body.Add(new { type = "TextBlock", weight = "Bolder", spacing = "Medium", wrap = true, text = "Recommendation" });
        body.Add(new
        {
            type = "TextBlock",
            wrap = true,
            text = string.IsNullOrWhiteSpace(request.Recommendation)
                ? block ? "Don't lodge — block the claim and refer it to Financial Crime." : "Lodge the claim with the insurer."
                : request.Recommendation
        });
        body.Add(new { type = "TextBlock", weight = "Bolder", spacing = "Medium", wrap = true, text = "Why approval is needed" });
        body.Add(new
        {
            type = "TextBlock",
            wrap = true,
            text = string.IsNullOrWhiteSpace(request.Reason)
                ? "Every claim lodgement needs a person's approval before it goes to the insurer."
                : request.Reason
        });
        body.Add(new
        {
            type = "TextBlock",
            size = "Small",
            isSubtle = true,
            wrap = true,
            text = block
                ? "Nothing is lodged. Approving confirms the block and the referral to Financial Crime. Contact the client only on details verified before any change."
                : "Ben's agent lodges this claim only after it's approved. Bank details are masked; health details are shared only with the people who need them."
        });

        // Incoming-webhook cards can't carry Action.Submit, so Approve /
        // Reject deep-link to the approval popup on the Claims page.
        var content = new Dictionary<string, object?>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["msteams"] = new { width = "Full" },
            ["body"] = body,
            ["actions"] = new object[]
            {
                new
                {
                    type = "Action.OpenUrl",
                    title = block ? "Approve block" : "Approve",
                    style = "positive",
                    url = BuildApprovalUrl(request.ApprovalId, "approve")
                },
                new
                {
                    type = "Action.OpenUrl",
                    title = "Reject",
                    style = "destructive",
                    url = BuildApprovalUrl(request.ApprovalId, "reject")
                },
                new
                {
                    type = "Action.OpenUrl",
                    title = "Open case in Caldova",
                    url = approvalUrl
                }
            }
        };

        return new Dictionary<string, object?>
        {
            ["type"] = "message",
            ["attachments"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["contentType"] = "application/vnd.microsoft.card.adaptive",
                    ["contentUrl"] = null,
                    ["content"] = content
                }
            }
        };
    }

    private static string Json(object payload) =>
        JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

    private static string Error(string message) =>
        Json(new { error = message });

    private static string Sanitize(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}

/// <summary>Input record for <see cref="TeamsMcpTools.SendApprovalCardCoreAsync"/>.</summary>
public record TeamsApprovalCardRequest(
    string ApprovalId,
    string CaseNumber,
    string ClientName,
    string Policy,
    string Cover,
    string ClaimType,
    string ExpectedBenefit,
    IReadOnlyList<string> Checks,
    string Recommendation,
    string? Reason,
    string Kind = ClaimDecisionKinds.Lodgement,
    string? Payee = null,
    string? LodgementDate = null,
    string? Approver = null);

/// <summary>Result envelope returned by <see cref="TeamsMcpTools.SendApprovalCardCoreAsync"/>.</summary>
public record TeamsCardSendResult(bool Sent, string Channel, string ApprovalUrl, string Message, object? Card = null);
