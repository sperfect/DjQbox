using DJQbox.Api.Contracts.Responses;
using DJQbox.Api.Infrastructure.Services;

namespace DJQbox.Api.Endpoints;

public static class NowPlayingEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/venues/{venueId}/now-playing")
            .WithTags("Now Playing")
            .RequireAuthorization();

        group.MapGet("/", GetNowPlaying);
        group.MapPost("/advance", AdvanceNowPlaying);
    }

    private static async Task<IResult> GetNowPlaying(
        Guid venueId,
        QueueService queueService,
        CancellationToken ct)
    {
        var (current, track) = await queueService.GetNowPlayingAsync(venueId, ct);
        if (current == null)
        {
            return Results.Ok(new NowPlayingResponse(null, null, "idle"));
        }

        return Results.Ok(new NowPlayingResponse(current.Id, track?.ToResponse(), "playing"));
    }

    private static async Task<IResult> AdvanceNowPlaying(
        Guid venueId,
        QueueService queueService,
        CancellationToken ct)
    {
        var (nextItem, nextTrack) = await queueService.AdvanceQueueAsync(venueId, ct);
        if (nextItem == null)
        {
            return Results.Ok(new NowPlayingResponse(null, null, "idle"));
        }

        return Results.Ok(new NowPlayingResponse(nextItem.Id, nextTrack?.ToResponse(), "playing"));
    }
}