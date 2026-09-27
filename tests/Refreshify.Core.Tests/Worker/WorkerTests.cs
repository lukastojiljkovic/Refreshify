using System.ComponentModel;
using System.IO.Pipes;
using Refreshify.Core.Engine;
using Refreshify.Core.Tools;
using Refreshify.Core.Worker;

namespace Refreshify.Core.Tests.Worker;

public sealed class WorkerTests : IAsyncDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeTool(ToolInfo info, Func<ToolContext, CancellationToken, Task<ToolResult>> run) : Tool(info)
    {
        public override Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken) => run(context, cancellationToken);
    }

    private static ToolInfo Info(string id, RunAs runAs) => new(id, id, ToolCategory.Repair, "", "Test.", "test", runAs);

    private static readonly Dictionary<string, Tool> Tools = new Tool[]
    {
        new FakeTool(Info("fake-admin", RunAs.Administrator), (context, _) =>
        {
            context.Status("Working", 50);
            return Task.FromResult(ToolResult.Succeeded("Done."));
        }),
        new FakeTool(Info("fake-wait", RunAs.Administrator), async (context, cancellationToken) =>
        {
            context.Status("Waiting");
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return ToolResult.Succeeded("Never.");
        }),
        new FakeTool(Info("fake-user", RunAs.User), (_, _) => Task.FromResult(ToolResult.Succeeded("Ran."))),
    }.ToDictionary(tool => tool.Info.Id);

    private readonly List<Task> _hosts = [];
    private readonly WorkerClient _client;
    private int _launches;

    public WorkerTests() => _client = new WorkerClient(pipeName =>
    {
        _launches++;
        _hosts.Add(new WorkerHost(Tools.GetValueOrDefault, LocalToolExecutor.Instance).RunAsync(pipeName, Environment.ProcessId, CancellationToken.None));
        return Environment.ProcessId;
    });

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await Task.WhenAll(_hosts).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
    }

    private static Tool Tool(string id) => new FakeTool(Info(id, RunAs.Administrator), (_, _) => throw new InvalidOperationException("Runs in the worker."));

    [Fact]
    public async Task A_tool_runs_in_the_worker_with_its_events_and_result()
    {
        var events = new List<ToolEvent>();

        var result = await _client.RunAsync(Tool("fake-admin"), new ToolOptions(), new SynchronousProgress<ToolEvent>(events.Add), Ct);

        Assert.Equal((ToolOutcome.Succeeded, "Done."), (result.Outcome, result.Summary));
        Assert.Contains(new ToolEvent(ToolEventKind.Status, "Working", 50), events);
    }

    [Fact]
    public async Task One_worker_serves_every_request_of_the_session()
    {
        await _client.RunAsync(Tool("fake-admin"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct);
        await _client.RunAsync(Tool("fake-admin"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct);

        Assert.Equal(1, _launches);
    }

    [Fact]
    public async Task Cancelling_reaches_the_tool_in_the_worker()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var progress = new SynchronousProgress<ToolEvent>(toolEvent =>
        {
            if (toolEvent.Text == "Waiting")
                cancellation.Cancel();
        });

        var result = await _client.RunAsync(Tool("fake-wait"), new ToolOptions(), progress, cancellation.Token);

        Assert.Equal(ToolOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task The_worker_only_runs_administrator_tools_from_its_catalog()
    {
        var user = await _client.RunAsync(Tool("fake-user"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct);
        var unknown = await _client.RunAsync(Tool("format-c"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct);

        Assert.Equal((ToolOutcome.Failed, ToolOutcome.Failed), (user.Outcome, unknown.Outcome));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(100_000, null)]
    [InlineData(24, "relative.iso")]
    [InlineData(24, @"C:\Windows\notepad.exe")]
    public async Task The_worker_rejects_invalid_options(int age, string? image)
    {
        var result = await _client.RunAsync(Tool("fake-admin"), new ToolOptions(age, image), new SynchronousProgress<ToolEvent>(_ => { }), Ct);

        Assert.Equal((ToolOutcome.Failed, "The tool's options aren't valid."), (result.Outcome, result.Summary));
    }

    [Fact]
    public async Task Declining_the_uac_prompt_is_reported_as_such()
    {
        await using var client = new WorkerClient(_ => throw new Win32Exception(1223));

        await Assert.ThrowsAsync<ElevationDeclinedException>(() =>
            client.RunAsync(Tool("fake-admin"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct));
    }

    [Fact]
    public async Task A_connection_from_another_process_is_refused()
    {
        await using var client = new WorkerClient(pipeName =>
        {
            _hosts.Add(new WorkerHost(Tools.GetValueOrDefault, LocalToolExecutor.Instance).RunAsync(pipeName, Environment.ProcessId, CancellationToken.None));
            return 4;
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            client.RunAsync(Tool("fake-admin"), new ToolOptions(), new SynchronousProgress<ToolEvent>(_ => { }), Ct));
    }

    [Fact]
    public async Task The_worker_refuses_a_pipe_served_by_another_process()
    {
        var name = $"refreshify-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var host = new WorkerHost(Tools.GetValueOrDefault, LocalToolExecutor.Instance).RunAsync(name, uiProcessId: 4, Ct);

        await server.WaitForConnectionAsync(Ct);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => host);
    }

    [Fact]
    public async Task A_worker_that_disappears_fails_the_request_and_the_next_one_starts_a_new_worker()
    {
        var launches = 0;
        await using var client = new WorkerClient(pipeName =>
        {
            if (++launches == 1)
            {
                // Connects, reads the request and vanishes, like a worker that crashed.
                _hosts.Add(Task.Run(async () =>
                {
                    await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(Ct);
                    await new StreamReader(pipe).ReadLineAsync(Ct);
                }, Ct));
            }
            else
            {
                _hosts.Add(new WorkerHost(Tools.GetValueOrDefault, LocalToolExecutor.Instance).RunAsync(pipeName, Environment.ProcessId, CancellationToken.None));
            }

            return Environment.ProcessId;
        });
        var progress = new SynchronousProgress<ToolEvent>(_ => { });

        await Assert.ThrowsAsync<IOException>(() => client.RunAsync(Tool("fake-admin"), new ToolOptions(), progress, Ct));
        var result = await client.RunAsync(Tool("fake-admin"), new ToolOptions(), progress, Ct);

        Assert.Equal((ToolOutcome.Succeeded, 2), (result.Outcome, launches));
    }
}
