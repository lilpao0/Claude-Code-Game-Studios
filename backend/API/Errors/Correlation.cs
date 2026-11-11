namespace CurioDistrict.API.Errors;

using Microsoft.Extensions.Logging;

/// <summary>
/// Correlation-ID handling for the API host (story 008, FND-009, AC1/AC4).
///
/// Rule, per the story's implementation notes: read an inbound
/// <c>X-Correlation-Id</c> header when present, otherwise generate one.
/// Echo it in the response header AND in the error envelope, and push it
/// into the logging scope so every log line the host writes for this
/// request carries it.
///
/// AC4 is the cross-process half: because the Worker is a separate process
/// the id must travel WITH the work — it is a field on the work record
/// (the outbox/event record that EP-ECONOMY and EP-AUTOMATION's jobs will
/// consume) — never only in the HTTP header.
/// <see cref="WorkRecord"/> below is that field, defined on the record
/// now so the contract exists before the first job lands.
/// </summary>
public static class Correlation
{
    /// <summary>The inbound/outbound header name — one constant, both hosts.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Reads one trimmed ASCII token (letters, digits, dash, underscore,
    /// period or colon, at most 128 characters). Missing, blank, multiple or
    /// malformed values receive a freshly generated ID.
    /// Generated at the HOST (this process), never in Domain — a fresh
    /// correlation id is the same kind of ambient decision as a fresh
    /// entity id, and story 007's purity scan keeps Domain clean of it.
    /// </summary>
    public static string ReadOrGenerate(HttpRequest request)
    {
        var values = request.Headers[HeaderName];
        if (values.Count != 1) return NewCorrelationId();
        var supplied = values[0]?.Trim();
        // A bounded ASCII token is safe in response headers and log lines.
        // Multiple, oversized or malformed values get a fresh host ID.
        if (string.IsNullOrEmpty(supplied) || supplied.Length > 128 ||
            supplied.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.' and not ':'))
        {
            return NewCorrelationId();
        }
        return supplied;
    }

    /// <summary>Mints a correlation id. Host-only (see class remarks).</summary>
    public static string NewCorrelationId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Opens a logging scope stamped with the correlation id. The scope
    /// state is a plain formatted string keyed on the header name, so a
    /// grep for <c>X-Correlation-Id</c> finds the id in console and
    /// structured sinks alike (AC4's API half).
    /// </summary>
    public static IDisposable OpenScope(ILogger logger, string correlationId)
    {
        var scope = logger.BeginScope($"{HeaderName}: {correlationId}");
        // BeginScope may report "no provider" as null; an empty scope
        // satisfies the IDisposable contract without leaking.
        return scope ?? NoOpScope.Value;
    }

    private sealed class NoOpScope : IDisposable
    {
        internal static readonly NoOpScope Value = new();
        public void Dispose() { }
    }
}
