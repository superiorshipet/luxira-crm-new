namespace Luxira.Api.Features.Orders.Models;

/// <summary>
/// Stable integer values persisted by the legacy CRM in Orders.OrderStatus.
/// These values are a database contract and must never be reordered or renumbered.
/// </summary>
public static class OrderStatusCodes
{
    public const int New = 0;
    public const int Prepared = 2;
    public const int Processed = 3;
    public const int InDelivery = 4;
    public const int TemporarilyDelivered = 5;
    public const int Delivered = 6;
    public const int FailedDelivery = 7;
    public const int WaitingForProcessing = 8;
    public const int Returned = 9;
    public const int DeliveryOrRepresentativeError = 10;
    public const int Cancelled = 11;
    public const int Postponed = 12;
    public const int BalanceUpdated = 13;
    public const int Paid = 14;
    public const int ReferenceArchive = 15;
    public const int Suspended = 16;
    public const int Unknown = 17;
    public const int Incomplete = 18;
    public const int FailedDeliveryStage2 = 19;
    public const int FailedDeliveryStage3 = 20;
    public const int FailedDeliveryStage4 = 21;
    public const int FailedDeliveryStage5 = 22;
    public const int FailedDeliveryStage6 = 23;
    public const int FailedDeliveryStage7 = 24;
    public const int IncompleteStage2 = 25;
    public const int IncompleteStage3 = 26;
    public const int IncompleteStage4 = 27;
    public const int IncompleteStage5 = 28;
    public const int IncompleteStage6 = 29;
    public const int FailedDeliveryStage1 = 30;
    public const int IncompleteStage1 = 31;
    public const int Suspicious = 32;

    public static readonly int[] FailureStatuses =
    [
        FailedDelivery,
        FailedDeliveryStage1,
        FailedDeliveryStage2,
        FailedDeliveryStage3,
        FailedDeliveryStage4,
        FailedDeliveryStage5,
        FailedDeliveryStage6,
        FailedDeliveryStage7,
    ];

    public static readonly int[] ClosedStatuses =
    [
        Delivered,
        BalanceUpdated,
        Paid,
        Cancelled,
        ReferenceArchive,
    ];

    private static readonly HashSet<int> DefinedStatuses =
    [
        New, Prepared, Processed, InDelivery, TemporarilyDelivered, Delivered,
        FailedDelivery, WaitingForProcessing, Returned,
        DeliveryOrRepresentativeError, Cancelled, Postponed, BalanceUpdated,
        Paid, ReferenceArchive, Suspended, Unknown, Incomplete,
        FailedDeliveryStage1, FailedDeliveryStage2, FailedDeliveryStage3,
        FailedDeliveryStage4, FailedDeliveryStage5, FailedDeliveryStage6,
        FailedDeliveryStage7, IncompleteStage1, IncompleteStage2,
        IncompleteStage3, IncompleteStage4, IncompleteStage5,
        IncompleteStage6, Suspicious,
    ];

    public static bool IsDefined(int status) => DefinedStatuses.Contains(status);

    public static string GetDisplayName(int status) => status switch
    {
        New => "طلب جديد",
        Prepared => "تم التجهيز",
        Processed => "تمت المعالجة",
        InDelivery => "قيد التوصيل",
        TemporarilyDelivered => "تم التسليم المؤقت",
        Delivered => "تم التسليم",
        FailedDelivery => "فشل التسليم",
        FailedDeliveryStage1 => "فشل التسليم 1",
        FailedDeliveryStage2 => "فشل التسليم 2",
        FailedDeliveryStage3 => "فشل التسليم 3",
        FailedDeliveryStage4 => "فشل التسليم 4",
        FailedDeliveryStage5 => "فشل التسليم 5",
        FailedDeliveryStage6 => "فشل التسليم 6",
        FailedDeliveryStage7 => "فشل التسليم 7",
        WaitingForProcessing => "انتظار المعالجة",
        Returned => "الطلبات المرجعة",
        DeliveryOrRepresentativeError => "أخطاء الشركات والمندوبين",
        Cancelled => "تم الإلغاء",
        Postponed => "الطلبات المؤجلة",
        BalanceUpdated => "تم تحديث الرصيد",
        Paid => "تم الدفع",
        ReferenceArchive => "أرشيف المرجع",
        Suspended => "الطلبات المعلقة",
        Unknown => "الطلبات غير المعرفة",
        Incomplete => "الطلبات غير المكتملة",
        IncompleteStage1 => "الطلبات غير المكتملة 1",
        IncompleteStage2 => "الطلبات غير المكتملة 2",
        IncompleteStage3 => "الطلبات غير المكتملة 3",
        IncompleteStage4 => "الطلبات غير المكتملة 4",
        IncompleteStage5 => "الطلبات غير المكتملة 5",
        IncompleteStage6 => "الطلبات غير المكتملة 6",
        Suspicious => "حالة مشكوك بها",
        _ => "غير معروف",
    };
}
