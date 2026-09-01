using System.Security.Claims;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Orders.Services;

public sealed record OrderStatusActor(
    string UserId,
    IReadOnlySet<string> Roles,
    bool IsTrustedSystem = false)
{
    public static OrderStatusActor FromPrincipal(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue("sub") ??
            throw new UnauthorizedException("Authenticated user identifier is missing.");
        var roles = principal.Claims
            .Where(claim => claim.Type is ClaimTypes.Role or "role")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new OrderStatusActor(userId, roles);
    }

    public static OrderStatusActor TrustedSystem(string actorId) =>
        new(actorId, new HashSet<string>(StringComparer.OrdinalIgnoreCase), true);
}

public sealed class OrderStatusTransitionPolicy
{
    private static readonly Dictionary<int, string[]> AllowedTargetRoles =
        new Dictionary<int, string[]>
        {
            [OrderStatusCodes.Prepared] = ["Admin", "Administrator", "DeliveryCompany", "DeliveryRepresentative", "ExecutiveDirector", "OrderPreparer", "WareHouse", "FollowUpDepartment"],
            [OrderStatusCodes.InDelivery] = ["Admin", "Administrator", "DeliveryCompany", "DeliveryRepresentative", "ExecutiveDirector", "WareHouse", "FollowUpDepartment"],
            [OrderStatusCodes.Delivered] = ["Admin", "Administrator", "DeliveryCompany", "DeliveryRepresentative", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDelivery] = ["Admin", "Administrator", "DeliveryCompany", "DeliveryRepresentative", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.Processed] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment", "CallCenter"],
            [OrderStatusCodes.ReferenceArchive] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.Postponed] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment", "CallCenter"],
            [OrderStatusCodes.Cancelled] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment", "CallCenter"],
            [OrderStatusCodes.WaitingForProcessing] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.TemporarilyDelivered] = ["FollowUpDepartment", "CallCenter"],
            [OrderStatusCodes.Suspended] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.Returned] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.DeliveryOrRepresentativeError] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.BalanceUpdated] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.Paid] = ["Admin", "Administrator"],
            [OrderStatusCodes.FailedDeliveryStage2] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDeliveryStage3] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDeliveryStage4] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDeliveryStage5] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDeliveryStage6] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
            [OrderStatusCodes.FailedDeliveryStage7] = ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"],
        };

    public void EnsureAllowed(
        Order order,
        int targetStatus,
        string? reason,
        OrderStatusActor actor)
    {
        if (!OrderStatusCodes.IsDefined(targetStatus))
        {
            throw new BadRequestException(
                $"Order status '{targetStatus}' is not part of the legacy status contract.");
        }

        if (OrderStatusCodes.FailureStatuses.Contains(targetStatus) &&
            string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestException("A failure reason is required for failure statuses.");
        }

        if (actor.IsTrustedSystem)
        {
            return;
        }

        if (order.CamexTrackingNumber.HasValue)
        {
            throw new ForbidException(
                "Orders owned by an automated courier can only be updated by its authenticated webhook workflow.");
        }

        var allowedRoles = AllowedTargetRoles.TryGetValue(
            targetStatus,
            out var configuredRoles)
                ? configuredRoles
                : ["Admin", "Administrator", "ExecutiveDirector", "FollowUpDepartment"];
        if (!allowedRoles.Any(actor.Roles.Contains))
        {
            throw new ForbidException("The current role cannot apply the requested order status.");
        }

        var isDeliveryActor = actor.Roles.Contains("DeliveryCompany") ||
            actor.Roles.Contains("DeliveryRepresentative");
        if (!isDeliveryActor)
        {
            return;
        }

        if (!string.Equals(
                order.DeliveryCompany?.UserId,
                actor.UserId,
                StringComparison.Ordinal))
        {
            throw new ForbidException("The order is not assigned to the current delivery account.");
        }

        var allowed =
            (order.OrderStatus == OrderStatusCodes.New &&
             targetStatus == OrderStatusCodes.Prepared) ||
            (order.OrderStatus == OrderStatusCodes.Prepared &&
             targetStatus == OrderStatusCodes.InDelivery) ||
            (order.OrderStatus == OrderStatusCodes.InDelivery &&
             targetStatus is OrderStatusCodes.Delivered or
                 OrderStatusCodes.FailedDelivery);

        if (!allowed)
        {
            throw new ForbidException(
                "Delivery users may only advance the order to the next delivery stage.");
        }
    }
}
