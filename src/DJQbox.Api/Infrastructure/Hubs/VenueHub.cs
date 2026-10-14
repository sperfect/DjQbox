using Microsoft.AspNetCore.SignalR;

namespace DJQbox.Api.Infrastructure.Hubs;

public class VenueHub : Hub
{
    public async Task JoinVenue(string venueId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"venue:{venueId}");
    }

    public async Task LeaveVenue(string venueId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"venue:{venueId}");
    }
}