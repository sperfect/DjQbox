using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DJQbox.Tests.Integration;

public class QueueTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public QueueTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddItemToQueue_AsAdmin_Returns201AndItemInQueue()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateAdminClient();

        var request = new { sourceAdapter = "local_files", sourceId = "track-004" };
        var postResponse = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/queue/items", request);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync($"/api/v1/venues/{_factory.VenueId}/queue");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var queueDoc = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = queueDoc.GetProperty("items").EnumerateArray().ToList();
        items.Any(i => i.GetProperty("sourceId").GetString() == "track-004").Should().BeTrue();
    }

    [Fact]
    public async Task AddItemToQueue_AsGuest_Returns403()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateGuestClient();

        var request = new { sourceAdapter = "local_files", sourceId = "track-005" };
        var response = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/queue/items", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReorderItem_UpdatesPositionCorrectly()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateAdminClient();

        // Get initial items
        var getResponse = await client.GetAsync($"/api/v1/venues/{_factory.VenueId}/queue");
        var queueDoc = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstItem = queueDoc.GetProperty("items").EnumerateArray().First();
        var firstItemId = firstItem.GetProperty("id").GetString();

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/v1/venues/{_factory.VenueId}/queue/items/{firstItemId}/position",
            new { position = 9.5f });

        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedItem = await patchResponse.Content.ReadFromJsonAsync<JsonElement>();
        updatedItem.GetProperty("position").GetSingle().Should().Be(9.5f);
    }

    [Fact]
    public async Task AdvanceQueue_MovesToNextPendingItem()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateAdminClient();

        var advanceResponse = await client.PostAsync($"/api/v1/venues/{_factory.VenueId}/now-playing/advance", null);
        advanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getNowPlaying = await client.GetAsync($"/api/v1/venues/{_factory.VenueId}/now-playing");
        getNowPlaying.StatusCode.Should().Be(HttpStatusCode.OK);

        var np = await getNowPlaying.Content.ReadFromJsonAsync<JsonElement>();
        np.GetProperty("status").GetString().Should().Be("playing");
    }
}