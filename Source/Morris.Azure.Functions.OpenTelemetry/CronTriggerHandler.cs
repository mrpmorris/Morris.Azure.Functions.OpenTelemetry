using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Morris.Azure.Functions.OpenTelemetry;

public class CronTriggerHandler : ITriggerHandler
{
    public async Task<Activity?> HandleAsync(ActivitySource activitySource, TriggerParameterInfo triggerParameterInfo, FunctionContext context, FunctionExecutionDelegate next)
    {
        string activityName = context.FunctionDefinition.Name;
        Activity? activity = activitySource.StartActivity(name: activityName, kind: ActivityKind.Server);

        var schedule = ((TimerTriggerAttribute)triggerParameterInfo.BindingAttribute).Schedule;
        activity?.SetTag(TraceSemanticConventions.AttributeFaasCron, schedule);

        await next(context);
        return activity;
    }
}
