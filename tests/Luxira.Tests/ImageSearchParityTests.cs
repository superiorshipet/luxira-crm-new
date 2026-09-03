using Luxira.Api.Features.SearchKeywords.Controllers;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Mvc.Routing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Luxira.Tests;

public sealed class ImageSearchParityTests
{
    [Fact]
    public void LegacySearchRouteIsPublished()
    {
        var route = typeof(ImageSearchController).GetMethod(nameof(ImageSearchController.Search))!
            .GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        Assert.Contains(route, attribute => attribute.HttpMethods.Contains("POST") && attribute.Template == "Search");
    }

    [Fact]
    public async Task PerceptualHashIsStableAndDistanceUsesAll64Bits()
    {
        using var image = new Image<Rgba32>(40, 40, Color.Black);
        for (var x = 0; x < 20; x++)
            for (var y = 0; y < 40; y++)
                image[x, y] = Color.White;
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        await image.SaveAsPngAsync(first);
        await image.SaveAsPngAsync(second);
        first.Position = second.Position = 0;
        Assert.Equal(await ImageSearchService.ComputeHashAsync(first), await ImageSearchService.ComputeHashAsync(second));
        Assert.Equal(64, ImageSearchService.HammingDistance(0, -1));
    }

    [Theory]
    [InlineData("<think>ignore 123</think> رقم الطلب ٠٥٠ ١٢٣ ٤٥٦٧", "0501234567")]
    [InlineData("محمد أحمد", "محمد أحمد")]
    [InlineData("NONE", null)]
    [InlineData("<think>unfinished 123", null)]
    public void VisionTextIsStrictlyNormalized(string input, string? expected) =>
        Assert.Equal(expected, ImageVisionService.Normalize(input));
}
