namespace DJQbox.Api.Domain;

public class Venue
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }
    public VenueMode Mode { get; set; } = VenueMode.Autopilot;
    public int VetoWindowSeconds { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Queue? Queue { get; set; }
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}