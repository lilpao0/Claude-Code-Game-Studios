using System.Text;
using System.Text.Json;
using CurioDistrict.API.Errors;
using CurioDistrict.API.Health;
using CurioDistrict.API.Middleware;
using CurioDistrict.Data.Outbox;
using CurioDistrict.Tests.Unit;
using CurioDistrict.Worker.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CurioDistrict.Tests.Unit.Api;

/// <summary>
/// Story 008 (FND-009) — the error contract: one documented envelope
/// (AC1), the status→code table and its document agreeing (AC2), an
/// unhandled fault that leaks nothing (AC3), and the correlation id
/// reaching the Worker's log through the work record (AC4).
///
/// The middleware is exercised directly on a <see cref="DefaultHttpContext"/>:
/// each case builds a stub request pipeline whose single downstream
/// component throws the fault under test, runs
/// <see cref="ExceptionHandlingMiddleware"/>, and asserts on the recorded
/// response — status, envelope, and headers. This is the unit half of the
/// contract; /gate-check and /smoke-check carry it into the real host.
/// </summary>
public class ErrorEnvelopeTests
{
    private const string SecretMarker = "CURIODISTRICT_SECRET_MARKER_4f1a";

    /// <summary>The six documented error statuses AC1/AC2 pin.</summary>
    private static readonly int[] DocumentedStatuses =
        { 400, 401, 403, 409, 422, 503 };

    [Theory]
    [InlineData("bad id")]
    [InlineData("bad\r\nInjected: true")]
    [InlineData("non-ascii-\u00e9")]
    public void An_unsafe_correlation_header_is_replaced_with_a_generated_id(string supplied)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[Correlation.HeaderName] = supplied;
        var id = Correlation.ReadOrGenerate(request);
        Assert.NotEqual(supplied, id);
        Assert.True(Guid.TryParseExact(id, "N", out _));
    }

    [Fact]
    public void Multiple_correlation_headers_and_an_oversized_id_generate_a_new_id()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[Correlation.HeaderName] = new Microsoft.Extensions.Primitives.StringValues(
            new[] { "first-id", "second-id" });
        Assert.True(Guid.TryParseExact(Correlation.ReadOrGenerate(request), "N", out _));
        request.Headers[Correlation.HeaderName] = new string('a', 129);
        Assert.True(Guid.TryParseExact(Correlation.ReadOrGenerate(request), "N", out _));
    }

    // ------------------------------------------------------------------
    // AC1 — one documented envelope, with a correlationId, on every
    //         error response.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(503)]
    public void Every_documented_error_status_returns_the_same_envelope_shape(int status)
    {
        var response = RunPipeline(next: ThrowingPipeline(() =>
            throw new ApiException(status, $"documented error for {status}")));

        Assert.Equal(status, response.StatusCode);

        var envelope = ApiErrorJson.Parse(response.Body);
        Assert.NotNull(envelope);
        Assert.Equal(ApiErrorCodes.ForStatus(status), envelope!.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
        Assert.Null(envelope.Details);
    }

    [Fact]
    public void All_documented_errors_share_one_envelope_key_set()
    {
        // AC1's edge case, taken literally: two different error responses
        // must have the same envelope key set — a response that adds an
        // extra key for one status is a failure.
        var keySets = DocumentedStatuses
            .Select(status =>
            {
                var body = RunPipeline(ThrowingPipeline(() =>
                    throw new ApiException(status, "boom"))).Body;
                return JsonDocument.Parse(body).RootElement
                    .EnumerateObject()
                    .Select(p => p.Name)
                    .OrderBy(n => n)
                    .ToList();
            })
            .ToList();

        var first = keySets[0];
        foreach (var keys in keySets.Skip(1))
        {
            Assert.Equal(first, keys);
        }

        // ...and that key set is the documented one, not something larger.
        Assert.Equal(new[] { "code", "correlationId", "message" }, first);
    }

    [Fact]
    public void An_inbound_correlation_id_is_echoed_in_header_and_envelope()
    {
        const string supplied = "supplied-req-42";

        var response = RunPipeline(
            ThrowingPipeline(() => throw new ApiException(400, "bad")),
            inboundHeader: supplied);

        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.Equal(supplied, envelope.CorrelationId);
        Assert.Equal(supplied, response.Headers[Correlation.HeaderName].ToString());
    }

    [Fact]
    public void A_missing_inbound_correlation_id_is_generated_and_echoed()
    {
        var response = RunPipeline(ThrowingPipeline(() =>
            throw new ApiException(400, "bad")));

        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
        Assert.NotEqual(string.Empty, response.Headers[Correlation.HeaderName].ToString());
        Assert.Equal(envelope.CorrelationId, response.Headers[Correlation.HeaderName].ToString());
    }

    [Fact]
    public void A_blank_inbound_correlation_id_is_generated_and_echoed()
    {
        // Exercise the request boundary that generates the id. A JSON
        // serializer cannot mint one for a manually constructed record.
        var response = RunPipeline(
            ThrowingPipeline(() => throw new ApiException(400, "bad")),
            inboundHeader: "   ");
        using var doc = JsonDocument.Parse(response.Body);
        Assert.False(doc.RootElement.TryGetProperty("details", out _));
        var correlationId = doc.RootElement.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(correlationId, response.Headers[Correlation.HeaderName].ToString());
    }

    // ------------------------------------------------------------------
    // AC2 — the status→code table agrees with its documentation.
    // ------------------------------------------------------------------

    [Fact]
    public void The_status_to_code_table_is_stable_and_documented()
    {
        // One artifact in code: every documented status maps to exactly
        // one code; no two statuses share one; every code is a stable,
        // machine-readable, non-numeric identifier (never the raw HTTP
        // number, never a human sentence).
        var table = ApiErrorCodes.DocumentedTable;

        foreach (var status in DocumentedStatuses)
        {
            Assert.True(table.ContainsKey(status),
                $"Documented status {status} is missing from the code table.");
        }

        var codes = table.Values.ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());

        foreach (var code in codes)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", code);
            Assert.False(int.TryParse(code, out _),
                $"Code '{code}' is a raw number — clients must branch on stable identifiers.");
        }

        Assert.Equal("http_error", ApiErrorCodes.ForStatus(501));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiErrorCodes.ForStatus(200));
    }

    [Fact]
    public void The_table_and_its_documentation_agree()
    {
        // AC2's "when the two are compared": the table in source and the
        // documented mapping in the same file must contain exactly the
        // same status→code pairs — a status present in only one artifact
        // is a failure.
        var file = Path.Combine(TestPaths.BackendRoot, "API", "Errors", "ApiErrorCodes.cs");
        var text = File.ReadAllText(file);
        // The documented mapping lives in the file's doc comment, one
        // "  <status>  <code>" line per row. The line may or may not
        // carry a "/// " doc prefix, so tolerate an optional one. The
        // header ("status  code") and the "------" divider do not match
        // because their first column is not a 3-digit status.
        var line = new System.Text.RegularExpressions.Regex(
            @"^\s*(?:///\s*)?(\d{3})\s+([a-z][a-z0-9_]*)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        var documented = line
            .Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => (Status: int.Parse(m.Groups[1].Value), Code: m.Groups[2].Value));

        var table = ApiErrorCodes.DocumentedTable;

        foreach (var (status, code) in documented)
        {
            Assert.True(table.TryGetValue(status, out var actual),
                $"Documented status {status} has no table entry.");
            Assert.Equal(code, actual);
        }

        // The reverse direction: the table's non-500 entries are all
        // documented in the file, so neither artifact has a status the
        // other lacks.
        foreach (var (status, code) in table.Where(kv => kv.Key != 500))
        {
            Assert.Contains((status, code), documented.ToList());
        }
    }

    // ------------------------------------------------------------------
    // AC3 — an unhandled fault: 500 + correlation id, leaks nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void An_unhandled_exception_returns_500_with_the_envelope_and_leaks_nothing()
    {
        // The test-only fault the QA case describes: an exception whose
        // message carries a secret marker. The client must see none of it.
        var response = RunPipeline(ThrowingPipeline(
            () => throw new InvalidOperationException(
                $"{SecretMarker} stack-marker at some frame")));

        Assert.Equal(500, response.StatusCode);

        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.Equal("internal_error", envelope.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));

        // The marker, the exception type and the stack marker must not
        // appear anywhere in the WHOLE response — body and headers both.
        var wholeResponse = response.Body +
            string.Join(";", response.Headers.SelectMany(h => h.Value));

        Assert.DoesNotContain(SecretMarker, wholeResponse);
        Assert.DoesNotContain("InvalidOperationException", wholeResponse);
        Assert.DoesNotContain("stack-marker", wholeResponse);

        // And the operator-facing log DID carry the real fault under the
        // correlation id — that is where the details live.
        Assert.Contains(response.LoggedMessages,
            line => line.Contains(SecretMarker) &&
                    line.Contains(envelope.CorrelationId));
    }

    [Fact]
    public void The_503_documented_error_carries_its_table_code()
    {
        var response = RunPipeline(ThrowingPipeline(() =>
            throw new ApiException(503, ServiceUnavailableBody)));

        Assert.Equal(503, response.StatusCode);
        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.Equal("service_unavailable", envelope.Code);
    }

    private const string ServiceUnavailableBody = "The district service is restarting.";

    // ------------------------------------------------------------------
    // AC1 backstop — a status-only error (no exception thrown) is
    //         normalized to the documented envelope.
    // ------------------------------------------------------------------

    [Fact]
    public void A_downstream_status_only_503_that_writes_a_body_is_normalized_to_the_envelope()
    {
        // The shape the checkpoint names: a downstream component — the
        // health endpoint's 503 report — sets an error status and writes
        // its own body without throwing. The client must still receive
        // the documented envelope, not the free-form body.
        var response = RunPipeline(
            next: StatusOnlyPipeline(status: 503, body: ServiceUnavailableBody),
            inboundHeader: "req-503-normalized");

        Assert.Equal(503, response.StatusCode);

        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.Equal("service_unavailable", envelope.Code);
        Assert.Equal("req-503-normalized", envelope.CorrelationId);
        Assert.Equal(envelope.CorrelationId, response.Headers[Correlation.HeaderName].ToString());

        // The original free-form body no longer reaches the client.
        Assert.DoesNotContain(ServiceUnavailableBody, response.Body);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(503)]
    public void A_downstream_status_only_error_is_normalized_to_its_table_code(int status)
    {
        // AC1's "same envelope for 400/401/403/409/422/503" on the
        // status-only path, not just the exception path: no body is
        // written at all, the status code alone is what normalizes.
        var response = RunPipeline(StatusOnlyPipeline(status: status, body: string.Empty));

        Assert.Equal(status, response.StatusCode);

        var envelope = ApiErrorJson.Parse(response.Body)!;
        Assert.Equal(ApiErrorCodes.ForStatus(status), envelope.Code);
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
        Assert.Equal(envelope.CorrelationId, response.Headers[Correlation.HeaderName].ToString());
    }

    [Fact]
    public void A_healthy_200_response_passes_through_the_middleware_untouched()
    {
        // The 200 path — including the health endpoint's plain-text
        // "healthy" body (story 004's contract) — must survive the
        // middleware byte-for-byte: no envelope, no status change.
        const string healthyBody = "api healthy";

        var response = RunPipeline(StatusOnlyPipeline(status: 200, body: healthyBody));

        Assert.Equal(200, response.StatusCode);
        // The body is the plain-text summary verbatim — NOT rewritten into
        // the envelope. (Asserting equality, not parsing: the 200 body is
        // documented opaque plain text, not JSON.)
        Assert.Equal(healthyBody, response.Body);
    }

    // ------------------------------------------------------------------
    // AC1 real-host path — /health's 503 authors its own envelope.
    //         (The exception-middleware backstop is inert once Kestrel has
    //         completed the response, so the health endpoint writes the
    //         envelope itself; these cases drive that writer directly.)
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_health_503_answer_is_the_documented_envelope()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers[Correlation.HeaderName] = "health-503-corr";

        await HealthEndpoint.WriteHealthResponse(context, EmptyReport(HealthStatus.Unhealthy));

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        var envelope = ApiErrorJson.Parse(body)!;
        Assert.Equal("service_unavailable", envelope.Code);
        Assert.Equal(HealthEndpoint.UnavailableMessage, envelope.Message);
        // The id the middleware put on the header is the one in the envelope.
        Assert.Equal("health-503-corr", envelope.CorrelationId);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task The_health_503_without_middleware_mints_a_correlation_id_itself()
    {
        // A direct unit call — no exception middleware upstream — must not
        // emit an empty id: the writer mints one.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await HealthEndpoint.WriteHealthResponse(context, EmptyReport(HealthStatus.Degraded));

        var envelope = ApiErrorJson.Parse(Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()))!;
        Assert.False(string.IsNullOrWhiteSpace(envelope.CorrelationId));
    }

    [Fact]
    public async Task The_health_200_answer_keeps_story_004s_plain_text_contract()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = StatusCodes.Status200OK;

        await HealthEndpoint.WriteHealthResponse(context, EmptyReport(HealthStatus.Healthy));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/plain", context.Response.ContentType);
        // The 200 body is the opaque plain-text summary, NOT the envelope.
        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.False(body.Contains("correlationId"),
            "The healthy body must stay the opaque summary, not the error envelope.");
        Assert.Equal(HealthEndpoint.HealthySummary, body);
    }

    /// <summary>A check entry-less report with the status under test.</summary>
    private static HealthReport EmptyReport(HealthStatus status)
        => new(new Dictionary<string, HealthReportEntry>(), status, TimeSpan.Zero);

    // ------------------------------------------------------------------
    // AC4 — the correlation id reaches the Worker's log, riding the
    //         work record, not the HTTP header.
    // ------------------------------------------------------------------

    [Fact]
    public void A_work_record_carries_the_correlation_id_the_worker_logs_with()
    {
        // The mechanism AC4 pins: the id is a field on the outbox/event
        // record. A worker logging a work item inside the record's scope
        // stamps the line with the SAME id the API's response carried.
        const string correlationId = "req-to-worker-7";
        var record = new WorkRecord(
            WorkItemId: "work-1",
            WorkType: "district.tick",
            CorrelationId: correlationId,
            CreatedUtc: DateTimeOffset.UnixEpoch);

        var logger = new CapturingLogger();
        using (WorkItemLogging.OpenWorkItemScope(logger, record.WorkItemId, record.CorrelationId))
        {
            logger.LogInformation("Processed work item {WorkItemId}", record.WorkItemId);
        }

        Assert.Contains(
            logger.Entries,
            entry => entry.Contains(correlationId) && entry.Contains("work-1"));
    }

    [Fact]
    public void The_work_record_round_trips_its_correlation_id_through_json()
    {
        // The record's contract is its serialized shape — the outbox row
        // is JSON, and the id must survive the trip unchanged so a
        // restarted worker still correlates.
        var record = new WorkRecord("w-9", "store.restock", "corr-abc",
            new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(record);
        var back = JsonSerializer.Deserialize<WorkRecord>(json)!;

        Assert.Equal("corr-abc", back.CorrelationId);
    }

    // ------------------------------------------------------------------
    // Pipeline plumbing.
    // ------------------------------------------------------------------

    private static CapturedResponse RunPipeline(
        RequestDelegate next,
        string? inboundHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (inboundHeader is not null)
        {
            context.Request.Headers[Correlation.HeaderName] = inboundHeader;
        }

        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(next, logger);

        middleware.InvokeAsync(context).GetAwaiter().GetResult();

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        return new CapturedResponse(
            context.Response.StatusCode,
            body,
            context.Response.Headers,
            logger.Entries);
    }

    /// <summary>A pipeline whose single downstream component throws.</summary>
    private static RequestDelegate ThrowingPipeline(Action fault) =>
        async _ =>
        {
            fault();
            await Task.CompletedTask;
        };

    /// <summary>
    /// A pipeline whose single downstream component sets a response status
    /// and (optionally) writes a body WITHOUT throwing — the health
    /// endpoint's 503 report is this shape. <paramref name="body"/> is
    /// empty to model a status-only answer (no body written at all).
    /// </summary>
    private static RequestDelegate StatusOnlyPipeline(int status, string body) =>
        async context =>
        {
            context.Response.StatusCode = status;
            if (body.Length > 0)
            {
                await context.Response.WriteAsync(body, Encoding.UTF8);
            }
        };

    private sealed record CapturedResponse(
        int StatusCode,
        string Body,
        IHeaderDictionary Headers,
        List<string> LoggedMessages);

    /// <summary>
    /// A logger that records formatted messages and the scopes they were
    /// logged under — enough to assert AC3's "the operator log carries
    /// the fault under the correlation id" without a real provider.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _lines = new();
        private readonly Stack<string> _scopes = new();

        internal List<string> Entries => _lines;

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

    /// <summary>The logger for the AC4 worker-log case (non-generic site).</summary>
    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _lines = new();
        private readonly Stack<string> _scopes = new();

        public List<string> Entries => _lines;

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
            var message = formatter(state, exception);
            _lines.Add(scopes.Length == 0 ? message : $"{scopes} :: {message}");
        }

        private sealed class Pop(Stack<string> stack) : IDisposable
        {
            public void Dispose() => stack.Pop();
        }
    }
}
