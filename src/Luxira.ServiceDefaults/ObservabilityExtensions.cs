using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Luxira.ServiceDefaults;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddLuxiraObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        var sampleRatio = configuration.GetValue<double?>(
                "OpenTelemetry:TraceSampleRatio")
            ?? (environment.IsDevelopment() ? 1d : 0.1d);
        sampleRatio = Math.Clamp(sampleRatio, 0d, 1d);

        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(
                        new ParentBasedSampler(
                            new TraceIdRatioBasedSampler(sampleRatio)))
                    .AddAspNetCoreInstrumentation(options =>
                        options.RecordException = true)
                    .AddHttpClientInstrumentation(options =>
                        options.RecordException = true);

                if (TryGetOtlpEndpoint(configuration, out var endpoint))
                {
                    tracing.AddOtlpExporter(options =>
                        options.Endpoint = endpoint);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (TryGetOtlpEndpoint(configuration, out var endpoint))
                {
                    metrics.AddOtlpExporter(options =>
                        options.Endpoint = endpoint);
                }
            });

        return services;
    }

    private static bool TryGetOtlpEndpoint(
        IConfiguration configuration,
        out Uri endpoint) =>
        Uri.TryCreate(
            configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            UriKind.Absolute,
            out endpoint!);
}
