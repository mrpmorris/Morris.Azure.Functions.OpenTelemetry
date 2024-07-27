using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Azure.Functions.Worker.Middleware;
using OpenTelemetry.Trace;

namespace Morris.Azure.Functions.OpenTelemetry;

public class ActivityTrackingMiddleware : IFunctionsWorkerMiddleware
{
	public const string ActivitySourceName = "AzureFunctionsWorker";

	private readonly static ConcurrentDictionary<string, TriggerParameterInfo> FunctionIdToTriggerParameterInfoLookup = new();
	private readonly static ActivitySource ActivitySource = new(ActivitySourceName);
	private readonly HttpTriggerHandler HttpTriggerHandler;
	private readonly ActivityTriggerHandler ActivityTriggerHandler;
	private readonly OrchestrationTriggerHandler OrchestratorTriggerHandler;
	private readonly ServiceBusTriggerHandler ServiceBusTriggerHandler;
    private readonly CronTriggerHandler CronTriggerHandler;
	private readonly FrozenDictionary<string, ICustomTriggerHandler> CustomTriggerHandlers;

	public ActivityTrackingMiddleware(
		HttpTriggerHandler httpTriggerHandler,
		ActivityTriggerHandler activityTriggerHandler,
		IEnumerable<ICustomTriggerHandler> customTriggerHandlers,
		OrchestrationTriggerHandler orchestratorTriggerHandler,
		ServiceBusTriggerHandler serviceBusTriggerHandler, 
		CronTriggerHandler cronTriggerHandler)
	{
		HttpTriggerHandler = httpTriggerHandler ?? throw new ArgumentNullException(nameof(httpTriggerHandler));
		CustomTriggerHandlers = customTriggerHandlers.ToFrozenDictionary(x => x.GetTriggerTypeName());
		ActivityTriggerHandler = activityTriggerHandler ?? throw new ArgumentNullException(nameof(activityTriggerHandler));
		OrchestratorTriggerHandler = orchestratorTriggerHandler ?? throw new ArgumentNullException(nameof(orchestratorTriggerHandler));
		ServiceBusTriggerHandler = serviceBusTriggerHandler ?? throw new ArgumentNullException(nameof(serviceBusTriggerHandler));
        CronTriggerHandler = cronTriggerHandler ?? throw new ArgumentNullException(nameof(cronTriggerHandler));
	}

	public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
	{
		Activity? activity = null;
		TriggerParameterInfo triggerParameterInfo = GetTriggerParameterInfo(context.FunctionDefinition);
		try
		{
			string triggerType = triggerParameterInfo.BindingMetadata.Type;
			ITriggerHandler? handler = triggerType switch
			{
				"httpTrigger" => HttpTriggerHandler,
				"activityTrigger" => ActivityTriggerHandler,
				"orchestrationTrigger" => OrchestratorTriggerHandler,
				"serviceBusTrigger" => ServiceBusTriggerHandler,
                "timerTrigger" => CronTriggerHandler,
				_ => CustomTriggerHandlers.TryGetValue(triggerType, out var h) ? h : null,
			};

			activity =
				handler is null
				? await PassthroughHandlerAsync(context, next)
				: await handler.HandleAsync(
					ActivitySource,
					triggerParameterInfo,
					context,
					next);

            if (activity is not null)
            {
                activity.SetTag(TraceSemanticConventions.AttributeFaasInvokedName, context.FunctionDefinition.Name);
                activity.SetTag(TraceSemanticConventions.AttributeFaasExecution, context.InvocationId);
                activity.SetTag(TraceSemanticConventions.AttributeFaasTrigger, triggerType);
                activity.SetTag(FunctionActivityConstants.Entrypoint, context.FunctionDefinition.EntryPoint);
                activity.SetTag(FunctionActivityConstants.Id, context.FunctionDefinition.Id);
            }
        }
        finally
        {
            activity?.Dispose();
        }
    }

	private async Task<Activity?> PassthroughHandlerAsync(FunctionContext context, FunctionExecutionDelegate next)
	{
		Activity? result = ActivitySource.StartActivity("Function Executed", ActivityKind.Server);
		await next(context);
		return result;
	}

	private static TriggerParameterInfo GetTriggerParameterInfo(FunctionDefinition functionDefinition) =>
		FunctionIdToTriggerParameterInfoLookup
			.GetOrAdd(
				key: functionDefinition.Id,
				valueFactory: _ => CreateTriggerParameterInfo(functionDefinition));

	private static TriggerParameterInfo CreateTriggerParameterInfo(FunctionDefinition functionDefinition)
	{
		foreach (FunctionParameter parameter in functionDefinition.Parameters)
		{
			foreach (KeyValuePair<string, object> kvp in parameter.Properties)
			{
				if (kvp.Value is TriggerBindingAttribute attribute)
					return new TriggerParameterInfo(
						parameter,
						functionDefinition.InputBindings[parameter.Name],
						attribute);
			}
		}
		throw new InvalidOperationException(
			$"Function \"{functionDefinition.Name}\" does not have a parameter"
			+ $" decorated with a \"{nameof(TriggerBindingAttribute)}\".");
	}
}
