using System.Text;
using DJQbox.Api.Endpoints;
using DJQbox.Api.Infrastructure.Hubs;
using DJQbox.Api.Infrastructure.Persistence;
using DJQbox.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter()));

    // Services
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddScoped<QueueService>();
    builder.Services.AddScoped<RecommendationService>();
    builder.Services.AddHostedService<VetoTimerService>();

    // Authentication
    var signingKey = builder.Configuration["Jwt:SigningKey"] ?? "poc-signing-key-change-in-production-min-32-chars!";
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Query string token support for SignalR WebSockets
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // CORS for frontend
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetIsOriginAllowed(_ => true)
                  .AllowCredentials();
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(connectionString))
    {
        builder.Services.AddDbContext<DjqboxDbContext>(options =>
            options.UseNpgsql(connectionString));
    }

    var app = builder.Build();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    if (!string.IsNullOrEmpty(connectionString))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjqboxDbContext>();
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(db);
    }

    // Static seed token logging for easy local testing
    var tokenService = app.Services.GetRequiredService<TokenService>();
    var adminToken = tokenService.GenerateToken(
        DatabaseSeeder.AdminUserId, "admin", DatabaseSeeder.VenueId,
        ["queue:read", "queue:write", "queue:recommend", "venue:admin", "profile:read", "profile:write"]);
    var guestToken = tokenService.GenerateToken(
        DatabaseSeeder.GuestUserId, "guest", DatabaseSeeder.VenueId,
        ["queue:read", "queue:recommend"]);

    Log.Information("POC Startup Admin Token: {AdminToken}", adminToken);
    Log.Information("POC Startup Guest Token: {GuestToken}", guestToken);

    // Map endpoints
    HealthEndpoints.Map(app);
    AuthEndpoints.Map(app);
    VenueEndpoints.Map(app);
    QueueEndpoints.Map(app);
    NowPlayingEndpoints.Map(app);
    RecommendationEndpoints.Map(app);
    SourceEndpoints.Map(app);

    // Map SignalR Hub
    app.MapHub<VenueHub>("/hubs/venue");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program { }