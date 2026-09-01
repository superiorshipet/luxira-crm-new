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
                        document.Info.Title = "Luxira API";
                        document.Info.Version = DocumentName;
                        document.Info.Description =
                            "Feature-based Luxira backend API. Import this document URL into Postman to generate the complete endpoint collection.";

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
                                    "Luxira access token. Use the token returned by the explicit authentication endpoint.",
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
        var exposeDocument =
            app.Environment.IsDevelopment()
            || app.Environment.IsEnvironment("Testing")
            || app.Configuration.GetValue<bool>("OpenApi:Expose");

        if (exposeDocument)
        {
            app.MapOpenApi(DocumentRoute)
                .AllowAnonymous();
        }

        return app;
    }
}
