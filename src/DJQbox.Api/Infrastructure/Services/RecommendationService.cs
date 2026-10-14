using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Hubs;
using DJQbox.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Services;

public enum SubmitStatus
{
    Success,
    Duplicate,
    VenueNotFound,
    TrackNotFound
}

public class RecommendationService(
    DjqboxDbContext db,
    QueueService queueService,
    IHubContext<VenueHub> hub,
    ILogger<RecommendationService> logger)
{
    public async Task<(SubmitStatus Status, Recommendation? Recommendation)> SubmitAsync(
        Guid venueId, string sourceAdapter, string sourceId, Guid submittedBy, CancellationToken ct = default)
    {
        var venue = await db.Venues.FindAsync([venueId], ct);
        if (venue == null) return (SubmitStatus.VenueNotFound, null);

        var track = await db.Tracks.FirstOrDefaultAsync(t => t.SourceAdapter == sourceAdapter && t.SourceId == sourceId, ct);
        if (track == null) return (SubmitStatus.TrackNotFound, null);

        // Check duplicate in pending recommendations
        var isDuplicatePending = await db.Recommendations.AnyAsync(r =>
            r.VenueId == venueId &&
            r.SourceAdapter == sourceAdapter &&
            r.SourceId == sourceId &&
            r.Status == RecommendationStatus.Pending, ct);

        if (isDuplicatePending) return (SubmitStatus.Duplicate, null);

        // Check duplicate in pending/playing queue items
        var queue = await db.Queues.FirstOrDefaultAsync(q => q.VenueId == venueId, ct);
        if (queue != null)
        {
            var isDuplicateQueue = await db.QueueItems.AnyAsync(qi =>
                qi.QueueId == queue.Id &&
                qi.SourceAdapter == sourceAdapter &&
                qi.SourceId == sourceId &&
                (qi.Status == QueueItemStatus.Pending || qi.Status == QueueItemStatus.Playing), ct);

            if (isDuplicateQueue) return (SubmitStatus.Duplicate, null);
        }

        var rec = new Recommendation
        {
            Id = Guid.NewGuid(),
            VenueId = venueId,
            SourceAdapter = sourceAdapter,
            SourceId = sourceId,
            SubmittedBy = submittedBy,
            SubmittedAt = DateTime.UtcNow,
            Status = RecommendationStatus.Pending,
            VetoDeadline = venue.Mode == VenueMode.Autopilot
                ? DateTime.UtcNow.AddSeconds(venue.VetoWindowSeconds)
                : null
        };

        db.Recommendations.Add(rec);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("recommendation.submitted {VenueId} {RecommendationId} {SourceAdapter} {SourceId}",
            venueId, rec.Id, sourceAdapter, sourceId);

        await hub.Clients.Group($"venue:{venueId}").SendAsync("recommendation.received", new
        {
            venueId,
            recommendationId = rec.Id,
            track,
            submittedBy,
            vetoDeadline = rec.VetoDeadline
        }, ct);

        return (SubmitStatus.Success, rec);
    }

    public async Task<List<Recommendation>> GetPendingAsync(Guid venueId, CancellationToken ct = default)
    {
        return await db.Recommendations
            .Where(r => r.VenueId == venueId && r.Status == RecommendationStatus.Pending)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> AcceptAsync(Guid recommendationId, Guid adminId, CancellationToken ct = default)
    {
        var rec = await db.Recommendations.FindAsync([recommendationId], ct);
        if (rec == null || rec.Status != RecommendationStatus.Pending) return false;

        rec.Status = RecommendationStatus.Accepted;
        await db.SaveChangesAsync(ct);

        await queueService.AddItemAsync(rec.VenueId, rec.SourceAdapter, rec.SourceId, adminId, ct);

        logger.LogInformation("recommendation.promoted {RecommendationId} {Reason}", recommendationId, "accepted");
        await hub.Clients.Group($"venue:{rec.VenueId}").SendAsync("recommendation.promoted", new
        {
            venueId = rec.VenueId,
            recommendationId = rec.Id,
            timestamp = DateTime.UtcNow
        }, ct);

        return true;
    }

    public async Task<bool> RejectAsync(Guid recommendationId, Guid adminId, CancellationToken ct = default)
    {
        var rec = await db.Recommendations.FindAsync([recommendationId], ct);
        if (rec == null || rec.Status != RecommendationStatus.Pending) return false;

        rec.Status = RecommendationStatus.Rejected;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("recommendation.rejected {RecommendationId} {AdminId}", recommendationId, adminId);
        return true;
    }

    public async Task<bool> VetoAsync(Guid recommendationId, Guid adminId, string? reason, CancellationToken ct = default)
    {
        var rec = await db.Recommendations.FindAsync([recommendationId], ct);
        if (rec == null || rec.Status != RecommendationStatus.Pending) return false;

        rec.Status = RecommendationStatus.Vetoed;
        rec.VetoDeadline = null;

        var veto = new Veto
        {
            Id = Guid.NewGuid(),
            RecommendationId = rec.Id,
            CastBy = adminId,
            CastAt = DateTime.UtcNow,
            Reason = reason ?? string.Empty
        };

        db.Vetoes.Add(veto);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("recommendation.vetoed {RecommendationId} {AdminId}", recommendationId, adminId);
        return true;
    }

    public async Task PromoteExpiredAsync(Guid recommendationId, CancellationToken ct = default)
    {
        var rec = await db.Recommendations.FindAsync([recommendationId], ct);
        if (rec == null || rec.Status != RecommendationStatus.Pending) return;

        rec.Status = RecommendationStatus.Promoted;
        await db.SaveChangesAsync(ct);

        await queueService.AddItemAsync(rec.VenueId, rec.SourceAdapter, rec.SourceId, rec.SubmittedBy, ct);

        logger.LogInformation("recommendation.promoted {RecommendationId} {Reason}", recommendationId, "autopilot");
        await hub.Clients.Group($"venue:{rec.VenueId}").SendAsync("recommendation.promoted", new
        {
            venueId = rec.VenueId,
            recommendationId = rec.Id,
            timestamp = DateTime.UtcNow
        }, ct);
    }
}