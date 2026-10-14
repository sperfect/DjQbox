using DJQbox.Api.Contracts.Requests;
using DJQbox.Api.Contracts.Responses;
using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Hubs;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Endpoints;

public static class VenueEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/venues")
            .WithTags("Venues")
            .RequireAuthorization();

        group.MapGet("/{venueId}", GetVenue);
        group.MapPatch("/{venueId}", UpdateVenue);
    }

    private static async Task<IResult> GetVenue(Guid venueId, DjqboxDbContext db, CancellationToken ct)
    {
        var venue = await db.Venues.FindAsync([venueId], ct);
        return venue == null ? ApiError.VenueNotFound : Results.Ok(venue.ToResponse());
    }

    private static async Task<IResult> UpdateVenue(
        Guid venueId,
        UpdateVenueRequest request,
        DjqboxDbContext db,
        IHubContext<VenueHub> hub,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!httpContext.HasScope("venue:admin"))
        {
            return ApiError.Forbidden;
        }

        var venue = await db.Venues.FindAsync([venueId], ct);
        if (venue == null) return ApiError.VenueNotFound;

        var logger = loggerFactory.CreateLogger("VenueEndpoints");
        var oldMode = venue.Mode;

        if (!string.IsNullOrEmpty(request.Mode))
        {
            if (Enum.TryParse<VenueMode>(request.Mode, true, out var newMode))
            {
                venue.Mode = newMode;

                // Mode switch handling for pending recommendations
                var pendingRecs = await db.Recommendations
                    .Where(r => r.VenueId == venueId && r.Status == RecommendationStatus.Pending)
                    .ToListAsync(ct);

                if (newMode == VenueMode.Autopilot)
                {
                    // DJ -> Autopilot: give pending recommendations a deadline
                    var deadline = DateTime.UtcNow.AddSeconds(venue.VetoWindowSeconds);
                    foreach (var rec in pendingRecs)
                    {
                        rec.VetoDeadline = deadline;
                    }
                }
                else
                {
                    // Autopilot -> DJ: remove veto deadline
                    foreach (var rec in pendingRecs)
                    {
                        rec.VetoDeadline = null;
                    }
                }

                logger.LogInformation("mode.changed {VenueId} {OldMode} {NewMode}", venueId, oldMode, newMode);

                await hub.Clients.Group($"venue:{venueId}").SendAsync("mode.changed", new
                {
                    venueId,
                    newMode = newMode.ToString().ToLowerInvariant(),
                    timestamp = DateTime.UtcNow
                }, ct);
            }
            else
            {
                return ApiError.ValidationFailed("Invalid venue mode. Expected 'dj' or 'autopilot'.");
            }
        }

        if (request.VetoWindowSeconds.HasValue)
        {
            venue.VetoWindowSeconds = request.VetoWindowSeconds.Value;
        }

        venue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(venue.ToResponse());
    }
}