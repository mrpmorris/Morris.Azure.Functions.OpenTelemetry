using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Diagnostics;

namespace Morris.Azure.Functions.OpenTelemetry;

public class OrchestrationTriggerHandler : ITriggerHandler
{
	public async Task<Activity?> HandleAsync(
		ActivitySource activitySource,
		TriggerParameterInfo triggerParameterInfo,
		FunctionContext context,
		FunctionExecutionDelegate next)
	{
		ActivityContext currentActivityContext = Activity.Current?.Context ?? new ActivityContext();
		var propagationContext = new PropagationContext(currentActivityContext, Baggage.Current);

		string activityName = context.FunctionDefinition.Name;
		Activity? activity = activitySource
			.StartActivity(
				name: activityName,
				kind: ActivityKind.Internal,
				propagationContext.ActivityContext);

		await next(context);
		return activity;
	}
}