using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Luxira.Api.OpenApi;

internal static class OpenApiExtensions
{
    internal const string DocumentName = "v1";
    internal const string DocumentRoute = "/swagger/{documentName}/swagger.json";
    internal const string V1DocumentPath = "/swagger/v1/swagger.json";

    internal static IServiceCollection AddLuxiraOpenApi(
        this IServiceCollection services)
    {
        services.AddOpenApi(
            DocumentName,
            options =>
            {
                options.AddDocumentTransformer(
                    (document, _, _) =>
                    {
                        document.Info.Title = "Luxira CRM API (.NET 10)";
                        document.Info.Version = DocumentName;
                        document.Info.Description =
                            "Feature-based Luxira CRM backend API. Import this document URL into Postman to generate the complete endpoint collection.";

                        return Task.CompletedTask;
                    });
                options.AddDocumentTransformer(
                    (document, _, _) =>
                    {
                        document.Components ??= new OpenApiComponents();
                        document.Components.SecuritySchemes ??=
                            new Dictionary<string, IOpenApiSecurityScheme>();
                        document.Components.SecuritySchemes["Bearer"] =
                            new OpenApiSecurityScheme
                            {
                                Type = SecuritySchemeType.Http,
                                Scheme = "bearer",
                                BearerFormat = "JWT",
                                Description =
                                    "Luxira access token. Use the token returned by the authentication endpoint.",
                            };

                        return Task.CompletedTask;
                    });
                options.AddOperationTransformer(
                    (operation, context, _) =>
                    {
                        var metadata = context.Description
                            .ActionDescriptor
                            .EndpointMetadata;
                        var isAnonymous = metadata
                            .OfType<IAllowAnonymous>()
                            .Any();

                        if (isAnonymous)
                        {
                            return Task.CompletedTask;
                        }

                        operation.Security ??= [];
                        operation.Security.Add(
                            new OpenApiSecurityRequirement
                            {
                                [new OpenApiSecuritySchemeReference(
                                    "Bearer",
                                    context.Document)] = [],
                            });

                        return Task.CompletedTask;
                    });
            });

        return services;
    }

    internal static IEndpointRouteBuilder MapLuxiraOpenApi(
        this WebApplication app)
    {
        app.MapOpenApi(DocumentRoute).AllowAnonymous();
        app.MapOpenApi("/openapi/v1.json").AllowAnonymous();

        // Interactive Swagger UI & Postman helper page
        app.MapGet("/swagger", () => Results.Content(@"<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='utf-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1' />
  <title>Luxira CRM API Documentation & Postman</title>
  <link rel='stylesheet' href='https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui.css' />
  <style>
    body { margin: 0; background: #fafafa; font-family: sans-serif; }
    .postman-bar { background: #ff6c37; color: white; padding: 12px 20px; font-size: 15px; font-weight: bold; display: flex; justify-content: space-between; align-items: center; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }
    .postman-btn { background: white; color: #ff6c37; border: none; padding: 8px 16px; border-radius: 4px; font-weight: bold; cursor: pointer; text-decoration: none; display: inline-block; margin-left: 8px; }
    .postman-btn:hover { background: #f0f0f0; }
  </style>
</head>
<body>
  <div class='postman-bar'>
    <span>🚀 Luxira CRM .NET 10 API — Postman Import Available</span>
    <div>
      <a class='postman-btn' href='/postman/collection.json' target='_blank'>📥 Postman Collection (v2.1)</a>
      <a class='postman-btn' href='/swagger/v1/swagger.json' target='_blank'>📄 OpenAPI Schema (JSON)</a>
    </div>
  </div>
  <div id='swagger-ui'></div>
  <script src='https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-bundle.js' crossorigin></script>
  <script>
    window.onload = () => {
      window.ui = SwaggerUIBundle({
        url: '/swagger/v1/swagger.json',
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
          SwaggerUIBundle.presets.apis,
          SwaggerUIBundle.SwaggerUIStandalonePreset
        ],
        layout: 'BaseLayout'
      });
    };
  </script>
</body>
</html>", "text/html")).AllowAnonymous();

        app.MapGet("/docs", () => Results.Redirect("/swagger")).AllowAnonymous();

        return app;
    }
}
