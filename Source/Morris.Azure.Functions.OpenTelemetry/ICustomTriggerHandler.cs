namespace Morris.Azure.Functions.OpenTelemetry;

public interface ICustomTriggerHandler : ITriggerHandler
{
	string GetTriggerTypeName();
}