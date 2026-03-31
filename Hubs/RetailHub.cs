using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RetailERP.Hubs;

/// <summary>
/// Central SignalR hub for real-time updates.
/// Clients join their company group on connect; server broadcasts events to the group.
/// </summary>
[Authorize]
public class RetailHub : Hub
{
    /// <summary>Called by the client after connection to join the company-scoped group.</summary>
    public async Task JoinCompanyGroup(string companyId)
    {
        if (!string.IsNullOrWhiteSpace(companyId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"company-{companyId}");
    }

    /// <summary>Called by POS clients to join a specific store group (for store-scoped events).</summary>
    public async Task JoinStoreGroup(string storeId)
    {
        if (!string.IsNullOrWhiteSpace(storeId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"store-{storeId}");
    }

    public override async Task OnConnectedAsync()
    {
        // Auto-join company group from claims (ITenantEntity pattern)
        var companyId = Context.User?.FindFirst("CompanyId")?.Value;
        if (!string.IsNullOrEmpty(companyId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"company-{companyId}");

        await base.OnConnectedAsync();
    }
}
