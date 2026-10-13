namespace DJQbox.Api.Endpoints;

public static class HealthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", GetHealth)
            .WithTags("Health");
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }
}
