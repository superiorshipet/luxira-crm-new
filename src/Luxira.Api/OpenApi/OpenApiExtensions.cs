using Microsoft.AspNetCore.OpenApi;

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

