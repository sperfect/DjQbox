using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Persistence;

public class DjqboxDbContext(DbContextOptions<DjqboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DjqboxDbContext).Assembly);
    }
}
