using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Luxira.Api.Features.ManufacturingCompanies.DTOs;
using Luxira.Api.Features.Warehouses.DTOs;

namespace Luxira.Api.IntegrationTests;

public sealed class WarehouseAndManufacturingFeatureTests(LuxiraApiFactory factory) : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task WarehouseAndManufacturingFlowSucceeds()
    {
        var token = TestJwtTokenFactory.Create("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create Warehouse
        var whReq = new CreateWarehouseRequest("مستودع الكرادة", "الكرادة 1", "بغداد - الكرادة", 1, "بغداد", null);
        var whRes = await _client.PostAsJsonAsync("/api/v1/warehouses", whReq);
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);

        var wh = await whRes.Content.ReadFromJsonAsync<WarehouseDto>();
        Assert.NotNull(wh);
        Assert.Equal("مستودع الكرادة", wh.Name);

        // Create Manufacturing Company / Store
        var compReq = new CreateManufacturingCompanyRequest("متجر لوتس بلو", "Lotus Blue", "LB-01", 1, "متجر رئيسي");
        var compRes = await _client.PostAsJsonAsync("/api/v1/manufacturing-companies", compReq);
        Assert.Equal(HttpStatusCode.OK, compRes.StatusCode);

        var comp = await compRes.Content.ReadFromJsonAsync<ManufacturingCompanyDto>();
        Assert.NotNull(comp);

        // Create Product
        var prodReq = new CreateProductRequest("عطر لوتس رويال 100 مل", "LB-ROYAL-100", 25000m, 12000m, comp.Id);
        var prodRes = await _client.PostAsJsonAsync("/api/v1/manufacturing-companies/products", prodReq);
        Assert.Equal(HttpStatusCode.OK, prodRes.StatusCode);

        var prod = await prodRes.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(prod);

        // Set Product Minimum Price
        var minPriceReq = new SetProductMinimumPriceRequest(prod.Id, 1, 20000m);
        var minPriceRes = await _client.PostAsJsonAsync("/api/v1/product-prices/minimum-prices", minPriceReq);
        Assert.Equal(HttpStatusCode.OK, minPriceRes.StatusCode);
    }
}
