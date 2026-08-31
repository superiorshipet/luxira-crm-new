using System.Reflection;

namespace Luxira.Api.IntegrationTests;

public sealed class ArchitectureConventionTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

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
