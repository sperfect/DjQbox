using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapGet("/tokens", GetSeedTokens);
    }

    private static async Task<IResult> GetSeedTokens(
        DjqboxDbContext db,
        TokenService tokenService,
        CancellationToken ct)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(ct);
        var venueId = venue?.Id ?? DatabaseSeeder.VenueId;

        var adminToken = tokenService.GenerateToken(
            DatabaseSeeder.AdminUserId,
            "admin",
            venueId,
            ["queue:read", "queue:write", "queue:recommend", "profile:read", "profile:write", "venue:admin"]);

        var guestToken = tokenService.GenerateToken(
            DatabaseSeeder.GuestUserId,
            "guest",
            venueId,
            ["queue:read", "queue:recommend"]);

        return Results.Ok(new
        {
            venue_id = venueId,
            admin_token = adminToken,
            guest_token = guestToken
        });
    }
}