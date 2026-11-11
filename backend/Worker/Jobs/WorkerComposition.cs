using CurioDistrict.Worker.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CurioDistrict.Worker.Jobs;

/// <summary>
/// The worker's composition root.
///
/// Like the API host, this is the only place that binds a concrete
/// implementation to a port. It references Domain only (ADR-0004).
/// </summary>
public static class WorkerComposition
{
    /// <summary>Registers the worker's services into the host's container.</summary>
    public static IServiceCollection AddCurioDistrictWorker(this IServiceCollection services)
    {
        // Story 008 (AC4): the cross-process dispatch mechanism — the
        // WorkItemDispatcher that receives a serialized outbox payload and
        // re-attaches the correlation scope on the Worker's log lines.
        //
        // The concrete district-tick (EP-ECONOMY / ADR-0018) and
        // offline-resolution (EP-AUTOMATION / ADR-0010) handlers land in
        // their epics; this story wires the mechanism itself, with an empty
        // handler registry, so the actual receive/dispatch path exists and is
        // testable now. The Worker consumes the serialized payload across the
        // process boundary — it never references the API host or its
        // <c>Data.Outbox.WorkRecord</c> (ADR-0004: Domain-only).
        services.AddSingleton<WorkItemDispatcher>(sp =>
            new WorkItemDispatcher(
                sp.GetRequiredService<ILogger<WorkItemDispatcher>>(),
                new Dictionary<string, Func<WorkItemPayload, Task>>()));

        // The gated out-of-process receive loop (AC4's "actual receive" half).
        // Registered ONLY when CURIODISTRICT_WORKER_OUTBOX_PORT names a
        // loopback port, so the production Worker host is unchanged unless a
        // test/dev operator opts in. The sanctioned outbox transport is the
        // Postgres outbox (ADR-0018); this relay is a stopgap so the
        // cross-process contract is provable now without inventing a channel.
        var outboxPort =
            int.TryParse(Environment.GetEnvironmentVariable(OutboxRelayReceiver.PortEnvVar), out var p)
                ? p
                : 0;
        if (outboxPort > 0)
        {
            services.AddHostedService<OutboxRelayReceiver>();
        }

        // No job registrations yet — the district tick (ADR-0018) and the
        // offline-automation resolution (ADR-0010) land in EP-ECONOMY and
        // EP-AUTOMATION.
        return services;
    }
}
