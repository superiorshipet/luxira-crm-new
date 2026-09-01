namespace Luxira.Api.Features.ReferenceData.FailureReasons;

internal static class FailureReasonCatalog
{
    internal static readonly FailureReason[] All =
    [
        new(1, "لم يرد على الهاتف"),
        new(2, "رقم الهاتف مغلق"),
        new(3, "رقم الهاتف غير فعال"),
        new(4, "رفض استلام الطلب"),
        new(5, "العميل غير متاح للاستلام"),
        new(6, "العميل مسافر"),
        new(7, "رفض بسبب عدم الفحص"),
        new(8, "المبلغ المطلوب غير متوفر"),
        new(9, "تأجيل الاستلام"),
        new(10, "خطأ بالطلبية"),
        new(11, "الطلب غير مطابق للمطلوب"),
    ];
}
