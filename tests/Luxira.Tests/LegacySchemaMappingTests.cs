using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Features.Employees.Models;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Tests;

public sealed class LegacySchemaMappingTests
{
    [Fact]
    public void PasswordVault_MapsLegacyTablesAndAuditRelationship()
    {
        using var context = CreateContext();
        var item = context.Model.FindEntityType(typeof(PasswordEmail));
        var history = context.Model.FindEntityType(typeof(PasswordEmailHistory));

        Assert.Equal("PasswordEmails", item?.GetTableName());
        Assert.Equal("PasswordEmailHistories", history?.GetTableName());
        Assert.NotNull(item?.FindNavigation(nameof(PasswordEmail.Histories)));
        Assert.NotNull(history?.FindNavigation(nameof(PasswordEmailHistory.PasswordEmail)));
    }

    [Fact]
    public void EmployeeTasks_MapAssignmentsInsteadOfInventedCompletionColumns()
    {
        using var context = CreateContext();
        var task = context.Model.FindEntityType(typeof(EmployeeTask));
        var assignment = context.Model.FindEntityType(typeof(EmployeeTaskAssignment));

        Assert.Equal("EmployeeTasks", task?.GetTableName());
        Assert.Equal("EmployeeTaskAssignments", assignment?.GetTableName());
        Assert.Null(task?.FindProperty("EmployeeId"));
        Assert.Null(task?.FindProperty("IsCompleted"));
        Assert.Null(task?.FindProperty("DueDate"));
        Assert.NotNull(task?.FindNavigation(nameof(EmployeeTask.Assignments)));
    }

    [Fact]
    public void EmployeeFacePrint_MapsPersistedLegacyColumns()
    {
        using var context = CreateContext();
        var employee = context.Model.FindEntityType(typeof(Employee));

        Assert.NotNull(employee?.FindProperty(nameof(Employee.FaceDescriptor)));
        Assert.NotNull(employee?.FindProperty(nameof(Employee.HasFacePrint)));
    }

    [Fact]
    public void SalaryPayment_MapsLegacyPayrollAndSoftDeleteColumns()
    {
        using var context = CreateContext();
        var payment = context.Model.FindEntityType(typeof(EmployeeSalaryPayment));

        Assert.Equal("EmployeeSalaryPayments", payment?.GetTableName());
        Assert.NotNull(payment?.FindProperty(nameof(EmployeeSalaryPayment.SalaryMonth)));
        Assert.NotNull(payment?.FindProperty(nameof(EmployeeSalaryPayment.RemainingAmount)));
        Assert.NotNull(payment?.FindProperty(nameof(EmployeeSalaryPayment.IsDeleted)));
        Assert.NotNull(payment?.FindProperty(nameof(EmployeeSalaryPayment.IsPermanentlyDeleted)));
        Assert.Null(payment?.FindProperty("Amount"));
        Assert.Null(payment?.FindProperty("PaymentDate"));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"legacy-schema-tests-{Guid.NewGuid():N}")
            .Options);
}
