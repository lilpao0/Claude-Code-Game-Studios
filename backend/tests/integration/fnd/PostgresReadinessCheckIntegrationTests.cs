using System.Net;
using System.Net.Sockets;
using CurioDistrict.Data.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CurioDistrict.Tests.Integration.Fnd;

/// <summary>
/// Story 004 AC1 — the readiness check's network-level behaviour, integration
/// part.
///
/// The unit suite (<c>tests/unit/fnd/PostgresReadinessCheckTests.cs</c>)
/// covers the configuration branches, which need no sockets. These tests
/// exercise the probe against real TCP behaviour, because the OS decides how
/// a dead or silent server surfaces (ECONNREFUSED, a pending socket, a
/// dropped packet) and that is environment-specific:
///
///  1. A loopback port nothing listens on → unhealthy, inside the compose
///     healthcheck's curl budget.
///  2. A loopback socket that accepts the handshake and then goes silent →
///     the probe's own deadline answers, not the driver's 15-second timer.
///  3. A live Postgres (only when <c>CURIODISTRICT_TEST_DB_CONNECTION_STRING</c>
///     names one) → healthy, the same path the smoke check proves on the
///     running stack.
/// </summary>
public class PostgresReadinessCheckIntegrationTests
{
    private static readonly HealthCheckContext Context = new();

    private static PostgresReadinessCheck CheckWith(string connectionString)
    {
        // The host reads the value from the ConnectionStrings section; the
        // fixture sets the same composite key, for the same reason the unit
        // fixture does (see PostgresReadinessCheckTests there).
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{PostgresReadinessCheck.ConnectionStringName}"] = connectionString,
        };

        return new PostgresReadinessCheck(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public async Task An_unreachable_postgres_is_unhealthy_inside_the_probe_budget()
    {
        // A loopback port nothing listens on. ECONNREFUSED on this OS means
        // the "not reachable" branch; on an OS that leaves the connect
        // pending, the probe's deadline branch answers instead. Both are
        // correct — the invariant is the unhealthy verdict inside the
        // compose healthcheck's curl budget, not which branch reported it.
        var port = FreePort();
        var check = CheckWith($"Host=127.0.0.1;Port={port};Database=none;Username=none;Password=none");

        var started = DateTimeOffset.UtcNow;
        var result = await check.CheckHealthAsync(Context);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains(result.Description!, new[]
        {
            "Postgres is not reachable.",
            "Postgres did not answer within 2s.",
        });
        Assert.True(
            elapsed < TimeSpan.FromSeconds(3),
            $"The probe took {elapsed.TotalSeconds:0.0}s — it must answer inside the compose " +
            "healthcheck's 3-second curl budget or the container is marked unhealthy for " +
            "the wrong reason.");
    }

    [Fact]
    public async Task A_silent_postgres_answers_on_the_probe_deadline_not_the_driver_timer()
    {
        // The seam is a loopback socket that accepts the TCP handshake and
        // then says nothing. Without the probe's own deadline the driver's
        // 15-second default connection timeout would decide when the check
        // answers — long after the compose healthcheck's curl has given up.
        using var blackhole = new TcpListener(IPAddress.Loopback, 0);
        blackhole.Start();
        var port = ((IPEndPoint)blackhole.LocalEndpoint).Port;

        var check = CheckWith($"Host=127.0.0.1;Port={port};Database=none;Username=none;Password=none");

        var started = DateTimeOffset.UtcNow;
        var result = await check.CheckHealthAsync(Context);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Postgres did not answer within 2s.", result.Description);
        Assert.Null(result.Exception);
        Assert.True(
            elapsed < TimeSpan.FromSeconds(3),
            $"The probe took {elapsed.TotalSeconds:0.0}s — it must give up on its own deadline " +
            "rather than outlive the compose healthcheck's curl budget.");
    }

    [SkippableFact]
    public async Task A_live_postgres_answers_healthy()
    {
        // The live path is only asserted against a real database. When none
        // is configured the test reports SKIPPED (via xunit.SkippableFact),
        // naming what to set — never a vacuous pass. The Docker smoke check
        // sets CURIODISTRICT_TEST_DB_CONNECTION_STRING to the running
        // stack's Postgres so this path actually executes alongside AC1, and
        // AC1's own /health 200 proves the same check answers healthy
        // against that database.
        var connectionString = Environment.GetEnvironmentVariable("CURIODISTRICT_TEST_DB_CONNECTION_STRING");

        Skip.IfNot(
            !string.IsNullOrWhiteSpace(connectionString),
            "No CURIODISTRICT_TEST_DB_CONNECTION_STRING — set it to a live Postgres to exercise the healthy path.");

        // The skip above guarantees non-blank; the null-forgiving operator
        // is the honest way to tell the compiler that.
        var check = CheckWith(connectionString!);

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Postgres accepted a query.", result.Description);
    }

    [Fact]
    public async Task A_slow_probe_is_cancelled_and_its_fault_is_observed_without_extending_the_budget()
    {
        // Focused regression on the probe lifecycle: when the probe loses
        // the race against its own deadline, the check must (1) cancel the
        // probe explicitly, (2) observe the probe's eventual fault so it
        // can never surface later as an unobserved-task exception on the
        // host, and (3) answer on schedule — a probe that ignores its
        // token must not extend the check past the compose healthcheck's
        // curl budget.
        //
        // The probe is a controlled seam: a TaskCompletionSource that stays
        // pending even after its token is cancelled — the deterministic
        // "Postgres went silent" shape. The test faults it DELIBERATELY,
        // only after the check has answered, so the observed fault is a
        // distinct, known exception and not an OCE artifact of the race.
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{PostgresReadinessCheck.ConnectionStringName}"] =
                "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
        };

        var pendingProbe = false;
        CancellationToken receivedProbeToken = default;
        var pendingSource = new TaskCompletionSource<HealthCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // The probe always answers with the same deliberately-pending task:
        // it stays pending after the check's deadline and after the check
        // cancels its token — the deterministic "Postgres went silent" shape
        // that only the check's own deadline can make return. The test
        // faults that task later, so the observed fault is a distinct,
        // known exception and not an OCE artifact of the race.
        Task<HealthCheckResult> ControlledProbe(string _connectionString, CancellationToken _token)
        {
            pendingProbe = true;
            receivedProbeToken = _token;
            return pendingSource.Task;
        }

        var check = new PostgresReadinessCheck(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            (cs, t) => ControlledProbe(cs, t));

        var started = DateTimeOffset.UtcNow;
        var result = await check.CheckHealthAsync(Context);
        var elapsed = DateTimeOffset.UtcNow - started;

        // The probe stayed pending until the deadline. Check cancellation
        // directly rather than inferring it from the unhealthy result.
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Postgres did not answer within 2s.", result.Description);
        Assert.True(pendingProbe, "The check probed through the seam — not the driver.");
        Assert.True(receivedProbeToken.IsCancellationRequested,
            "The losing probe must receive cancellation before the check returns.");
        Assert.True(
            elapsed < TimeSpan.FromSeconds(3),
            $"The check took {elapsed.TotalSeconds:0.0}s — the losing probe must not extend " +
            "the answer past the compose healthcheck's 3-second curl budget.");

        // Now fault the deliberately pending probe and observe that the
        // check's continuation records the fault — the exact content that
        // would otherwise surface as an unobserved-task exception.
        pendingSource.TrySetException(new InvalidOperationException("controlled probe fault"));
        var observedBy = DateTimeOffset.UtcNow.AddSeconds(2);
        while (check.ObservableProbeFault is null && DateTimeOffset.UtcNow < observedBy)
        {
            await Task.Delay(20);
        }

        Assert.NotNull(check.ObservableProbeFault);

        var fault = check.ObservableProbeFault!.GetBaseException();
        // The observed fault is the controlled probe's own exception — not
        // an OCE of the race — proving the check contained the real fault.
        Assert.IsType<InvalidOperationException>(fault);
        Assert.Equal("controlled probe fault", fault.Message);
    }

    /// <summary>
    /// A loopback port nothing is listening on: the OS answers the connect
    /// attempt itself, so no external network is involved.
    /// </summary>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
