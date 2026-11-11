using Microsoft.Extensions.Logging;

namespace CurioDistrict.Worker.Logging;

/// <summary>
/// The Worker half of the correlation contract (story 008, FND-009, AC4).
///
/// The correlation id travels WITH the work — a field on the outbox/event
/// record (<c>CurioDistrict.Data.Outbox.WorkRecord.CorrelationId</c>) — not
/// over the HTTP header. This helper re-attaches it on the Worker side:
/// every log line a work item produces is written inside a scope stamped
/// with the record's id, so a grep for the id in the Worker's log finds
/// exactly the lines for the work the request caused.
///
/// The scope is a plain formatted string under the SAME header name the
/// API uses (<c>X-Correlation-Id</c>), so one grep covers both processes.
/// It is deliberately a string (no structured dictionary state): any
/// logger provider renders it, and the Worker keeps referencing Domain
/// only (ADR-0004) — this helper is self-contained.
/// </summary>
public static class WorkItemLogging
{
    /// <summary>The header name, mirrored from the API host — one constant, both hosts.</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>
    /// Opens the work-item scope for one outbox/event record and returns
    /// it as the disposable that brackets the item's log lines.
    /// </summary>
    public static IDisposable OpenWorkItemScope(
        ILogger logger,
        string workItemId,
        string correlationId)
    {
        var scope = logger.BeginScope(
            $"{CorrelationHeader}: {correlationId} | WorkItem: {workItemId}");
        // BeginScope may legitimately report "no scope active" as null when
        // the logger has no provider; an empty scope then still satisfies
        // the IDisposable contract without leaking.
        return scope ?? NoOpScope.Value;
    }

    /// <summary>Convenience: the scope opened from a work record's fields.</summary>
    public static IDisposable OpenWorkItemScope(ILogger logger, (string WorkItemId, string CorrelationId) record)
        => OpenWorkItemScope(logger, record.WorkItemId, record.CorrelationId);

    private sealed class NoOpScope : IDisposable
    {
        internal static readonly NoOpScope Value = new();
        public void Dispose() { }
    }
}
