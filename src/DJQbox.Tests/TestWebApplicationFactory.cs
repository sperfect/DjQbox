using DJQbox.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DJQbox.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        // If Docker is running, use Testcontainers; otherwise fallback to in-memory/isolated connection
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .Build();
            await _postgres.StartAsync();
        }
        catch
        {
            _postgres = null;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production-needs-to-be-32-chars-long!"
            };

            if (_postgres != null)
            {
                inMemorySettings["ConnectionStrings:Default"] = _postgres.GetConnectionString();
            }

            config.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            if (_postgres == null)
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DjqboxDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<DjqboxDbContext>(options =>
                {
                    options.UseInMemoryDatabase("DjqboxTestDb");
                });
            }
        });
    }

    public new async Task DisposeAsync()
    {
        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
