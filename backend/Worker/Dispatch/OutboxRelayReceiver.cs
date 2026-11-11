using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace CurioDistrict.Worker.Dispatch;

/// <summary>
/// The Worker's out-of-process receive loop (story 008, FND-009, AC4).
///
/// This is the "actual receive" half the unit-level
/// <see cref="WorkItemDispatcher"/> cannot show on its own: a real, gated
/// loopback-TCP listener that a second OS process (the API host, in test) can
/// hand a serialized outbox payload to, and that hands each payload to the
/// production <see cref="WorkItemDispatcher"/> — which re-attaches the
/// correlation scope on the Worker's log lines.
///
/// **It is deliberately a stopgap, not a proposed architecture.** The
/// sanctioned outbox transport is the Postgres outbox (ADR-0018, owned by
/// EP-ECONOMY), which lands with the district tick. This relay is temporary
/// dev/test transport for cross-process contract verification, not durable
/// delivery or a production architecture decision. It is OFF by default:
/// it is registered only when the
/// <see cref="PortEnvVar"/> environment variable names a loopback port, so the
/// production Worker host is unchanged unless a test/dev operator opts in.
/// </summary>
public sealed class OutboxRelayReceiver : BackgroundService
{
    /// <summary>
    /// The environment variable that enables and addresses the relay: a loopback
    /// TCP port as a decimal string. Unset or empty → the relay is not
    /// registered at all.
    /// </summary>
    public const string PortEnvVar = "CURIODISTRICT_WORKER_OUTBOX_PORT";

    private readonly WorkItemDispatcher _dispatcher;
    private readonly int _port;
    private readonly ILogger<OutboxRelayReceiver> _logger;

    /// <summary>Creates the opt-in dev/test receiver using its configured port.</summary>
    /// <param name="dispatcher">Dispatches validated frames within a correlation scope.</param>
    /// <param name="logger">Records listener lifecycle and client failures.</param>
    public OutboxRelayReceiver(WorkItemDispatcher dispatcher, ILogger<OutboxRelayReceiver> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _port = int.TryParse(Environment.GetEnvironmentVariable(PortEnvVar), out var p) ? p : 0;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Bind the port SYNCHRONOUSLY before announcing: a start that
        // throws (port in use) must surface as a host fault, and the "now
        // listening" log must only fire once the port is actually live, so
        // a separate process that connects on seeing it never races the
        // bind (the test's cross-process send depends on that ordering).
        var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        _logger.LogInformation(
            "Outbox relay listening on 127.0.0.1:{Port} — one newline-delimited " +
            "WorkItemPayload per line.", _port);

        return AcceptLoopAsync(listener, stoppingToken);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken stoppingToken)
    {
        using var connectionsStopping = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var clients = new List<Task>();
        try
        {
            // One accept loop; each accepted connection is serviced
            // concurrently so a stuck client can't wedge the receiver.
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Host is stopping.
                }

                clients.RemoveAll(task => task.IsCompletedSuccessfully);
                clients.Add(HandleClientAsync(client, connectionsStopping.Token));
            }
        }
        finally
        {
            listener.Stop();
            await connectionsStopping.CancelAsync();
            await Task.WhenAll(clients);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        try
        {
            using var ownedClient = client;
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await ReadFramesAsync(reader, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // All accepted clients are awaited when the host stops.
        }
        catch (IOException)
        {
            _logger.LogDebug("Outbox relay client disconnected.");
        }
        catch (Exception fault)
        {
            _logger.LogError(fault, "Outbox relay client failed.");
        }
    }

    private async Task ReadFramesAsync(StreamReader reader, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);
            if (line is null) break; // The sender closed the connection.
            if (line.Length == 0) continue;

            try
            {
                // Correlation travels on each newline-delimited work payload.
                await _dispatcher.DispatchAsync(line);
            }
            catch (JsonException)
            {
                // Reject one invalid frame, then read the next; never log its body.
                _logger.LogWarning("Ignoring malformed outbox relay JSON.");
            }
        }
    }
}

