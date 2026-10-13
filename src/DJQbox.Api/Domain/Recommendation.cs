namespace DJQbox.Api.Domain;

public class Recommendation
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public Venue? Venue { get; set; }
    public string SourceAdapter { get; set; } = "local_files";
    public string SourceId { get; set; } = string.Empty;
    public Guid SubmittedBy { get; set; }
    public User? SubmittedByUser { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Pending;
    public DateTime? VetoDeadline { get; set; }
    public ICollection<Veto> Vetoes { get; set; } = new List<Veto>();
}