using System.Text;
using System.Text.Json;
using CurioDistrict.Worker.Logging;
using Microsoft.Extensions.Logging;

namespace CurioDistrict.Worker.Dispatch;

/// <summary>
/// The Worker's receive/dispatch path for work that crossed the process
/// boundary from the API host (story 008, FND-009, AC4).
///
/// This is the actual "Worker log for the resulting work item" half of AC4,
/// not just the scope helper: it takes the serialized payload the API host's
/// outbox row produced, deserializes it into a <see cref="WorkItemPayload"/>,
/// re-attaches the record's correlation id onto the Worker's log scope via
/// <see cref="WorkItemLogging.OpenWorkItemScope"/>, and — for a type it has a
/// handler for — runs that handler. Every log line the handler emits lands in
/// the correlation scope, so a grep for the id in the Worker's log finds
/// exactly the lines for the work the request caused.
///
/// The boundary is preserved by construction: the dispatcher consumes the
/// serialized bytes and the Domain-only <see cref="WorkItemPayload"/> mirror
/// — it never references <c>CurioDistrict.Data</c> or the API host. The actual
/// "work" is a handler registered per <c>WorkType</c> by the composition root;
/// this story wires the mechanism, the concrete district-tick and
/// offline-resolution handlers land with EP-ECONOMY / EP-AUTOMATION.
/// </summary>
public sealed class WorkItemDispatcher
{
    private readonly ILogger<WorkItemDispatcher> _logger;
    private readonly IReadOnlyDictionary<string, Func<WorkItemPayload, Task>> _handlers;

    /// <summary>
    /// Builds the dispatcher. <paramref name="handlers"/> maps a
    /// <c>WorkType</c> to the handler that performs the work for that type.
    /// </summary>
    public WorkItemDispatcher(ILogger<WorkItemDispatcher> logger,
        IReadOnlyDictionary<string, Func<WorkItemPayload, Task>> handlers)
    {
        _logger = logger;
        _handlers = handlers;
    }

    /// <summary>
    /// Receives one cross-process payload and dispatches it. The payload is
    /// deserialized, the work item is logged inside its correlation scope,
    /// and — when a handler is registered for the type — the handler runs
    /// inside that same scope. An unknown type is logged and skipped, not a
    /// fault: the Worker must not crash on a type a newer host emits.
    /// </summary>
    public async Task DispatchAsync(string serializedPayload)
    {
        var payload = WorkItemPayload.Deserialize(serializedPayload);

        // Re-attach the correlation id ON the Worker's log: this is the
        // scope that stamps every line this work item produces, for any
        // provider that renders scope state. The id is ALSO carried in
        // the message itself as a structured argument — a provider that
        // does NOT render scope state (the generic host's default console
        // sink is one) still shows the id on the line, so a grep for it
        // finds this work item under ANY sink. That is AC4's
        // "the log line for that work item contains the same correlation
        // ID" for every configuration the host can run with.
        using (WorkItemLogging.OpenWorkItemScope(_logger, payload.WorkItemId, payload.CorrelationId))
        {
            _logger.LogInformation(
                "[{CorrelationId}] Dispatching work item {WorkItemId} of type {WorkType}",
                payload.CorrelationId, payload.WorkItemId, payload.WorkType);

            if (_handlers.TryGetValue(payload.WorkType, out var handler))
            {
                await handler(payload);
            }
            else
            {
                _logger.LogWarning(
                    "[{CorrelationId}] No handler registered for work type {WorkType}; item {WorkItemId} skipped.",
                    payload.CorrelationId, payload.WorkType, payload.WorkItemId);
            }
        }
    }
}
