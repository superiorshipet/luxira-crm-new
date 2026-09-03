using Luxira.Api.Features.SearchKeywords.Controllers;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class VoiceSearchParityTests
{
    [Theory]
    [InlineData("واحد اتنين تلاته اربعه خمسه", "12345")]
    [InlineData("رقم الطلب خمسمية و تلاتة وعشرين", "523")]
    [InlineData("١٢٣٤٥٦", "123456")]
    [InlineData("كود الف و ميتين", "1200")]
    public void SpokenArabicNumberIsNormalized(string spoken, string expected) =>
        Assert.Equal(expected, SpokenArabicNumberNormalizer.ExtractOrderNumber(spoken));

    [Fact]
    public void LegacyTranscribeRouteIsPublished()
    {
        var attributes = typeof(VoiceSearchController).GetMethod(nameof(VoiceSearchController.Transcribe))!
            .GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains("POST") && attribute.Template == "Transcribe");
    }
}
