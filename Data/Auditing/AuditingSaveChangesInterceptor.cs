using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailERP.Data.Entities;

namespace RetailERP.Data.Auditing;

public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;

    public AuditingSaveChangesInterceptor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var utcNow = DateTime.UtcNow;
        var userId = GetUserId();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditableEntity auditable) continue;

            if (entry.State == EntityState.Added)
            {
                auditable.CreatedAtUtc = utcNow;
                auditable.UpdatedAtUtc = utcNow;

                if (entry.Entity is StockTransaction st && userId.HasValue)
                    st.ActorUserId ??= userId;

                if (userId.HasValue)
                {
                    auditable.CreatedByUserId ??= userId;
                    auditable.UpdatedByUserId ??= userId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                auditable.UpdatedAtUtc = utcNow;
                if (userId.HasValue) auditable.UpdatedByUserId = userId;

                if (entry.Entity is StockTransaction st && userId.HasValue)
                    st.ActorUserId ??= userId;

                // Prevent accidental edits to creation metadata.
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedByUserId)).IsModified = false;
            }
        }
    }

    private Guid? GetUserId()
    {
        var user = _http.HttpContext?.User;
        var idStr = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
