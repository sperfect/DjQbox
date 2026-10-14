using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DJQbox.Tests.Integration;

public class AuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/venues/{_factory.VenueId}/queue");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GuestToken_CannotAddQueueItem_Returns403()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateGuestClient();
        var request = new { sourceAdapter = "local_files", sourceId = "track-003" };

        var response = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/queue/items", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminToken_CanAddQueueItem_Returns201()
    {
        await _factory.SeedInMemoryDatabaseAsync();
        var client = _factory.CreateAdminClient();
        var request = new { sourceAdapter = "local_files", sourceId = "track-003" };

        var response = await client.PostAsJsonAsync($"/api/v1/venues/{_factory.VenueId}/queue/items", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}