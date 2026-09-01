using System.Reflection;
using Luxira.Api.Core;

namespace Luxira.Api.IntegrationTests;

public sealed class ArchitectureConventionTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void AllFeatureModulesImplementIModule()
    {
        var modules = ApiAssembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Module", StringComparison.Ordinal) && !t.IsInterface && !t.IsAbstract)
            .ToArray();

        Assert.NotEmpty(modules);
        Assert.All(modules, module =>
        {
            Assert.True(typeof(IModule).IsAssignableFrom(module), $"Module {module.FullName} must implement IModule.");
            Assert.True(module.Namespace?.StartsWith("Luxira.Api.Features.", StringComparison.Ordinal) == true,
                $"Module {module.FullName} must reside in Luxira.Api.Features.* namespace.");
        });
    }

    [Fact]
    public void ControllersStayInsideFeatureFolders()
    {
        var controllers = ApiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(controllers);
        Assert.All(controllers, controller =>
        {
            Assert.True(controller.Namespace?.StartsWith("Luxira.Api.Features.", StringComparison.Ordinal) == true,
                $"Controller {controller.FullName} must reside in Luxira.Api.Features.*");
        });
    }

    [Fact]
    public void FeatureTypesDoNotCreateGenericDumpingGrounds()
    {
        var forbiddenSegments = new[] { ".Common.Services", ".Common.Helpers" };
        var violations = ApiAssembly
            .GetTypes()
            .Where(type => type.Namespace is not null)
            .Where(type => forbiddenSegments.Any(segment =>
                type.Namespace!.Contains(segment, StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(violations);
    }
}
