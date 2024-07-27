using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morris.Azure.Functions.OpenTelemetry;

public class ServiceBusTriggerHandler : ITriggerHandler
{
    public async Task<Activity?> HandleAsync(ActivitySource activitySource, TriggerParameterInfo triggerParameterInfo, FunctionContext context, FunctionExecutionDelegate next)
    {
        string? applicationPropertiesJson = context.BindingContext.BindingData["ApplicationProperties"]?.ToString();
        ApplicationProperties? applicationProperties = null;

        if (applicationPropertiesJson != null)
        {
            applicationProperties = JsonSerializer.Deserialize<ApplicationProperties>(applicationPropertiesJson);
        }

        ActivityContext currentActivityContext = Activity.Current?.Context ?? new ActivityContext();
        var propagationContext = new PropagationContext(currentActivityContext, Baggage.Current);

        PropagationContext newPropagationContext = Propagators
                .DefaultTextMapPropagator
                .Extract(context: propagationContext, carrier: applicationProperties, getter: ExtractContextFromApplicationProperties);

        string activityName = context.FunctionDefinition.Name;
        Activity? activity = null;

        if (newPropagationContext.ActivityContext.TraceFlags == ActivityTraceFlags.Recorded)
        {
            activity = activitySource.StartActivity(name: activityName, kind: ActivityKind.Server, newPropagationContext.ActivityContext);
        }
        else
        {
            activity = activitySource.StartActivity(name: activityName, kind: ActivityKind.Server);
        }
        
        activity?.SetTag("messaging.system", "servicebus");
        activity?.SetTag("messaging.operation.type", "receive");
        activity?.SetTag("messaging.servicebus.message.enqueued_time", context.BindingContext.BindingData["EnqueuedTimeUtc"]?.ToString()); 
        activity?.SetTag("messaging.message.id", context.BindingContext.BindingData["MessageId"]?.ToString());
        activity?.SetTag("messaging.message.sequence_number", context.BindingContext.BindingData["SequenceNumber"]?.ToString());
        activity?.SetTag("messaging.servicebus.message.delivery_count", context.BindingContext.BindingData["DeliveryCount"]?.ToString());

        await next(context);
        return activity;
    }

    internal class ApplicationProperties()
    {
        [JsonPropertyName("Diagnostic-Id")]
        public string? DiagnosticId { get; set; }
    }

    internal static IEnumerable<string> ExtractContextFromApplicationProperties(ApplicationProperties? applicationProperties, string key) =>
        key == "traceparent" && applicationProperties?.DiagnosticId != null ? new List<string>() { applicationProperties.DiagnosticId } : [];
}
