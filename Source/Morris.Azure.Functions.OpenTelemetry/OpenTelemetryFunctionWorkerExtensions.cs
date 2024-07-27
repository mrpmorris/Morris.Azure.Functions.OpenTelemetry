using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;


namespace Morris.Azure.Functions.OpenTelemetry;

public static class OpenTelemetryFunctionWorkerExtensions
{
	public static IFunctionsWorkerApplicationBuilder AddMorrisOpenTelemetry(this IFunctionsWorkerApplicationBuilder builder)
	{
		builder.UseMiddleware<ActivityTrackingMiddleware>();
		builder.Services.TryAddSingleton<AzureResourceDetector>();
		RegisterHandlersForWellKnownTriggers(builder);
		builder.Services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracerProvider) =>
			tracerProvider
				.ConfigureResource(resourceBuilder => resourceBuilder
					.AddDetector(serviceProvider.GetRequiredService<AzureResourceDetector>())
				 )
				.AddSource(ActivityTrackingMiddleware.ActivitySourceName)
		);

		return builder;
	}

	private static void RegisterHandlersForWellKnownTriggers(IFunctionsWorkerApplicationBuilder builder)
	{
		builder.Services.TryAddSingleton<HttpTriggerHandler>();
		builder.Services.TryAddSingleton<ActivityTriggerHandler>();
		builder.Services.TryAddSingleton<OrchestrationTriggerHandler>();
		builder.Services.TryAddSingleton<ServiceBusTriggerHandler>();
        builder.Services.TryAddSingleton<CronTriggerHandler>();
	}
}