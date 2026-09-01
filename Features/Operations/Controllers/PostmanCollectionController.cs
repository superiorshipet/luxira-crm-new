using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/postman")]
public class PostmanCollectionController : ControllerBase
{
    private readonly EndpointDataSource _endpointDataSource;

    public PostmanCollectionController(EndpointDataSource endpointDataSource)
    {
        _endpointDataSource = endpointDataSource;
    }

    [HttpGet("collection.json")]
    [HttpGet("/postman/collection.json")]
    [HttpGet("/ApiCollectionJson/GetPostmanCollection")]
    public IActionResult GetPostmanCollection()
    {
        var host = Request.Host.Value;
        var scheme = Request.Scheme;
        var baseUrl = $"{scheme}://{host}";

        var endpoints = _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => !string.IsNullOrEmpty(e.RoutePattern.RawText))
            .ToList();

        var folders = new Dictionary<string, List<object>>();

        foreach (var endpoint in endpoints)
        {
            var rawRoute = endpoint.RoutePattern.RawText!;
            if (rawRoute.StartsWith("swagger", StringComparison.OrdinalIgnoreCase) ||
                rawRoute.StartsWith("postman", StringComparison.OrdinalIgnoreCase) ||
                rawRoute.StartsWith("openapi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var httpMethodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            var httpMethod = httpMethodMetadata?.HttpMethods.Count > 0 ? httpMethodMetadata.HttpMethods[0] : "GET";
            var actionDescriptor = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();

            var controllerName = actionDescriptor?.ControllerName ?? "General";
            var actionName = actionDescriptor?.ActionName ?? endpoint.DisplayName ?? rawRoute;

            // Determine folder name by feature / controller
            var folderName = controllerName switch
            {
                var c when c.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("User", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("AccountSwitch", StringComparison.OrdinalIgnoreCase) => "01. Auth & Users",
                var c when c.Contains("Order", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Potential", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Urgent", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("PrepareForDelivery", StringComparison.OrdinalIgnoreCase) => "02. Orders & Fulfillment",
                var c when c.Contains("Employee", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Attendance", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Salary", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Break", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Task", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Rating", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Management", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Screen", StringComparison.OrdinalIgnoreCase) => "03. Employees & HR",
                var c when c.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Financial", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Exchange", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Pdf", StringComparison.OrdinalIgnoreCase) => "04. Finance & Expenses",
                var c when c.Contains("Delivery", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Camex", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Sandoog", StringComparison.OrdinalIgnoreCase) => "05. Delivery & Couriers",
                var c when c.Contains("Manufacturing", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Product", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("StoreCode", StringComparison.OrdinalIgnoreCase) => "06. Products & Stores",
                var c when c.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Inventory", StringComparison.OrdinalIgnoreCase) => "07. Warehouses",
                var c when c.Contains("Marketing", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Campaign", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Lead", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("StoreScript", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Video", StringComparison.OrdinalIgnoreCase) => "08. Marketing & Ads",
                var c when c.Contains("Chat", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Facebook", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Notification", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Conference", StringComparison.OrdinalIgnoreCase) => "09. Communication",
                var c when c.Contains("Operations", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Dashboard", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("Console", StringComparison.OrdinalIgnoreCase) ||
                           c.Contains("S3", StringComparison.OrdinalIgnoreCase) => "10. Operations & Diagnostics",
                var c when c.Contains("Search", StringComparison.OrdinalIgnoreCase) => "11. Search & AI",
                _ => "12. Reference Data & Common"
            };

            if (!folders.TryGetValue(folderName, out var folderItems))
            {
                folderItems = new List<object>();
                folders[folderName] = folderItems;
            }

            var cleanPath = rawRoute.TrimStart('/');
            var pathSegments = cleanPath.Split('/').ToList();

            var requestItem = new Dictionary<string, object>
            {
                ["name"] = $"{httpMethod} {rawRoute} - ({actionName})",
                ["request"] = new Dictionary<string, object>
                {
                    ["method"] = httpMethod,
                    ["header"] = new List<object>
                    {
                        new Dictionary<string, string>
                        {
                            ["key"] = "Accept",
                            ["value"] = "application/json"
                        }
                    },
                    ["url"] = new Dictionary<string, object>
                    {
                        ["raw"] = $"{{{{baseUrl}}}}/{cleanPath}",
                        ["host"] = new List<string> { "{{baseUrl}}" },
                        ["path"] = pathSegments
                    }
                }
            };

            // If it's a login request, attach test script to auto-set token variable
            if (rawRoute.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                ((Dictionary<string, object>)requestItem["request"])["body"] = new Dictionary<string, object>
                {
                    ["mode"] = "raw",
                    ["raw"] = "{\n  \"username\": \"{{username}}\",\n  \"password\": \"{{password}}\"\n}",
                    ["options"] = new Dictionary<string, object>
                    {
                        ["raw"] = new Dictionary<string, string> { ["language"] = "json" }
                    }
                };

                requestItem["event"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["listen"] = "test",
                        ["script"] = new Dictionary<string, object>
                        {
                            ["type"] = "text/javascript",
                            ["exec"] = new List<string>
                            {
                                "if (pm.response.code === 200) {",
                                "    var data = pm.response.json();",
                                "    if (data.token) {",
                                "        pm.collectionVariables.set('bearer_token', data.token);",
                                "        console.log('Bearer token automatically set for all requests!');",
                                "    }",
                                "}"
                            }
                        }
                    }
                };
            }
            else if (httpMethod == "POST" || httpMethod == "PUT")
            {
                ((Dictionary<string, object>)requestItem["request"])["body"] = new Dictionary<string, object>
                {
                    ["mode"] = "raw",
                    ["raw"] = "{\n}",
                    ["options"] = new Dictionary<string, object>
                    {
                        ["raw"] = new Dictionary<string, string> { ["language"] = "json" }
                    }
                };
            }

            folderItems.Add(requestItem);
        }

        var postmanItems = folders.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new Dictionary<string, object>
        {
            ["name"] = f.Key,
            ["item"] = f.Value
        }).ToList();

        var collection = new Dictionary<string, object>
        {
            ["info"] = new Dictionary<string, object>
            {
                ["_postman_id"] = Guid.NewGuid().ToString(),
                ["name"] = "Luxira CRM API v1.0 (.NET 10)",
                ["description"] = "Full Feature-Based Luxira CRM API collection for Postman. Contains all endpoints organized by feature slice with automatic JWT authentication management.",
                ["schema"] = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            ["auth"] = new Dictionary<string, object>
            {
                ["type"] = "bearer",
                ["bearer"] = new List<object>
                {
                    new Dictionary<string, string>
                    {
                        ["key"] = "token",
                        ["value"] = "{{bearer_token}}",
                        ["type"] = "string"
                    }
                }
            },
            ["variable"] = new List<object>
            {
                new Dictionary<string, string>
                {
                    ["key"] = "baseUrl",
                    ["value"] = baseUrl,
                    ["type"] = "string"
                },
                new Dictionary<string, string>
                {
                    ["key"] = "bearer_token",
                    ["value"] = "",
                    ["type"] = "string"
                },
                new Dictionary<string, string>
                {
                    ["key"] = "username",
                    ["value"] = "",
                    ["type"] = "string"
                },
                new Dictionary<string, string>
                {
                    ["key"] = "password",
                    ["value"] = "",
                    ["type"] = "string"
                }
            },
            ["item"] = postmanItems
        };

        return new JsonResult(collection);
    }
}
