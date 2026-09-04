using Microsoft.EntityFrameworkCore;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Operations.Models;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Features.SearchKeywords.Models;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Features.Communication.Models;

namespace Luxira.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Auth & Users
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
    public DbSet<ApplicationUserRole> UserRoles => Set<ApplicationUserRole>();
    public DbSet<UserSwitchGroup> UserSwitchGroups => Set<UserSwitchGroup>();
    public DbSet<UserSwitchGroupMember> UserSwitchGroupMembers => Set<UserSwitchGroupMember>();

    // Delivery & Couriers
    public DbSet<DeliveryCompany> DeliveryCompanies => Set<DeliveryCompany>();
    public DbSet<DeliveryCompanyPrice> DeliveryCompanyPrices => Set<DeliveryCompanyPrice>();
    public DbSet<StoreDeliveryCompanyAssignment> StoreDeliveryCompanyAssignments => Set<StoreDeliveryCompanyAssignment>();
    public DbSet<CamexCity> CamexCities => Set<CamexCity>();
    public DbSet<CamexCityMapping> CamexCityMappings => Set<CamexCityMapping>();
    public DbSet<CamexStoreMapping> CamexStoreMappings => Set<CamexStoreMapping>();

    // Orders & Operations
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<StatusUpdateBatchLog> StatusUpdateBatchLogs => Set<StatusUpdateBatchLog>();
    public DbSet<StatusUpdateBatchLogItem> StatusUpdateBatchLogItems => Set<StatusUpdateBatchLogItem>();
    public DbSet<AppLog> AppLogs => Set<AppLog>();
    public DbSet<AppMetric> AppMetrics => Set<AppMetric>();
    public DbSet<OrderStatusHistoryDeliveryCompanySnapshot> OrderStatusHistoryDeliveryCompanySnapshots => Set<OrderStatusHistoryDeliveryCompanySnapshot>();
    public DbSet<OrderEditHistory> OrderEditHistories => Set<OrderEditHistory>();
    public DbSet<OrderWarehouse> OrderWarehouses => Set<OrderWarehouse>();
    public DbSet<OrderReport> OrderReports => Set<OrderReport>();
    public DbSet<OrderReportOrder> OrderReportOrders => Set<OrderReportOrder>();
    public DbSet<OrderBonusConfiguration> OrderBonusConfigurations => Set<OrderBonusConfiguration>();
    public DbSet<OrderPost> OrderPosts => Set<OrderPost>();
    public DbSet<OrderPostImage> OrderPostImages => Set<OrderPostImage>();
    public DbSet<OrderPostDeletedHistory> OrderPostDeletedHistories => Set<OrderPostDeletedHistory>();
    public DbSet<OrderPostEmployeeDeduction> OrderPostEmployeeDeductions => Set<OrderPostEmployeeDeduction>();
    public DbSet<OrderMetaActionClick> OrderMetaActionClicks => Set<OrderMetaActionClick>();
    public DbSet<OrderFollowUpRequest> OrderFollowUpRequests => Set<OrderFollowUpRequest>();
    public DbSet<OrderDetailsFieldAuditLog> OrderDetailsFieldAuditLogs => Set<OrderDetailsFieldAuditLog>();
    public DbSet<OrderContentViewLog> OrderContentViewLogs => Set<OrderContentViewLog>();
    public DbSet<OrderContentViewReadState> OrderContentViewReadStates => Set<OrderContentViewReadState>();
    public DbSet<OrderPackagingAchievementRun> OrderPackagingAchievementRuns => Set<OrderPackagingAchievementRun>();
    public DbSet<OrderPackagingAchievementNotification> OrderPackagingAchievementNotifications => Set<OrderPackagingAchievementNotification>();
    public DbSet<ScheduledSendRequest> ScheduledSendRequests => Set<ScheduledSendRequest>();
    public DbSet<PotentialOrder> PotentialOrders => Set<PotentialOrder>();
    public DbSet<UrgentReport> UrgentReports => Set<UrgentReport>();

    // Employees & HR
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<DevelopmentTaskCategoryAssignmentRule> DevelopmentTaskCategoryAssignmentRules => Set<DevelopmentTaskCategoryAssignmentRule>();
    public DbSet<EmployeeAttendanceLog> EmployeeAttendanceLogs => Set<EmployeeAttendanceLog>();
    public DbSet<EmployeeWorkShift> EmployeeWorkShifts => Set<EmployeeWorkShift>();
    public DbSet<EmployeeActivityLog> EmployeeActivityLogs => Set<EmployeeActivityLog>();
    public DbSet<EmployeeSalaryPayment> EmployeeSalaryPayments => Set<EmployeeSalaryPayment>();
    public DbSet<EmployeeBonusRate> EmployeeBonusRates => Set<EmployeeBonusRate>();
    public DbSet<EmployeeBonusPayment> EmployeeBonusPayments => Set<EmployeeBonusPayment>();
    public DbSet<EmployeeTask> EmployeeTasks => Set<EmployeeTask>();
    public DbSet<EmployeeTaskAssignment> EmployeeTaskAssignments => Set<EmployeeTaskAssignment>();
    public DbSet<SystemDevelopmentTask> SystemDevelopmentTasks => Set<SystemDevelopmentTask>();
    public DbSet<SystemDevelopmentTaskImage> SystemDevelopmentTaskImages => Set<SystemDevelopmentTaskImage>();
    public DbSet<SystemDevelopmentTaskAuditLog> SystemDevelopmentTaskAuditLogs => Set<SystemDevelopmentTaskAuditLog>();
    public DbSet<DevelopmentTaskAssignment> DevelopmentTaskAssignments => Set<DevelopmentTaskAssignment>();
    public DbSet<DevelopmentTaskComment> DevelopmentTaskComments => Set<DevelopmentTaskComment>();
    public DbSet<MarketingWorkReport> MarketingWorkReports => Set<MarketingWorkReport>();
    public DbSet<DevelopmentTaskReviewSubmission> DevelopmentTaskReviewSubmissions => Set<DevelopmentTaskReviewSubmission>();
    public DbSet<DevelopmentTaskReviewFile> DevelopmentTaskReviewFiles => Set<DevelopmentTaskReviewFile>();
    public DbSet<EmployeeError> EmployeeErrors => Set<EmployeeError>();
    public DbSet<EmployeeErrorEditHistory> EmployeeErrorEditHistories => Set<EmployeeErrorEditHistory>();
    public DbSet<EmployeeTransaction> EmployeeTransactions => Set<EmployeeTransaction>();
    public DbSet<EmployeeViolation> EmployeeViolations => Set<EmployeeViolation>();
    public DbSet<EmployeeRating> EmployeeRatings => Set<EmployeeRating>();
    public DbSet<PersonalNote> PersonalNotes => Set<PersonalNote>();
    public DbSet<PersonalNoteHistory> PersonalNoteHistories => Set<PersonalNoteHistory>();
    public DbSet<IdeaSuggestion> IdeaSuggestions => Set<IdeaSuggestion>();
    public DbSet<ManagementRequest> ManagementRequests => Set<ManagementRequest>();
    public DbSet<ManagementRequestNotification> ManagementRequestNotifications => Set<ManagementRequestNotification>();
    public DbSet<ScreenRecord> ScreenRecords => Set<ScreenRecord>();

    // Finance & Expenses
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<SalesIndicator> SalesIndicators => Set<SalesIndicator>();

    // Warehouses & Inventory
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<MainWarehouse> MainWarehouses => Set<MainWarehouse>();
    public DbSet<SubWarehouse> SubWarehouses => Set<SubWarehouse>();
    public DbSet<ManufacturingCompanyMainWarehouse> ManufacturingCompanyMainWarehouses => Set<ManufacturingCompanyMainWarehouse>();
    public DbSet<WarehouseEditHistory> WarehouseEditHistories => Set<WarehouseEditHistory>();

    // Manufacturing & Products
    public DbSet<ManufacturingCompany> ManufacturingCompanies => Set<ManufacturingCompany>();
    public DbSet<MainProduct> MainProducts => Set<MainProduct>();
    public DbSet<ProductPriceEditHistory> ProductPriceEditHistories => Set<ProductPriceEditHistory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductImageDraft> ProductImageDrafts => Set<ProductImageDraft>();
    public DbSet<ProductImageUserPin> ProductImageUserPins => Set<ProductImageUserPin>();
    public DbSet<EmployeeManufacturingCompany> EmployeeManufacturingCompanies => Set<EmployeeManufacturingCompany>();
    public DbSet<ProductMinimumSellingPrice> ProductMinimumSellingPrices => Set<ProductMinimumSellingPrice>();
    public DbSet<CountryMinimumPrice> CountryMinimumPrices => Set<CountryMinimumPrice>();
    public DbSet<StoreCodeFolder> StoreCodeFolders => Set<StoreCodeFolder>();
    public DbSet<StoreCodeStoreGroup> StoreCodeStoreGroups => Set<StoreCodeStoreGroup>();
    public DbSet<StoreCodeEditHistory> StoreCodeEditHistories => Set<StoreCodeEditHistory>();

    // Search Keywords
    public DbSet<SearchKeywordOption> SearchKeywordOptions => Set<SearchKeywordOption>();

    // Media & Storage
    public DbSet<S3StoredObject> S3StoredObjects => Set<S3StoredObject>();

    // Marketing & Advertising
    public DbSet<AdvertisingCampaign> AdvertisingCampaigns => Set<AdvertisingCampaign>();
    public DbSet<AdvertisingManagerStoreFolder> AdvertisingManagerStoreFolders => Set<AdvertisingManagerStoreFolder>();
    public DbSet<AdvertisingManagerItem> AdvertisingManagerItems => Set<AdvertisingManagerItem>();
    public DbSet<AdvertisingManagerItemAccount> AdvertisingManagerItemAccounts => Set<AdvertisingManagerItemAccount>();
    public DbSet<AdvertisingManagerAccountProfile> AdvertisingManagerAccountProfiles => Set<AdvertisingManagerAccountProfile>();
    public DbSet<AdvertisingManagerAccountLink> AdvertisingManagerAccountLinks => Set<AdvertisingManagerAccountLink>();
    public DbSet<AdvertisingManagerPaymentCard> AdvertisingManagerPaymentCards => Set<AdvertisingManagerPaymentCard>();
    public DbSet<MarketingLead> MarketingLeads => Set<MarketingLead>();
    public DbSet<StoreScript> StoreScripts => Set<StoreScript>();
    public DbSet<SeedScriptSetting> SeedScriptSettings => Set<SeedScriptSetting>();
    public DbSet<ScriptTarget> ScriptTargets => Set<ScriptTarget>();
    public DbSet<ScriptThemeToken> ScriptThemeTokens => Set<ScriptThemeToken>();
    public DbSet<ScriptSetting> ScriptSettings => Set<ScriptSetting>();
    public DbSet<ScriptCountry> ScriptCountries => Set<ScriptCountry>();
    public DbSet<ScriptCountryValue> ScriptCountryValues => Set<ScriptCountryValue>();
    public DbSet<ScriptCategory> ScriptCategories => Set<ScriptCategory>();
    public DbSet<ScriptSubCategory> ScriptSubCategories => Set<ScriptSubCategory>();
    public DbSet<ScriptMessage> ScriptMessages => Set<ScriptMessage>();
    public DbSet<ScriptTranslation> ScriptTranslations => Set<ScriptTranslation>();
    public DbSet<ScriptEditHistory> ScriptEditHistories => Set<ScriptEditHistory>();
    public DbSet<WebsiteDomain> WebsiteDomains => Set<WebsiteDomain>();
    public DbSet<WebsiteDomainEditLog> WebsiteDomainEditLogs => Set<WebsiteDomainEditLog>();
    public DbSet<VideoLink> VideoLinks => Set<VideoLink>();
    public DbSet<VideoLinkChangeHistory> VideoLinkChangeHistories => Set<VideoLinkChangeHistory>();

    // Communication & Messages
    public DbSet<HelpCenterChatMessage> HelpCenterChatMessages => Set<HelpCenterChatMessage>();
    public DbSet<HelpCenterChatReadState> HelpCenterChatReadStates => Set<HelpCenterChatReadState>();
    public DbSet<HelpCenterChatMessageEdit> HelpCenterChatMessageEdits => Set<HelpCenterChatMessageEdit>();
    public DbSet<HelpCenterChatReaction> HelpCenterChatReactions => Set<HelpCenterChatReaction>();
    public DbSet<HelpCenterChatPin> HelpCenterChatPins => Set<HelpCenterChatPin>();
    public DbSet<HelpCenterChatMessageRead> HelpCenterChatMessageReads => Set<HelpCenterChatMessageRead>();
    public DbSet<HelpCenterChatMention> HelpCenterChatMentions => Set<HelpCenterChatMention>();
    public DbSet<HelpCenterChatSetting> HelpCenterChatSettings => Set<HelpCenterChatSetting>();
    public DbSet<HelpCenterChatMessageOrderLink> HelpCenterChatMessageOrderLinks => Set<HelpCenterChatMessageOrderLink>();
    public DbSet<HelpCenterChatMessageHiddenForUser> HelpCenterChatMessageHiddenForUsers => Set<HelpCenterChatMessageHiddenForUser>();
    public DbSet<HelpCenterChatUserPresence> HelpCenterChatUserPresence => Set<HelpCenterChatUserPresence>();
    public DbSet<HelpCenterChatKeyword> HelpCenterChatKeywords => Set<HelpCenterChatKeyword>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<WhatsAppAutomationAccount> WhatsAppAutomationAccounts => Set<WhatsAppAutomationAccount>();
    public DbSet<WhatsAppAutomationAccountStore> WhatsAppAutomationAccountStores => Set<WhatsAppAutomationAccountStore>();
    public DbSet<WhatsAppAutomationTemplate> WhatsAppAutomationTemplates => Set<WhatsAppAutomationTemplate>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<AdminNotificationReplyState> AdminNotificationReplyStates => Set<AdminNotificationReplyState>();
    public DbSet<SystemEmailLog> SystemEmailLogs => Set<SystemEmailLog>();
    public DbSet<ConferenceMeeting> ConferenceMeetings => Set<ConferenceMeeting>();
    public DbSet<CallRecording> CallRecordings => Set<CallRecording>();
    public DbSet<PasswordEmail> PasswordEmails => Set<PasswordEmail>();
    public DbSet<PasswordEmailHistory> PasswordEmailHistories => Set<PasswordEmailHistory>();
    public DbSet<PasswordPageType> PasswordPageTypes => Set<PasswordPageType>();
    public DbSet<StorePasswordPage> StorePasswordPages => Set<StorePasswordPage>();
    public DbSet<PasswordPageChangeLog> PasswordPageChangeLogs => Set<PasswordPageChangeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
