using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CurioDistrict.API.Composition;
using CurioDistrict.API.Errors;
using CurioDistrict.Data.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CurioDistrict.Tests.Integration.Worker;

/// <summary>
/// Drives a real HTTP request through the API pipeline, serializes the resulting
/// work in its test-only endpoint, and verifies the actual second Worker's stdout.
/// The opt-in loopback relay is dev/test transport, not a durable Postgres outbox.
/// </summary>
public class WorkerDispatchProcessTests(ITestOutputHelper output)
{
    private const int StartupTimeoutMs = 30_000;
    private const int LogCaptureTimeoutMs = 15_000;

    [Theory]
    [InlineData("corr-ac4-process-1", false)]
    [InlineData(null, false)]
    [InlineData("corr-after-malformed-frame", true)]
    public async Task A_separate_worker_process_logs_the_http_requests_correlation_id(string? suppliedId, bool malformedFirst)
    {
        var workerDll = WorkerBinaryPath();
        Assert.True(File.Exists(workerDll), $"Build the Worker before testing: {workerDll}");
        var port = FreePort();
        const string workItemId = "work-http-42";
        using var worker = StartWorker(workerDll, port);
        var listenerLine = await WaitForLine(worker, "Outbox relay listening", StartupTimeoutMs);
        Assert.True(listenerLine is not null, "Worker relay did not start.\n" + string.Join("\n", worker.OutputLines));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddCurioDistrictComposition();
        await using var app = builder.Build();
        app.UseCurioDistrictPipeline();
        // Test-only request causes the send; the sender never receives suppliedId
        // from the test. It must use the id established by the real middleware.
        app.MapPost("/test/dispatch", async (HttpContext context) =>
        {
            var id = context.Response.Headers[Correlation.HeaderName].ToString();
            var record = new WorkRecord(workItemId, "district.tick", id,
                new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
            using var sender = new TcpClient();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await sender.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
            var prefix = malformedFirst ? "{invalid-json}\nnull\n{}\n" : "";
            await sender.GetStream().WriteAsync(
                Encoding.UTF8.GetBytes(prefix + JsonSerializer.Serialize(record) + "\n"), deadline.Token);
            return Results.Accepted(value: new { workItemId });
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/test/dispatch");
            if (suppliedId is not null)
            {
                request.Headers.Add(Correlation.HeaderName, suppliedId);
            }
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var responseId = response.Headers.GetValues(Correlation.HeaderName).Single();
            Assert.False(string.IsNullOrWhiteSpace(responseId));
            if (suppliedId is not null) Assert.Equal(suppliedId, responseId);

            var logLine = await WaitForLine(worker, $"[{responseId}] Dispatching work item {workItemId}",
                LogCaptureTimeoutMs);
            Assert.True(logLine is not null, "Worker did not log the HTTP request's work.\n" + string.Join("\n", worker.OutputLines));
            output.WriteLine($"HTTP {(int)response.StatusCode}, response correlation={responseId}; child Worker PID={worker.Process.Id}");
            output.WriteLine(logLine);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static string WorkerBinaryPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Worker", "bin", "Release")))
            {
                return Path.Combine(dir.FullName, "Worker", "bin", "Release", "net10.0", "CurioDistrict.Worker.dll");
            }
        }
        throw new DirectoryNotFoundException("Could not locate backend/Worker/bin/Release from the test layout.");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record WorkerProcessWrapper(Process Process, ConcurrentQueue<string> OutputLines) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit(5_000);
                }
            }
            finally
            {
                Process.Dispose();
            }
        }
    }

    private static WorkerProcessWrapper StartWorker(string workerDll, int port)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(workerDll);
        start.Environment["CURIODISTRICT_WORKER_OUTBOX_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var process = Process.Start(start)!;
        var lines = new ConcurrentQueue<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lines.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lines.Enqueue(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new WorkerProcessWrapper(process, lines);
    }

    private static async Task<string?> WaitForLine(WorkerProcessWrapper worker, string marker, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            var match = worker.OutputLines.FirstOrDefault(line => line.Contains(marker, StringComparison.Ordinal));
            if (match is not null) return match;
            if (worker.Process.HasExited) return null;
            await Task.Delay(25);
        }
        return null;
    }
}
