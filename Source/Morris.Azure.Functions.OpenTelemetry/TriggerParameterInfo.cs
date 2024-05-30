using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

public readonly record struct TriggerParameterInfo(
	FunctionParameter Parameter,
	BindingMetadata BindingMetadata,
	TriggerBindingAttribute BindingAttribute);
