namespace DJQbox.Api.Domain;

public class Veto
{
    public Guid Id { get; set; }
    public Guid RecommendationId { get; set; }
    public Recommendation? Recommendation { get; set; }
    public Guid CastBy { get; set; }
    public User? CastByUser { get; set; }
    public DateTime CastAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}