using DJQbox.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Persistence;

public class DjqboxDbContext(DbContextOptions<DjqboxDbContext> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Queue> Queues => Set<Queue>();
    public DbSet<QueueItem> QueueItems => Set<QueueItem>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<Veto> Vetoes => Set<Veto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DjqboxDbContext).Assembly);
    }
}
