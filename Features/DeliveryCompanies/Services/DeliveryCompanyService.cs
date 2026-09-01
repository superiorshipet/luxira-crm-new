using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.DeliveryCompanies.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public class DeliveryCompanyService
{
    private readonly DeliveryCompanyRepository _repository;
    private readonly ILogger<DeliveryCompanyService> _logger;

    public DeliveryCompanyService(DeliveryCompanyRepository repository, ILogger<DeliveryCompanyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<DeliveryCompanyResult> ListCompaniesAsync(int? countryId = null, CancellationToken ct = default)
    {
        var companies = await _repository.GetAllAsync(countryId, isRepresentative: false, ct);
        var records = companies.Select(MapToRecord).ToList();
        return new DeliveryCompanyResult(records, records.Count);
    }

    public async Task<DeliveryRepresentativeResult> ListRepresentativesAsync(int? countryId = null, CancellationToken ct = default)
    {
        var reps = await _repository.GetAllAsync(countryId, isRepresentative: true, ct);
        var records = reps.Select(r => new DeliveryRepresentativeRecord(
            r.Id,
            r.Name,
            r.DisplayName,
            r.PhoneNumber,
            r.Address,
            r.Country,
            r.City,
            r.IsActive
        )).ToList();
        return new DeliveryRepresentativeResult(records, records.Count);
    }

    public async Task<DeliveryOptionResult> ListOptionsAsync(int? countryId = null, CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(countryId, isRepresentative: null, ct);
        var records = all.Where(d => d.IsShown).Select(d => new DeliveryOptionRecord(
            d.Id,
            d.Name,
            d.DisplayName,
            d.Country,
            d.City,
            d.IsRepresentative
        )).ToList();
        return new DeliveryOptionResult(records, records.Count);
    }

    public async Task<DeliveryPriceResult> GetPriceAsync(int deliveryCompanyId, int countryId, string? cityId, CancellationToken ct = default)
    {
        var price = await _repository.GetSpecificDeliveryPriceAsync(deliveryCompanyId, countryId, cityId, ct);
        if (price == null)
        {
            return new DeliveryPriceResult(deliveryCompanyId, countryId, cityId, 0m, "NotFoundFallback");
        }

        return new DeliveryPriceResult(deliveryCompanyId, countryId, cityId, price.Value, "SqlServer");
    }

    public async Task<DeliveryCompanyRecord> CreateCompanyAsync(CreateDeliveryCompanyRequest request, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Delivery company name is required.");
        }

        var entity = new DeliveryCompany
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Address = request.Address,
            IdNumber = request.IdNumber,
            PhoneNumber = request.PhoneNumber,
            Country = request.Country,
            City = request.City,
            Specialty = request.Specialty,
            Website = request.Website,
            Notes = request.Notes,
            IsRepresentative = request.IsRepresentative,
            UserId = userId,
            IsActive = true,
            IsShown = true,
            CreatedDate = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(entity, ct);
        return MapToRecord(created);
    }

    public async Task SetPriceAsync(SetDeliveryPriceRequest request, CancellationToken ct = default)
    {
        if (request.Price < 0)
        {
            throw new BadRequestException("Price cannot be negative.");
        }

        var price = new DeliveryCompanyPrice
        {
            DeliveryCompanyId = request.DeliveryCompanyId,
            Country = request.Country,
            City = request.City,
            Price = request.Price
        };

        await _repository.SetPriceAsync(price, ct);
    }

    private static DeliveryCompanyRecord MapToRecord(DeliveryCompany d) => new(
        d.Id,
        d.Name,
        d.DisplayName,
        d.ImageUrl,
        d.InformationUrl,
        d.TaxRegistrationNumber,
        d.Address,
        d.IdNumber,
        d.PhoneNumber,
        d.Specialty,
        d.Website,
        d.Notes,
        d.Country,
        d.City,
        d.CreatedDate,
        d.IsActive,
        d.IsShown,
        d.IsRepresentative,
        d.AutoConvertDeliveredToBalanceUpdated
    );
}
