using System.Reflection;
using Luxira.Application;
using Luxira.Infrastructure;

namespace Luxira.Api.IntegrationTests;

public sealed class ArchitectureConventionTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly ApplicationAssembly =
        typeof(ApplicationExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly =
        typeof(InfrastructureExtensions).Assembly;

    [Fact]
    public void EndpointTypesStayInsideFeatureFolders()
    {
        var misplacedEndpoints = ApiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Endpoints", StringComparison.Ordinal))
            .Where(type => type.Namespace is null ||
                !type.Namespace.StartsWith(
                    "Luxira.Api.Features.",
                    StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(misplacedEndpoints);
    }

    [Fact]
    public void ControllersStayInsideFeatureFolders()
    {
        var misplacedControllers = ApiAssembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => type.Namespace is null ||
                !type.Namespace.StartsWith(
                    "Luxira.Api.Features.",
                    StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(misplacedControllers);
    }

    [Fact]
    public void DependencyDirectionKeepsApplicationIndependent()
    {
        var applicationReferences = ApplicationAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();
        var infrastructureReferences = InfrastructureAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Luxira.Api", applicationReferences);
        Assert.DoesNotContain("Luxira.Infrastructure", applicationReferences);
        Assert.DoesNotContain("Luxira.Api", infrastructureReferences);
        Assert.Contains("Luxira.Application", infrastructureReferences);
    }

    [Fact]
    public void DeliveryUseCasesHaveExplicitRepositoryPorts()
    {
        var deliveryServices = ApplicationAssembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Luxira.Application.Features.DeliveryCompanies.",
                StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(deliveryServices);
        Assert.All(deliveryServices, service =>
        {
            var constructor = Assert.Single(service.GetConstructors());
            Assert.Contains(constructor.GetParameters(), parameter =>
                parameter.ParameterType.IsInterface &&
                parameter.ParameterType.Name.EndsWith(
                    "Repository",
                    StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ApplicationTypesDoNotCreateGenericDumpingGroundNamespaces()
    {
        var forbiddenSegments = new[] { ".Common", ".Helpers", ".Services" };
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
