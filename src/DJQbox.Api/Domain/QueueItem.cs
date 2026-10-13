namespace DJQbox.Api.Domain;

public class QueueItem
{
    public Guid Id { get; set; }
    public Guid QueueId { get; set; }
    public Queue? Queue { get; set; }
    public string SourceAdapter { get; set; } = "local_files";
    public string SourceId { get; set; } = string.Empty;
    public float Position { get; set; }
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Pending;
    public Guid? AddedBy { get; set; }
    public User? AddedByUser { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}