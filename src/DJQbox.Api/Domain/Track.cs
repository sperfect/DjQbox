namespace DJQbox.Api.Domain;

public class Track
{
    public Guid Id { get; set; }
    public string SourceAdapter { get; set; } = "local_files";
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public int? Bpm { get; set; }
    public float? EnergyLevel { get; set; }
    public string[] MoodTags { get; set; } = Array.Empty<string>();
    public string[] GenreTags { get; set; } = Array.Empty<string>();
    public bool Explicit { get; set; }
    public string StreamUrl { get; set; } = string.Empty;
    public DateTime MetadataFetchedAt { get; set; } = DateTime.UtcNow;
}