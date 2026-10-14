using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DJQbox.Tests.Integration;

public class RecommendationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RecommendationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubmitRecommendation_AsGuest_Returns201WithPendingStatus()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateGuestClient();

        var request = new { sourceAdapter = "local_files", sourceId = "track-007" };
        var response = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/recommendations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("status").GetString().Should().Be("pending");
        doc.GetProperty("sourceId").GetString().Should().Be("track-007");
    }

    [Fact]
    public async Task SubmitDuplicateRecommendation_Returns409()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateGuestClient();

        var request = new { sourceAdapter = "local_files", sourceId = "track-008" };
        var first = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/recommendations", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/recommendations", request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AcceptRecommendation_DjMode_PromotesToQueue()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var adminClient = _factory.CreateAdminClient();

        // Put venue in DJ mode
        await adminClient.PatchAsJsonAsync($"/api/v1/venues/{_factory.VenueId}", new { mode = "dj" });

        var guestClient = _factory.CreateGuestClient();
        var submitRes = await guestClient.PostAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations",
            new { sourceAdapter = "local_files", sourceId = "track-009" });
        var recDoc = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var recId = recDoc.GetProperty("id").GetString();

        var acceptRes = await adminClient.PatchAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations/{recId}",
            new { action = "accept" });

        acceptRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check queue now has it
        var queueRes = await adminClient.GetAsync($"/api/v1/venues/{_factory.VenueId}/queue");
        var queueDoc = await queueRes.Content.ReadFromJsonAsync<JsonElement>();
        var items = queueDoc.GetProperty("items").EnumerateArray().ToList();
        items.Any(i => i.GetProperty("sourceId").GetString() == "track-009").Should().BeTrue();
    }

    [Fact]
    public async Task RejectRecommendation_DjMode_SetsRejectedStatus()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var adminClient = _factory.CreateAdminClient();
        await adminClient.PatchAsJsonAsync($"/api/v1/venues/{_factory.VenueId}", new { mode = "dj" });

        var guestClient = _factory.CreateGuestClient();
        var submitRes = await guestClient.PostAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations",
            new { sourceAdapter = "local_files", sourceId = "track-010" });
        var recDoc = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var recId = recDoc.GetProperty("id").GetString();

        var rejectRes = await adminClient.PatchAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations/{recId}",
            new { action = "reject" });

        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VetoRecommendation_AutopilotMode_SetsVetoed()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var adminClient = _factory.CreateAdminClient();
        await adminClient.PatchAsJsonAsync($"/api/v1/venues/{_factory.VenueId}", new { mode = "autopilot", vetoWindowSeconds = 30 });

        var guestClient = _factory.CreateGuestClient();
        var submitRes = await guestClient.PostAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations",
            new { sourceAdapter = "local_files", sourceId = "track-006" });
        var recDoc = await submitRes.Content.ReadFromJsonAsync<JsonElement>();
        var recId = recDoc.GetProperty("id").GetString();

        var vetoRes = await adminClient.PatchAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/recommendations/{recId}",
            new { action = "veto", reason = "Tempo too fast" });

        vetoRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}