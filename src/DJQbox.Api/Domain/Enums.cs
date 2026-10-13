namespace DJQbox.Api.Domain;

public enum VenueMode
{
    Dj,
    Autopilot
}

public enum QueueItemStatus
{
    Pending,
    Playing,
    Played,
    Skipped
}

public enum RecommendationStatus
{
    Pending,
    Accepted,
    Rejected,
    Promoted,
    Vetoed,
    Expired
}

public enum UserRole
{
    Owner,
    Admin,
    Guest
}
