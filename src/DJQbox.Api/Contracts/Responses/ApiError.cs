namespace DJQbox.Api.Contracts.Responses;

public record ApiErrorBody(string Code, string Message, object? Detail = null);
public record ApiErrorResponse(ApiErrorBody Error);

public static class ApiError
{
    public static IResult VenueNotFound =>
        Results.NotFound(new ApiErrorResponse(new("VENUE_NOT_FOUND", "Venue not found.")));

    public static IResult DuplicateRecommendation =>
        Results.Conflict(new ApiErrorResponse(new("DUPLICATE_RECOMMENDATION", "This track is already pending or in the queue.")));

    public static IResult TrackNotFound =>
        Results.NotFound(new ApiErrorResponse(new("TRACK_NOT_FOUND", "The requested track was not found.")));

    public static IResult QueueItemNotFound =>
        Results.NotFound(new ApiErrorResponse(new("QUEUE_ITEM_NOT_FOUND", "Queue item not found.")));

    public static IResult RecommendationNotFound =>
        Results.NotFound(new ApiErrorResponse(new("RECOMMENDATION_NOT_FOUND", "Recommendation not found.")));

    public static IResult InvalidRecommendationState(string msg) =>
        Results.BadRequest(new ApiErrorResponse(new("INVALID_RECOMMENDATION_STATE", msg)));

    public static IResult ValidationFailed(string message, object? detail = null) =>
        Results.BadRequest(new ApiErrorResponse(new("VALIDATION_ERROR", message, detail)));

    public static IResult Unauthorized =>
        Results.Json(new ApiErrorResponse(new("UNAUTHORIZED", "Token missing or invalid.")), statusCode: 401);

    public static IResult Forbidden =>
        Results.Json(new ApiErrorResponse(new("FORBIDDEN", "Token valid but insufficient scope.")), statusCode: 403);
}