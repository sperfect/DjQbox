using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Hubs;
using DJQbox.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Services;

public class QueueService(
    DjqboxDbContext db,
    IHubContext<VenueHub> hub,
    ILogger<QueueService> logger)
{
    public async Task<Queue?> GetQueueWithTracksAsync(Guid venueId, CancellationToken ct = default)
    {
        return await db.Queues
            .Include(q => q.Items.OrderBy(i => i.Position))
            .FirstOrDefaultAsync(q => q.VenueId == venueId, ct);
    }

    public async Task<Dictionary<string, Track>> GetTracksLookupAsync(IEnumerable<QueueItem> items, CancellationToken ct = default)
    {
        var sourceIds = items.Select(i => i.SourceId).Distinct().ToList();
        return await db.Tracks
            .Where(t => sourceIds.Contains(t.SourceId))
            .ToDictionaryAsync(t => $"{t.SourceAdapter}:{t.SourceId}", ct);
    }

    public async Task<QueueItem?> AddItemAsync(Guid venueId, string sourceAdapter, string sourceId, Guid? addedBy, CancellationToken ct = default)
    {
        var queue = await db.Queues
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.VenueId == venueId, ct);

        if (queue == null) return null;

        var maxPosition = queue.Items.Any() ? queue.Items.Max(i => i.Position) : 0f;
        var nextPosition = MathF.Floor(maxPosition) + 1.0f;

        var hasPlaying = queue.Items.Any(i => i.Status == QueueItemStatus.Playing);
        var initialStatus = hasPlaying ? QueueItemStatus.Pending : QueueItemStatus.Playing;

        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = queue.Id,
            SourceAdapter = sourceAdapter,
            SourceId = sourceId,
            Position = nextPosition,
            Status = initialStatus,
            AddedBy = addedBy,
            AddedAt = DateTime.UtcNow
        };

        db.QueueItems.Add(item);
        queue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("queue.item_added {VenueId} {QueueItemId} {SourceAdapter} {SourceId}", venueId, item.Id, sourceAdapter, sourceId);

        await hub.Clients.Group($"venue:{venueId}").SendAsync("queue.updated", new
        {
            venueId,
            changedItemIds = new[] { item.Id },
            timestamp = DateTime.UtcNow
        }, ct);

        if (initialStatus == QueueItemStatus.Playing)
        {
            var track = await db.Tracks.FirstOrDefaultAsync(t => t.SourceAdapter == sourceAdapter && t.SourceId == sourceId, ct);
            await hub.Clients.Group($"venue:{venueId}").SendAsync("nowplaying.changed", new
            {
                venueId,
                queueItemId = item.Id,
                track,
                timestamp = DateTime.UtcNow
            }, ct);
        }

        return item;
    }

    public async Task<bool> RemoveItemAsync(Guid queueItemId, CancellationToken ct = default)
    {
        var item = await db.QueueItems.Include(i => i.Queue).FirstOrDefaultAsync(i => i.Id == queueItemId, ct);
        if (item == null) return false;

        var venueId = item.Queue?.VenueId;
        db.QueueItems.Remove(item);
        await db.SaveChangesAsync(ct);

        if (venueId.HasValue)
        {
            logger.LogInformation("queue.item_removed {VenueId} {QueueItemId}", venueId.Value, queueItemId);
            await hub.Clients.Group($"venue:{venueId.Value}").SendAsync("queue.updated", new
            {
                venueId = venueId.Value,
                changedItemIds = new[] { queueItemId },
                timestamp = DateTime.UtcNow
            }, ct);
        }

        return true;
    }

    public async Task<QueueItem?> ReorderItemAsync(Guid queueItemId, float newPosition, CancellationToken ct = default)
    {
        var item = await db.QueueItems.Include(i => i.Queue).ThenInclude(q => q!.Items).FirstOrDefaultAsync(i => i.Id == queueItemId, ct);
        if (item == null || item.Queue == null) return null;

        item.Position = newPosition;

        // Check if any two pending items are within 0.001 distance for rebalance
        var ordered = item.Queue.Items.OrderBy(i => i.Position).ToList();
        bool needsRebalance = false;
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            if (Math.Abs(ordered[i + 1].Position - ordered[i].Position) < 0.001f)
            {
                needsRebalance = true;
                break;
            }
        }

        if (needsRebalance)
        {
            float pos = 1.0f;
            foreach (var qi in ordered)
            {
                qi.Position = pos;
                pos += 1.0f;
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("queue.reordered {VenueId} {QueueItemId} {NewPosition}", item.Queue.VenueId, queueItemId, item.Position);

        await hub.Clients.Group($"venue:{item.Queue.VenueId}").SendAsync("queue.updated", new
        {
            venueId = item.Queue.VenueId,
            changedItemIds = new[] { queueItemId },
            timestamp = DateTime.UtcNow
        }, ct);

        return item;
    }

    public async Task<(QueueItem? Current, Track? Track)> GetNowPlayingAsync(Guid venueId, CancellationToken ct = default)
    {
        var queue = await db.Queues.FirstOrDefaultAsync(q => q.VenueId == venueId, ct);
        if (queue == null) return (null, null);

        var currentItem = await db.QueueItems
            .Where(i => i.QueueId == queue.Id && i.Status == QueueItemStatus.Playing)
            .OrderBy(i => i.Position)
            .FirstOrDefaultAsync(ct);

        if (currentItem == null) return (null, null);

        var track = await db.Tracks.FirstOrDefaultAsync(t => t.SourceAdapter == currentItem.SourceAdapter && t.SourceId == currentItem.SourceId, ct);
        return (currentItem, track);
    }

    public async Task<(QueueItem? AdvancedTo, Track? Track)> AdvanceQueueAsync(Guid venueId, CancellationToken ct = default)
    {
        var queue = await db.Queues.FirstOrDefaultAsync(q => q.VenueId == venueId, ct);
        if (queue == null) return (null, null);

        var currentItem = await db.QueueItems
            .Where(i => i.QueueId == queue.Id && i.Status == QueueItemStatus.Playing)
            .FirstOrDefaultAsync(ct);

        if (currentItem != null)
        {
            currentItem.Status = QueueItemStatus.Played;
        }

        var nextItem = await db.QueueItems
            .Where(i => i.QueueId == queue.Id && i.Status == QueueItemStatus.Pending)
            .OrderBy(i => i.Position)
            .FirstOrDefaultAsync(ct);

        Track? nextTrack = null;
        if (nextItem != null)
        {
            nextItem.Status = QueueItemStatus.Playing;
            nextTrack = await db.Tracks.FirstOrDefaultAsync(t => t.SourceAdapter == nextItem.SourceAdapter && t.SourceId == nextItem.SourceId, ct);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("queue.advanced {VenueId} {PreviousItemId} {NextItemId}",
            venueId, currentItem?.Id, nextItem?.Id);

        await hub.Clients.Group($"venue:{venueId}").SendAsync("nowplaying.changed", new
        {
            venueId,
            queueItemId = nextItem?.Id,
            track = nextTrack,
            timestamp = DateTime.UtcNow
        }, ct);

        await hub.Clients.Group($"venue:{venueId}").SendAsync("queue.updated", new
        {
            venueId,
            changedItemIds = new[] { currentItem?.Id, nextItem?.Id }.Where(id => id.HasValue).Select(id => id!.Value).ToArray(),
            timestamp = DateTime.UtcNow
        }, ct);

        return (nextItem, nextTrack);
    }
}