using Luxira.Api.Features.ManufacturingCompanies.DTOs;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Features.ManufacturingCompanies.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.ManufacturingCompanies.Services;

public class ManufacturingCompanyService
{
    private readonly ManufacturingCompanyRepository _repository;

    public ManufacturingCompanyService(ManufacturingCompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ManufacturingCompanyDto>> GetCompaniesAsync(int? countryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        var list = await _repository.GetAllCompaniesAsync(countryId, isActive, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<ManufacturingCompanyDto> CreateCompanyAsync(CreateManufacturingCompanyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Company name is required.");
        }

        var entity = new ManufacturingCompany
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Code = request.Code,
            Country = request.Country,
            Notes = request.Notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddCompanyAsync(entity, ct);
        return MapToDto(created);
    }

    public async Task<List<ProductDto>> GetProductsAsync(int? companyId = null, CancellationToken ct = default)
    {
        var list = await _repository.GetProductsAsync(companyId, ct);
        return list.Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.SKU,
            p.DefaultPrice,
            p.DefaultCost,
            p.ManufacturingCompanyId,
            p.ManufacturingCompany?.Name,
            p.IsActive,
            p.Images.Select(i => i.ImageUrl).ToList()
        )).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Product name is required.");
        }

        var product = new MainProduct
        {
            Name = request.Name,
            SKU = request.SKU,
            DefaultPrice = request.DefaultPrice,
            DefaultCost = request.DefaultCost,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            IsActive = true
        };

        var created = await _repository.AddProductAsync(product, ct);
        return new ProductDto(
            created.Id,
            created.Name,
            created.SKU,
            created.DefaultPrice,
            created.DefaultCost,
            created.ManufacturingCompanyId,
            null,
            created.IsActive,
            new List<string>()
        );
    }

    public async Task<List<ProductMinimumPriceDto>> GetMinimumPricesAsync(int? productId = null, int? countryId = null, CancellationToken ct = default)
    {
        var list = await _repository.GetMinimumPricesAsync(productId, countryId, ct);
        var products = await _repository.GetProductsAsync(null, ct);
        var productMap = products.ToDictionary(p => p.Id, p => p.Name);

        return list.Select(m => new ProductMinimumPriceDto(
            m.Id,
            m.MainProductId,
            productMap.TryGetValue(m.MainProductId, out var name) ? name : string.Empty,
            m.Country,
            m.MinimumPrice
        )).ToList();
    }

    public async Task SetMinimumPriceAsync(SetProductMinimumPriceRequest request, CancellationToken ct = default)
    {
        var minPrice = new ProductMinimumSellingPrice
        {
            MainProductId = request.MainProductId,
            Country = request.Country,
            MinimumPrice = request.MinimumPrice
        };

        await _repository.SetMinimumPriceAsync(minPrice, ct);
    }

    private static ManufacturingCompanyDto MapToDto(ManufacturingCompany m) => new(
        m.Id,
        m.Name,
        m.DisplayName,
        m.Code,
        m.Country,
        m.Notes,
        m.IsActive
    );
}
