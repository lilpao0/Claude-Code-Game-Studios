using System.Text.Json;
using CurioDistrict.Data.Outbox;
using CurioDistrict.Tests.Unit;
using CurioDistrict.Worker.Dispatch;
using Microsoft.Extensions.Logging;

namespace CurioDistrict.Tests.Unit.Worker;

/// <summary>
/// Story 008 (FND-009) AC4 — the correlation id reaching the Worker's log,
/// across the process boundary.
///
/// The mechanism, recorded here and in <c>WorkItemDispatcher</c>: the API
/// host's outbox row (<see cref="WorkRecord"/>) serializes to JSON; that JSON
/// is what crosses the process boundary to the Worker. The Worker
/// deserializes it into its Domain-only mirror (<see cref="WorkItemPayload"/>)
/// and re-attaches the record's correlation id onto its own log scope via
/// <c>WorkItemLogging</c>. These tests drive the real
/// <see cref="WorkItemDispatcher"/> end to end: serialize on the "API side",
/// hand the bytes over, dispatch on the "Worker side", and assert the id and
/// the work item's log lines both carry it.
/// </summary>
public class WorkerDispatchAc4Tests
{
    /// <summary>A handler registry mirroring what the composition root wires.</summary>
    private static IReadOnlyDictionary<string, Func<WorkItemPayload, Task>> Handlers(params
        (string WorkType, Func<WorkItemPayload, Task> Handler)[] entries)
        => entries.ToDictionary(e => e.WorkType, e => e.Handler);

    [Fact]
    public async Task The_worker_dispatch_of_an_api_outbox_payload_logs_the_api_correlation_id()
    {
        // API side: the outbox row the host writes when a request causes work.
        var apiRecord = new WorkRecord(
            WorkItemId: "work-42",
            WorkType: "district.tick",
            CorrelationId: "corr-ac4-1",
            CreatedUtc: new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

        // The cross-process boundary: this JSON string is what travels to the
        // Worker. The Worker never sees the WorkRecord type — only the bytes.
        var wire = JsonSerializer.Serialize(apiRecord);

        // Worker side: the real dispatcher, a capturing logger, and a handler
        // for the district-tick type.
        var logger = new CapturingLogger<WorkItemDispatcher>();
        var handlerRan = false;
        var dispatcher = new WorkItemDispatcher(logger, Handlers(
            ("district.tick", payload =>
            {
                handlerRan = true;
                return Task.CompletedTask;
            })));

        await dispatcher.DispatchAsync(wire);

        Assert.True(handlerRan, "The registered handler must run inside the dispatch.");

        // AC4: the Worker's log line for this work item carries the SAME id
        // the API host put on its outbox row — proving the id travelled with
        // the work, not over the HTTP header.
        Assert.Contains(
            logger.Entries,
            line => line.Contains("corr-ac4-1") && line.Contains("work-42"));
    }

    [Fact]
    public async Task The_worker_handler_runs_inside_the_correlation_scope()
    {
        // The handler itself logs — that log line must sit under the same
        // correlation scope, so an operator grepping the id finds the work's
        // own progress, not just the dispatch line.
        var wire = JsonSerializer.Serialize(new WorkRecord(
            "work-9", "store.restock", "corr-scope-2",
            new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero)));

        var logger = new CapturingLogger<WorkItemDispatcher>();
        var dispatcher = new WorkItemDispatcher(logger, Handlers(
            ("store.restock", payload =>
            {
                logger.LogInformation("Restocked store inventory.");
                return Task.CompletedTask;
            })));

        await dispatcher.DispatchAsync(wire);

        Assert.Contains(
            logger.Entries,
            line => line.Contains("Restocked store inventory.") && line.Contains("corr-scope-2"));
    }

    [Fact]
    public void A_serialized_payload_round_trips_every_field_across_the_boundary()
    {
        // The id is not the only field that must survive: the whole record
        // travels, and the mirror deserializes it losslessly.
        var record = new WorkRecord(
            "work-31", "offline.resolve", "corr-rt-3",
            new DateTimeOffset(2026, 11, 9, 6, 30, 0, TimeSpan.FromHours(-5)));

        var wire = JsonSerializer.Serialize(record);
        var payload = WorkItemPayload.Deserialize(wire);

        Assert.Equal(record.WorkItemId, payload.WorkItemId);
        Assert.Equal(record.WorkType, payload.WorkType);
        Assert.Equal(record.CorrelationId, payload.CorrelationId);
        Assert.Equal(record.CreatedUtc, payload.CreatedUtc);
    }

    [Fact]
    public async Task An_unknown_work_type_is_logged_not_thrown()
    {
        // The Worker must survive a type a newer host emits — log a warning
        // and skip, not crash the host loop.
        var wire = JsonSerializer.Serialize(new WorkRecord(
            "work-55", "future.type", "corr-unknown-4",
            DateTimeOffset.UnixEpoch));

        var logger = new CapturingLogger<WorkItemDispatcher>();
        var dispatcher = new WorkItemDispatcher(logger, Handlers()); // no handlers

        await dispatcher.DispatchAsync(wire);

        Assert.Contains(
            logger.Entries,
            line => line.Contains("future.type") && line.Contains("work-55"));
    }

    /// <summary>Records formatted messages with their active scope chain.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _lines = new();
        private readonly Stack<string> _scopes = new();

        public List<string> Entries =>
            _lines
                .Select(line =>
                {
                    var scopes = string.Join(" | ", _scopes.Reverse());
                    return scopes.Length == 0 ? line : $"{scopes} :: {line}";
                })
                .ToList();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _scopes.Push(state?.ToString() ?? string.Empty);
            return new Pop(_scopes);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = string.Join(" | ", _scopes.Reverse());
            var message = formatter(state, exception) +
                          (exception is null ? string.Empty : " :: " + exception);
            _lines.Add(scopes.Length == 0 ? message : $"{scopes} :: {message}");
        }

        private sealed class Pop(Stack<string> stack) : IDisposable
        {
            public void Dispose() => stack.Pop();
        }
    }
}
