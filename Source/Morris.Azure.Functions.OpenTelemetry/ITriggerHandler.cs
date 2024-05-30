using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker;
using System.Diagnostics;

namespace Morris.Azure.Functions.OpenTelemetry;

public interface ITriggerHandler
{
	Task<Activity?> HandleAsync(
		ActivitySource activitySource,
		TriggerParameterInfo triggerParameterInfo,
		FunctionContext context,
		FunctionExecutionDelegate next);
}
