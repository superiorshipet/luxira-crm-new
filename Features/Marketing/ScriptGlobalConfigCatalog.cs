namespace Luxira.Api.Features.Marketing;

public sealed record ScriptGlobalConfigItem(
    string Key,
    string Group,
    string Label,
    string Description,
    string Kind,
    string Default,
    string Unit = "",
    bool ReadOnly = false);

public static class ScriptGlobalConfigCatalog
{
    public static readonly (string Id, string Title, string Blurb)[] Groups =
    [
        ("typing", "سرعة الكتابة", "إيقاع كتابة الحروف والوقفات."),
        ("delivery", "الإرسال والتأكيد", "توقيت الإرسال ومحاولات التأكد."),
        ("theme", "الألوان الافتراضية", "ألوان المتاجر التي لا تملك تخصيصًا."),
        ("chrome", "نصوص الواجهة", "النصوص الثابتة في شريط الأزرار."),
        ("behaviour", "سلوك الشريط", "إعدادات تشغيل الشريط العامة."),
    ];

    public static readonly IReadOnlyList<ScriptGlobalConfigItem> Items =
    [
        N("TYPE_MIN", "typing", "أقل زمن لدفعة حروف", "75", "ms"),
        N("TYPE_MAX", "typing", "أعلى زمن لدفعة حروف", "125", "ms"),
        N("TYPE_SPACE", "typing", "وقفة بعد المسافة", "10", "ms"),
        N("TYPE_PUNCT", "typing", "وقفة بعد علامة الترقيم", "37", "ms"),
        N("CHUNK_MIN", "typing", "أصغر دفعة حروف", "3", "حرف"),
        N("CHUNK_MAX", "typing", "أكبر دفعة حروف", "5", "حرف"),
        N("CHUNK_EVERY", "typing", "كل كم دفعة يحدث تفكير", "6", "دفعة"),
        N("CHUNK_BONUS", "typing", "طول وقفة التفكير", "58", "ms"),
        N("PRE", "delivery", "وقفة المراجعة قبل الإرسال", "58", "ms"),
        N("DELAY", "delivery", "الفجوة بين رسالتين", "200", "ms"),
        N("POST", "delivery", "وقفة بعد الإرسال", "25", "ms"),
        N("INIT_GAP", "delivery", "الفجوة قبل أول رسالة", "83", "ms"),
        N("ACK_TIMEOUT", "delivery", "مهلة تأكيد الوصول", "2000", "ms"),
        N("ACK_POLL", "delivery", "معدل فحص التأكيد", "200", "ms"),
        N("ACK_RETRY", "delivery", "عدد محاولات إعادة الإرسال", "3", "محاولة"),
        C("GOLD", "لون الأيقونة", "#2563eb"),
        C("PROGRESS", "لون شريط التقدم", "#16a34a"),
        C("STOP", "لون زر الإيقاف", "#dc2626"),
        C("STOP_HOVER", "لون زر الإيقاف عند المرور", "#b91c1c"),
        T("CHROME_SEARCH_PLACEHOLDER", "chrome", "نص خانة البحث", "ابحث"),
        T("CHROME_STOP", "chrome", "نص زر الإيقاف", "إيقاف الإرسال"),
        T("CHROME_SEND_FAILED", "chrome", "نص فشل الإرسال", "فشل الإرسال"),
        T("CHROME_NOT_READY", "chrome", "نص عدم وجود محادثة", "افتح محادثة أولا"),
        T("CHROME_BACK", "chrome", "نص زر الرجوع", "رجوع"),
        N("SCALE", "behaviour", "حجم الشريط", "0.85"),
        N("SELF_HEAL_INTERVAL_MS", "behaviour", "معدل إعادة بناء الشريط", "1500", "ms"),
        N("SWIPE_THRESHOLD_PX", "behaviour", "حد السحب باللمس", "20", "px"),
        T("TWEMOJI_BASE", "behaviour", "مصدر صور الإيموجي", "https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72/"),
        new("ENGINE_VERSION", "behaviour", "إصدار المحرك", "يتغير مع النشر فقط.", "text", "1.0.0", ReadOnly: true),
    ];

    private static readonly Dictionary<string, ScriptGlobalConfigItem> ByKey = Items.ToDictionary(item => item.Key, StringComparer.Ordinal);
    private static readonly HashSet<string> ThemeKeys = ["GOLD", "PROGRESS", "STOP", "STOP_HOVER"];
    private static readonly HashSet<string> FlagKeys = ["SELF_HEAL_INTERVAL_MS", "SWIPE_THRESHOLD_PX", "TWEMOJI_BASE"];

    public static ScriptGlobalConfigItem? Find(string key) => ByKey.GetValueOrDefault(key);
    public static string TargetFor(string key) => ThemeKeys.Contains(key) ? "theme" : FlagKeys.Contains(key) ? "flags" : key.StartsWith("CHROME_", StringComparison.Ordinal) ? "chrome" : "settings";
    public static string RuntimeKeyFor(string key) => key.StartsWith("CHROME_", StringComparison.Ordinal) ? key["CHROME_".Length..] : key;

    private static ScriptGlobalConfigItem N(string key, string group, string label, string value, string unit = "") => new(key, group, label, label, "number", value, unit);
    private static ScriptGlobalConfigItem C(string key, string label, string value) => new(key, "theme", label, label, "color", value);
    private static ScriptGlobalConfigItem T(string key, string group, string label, string value) => new(key, group, label, label, "text", value);
}
