using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DJQbox.Api.Infrastructure.Persistence;

public class DjqboxDbContextFactory : IDesignTimeDbContextFactory<DjqboxDbContext>
{
    public DjqboxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DjqboxDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=djqbox;Username=djqbox;Password=djqbox");
        return new DjqboxDbContext(optionsBuilder.Options);
    }
}