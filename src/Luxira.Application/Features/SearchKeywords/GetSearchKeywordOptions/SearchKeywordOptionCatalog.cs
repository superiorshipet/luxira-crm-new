namespace Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;

internal static class SearchKeywordOptionCatalog
{
    internal static readonly string[] FallbackCategories =
    [
        "أسعار وترتيب",
        "فترات زمنية",
        "حالات الطلبات",
        "دول ومناطق",
        "مصادر الطلبات",
        "فلاتر مخصصة",
        "عام",
    ];

    internal static readonly SearchKeywordOption[] TargetTypes =
    [
        new("SortOrder", "ترتيب وفرز النتائج (Sort Order)"),
        new("OrderStatus", "حالة الطلب (Order Status)"),
        new("Country", "دولة (Country)"),
        new("City", "مدينة أو محافظة (City / State)"),
        new("DateScope", "نطاق زمني وتاريخ (Date Scope)"),
        new("OrderSource", "مصدر الطلب (Order Source)"),
        new("SpecialClients", "عملاء مميزين VIP"),
        new("Offers", "عروض وخصومات"),
        new("Complaints", "شكاوى وبلاغات"),
        new("FromComments", "طلبات الكومنتات"),
        new("Unpaid", "غير مدفوع"),
        new("Gender", "الجنس (رجالي / حريمي)"),
    ];

    internal static readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<SearchKeywordOption>> TypeOptions =
        new Dictionary<string, IReadOnlyList<SearchKeywordOption>>
        {
            ["SortOrder"] =
            [
                new("HighestPrice", "الأعلى سعراً (Highest Price)"),
                new("LowestPrice", "الأرخص سعراً (Lowest Price)"),
                new("TopSelling", "الأكثر مبيعاً (Top Selling)"),
                new("LowestSelling", "الأقل مبيعاً (Lowest Selling)"),
                new("Fastest", "الأسرع توصيلاً (Fastest)"),
                new("Slowest", "الأبطأ توصيلاً (Slowest)"),
                new("LowestImports", "الأقل واردات ومخزون"),
            ],
            ["OrderStatus"] =
            [
                new("طلب_جديد", "طلب جديد"),
                new("تم_التجهيز", "تم التجهيز"),
                new("قيد_التوصيل", "قيد التوصيل"),
                new("تم_التسليم", "تم التسليم"),
                new("تم_الإلغاء", "تم الإلغاء / كنسل"),
                new("الطلبات_المرجعة", "الطلبات المرجعة"),
                new("أرشيف_المرجع", "أرشيف المرجع"),
                new("انتظار_المعالجة", "انتظار المعالجة / معلقة"),
                new("الطلبات_المؤجلة", "الطلبات المؤجلة"),
                new("الطلبات_الغير_مكتملة", "الطلبات غير المكتملة"),
                new("فشل_التسليم_1", "فشل التسليم 1"),
                new("فشل_التسليم_2", "فشل التسليم 2"),
                new("فشل_التسليم_3", "فشل التسليم 3"),
                new("فشل_التسليم_4", "فشل التسليم 4"),
                new("فشل_التسليم_5", "فشل التسليم 5"),
                new("فشل_التسليم_6", "فشل التسليم 6"),
                new("فشل_التسليم_7", "فشل التسليم 7"),
                new("تم_الدفع", "تم الدفع"),
            ],
            ["Country"] =
            [
                new("سلطنة_عمان", "سلطنة عمان"),
                new("مصر", "مصر"),
                new("السعودية", "السعودية"),
                new("الإمارات", "الإمارات"),
                new("الكويت", "الكويت"),
                new("قطر", "قطر"),
                new("البحرين", "البحرين"),
                new("العراق", "العراق"),
                new("الأردن", "الأردن"),
                new("تركيا", "تركيا"),
                new("ليبيا", "ليبيا"),
                new("فلسطين", "فلسطين"),
                new("تونس", "تونس"),
                new("المغرب", "المغرب"),
                new("الجزائر", "الجزائر"),
                new("لبنان", "لبنان"),
            ],
            ["DateScope"] =
            [
                new("Today", "اليوم (Today)"),
                new("Yesterday", "أمس / امبارح (Yesterday)"),
                new("ThisWeek", "هذا الأسبوع (This Week)"),
                new("ThisMonth", "هذا الشهر (This Month)"),
            ],
            ["OrderSource"] =
            [
                new("واتساب", "واتساب (WhatsApp)"),
                new("فيسبوك", "فيسبوك (Facebook)"),
                new("فيسبوك", "فيسبوك (Facebook)"),
                new("ميتا", "ميتا (Meta)"),
            ],
            ["Gender"] =
            [
                new("Female", "حريمي / نسائي (Female)"),
                new("Male", "رجالي / شبابي (Male)"),
            ],
            ["SpecialClients"] = [new("true", "عملاء مميزين VIP")],
            ["Offers"] = [new("true", "عروض وخصومات")],
            ["Complaints"] = [new("true", "شكاوى وبلاغات")],
            ["FromComments"] = [new("true", "طلبات الكومنتات")],
            ["Unpaid"] = [new("true", "غير مدفوع")],
        };
}
