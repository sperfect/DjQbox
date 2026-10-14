using DJQbox.Api.Contracts.Responses;
using DJQbox.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Endpoints;

public static class SourceEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/sources")
            .WithTags("Sources")
            .RequireAuthorization();

        group.MapGet("/", GetSources);
        group.MapGet("/local_files/search", SearchLocalFiles);
    }

    private static IResult GetSources()
    {
        var sources = new[]
        {
            new SourceAdapterResponse(
                "local_files",
                "Local Files",
                true,
                ["search", "resolve", "stream"])
        };

        return Results.Ok(sources);
    }

    private static async Task<IResult> SearchLocalFiles(
        string? q,
        DjqboxDbContext db,
        CancellationToken ct)
    {
        var query = q?.Trim().ToLowerInvariant() ?? string.Empty;

        var tracks = await db.Tracks
            .Where(t => t.SourceAdapter == "local_files" &&
                (string.IsNullOrEmpty(query) ||
                 t.Title.ToLower().Contains(query) ||
                 t.Artist.ToLower().Contains(query) ||
                 t.GenreTags.Any(g => g.ToLower().Contains(query)) ||
                 t.MoodTags.Any(m => m.ToLower().Contains(query))))
            .Take(10)
            .ToListAsync(ct);

        return Results.Ok(tracks.Select(t => t.ToResponse()).ToList());
    }
}