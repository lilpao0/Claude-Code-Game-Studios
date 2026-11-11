using CurioDistrict.Data.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CurioDistrict.Tests.Unit.Fnd;

/// <summary>
/// Story 004 AC1 — the readiness check behind <c>GET /health</c>, unit part.
///
/// The compose healthcheck and the CI pipeline both read the health endpoint's
/// status code as "the API can serve requests". A check that answers "healthy"
/// without touching Postgres would let the stack publish itself as ready while
/// every request behind it fails — so these tests pin the configuration-level
/// behaviours that make the check honest:
///
///  1. No connection string configured → unhealthy, with the no-configuration
///     description — not an exception, and not indistinguishable from a
///     server failure. The endpoint must answer 503, never 500.
///  2. A blank connection string is the same failure mode: the compose
///     <c>:?</c> form exists to prevent it, but the check must not depend on
///     compose to catch it.
///  3. The connection string name the check probes is the one compose supplies.
///     If it looked up a different name it would always report the
///     no-configuration branch, and the stack would never come up.
///
/// The network-level behaviours — a live Postgres answering, an unreachable
/// server, and the probe's own deadline — are in the integration suite
/// (<c>tests/integration/fnd/</c>), where a real Postgres container is the
/// controllable seam instead of loopback sockets whose OS behaviour
/// (refused vs. pending) is environment-specific.
/// </summary>
public class PostgresReadinessCheckTests
{
    private static readonly HealthCheckContext Context = new();

    private static PostgresReadinessCheck CheckWith(string? connectionString)
    {
        var settings = new Dictionary<string, string?>();

        // A null value models "the variable was never supplied" — the key is
        // absent from configuration entirely.
        if (connectionString is not null)
        {
            // The real host reads the value from the ConnectionStrings section
            // (compose supplies it as the ConnectionStrings__CurioDistrict
            // environment variable). GetConnectionString("CurioDistrict")
            // expands to the key "ConnectionStrings:CurioDistrict", so the
            // fixture must set that composite key. A bare root "CurioDistrict"
            // key is invisible to it and would make every test below fall
            // through to the no-connection-string path instead of the branch
            // it names — pinning the description is what keeps that wrong-key
            // bug from passing vacuously: with the key wrong, branch 1 would
            // also report "not reachable", and the assertion would fail.
            settings[$"ConnectionStrings:{PostgresReadinessCheck.ConnectionStringName}"] = connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new PostgresReadinessCheck(configuration);
    }

    [Fact]
    public async Task A_missing_connection_string_is_unhealthy_with_the_no_configuration_result()
    {
        var result = await CheckWith(connectionString: null).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(
            "No connection string named 'CurioDistrict' is configured — the API cannot reach Postgres.",
            result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task A_blank_connection_string_is_unhealthy_with_the_no_configuration_result()
    {
        // An empty value is the failure mode the compose `:?` form exists to
        // prevent, but the check must not depend on compose to catch it. It is
        // the SAME branch as a missing key, so the same description.
        var result = await CheckWith(connectionString: "   ").CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(
            "No connection string named 'CurioDistrict' is configured — the API cannot reach Postgres.",
            result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void The_check_probes_the_connection_string_the_compose_file_supplies()
    {
        // The compose file sets `ConnectionStrings__CurioDistrict`. If the
        // check looked up a different name it would always report the
        // no-configuration branch, and the stack would never come up.
        Assert.Equal("CurioDistrict", PostgresReadinessCheck.ConnectionStringName);
    }
}
