using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Tools;

public sealed class CleanupToolTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ToolInfo TestInfo = new(
        "test-cleanup", "Test cleanup", ToolCategory.Cleanup, "", "Deletes test files.", "files", RunAs.User);

    private readonly string _root = Directory.CreateTempSubdirectory("refreshify-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateFile(string name, int bytes)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static Task<ToolResult> RunAsync(Tool tool) =>
        ToolRunner.RunAsync(tool, new ToolContext(new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), ProcessRunner.Default), Ct);

    [Fact]
    public async Task Cleans_every_target_and_reports_the_measured_space()
    {
        CreateFile(@"a\one.tmp", 1024);
        CreateFile(@"b\two.tmp", 2048);
        var tool = new CleanupTool(TestInfo, () => [new CleanupTarget(Path.Combine(_root, "a")), new CleanupTarget(Path.Combine(_root, "b"))]);

        var result = await RunAsync(tool);

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.Equal(3072, result.BytesFreed);
        Assert.Equal("Freed 3.0 KB (2 files).", result.Summary);
    }

    [Fact]
    public async Task Shows_what_it_has_deleted_so_far()
    {
        CreateFile("one.tmp", 1024);
        var statuses = new List<string>();
        var progress = new SynchronousProgress<ToolEvent>(toolEvent => statuses.Add(toolEvent.Text));
        var tool = new CleanupTool(TestInfo, () => [new CleanupTarget(_root)]);

        await ToolRunner.RunAsync(tool, new ToolContext(new ToolOptions(), progress, ProcessRunner.Default), Ct);

        Assert.Equal(["Deleting files", "Deleted 1 file (1.0 KB) so far"], statuses);
    }

    [Fact]
    public async Task Files_in_use_are_mentioned_but_are_not_a_failure()
    {
        CreateFile("free.tmp", 10);
        using var locked = new FileStream(CreateFile("locked.tmp", 10), FileMode.Open, FileAccess.Read, FileShare.None);
        var tool = new CleanupTool(TestInfo, () => [new CleanupTarget(_root)]);

        var result = await RunAsync(tool);

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.Equal("Freed 10 bytes (1 file). Skipped 1 file that is in use.", result.Summary);
    }

    [Fact]
    public async Task Reports_when_there_is_nothing_to_clean()
    {
        var tool = new CleanupTool(TestInfo, () => [new CleanupTarget(Path.Combine(_root, "missing"))]);

        var result = await RunAsync(tool);

        Assert.Equal(ToolOutcome.Succeeded, result.Outcome);
        Assert.Equal("There was nothing to clean up.", result.Summary);
    }

    [Fact]
    public async Task A_precondition_result_is_returned_without_deleting_anything()
    {
        var file = CreateFile("keep.tmp", 10);
        var tool = new CleanupTool(TestInfo, () => [new CleanupTarget(_root)],
            precondition: () => ToolResult.Skipped("Windows is waiting for a restart."));

        var result = await RunAsync(tool);

        Assert.Equal(ToolOutcome.Skipped, result.Outcome);
        Assert.True(File.Exists(file));
    }
}
