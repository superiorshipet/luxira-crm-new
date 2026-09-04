using Luxira.Api.Features.DeliveryCompanies.Controllers;
using Luxira.Api.Features.Employees.Controllers;
using Luxira.Api.Features.Expenses.Controllers;
using Luxira.Api.Features.ManufacturingCompanies.Controllers;
using Luxira.Api.Features.Marketing.Controllers;
using Luxira.Api.Features.Operations.Controllers;
using Luxira.Api.Features.Orders.Controllers;
using Luxira.Api.Features.Warehouses.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Luxira.Tests;

public sealed class LegacyAuthorizationParityTests
{
    [Theory]
    [InlineData(typeof(AdminDashboardController), "Admin,Administrator")]
    [InlineData(typeof(PotentialOrderController), "Admin,Administrator")]
    [InlineData(typeof(OrderPostsDuplicateDeductionController), "Admin,Administrator,ExecutiveDirector")]
    [InlineData(typeof(OperationsCenterController), "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [InlineData(typeof(MainWareHouseController), "Admin,Administrator,ExecutiveDirector")]
    [InlineData(typeof(PendingDownloadReminderController), "CallCenter,FollowUpDepartment")]
    [InlineData(typeof(EmployeeErrorsController), "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    [InlineData(typeof(SalesIndicatorsController), "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [InlineData(typeof(OrderBonusConfigurationController), "Admin,Administrator")]
    [InlineData(typeof(S3DashboardController), "Admin,Administrator")]
    public void ControllerRolesPreserveLegacyAccess(Type controller, string expectedRoles)
    {
        Assert.Equal(expectedRoles, controller.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles);
    }

    [Theory]
    [InlineData(typeof(ExchangeRateController), nameof(ExchangeRateController.Index), "Admin,Administrator,Accountant,Observer,DeliveryCompany,ExecutiveDirector,DeliveryRepresentative")]
    [InlineData(typeof(FinancialController), nameof(FinancialController.Countries), "Admin,Administrator")]
    [InlineData(typeof(RatingController), nameof(RatingController.StoreList), "Admin,Administrator,ExecutiveDirector")]
    [InlineData(typeof(DeliveryCompanyController), nameof(DeliveryCompanyController.GetCompanies), "Admin,Administrator,Accountant,Observer,ExecutiveDirector,FollowUpDepartment")]
    [InlineData(typeof(DeliveryRepresentativeController), nameof(DeliveryRepresentativeController.GetRepresentatives), "Admin,Administrator,Accountant,Observer,ExecutiveDirector,FollowUpDepartment")]
    public void ActionRolesPreserveLegacyAccess(Type controller, string action, string expectedRoles)
    {
        var methods = controller.GetMethods().Where(method => method.Name == action).ToList();
        Assert.NotEmpty(methods);
        Assert.All(methods, method => Assert.Contains(
            method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(),
            authorize => authorize.Roles == expectedRoles));
    }
}
