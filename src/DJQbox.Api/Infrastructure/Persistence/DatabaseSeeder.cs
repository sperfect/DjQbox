using DJQbox.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static readonly Guid VenueId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid GuestUserId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    public static readonly Guid QueueId = Guid.Parse("00000000-0000-0000-0000-000000000100");

    public static async Task SeedAsync(DjqboxDbContext db, CancellationToken ct = default)
    {
        if (await db.Venues.AnyAsync(ct))
        {
            return;
        }

        var adminUser = new User
        {
            Id = AdminUserId,
            Role = UserRole.Admin,
            DisplayName = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        var guestUser = new User
        {
            Id = GuestUserId,
            Role = UserRole.Guest,
            DisplayName = "Guest",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(adminUser, guestUser);

        var venue = new Venue
        {
            Id = VenueId,
            Name = "The POC Venue",
            OwnerId = AdminUserId,
            Mode = VenueMode.Autopilot,
            VetoWindowSeconds = 30,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var queue = new Queue
        {
            Id = QueueId,
            VenueId = VenueId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Venues.Add(venue);
        db.Queues.Add(queue);

        var tracks = new List<Track>
        {
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-001",
                Title = "Midnight Coffee",
                Artist = "Luna Bay",
                DurationSeconds = 195,
                Bpm = 85,
                EnergyLevel = 0.35f,
                MoodTags = new[] { "chill", "focused" },
                GenreTags = new[] { "lo_fi", "jazz" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612081_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-002",
                Title = "Neon Skyline",
                Artist = "Solar Wave",
                DurationSeconds = 240,
                Bpm = 124,
                EnergyLevel = 0.78f,
                MoodTags = new[] { "energetic" },
                GenreTags = new[] { "electronic", "synthwave" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612082_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-003",
                Title = "Rainy Afternoon",
                Artist = "Quiet Echo",
                DurationSeconds = 210,
                Bpm = 75,
                EnergyLevel = 0.25f,
                MoodTags = new[] { "chill" },
                GenreTags = new[] { "acoustic", "ambient" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612083_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-004",
                Title = "Velvet Lounge",
                Artist = "Miles Ahead",
                DurationSeconds = 275,
                Bpm = 95,
                EnergyLevel = 0.45f,
                MoodTags = new[] { "chill", "focused" },
                GenreTags = new[] { "jazz" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612084_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-005",
                Title = "Digital Sunrise",
                Artist = "Circuit Flow",
                DurationSeconds = 220,
                Bpm = 120,
                EnergyLevel = 0.65f,
                MoodTags = new[] { "energetic", "focused" },
                GenreTags = new[] { "electronic" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612085_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-006",
                Title = "Autumn Leaves Tap",
                Artist = "Blue Trio",
                DurationSeconds = 180,
                Bpm = 90,
                EnergyLevel = 0.40f,
                MoodTags = new[] { "chill" },
                GenreTags = new[] { "jazz" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612086_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-007",
                Title = "Subway Grooves",
                Artist = "Urban Pulse",
                DurationSeconds = 205,
                Bpm = 110,
                EnergyLevel = 0.70f,
                MoodTags = new[] { "energetic" },
                GenreTags = new[] { "hip_hop", "lo_fi" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612087_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-008",
                Title = "Starlight Drift",
                Artist = "Cosmic Calm",
                DurationSeconds = 300,
                Bpm = 70,
                EnergyLevel = 0.20f,
                MoodTags = new[] { "chill", "focused" },
                GenreTags = new[] { "ambient" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612088_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-009",
                Title = "High Voltage",
                Artist = "Overdrive",
                DurationSeconds = 190,
                Bpm = 135,
                EnergyLevel = 0.90f,
                MoodTags = new[] { "energetic" },
                GenreTags = new[] { "rock", "electronic" },
                Explicit = true,
                StreamUrl = "https://cdn.freesound.org/previews/612/612089_5674468-lq.mp3"
            },
            new() {
                Id = Guid.NewGuid(),
                SourceAdapter = "local_files",
                SourceId = "track-010",
                Title = "Cozy Bookstore",
                Artist = "Warm Pages",
                DurationSeconds = 230,
                Bpm = 80,
                EnergyLevel = 0.30f,
                MoodTags = new[] { "chill", "focused" },
                GenreTags = new[] { "lo_fi", "acoustic" },
                Explicit = false,
                StreamUrl = "https://cdn.freesound.org/previews/612/612090_5674468-lq.mp3"
            }
        };

        db.Tracks.AddRange(tracks);

        var item1 = new QueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = QueueId,
            SourceAdapter = "local_files",
            SourceId = "track-001",
            Position = 1.0f,
            Status = QueueItemStatus.Playing,
            AddedBy = AdminUserId,
            AddedAt = DateTime.UtcNow
        };

        var item2 = new QueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = QueueId,
            SourceAdapter = "local_files",
            SourceId = "track-002",
            Position = 2.0f,
            Status = QueueItemStatus.Pending,
            AddedBy = AdminUserId,
            AddedAt = DateTime.UtcNow
        };

        db.QueueItems.AddRange(item1, item2);

        await db.SaveChangesAsync(ct);
    }
}