using DJQbox.Api.Contracts.Requests;
using DJQbox.Api.Contracts.Responses;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Endpoints;

public static class QueueEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/venues/{venueId}/queue")
            .WithTags("Queue")
            .RequireAuthorization();

        group.MapGet("/", GetQueue);
        group.MapPost("/items", AddItem);
        group.MapDelete("/items/{itemId}", RemoveItem);
        group.MapPatch("/items/{itemId}/position", ReorderItem);
    }

    private static async Task<IResult> GetQueue(
        Guid venueId,
        QueueService queueService,
        CancellationToken ct)
    {
        var queue = await queueService.GetQueueWithTracksAsync(venueId, ct);
        if (queue == null) return ApiError.VenueNotFound;

        var tracksLookup = await queueService.GetTracksLookupAsync(queue.Items, ct);
        var itemResponses = queue.Items
            .OrderBy(i => i.Position)
            .Select(i =>
            {
                tracksLookup.TryGetValue($"{i.SourceAdapter}:{i.SourceId}", out var track);
                return i.ToResponse(track);
            })
            .ToList();

        return Results.Ok(new QueueResponse(queue.Id, venueId, itemResponses));
    }

    private static async Task<IResult> AddItem(
        Guid venueId,
        AddQueueItemRequest request,
        QueueService queueService,
        DjqboxDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:write"))
        {
            return ApiError.Forbidden;
        }

        var track = await db.Tracks.FirstOrDefaultAsync(t =>
            t.SourceAdapter == request.SourceAdapter && t.SourceId == request.SourceId, ct);
        if (track == null) return ApiError.TrackNotFound;

        var userId = httpContext.GetUserId();
        var item = await queueService.AddItemAsync(venueId, request.SourceAdapter, request.SourceId, userId, ct);
        if (item == null) return ApiError.VenueNotFound;

        return Results.Created($"/api/v1/venues/{venueId}/queue/items/{item.Id}", item.ToResponse(track));
    }

    private static async Task<IResult> RemoveItem(
        Guid venueId,
        Guid itemId,
        QueueService queueService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:write"))
        {
            return ApiError.Forbidden;
        }

        var success = await queueService.RemoveItemAsync(itemId, ct);
        return success ? Results.NoContent() : ApiError.QueueItemNotFound;
    }

    private static async Task<IResult> ReorderItem(
        Guid venueId,
        Guid itemId,
        ReorderQueueItemRequest request,
        QueueService queueService,
        DjqboxDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:write"))
        {
            return ApiError.Forbidden;
        }

        var item = await queueService.ReorderItemAsync(itemId, request.Position, ct);
        if (item == null) return ApiError.QueueItemNotFound;

        var track = await db.Tracks.FirstOrDefaultAsync(t =>
            t.SourceAdapter == item.SourceAdapter && t.SourceId == item.SourceId, ct);

        return Results.Ok(item.ToResponse(track));
    }
}