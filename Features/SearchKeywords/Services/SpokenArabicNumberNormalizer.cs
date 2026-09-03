using System.Globalization;
using System.Text.RegularExpressions;

namespace Luxira.Api.Features.SearchKeywords.Services;

public static partial class SpokenArabicNumberNormalizer
{
    private static readonly Dictionary<string, int> Values = new(StringComparer.Ordinal)
    {
        ["صفر"] = 0, ["زيرو"] = 0, ["واحد"] = 1, ["واحده"] = 1, ["احد"] = 1,
        ["اتنين"] = 2, ["اثنين"] = 2, ["اثنان"] = 2, ["تلاته"] = 3, ["ثلاثه"] = 3,
        ["اربعه"] = 4, ["خمسه"] = 5, ["همسه"] = 5, ["سته"] = 6, ["سبعه"] = 7,
        ["تمانيه"] = 8, ["ثمانيه"] = 8, ["تسعه"] = 9, ["عشره"] = 10,
        ["حداشر"] = 11, ["احداشر"] = 11, ["اتناشر"] = 12, ["اثناشر"] = 12,
        ["تلتاشر"] = 13, ["ثلاثتاشر"] = 13, ["اربعتاشر"] = 14, ["خمستاشر"] = 15,
        ["همستاشر"] = 15, ["ستاشر"] = 16, ["سبعتاشر"] = 17, ["تمنتاشر"] = 18,
        ["تمانتاشر"] = 18, ["تسعتاشر"] = 19, ["عشرين"] = 20, ["تلاتين"] = 30,
        ["ثلاثين"] = 30, ["اربعين"] = 40, ["خمسين"] = 50, ["ستين"] = 60,
        ["سبعين"] = 70, ["تمانين"] = 80, ["ثمانين"] = 80, ["تسعين"] = 90,
        ["ميه"] = 100, ["مئه"] = 100, ["مائه"] = 100, ["ميتين"] = 200,
        ["مائتين"] = 200, ["تلتميه"] = 300, ["ثلاثميه"] = 300, ["اربعميه"] = 400,
        ["خمسميه"] = 500, ["همسميه"] = 500, ["ستميه"] = 600, ["سبعميه"] = 700,
        ["تمانميه"] = 800, ["ثمانميه"] = 800, ["تسعميه"] = 900,
    };
    private static readonly HashSet<string> Ignored = ["و", "رقم", "كود", "طلب", "اوردر", "order", "الاوردر", "التتبع"];

    public static string? ExtractOrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = NormalizeDigits(value)
            .Replace("터", "ت", StringComparison.Ordinal)
            .Replace("스", "س", StringComparison.Ordinal)
            .Replace("로", "لو", StringComparison.Ordinal);
        normalized = DiacriticsRegex().Replace(normalized, string.Empty);
        normalized = Regex.Replace(normalized, @"\bهم\s+سمي(ه|ة)?\b", "همسميه", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\bهم\s+ستاشر\b", "همستاشر", RegexOptions.IgnoreCase);
        var tokens = TokenSplitRegex().Split(normalized).Where(token => token.Length > 0).ToList();
        var numbers = new List<(string Raw, int? Value, bool Thousand, bool Digits)>();
        foreach (var token in tokens)
        {
            if (token.All(char.IsDigit)) { numbers.Add((token, null, false, true)); continue; }
            var word = NormalizeWord(token);
            if (Ignored.Contains(word)) continue;
            if (word is "الف" or "الاف") { numbers.Add((word, null, true, false)); continue; }
            if (Values.TryGetValue(word, out var number)) numbers.Add((word, number, false, false));
        }
        if (numbers.Count == 0) return null;
        if (numbers.Count >= 2 && numbers.All(item => item.Digits || item.Value is >= 0 and <= 9))
            return string.Concat(numbers.Select(item => item.Digits ? item.Raw : item.Value!.Value.ToString(CultureInfo.InvariantCulture)));
        var total = 0;
        var current = 0;
        foreach (var item in numbers)
        {
            if (item.Thousand) { total += (current == 0 ? 1 : current) * 1000; current = 0; }
            else if (item.Digits) current += int.Parse(item.Raw, CultureInfo.InvariantCulture);
            else current += item.Value ?? 0;
        }
        return (total + current).ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeDigits(string value) => value
        .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
        .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9')
        .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
        .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');

    private static string NormalizeWord(string value)
    {
        var word = value.Trim().ToLowerInvariant().Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه").Replace("ى", "ي");
        if (word.StartsWith('و') && word.Length > 2 && word is not ("واحد" or "واحده")) word = word[1..];
        return word;
    }

    [GeneratedRegex(@"[^\p{L}\d]+")]
    private static partial Regex TokenSplitRegex();

    [GeneratedRegex(@"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]")]
    private static partial Regex DiacriticsRegex();
}
