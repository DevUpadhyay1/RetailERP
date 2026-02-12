using System.Security.Claims;
using System.Text.Json;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityType, string? entityId, object? data = null)
    {
        var user = _http.HttpContext?.User;

        Guid? actorId = null;
        var userIdStr = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdStr, out var parsed)) actorId = parsed;

        var email = user?.Identity?.Name;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorId,
            ActorEmail = email,
            DataJson = data is null ? null : JsonSerializer.Serialize(data)
        });

        await _db.SaveChangesAsync();
    }
}