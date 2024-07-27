using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;

namespace Morris.Azure.Functions.OpenTelemetry;

public class HttpTriggerHandler : ITriggerHandler
{
	public async Task<Activity?> HandleAsync(
		ActivitySource activitySource,
		TriggerParameterInfo triggerParameterInfo,
		FunctionContext context,
		FunctionExecutionDelegate next)
	{
		HttpRequestData? requestData = await context.GetHttpRequestDataAsync();
		if (requestData is null)
			return null;

		ActivityContext currentActivityContext = Activity.Current?.Context ?? new ActivityContext();
		var propagationContext = new PropagationContext(currentActivityContext, Baggage.Current);

		PropagationContext newPropagationContext =
			Propagators
				.DefaultTextMapPropagator
				.Extract(
					context: propagationContext,
					carrier: requestData.Headers,
					getter: ExtractContextFromHeaderCollection);

        string activityName = $"{requestData.Method} {context.FunctionDefinition.Name}";
        Activity? activity = null;

        if (newPropagationContext.ActivityContext.TraceFlags == ActivityTraceFlags.Recorded)
        {
            activity = activitySource.StartActivity(
				name: activityName, 
				kind: ActivityKind.Server, 
				newPropagationContext.ActivityContext);
        }
        else
        {
            activity = activitySource.StartActivity(
				name: activityName, 
				kind: ActivityKind.Server);

        }

        string? route = requestData.Url.AbsolutePath;
        activity?.SetTag(TraceSemanticConventions.AttributeHttpRoute, route);
        activity?.SetTag(TraceSemanticConventions.AttributeHttpMethod, requestData.Method);
        activity?.SetTag(TraceSemanticConventions.AttributeHttpTarget, requestData.Url);
        activity?.SetTag(TraceSemanticConventions.AttributeNetHostName, requestData.Url.Host);
        activity?.SetTag(TraceSemanticConventions.AttributeNetHostPort, requestData.Url.Port);
        activity?.SetTag(TraceSemanticConventions.AttributeHttpScheme, requestData.Url.Scheme);

        try
        {
            await next(context);
        }
        finally
        {
            if (context.GetHttpResponseData() is { } responseData)
            {
                activity?.SetTag(TraceSemanticConventions.AttributeHttpStatusCode, (int)responseData.StatusCode);
            }
        }

		return activity;
	}

	internal static IEnumerable<string> ExtractContextFromHeaderCollection(HttpHeadersCollection headersCollection, string key) =>
		headersCollection.TryGetValues(key, out var propertyValue) ? propertyValue : [];
}
