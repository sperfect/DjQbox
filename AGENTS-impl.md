# DJQBox — Implementation Agent Instructions

Read `AGENTS.md` first. This file contains specific instructions for agents writing implementation code for DJQBox.

---

## Your Role

You are a **Senior .NET Engineer** implementing DJQBox against the spec suite in `docs/spec/`. You write production-quality, tested, idiomatic .NET 8 code. You do not modify spec documents. If the spec is ambiguous or incomplete, note it in a `// TODO: clarify spec` comment and implement the most reasonable interpretation, then flag it for the spec agent.

---

## Non-Negotiables

These rules apply to every line of code you write:

1. **Test first.** Write the failing test before writing the implementation. No exceptions. See the TDD section below.
2. **Spec is the contract.** API endpoint paths, request/response shapes, error codes, and domain behavior must match `docs/spec/04-api-contracts.md` and the relevant domain spec exactly.
3. **No secrets in code.** Connection strings, signing keys, API keys — all come from environment variables or configuration, never hardcoded.
4. **Structured logs only.** Use Serilog. Every log entry is structured. No `Console.WriteLine`. No string interpolation in log messages — use message templates.
5. **No PII in logs.** User display names, email addresses, and token values must not appear in log output. Use IDs.
6. **Explicit error codes.** Every error response uses the canonical error schema from `docs/spec/04-api-contracts.md`. Raw exception messages are never returned to clients.

---

## Test-Driven Development (TDD)

### The Cycle
For every unit of work:

```
1. Write a failing test that describes the desired behavior
2. Run the test — confirm it fails for the right reason
3. Write the minimum code to make the test pass
4. Run the test — confirm it passes
5. Refactor if needed — tests must still pass
6. Repeat
```

Never write implementation code without a test that exercises it first.

### Test Types

**Integration tests** (primary for this project):
- Use `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`).
- Use `Testcontainers.PostgreSql` — every test run gets a real Postgres instance.
- Test the full HTTP request → service → database → response cycle.
- Assert on HTTP status codes, response body shapes, and database state.
- These are the tests that prove the spec is implemented correctly.

**Unit tests** (for complex isolated logic):
- Use pure xUnit with no `WebApplicationFactory`.
- Use for: scoring algorithm (`ProfileService.Score`), veto timer logic, fractional position arithmetic, JWT validation logic.
- Mock or stub only at service boundaries (not inside domain logic).
- Avoid mocking the database — use integration tests for persistence behavior.

### Test Naming Convention
```
[MethodOrScenario]_[Context]_[ExpectedResult]

Examples:
SubmitRecommendation_AsGuest_Returns201WithPendingStatus
SubmitRecommendation_DuplicateTrack_Returns409
VetoWindowExpiry_WithNoPriorVeto_PromotesRecommendationToQueue
AcceptRecommendation_InAutopilotMode_Returns400
SetVolume_WhenCapabilityNotDeclared_ReturnsVolumeNotSupportedError
```

### Test File Organisation
```
/DJQbox.Tests
  /Integration
    QueueTests.cs           ← queue CRUD, ordering, advance
    RecommendationTests.cs  ← submission, accept, reject, veto
    AutopilotTests.cs       ← veto timer, auto-promotion, mode switch
    AuthTests.cs            ← token validation, scope enforcement
    SourceTests.cs          ← adapter search, track resolution
    NowPlayingTests.cs      ← now-playing state, advance on trackEnded
  /Unit
    FractionalPositionTests.cs
    ProfileScoringTests.cs
    TokenServiceTests.cs
    VetoTimerTests.cs
  TestWebApplicationFactory.cs
  DatabaseSeedHelper.cs
```

### Integration Test Pattern

```csharp
public class RecommendationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public RecommendationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SubmitRecommendation_AsGuest_Returns201WithPendingStatus()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GuestToken);

        var request = new { source_adapter = "local_files", source_id = "track-001" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/venues/{TestData.VenueId}/recommendations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();
        body!.Status.Should().Be("pending");
        body.Track.Should().NotBeNull();
    }
}
```

### TestWebApplicationFactory Pattern

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public string AdminToken { get; private set; } = "";
    public string GuestToken { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16").Build();
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = "test-signing-key-not-for-production"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace any environment-specific registrations here if needed
        });
    }

    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

---

## Project Conventions

### Program.cs Structure
Keep `Program.cs` lean. It should only contain:
1. Service registrations (`builder.Services.Add...`)
2. Middleware pipeline (`app.Use...`)
3. Endpoint mapping calls (`QueueEndpoints.Map(app)`)
4. App run (`app.Run()`)

All endpoint logic goes in dedicated `*Endpoints.cs` files.

### Endpoint Files Pattern

```csharp
// QueueEndpoints.cs
public static class QueueEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/venues/{venueId}/queue")
            .WithTags("Queue")
            .RequireAuthorization();

        group.MapGet("/", GetQueue);
        group.MapPost("/items", AddItem).RequireScope("queue:write");
        group.MapDelete("/items/{itemId}", RemoveItem).RequireScope("queue:write");
        group.MapPatch("/items/{itemId}/position", ReorderItem).RequireScope("queue:write");
    }

    private static async Task<IResult> GetQueue(
        Guid venueId,
        QueueService queueService,
        CancellationToken ct)
    {
        var queue = await queueService.GetQueueAsync(venueId, ct);
        return queue is null
            ? Results.NotFound(ApiError.VenueNotFound)
            : Results.Ok(queue.ToResponse());
    }

    // ... other handlers
}
```

### Response and Error Types

```csharp
// Responses/ApiError.cs
public record ApiErrorBody(string Code, string Message, object? Detail = null);
public record ApiErrorResponse(ApiErrorBody Error);

public static class ApiError
{
    public static IResult VenueNotFound =>
        Results.NotFound(new ApiErrorResponse(new("VENUE_NOT_FOUND", "Venue not found.")));

    public static IResult DuplicateRecommendation =>
        Results.Conflict(new ApiErrorResponse(new("DUPLICATE_RECOMMENDATION",
            "This track is already pending or in the queue.")));

    public static IResult Forbidden(string scope) =>
        Results.Forbid(); // 403 — add detail if useful
}
```

Error codes must match those defined in `docs/spec/04-api-contracts.md`.

### Domain Service Pattern

Services contain all business logic. Endpoints are thin (validate input → call service → return result).

```csharp
// Services/RecommendationService.cs
public class RecommendationService(
    DjqboxDbContext db,
    IHubContext<VenueHub> hub,
    ILogger<RecommendationService> logger)
{
    public async Task<SubmitResult> SubmitAsync(
        Guid venueId, string sourceAdapter, string sourceId, Guid submittedBy,
        CancellationToken ct)
    {
        // 1. Check for duplicate
        var isDuplicate = await db.Recommendations
            .AnyAsync(r => r.VenueId == venueId
                        && r.SourceAdapter == sourceAdapter
                        && r.SourceId == sourceId
                        && r.Status == RecommendationStatus.Pending, ct);

        if (isDuplicate) return SubmitResult.Duplicate;

        // 2. Load venue to check mode
        var venue = await db.Venues.FindAsync([venueId], ct);
        if (venue is null) return SubmitResult.VenueNotFound;

        // 3. Create recommendation
        var rec = new Recommendation
        {
            Id = Guid.NewGuid(),
            VenueId = venueId,
            SourceAdapter = sourceAdapter,
            SourceId = sourceId,
            SubmittedBy = submittedBy,
            SubmittedAt = DateTime.UtcNow,
            Status = RecommendationStatus.Pending,
            VetoDeadline = venue.Mode == VenueMode.Autopilot
                ? DateTime.UtcNow.AddSeconds(venue.VetoWindowSeconds)
                : null
        };

        db.Recommendations.Add(rec);
        await db.SaveChangesAsync(ct);

        // 4. Log domain event
        logger.LogInformation(
            "recommendation.submitted {VenueId} {RecommendationId} {SourceAdapter} {SourceId}",
            venueId, rec.Id, sourceAdapter, sourceId);

        // 5. Emit real-time event
        await hub.Clients.Group($"venue:{venueId}")
            .SendAsync("recommendation.received", rec.ToHubPayload(), ct);

        return SubmitResult.Ok(rec);
    }
}
```

### Entity Configuration (EF Core)

Use `IEntityTypeConfiguration<T>` classes, not fluent API in `OnModelCreating`:

```csharp
// Infrastructure/Persistence/Configurations/RecommendationConfiguration.cs
public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.SourceAdapter).HasMaxLength(100);
        builder.Property(r => r.SourceId).HasMaxLength(500);
        builder.HasIndex(r => new { r.VenueId, r.SourceAdapter, r.SourceId, r.Status });
    }
}
```

### Veto Timer (Background Service)

The veto timer runs as an `IHostedService`. It polls every 5 seconds for expired recommendations:

```csharp
public class VetoTimerService(IServiceScopeFactory scopeFactory, ILogger<VetoTimerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DjqboxDbContext>();
            var recService = scope.ServiceProvider.GetRequiredService<RecommendationService>();

            var expired = await db.Recommendations
                .Where(r => r.Status == RecommendationStatus.Pending
                         && r.VetoDeadline != null
                         && r.VetoDeadline <= DateTime.UtcNow)
                .ToListAsync(stoppingToken);

            foreach (var rec in expired)
                await recService.PromoteAsync(rec.Id, "autopilot", stoppingToken);
        }
    }
}
```

---

## Coding Standards

### Nullability
Enable nullable reference types in all projects (`<Nullable>enable</Nullable>`). No `!` null-forgiving operators without a comment explaining why.

### Records vs Classes
- **Records** for: request/response DTOs, value objects (`TrackRef`), result types.
- **Classes** for: EF Core entities, services, background services.

### Async
- All I/O operations are async. No `.Result` or `.Wait()` calls.
- Always accept and pass `CancellationToken` through the call chain.
- Use `ConfigureAwait(false)` in library code; not required in ASP.NET Core endpoint handlers.

### Dependency Injection
- Register services as `Scoped` (per-request) unless they hold no state (`Singleton`) or are cheap and stateless (`Transient`).
- `DbContext` is always `Scoped`.
- Background services (`IHostedService`) are `Singleton`; use `IServiceScopeFactory` to create scoped services inside them.

### No Magic Strings
Use constants or enums for: error codes, event names, scope names, claim types, log event names.

```csharp
public static class EventNames
{
    public const string QueueUpdated = "queue.updated";
    public const string NowPlayingChanged = "nowplaying.changed";
    public const string RecommendationReceived = "recommendation.received";
}

public static class Scopes
{
    public const string QueueWrite = "queue:write";
    public const string QueueRecommend = "queue:recommend";
    public const string ProfileWrite = "profile:write";
}
```

---

## Logging Conventions

Use Serilog message templates with named properties. Do not use string interpolation.

```csharp
// Good
logger.LogInformation("queue.advanced {VenueId} {PreviousItemId} {NextItemId}",
    venueId, previousItemId, nextItemId);

// Bad — string interpolation loses structured properties
logger.LogInformation($"Queue advanced for venue {venueId}");
```

Map spec domain event names directly to log message prefixes (see `docs/spec/11-observability-spec.md` Required Domain Events table).

---

## Git Conventions

- Branch naming: `feature/poc-task-N-short-description` (e.g. `feature/poc-task-2-domain-model`)
- Commit messages: `[Task N] Short description` (e.g. `[Task 2] Add EF Core entities and initial migration`)
- One commit per passing test suite — do not commit broken tests.
- Do not commit: `appsettings.Development.json`, `*.user`, `.env`, migration snapshots that include connection strings.

---

## Task Execution Order

Work through `docs/impl/POC-implementation-plan.md` in task order. Each task has an Acceptance section — all criteria must pass before moving to the next task.

Current task status — update this section as tasks complete:

| Task | Description | Status |
|---|---|---|
| Task 1 | Project scaffold + health endpoint | Completed |
| Task 2 | Domain model + EF Core | Completed |
| Task 3 | Auth (simplified JWT) | Completed |
| Task 4 | Queue endpoints | Completed |
| Task 5 | Recommendation endpoints + veto timer | Completed |
| Task 6 | SignalR hub + real-time events | Completed |
| Task 7 | Source endpoint (local files stub) | Completed |
| Task 8 | Dummy frontend (index.html) | Completed |
| Task 9 | Integration tests | Completed |
| Task 10 | Docker polish + README | Completed |

---

## When the Spec is Ambiguous

If you encounter behavior that the spec does not fully define:

1. Implement the most reasonable default behavior.
2. Add a `// SPEC-GAP: <description of ambiguity>` comment at the relevant code location.
3. Write a test that documents your assumption.
4. Do not modify the spec yourself — flag it for the spec agent.

Common spec gaps to watch for:
- What happens when `GET /now-playing` is called for a venue with no player registered?
- What is the maximum number of pending recommendations per venue?
- What happens to the queue if the venue player is offline when a recommendation is promoted?

---

## Definition of Done (per task)

A task is done when:
- [ ] All tests for that task pass (`dotnet test`).
- [ ] No compiler warnings.
- [ ] No `// TODO` comments without a linked spec gap or known issue.
- [ ] `docker compose up --build` still starts cleanly.
- [ ] The task's Acceptance criteria in `POC-implementation-plan.md` are all met.
