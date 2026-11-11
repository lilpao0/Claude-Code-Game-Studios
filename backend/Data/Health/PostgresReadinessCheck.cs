using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CurioDistrict.Data.Health;

/// <summary>
/// The Postgres readiness check (story 004, FND-004).
///
/// The compose healthcheck and the CI pipeline both treat <c>GET /health</c>
/// as the API's readiness gate: a 200 means "the API can serve requests". A
/// liveness-only endpoint would answer 200 while the database is unreachable
/// or the connection string is wrong — the API would be published as healthy
/// and every request behind it would fail. This check makes the endpoint
/// honest: it opens the connection the API itself is configured with and runs
/// a real query.
///
/// It lives in the Data layer, not the API host: Postgres access is the
/// Data layer's concern (ADR-0004), and the API host composes it in. The
/// Domain holds no knowledge of Postgres.
/// </summary>
public sealed class PostgresReadinessCheck : IHealthCheck
{
    /// <summary>
    /// The name of the connection string the check probes — the one compose
    /// supplies as <c>ConnectionStrings__CurioDistrict</c>.
    /// </summary>
    public const string ConnectionStringName = "CurioDistrict";

    /// <summary>The name the check is registered under.</summary>
    public const string CheckName = "postgres";

    // The compose healthcheck gives curl 3 seconds. The probe must answer
    // inside that window or the container is marked unhealthy for the wrong
    // reason (a curl timeout, not the check's verdict).
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly IConfiguration _configuration;

    /// <summary>
    /// What the check calls to reach Postgres. The production default is the
    /// real driver probe (<see cref="Probe"/>); the integration suite swaps in
    /// a controlled one so it can exercise the probe's cancellation and fault
    /// lifecycle without a live server.
    /// </summary>
    internal Func<string, CancellationToken, Task<HealthCheckResult>> ProbeFactory { get; }

    /// <summary>
    /// The losing probe's exception, observed on the deadline path, or null
    /// while the probe has not settled yet. Exposed so the integration suite
    /// can assert that a faulted probe is contained rather than surfacing
    /// later as an unobserved-task exception.
    /// </summary>
    internal Exception? ObservableProbeFault { get; private set; }

    /// <summary>Creates the readiness check using the API's configured database.</summary>
    /// <param name="configuration">Supplies the named CurioDistrict connection string.</param>
    public PostgresReadinessCheck(IConfiguration configuration)
        : this(configuration, null)
    {
    }

    internal PostgresReadinessCheck(
        IConfiguration configuration,
        Func<string, CancellationToken, Task<HealthCheckResult>>? probeFactory)
    {
        _configuration = configuration;
        ProbeFactory = probeFactory ?? Probe;
    }

    /// <summary>
    /// Opens the configured connection and runs <c>SELECT 1</c>. Any failure
    /// — a missing connection string, an unreachable host, a rejected
    /// password — is reported as unhealthy rather than thrown, so the endpoint
    /// answers 503 instead of returning a 500 with a stack trace.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                $"No connection string named '{ConnectionStringName}' is configured — " +
                "the API cannot reach Postgres.");
        }

        // Driver timers must not outlive the compose healthcheck's curl budget.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(ProbeTimeout);
        var probe = ProbeFactory(connectionString, deadline.Token);
        var finished = await Task.WhenAny(probe, Task.Delay(ProbeTimeout, cancellationToken));
        if (finished != probe)
        {
            deadline.Cancel();
            ObserveProbeFault(probe);
            return ProbeDidNotAnswer(cancellationToken);
        }

        return await ReadProbeResultAsync(probe, cancellationToken);
    }

    private static HealthCheckResult ProbeDidNotAnswer(CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested
            ? HealthCheckResult.Unhealthy("Probe cancelled before Postgres answered.")
            : HealthCheckResult.Unhealthy($"Postgres did not answer within {ProbeTimeout.TotalSeconds:0}s.");

    private void ObserveProbeFault(Task<HealthCheckResult> probe)
    {
        // Observe an eventual fault without extending the deadline, even when
        // a driver ignores cancellation. The check exposes this for diagnostics.
        _ = probe.ContinueWith(lost => ObservableProbeFault = lost.Exception,
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private static async Task<HealthCheckResult> ReadProbeResultAsync(
        Task<HealthCheckResult> probe, CancellationToken cancellationToken)
    {
        try
        {
            return await probe;
        }
        catch (OperationCanceledException)
        {
            // Either our deadline or the caller won the race; both are unhealthy.
            return ProbeDidNotAnswer(cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres is not reachable.", ex);
        }
    }

    /// <summary>
    /// Opens the configured connection and runs <c>SELECT 1</c>.
    /// </summary>
    internal static async Task<HealthCheckResult> Probe(string connectionString, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(token);

        return HealthCheckResult.Healthy("Postgres accepted a query.");
    }
}

