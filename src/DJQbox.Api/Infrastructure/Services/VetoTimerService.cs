using DJQbox.Api.Domain;
using DJQbox.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DJQbox.Api.Infrastructure.Services;

public class VetoTimerService(IServiceScopeFactory scopeFactory, ILogger<VetoTimerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DjqboxDbContext>();
                var recService = scope.ServiceProvider.GetRequiredService<RecommendationService>();

                var now = DateTime.UtcNow;
                var expired = await db.Recommendations
                    .Where(r => r.Status == RecommendationStatus.Pending
                             && r.VetoDeadline != null
                             && r.VetoDeadline <= now)
                    .Select(r => r.Id)
                    .ToListAsync(stoppingToken);

                foreach (var recId in expired)
                {
                    await recService.PromoteExpiredAsync(recId, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error occurred processing expired recommendations in veto timer");
            }
        }
    }
}