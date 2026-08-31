namespace Luxira.Infrastructure.DeliveryCompanies;

public interface IDeliveryCompanyReader
{
    Task<IReadOnlyList<DeliveryCompanyListItem>> ListCompaniesAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryCompanyListItem>> ListRepresentativesAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken);

    Task<int?> GetAssignedCompanyIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryOptionListItem>> ListCompaniesAndRepresentativesAsync(
        int? countryId,
        string? cityId,
        int? restrictToCompanyId,
        CancellationToken cancellationToken);
}

public sealed record DeliveryCompanyListItem(
    int Id,
    string Name,
    string? LogoUrl);

public sealed record DeliveryOptionListItem(
    int Id,
    string Name,
    string? LogoUrl,
    bool IsRepresentative);

public sealed class ReadInfrastructureUnavailableException(string message)
    : InvalidOperationException(message);
