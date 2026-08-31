namespace Luxira.Api.Features.ReferenceData.Countries;

internal static class CountryCatalog
{
    internal static readonly CountryResponse[] All =
    [
        new(1, "العراق", "/Countries/iraq.svg"),
        new(2, "الإمارات", "/Countries/emirates.svg"),
        new(3, "قطر", "/Countries/qatar.svg"),
        new(4, "ليبيا", "/Countries/libya.svg"),
        new(5, "سلطنة_عمان", "/Countries/oman.svg"),
        new(6, "فلسطين", "/Countries/palestine.svg"),
        new(7, "تركيا", "/Countries/turkey.svg"),
        new(8, "الأردن", "/Countries/jordan.svg"),
        new(9, "الكويت", "/Countries/kuwait.svg"),
        new(10, "البحرين", "/Countries/bahrain.svg"),
        new(11, "السعودية", "/Countries/saudiarabia.svg"),
        new(12, "تونس", "/Countries/tunisia.svg"),
        new(13, "المغرب", "/Countries/morocco.svg"),
        new(14, "الجزائر", "/Countries/algeria.svg"),
        new(15, "لبنان", "/Countries/lebanon.svg"),
        new(16, "مصر", "/Countries/egypt.svg"),
    ];

    internal static readonly CountryResponse[] PreparationForDelivery =
    [
        All[0],
        All[3],
        All[4],
        All[1],
    ];
}

internal sealed record CountryResponse(
    int Id,
    string Name,
    string ImageUrl);
