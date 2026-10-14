using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DJQbox.Tests.Integration;

public class AutopilotTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AutopilotTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VetoWindowExpiry_PromotesRecommendationToQueue()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var adminClient = _factory.CreateAdminClient();

        // Set veto window to 1 second
        await adminClient.PatchAsJsonAsync($"/api/v1/venues/{_factory.VenueId}", new { mode = "autopilot", vetoWindowSeconds = 1 });

        var guestClient = _factory.CreateGuestClient();
        var submitRes = await guestClient.PostAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations",
            new { sourceAdapter = "local_files", sourceId = "track-005" });
        submitRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var recDoc = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var recId = recDoc.GetProperty("id").GetString();

        // Wait 2.5 seconds for background veto timer to tick and promote
        await Task.Delay(2500);

        // Verify item is now in queue
        var queueRes = await adminClient.GetAsync($"/api/v1/venues/{_factory.VenueId}/queue");
        var queueDoc = await queueRes.Content.ReadFromJsonAsync<JsonElement>();
        var items = queueDoc.GetProperty("items").EnumerateArray().ToList();
        items.Any(i => i.GetProperty("sourceId").GetString() == "track-005").Should().BeTrue();
    }
}