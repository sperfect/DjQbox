using DJQbox.Api.Domain;

namespace DJQbox.Api.Contracts.Responses;

public record TrackResponse(
    Guid Id,
    string SourceAdapter,
    string SourceId,
    string Title,
    string Artist,
    int DurationSeconds,
    int? Bpm,
    float? EnergyLevel,
    string[] MoodTags,
    string[] GenreTags,
    bool Explicit,
    string StreamUrl
);

public record QueueItemResponse(
    Guid Id,
    Guid QueueId,
    string SourceAdapter,
    string SourceId,
    float Position,
    string Status,
    Guid? AddedBy,
    DateTime AddedAt,
    TrackResponse? Track
);

public record QueueResponse(
    Guid Id,
    Guid VenueId,
    List<QueueItemResponse> Items
);

public record NowPlayingResponse(
    Guid? QueueItemId,
    TrackResponse? Track,
    string Status
);

public record RecommendationResponse(
    Guid Id,
    Guid VenueId,
    string SourceAdapter,
    string SourceId,
    Guid SubmittedBy,
    DateTime SubmittedAt,
    string Status,
    DateTime? VetoDeadline,
    TrackResponse? Track
);

public record VenueResponse(
    Guid Id,
    string Name,
    string Mode,
    int VetoWindowSeconds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SourceAdapterResponse(
    string Id,
    string Name,
    bool Available,
    string[] Capabilities
);

public static class MappingExtensions
{
    public static TrackResponse ToResponse(this Track t) =>
        new(t.Id, t.SourceAdapter, t.SourceId, t.Title, t.Artist, t.DurationSeconds, t.Bpm, t.EnergyLevel, t.MoodTags, t.GenreTags, t.Explicit, t.StreamUrl);

    public static QueueItemResponse ToResponse(this QueueItem i, Track? track = null) =>
        new(i.Id, i.QueueId, i.SourceAdapter, i.SourceId, i.Position, i.Status.ToString().ToLowerInvariant(), i.AddedBy, i.AddedAt, track?.ToResponse());

    public static RecommendationResponse ToResponse(this Recommendation r, Track? track = null) =>
        new(r.Id, r.VenueId, r.SourceAdapter, r.SourceId, r.SubmittedBy, r.SubmittedAt, r.Status.ToString().ToLowerInvariant(), r.VetoDeadline, track?.ToResponse());

    public static VenueResponse ToResponse(this Venue v) =>
        new(v.Id, v.Name, v.Mode.ToString().ToLowerInvariant(), v.VetoWindowSeconds, v.CreatedAt, v.UpdatedAt);
}