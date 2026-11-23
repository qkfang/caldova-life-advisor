using System.Text.Json;
using CaldovaLife.App.Services;

namespace CaldovaLife.App.Api;

internal record GroupChatStartRequest(string CaseNumber);

/// <summary>
/// HTTP endpoints behind the "Group Chat" tab on <c>/agents/team-leader</c>.
/// Theo (the Team Leader Agent) acts as the group-chat manager — modelled on
/// <c>Microsoft.Agents.AI.Workflows.GroupChatManager</c> — and runs a
/// discussion with the seven other practice specialists (Olivia, Penny, Ivy,
/// Felix, Uma, Ben and Cara) about a single Client Case, deciding who speaks
/// next and when the team has a plan. When Foundry isn't configured the same
/// endpoints stream an offline replay built from the case facts, so the page
/// always completes offline.
/// </summary>
public static class TeamLeaderGroupChatApi
{
    public static void MapTeamLeaderGroupChatEndpoints(
        this WebApplication app,
        TeamLeaderGroupChatService service,
        ILogger logger)
    {
        // Static roster — used by the UI to render the participants before
        // the discussion starts.
        app.MapGet("/team-leader/groupchat/participants", () => Results.Ok(
            TeamLeaderGroupChatService.Participants.Select(p => new
            {
                id = p.Id,
                persona = p.Persona,
                role = p.Role
            })));

        // Start a new group-chat session for a case. Returns the session id
        // straight away; orchestration runs on a background task and turns
        // are streamed via the /stream endpoint below. `mode` is "live"
        // (Foundry agents) or "offline" (deterministic replay); a live
        // session that can't reach Foundry streams a `mode` event and
        // continues offline.
        app.MapPost("/team-leader/groupchat/start", (GroupChatStartRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseNumber))
                return Results.BadRequest(new { error = "caseNumber is required" });

            var clientCase = service.GetCase(request.CaseNumber);
            if (clientCase is null)
                return Results.NotFound(new { error = $"case '{request.CaseNumber}' not found" });

            logger.LogInformation("Team Leader group chat starting: caseNumber={CaseNumber}, live={Live}",
                Sanitize(clientCase.CaseNumber), service.IsConfigured);

            var session = service.StartSession(clientCase);
            return Results.Ok(new
            {
                sessionId = session.Id,
                caseNumber = session.CaseNumber,
                mode = session.Mode,
                participants = TeamLeaderGroupChatService.Participants.Select(p => new
                {
                    id = p.Id,
                    persona = p.Persona,
                    role = p.Role
                })
            });
        });

        // Server-Sent Events stream of turns for a given session. The browser
        // connects with EventSource and renders each turn as it arrives.
        app.MapGet("/team-leader/groupchat/stream/{sessionId}", async (HttpContext ctx, string sessionId) =>
        {
            var session = service.GetSession(sessionId);
            if (session is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                await ctx.Response.WriteAsJsonAsync(new { error = $"session '{sessionId}' not found" });
                return;
            }

            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            await ctx.Response.Body.FlushAsync();

            // Stream turns as they arrive on the unbounded channel. The
            // orchestrator started before the client connected, but the
            // channel buffers every event, so no turns are lost.
            try
            {
                await foreach (var turn in session.Events.Reader.ReadAllAsync(ctx.RequestAborted))
                {
                    await WriteEventAsync(ctx, turn, ctx.RequestAborted);
                    if (turn.Kind == "done") break;
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected; nothing to do.
            }
        });
    }

    private static async Task WriteEventAsync(HttpContext ctx, GroupChatTurn turn, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            kind = turn.Kind,
            speakerId = turn.SpeakerId,
            persona = turn.SpeakerPersona,
            role = turn.SpeakerRole,
            text = turn.Text,
            timestamp = turn.Timestamp
        });
        await ctx.Response.WriteAsync($"data: {payload}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');
}
