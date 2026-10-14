namespace DJQbox.Api.Contracts.Requests;

public record AddQueueItemRequest(
    string SourceAdapter,
    string SourceId
);

public record ReorderQueueItemRequest(
    float Position
);

public record UpdateVenueRequest(
    string? Mode,
    int? VetoWindowSeconds
);

public record SubmitRecommendationRequest(
    string SourceAdapter,
    string SourceId
);

public record ReviewRecommendationRequest(
    string Action, // accept, reject, veto
    string? Reason = null
);