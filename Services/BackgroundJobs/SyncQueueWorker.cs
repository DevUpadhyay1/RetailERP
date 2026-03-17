using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Services.BackgroundJobs;

/// <summary>
/// Sprint 9: Background worker that processes pending sync queue entries.
/// Runs every 60 seconds, picks up Pending (Status = 1) SyncLog rows and marks them Synced (2).
/// </summary>
public sealed class SyncQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncQueueWorker> _log;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public SyncQueueWorker(IServiceScopeFactory scopeFactory, ILogger<SyncQueueWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Sprint 9: SyncQueueWorker started (interval: {Interval})", Interval);

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "SyncQueueWorker: error processing pending entries");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Pick up a batch of pending entries (oldest first, max 50 per cycle)
        var pending = await db.SyncLogs
            .Where(s => s.Status == 1)
            .OrderBy(s => s.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _log.LogInformation("SyncQueueWorker: processing {Count} pending sync entries", pending.Count);

        foreach (var entry in pending)
        {
            try
            {
                // In a real implementation this would push the change to
                // the target device/system. For now we mark it as synced.
                entry.Status = 2; // Synced
                entry.SyncedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SyncQueueWorker: conflict on SyncLog {Id}", entry.SyncLogId);
                entry.Status = 3; // Conflict
                entry.ConflictDetails = ex.Message;
            }
        }

        await db.SaveChangesAsync(ct);
        _log.LogInformation("SyncQueueWorker: completed batch of {Count}", pending.Count);
    }
}
