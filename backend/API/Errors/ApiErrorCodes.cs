using Microsoft.AspNetCore.Http;

namespace CurioDistrict.API.Errors;

/// <summary>
/// The documented status→code table (story 008, FND-009, AC2) — ONE data
/// structure in one place, per the implementation notes: the table is the
/// single source of truth, and a client must branch on <c>code</c>, never
/// on the human <c>message</c> or on the status alone.
///
/// Documented mapping (doc 13's API contract, fixed by this story):
///
///   status  code
///   ------  -------------------
///   400     bad_request
///   401     unauthorized
///   403     forbidden
///   404     not_found
///   405     method_not_allowed
///   409     conflict
///   422     unprocessable_request
///   500     internal_error
///   503     service_unavailable
///
/// Other 400–599 statuses use the stable fallback code http_error; their HTTP
/// status is preserved. Non-error statuses are rejected by ForStatus.
///
/// Invariants (pinned by the AC2 unit test in
/// <c>backend/tests/unit/api/error_envelope_test.cs</c>): each status maps
/// to exactly one code; no two statuses share a code; every code is a
/// stable, non-numeric, lowercase-identifier string.
/// </summary>
public static class ApiErrorCodes
{
    /// <summary>
    /// Specific status mappings. An error status absent from the table is
    /// handled by the documented http_error fallback rather than an
    /// endpoint inventing its own code.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> Table =
        new Dictionary<int, string>
        {
            [StatusCodes.Status400BadRequest] = "bad_request",
            [StatusCodes.Status401Unauthorized] = "unauthorized",
            [StatusCodes.Status403Forbidden] = "forbidden",
            [StatusCodes.Status404NotFound] = "not_found",
            [StatusCodes.Status405MethodNotAllowed] = "method_not_allowed",
            [StatusCodes.Status409Conflict] = "conflict",
            [StatusCodes.Status422UnprocessableEntity] = "unprocessable_request",
            [StatusCodes.Status500InternalServerError] = "internal_error",
            [StatusCodes.Status503ServiceUnavailable] = "service_unavailable",
        };

    /// <summary>The documented mapping, exposed read-only.</summary>
    public static IReadOnlyDictionary<int, string> DocumentedTable => Table;

    /// <summary>
    /// Resolves the stable machine-readable code for a documented status.
    /// Uses http_error for other error statuses, preserving their envelope.
    /// A more specific code is added to the table and documentation together.
    /// </summary>
    public static string ForStatus(int status)
    {
        if (!Table.TryGetValue(status, out var code))
        {
            if (status is >= 400 and <= 599) return "http_error";
            throw new ArgumentOutOfRangeException(nameof(status), status,
                "An error status must be between 400 and 599.");
        }

        return code;
    }

    /// <summary>
    /// The one code used when the envelope is written for an unhandled
    /// exception (AC3). Named out of <see cref="ForStatus"/> so the call
    /// site documents intent instead of a raw status number.
    /// </summary>
    public static string Unhandled => ForStatus(StatusCodes.Status500InternalServerError);
}
