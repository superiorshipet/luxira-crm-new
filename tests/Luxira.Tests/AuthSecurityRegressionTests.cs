using System.Security.Claims;
using Luxira.Api.Features.Auth;
using Luxira.Api.Features.Auth.Controllers;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Features.Employees.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Luxira.Tests;

public sealed class AuthSecurityRegressionTests
{
    [Theory]
    [InlineData("abc12", false)]
    [InlineData("abcdef", false)]
    [InlineData("abcde1", true)]
    public void Password_policy_matches_legacy_requirements(string password, bool expected) =>
        Assert.Equal(expected, LuxiraPasswordPolicy.IsValid(password));

    [Fact]
    public void Admin_claims_do_not_impersonate_executive_director_role()
    {
        var user = UserWithRole("Admin");
        var claims = CreateJwtService().CreateClaims(user);

        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == "Admin");
        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == "Administrator");
        Assert.DoesNotContain(claims, claim => claim.Type == "role" && claim.Value == "ExecutiveDirector");
        Assert.Contains(claims, claim => claim.Type == LuxiraClaimTypes.SecurityStamp && claim.Value == user.SecurityStamp);
    }

    [Fact]
    public void Executive_director_keeps_own_role()
    {
        var claims = CreateJwtService().CreateClaims(UserWithRole("ExecutiveDirector"));
        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == "ExecutiveDirector");
        Assert.DoesNotContain(claims, claim => claim.Type == "role" && claim.Value == "Admin");
    }

    [Theory]
    [InlineData(nameof(ScreenRecordsController.GetRecords))]
    [InlineData(nameof(ScreenRecordsController.UploadRecord))]
    public void Raw_screen_record_endpoints_require_management_roles(string methodName)
    {
        var method = typeof(ScreenRecordsController).GetMethod(methodName)!;
        var attribute = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin,Administrator,ExecutiveDirector", attribute.Roles);
    }

    [Fact]
    public void Password_reset_page_supports_current_and_legacy_routes()
    {
        var action = typeof(PasswordResetController).GetMethod(nameof(PasswordResetController.ResetPasswordPage))!;
        var routes = action.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Select(item => item.Template);
        Assert.Contains("/reset-password", routes);
        Assert.Contains("/Account/ResetPassword", routes);
    }

    private static ApplicationUser UserWithRole(string role) => new()
    {
        Id = "user-1",
        UserName = "user",
        SecurityStamp = "stamp-1",
        UserRoles =
        [
            new ApplicationUserRole
            {
                RoleId = "role-1",
                Role = new ApplicationRole { Id = "role-1", Name = role },
            },
        ],
    };

    private static JwtService CreateJwtService()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:Key"] = "12345678901234567890123456789012",
        }).Build();
        return new JwtService(JwtSigningMaterial.Create(configuration, new TestEnvironment()));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Luxira.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
