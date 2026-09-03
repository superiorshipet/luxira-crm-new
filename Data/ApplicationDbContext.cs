using Microsoft.EntityFrameworkCore;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.Orders.Models;
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
    public DbSet<OrderEditHistory> OrderEditHistories => Set<OrderEditHistory>();
    public DbSet<OrderWarehouse> OrderWarehouses => Set<OrderWarehouse>();
    public DbSet<OrderReport> OrderReports => Set<OrderReport>();
    public DbSet<OrderReportOrder> OrderReportOrders => Set<OrderReportOrder>();
    public DbSet<OrderBonusConfiguration> OrderBonusConfigurations => Set<OrderBonusConfiguration>();
    public DbSet<OrderPost> OrderPosts => Set<OrderPost>();
    public DbSet<OrderPostImage> OrderPostImages => Set<OrderPostImage>();
    public DbSet<OrderMetaActionClick> OrderMetaActionClicks => Set<OrderMetaActionClick>();
    public DbSet<OrderFollowUpRequest> OrderFollowUpRequests => Set<OrderFollowUpRequest>();
    public DbSet<PotentialOrder> PotentialOrders => Set<PotentialOrder>();
    public DbSet<UrgentReport> UrgentReports => Set<UrgentReport>();

    // Employees & HR
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeAttendanceLog> EmployeeAttendanceLogs => Set<EmployeeAttendanceLog>();
    public DbSet<EmployeeWorkShift> EmployeeWorkShifts => Set<EmployeeWorkShift>();
    public DbSet<EmployeeActivityLog> EmployeeActivityLogs => Set<EmployeeActivityLog>();
    public DbSet<EmployeeSalaryPayment> EmployeeSalaryPayments => Set<EmployeeSalaryPayment>();
    public DbSet<EmployeeBonusRate> EmployeeBonusRates => Set<EmployeeBonusRate>();
    public DbSet<EmployeeBonusPayment> EmployeeBonusPayments => Set<EmployeeBonusPayment>();
    public DbSet<EmployeeTask> EmployeeTasks => Set<EmployeeTask>();
    public DbSet<EmployeeTaskAssignment> EmployeeTaskAssignments => Set<EmployeeTaskAssignment>();
    public DbSet<EmployeeError> EmployeeErrors => Set<EmployeeError>();
    public DbSet<EmployeeErrorEditHistory> EmployeeErrorEditHistories => Set<EmployeeErrorEditHistory>();
    public DbSet<EmployeeTransaction> EmployeeTransactions => Set<EmployeeTransaction>();
    public DbSet<EmployeeViolation> EmployeeViolations => Set<EmployeeViolation>();
    public DbSet<EmployeeRating> EmployeeRatings => Set<EmployeeRating>();
    public DbSet<PersonalNote> PersonalNotes => Set<PersonalNote>();
    public DbSet<ManagementRequest> ManagementRequests => Set<ManagementRequest>();
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
    public DbSet<MarketingLead> MarketingLeads => Set<MarketingLead>();
    public DbSet<StoreScript> StoreScripts => Set<StoreScript>();
    public DbSet<WebsiteDomain> WebsiteDomains => Set<WebsiteDomain>();
    public DbSet<VideoLink> VideoLinks => Set<VideoLink>();

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
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<ConferenceMeeting> ConferenceMeetings => Set<ConferenceMeeting>();
    public DbSet<PasswordEmail> PasswordEmails => Set<PasswordEmail>();
    public DbSet<PasswordEmailHistory> PasswordEmailHistories => Set<PasswordEmailHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
