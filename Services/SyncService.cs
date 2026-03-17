using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Phase 8: Offline sync log management.
/// POS terminals can queue changes offline; this service processes the sync queue.
/// </summary>
public class SyncService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public SyncService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Queue an offline change from a device.</summary>
    public async Task<Guid> QueueChangeAsync(string deviceId, string entityType, string entityId, string action, object? payload)
    {
        var log = new SyncLog
        {
            SyncLogId = Guid.NewGuid(),
            DeviceId = deviceId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Payload = payload is not null ? JsonSerializer.Serialize(payload) : null,
            QueuedAtUtc = DateTime.UtcNow,
            Status = 1 // Pending
        };

        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync();
        return log.SyncLogId;
    }

    /// <summary>Get all pending sync entries for a device.</summary>
    public async Task<List<SyncLog>> GetPendingAsync(string? deviceId = null)
    {
        var q = _db.SyncLogs.AsNoTracking().Where(s => s.Status == 1);
        if (!string.IsNullOrEmpty(deviceId))
            q = q.Where(s => s.DeviceId == deviceId);

        return await q.OrderBy(s => s.QueuedAtUtc).ToListAsync();
    }

    /// <summary>Mark a sync entry as successfully synced.</summary>
    public async Task MarkSyncedAsync(Guid syncLogId)
    {
        var log = await _db.SyncLogs.FirstOrDefaultAsync(s => s.SyncLogId == syncLogId);
        if (log is null) return;

        log.Status = 2; // Synced
        log.SyncedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>Mark a sync entry as having a conflict.</summary>
    public async Task MarkConflictAsync(Guid syncLogId, string conflictDetails)
    {
        var log = await _db.SyncLogs.FirstOrDefaultAsync(s => s.SyncLogId == syncLogId);
        if (log is null) return;

        log.Status = 3; // Conflict
        log.ConflictDetails = conflictDetails;
        await _db.SaveChangesAsync();
    }

    /// <summary>Resolve a conflict by choosing server or client version.</summary>
    public async Task ResolveConflictAsync(Guid syncLogId, string resolution)
    {
        var log = await _db.SyncLogs.FirstOrDefaultAsync(s => s.SyncLogId == syncLogId && s.Status == 3);
        if (log is null) throw new InvalidOperationException("No conflict to resolve.");

        if (resolution is not ("Server" or "Client"))
            throw new InvalidOperationException("Resolution must be 'Server' or 'Client'.");

        log.Resolution = resolution;
        log.ResolvedAtUtc = DateTime.UtcNow;
        log.Status = 2; // Mark as resolved/synced

        await _db.SaveChangesAsync();

        try
        {
            await _audit.LogAsync(
                action: "SyncConflictResolved",
                entityType: "SyncLog",
                entityId: log.SyncLogId.ToString(),
                data: new
                {
                    log.DeviceId,
                    log.EntityType,
                    log.EntityId,
                    Resolution = resolution
                });
        }
        catch { }
    }

    /// <summary>Get dashboard stats for the sync queue.</summary>
    public async Task<SyncStats> GetStatsAsync()
    {
        var logs = await _db.SyncLogs.AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return new SyncStats
        {
            Pending = logs.FirstOrDefault(x => x.Status == 1)?.Count ?? 0,
            Synced = logs.FirstOrDefault(x => x.Status == 2)?.Count ?? 0,
            Conflicts = logs.FirstOrDefault(x => x.Status == 3)?.Count ?? 0
        };
    }

    public class SyncStats
    {
        public int Pending { get; set; }
        public int Synced { get; set; }
        public int Conflicts { get; set; }
    }
}
