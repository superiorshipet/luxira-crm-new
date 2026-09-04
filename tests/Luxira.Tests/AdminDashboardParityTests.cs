using System.Reflection;
using Luxira.Api.Features.Operations.Controllers;
using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Tests;

public sealed class AdminDashboardParityTests
{
    [Theory]
    [InlineData("+964 770-123-4567", "07701234567")]
    [InlineData("00974 5512 3456", "55123456")]
    [InlineData("٠٩١-٢٣٤ ٥٦٧٨", "0912345678")]
    [InlineData("⁦+218 91 234 5678⁩", "0912345678")]
    public void PotentialOrderPhoneNormalizationMatchesLegacy(string raw, string expected)
    {
        var method = typeof(AdminDashboardController).GetMethod(
            "NormalizePhone",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(expected, method!.Invoke(null, [raw]));
    }

    [Fact]
    public void AdminDashboardRemainsAdminOnly()
    {
        var authorize = typeof(AdminDashboardController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal("Admin,Administrator", authorize?.Roles);
    }

    [Fact]
    public async Task DryRunReportsWithoutWriting()
    {
        await using var context = CreateContext();
        context.PotentialOrders.Add(new PotentialOrder { Id = 1, PhoneNumber = "+964 770-123-4567", StoreName = "Store", ApplicationUserId = "user" });
        await context.SaveChangesAsync();
        var controller = new AdminDashboardController(context);

        await controller.DryRunPoPhoneNormalization(default);

        Assert.Equal("+964 770-123-4567", (await context.PotentialOrders.FindAsync(1))!.PhoneNumber);
    }

    [Fact]
    public async Task ApplyNormalizesPotentialOrdersIdempotently()
    {
        await using var context = CreateContext();
        context.PotentialOrders.Add(new PotentialOrder { Id = 1, PhoneNumber = "+964 770-123-4567", StoreName = "Store", ApplicationUserId = "user" });
        await context.SaveChangesAsync();
        var controller = new AdminDashboardController(context);

        await controller.ApplyPoPhoneNormalization(default);
        await controller.ApplyPoPhoneNormalization(default);

        Assert.Equal("07701234567", (await context.PotentialOrders.FindAsync(1))!.PhoneNumber);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-dashboard-{Guid.NewGuid():N}")
            .Options);
}
