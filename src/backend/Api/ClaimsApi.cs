using System.Text;
using OpenAI.Responses;
using CaldovaLife.Agents;
using CaldovaLife.App.Mcp;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

/// <summary>Claim details confirmed on step 2 of the Claims page. Any field left null falls back to the case.</summary>
record ClaimsProcessRequest(
    string CaseNumber,
    string? ClaimType,
    string? ClaimantName,
    string? ClaimantRelationship,
    string? AccountName,
    string? Bsb,
    string? Account,
    string? EventDate,
    decimal? InsuredAmount,
    decimal? AnnualEarnings,
    int? WaitingPeriodDays,
    decimal? BeneficiarySharePercent);

record ClaimsDecisionRequest(string? DecidedBy, string? Comment);

/// <summary>
/// HTTP endpoints behind the Claims Agent page (<c>/agents/claims</c>, Ben —
/// Benefit Claims Advocate):
///
/// 1. List the Client Cases in the in-memory <see cref="CaseStore"/> and
///    return the claim details for a case (new advice cases get a short note
///    pointing to the right agents instead).
/// 2. Engage the Foundry <c>ClaimsAgent</c> (streamed over SSE) after the
///    Claims desk has run the claimant, policy and benefit checks and raised
///    the Microsoft Teams approval — so the page always has a result, even
///    when Foundry isn't configured.
/// 3. Approve / reject (the in-page stand-in for the Teams card), resume the
///    agent after approval, poll the approval and lodge the claim — lodging
///    refuses unless a person has approved it.
/// </summary>
public static class ClaimsApi
{
    private const string AgentRole = "claims";

    public static void MapClaimsEndpoints(
        this WebApplication app,
        CaseStore store,
        CaldovaAgentFactory factory,
        ILogger logger)
    {
        // Always registered: TeamsMcpTools simulates delivery so the claim
        // flow works end to end without external infrastructure.
        var approvals = app.Services.GetRequiredService<ClaimApprovalStore>();
        var teams = app.Services.GetRequiredService<TeamsMcpTools>();

        // MCP tool descriptor pointing the Claims Agent at the claims_* tools
        // served from /mcp on this app. APP_MCP_URL must be publicly
        // reachable (dev tunnel) because the Foundry Responses API enumerates
        // MCP tools from the cloud. When unset, the agent runs without the
        // tool surface and is handed the desk's pre-executed tool results.
        var configuredMcpUrl = app.Configuration["APP_MCP_URL"];
        ResponseTool? claimsMcpTool = null;
        if (!string.IsNullOrWhiteSpace(configuredMcpUrl))
        {
            try
            {
                claimsMcpTool = ResponseTool.CreateMcpTool(
                    serverLabel: CaldovaAgent.DefaultMcpServerLabel,
                    serverUri: new Uri($"{configuredMcpUrl.TrimEnd('/')}/mcp"),
                    toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.AlwaysRequireApproval));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to construct the claims MCP tool descriptor; the Claims Agent will run without it.");
            }
        }
        else
        {
            logger.LogWarning("APP_MCP_URL is not configured; the Claims Agent will run without the claims_* MCP tool surface. Set APP_MCP_URL to a publicly reachable tunnel URL to enable it.");
        }

        List<ResponseTool>? BuildExtraTools() =>
            claimsMcpTool is null ? null : new List<ResponseTool> { claimsMcpTool };

        CaldovaAgent? TryCreateAgent(out string? error)
        {
            error = null;
            if (!factory.IsConfigured) return null;
            try
            {
                return factory.Create(AgentRole, BuildExtraTools());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Claims Agent could not be created; falling back to the offline result");
                error = ex.Message;
                return null;
            }
        }

        // ── Cases and claim details ─────────────────────────────────────

        app.MapGet("/claims/cases", () => Results.Ok(store.All().Select(r =>
        {
            var profile = ClaimRules.ProfileFor(r);
            return new
            {
                caseNumber = r.CaseNumber,
                clientName = r.ClientName,
                requestType = r.RequestType,
                clientNumber = r.ClientNumber,
                cover = r.Cover,
                amount = r.Amount,
                sampleId = r.SampleId,
                priority = r.Priority,
                claimFlow = profile.Flow,
                createdAt = r.CreatedAt
            };
        })));

        app.MapGet("/claims/cases/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            return record is null
                ? Results.NotFound(new { error = $"case '{caseNumber}' not found" })
                : Results.Ok(record);
        });

        // Deterministic, editable claim details for the case (step 2).
        app.MapGet("/claims/inputs/{caseNumber}", (string caseNumber) =>
        {
            var record = store.Get(caseNumber);
            if (record is null)
                return Results.NotFound(new { error = $"case '{caseNumber}' not found" });

            var p = ClaimRules.ProfileFor(record);
            var current = approvals.CurrentForCase(record.CaseNumber);
            if (p.Flow == ClaimFlows.NotAClaim)
            {
                return Results.Ok(new
                {
                    caseNumber = record.CaseNumber,
                    sampleId = record.SampleId,
                    profileId = p.ProfileId,
                    flow = p.Flow,
                    clientDisplay = p.ClientDisplay,
                    notAClaim = new
                    {
                        note = p.NotAClaimNote,
                        links = p.NotAClaimLinks.Select(l => new { label = l.Label, href = l.Href })
                    }
                });
            }

            return Results.Ok(new
            {
                caseNumber = record.CaseNumber,
                sampleId = record.SampleId,
                profileId = p.ProfileId,
                flow = p.Flow,
                clientDisplay = p.ClientDisplay,
                clientNumber = p.ClientNumber,
                policy = PolicyView(p, null),
                claimType = p.ClaimType,
                claimTypes = Options(ClaimCoverTypes.All),
                claimantName = p.ClaimantName,
                claimantRelationship = p.ClaimantRelationship,
                relationships = Options(ClaimantRelationships.All),
                supportPerson = p.SupportPerson,
                verifiedContact = p.VerifiedContact,
                recentChange = p.RecentChange,
                accountName = p.AccountName,
                bsb = p.Bsb,
                account = p.Account,
                accountOnFile = p.AccountOnFileLast4 is null ? null : $"{p.AccountOnFileName ?? p.ClientName} · ****{p.AccountOnFileLast4}",
                accountNote = p.AccountNote,
                eventLabel = p.EventLabel,
                eventDate = p.EventDate is { } d ? ClaimRules.FormatShortDate(d) : null,
                eventDescription = p.EventDescription,
                notification = p.Notification,
                insuredAmount = p.InsuredAmount,
                annualEarnings = p.AnnualEarnings,
                earningsPeriod = p.EarningsPeriod,
                earningsEvidence = p.EarningsEvidence,
                replacementRatePercent = p.ReplacementRatePercent,
                waitingPeriodDays = p.WaitingPeriodDays,
                beneficiarySharePercent = p.BeneficiarySharePercent,
                amountClaimed = p.AmountClaimed,
                vulnerable = p.Vulnerable,
                vulnerabilityNote = p.VulnerabilityNote,
                hint = p.InputsHint,
                currentApproval = current is null ? null : SerializeApproval(current, teams.IsConfigured, factory.IsConfigured)
            });
        });

        // ── Engage the Claims Agent ─────────────────────────────────────
        //
        // The desk runs the deterministic checks and raises the approval
        // (posting the Teams card) BEFORE the agent runs, so the page always
        // has a result and claims_requestApproval returns the same approval
        // id (one live approval per case). Send `Accept: text/event-stream`
        // to stream the agent's reply.
        app.MapPost("/claims/process", async (HttpContext httpContext, ClaimsProcessRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var record = store.Get(request.CaseNumber.Trim());
            if (record is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            var run = ClaimRules.BuildRun(record, ToInputs(request));
            if (run.Flow == ClaimFlows.NotAClaim)
            {
                var notAClaim = new
                {
                    caseNumber = record.CaseNumber,
                    clientName = record.ClientName,
                    flow = run.Flow,
                    note = run.Profile.NotAClaimNote,
                    links = run.Profile.NotAClaimLinks.Select(l => new { label = l.Label, href = l.Href }),
                    agentConfigured = factory.IsConfigured
                };
                if (AgentSseStreaming.WantsEventStream(httpContext))
                {
                    await AgentSseStreaming.StreamAsync(httpContext, null, string.Empty, (_, _) => notAClaim, logger, "Claims Agent");
                    return Results.Empty;
                }
                return Results.Ok(notAClaim);
            }

            var claimUpdate = BuildClaimUpdate(run);
            ClaimApprovalRecord? approval = null;
            TeamsCardSendResult? teamsResult = null;
            if (run.Flow == ClaimFlows.Hold)
            {
                approvals.SupersedeOpenForCase(record.CaseNumber, "a claim that isn't ready to lodge");
            }
            else
            {
                var approvalId = ClaimApprovalStore.NewApprovalId();
                approvals.SupersedeOpenForCase(record.CaseNumber, approvalId);
                approval = approvals.Add(ClaimRules.CreateApprovalRecord(run, approvalId, ClaimRules.RaisedByDesk, claimUpdate));
                teamsResult = await teams.SendApprovalCardCoreAsync(ClaimsMcpTools.BuildTeamsRequest(approval));
                approval.TeamsSent = teamsResult.Sent;
                approval.TeamsChannel = teamsResult.Channel;
                approval.TeamsMessage = teamsResult.Message;
                approval.ApprovalUrl = teamsResult.ApprovalUrl;
            }

            var mockMode = claimsMcpTool is null;
            var deskToolCalls = BuildDeskToolCalls(run, approval, teamsResult);
            var prompt = BuildPrompt(run, approval, mockMode ? deskToolCalls : null);

            logger.LogInformation(
                "Claims process: case={CaseNumber} flow={Flow} benefit={Benefit} approvalId={ApprovalId}",
                Sanitize(record.CaseNumber), run.Flow, run.Benefit.BenefitDisplay, Sanitize(approval?.ApprovalId ?? "-"));

            string? createError = null;

            object BuildEnvelope(AgentTraceResult? trace, string? agentError)
            {
                var error = agentError ?? createError;

                // Capture the response id so the approval popup can resume
                // this conversation and let the agent lodge the claim.
                if (approval is not null && trace?.ResponseId is { Length: > 0 } responseId)
                    approval.PreviousResponseId = responseId;

                var liveText = trace?.Text;
                var offline = string.IsNullOrWhiteSpace(liveText);
                return new
                {
                    caseNumber = record.CaseNumber,
                    clientName = record.ClientName,
                    clientNumber = run.Profile.ClientNumber,
                    clientDisplay = run.Profile.ClientDisplay,
                    cover = record.Cover,
                    requestType = record.RequestType,
                    sampleId = record.SampleId,
                    flow = run.Flow,
                    claimType = run.ClaimType,
                    claimTypeLabel = ClaimCoverTypes.ClaimLabel(run.ClaimType),
                    policy = PolicyView(run.Profile, run.Policy),
                    claimantChecks = ChecksView(run.Claimant.Checks),
                    policyChecks = ChecksView(run.Policy.Checks),
                    claimPack = run.Policy.ClaimPack.Select(i => new { document = i.Document, status = i.Status, note = i.Note }),
                    benefit = BenefitView(run),
                    timeframes = run.Timeframes.Select(f => new { label = f.Label, value = f.Value }),
                    notes = NotesView(run),
                    activity = run.Flow == ClaimFlows.Refer
                        ? run.Profile.ActivityTimeline.Select(e => new { when = e.When, what = e.What })
                        : Enumerable.Empty<object>(),
                    humanApprovalRequired = run.Approval.Required,
                    humanApprovalReasons = run.Approval.Reasons,
                    approver = run.Approval.Approver,
                    recommendation = run.Approval.Recommendation,
                    lodgeAllowed = run.Flow == ClaimFlows.Lodge,
                    lodgeBlockedReason = LodgeBlockedReason(run),
                    claimApproval = approval is null ? null : SerializeApproval(approval, teams.IsConfigured, factory.IsConfigured),
                    confirmationTitle = run.Flow switch
                    {
                        ClaimFlows.Refer => "Internal note — no claim lodged",
                        ClaimFlows.Hold => "Internal note — not ready to lodge",
                        _ => "Claim update draft (for Cara)"
                    },
                    claimUpdate = ClaimRules.RenderClaimUpdate(claimUpdate, approval),
                    agentNotes = offline ? BuildOfflineNarrative(run, approval, teamsResult, factory.IsConfigured, error) : liveText,
                    narrativeSource = offline ? "offline" : "agent",
                    agentConfigured = factory.IsConfigured,
                    agentError = error,
                    agentInput = trace?.Input,
                    agentRawOutput = BuildRawOutput(run, trace, mockMode, deskToolCalls),
                    mcpConfigured = claimsMcpTool is not null
                };
            }

            if (AgentSseStreaming.WantsEventStream(httpContext))
            {
                var streamingAgent = TryCreateAgent(out createError);
                await AgentSseStreaming.StreamAsync(httpContext, streamingAgent, prompt, BuildEnvelope, logger, "Claims Agent");
                return Results.Empty;
            }

            AgentTraceResult? traceResult = null;
            string? invocationError = null;
            var agent = TryCreateAgent(out createError);
            if (agent is not null)
            {
                try
                {
                    traceResult = await agent.RunWithTraceAsync(prompt);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Claims Agent invocation failed; falling back to the offline result");
                    invocationError = ex.Message;
                }
            }

            return Results.Ok(BuildEnvelope(traceResult, invocationError));
        });

        // ── Claim approvals (status, Teams fallback, lodgement) ─────────

        app.MapGet("/claims/approvals", (string? caseNumber) =>
        {
            var records = string.IsNullOrWhiteSpace(caseNumber)
                ? approvals.All()
                : approvals.ForCase(caseNumber);
            return Results.Ok(records.Select(r => SerializeApproval(r, teams.IsConfigured, factory.IsConfigured)));
        });

        app.MapGet("/claims/approvals/{approvalId}", (string approvalId) =>
        {
            var record = approvals.Get(approvalId);
            return record is null
                ? Results.NotFound(new { error = $"approval '{approvalId}' not found" })
                : Results.Ok(SerializeApproval(record, teams.IsConfigured, factory.IsConfigured));
        });

        // In-page stand-in for the Approve / Reject buttons on the Teams card.
        app.MapPost("/claims/approvals/{approvalId}/approve", (string approvalId, ClaimsDecisionRequest? body) =>
        {
            var result = approvals.Approve(approvalId, body?.DecidedBy, body?.Comment);
            if (result.Ok)
            {
                logger.LogInformation("Claim approval granted: {ApprovalId} ({Kind}) by {By}",
                    Sanitize(approvalId), result.Record!.Kind, Sanitize(result.Record.DecidedBy ?? string.Empty));
            }
            return ToResult(result, teams.IsConfigured, factory.IsConfigured);
        });

        app.MapPost("/claims/approvals/{approvalId}/reject", (string approvalId, ClaimsDecisionRequest? body) =>
        {
            var result = approvals.Reject(approvalId, body?.DecidedBy, body?.Comment);
            if (result.Ok)
            {
                logger.LogInformation("Claim approval rejected: {ApprovalId} by {By}",
                    Sanitize(approvalId), Sanitize(result.Record!.DecidedBy ?? string.Empty));
            }
            return ToResult(result, teams.IsConfigured, factory.IsConfigured);
        });

        // Lodge — refuses unless a lodgement decision has been approved.
        app.MapPost("/claims/approvals/{approvalId}/lodge", (string approvalId) =>
        {
            var result = approvals.Lodge(approvalId);
            if (result.Ok)
            {
                logger.LogInformation("Claim lodged: {ApprovalId} ref={Reference}",
                    Sanitize(approvalId), Sanitize(result.Record!.LodgementReference ?? string.Empty));
            }
            return ToResult(result, teams.IsConfigured, factory.IsConfigured);
        });

        // Approval popup: record the decision, then resume the same Claims
        // Agent conversation (response id captured by /claims/process) so the
        // agent calls claims_lodgeClaim over MCP. Without the MCP surface the
        // desk lodges first and hands the agent the tool result.
        app.MapPost("/claims/approvals/{approvalId}/agent-approve", async (string approvalId, ClaimsDecisionRequest? body) =>
        {
            var decision = approvals.Approve(approvalId, body?.DecidedBy, body?.Comment);
            if (!decision.Ok)
                return ToResult(decision, teams.IsConfigured, factory.IsConfigured);

            var record = decision.Record!;
            logger.LogInformation("Claim popup approval granted: {ApprovalId} ({Kind}) by {By}",
                Sanitize(record.ApprovalId), record.Kind, Sanitize(record.DecidedBy ?? string.Empty));

            string? agentNarrative = null;
            string? agentError = null;
            string? agentResponseId = null;
            var resumeAttempted = !string.IsNullOrWhiteSpace(record.PreviousResponseId) && factory.IsConfigured;

            if (resumeAttempted)
            {
                try
                {
                    var agent = factory.Create(AgentRole, BuildExtraTools());
                    var trace = await agent.ChatWithTraceAsync(record.PreviousResponseId!, BuildResumeMessage(record, claimsMcpTool is null, approvals));
                    agentNarrative = trace.Text;
                    agentResponseId = trace.ResponseId;
                    record.AgentResumeNotes = trace.Text;
                    if (!string.IsNullOrEmpty(trace.ResponseId))
                        record.PreviousResponseId = trace.ResponseId;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Claims Agent resume after approval failed for {ApprovalId}", Sanitize(record.ApprovalId));
                    agentError = ex.Message;
                }
            }

            // Re-read: the agent's MCP call may have lodged the claim.
            var refreshed = approvals.Get(approvalId) ?? record;
            return Results.Ok(new
            {
                approval = SerializeApproval(refreshed, teams.IsConfigured, factory.IsConfigured),
                agentConfigured = factory.IsConfigured,
                agentResumeAttempted = resumeAttempted,
                agentNarrative,
                agentResponseId,
                agentError
            });
        });
    }

    private static ClaimRunInputs ToInputs(ClaimsProcessRequest r) => new()
    {
        ClaimType = r.ClaimType,
        ClaimantName = r.ClaimantName,
        ClaimantRelationship = r.ClaimantRelationship,
        AccountName = r.AccountName,
        Bsb = r.Bsb,
        Account = r.Account,
        EventDate = r.EventDate,
        InsuredAmount = r.InsuredAmount is { } i ? Math.Max(0m, i) : null,
        AnnualEarnings = r.AnnualEarnings is { } e ? Math.Max(0m, e) : null,
        WaitingPeriodDays = r.WaitingPeriodDays is { } w ? Math.Clamp(w, 0, 730) : null,
        BeneficiarySharePercent = r.BeneficiarySharePercent,
    };

    /// <summary>The message that resumes the agent after the approver's decision.</summary>
    private static string BuildResumeMessage(ClaimApprovalRecord record, bool noMcp, ClaimApprovalStore approvals)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HUMAN APPROVAL DECISION");
        sb.AppendLine("=======================");
        sb.AppendLine(record.IsBlockDecision
            ? "The Team Leader has APPROVED THE BLOCK in Microsoft Teams. The claim is NOT lodged."
            : "The approver has APPROVED lodging the claim in Microsoft Teams.");
        sb.AppendLine($"Approval id: {record.ApprovalId}");
        sb.AppendLine($"Approved by: {record.DecidedBy}");
        sb.AppendLine($"Approved at: {record.DecidedAt:O}");
        if (!string.IsNullOrWhiteSpace(record.DecisionComment))
            sb.AppendLine($"Approver comment: {record.DecisionComment}");
        sb.AppendLine($"Case: {record.CaseNumber} — {record.ClientDisplay}");
        sb.AppendLine($"Policy: {record.Insurer} {record.PolicyNumber} — {ClaimCoverTypes.ClaimLabel(record.ClaimType)}");
        sb.AppendLine($"Expected benefit: {record.ExpectedBenefitDisplay}");
        sb.AppendLine();

        if (record.IsBlockDecision)
        {
            sb.AppendLine("Do NOT call claims_lodgeClaim — it refuses a block decision. Briefly confirm that no claim is lodged, " +
                "what is protected, and the next steps for Financial Crime and Client Communications. Never accuse the client.");
            return sb.ToString();
        }

        if (noMcp)
        {
            var lodged = approvals.Lodge(record.ApprovalId);
            sb.AppendLine("MOCK MCP TOOL RESULT");
            sb.AppendLine("====================");
            sb.AppendLine("Note: APP_MCP_URL is not configured. claims_lodgeClaim has been executed locally by the Claims desk with the " +
                "following result; treat it as authoritative.");
            sb.AppendLine();
            sb.AppendLine("claims_lodgeClaim →");
            sb.AppendLine(ClaimsMcpTools.ToJson(ClaimsMcpTools.LodgePayload(lodged)));
            sb.AppendLine();
            sb.AppendLine("claims_lodgeClaim has ALREADY been executed for this run — do NOT call it again and do NOT say the tool is " +
                "unavailable. Briefly summarise the outcome: lodgement reference, the insurer's Life Insurance Code of Practice " +
                "timeframes, and what the client will hear next.");
        }
        else
        {
            sb.AppendLine($"Please now call the claims_lodgeClaim MCP tool with approvalId='{record.ApprovalId}' to lodge the claim, then briefly " +
                "summarise the outcome: lodgement reference, the insurer's Life Insurance Code of Practice timeframes, and what the client will hear next.");
        }
        return sb.ToString();
    }

    // ── Views for the page ──────────────────────────────────────────────

    private static object PolicyView(ClaimCaseProfile p, ClaimPolicyCheckResult? policy) => new
    {
        insurer = p.Insurer,
        policyNumber = p.PolicyNumber,
        policyOwner = p.PolicyOwner,
        lifeInsured = p.LifeInsured,
        started = p.PolicyStartDisplay,
        timeInForce = policy?.TimeInForce ?? p.TimeInForceText
            ?? (p.PolicyStart is { } start ? ClaimRules.DescribeDuration(start, p.LodgementDate ?? ClaimRules.MelbourneToday()) : null),
        premiums = p.Premiums,
        heldOutsideSuper = p.HeldOutsideSuper,
        cover = p.CoverLines,
        definition = p.Definition,
        exclusions = p.Exclusions,
        waitingPeriodDays = p.WaitingPeriodDays,
        benefitPeriod = p.BenefitPeriod,
        beneficiaries = p.Beneficiaries.Select(b => new
        {
            name = b.Name,
            relationship = b.Relationship,
            share = ClaimRules.FormatPercent(b.SharePercent)
        }),
        pdsTitle = p.PdsTitle
    };

    private static IEnumerable<object> ChecksView(IEnumerable<ClaimCheckLine> checks) =>
        checks.Select(c => new
        {
            key = c.Key,
            label = c.Label,
            status = c.Status,
            value = c.Value,
            detail = c.Detail,
            source = c.Source
        });

    private static object BenefitView(ClaimRun run)
    {
        var b = run.Benefit;
        if (run.Flow == ClaimFlows.Refer)
        {
            var amount = run.AmountAtStake;
            return new
            {
                mode = "protected",
                title = "Claim not lodged",
                rows = new[]
                {
                    new { label = $"{ClaimCoverTypes.ClaimLabel(run.ClaimType)} — amount claimed", kind = "input", display = Fmt(amount) },
                    new { label = $"Lodged with {run.Profile.Insurer}", kind = "deduct", display = Fmt(0m) },
                    new { label = "Paid out", kind = "deduct", display = Fmt(0m) },
                    new { label = "Held back while the claim is referred", kind = "total", display = Fmt(amount) },
                },
                resultLabel = $"Lodged with {run.Profile.Insurer}",
                resultDisplay = "$0 — nothing lodged",
                paidTo = (string?)null,
                notes = new[]
                {
                    "No benefit is calculated: the claimant checks failed, so nothing goes to the insurer while Financial Crime reviews the claim.",
                },
                basis = (string?)null,
                taxTreatment = (string?)null,
                ruleCited = $"{ClaimRules.ClaimsRuleTitle} · {ClaimRules.FinancialCrimeGuideTitle}",
                taxRuleCited = (string?)null
            };
        }

        return new
        {
            mode = b.Mode,
            title = b.Title,
            rows = b.Steps.Select(s => new { label = s.Label, kind = s.Kind, display = s.Display }).ToArray(),
            resultLabel = b.Mode == "income" ? "Expected monthly benefit" : "Expected benefit",
            resultDisplay = b.BenefitDisplay,
            paidTo = (string?)b.PaidTo,
            notes = b.Notes.ToArray(),
            basis = b.ClaimType == ClaimCoverTypes.IncomeProtection ? ClaimRules.IncomeBenefitBasis(run.Profile) : null,
            taxTreatment = (string?)b.TaxTreatment,
            ruleCited = b.RuleCited,
            taxRuleCited = (string?)b.TaxRuleCited
        };
    }

    private static List<object> NotesView(ClaimRun run)
    {
        var p = run.Profile;
        var notes = new List<object>();
        foreach (var note in p.Notes)
        {
            // The adviser note is about the insured benefit being above what
            // the earnings support — only show it when that's what happened.
            if (note.Tone == "adviser" && run.ClaimType == ClaimCoverTypes.IncomeProtection && !run.Benefit.LimitedByEarnings)
                continue;
            notes.Add(new { title = note.Title, text = note.Text, tone = note.Tone });
        }
        if (p.Vulnerable && !string.IsNullOrWhiteSpace(p.VulnerabilityNote))
            notes.Add(new { title = "A person first", text = p.VulnerabilityNote, tone = "care" });
        if (!string.IsNullOrWhiteSpace(p.FinancialCrimeNote) && run.Flow != ClaimFlows.Hold)
            notes.Add(new { title = run.Flow == ClaimFlows.Refer ? "Financial Crime review" : "Financial Crime", text = p.FinancialCrimeNote, tone = run.Flow == ClaimFlows.Refer ? "care" : "info" });
        if (run.Flow == ClaimFlows.Refer && !string.IsNullOrWhiteSpace(p.ReferralNextStep))
            notes.Add(new { title = "Next steps", text = p.ReferralNextStep, tone = "info" });
        return notes;
    }

    private static string? LodgeBlockedReason(ClaimRun run) => run.Flow switch
    {
        ClaimFlows.Refer => $"Lodging is disabled — the claimant checks failed and the claim is referred to Financial Crime. Nothing goes to {run.Profile.Insurer}.",
        ClaimFlows.Hold => $"Lodging is disabled. {string.Join(" ", run.Approval.Reasons)}",
        _ => null
    };

    private static object SerializeApproval(ClaimApprovalRecord r, bool teamsConfigured, bool agentConfigured)
    {
        var lodged = r.Status == ClaimApprovalStatus.Lodged;
        var blocked = r.Status == ClaimApprovalStatus.Blocked;
        return new
        {
            approvalId = r.ApprovalId,
            caseNumber = r.CaseNumber,
            kind = r.Kind,
            cardTitle = r.IsBlockDecision ? TeamsMcpTools.BlockCardTitle : TeamsMcpTools.LodgementCardTitle,
            clientName = r.ClientName,
            clientNumber = r.ClientNumber,
            clientDisplay = r.ClientDisplay,
            insurer = r.Insurer,
            policyNumber = r.PolicyNumber,
            coverDisplay = r.CoverDisplay,
            claimType = r.ClaimType,
            claimTypeLabel = ClaimCoverTypes.ClaimLabel(r.ClaimType),
            claimantName = r.ClaimantName,
            claimantRole = r.ClaimantRole,
            payeeName = r.PayeeName,
            payeeAccount = r.PayeeAccount,
            expectedBenefit = r.ExpectedBenefit,
            expectedBenefitDisplay = r.ExpectedBenefitDisplay,
            amountClaimed = r.AmountClaimed,
            amountClaimedFormatted = r.AmountClaimed is { } a ? Fmt(a) : null,
            checks = r.Checks,
            recommendation = r.Recommendation,
            reason = r.Reason,
            approvalReasons = r.ApprovalReasons,
            approver = r.Approver,
            raisedBy = r.RaisedBy,
            lodgementDate = r.LodgementDate is { } d ? ClaimRules.FormatDate(d) : null,
            status = r.StatusKey,
            decision = r.Decision,
            decidedBy = r.DecidedBy,
            decidedAt = r.DecidedAt,
            decisionComment = r.DecisionComment,
            lodgementReference = r.LodgementReference,
            lodgedAt = r.LodgedAt,
            canLodge = !r.IsBlockDecision && r.Status == ClaimApprovalStatus.Approved,
            insurerOutcome = lodged
                ? r.InsurerOutcome.Select(e => new { when = e.When, what = e.What })
                : Enumerable.Empty<object>(),
            outcomeLabel = lodged || blocked ? r.OutcomeLabel : null,
            outcomeSummary = lodged || blocked ? r.OutcomeSummary : null,
            protectedNote = blocked ? r.ProtectedNote : null,
            timeframes = r.Timeframes.Select(f => new { label = f.Label, value = f.Value }),
            teamsSent = r.TeamsSent,
            teamsChannel = r.TeamsChannel,
            teamsMessage = r.TeamsMessage,
            teamsConfigured,
            approvalUrl = r.ApprovalUrl,
            claimUpdate = ClaimRules.RenderClaimUpdate(r.ClaimUpdateDraft, r),
            previousResponseId = r.PreviousResponseId,
            agentResumeAvailable = agentConfigured && !string.IsNullOrWhiteSpace(r.PreviousResponseId),
            agentResumeNotes = r.AgentResumeNotes,
            createdAt = r.CreatedAt
        };
    }

    private static IResult ToResult(ClaimTransitionResult result, bool teamsConfigured, bool agentConfigured)
    {
        if (result.Ok)
            return Results.Ok(SerializeApproval(result.Record!, teamsConfigured, agentConfigured));
        if (result.Record is null)
            return Results.NotFound(new { error = result.Error });
        return Results.Conflict(new
        {
            error = result.Error,
            approval = SerializeApproval(result.Record, teamsConfigured, agentConfigured)
        });
    }

    private static IEnumerable<object> Options(IEnumerable<ClaimOption> options) =>
        options.Select(o => new { key = o.Key, label = o.Label });

    // ── Desk tool calls (the claims_* MCP tools run deterministically) ──

    private sealed record DeskToolCall(string Tool, object Arguments, object Result, object? AdaptiveCard = null);

    private static List<DeskToolCall> BuildDeskToolCalls(ClaimRun run, ClaimApprovalRecord? approval, TeamsCardSendResult? teamsResult)
    {
        var c = run.Case;
        var calls = new List<DeskToolCall>
        {
            new("claims_validateClaimant",
                new
                {
                    caseNumber = c.CaseNumber,
                    claimantName = run.Claimant.ClaimantName,
                    relationship = run.Claimant.Relationship,
                    accountName = run.Claimant.AccountName,
                    accountNumber = run.Claimant.MaskedAccount
                },
                ClaimsMcpTools.ValidateClaimantPayload(run)),
            new("claims_checkPolicy",
                new
                {
                    caseNumber = c.CaseNumber,
                    claimType = run.ClaimType,
                    eventDate = run.EventDate is { } d ? ClaimRules.FormatShortDate(d) : null
                },
                ClaimsMcpTools.CheckPolicyPayload(run)),
        };

        if (run.Flow != ClaimFlows.Refer)
        {
            calls.Add(new("claims_calculateBenefit",
                new
                {
                    caseNumber = c.CaseNumber,
                    claimType = run.ClaimType,
                    insuredAmount = run.Benefit.InsuredAmount,
                    annualEarnings = run.ClaimType == ClaimCoverTypes.IncomeProtection && run.Benefit.MonthlyEarnings is { } m ? m * 12m : (decimal?)null
                },
                ClaimsMcpTools.CalculateBenefitPayload(run)));
        }

        calls.Add(new("claims_checkApprovalRules",
            new { caseNumber = c.CaseNumber, claimType = run.ClaimType, claimantName = run.Claimant.ClaimantName, accountName = run.Claimant.AccountName },
            ClaimsMcpTools.CheckApprovalRulesPayload(run)));

        if (approval is not null)
        {
            calls.Add(new("claims_requestApproval",
                new
                {
                    caseNumber = c.CaseNumber,
                    expectedBenefit = approval.ExpectedBenefit,
                    recommendation = approval.Recommendation
                },
                ClaimsMcpTools.RequestApprovalPayload(approval, teamsResult, alreadyRaised: false)));
        }

        if (approval is not null && teamsResult is not null)
        {
            var card = ClaimsMcpTools.BuildTeamsRequest(approval);
            calls.Add(new("teams_sendApprovalCard",
                new
                {
                    approvalId = card.ApprovalId,
                    caseNumber = card.CaseNumber,
                    clientName = card.ClientName,
                    policy = card.Policy,
                    cover = card.Cover,
                    claimType = card.ClaimType,
                    expectedBenefit = card.ExpectedBenefit,
                    checks = string.Join("\n", card.Checks),
                    recommendation = card.Recommendation,
                    reason = card.Reason,
                    decision = card.Kind == ClaimDecisionKinds.Block ? "block" : "lodge",
                    payee = card.Payee,
                    lodgementDate = card.LodgementDate
                },
                new
                {
                    tool = "teams_sendApprovalCard",
                    calledBy = "claims_requestApproval",
                    approvalId = approval.ApprovalId,
                    caseNumber = approval.CaseNumber,
                    kind = approval.Kind,
                    sent = teamsResult.Sent,
                    channel = teamsResult.Channel,
                    approvalUrl = teamsResult.ApprovalUrl,
                    message = teamsResult.Message
                },
                teamsResult.Card));
        }

        return calls;
    }

    /// <summary>Knowledge-index titles the offline run relies on (shown on the Reference tab).</summary>
    private static IEnumerable<object> OfflineCitations(ClaimRun run)
    {
        var titles = new List<string>();
        if (run.Profile.PdsTitle.Length > 0 && run.Profile.PdsTitle != ClaimRules.ClaimsRuleTitle)
            titles.Add(run.Profile.PdsTitle);
        titles.Add(ClaimRules.ClaimsRuleTitle);
        titles.Add(ClaimRules.DisclosureRuleTitle);
        if (run.Flow != ClaimFlows.Refer)
            titles.Add(ClaimRules.TaxRuleTitle);
        titles.Add(ClaimRules.AuthorityRuleTitle);
        titles.Add(ClaimRules.InGoodOrderTitle);
        if (run.Flow == ClaimFlows.Refer || !string.IsNullOrWhiteSpace(run.Profile.FinancialCrimeNote))
            titles.Add(ClaimRules.FinancialCrimeGuideTitle);
        titles.AddRange(run.Profile.CaseFiles);
        return titles.Distinct().Select(t => new { title = t, url = string.Empty });
    }

    private static object BuildRawOutput(ClaimRun run, AgentTraceResult? trace, bool mockMode, IReadOnlyList<DeskToolCall> deskToolCalls)
    {
        var toolCalls = deskToolCalls.Select(call => call.AdaptiveCard is null
            ? (object)new { tool = call.Tool, arguments = call.Arguments, result = call.Result }
            : new { tool = call.Tool, arguments = call.Arguments, result = call.Result, adaptiveCard = call.AdaptiveCard }).ToList();

        if (trace is not null)
        {
            return new
            {
                text = trace.Text,
                citations = trace.Citations,
                outputItems = trace.OutputItems,
                responseId = trace.ResponseId,
                durationMs = trace.DurationMs,
                preExecutedToolCalls = mockMode ? toolCalls : null
            };
        }

        return new
        {
            mode = "offline",
            note = "The Foundry agent was not invoked, so Ben's Claims desk ran the claims_* MCP tools locally. These are the tool calls and results behind the page, and the documents the result relies on.",
            citations = OfflineCitations(run),
            toolCalls
        };
    }

    // ── Agent prompt ────────────────────────────────────────────────────

    private static string BuildPrompt(ClaimRun run, ClaimApprovalRecord? approval, IReadOnlyList<DeskToolCall>? preExecuted)
    {
        var c = run.Case;
        var p = run.Profile;
        var sb = new StringBuilder();

        sb.AppendLine("CLIENT CASE");
        sb.AppendLine("===========");
        sb.AppendLine($"Case number: {c.CaseNumber}");
        sb.AppendLine($"Client: {p.ClientDisplay}");
        sb.AppendLine($"Request type: {c.RequestType}");
        sb.AppendLine($"Received: {c.ReceivedDate}");
        sb.AppendLine($"Adviser: {c.AdviserName}");
        if (!string.IsNullOrWhiteSpace(c.ReferredBy))
            sb.AppendLine($"Referred by: {c.ReferredBy}");
        sb.AppendLine($"Request summary: {c.RequestSummary}");
        sb.AppendLine($"Priority: {c.Priority} — {c.PriorityReason}");
        sb.AppendLine();

        sb.AppendLine("POLICY (from the policy schedule)");
        sb.AppendLine("=================================");
        sb.AppendLine($"Insurer and policy: {p.Insurer} {p.PolicyNumber} (held {(p.HeldOutsideSuper ? "outside" : "inside")} super)");
        sb.AppendLine($"Policy owner: {p.PolicyOwner} · Life insured: {p.LifeInsured}");
        sb.AppendLine($"In force since: {p.PolicyStartDisplay} · Premiums: {p.Premiums}");
        sb.AppendLine("Cover:");
        foreach (var line in p.CoverLines)
            sb.AppendLine($"- {line}");
        if (p.Beneficiaries.Count > 0)
            sb.AppendLine($"Nominated beneficiaries: {string.Join(", ", p.Beneficiaries.Select(b => $"{b.Name} ({b.Relationship}) {ClaimRules.FormatPercent(b.SharePercent)}"))}");
        sb.AppendLine($"Definition the claim relies on: {p.Definition}");
        if (p.WaitingPeriodDays is { } waiting)
            sb.AppendLine($"Waiting period: {waiting} days · Benefit period: {p.BenefitPeriod ?? "as scheduled"}");
        sb.AppendLine($"Exclusions: {p.Exclusions}");
        sb.AppendLine();

        sb.AppendLine("CLAIM DETAILS (confirmed by Ben)");
        sb.AppendLine("================================");
        sb.AppendLine($"Claim type: {ClaimCoverTypes.ClaimLabel(run.ClaimType)}");
        if (run.EventDate is { } eventDate)
            sb.AppendLine($"{p.EventLabel}: {ClaimRules.FormatDate(eventDate)}");
        sb.AppendLine($"What happened: {p.EventDescription}");
        sb.AppendLine($"Notification: {p.Notification}");
        sb.AppendLine($"Claimant: {run.Claimant.ClaimantName} — {run.Claimant.RelationshipLabel}");
        if (!string.IsNullOrWhiteSpace(p.SupportPerson))
            sb.AppendLine($"Support person: {p.SupportPerson}");
        sb.AppendLine($"Verified contact details: {p.VerifiedContact}");
        if (!string.IsNullOrWhiteSpace(p.RecentChange))
            sb.AppendLine($"Recent change of contact details: {p.RecentChange}");
        sb.AppendLine($"Bank account for the benefit: {run.Claimant.AccountName} — {run.Claimant.MaskedAccount}");
        sb.AppendLine(run.ClaimType == ClaimCoverTypes.IncomeProtection
            ? $"Insured monthly benefit: {Fmt(run.Benefit.InsuredAmount)}"
            : $"Sum insured: {Fmt(run.Benefit.InsuredAmount)}");
        if (run.ClaimType == ClaimCoverTypes.IncomeProtection && run.Benefit.MonthlyEarnings is { } monthly)
            sb.AppendLine($"Earnings in the {p.EarningsPeriod ?? "12 months before the disability"}: {Fmt(monthly * 12m)} ({p.EarningsEvidence ?? "earnings evidence"})");
        if (p.AmountClaimed is { } claimed)
            sb.AppendLine($"Amount claimed: {Fmt(claimed)}");
        if (p.Vulnerable && !string.IsNullOrWhiteSpace(p.VulnerabilityNote))
            sb.AppendLine($"Vulnerability: {p.VulnerabilityNote}");
        sb.AppendLine();

        var notes = run.Profile.Notes
            .Where(n => !(n.Tone == "adviser" && run.ClaimType == ClaimCoverTypes.IncomeProtection && !run.Benefit.LimitedByEarnings))
            .ToList();
        if (notes.Count > 0)
        {
            sb.AppendLine("NOTES TO INCLUDE");
            sb.AppendLine("================");
            foreach (var note in notes)
                sb.AppendLine($"- {note.Title}: {note.Text}");
            sb.AppendLine();
        }

        if (approval is not null)
        {
            sb.AppendLine("CLAIMS DESK");
            sb.AppendLine("===========");
            sb.AppendLine(approval.IsBlockDecision
                ? $"Block decision {approval.ApprovalId} has been raised with {approval.Approver}, and the \"{TeamsMcpTools.BlockCardTitle}\" card has been posted to Microsoft Teams ({approval.TeamsChannel}). claims_lodgeClaim refuses a block decision. Approval link: {approval.ApprovalUrl}"
                : $"Approval {approval.ApprovalId} has been raised with {approval.Approver}, and the \"{TeamsMcpTools.LodgementCardTitle}\" card has been posted to Microsoft Teams ({approval.TeamsChannel}). Approval link: {approval.ApprovalUrl}");
            sb.AppendLine();
        }

        var mockBlock = preExecuted is null ? null : BuildMockToolResultsBlock(preExecuted);
        var toolNote = mockBlock is null
            ? null
            : "The claims_* MCP tools have ALREADY BEEN INVOKED for this run by the Claims desk — their results are in the MOCK MCP TOOL RESULTS section above. " +
              "Treat those JSON payloads as authoritative tool outputs. Do NOT say the tools are unavailable, do NOT ask for them to be enabled and do NOT try to call them again.";
        var cite = $"Cite documents by title — e.g. \"{p.PdsTitle}\", \"{ClaimRules.ClaimsRuleTitle}\", \"{ClaimRules.DisclosureRuleTitle}\", " +
                   $"\"{ClaimRules.TaxRuleTitle}\", \"{ClaimRules.AuthorityRuleTitle}\"" +
                   (p.CaseFiles.Count > 0 ? $" and the case files (e.g. \"{p.CaseFiles[0]}\")" : string.Empty) + ".";

        switch (run.Flow)
        {
            case ClaimFlows.Lodge:
                if (mockBlock is not null)
                {
                    sb.Append(mockBlock);
                    sb.AppendLine(toolNote);
                    sb.AppendLine("Do NOT lodge the claim in this run — stop after surfacing the approvalId and asking the approver to approve in Microsoft Teams.");
                }
                else
                {
                    sb.AppendLine("Use the claims_* MCP tools in this order: claims_validateClaimant, claims_checkPolicy, claims_calculateBenefit, " +
                        "claims_checkApprovalRules, then claims_requestApproval — it returns the approval the desk has already raised for this case" +
                        (approval is null ? string.Empty : $" ({approval.ApprovalId})") + " rather than a duplicate. " +
                        "Do NOT call claims_lodgeClaim in this run: stop and ask the approver to approve in Microsoft Teams.");
                }
                sb.AppendLine();
                sb.AppendLine("Then produce the standard Claims output (Claimant & Policy Checks, Claim Pack, Expected Benefit, Insurer Timeframes, Notes, " +
                    $"Approval Request, Human Approval Required). Show the benefit working step by step and the tax treatment. {cite} " +
                    "Mask bank details. Plain English and empathy; no personal advice — anything for Amelia is a note marked \"not advice\". " +
                    "End with an explicit ask for the approver to approve lodgement in Microsoft Teams.");
                break;

            case ClaimFlows.Refer:
                sb.AppendLine("THIS CLAIM MUST NOT BE LODGED");
                sb.AppendLine("=============================");
                sb.AppendLine("Claimant red flags:");
                foreach (var flag in run.Claimant.RedFlags)
                    sb.AppendLine($"- {flag}");
                if (!run.Policy.DefinitionMet)
                    sb.AppendLine($"- Definition not shown: {p.DefinitionNote}");
                sb.AppendLine();
                if (mockBlock is not null)
                {
                    sb.Append(mockBlock);
                    sb.AppendLine(toolNote);
                }
                else
                {
                    sb.AppendLine("Use claims_validateClaimant, claims_checkPolicy and claims_checkApprovalRules to evidence why the claim can't be lodged. " +
                        "claims_requestApproval returns the block decision the desk already raised. Do NOT call claims_lodgeClaim.");
                }
                sb.AppendLine();
                sb.AppendLine("Produce this output instead of the standard one:");
                sb.AppendLine("1. **Claimant Checks** — each red flag, stated as a fact. Never accuse the client — they may be the victim.");
                sb.AppendLine("2. **Policy Check** — the cover, the definition the claim relies on and what's missing.");
                sb.AppendLine($"3. **Recommendation — don't lodge** — nothing goes to {p.Insurer}; {Fmt(run.AmountAtStake)} is held back; refer the case to Financial Crime (Felix); contact the client only on details verified before any change.");
                sb.AppendLine("4. **Approval Request** — the block decision id and the Teams status.");
                sb.AppendLine("5. **Human Approval Required** — Yes: blocking is the Team Leader's decision. End by asking Theo to approve the block in Microsoft Teams.");
                sb.AppendLine(cite);
                break;

            default:
                sb.AppendLine("NOT READY TO LODGE");
                sb.AppendLine("==================");
                foreach (var reason in run.Approval.Reasons)
                    sb.AppendLine($"- {reason}");
                sb.AppendLine();
                if (mockBlock is not null)
                {
                    sb.Append(mockBlock);
                    sb.AppendLine(toolNote);
                }
                else
                {
                    sb.AppendLine("Use claims_validateClaimant, claims_checkPolicy and claims_checkApprovalRules to show what's missing. " +
                        "Do NOT call claims_requestApproval or claims_lodgeClaim.");
                }
                sb.AppendLine();
                sb.AppendLine("Produce: 1. **Checks** 2. **What's needed before lodging** 3. **Human Approval Required** — ask Amelia to confirm the claimant and the cover with the client.");
                sb.AppendLine(cite);
                break;
        }

        sb.AppendLine();
        sb.AppendLine("Keep the response under 700 words.");
        return sb.ToString();
    }

    private static string BuildMockToolResultsBlock(IReadOnlyList<DeskToolCall> calls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MOCK MCP TOOL RESULTS");
        sb.AppendLine("=====================");
        sb.AppendLine("Note: APP_MCP_URL is not configured, so the claims_* MCP tool surface has been pre-executed locally by the " +
            "Claims desk. The JSON below is what each tool returned and MUST be treated as authoritative for this run.");
        sb.AppendLine();
        foreach (var call in calls)
        {
            sb.AppendLine($"{call.Tool} →");
            sb.AppendLine(ClaimsMcpTools.ToJson(call.Result));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ── Offline narrative (no Foundry, or the agent call failed) ────────

    private static string BuildOfflineNarrative(
        ClaimRun run,
        ClaimApprovalRecord? approval,
        TeamsCardSendResult? teamsResult,
        bool agentConfigured,
        string? agentError)
    {
        var p = run.Profile;
        var sb = new StringBuilder();
        var why = !agentConfigured
            ? "the Foundry agent isn't configured"
            : $"the Foundry agent call didn't complete ({Shorten(agentError ?? "no reply", 160)})";
        sb.AppendLine($"> Offline run — {why}. Ben's Claims desk ran the claims_* MCP tools locally; see Raw Output for every call.");
        sb.AppendLine();

        switch (run.Flow)
        {
            case ClaimFlows.Lodge:
            {
                sb.AppendLine("**1. Claimant & Policy Checks**");
                sb.AppendLine();
                AppendChecks(sb, run.Claimant.Checks.Concat(run.Policy.Checks));
                sb.AppendLine("**2. Claim Pack**");
                sb.AppendLine();
                AppendClaimPack(sb, run.Policy.ClaimPack);
                sb.AppendLine("**3. Expected Benefit**");
                sb.AppendLine();
                AppendBenefit(sb, run);
                sb.AppendLine("**4. Insurer Timeframes — Life Insurance Code of Practice**");
                sb.AppendLine();
                foreach (var f in run.Timeframes)
                    sb.AppendLine($"- **{Md(f.Label)}:** {Md(f.Value)}");
                sb.AppendLine();
                sb.AppendLine($"_Rule: {ClaimRules.ClaimsRuleTitle}._");
                sb.AppendLine();
                sb.AppendLine("**5. Notes**");
                sb.AppendLine();
                AppendNotes(sb, run);
                sb.AppendLine("**6. Approval Request**");
                sb.AppendLine();
                AppendApproval(sb, approval, teamsResult);
                sb.AppendLine($"- **Recommendation:** {Md(run.Approval.Recommendation)}");
                sb.AppendLine();
                sb.AppendLine("**7. Human Approval Required**");
                sb.AppendLine();
                sb.AppendLine($"**Yes** — {Md(string.Join(" ", run.Approval.Reasons))}");
                sb.AppendLine();
                sb.AppendLine($"**Theo, please approve lodging {ClaimRules.Possessive(ClaimRules.FirstName(run.Claimant.ClaimantName))} " +
                    $"{ClaimCoverTypes.ClaimLabel(run.ClaimType).ToLowerInvariant()} with {p.Insurer} in Microsoft Teams — I'll lodge it only after you approve.**");
                break;
            }

            case ClaimFlows.Refer:
            {
                var first = ClaimRules.FirstName(p.ClientName.Length > 0 ? p.ClientName : run.Claimant.ClaimantName);
                sb.AppendLine("**1. Claimant Checks**");
                sb.AppendLine();
                AppendChecks(sb, run.Claimant.Checks);
                sb.AppendLine("**2. Policy Check**");
                sb.AppendLine();
                AppendChecks(sb, run.Policy.Checks.Where(c => c.Key is "in-force" or "definition" or "time-in-force"));
                if (p.ActivityTimeline.Count > 0)
                {
                    sb.AppendLine("**3. What Happened**");
                    sb.AppendLine();
                    foreach (var e in p.ActivityTimeline)
                        sb.AppendLine($"- **{Md(e.When)}** — {Md(e.What)}");
                    sb.AppendLine();
                }
                sb.AppendLine($"**{(p.ActivityTimeline.Count > 0 ? 4 : 3)}. Recommendation — don't lodge**");
                sb.AppendLine();
                sb.AppendLine($"Nothing goes to {p.Insurer}: the **{Fmt(run.AmountAtStake)}** claimed is held back, and the claim is referred to Financial Crime (Felix). " +
                    $"Contact {first} only on the details verified before any change — {Md(p.VerifiedContact)}. " +
                    $"This isn't an accusation: {first} may be the victim.");
                sb.AppendLine();
                sb.AppendLine($"**{(p.ActivityTimeline.Count > 0 ? 5 : 4)}. Approval Request**");
                sb.AppendLine();
                AppendApproval(sb, approval, teamsResult);
                sb.AppendLine();
                sb.AppendLine($"**{(p.ActivityTimeline.Count > 0 ? 6 : 5)}. Human Approval Required**");
                sb.AppendLine();
                sb.AppendLine($"**Yes** — {Md(string.Join(" ", run.Approval.Reasons))}");
                sb.AppendLine();
                sb.AppendLine($"**Theo, please approve the block in Microsoft Teams. I won't lodge anything with {p.Insurer}.**");
                break;
            }

            default:
            {
                sb.AppendLine("**1. Checks**");
                sb.AppendLine();
                AppendChecks(sb, run.Claimant.Checks.Concat(run.Policy.Checks));
                sb.AppendLine("**2. What's Needed Before Lodging**");
                sb.AppendLine();
                foreach (var reason in run.Approval.Reasons)
                    sb.AppendLine($"- {Md(reason)}");
                sb.AppendLine();
                sb.AppendLine("**3. Human Approval Required**");
                sb.AppendLine();
                sb.AppendLine($"Not yet — no approval is raised and nothing goes to {p.Insurer}. **Amelia, please confirm who is entitled to claim, and the cover, with the client — then Ben's agent runs the checks again.**");
                break;
            }
        }

        return sb.ToString();
    }

    private static void AppendChecks(StringBuilder sb, IEnumerable<ClaimCheckLine> checks)
    {
        foreach (var c in checks)
            sb.AppendLine($"- {Icon(c.Status)} **{Md(c.Label)}** ({Md(c.Value)}) — {Md(c.Detail)}");
        sb.AppendLine();
    }

    private static void AppendClaimPack(StringBuilder sb, IEnumerable<ClaimPackItem> items)
    {
        sb.AppendLine("| Document | Status | Note |");
        sb.AppendLine("|---|---|---|");
        foreach (var i in items)
            sb.AppendLine($"| {Md(i.Document)} | {PackStatus(i.Status)} | {Md(i.Note)} |");
        sb.AppendLine();
        sb.AppendLine($"_Checklist: {ClaimRules.InGoodOrderTitle}._");
        sb.AppendLine();
    }

    private static void AppendBenefit(StringBuilder sb, ClaimRun run)
    {
        var b = run.Benefit;
        sb.AppendLine("| Working | Amount |");
        sb.AppendLine("|---|---:|");
        foreach (var s in b.Steps.Where(s => s.Kind != "note"))
        {
            sb.AppendLine(s.Kind == "total"
                ? $"| **{Md(s.Label)}** | **{Md(s.Display)}** |"
                : $"| {Md(s.Label)} | {Md(s.Display)} |");
        }
        sb.AppendLine();
        foreach (var s in b.Steps.Where(s => s.Kind == "note"))
            sb.AppendLine($"- **{Md(s.Label)}:** {Md(s.Display)}");
        sb.AppendLine($"- **Paid to:** {Md(b.PaidTo)}");
        foreach (var note in b.Notes)
            sb.AppendLine($"- {Md(note)}");
        if (b.ClaimType == ClaimCoverTypes.IncomeProtection)
            sb.AppendLine($"- **Basis:** {Md(ClaimRules.IncomeBenefitBasis(run.Profile))}");
        sb.AppendLine($"- **Tax:** {Md(b.TaxTreatment)}");
        sb.AppendLine();
        sb.AppendLine($"_Rules: {b.RuleCited} · {b.TaxRuleCited}._");
        sb.AppendLine();
    }

    private static void AppendNotes(StringBuilder sb, ClaimRun run)
    {
        var p = run.Profile;
        var any = false;
        foreach (var note in p.Notes)
        {
            if (note.Tone == "adviser" && run.ClaimType == ClaimCoverTypes.IncomeProtection && !run.Benefit.LimitedByEarnings)
                continue;
            sb.AppendLine($"- **{Md(note.Title)}:** {Md(note.Text)}");
            any = true;
        }
        if (p.Vulnerable && !string.IsNullOrWhiteSpace(p.VulnerabilityNote))
        {
            sb.AppendLine($"- **A person first:** {Md(p.VulnerabilityNote)}");
            any = true;
        }
        if (!string.IsNullOrWhiteSpace(p.FinancialCrimeNote))
        {
            sb.AppendLine($"- **Financial Crime:** {Md(p.FinancialCrimeNote)}");
            any = true;
        }
        if (!any)
            sb.AppendLine("- None.");
        sb.AppendLine();
    }

    private static void AppendApproval(StringBuilder sb, ClaimApprovalRecord? approval, TeamsCardSendResult? teamsResult)
    {
        if (approval is null)
        {
            sb.AppendLine("- No approval raised.");
            return;
        }
        sb.AppendLine(approval.IsBlockDecision
            ? $"- Block decision **{approval.ApprovalId}** — {approval.StatusKey}, with {approval.Approver}"
            : $"- Approval id **{approval.ApprovalId}** — {approval.StatusKey}, with {approval.Approver}");
        sb.AppendLine(teamsResult is { Sent: true }
            ? $"- Microsoft Teams: \"{(approval.IsBlockDecision ? TeamsMcpTools.BlockCardTitle : TeamsMcpTools.LodgementCardTitle)}\" card posted to {teamsResult.Channel} via teams_sendApprovalCard"
            : "- Microsoft Teams: card not sent — use the approval link");
        sb.AppendLine($"- Approval link: {approval.ApprovalUrl}");
    }

    // ── Claim update drafts (Markdown; the reference is filled on lodgement) ─

    private static string BuildClaimUpdate(ClaimRun run) => run.Flow switch
    {
        ClaimFlows.Refer => ReferredNote(run),
        ClaimFlows.Hold => HoldNote(run),
        _ => run.ClaimType switch
        {
            ClaimCoverTypes.IncomeProtection => IncomeProtectionLetter(run),
            ClaimCoverTypes.Death => DeathLetter(run),
            _ => LumpSumLetter(run)
        }
    };

    private static string IncomeProtectionLetter(ClaimRun run)
    {
        var c = run.Case;
        var p = run.Profile;
        var b = run.Benefit;
        var first = ClaimRules.FirstName(run.Claimant.ClaimantName);
        var support = SupportPersonName(p.SupportPerson);
        var sb = new StringBuilder();
        sb.AppendLine($"**To:** {run.Claimant.ClaimantName}" + (support is null ? string.Empty : $" · **cc:** {support} (support person, with {ClaimRules.Possessive(first)} written authority)") +
            $" · **Re:** Your income protection claim — case {c.CaseNumber}");
        sb.AppendLine();
        sb.AppendLine($"Dear {first},");
        sb.AppendLine();
        sb.AppendLine("We're sorry you've been hurt, and we hope your recovery is going well. Here's where your claim is up to.");
        sb.AppendLine();
        sb.AppendLine($"- **Claim lodged with {p.Insurer}:** {ClaimRules.FormatDate(run.LodgementDate)} — reference {ClaimRules.ReferenceToken}");
        var expected = $"- **Your expected benefit:** **{b.BenefitDisplay}**";
        if (b.AccrualStart is { } accrual)
            expected += $", from {ClaimRules.FormatDate(accrual)} after your {p.WaitingPeriodDays}-day waiting period";
        expected += ", paid monthly in arrears" + (b.FirstPayment is null
            ? $". {p.Insurer} makes the decision."
            : $". {p.Insurer} makes the decision; once it accepts the claim, the first payment would be {b.FirstPayment}.");
        sb.AppendLine(expected);
        if (b.LimitedByEarnings && b.MonthlyEarnings is { } monthly && b.EarningsCap is { } cap)
        {
            sb.AppendLine($"- **Why not {Fmt(b.InsuredAmount)}?** Your policy pays the lesser of your insured {Fmt(b.InsuredAmount)} a month and " +
                $"{ClaimRules.FormatPercent(p.ReplacementRatePercent)} of your average monthly earnings in the 12 months before your injury. " +
                $"You earned {Fmt(monthly * 12m)} in the {p.EarningsPeriod ?? "12 months before"} — {Fmt(monthly)} a month — and " +
                $"{ClaimRules.FormatPercent(p.ReplacementRatePercent)} of that is {Fmt(cap)}.");
        }
        sb.AppendLine("- **Tax:** income protection payments replace income, so they're taxable income — your accountant can confirm how this applies to you.");
        sb.AppendLine($"- **What happens next:** under the Life Insurance Code of Practice, {p.Insurer} will contact you within 10 business days, " +
            "update you at least every 20 business days, and decide within 2 months.");
        sb.AppendLine();
        sb.AppendLine("If you have any questions, Amelia or I are happy to help" +
            (support is null ? "." : $", and {ClaimRules.FirstName(support)} can keep helping you as your support person."));
        sb.AppendLine($"If you're unhappy with how your claim is handled, you can complain to {p.Insurer}, and then to the Australian Financial Complaints Authority (AFCA).");
        sb.AppendLine();
        AppendSignOff(sb, "Kind regards,", p.Vulnerable && support is not null
            ? $"_Draft — Amelia calls {first} personally first; then Cara sends this, once the claim is lodged._"
            : "_Draft — Cara sends this once the claim is lodged._");
        return sb.ToString();
    }

    private static string DeathLetter(ClaimRun run)
    {
        var c = run.Case;
        var p = run.Profile;
        var b = run.Benefit;
        var first = ClaimRules.FirstName(run.Claimant.ClaimantName);
        var deceased = ClaimRules.FirstName(p.LifeInsured);
        var named = !deceased.StartsWith("the ", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine($"**To:** {run.Claimant.ClaimantName} · **Re:** {(named ? ClaimRules.Possessive(deceased) : "The")} life insurance — case {c.CaseNumber}");
        sb.AppendLine();
        sb.AppendLine($"Dear {first},");
        sb.AppendLine();
        sb.AppendLine(named
            ? $"We're so sorry for the loss of {deceased}. We'll take care of the claim on {ClaimRules.Possessive(deceased)} {p.Insurer} policy, so there's as little as possible for you to do."
            : $"We're so sorry for your loss. We'll take care of the claim with {p.Insurer}, so there's as little as possible for you to do.");
        sb.AppendLine();
        sb.AppendLine($"- **Claim lodged with {p.Insurer}:** {ClaimRules.FormatDate(run.LodgementDate)} — reference {ClaimRules.ReferenceToken}");
        sb.AppendLine($"- **The benefit:** the life cover is {Fmt(b.Benefit)} (policy {p.PolicyNumber}). {p.Insurer} makes the decision on the claim.");
        sb.AppendLine(named
            ? $"- **Paid to you directly:** {deceased} nominated you as the beneficiary on the policy, so once the claim is accepted the benefit is paid straight to you — not through {ClaimRules.Possessive(deceased)} estate — into your account ({Md(run.Claimant.MaskedAccount)})."
            : $"- **Paid to you directly:** once the claim is accepted, into your account ({Md(run.Claimant.MaskedAccount)}).");
        sb.AppendLine("- **Tax:** life insurance paid outside super like this is generally not taxable.");
        if (p.ProfileId == "death-claim-robert")
        {
            sb.AppendLine("- **Mei's super:** Mei's Riverbend Super account also had $180,000 of death cover. That's a separate claim with the fund, " +
                "and we've put together a checklist to help you with it.");
        }
        sb.AppendLine($"- **What happens next:** {p.Insurer} will contact you within 10 business days and keep you updated at least every 20 business days. " +
            "Under the Life Insurance Code of Practice, it decides within 6 months.");
        sb.AppendLine();
        sb.AppendLine(p.ProfileId == "death-claim-robert"
            ? "Please take all the time you need. Amelia is your one contact at Caldova, and she's here whenever you're ready — about anything at all."
            : "Please take all the time you need. If you'd rather talk, just call us.");
        sb.AppendLine($"If you're ever unhappy with how the claim is handled, you can complain to {p.Insurer}, and then to the Australian Financial Complaints Authority (AFCA).");
        sb.AppendLine();
        AppendSignOff(sb, "With our deepest sympathy,", "_Draft — Theo approves every message to the family before Cara sends it._");
        return sb.ToString();
    }

    private static string LumpSumLetter(ClaimRun run)
    {
        var c = run.Case;
        var p = run.Profile;
        var b = run.Benefit;
        var first = ClaimRules.FirstName(run.Claimant.ClaimantName);
        var sb = new StringBuilder();
        sb.AppendLine($"**To:** {run.Claimant.ClaimantName} · **Re:** Your {ClaimCoverTypes.ClaimLabel(run.ClaimType).ToLowerInvariant()} — case {c.CaseNumber}");
        sb.AppendLine();
        sb.AppendLine($"Dear {first},");
        sb.AppendLine();
        sb.AppendLine("We're sorry you're going through this. Here's where your claim is up to.");
        sb.AppendLine();
        sb.AppendLine($"- **Claim lodged with {p.Insurer}:** {ClaimRules.FormatDate(run.LodgementDate)} — reference {ClaimRules.ReferenceToken}");
        sb.AppendLine($"- **Expected benefit:** {b.BenefitDisplay}, paid into your account ({Md(run.Claimant.MaskedAccount)}) once {p.Insurer} accepts the claim. {p.Insurer} makes the decision.");
        sb.AppendLine($"- **Tax:** {Md(b.TaxTreatment)}");
        sb.AppendLine($"- **What happens next:** {p.Insurer} will contact you within 10 business days and keep you updated at least every 20 business days. " +
            "Under the Life Insurance Code of Practice, it decides within 6 months.");
        sb.AppendLine();
        sb.AppendLine($"If you're unhappy with how your claim is handled, you can complain to {p.Insurer}, and then to the Australian Financial Complaints Authority (AFCA).");
        sb.AppendLine();
        AppendSignOff(sb, "Kind regards,", "_Draft — a person approves this before Cara sends it to a seriously ill client._");
        return sb.ToString();
    }

    private static string ReferredNote(ClaimRun run)
    {
        var c = run.Case;
        var p = run.Profile;
        var sb = new StringBuilder();
        sb.AppendLine($"**Internal note — no claim lodged** · case {c.CaseNumber} · not for sending to the requester");
        sb.AppendLine();
        sb.AppendLine($"- **Claim received:** {ClaimCoverTypes.ClaimLabel(run.ClaimType).ToLowerInvariant()} for {Fmt(run.AmountAtStake)}, " +
            $"asking for payment to \"{Md(run.Claimant.AccountName)}\" ({Md(run.Claimant.MaskedAccount)}).");
        sb.AppendLine($"- **Outcome:** not lodged with {p.Insurer} — the claimant checks failed, and the claim is referred to Financial Crime. The block is Theo's decision.");
        sb.AppendLine($"- **Held back:** {Fmt(run.AmountAtStake)}." + (string.IsNullOrWhiteSpace(p.ProtectedNote) ? string.Empty : $" {p.ProtectedNote}"));
        sb.AppendLine($"- **Contact:** only on the details verified before any change — {Md(p.VerifiedContact)}. Never reply to the details on the claim.");
        if (!string.IsNullOrWhiteSpace(p.ReferralNextStep))
            sb.AppendLine($"- **Next step:** {Md(p.ReferralNextStep)}");
        sb.AppendLine();
        sb.AppendLine("Ben — Benefit Claims Advocate, Caldova Life Advice");
        return sb.ToString();
    }

    private static string HoldNote(ClaimRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Internal note — not ready to lodge** · case {run.Case.CaseNumber}");
        sb.AppendLine();
        foreach (var reason in run.Approval.Reasons)
            sb.AppendLine($"- **Why:** {Md(reason)}");
        sb.AppendLine($"- **Next step:** Ben confirms the details with Amelia and the client. Nothing goes to {run.Profile.Insurer} until then.");
        sb.AppendLine();
        sb.AppendLine("Ben — Benefit Claims Advocate, Caldova Life Advice");
        return sb.ToString();
    }

    private static void AppendSignOff(StringBuilder sb, string closing, string? footer)
    {
        sb.AppendLine(closing);
        sb.AppendLine("Ben — Benefit Claims Advocate, Caldova Life Advice");
        if (footer is not null)
        {
            sb.AppendLine();
            sb.AppendLine(footer);
        }
    }

    // ── Small helpers ───────────────────────────────────────────────────

    private static string Fmt(decimal amount) => ClaimRules.FormatMoney(amount);

    private static string Icon(string status) => status switch
    {
        "pass" => "✅",
        "fail" => "❌",
        "warn" => "⚠️",
        _ => "➖"
    };

    private static string PackStatus(string status) => status switch
    {
        "verified" => "✅ Verified",
        "received" => "✅ Received",
        "prepared" => "📝 Prepared",
        "later" => "🕒 Later",
        "to-come" => "⏳ To come",
        "missing" => "❌ Missing",
        "referred" => "🛡️ Referred",
        _ => status
    };

    /// <summary>"Kylie Bradley (his wife) — support person …" → "Kylie Bradley".</summary>
    private static string? SupportPersonName(string? supportPerson)
    {
        if (string.IsNullOrWhiteSpace(supportPerson)) return null;
        var s = supportPerson.Trim();
        var cut = s.IndexOfAny(['(', '—']);
        return (cut > 0 ? s[..cut] : s).Trim();
    }

    /// <summary>Escape Markdown control characters in masked bank details and free text.</summary>
    private static string Md(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("|", "\\|");

    private static string Shorten(string value, int max) =>
        value.Length > max ? value[..(max - 1)] + "…" : value;

    private static string Sanitize(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
