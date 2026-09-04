using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Warehouses.Models;

namespace Luxira.Api.Features.Media.Services;
    /// <summary>
    /// One database column that stores a media reference (a wwwroot path before migration, the
    /// serving route after).
    /// </summary>
    public sealed class MediaColumnSpec
    {
        /// <summary>EF entity CLR type. Must have an int "Id" key.</summary>
        public Type Entity { get; init; } = null!;

        /// <summary>Property holding the URL/path string.</summary>
        public string UrlProperty { get; init; } = null!;

        /// <summary>
        /// Property that receives the S3 key on migration, or null for columns that are only
        /// rewritten — snapshot copies of files owned by another spec (log-table avatars), and
        /// JSON lists. A null key property does not mean "no upload": if the file behind the URL
        /// is not in the bucket yet it is uploaded under the same mirrored key its owner would use.
        /// </summary>
        public string? KeyProperty { get; init; }

        /// <summary>
        /// wwwroot folder this column's files normally live under, forward slashes, no leading
        /// slash. Only affects key aesthetics: a file outside this folder still migrates, under
        /// "{Prefix}/_outside/{full relative path}", and both forms map back to a unique local
        /// path — see <see cref="MediaModuleRegistry.DeriveKey"/>.
        /// </summary>
        public string Folder { get; init; } = null!;

        /// <summary>S3 prefix. Shared prefixes are deliberate: two columns pointing at the same
        /// local file must derive the same key, so the file is uploaded once, not twice.</summary>
        public string Prefix { get; init; } = null!;

        /// <summary>The column holds a JSON array of URL strings rather than a single URL.</summary>
        public bool IsJsonArray { get; init; }

        /// <summary>The column holds one or more URLs separated by "||" (employee errors).</summary>
        public bool IsPipeList { get; init; }

        /// <summary>
        /// The column holds the product-images mixed format: a single URL, a "||" list, or a JSON
        /// array. Rewritten in place; serialized back the way ProductImagesController does it —
        /// one url plain, several as a JSON array.
        /// </summary>
        public bool IsMediaList { get; init; }
    }

    /// <summary>A dashboard row: one logical media system, spanning one or more columns.</summary>
    public sealed class MediaModule
    {
        public string Key { get; init; } = null!;
        public string Label { get; init; } = null!;
        public string? Note { get; init; }
        public MediaColumnSpec[] Columns { get; init; } = Array.Empty<MediaColumnSpec>();
    }

    /// <summary>
    /// Every media system in the CRM, declaratively: which column, which folder, which S3 prefix.
    /// The migration service, the delete-local service, the serving route's prefix whitelist and
    /// the dashboard's module table are all driven from this one list.
    ///
    /// Order posts are NOT in the migrate flow here — they shipped first with their own bespoke
    /// backfill (MediaMigrationService.MigrateBatchAsync) that deliberately preserves Url. They do
    /// appear as a module so the unified dashboard table and the delete-local flow cover them.
    /// </summary>
    public static class MediaModuleRegistry
    {
        public const string ServingRoute = "/Media/File";

        /// <summary>Module key whose migrate flow is the bespoke order-posts one.</summary>
        public const string OrderPostsModuleKey = "order-posts";

        public static readonly MediaModule[] Modules =
        {
            new()
            {
                Key = OrderPostsModuleKey,
                Label = "صور الإبلاغات (منشورات الطلبات)",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(OrderPostImage), UrlProperty = nameof(OrderPostImage.Url), KeyProperty = nameof(OrderPostImage.S3Key), Folder = "images/orderposts", Prefix = "orderposts" },
                },
            },
            new()
            {
                Key = "orders",
                Label = "صور الطلبات والإيصالات",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(Order), UrlProperty = nameof(Order.PhotoUrl), KeyProperty = nameof(Order.PhotoS3Key), Folder = "images/orders", Prefix = "orders" },
                    new() { Entity = typeof(Order), UrlProperty = nameof(Order.PaymentReceiptUrl), KeyProperty = nameof(Order.PaymentReceiptS3Key), Folder = "images/receipts", Prefix = "receipts" },
                },
            },
            new()
            {
                Key = "failure-reasons",
                Label = "صور أسباب فشل التسليم",
                Columns = new MediaColumnSpec[]
                {
                    // Stores several URLs joined by "|", exactly like the EmployeeError columns
                    // below. Without this flag the cleanup sweep treats the whole joined string as
                    // one path, finds no file by that name, and clears the entire list.
                    new() { Entity = typeof(OrderStatusHistory), UrlProperty = nameof(OrderStatusHistory.FailureReasonImageUrl), KeyProperty = nameof(OrderStatusHistory.FailureReasonImageS3Key), Folder = "images/failure-reasons", Prefix = "failure-reasons", IsPipeList = true },
                },
            },
            new()
            {
                Key = "order-followups",
                Label = "صور طلبات المتابعة",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(OrderFollowUpRequest), UrlProperty = nameof(OrderFollowUpRequest.ImagePath), KeyProperty = nameof(OrderFollowUpRequest.ImageS3Key), Folder = "uploads/order-followups", Prefix = "order-followups" },
                },
            },
            new()
            {
                Key = "stores",
                Label = "شعارات وفواتير المتاجر",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(ManufacturingCompany), UrlProperty = nameof(ManufacturingCompany.ImageUrl), KeyProperty = nameof(ManufacturingCompany.ImageS3Key), Folder = "Stores", Prefix = "stores" },
                    new() { Entity = typeof(ManufacturingCompany), UrlProperty = nameof(ManufacturingCompany.ImageUrl2), KeyProperty = nameof(ManufacturingCompany.ImageUrl2S3Key), Folder = "Stores", Prefix = "stores" },
                    new() { Entity = typeof(ManufacturingCompany), UrlProperty = nameof(ManufacturingCompany.InvoiceImage), KeyProperty = nameof(ManufacturingCompany.InvoiceImageS3Key), Folder = "Stores", Prefix = "stores" },
                },
            },
            new()
            {
                Key = "delivery-companies",
                Label = "شعارات وملفات شركات التوصيل",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(DeliveryCompany), UrlProperty = nameof(DeliveryCompany.ImageUrl), KeyProperty = nameof(DeliveryCompany.ImageS3Key), Folder = "deliverycompanies", Prefix = "deliverycompanies" },
                    new() { Entity = typeof(DeliveryCompany), UrlProperty = nameof(DeliveryCompany.InformationUrl), KeyProperty = nameof(DeliveryCompany.InformationS3Key), Folder = "deliverycompanies", Prefix = "deliverycompanies" },
                },
            },
            new()
            {
                Key = "employees",
                Label = "ملفات الموظفين (صور، سير ذاتية، هويات)",
                Note = "السير الذاتية وصور الهويات تُقدَّم بصلاحيات مقيدة",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(Employee), UrlProperty = nameof(Employee.ImageUrl), KeyProperty = nameof(Employee.ImageS3Key), Folder = "Employees", Prefix = "employees" },
                    new() { Entity = typeof(Employee), UrlProperty = nameof(Employee.Cv), KeyProperty = nameof(Employee.CvS3Key), Folder = "Employees", Prefix = "employees-private" },
                    new() { Entity = typeof(Employee), UrlProperty = nameof(Employee.IdCardFrontImage), KeyProperty = nameof(Employee.IdCardFrontImageS3Key), Folder = "Employees", Prefix = "employees-private" },
                    new() { Entity = typeof(Employee), UrlProperty = nameof(Employee.IdCardBackImage), KeyProperty = nameof(Employee.IdCardBackImageS3Key), Folder = "Employees", Prefix = "employees-private" },
                },
            },
            new()
            {
                Key = "employee-avatars-snapshots",
                Label = "نسخ صور الموظفين داخل السجلات",
                Note = "إعادة توجيه فقط — الملفات نفسها تخص وحدة الموظفين",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(StatusUpdateBatchLog), UrlProperty = nameof(StatusUpdateBatchLog.EmployeeImageUrl), KeyProperty = null, Folder = "Employees", Prefix = "employees" },
                    new() { Entity = typeof(EmployeeTaskAssignment), UrlProperty = nameof(EmployeeTaskAssignment.EmployeeImageUrl), KeyProperty = null, Folder = "Employees", Prefix = "employees" },
                },
            },
            new()
            {
                Key = "main-warehouses",
                Label = "صور المستودعات الرئيسية",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(MainWarehouse), UrlProperty = nameof(MainWarehouse.ImageUrl), KeyProperty = nameof(MainWarehouse.ImageS3Key), Folder = "MainWarehouseImages", Prefix = "mainwarehouses" },
                },
            },
            new()
            {
                Key = "products",
                Label = "صور المنتجات",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(MainProduct), UrlProperty = nameof(MainProduct.ImageUrl), KeyProperty = nameof(MainProduct.ImageS3Key), Folder = "Products", Prefix = "products" },
                },
            },
            new()
            {
                Key = "product-images",
                Label = "مكتبة صور المنتجات",
                Note = "الأعمدة تحمل رابطًا واحدًا أو عدة روابط فتُعاد كتابة الروابط بدل عمود مفتاح",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(ProductImage), UrlProperty = nameof(ProductImage.ImageUrl), KeyProperty = null, Folder = "ProductImages", Prefix = "productimages", IsMediaList = true },
                    // Draft files live under ProductImagesTemp until publish moves them; either
                    // way DeriveKey's _outside fallback keeps the mapping reversible.
                    new() { Entity = typeof(ProductImageDraft), UrlProperty = nameof(ProductImageDraft.ImageUrl), KeyProperty = null, Folder = "ProductImages", Prefix = "productimages", IsMediaList = true },
                },
            },
            new()
            {
                Key = "screen-records",
                Label = "تسجيلات الشاشة",
                Note = "التسجيل يُلحق مقاطع بملف اليوم طوال الدوام، لذلك تُرحَّل الأيام المغلقة فقط (الأمس فما قبل)",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(ScreenRecord), UrlProperty = nameof(ScreenRecord.VideoPath), KeyProperty = nameof(ScreenRecord.VideoS3Key), Folder = "ScreenRecords", Prefix = "screenrecords" },
                },
            },
            new()
            {
                Key = "call-recordings",
                Label = "تسجيلات المكالمات",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(CallRecording), UrlProperty = nameof(CallRecording.RecordingPath), KeyProperty = nameof(CallRecording.RecordingS3Key), Folder = "CallRecordings", Prefix = "callrecordings" },
                },
            },
            new()
            {
                Key = "attendance",
                Label = "صور الحضور والانصراف",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(EmployeeAttendanceLog), UrlProperty = nameof(EmployeeAttendanceLog.FaceImagePath), KeyProperty = nameof(EmployeeAttendanceLog.FaceImageS3Key), Folder = "attendance-faces", Prefix = "attendance-faces" },
                    new() { Entity = typeof(EmployeeAttendanceLog), UrlProperty = nameof(EmployeeAttendanceLog.CheckOutFaceImagePath), KeyProperty = nameof(EmployeeAttendanceLog.CheckOutFaceImageS3Key), Folder = "attendance-checkout-faces", Prefix = "attendance-checkout-faces" },
                },
            },
            new()
            {
                Key = "employee-errors",
                Label = "صور أخطاء الموظفين",
                Note = "الأعمدة تحمل عدة روابط مفصولة بـ || فتُعاد كتابة الروابط بدل عمود مفتاح",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(EmployeeError), UrlProperty = nameof(EmployeeError.ImageUrl), KeyProperty = null, Folder = "uploads/EmployeeErrors", Prefix = "employeeerrors", IsPipeList = true },
                    new() { Entity = typeof(EmployeeErrorEditHistory), UrlProperty = nameof(EmployeeErrorEditHistory.OldImageUrl), KeyProperty = null, Folder = "uploads/EmployeeErrors", Prefix = "employeeerrors", IsPipeList = true },
                    new() { Entity = typeof(EmployeeErrorEditHistory), UrlProperty = nameof(EmployeeErrorEditHistory.NewImageUrl), KeyProperty = null, Folder = "uploads/EmployeeErrors", Prefix = "employeeerrors", IsPipeList = true },
                },
            },
            new()
            {
                Key = "urgent-reports",
                Label = "لقطات البلاغات العاجلة",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(UrgentReport), UrlProperty = nameof(UrgentReport.ScreenshotPath), KeyProperty = nameof(UrgentReport.ScreenshotS3Key), Folder = "uploads/UrgentReports", Prefix = "urgent-reports" },
                },
            },
            new()
            {
                Key = "campaigns",
                Label = "صور الحملات",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(AdvertisingCampaign), UrlProperty = nameof(AdvertisingCampaign.ImageUrl), KeyProperty = nameof(AdvertisingCampaign.ImageS3Key), Folder = "images/campaigns", Prefix = "campaigns" },
                },
            },
            new()
            {
                Key = "password-pages",
                Label = "صور صفحات كلمات المرور",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(StorePasswordPage), UrlProperty = nameof(StorePasswordPage.PageImageUrl), KeyProperty = nameof(StorePasswordPage.PageImageS3Key), Folder = "uploads/password-pages", Prefix = "password-pages" },
                },
            },
            new()
            {
                Key = "employee-tasks",
                Label = "مرفقات مهام الموظفين",
                Columns = new MediaColumnSpec[]
                {
                    new() { Entity = typeof(EmployeeTask), UrlProperty = nameof(EmployeeTask.AttachmentUrl), KeyProperty = nameof(EmployeeTask.AttachmentS3Key), Folder = "uploads/employee-tasks", Prefix = "employee-tasks" },
                    new() { Entity = typeof(EmployeeTask), UrlProperty = nameof(EmployeeTask.AttachmentImagesJson), KeyProperty = null, Folder = "uploads/employee-tasks", Prefix = "employee-tasks", IsJsonArray = true },
                },
            },
        };

        /// <summary>
        /// Prefixes the serving route will redirect for. Anything else is rejected — the route
        /// must not become a general-purpose signer for arbitrary bucket keys.
        /// </summary>
        public static readonly HashSet<string> AllPrefixes = Modules
            .SelectMany(m => m.Columns)
            .Select(c => c.Prefix)
            .ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Prefixes that only some roles may fetch. Everything else is any-authenticated-user —
        /// same audience the wwwroot static files had. These mirror the role guards that already
        /// sit on the modules' own pages, because a mirrored key can be guessable (screen records
        /// are "{userId}/{date}/{date}.webm") and the redirect route must not be a way around them.
        /// </summary>
        public static readonly Dictionary<string, string[]> RestrictedPrefixes = new(StringComparer.Ordinal)
        {
            ["screenrecords"] = new[] { "Admin", "ExecutiveDirector" },
            ["callrecordings"] = new[] { "Admin", "ExecutiveDirector" },
            ["employees-private"] = new[] { "Admin", "ExecutiveDirector" },
            ["attendance-faces"] = new[] { "Admin", "ExecutiveDirector", "Accountant" },
            ["attendance-checkout-faces"] = new[] { "Admin", "ExecutiveDirector", "Accountant" },
        };

        public static MediaModule? Find(string key) =>
            Modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.Ordinal));

        public static string BuildServingUrl(string key) =>
            ServingRoute + "?key=" + Uri.EscapeDataString(key);

        /// <summary>
        /// True for both serving routes — ours and the older order-posts one. Tolerates a missing
        /// leading slash: URL columns in this codebase mix "/images/x.jpg" and "images/x.jpg"
        /// conventions (FileUploadService always returned slashless), and a rewrite preserves
        /// whichever form the row had so its views keep working.
        /// </summary>
        public static bool IsServingUrl(string? url)
        {
            if (url is null)
                return false;

            var trimmed = url.TrimStart('/');

            return trimmed.StartsWith("Media/File", StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith("OrderPosts/Image", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Serving URL in the same leading-slash convention as the value it replaces. Views
        /// compensate for their column's convention (prepending "/" or "~/" to slashless values),
        /// so a rewrite that changes the convention would break exactly the views that work today.
        /// </summary>
        public static string BuildServingUrlLike(string key, string? previousValue)
        {
            var url = BuildServingUrl(key);

            return previousValue is not null && !previousValue.StartsWith('/')
                ? url[1..]
                : url;
        }

        /// <summary>The S3 key inside a serving-route URL, or null if the URL is not one.</summary>
        public static string? TryExtractKey(string? url)
        {
            if (!IsServingUrl(url))
                return null;

            var marker = url!.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                return null;

            var value = url[(marker + 4)..];
            var end = value.IndexOf('&');
            if (end >= 0)
                value = value[..end];

            value = Uri.UnescapeDataString(value);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Path-mirrored key for a file under wwwroot. Mirroring — instead of a generated Guid —
        /// is what makes the whole registry flow reversible and idempotent: the original path is
        /// derivable back from the key (<see cref="DeriveRelativePath"/>), and two columns
        /// referencing the same file derive the same key, so the file is stored once.
        /// </summary>
        public static string DeriveKey(MediaColumnSpec spec, string wwwrootRelativePath)
        {
            var rel = wwwrootRelativePath.Replace('\\', '/').TrimStart('/');

            return rel.StartsWith(spec.Folder + "/", StringComparison.OrdinalIgnoreCase)
                ? spec.Prefix + "/" + rel[(spec.Folder.Length + 1)..]
                : spec.Prefix + "/_outside/" + rel;
        }

        /// <summary>Inverse of <see cref="DeriveKey"/>: wwwroot-relative path for a mirrored key.
        /// Returns null for keys that are not mirrors (Guid keys from direct uploads).</summary>
        public static string? DeriveRelativePath(MediaColumnSpec spec, string key)
        {
            if (!key.StartsWith(spec.Prefix + "/", StringComparison.Ordinal))
                return null;

            var tail = key[(spec.Prefix.Length + 1)..];

            return tail.StartsWith("_outside/", StringComparison.Ordinal)
                ? tail["_outside/".Length..]
                : spec.Folder + "/" + tail;
        }

        /// <summary>
        /// S3 prefix for a FileUploadService subdirectory — the registry's prefix when the folder
        /// is a known module folder, otherwise a sanitized form of the folder name itself, so an
        /// upload to a brand-new folder still lands under a sensible prefix instead of "misc".
        /// </summary>
        public static string PrefixForFolder(string subDirectory)
        {
            var normalized = (subDirectory ?? string.Empty).Replace('\\', '/').Trim('/');

            var spec = Modules
                .SelectMany(m => m.Columns)
                .FirstOrDefault(c => string.Equals(c.Folder, normalized, StringComparison.OrdinalIgnoreCase));

            if (spec is not null)
                return spec.Prefix;

            var cleaned = new string(normalized
                .ToLowerInvariant()
                .Replace('/', '-')
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                .ToArray())
                .Trim('-');

            return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
        }
    }
