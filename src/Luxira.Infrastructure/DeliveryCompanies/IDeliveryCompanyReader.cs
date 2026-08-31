namespace Luxira.Infrastructure.DeliveryCompanies;

public interface IDeliveryCompanyReader
{
    Task<IReadOnlyList<DeliveryCompanyListItem>> ListCompaniesAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken);
}

public sealed record DeliveryCompanyListItem(
    int Id,
    string Name,
    string? LogoUrl);

public sealed class ReadInfrastructureUnavailableException(string message)
    : InvalidOperationException(message);
