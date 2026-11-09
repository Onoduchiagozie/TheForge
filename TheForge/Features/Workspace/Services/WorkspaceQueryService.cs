using System.Text;
using TheForge.Features.Workspace.Models;
using TheForge.Services.Interfaces;

namespace TheForge.Features.Workspace.Services;

public sealed class WorkspaceQueryService(IForgeApiService api, QueryStreamParser parser, ILogger<WorkspaceQueryService> logger)
{
    public async Task ExecuteAsync(WorkspaceState state, Func<Task> notify, CancellationToken cancellationToken = default)
    {
        if (state.SubmitDisabled) return;
        var request = state.BuildRequest();
        state.ResetResponse();
        state.Status = StreamState.Streaming;
        await notify();
        try
        {
            if (!state.IsWideNet)
            {
                var result = state.Mode == WorkspaceMode.Explorer
                    ? await api.QueryExploreAsync(request, cancellationToken)
                    : await api.QueryFocusAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (result?.Response.Contains("[ERROR]", StringComparison.Ordinal) == true)
                {
                    logger.LogWarning("Oracle generation failed: {Detail}", result.Response);
                    state.ErrorMessage = "The service is temporarily busy. Please try again later.";
                    state.Status = StreamState.Error;
                    state.Stage = QueryStage.Error;
                    return;
                }
                state.ApplyResponse(result ?? throw new InvalidOperationException("The archive returned no data."));
                return;
            }

            using var response = await api.BeginNarrateStreamAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var buffer = new StringBuilder();
            await foreach (var frame in parser.ReadAsync(reader, cancellationToken))
            {
                switch (frame.Kind)
                {
                    case QueryStreamFrameKind.Text:
                        buffer.Append(frame.Text);
                        state.ResponseText = buffer.ToString();
                        break;
                    case QueryStreamFrameKind.Stage: state.Stage = frame.Stage; break;
                    case QueryStreamFrameKind.Sources: state.Sources = frame.Sources ?? []; break;
                    case QueryStreamFrameKind.Complete:
                        state.Stage = QueryStage.Complete;
                        state.CompleteTurn();
                        break;
                    case QueryStreamFrameKind.Error:
                        logger.LogWarning("Oracle stream failed: {Detail}", frame.Text);
                        state.ErrorMessage = "The service could not finish your request. Please try again later.";
                        state.Status = StreamState.Error;
                        state.Stage = QueryStage.Error;
                        break;
                }
                await notify();
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Status != StreamState.Error) state.Status = StreamState.Complete;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Status = StreamState.Cancelled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workspace query failed in {Mode}/{SubMode}", state.Mode, state.SubMode);
            state.ErrorMessage = state.IsWideNet ? "The archive stream failed before completion."
                : state.Mode == WorkspaceMode.Explorer ? "Explorer analysis failed." : "Focus query failed.";
            state.Status = StreamState.Error;
            state.Stage = QueryStage.Error;
        }
        finally
        {
            await notify();
        }
    }
}
