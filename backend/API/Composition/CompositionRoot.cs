using CurioDistrict.Data.Health;
using Microsoft.Extensions.DependencyInjection;

namespace CurioDistrict.API.Composition;

/// <summary>
/// The composition root (ADR-0004).
///
/// Every concrete implementation of a Domain port is registered here and only
/// here. A use case in <c>CurioDistrict.Domain</c> never sees a concrete type —
/// it sees the port it depends on. Story 007 fills the ports in; story 008 adds
/// the middleware pipeline.
/// </summary>
public static class CompositionRoot
{
    /// <summary>Registers the backend's services into the host's container.</summary>
    public static IServiceCollection AddCurioDistrictComposition(this IServiceCollection services)
    {
        // The health endpoint (story 004) is the API's liveness/readiness
        // probe. A readiness check gates "healthy": the API answers 200 only
        // while Postgres accepts a query (compose healthcheck and the CI
        // pipeline both rely on it — story 004 AC1).
        services.AddHealthChecks()
            .AddCheck<PostgresReadinessCheck>(PostgresReadinessCheck.CheckName);

        return services;
    }
}
