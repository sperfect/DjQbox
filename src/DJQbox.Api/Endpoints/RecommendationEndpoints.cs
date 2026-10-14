using DJQbox.Api.Contracts.Requests;
using DJQbox.Api.Contracts.Responses;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/venues/{venueId}/recommendations")
            .WithTags("Recommendations")
            .RequireAuthorization();

        group.MapPost("/", SubmitRecommendation);
        group.MapGet("/", GetPendingRecommendations);
        group.MapPatch("/{id}", ReviewRecommendation);
    }

    private static async Task<IResult> SubmitRecommendation(
        Guid venueId,
        SubmitRecommendationRequest request,
        RecommendationService recService,
        DjqboxDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:recommend"))
        {
            return ApiError.Forbidden;
        }

        var userId = httpContext.GetUserId() ?? DatabaseSeeder.GuestUserId;
        var (status, rec) = await recService.SubmitAsync(venueId, request.SourceAdapter, request.SourceId, userId, ct);

        return status switch
        {
            SubmitStatus.VenueNotFound => ApiError.VenueNotFound,
            SubmitStatus.TrackNotFound => ApiError.TrackNotFound,
            SubmitStatus.Duplicate => ApiError.DuplicateRecommendation,
            SubmitStatus.Success => Results.Created(
                $"/api/v1/venues/{venueId}/recommendations/{rec!.Id}",
                rec.ToResponse(await db.Tracks.FirstOrDefaultAsync(t => t.SourceAdapter == rec.SourceAdapter && t.SourceId == rec.SourceId, ct))),
            _ => Results.StatusCode(500)
        };
    }

    private static async Task<IResult> GetPendingRecommendations(
        Guid venueId,
        RecommendationService recService,
        DjqboxDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:write"))
        {
            return ApiError.Forbidden;
        }

        var recs = await recService.GetPendingAsync(venueId, ct);
        var sourceIds = recs.Select(r => r.SourceId).Distinct().ToList();
        var tracks = await db.Tracks.Where(t => sourceIds.Contains(t.SourceId)).ToDictionaryAsync(t => $"{t.SourceAdapter}:{t.SourceId}", ct);

        var response = recs.Select(r =>
        {
            tracks.TryGetValue($"{r.SourceAdapter}:{r.SourceId}", out var track);
            return r.ToResponse(track);
        }).ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> ReviewRecommendation(
        Guid venueId,
        Guid id,
        ReviewRecommendationRequest request,
        RecommendationService recService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("queue:write"))
        {
            return ApiError.Forbidden;
        }

        var adminId = httpContext.GetUserId() ?? DatabaseSeeder.AdminUserId;
        var action = request.Action?.ToLowerInvariant();

        switch (action)
        {
            case "accept":
                var acceptSuccess = await recService.AcceptAsync(id, adminId, ct);
                return acceptSuccess ? Results.Ok(new { status = "accepted" }) : ApiError.RecommendationNotFound;

            case "reject":
                var rejectSuccess = await recService.RejectAsync(id, adminId, ct);
                return rejectSuccess ? Results.Ok(new { status = "rejected" }) : ApiError.RecommendationNotFound;

            case "veto":
                var vetoSuccess = await recService.VetoAsync(id, adminId, request.Reason, ct);
                return vetoSuccess ? Results.Ok(new { status = "vetoed" }) : ApiError.RecommendationNotFound;

            default:
                return ApiError.ValidationFailed("Invalid action. Must be 'accept', 'reject', or 'veto'.");
        }
    }
}