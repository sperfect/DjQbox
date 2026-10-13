using DJQbox.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DJQbox.Api.Infrastructure.Persistence.Configurations;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Mode).HasConversion<string>().HasMaxLength(50);
        builder.HasOne(v => v.Queue)
            .WithOne(q => q.Venue)
            .HasForeignKey<Queue>(q => q.VenueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Owner)
            .WithMany()
            .HasForeignKey(v => v.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.CredentialRef).HasMaxLength(255);
    }
}

public class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.SourceAdapter).HasMaxLength(100).IsRequired();
        builder.Property(t => t.SourceId).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();
        builder.Property(t => t.Artist).HasMaxLength(300).IsRequired();
        builder.Property(t => t.StreamUrl).HasMaxLength(2000);
        builder.Property(t => t.MoodTags).HasColumnType("text[]");
        builder.Property(t => t.GenreTags).HasColumnType("text[]");
        builder.HasIndex(t => new { t.SourceAdapter, t.SourceId }).IsUnique();
    }
}

public class QueueConfiguration : IEntityTypeConfiguration<Queue>
{
    public void Configure(EntityTypeBuilder<Queue> builder)
    {
        builder.HasKey(q => q.Id);
        builder.HasMany(q => q.Items)
            .WithOne(i => i.Queue)
            .HasForeignKey(i => i.QueueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class QueueItemConfiguration : IEntityTypeConfiguration<QueueItem>
{
    public void Configure(EntityTypeBuilder<QueueItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.SourceAdapter).HasMaxLength(100).IsRequired();
        builder.Property(i => i.SourceId).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(i => new { i.QueueId, i.Position });
    }
}

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SourceAdapter).HasMaxLength(100).IsRequired();
        builder.Property(r => r.SourceId).HasMaxLength(500).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(r => new { r.VenueId, r.SourceAdapter, r.SourceId, r.Status });
        builder.HasMany(r => r.Vetoes)
            .WithOne(v => v.Recommendation)
            .HasForeignKey(v => v.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VetoConfiguration : IEntityTypeConfiguration<Veto>
{
    public void Configure(EntityTypeBuilder<Veto> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Reason).HasMaxLength(500);
    }
}