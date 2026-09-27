using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Engine;

/// <summary>Runs a tool where it belongs: in this process, or in the elevated worker.</summary>
public interface IToolExecutor
{
    Task<ToolResult> RunAsync(Tool tool, ToolOptions options, IProgress<ToolEvent> progress, CancellationToken cancellationToken);
}

public sealed class LocalToolExecutor : IToolExecutor
{
    public static readonly LocalToolExecutor Instance = new();

    public Task<ToolResult> RunAsync(Tool tool, ToolOptions options, IProgress<ToolEvent> progress, CancellationToken cancellationToken) =>
        ToolRunner.RunAsync(tool, new ToolContext(options, progress, ProcessRunner.Default), cancellationToken);
}

/// <summary>The user declined the UAC prompt, so administrator tools can't run in this run.</summary>
public sealed class ElevationDeclinedException() : Exception("Administrator approval was declined.");
