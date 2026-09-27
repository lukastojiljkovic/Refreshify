using System.Text.Json.Serialization;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Worker;

/// <summary>
/// Messages between the app and its elevated worker, one JSON object per line. The worker is only ever asked to run a
/// catalog tool by id with typed options; it never receives a command line.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RunMessage), "run")]
[JsonDerivedType(typeof(CancelMessage), "cancel")]
[JsonDerivedType(typeof(EventMessage), "event")]
[JsonDerivedType(typeof(ResultMessage), "result")]
public abstract record WorkerMessage(int RequestId);

public sealed record RunMessage(int RequestId, string ToolId, ToolOptions Options) : WorkerMessage(RequestId);

public sealed record CancelMessage(int RequestId) : WorkerMessage(RequestId);

public sealed record EventMessage(int RequestId, ToolEvent Event) : WorkerMessage(RequestId);

public sealed record ResultMessage(int RequestId, ToolResult Result) : WorkerMessage(RequestId);

[JsonSourceGenerationOptions(UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkerMessage))]
internal sealed partial class WorkerJson : JsonSerializerContext;
