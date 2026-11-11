namespace CurioDistrict.API.Errors;

/// <summary>
/// The exception an endpoint throws to authorize a *documented* error
/// response (story 008, FND-009). The global exception middleware catches
/// it and writes the single error envelope for its <see cref="StatusCode"/>,
/// with the machine-readable code looked up from
/// <see cref="ApiErrorCodes"/> — the server decides the outcome and authors
/// the body (ADR-0003).
///
/// <see cref="StatusCode"/> is the one status in the documented table
/// (400, 401, 403, 409, 422, 503 — and 500 when the caller wants to be
/// explicit). <see cref="Code"/> is an optional override for statuses that
/// need a code finer than the table's one; when null the middleware uses
/// the table's code.
///
/// A plain <see cref="Exception"/> (i.e. NOT this type) is an unhandled
/// fault: the middleware returns 500 with the generic message and leaks
/// nothing (AC3).
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>The documented status the response carries.</summary>
    public int StatusCode { get; }

    /// <summary>
    /// The envelope's machine-readable code. Null means "use the code the
    /// status→code table maps <see cref="StatusCode"/> to."
    /// </summary>
    public string? Code { get; }

    /// <summary>Creates an authored error using the status table's code.</summary>
    /// <param name="statusCode">The HTTP error status returned to the client.</param>
    /// <param name="message">The server-authored, client-safe message.</param>
    /// <param name="inner">Optional operator diagnostic context.</param>
    public ApiException(int statusCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    /// <summary>Creates an authored error with an optional specific machine code.</summary>
    /// <param name="statusCode">The HTTP error status returned to the client.</param>
    /// <param name="code">A stable machine code, or null to use the status table.</param>
    /// <param name="message">The server-authored, client-safe message.</param>
    /// <param name="inner">Optional operator diagnostic context.</param>
    public ApiException(int statusCode, string? code, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
