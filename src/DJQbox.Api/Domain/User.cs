namespace DJQbox.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public UserRole Role { get; set; } = UserRole.Guest;
    public string DisplayName { get; set; } = string.Empty;
    public string CredentialRef { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}