namespace DJQbox.Api.Domain;

public class Queue
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<QueueItem> Items { get; set; } = new List<QueueItem>();
}