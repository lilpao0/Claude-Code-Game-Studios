using System.Text;
using CurioDistrict.API.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CurioDistrict.API.Health;

/// <summary>
/// The API health endpoint (story 004, FND-004).
///
/// <c>GET /health</c> returns 200 when the API can serve requests and 503
/// when it cannot. This is the endpoint the compose healthcheck calls and the
/// endpoint the CI pipeline (story 005) calls, so its contract must not drift
/// between the two.
///
/// The endpoint lives in the API host, not in the Domain: it reports on
/// infrastructure, and the Domain holds no knowledge of Postgres or of the
/// host process (ADR-0004).
///
/// Body contract (story 008 added the error half, FND-009 AC1): the
/// <em>healthy</em> body remains the plain-text summary — no JSON schema is
/// promised there, so callers treat it as opaque and rely on the status
/// code. The <em>error</em> (503) body is the documented error envelope
/// (AC1's "every error response uses the one documented envelope" covers
/// /health like any other route). The endpoint can be tested independently;
/// in the full pipeline the middleware buffers and normalizes error bodies
/// before committing them to Kestrel.
/// </summary>
public static class HealthEndpoint
{
    /// <summary>The path the health endpoint is served on.</summary>
    public const string Path = "/health";

    /// <summary>The one body the 503 answer's envelope carries (AC1).</summary>
    public const string UnavailableMessage = "The district service is unavailable.";

    /// <summary>
    /// The opaque plain-text summary the healthy answer carries (story 004's
    /// contract, preserved through story 008's envelope split). Callers
    /// read the status code, never this body.
    /// </summary>
    public const string HealthySummary = "api healthy";

    /// <summary>
    /// Maps the health endpoint. The response writer is the split documented
    /// above: healthy → plain-text summary; degraded/unhealthy → 503 + the
    /// documented error envelope.
    /// </summary>
    public static IEndpointRouteBuilder MapCurioDistrictHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            // The default writer already emits 503 when any check is
            // unhealthy, and 200 when they all pass. Being explicit keeps
            // the contract readable at the call site.
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },

            // Story 008 (AC1): the 503 answer is the documented envelope,
            // not the default plain-text report. The healthy answer keeps
            // story 004's opaque summary.
            ResponseWriter = WriteHealthResponse,
        });

        return endpoints;
    }

    /// <summary>
    /// The /health response writer (story 008, FND-009, AC1's real-host
    /// path). Split out of the mapping so the unit test drives both
    /// answers directly without a Kestrel host.
    ///
    /// The error answer's <c>correlationId</c> is the one the
    /// <see cref="Middleware.ExceptionHandlingMiddleware"/> already put on
    /// the response header for this request — the same id on the header and
    /// in the envelope, on every host. When the endpoint is reached without
    /// that middleware (a direct unit call, or a future non-enveloped
    /// pipeline) it is minted here instead, so the envelope never carries
    /// an empty id.
    /// </summary>
    public static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            // Story 004's contract, preserved: the healthy body is the
            // plain-text summary, opaque to callers — the compose
            // healthcheck and the CI pipeline read the status code only.
            // (The check's entry details are never promised here; the
            // status is the contract, this line is the summary.)
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                HealthySummary,
                Encoding.UTF8,
                context.RequestAborted);
            return;
        }

        var correlationId = context.Response.Headers[Correlation.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Correlation.NewCorrelationId();
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            ApiErrorJson.Write(new ApiErrorEnvelope(
                ApiErrorCodes.ForStatus(StatusCodes.Status503ServiceUnavailable),
                UnavailableMessage,
                correlationId!)),
            Encoding.UTF8,
            context.RequestAborted);
    }
}
