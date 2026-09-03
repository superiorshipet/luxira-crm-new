using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.S3;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/manufacturing/products-prices")]
[Route("ProductsPrices")]
public class ProductsPricesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public ProductsPricesController(ApplicationDbContext context, S3StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/ProductsPrices/Index")]
    public async Task<IActionResult> Index(
        [FromQuery] int? manufacturingCompanyId,
        [FromQuery] int? country = null,
        [FromQuery] string? productName = null,
        [FromQuery] string? saleType = null,
        [FromQuery] bool showTrash = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.MainProducts
            .AsNoTracking()
            .Where(p => p.IsDeleted == showTrash)
            .AsQueryable();

        if (manufacturingCompanyId.HasValue)
            query = query.Where(p => p.ManufacturingCompanyId == manufacturingCompanyId.Value);
        if (country.HasValue) query = query.Where(p => p.Country == country.Value);
        if (!string.IsNullOrWhiteSpace(productName))
        {
            var name = productName.Trim();
            query = query.Where(p => p.Name == name);
        }
        if (!string.IsNullOrWhiteSpace(saleType))
        {
            var normalizedSaleType = saleType.Trim();
            query = query.Where(p => p.SaleType == normalizedSaleType);
        }

        var total = await query.CountAsync(ct);
        var productRows = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var productIds = productRows.Select(product => product.Id).ToList();
        var histories = await _context.ProductPriceEditHistories.AsNoTracking()
            .Where(history => productIds.Contains(history.MainProductId))
            .OrderByDescending(history => history.EditedAt).ThenByDescending(history => history.Id)
            .ToListAsync(ct);
        var companyIds = productRows.Select(product => product.ManufacturingCompanyId).Distinct().ToList();
        var companyNames = await _context.ManufacturingCompanies.AsNoTracking().Where(company => companyIds.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, company => company.Name, ct);
        var products = productRows.Select(product => new
        {
            product.Id,
            product.Country,
            productName = product.Name,
            productImage = product.ImageUrl,
            productPrice = product.Price,
            minimumSellingPrice = product.MinimumSellingPrice > 0 ? product.MinimumSellingPrice : product.Price,
            maximumSellingPrice = product.MaximumSellingPrice > 0 ? product.MaximumSellingPrice : product.Price,
            product.DeliveryPrice,
            currencyCode = GetCurrency(product.Country),
            product.Quantity,
            product.SaleType,
            manufacturingCompanyName = companyNames.GetValueOrDefault(product.ManufacturingCompanyId, string.Empty),
            selectedManufacturingCompanyId = product.ManufacturingCompanyId,
            product.IsDeleted,
            product.DeletedAt,
            product.DeletedByUserId,
            product.DeletedByName,
            editHistories = histories.Where(history => history.MainProductId == product.Id).Take(80)
        }).ToList();

        return Ok(new
        {
            total, page, pageSize, items = products, country, manufacturingCompanyId, productName, saleType, showTrash,
            productFilterOptions = await _context.MainProducts.AsNoTracking().Where(product => product.IsDeleted == showTrash && product.Name != string.Empty)
                .GroupBy(product => new { product.Name, product.ImageUrl }).Select(group => new { group.Key.Name, group.Key.ImageUrl }).OrderBy(item => item.Name).ToListAsync(ct),
            manufacturingCompanyOptions = await _context.ManufacturingCompanies.AsNoTracking().OrderBy(company => company.Name)
                .Select(company => new { company.Id, company.Name, Logo = company.ImageUrl }).ToListAsync(ct),
            countryOptions = CountryOptions(),
            saleTypeOptions = new[] { "بيع فردي", "بيع مدمج" }
        });
    }

    [HttpGet("/ProductsPrices/Create")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        quantity = 1,
        saleType = "بيع فردي",
        productOptions = await _context.MainWarehouses.AsNoTracking().OrderBy(product => product.Name)
            .Select(product => new { product.Id, product.Name, product.ImageUrl }).ToListAsync(ct),
        manufacturingCompanies = await _context.ManufacturingCompanies.AsNoTracking().OrderBy(company => company.Name)
            .Select(company => new { company.Id, company.Name, Logo = company.ImageUrl }).ToListAsync(ct),
        countryOptions = CountryOptions()
    });

    [HttpPost("/api/v1/manufacturing/products-prices/Create")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Create([FromBody] CreateProductPriceRequest request, CancellationToken ct)
    {
        var error = await ValidateRequestAsync(request, null, ct);
        if (error is not null) throw new BadRequestException(error);

        var minimumPrice = request.MinimumSellingPrice ?? request.Price;
        var maximumPrice = request.MaximumSellingPrice ?? minimumPrice;
        var product = new MainProduct
        {
            Name = request.Name.Trim(),
            Country = request.Country,
            Price = minimumPrice,
            MinimumSellingPrice = minimumPrice,
            MaximumSellingPrice = maximumPrice,
            DeliveryPrice = Math.Max(0, request.DeliveryPrice),
            Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
            SaleType = NormalizeSaleType(request.SaleType),
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            ImageUrl = request.ImageUrl,
            IsDeleted = false
        };

        await _context.MainProducts.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(ToResponse(product));
    }

    [HttpPost("/ProductsPrices/Create")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> CreateLegacy([FromForm] LegacyProductPriceRequest request, IFormFile? productImage, CancellationToken ct)
    {
        var imageUrl = request.ProductImage;
        if (productImage is { Length: > 0 })
            imageUrl = (await _storage.UploadAsync(productImage, "products", User.GetUserId(), ct)).PublicUrl;
        var countries = ExpandCountry(request.Country);
        foreach (var targetCountry in countries)
        {
            var mapped = request.ToRequest(targetCountry, imageUrl);
            var error = await ValidateRequestAsync(mapped, null, ct);
            if (error is not null) return BadRequest(new { success = false, message = error });
        }
        var products = countries.Select(targetCountry => NewProduct(request.ToRequest(targetCountry, imageUrl))).ToList();
        _context.MainProducts.AddRange(products);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم إنشاء المنتج بنجاح.", items = products.Select(ToResponse) });
    }

    [HttpPost("/api/v1/manufacturing/products-prices/CreateBulk")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> CreateBulk([FromBody] List<CreateProductPriceRequest> items, CancellationToken ct)
    {
        if (items.Count == 0)
            throw new BadRequestException("At least one product is required.");

        if (items.Count > 500)
            throw new BadRequestException("A maximum of 500 products can be created per request.");

        var products = new List<MainProduct>(items.Count);
        foreach (var request in items)
        {
            var error = await ValidateRequestAsync(request, null, ct);
            if (error is not null) throw new BadRequestException(error);

            var minimumPrice = request.MinimumSellingPrice ?? request.Price;
            products.Add(new MainProduct
            {
                Name = request.Name.Trim(),
                Country = request.Country,
                Price = minimumPrice,
                MinimumSellingPrice = minimumPrice,
                MaximumSellingPrice = request.MaximumSellingPrice ?? minimumPrice,
                DeliveryPrice = Math.Max(0, request.DeliveryPrice),
                Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
                SaleType = NormalizeSaleType(request.SaleType),
                ManufacturingCompanyId = request.ManufacturingCompanyId,
                ImageUrl = request.ImageUrl,
                IsDeleted = false
            });
        }

        await _context.MainProducts.AddRangeAsync(products, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, createdCount = products.Count });
    }

    [HttpPost("/ProductsPrices/CreateBulk")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> CreateBulkLegacy([FromBody] List<LegacyBulkProductPriceRequest> items, CancellationToken ct)
    {
        if (items is null || items.Count == 0) return Ok(new { success = false, message = "لا توجد منتجات للحفظ." });
        var cleaned = items.Where(item => item.SelectedMainProductId > 0 && item.Country is > 0
                && item.MinimumSellingPrice >= 0 && item.MaximumSellingPrice >= item.MinimumSellingPrice
                && item.SelectedManufacturingCompanyId > 0)
            .SelectMany(item => ExpandCountry(item.Country!.Value).Select(country => item with { Country = country })).ToList();
        if (cleaned.Count == 0) return Ok(new { success = false, message = "تأكد من اختيار المنتج والدولة والمتجر وحدود السعر." });
        var duplicate = cleaned.GroupBy(item => new { item.SelectedMainProductId, item.Country, item.SelectedManufacturingCompanyId, SaleType = NormalizeSaleType(item.SaleType) })
            .Any(group => group.Count() > 1);
        if (duplicate) return Ok(new { success = false, message = "لا يمكن إضافة نفس المنتج لنفس الدولة ونفس المتجر ونفس نوع البيع أكثر من مرة داخل الجدول." });
        var sourceIds = cleaned.Select(item => item.SelectedMainProductId).Distinct().ToList();
        var sources = await _context.MainWarehouses.AsNoTracking().Where(item => sourceIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name, item.ImageUrl }).ToDictionaryAsync(item => item.Id, ct);
        if (sources.Count != sourceIds.Count) return Ok(new { success = false, message = "يوجد منتج رئيسي غير موجود." });
        var requests = cleaned.Select(item => new CreateProductPriceRequest(sources[item.SelectedMainProductId].Name,
            item.MinimumSellingPrice, item.SelectedManufacturingCompanyId, item.Country!.Value, item.MinimumSellingPrice,
            item.MaximumSellingPrice, Math.Max(0, item.DeliveryPrice), Math.Max(1, item.Quantity), item.SaleType,
            sources[item.SelectedMainProductId].ImageUrl)).ToList();
        foreach (var request in requests)
        {
            var error = await ValidateRequestAsync(request, null, ct);
            if (error is not null) return Ok(new { success = false, message = error });
        }
        var products = requests.Select(NewProduct).ToList();
        _context.MainProducts.AddRange(products);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم إنشاء كل المنتجات بنجاح.", redirectUrl = "/ProductsPrices/Index", createdCount = products.Count });
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateProductPriceRequest request, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        var error = await ValidateRequestAsync(request, id, ct);
        if (error is not null) throw new BadRequestException(error);

        await ApplyEdit(product, request, ct);
        return Ok(ToResponse(product));
    }

    [HttpGet("/ProductsPrices/Edit")]
    public async Task<IActionResult> EditForm(int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
        return product is null ? NotFound() : Ok(ToResponse(product));
    }

    [HttpPost("/ProductsPrices/Edit")]
    public async Task<IActionResult> EditLegacy([FromForm] LegacyProductPriceEditRequest request, IFormFile? productImage, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(item => item.Id == request.Id && !item.IsDeleted, ct);
        if (product is null) return NotFound();
        var imageUrl = request.ProductImage;
        if (productImage is { Length: > 0 }) imageUrl = (await _storage.UploadAsync(productImage, "products", User.GetUserId(), ct)).PublicUrl;
        var mapped = request.ToRequest(request.Country, imageUrl);
        var error = await ValidateRequestAsync(mapped, request.Id, ct);
        if (error is not null) return BadRequest(new { success = false, message = error });
        await ApplyEdit(product, mapped, ct);
        return Ok(new { success = true, message = "تم التعديل بنجاح.", item = ToResponse(product) });
    }

    [HttpPost("/ProductsPrices/EditPopup")]
    public Task<IActionResult> EditPopup([FromForm] LegacyProductPriceEditRequest request, IFormFile? productImage, CancellationToken ct) =>
        EditLegacy(request, productImage, ct);

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([RouteOrRequest] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsDeleted = true;
        product.DeletedAt = IstanbulTimeHelper.Now;
        product.DeletedByUserId = User.GetUserId();
        product.DeletedByName = User.Identity?.Name;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product deleted." });
    }

    [HttpPost("/ProductsPrices/Delete")]
    public async Task<IActionResult> DeleteLegacy([FromForm] int id, CancellationToken ct)
    {
        if (!await _context.MainProducts.AnyAsync(product => product.Id == id, ct))
            return NotFound(new { success = false, message = "المنتج غير موجود." });
        await DeleteCore(id, ct);
        return Ok(new { success = true, message = "تم نقل المنتج إلى سلة المهملات." });
    }

    [HttpPost("Restore/{id:int}")]
    public async Task<IActionResult> Restore([RouteOrRequest] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedByUserId = null;
        product.DeletedByName = null;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product restored." });
    }

    [HttpPost("/ProductsPrices/Restore")]
    public async Task<IActionResult> RestoreLegacy([FromForm] int id, CancellationToken ct)
    {
        if (!await _context.MainProducts.AnyAsync(product => product.Id == id, ct))
            return NotFound(new { success = false, message = "المنتج غير موجود." });
        await RestoreCore(id, ct);
        return Ok(new { success = true, message = "تم استرداد المنتج." });
    }

    [HttpPost("/ProductsPrices/DeleteAll")]
    public async Task<IActionResult> DeleteAll([FromForm] int? country, [FromForm] int? manufacturingCompanyId,
        [FromForm] string? productName, [FromForm] string? saleType, CancellationToken ct)
    {
        var query = _context.MainProducts.Where(product => !product.IsDeleted);
        if (country.HasValue) query = query.Where(product => product.Country == country);
        if (manufacturingCompanyId is > 0) query = query.Where(product => product.ManufacturingCompanyId == manufacturingCompanyId);
        if (!string.IsNullOrWhiteSpace(productName))
        {
            var name = productName.Trim();
            query = query.Where(product => product.Name == name);
        }
        if (!string.IsNullOrWhiteSpace(saleType))
        {
            var type = saleType.Trim();
            query = query.Where(product => product.SaleType == type);
        }
        var products = await query.ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId();
        var userName = await GetCurrentUserName(ct);
        foreach (var product in products)
        {
            product.IsDeleted = true;
            product.DeletedAt = now;
            product.DeletedByUserId = userId;
            product.DeletedByName = userName;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = products.Count > 0 ? "تم نقل المنتجات إلى سلة المهملات." : "لا توجد منتجات للحذف.", count = products.Count });
    }

    [HttpPost("/ProductsPrices/PermanentDelete")]
    public async Task<IActionResult> PermanentDelete([FromForm] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (product is null) return NotFound(new { success = false, message = "المنتج غير موجود." });
        await _context.ProductPriceEditHistories.Where(history => history.MainProductId == id).ExecuteDeleteAsync(ct);
        _context.MainProducts.Remove(product);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم حذف المنتج نهائيًا." });
    }

    private async Task DeleteCore(int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (product is null) return;
        product.IsDeleted = true;
        product.DeletedAt = IstanbulTimeHelper.Now;
        product.DeletedByUserId = User.GetUserId();
        product.DeletedByName = await GetCurrentUserName(ct);
        await _context.SaveChangesAsync(ct);
    }

    private async Task RestoreCore(int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (product is null) return;
        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedByUserId = null;
        product.DeletedByName = null;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ApplyEdit(MainProduct product, CreateProductPriceRequest request, CancellationToken ct)
    {
        var minimum = request.MinimumSellingPrice ?? request.Price;
        var maximum = request.MaximumSellingPrice ?? minimum;
        var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
        var delivery = Math.Max(0, request.DeliveryPrice);
        var type = NormalizeSaleType(request.SaleType);
        var name = request.Name.Trim();
        var now = IstanbulTimeHelper.Now;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = await GetCurrentUserName(ct);
        var history = new List<ProductPriceEditHistory>();
        void Track(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            history.Add(new ProductPriceEditHistory
            {
                MainProductId = product.Id, FieldName = field, OldValue = oldValue, NewValue = newValue,
                EditedAt = now, EditedByUserId = userId, EditedByName = userName
            });
        }
        Track("اسم المنتج", product.Name, name);
        Track("الدولة", product.Country.ToString(System.Globalization.CultureInfo.InvariantCulture), request.Country.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Track("المتجر", product.ManufacturingCompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.ManufacturingCompanyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Track("الحد الأدنى للبيع", product.MinimumSellingPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), minimum.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        Track("الحد الأعلى للبيع", product.MaximumSellingPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), maximum.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        Track("سعر التوصيل", product.DeliveryPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), delivery.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        Track("الكمية", product.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture), quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Track("نوع البيع", product.SaleType, type);
        if (request.ImageUrl is not null) Track("الصورة", product.ImageUrl, request.ImageUrl);
        product.Name = name;
        product.Country = request.Country;
        product.Price = minimum;
        product.MinimumSellingPrice = minimum;
        product.MaximumSellingPrice = maximum;
        product.DeliveryPrice = delivery;
        product.Quantity = quantity;
        product.SaleType = type;
        product.ManufacturingCompanyId = request.ManufacturingCompanyId;
        product.ImageUrl = request.ImageUrl ?? product.ImageUrl;
        if (history.Count > 0) _context.ProductPriceEditHistories.AddRange(history);
        await _context.SaveChangesAsync(ct);
    }

    private async Task<string> GetCurrentUserName(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employeeName = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(employeeName) ? User.Identity?.Name ?? "مستخدم" : employeeName;
    }

    private async Task<string?> ValidateRequestAsync(
        CreateProductPriceRequest request,
        int? excludedProductId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Product name is required.";
        if (request.Country <= 0) return "Country is required.";
        if (request.ManufacturingCompanyId <= 0) return "Manufacturing company is required.";

        var minimumPrice = request.MinimumSellingPrice ?? request.Price;
        var maximumPrice = request.MaximumSellingPrice ?? minimumPrice;
        if (minimumPrice < 0 || maximumPrice < minimumPrice)
            return "Maximum selling price must be greater than or equal to minimum selling price.";

        if (!await _context.ManufacturingCompanies.AsNoTracking()
                .AnyAsync(company => company.Id == request.ManufacturingCompanyId, ct))
            return "Manufacturing company was not found.";

        var normalizedName = request.Name.Trim();
        var saleType = NormalizeSaleType(request.SaleType);
        var duplicateExists = await _context.MainProducts.AsNoTracking().AnyAsync(
            product => !product.IsDeleted
                && product.Id != excludedProductId
                && product.Name == normalizedName
                && product.Country == request.Country
                && product.ManufacturingCompanyId == request.ManufacturingCompanyId
                && product.SaleType == saleType,
            ct);
        return duplicateExists
            ? "The same product already exists for this country, store, and sale type."
            : null;
    }

    private static string NormalizeSaleType(string? saleType) =>
        string.IsNullOrWhiteSpace(saleType) ? "بيع فردي" : saleType.Trim();

    private static MainProduct NewProduct(CreateProductPriceRequest request)
    {
        var minimum = request.MinimumSellingPrice ?? request.Price;
        return new MainProduct
        {
            Name = request.Name.Trim(), Country = request.Country, Price = minimum, MinimumSellingPrice = minimum,
            MaximumSellingPrice = request.MaximumSellingPrice ?? minimum, DeliveryPrice = Math.Max(0, request.DeliveryPrice),
            Quantity = Math.Max(1, request.Quantity), SaleType = NormalizeSaleType(request.SaleType),
            ManufacturingCompanyId = request.ManufacturingCompanyId, ImageUrl = request.ImageUrl, IsDeleted = false
        };
    }

    private static int[] ExpandCountry(int country) => country is 5 or 9 or 10 ? [5, 9, 10] : [country];
    private static object[] CountryOptions() =>
    [
        new { Id = 1, Name = "العراق", CurrencyCode = "IQD" }, new { Id = 2, Name = "الإمارات", CurrencyCode = "AED" },
        new { Id = 3, Name = "قطر", CurrencyCode = "QAR" }, new { Id = 4, Name = "ليبيا", CurrencyCode = "LYD" },
        new { Id = 5, Name = "سلطنة عمان", CurrencyCode = "OMR" }, new { Id = 6, Name = "فلسطين", CurrencyCode = "ILS" },
        new { Id = 7, Name = "تركيا", CurrencyCode = "TRY" }, new { Id = 8, Name = "الأردن", CurrencyCode = "JOD" },
        new { Id = 9, Name = "الكويت", CurrencyCode = "KWD" }, new { Id = 10, Name = "البحرين", CurrencyCode = "BHD" },
        new { Id = 11, Name = "السعودية", CurrencyCode = "SAR" }, new { Id = 12, Name = "تونس", CurrencyCode = "TND" },
        new { Id = 13, Name = "المغرب", CurrencyCode = "MAD" }, new { Id = 14, Name = "الجزائر", CurrencyCode = "DZD" },
        new { Id = 15, Name = "لبنان", CurrencyCode = "LBP" }, new { Id = 16, Name = "مصر", CurrencyCode = "EGP" }
    ];
    private static string GetCurrency(int country) => country switch
    {
        1 => "IQD", 2 => "AED", 3 => "QAR", 4 => "LYD", 5 => "OMR", 6 => "ILS", 7 => "TRY", 8 => "JOD",
        9 => "KWD", 10 => "BHD", 11 => "SAR", 12 => "TND", 13 => "MAD", 14 => "DZD", 15 => "LBP", 16 => "EGP", _ => string.Empty
    };

    private static object ToResponse(MainProduct product) => new
    {
        product.Id,
        product.Name,
        product.Country,
        product.Price,
        product.MinimumSellingPrice,
        product.MaximumSellingPrice,
        product.DeliveryPrice,
        product.Quantity,
        product.SaleType,
        product.ImageUrl,
        product.ManufacturingCompanyId,
        product.IsDeleted
    };
}

public record CreateProductPriceRequest(
    string Name,
    decimal Price,
    int ManufacturingCompanyId,
    int Country,
    decimal? MinimumSellingPrice = null,
    decimal? MaximumSellingPrice = null,
    decimal DeliveryPrice = 0,
    int Quantity = 1,
    string? SaleType = null,
    string? ImageUrl = null);

public sealed record ProductPriceEditRequest(
    int Id,
    string Name,
    decimal Price,
    int ManufacturingCompanyId,
    int Country,
    decimal? MinimumSellingPrice = null,
    decimal? MaximumSellingPrice = null,
    decimal DeliveryPrice = 0,
    int Quantity = 1,
    string? SaleType = null,
    string? ImageUrl = null)
    : CreateProductPriceRequest(Name, Price, ManufacturingCompanyId, Country, MinimumSellingPrice,
        MaximumSellingPrice, DeliveryPrice, Quantity, SaleType, ImageUrl);

public record LegacyProductPriceRequest(
    string ProductName,
    decimal ProductPrice,
    int SelectedManufacturingCompanyId,
    int Country,
    decimal MinimumSellingPrice = 0,
    decimal MaximumSellingPrice = 0,
    decimal DeliveryPrice = 0,
    int Quantity = 1,
    string? SaleType = null,
    string? ProductImage = null)
{
    public CreateProductPriceRequest ToRequest(int country, string? imageUrl = null)
    {
        var minimum = MinimumSellingPrice > 0 ? MinimumSellingPrice : ProductPrice;
        var maximum = MaximumSellingPrice > 0 ? MaximumSellingPrice : minimum;
        return new CreateProductPriceRequest(ProductName, minimum, SelectedManufacturingCompanyId, country,
            minimum, maximum, DeliveryPrice, Quantity, SaleType, imageUrl ?? ProductImage);
    }
}

public sealed record LegacyProductPriceEditRequest(
    int Id,
    string ProductName,
    decimal ProductPrice,
    int SelectedManufacturingCompanyId,
    int Country,
    decimal MinimumSellingPrice = 0,
    decimal MaximumSellingPrice = 0,
    decimal DeliveryPrice = 0,
    int Quantity = 1,
    string? SaleType = null,
    string? ProductImage = null)
    : LegacyProductPriceRequest(ProductName, ProductPrice, SelectedManufacturingCompanyId, Country,
        MinimumSellingPrice, MaximumSellingPrice, DeliveryPrice, Quantity, SaleType, ProductImage);

public sealed record LegacyBulkProductPriceRequest(
    int SelectedMainProductId,
    int? Country,
    decimal MinimumSellingPrice,
    decimal MaximumSellingPrice,
    decimal DeliveryPrice,
    decimal ProductPrice,
    int Quantity,
    string? SaleType,
    int SelectedManufacturingCompanyId);
