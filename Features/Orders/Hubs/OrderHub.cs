using Luxira.Api.Data;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Hubs;

[Authorize]
public class OrderHub : Hub
{
    private readonly ApplicationDbContext _context;

    public OrderHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user != null)
        {
            if (user.IsInRole("Admin") || user.IsInRole("ExecutiveDirector") || user.IsInRole("Accountant"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "UsersExpectDelivery");
                await Groups.AddToGroupAsync(Context.ConnectionId, "UrgentReportListeners");
                await Groups.AddToGroupAsync(Context.ConnectionId, "OrderPostListeners");
            }

            var userId = user.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinDeliveryCompanyGroup(int deliveryCompanyId)
    {
        if (!await CanAccessDeliveryCompanyAsync(deliveryCompanyId))
            throw new HubException("You do not have access to this delivery company.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"deliveryCompany_{deliveryCompanyId}");
    }

    public async Task LeaveDeliveryCompanyGroup(int deliveryCompanyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deliveryCompany_{deliveryCompanyId}");
    }

    private async Task<bool> CanAccessDeliveryCompanyAsync(int deliveryCompanyId)
    {
        if (deliveryCompanyId <= 0) return false;

        var user = Context.User;
        if (user is null) return false;
        if (user.IsInRole("Admin") || user.IsInRole("Administrator") ||
            user.IsInRole("ExecutiveDirector") || user.IsInRole("Accountant"))
            return true;

        var userId = user.GetUserId();
        return !string.IsNullOrWhiteSpace(userId) &&
            await _context.DeliveryCompanies.AsNoTracking().AnyAsync(
                company => company.Id == deliveryCompanyId && company.UserId == userId,
                Context.ConnectionAborted);
    }
}
