namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;

public sealed class ListDeliveryOptionsService(
    IListDeliveryOptionsRepository repository)
{
    public async Task<IReadOnlyList<DeliveryOptionResult>> ExecuteAsync(
        int? countryId,
        string? cityId,
        int? orderId,
        bool restrictCallCenterByOrder,
        CancellationToken cancellationToken)
    {
        int? assignedCompanyId = null;
        if (restrictCallCenterByOrder && orderId.HasValue)
        {
            assignedCompanyId = await repository.GetAssignedCompanyIdForOrderAsync(
                orderId.Value,
                cancellationToken);
            if (!assignedCompanyId.HasValue)
            {
                return Array.Empty<DeliveryOptionResult>();
            }
        }

        var options = await repository.ListAsync(
            countryId,
            cityId,
            assignedCompanyId,
            cancellationToken);
        return options
            .Select(option => new DeliveryOptionResult(
                option.Id,
                option.Name,
                DeliveryMediaUrl.Resolve(option.LogoUrl),
                option.IsRepresentative))
            .ToArray();
    }
}
