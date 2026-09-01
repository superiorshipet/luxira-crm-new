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
            Name = request.Name.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            MainWarehouseId = request.MainWarehouseId,
            IsShown = request.IsShown,
            ImageUrl = request.ImageUrl,
            ImageUrl2 = request.ImageUrl2,
            InvoiceImage = request.InvoiceImage
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
            p.Country,
            p.Price,
            p.MinimumSellingPrice,
            p.MaximumSellingPrice,
            p.DeliveryPrice,
            p.Quantity,
            p.SaleType,
            p.ImageUrl,
            p.ManufacturingCompanyId,
            p.ManufacturingCompany?.Name,
            p.IsDeleted
        )).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Product name is required.");
        }

        if (request.Country <= 0)
            throw new BadRequestException("Country is required.");

        if (request.ManufacturingCompanyId <= 0 ||
            !await _repository.CompanyExistsAsync(request.ManufacturingCompanyId, ct))
            throw new BadRequestException("Manufacturing company was not found.");

        var minimumPrice = request.MinimumSellingPrice;
        var maximumPrice = request.MaximumSellingPrice > 0
            ? request.MaximumSellingPrice
            : minimumPrice;
        if (minimumPrice < 0 || maximumPrice < minimumPrice)
            throw new BadRequestException("Maximum selling price must be greater than or equal to minimum selling price.");

        var saleType = string.IsNullOrWhiteSpace(request.SaleType)
            ? "بيع فردي"
            : request.SaleType.Trim();
        var normalizedName = request.Name.Trim();
        if (await _repository.ActiveProductExistsAsync(
                normalizedName,
                request.Country,
                request.ManufacturingCompanyId,
                saleType,
                ct))
            throw new BadRequestException("The same product already exists for this country, store, and sale type.");

        var product = new MainProduct
        {
            Name = normalizedName,
            Country = request.Country,
            Price = minimumPrice,
            MinimumSellingPrice = minimumPrice,
            MaximumSellingPrice = maximumPrice,
            DeliveryPrice = Math.Max(0, request.DeliveryPrice),
            Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
            SaleType = saleType,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            ImageUrl = request.ImageUrl,
            IsDeleted = false
        };

        var created = await _repository.AddProductAsync(product, ct);
        return new ProductDto(
            created.Id,
            created.Name,
            created.Country,
            created.Price,
            created.MinimumSellingPrice,
            created.MaximumSellingPrice,
            created.DeliveryPrice,
            created.Quantity,
            created.SaleType,
            created.ImageUrl,
            created.ManufacturingCompanyId,
            null,
            created.IsDeleted
        );
    }

    public async Task<List<ProductMinimumPriceDto>> GetMinimumPricesAsync(int? productId = null, int? countryId = null, CancellationToken ct = default)
    {
        var list = await _repository.GetMinimumPricesAsync(productId, countryId, ct);
        return list.Select(m => new ProductMinimumPriceDto(
            m.Id,
            m.Country,
            m.ManufacturingCompanyId,
            m.MainWarehouseId,
            m.MinimumSellingPrice,
            m.CreatedAt,
            m.UpdatedAt
        )).ToList();
    }

    public async Task SetMinimumPriceAsync(SetProductMinimumPriceRequest request, CancellationToken ct = default)
    {
        var minPrice = new ProductMinimumSellingPrice
        {
            Country = request.Country,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            MainWarehouseId = request.MainWarehouseId,
            MinimumSellingPrice = request.MinimumSellingPrice,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.SetMinimumPriceAsync(minPrice, ct);
    }

    private static ManufacturingCompanyDto MapToDto(ManufacturingCompany m) => new(
        m.Id,
        m.Name,
        m.ImageUrl,
        m.IsShown,
        m.InvoiceImage,
        m.ImageUrl2,
        m.PhoneNumber,
        m.MainWarehouseId,
        m.IsPasswordEmailStore
    );
}
