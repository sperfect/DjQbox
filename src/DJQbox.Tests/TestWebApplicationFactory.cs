using System.Net.Http.Headers;
using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
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

    public string AdminToken { get; private set; } = string.Empty;
    public string GuestToken { get; private set; } = string.Empty;
    public Guid VenueId => DatabaseSeeder.VenueId;

    public async Task InitializeAsync()
    {
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

        // Generate tokens using the static key configured in Program.cs
        var tokenService = new TokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "poc-signing-key-change-in-production-min-32-chars!"
        }).Build());

        AdminToken = tokenService.GenerateToken(
            DatabaseSeeder.AdminUserId, "admin", DatabaseSeeder.VenueId,
            ["queue:read", "queue:write", "queue:recommend", "venue:admin", "profile:read", "profile:write"]);

        GuestToken = tokenService.GenerateToken(
            DatabaseSeeder.GuestUserId, "guest", DatabaseSeeder.VenueId,
            ["queue:read", "queue:recommend"]);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "poc-signing-key-change-in-production-min-32-chars!"
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
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DjqboxDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<DjqboxDbContext>(options =>
                {
                    options.UseInMemoryDatabase("DjqboxTestDb-" + Guid.NewGuid());
                });
            }
        });
    }

    public async Task SeedInMemoryDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjqboxDbContext>();
        await DatabaseSeeder.SeedAsync(db);
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        return client;
    }

    public HttpClient CreateGuestClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GuestToken);
        return client;
    }

    public new async Task DisposeAsync()
    {
        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }
}