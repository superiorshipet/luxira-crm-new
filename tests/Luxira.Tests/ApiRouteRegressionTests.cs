using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class ApiRouteRegressionTests
{
    [Fact]
    public void CanonicalControllerRoutes_AreUniquePerHttpMethod()
    {
        var routes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => GetCanonicalRoutes(type))
            .ToList();

        var duplicates = routes
            .GroupBy(route => $"{route.HttpMethod} {route.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(route => route.Action).Distinct().Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(route => route.Action).Distinct())}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"Duplicate canonical routes:{Environment.NewLine}{string.Join(Environment.NewLine, duplicates)}");
    }

    private static IEnumerable<ApiRoute> GetCanonicalRoutes(Type controllerType)
    {
        var controllerRoutes = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .ToList();

        foreach (var method in controllerType.GetMethods())
        {
            foreach (var attribute in method
                .GetCustomAttributes(inherit: true)
                .OfType<HttpMethodAttribute>())
            {
                foreach (var httpMethod in attribute.HttpMethods)
                {
                    foreach (var template in ExpandTemplates(
                        controllerType,
                        method.Name,
                        controllerRoutes,
                        attribute.Template))
                    {
                        if (template.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new ApiRoute(
                                httpMethod,
                                template,
                                $"{controllerType.Name}.{method.Name}");
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<string> ExpandTemplates(
        Type controllerType,
        string actionName,
        IReadOnlyCollection<string> controllerRoutes,
        string? actionTemplate)
    {
        if (actionTemplate?.StartsWith('/') == true)
        {
            yield return NormalizeTemplate(controllerType, actionName, actionTemplate);
            yield break;
        }

        foreach (var controllerRoute in controllerRoutes)
        {
            var combined = string.IsNullOrWhiteSpace(actionTemplate)
                ? controllerRoute
                : $"{controllerRoute.TrimEnd('/')}/{actionTemplate.TrimStart('/')}";
            yield return NormalizeTemplate(controllerType, actionName, combined);
        }
    }

    private static string NormalizeTemplate(
        Type controllerType,
        string actionName,
        string template)
    {
        const string controllerSuffix = "Controller";
        var controllerName = controllerType.Name.EndsWith(
            controllerSuffix,
            StringComparison.Ordinal)
                ? controllerType.Name[..^controllerSuffix.Length]
                : controllerType.Name;

        return template
            .Trim('/')
            .Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ApiRoute(string HttpMethod, string Template, string Action);
}
