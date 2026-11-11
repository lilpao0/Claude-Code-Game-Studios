using System.Text;
using CurioDistrict.API.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CurioDistrict.API.Middleware;

/// <summary>
/// Wraps the API pipeline with correlation logging and the documented error
/// envelope. Responses are buffered until downstream completes so even an
/// endpoint that writes and flushes an error body can be normalized on Kestrel.
/// This pipeline currently serves finite responses; streaming endpoints require
/// a separate response policy before they can be added.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>The generic client message for an unhandled fault.</summary>
    public const string UnhandledMessage = "An unexpected error occurred.";

    private const string StatusOnlyMessage = "The request could not be completed.";

    /// <summary>Runs downstream without committing an unnormalized error body.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Correlation.ReadOrGenerate(context.Request);
        context.Response.Headers[Correlation.HeaderName] = correlationId;
        using var scope = Correlation.OpenScope(logger, correlationId);

        var originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;
        try
        {
            try
            {
                await next(context);
                if (context.Response.StatusCode is >= 400 and <= 599)
                {
                    await WriteEnvelope(context, correlationId, context.Response.StatusCode, null,
                        StatusOnlyMessage);
                }
            }
            catch (ApiException documented)
            {
                await WriteEnvelope(context, correlationId, documented.StatusCode,
                    documented.Code, documented.Message);
            }
            catch (Exception fault)
            {
                logger.LogError(fault, "Unhandled fault on {Path} — correlation {CorrelationId}",
                    context.Request.Path, correlationId);
                await WriteEnvelope(context, correlationId, StatusCodes.Status500InternalServerError,
                    null, UnhandledMessage);
            }

            bufferedBody.Position = 0;
            await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            // Restore the server's stream even if downstream or the client disconnects.
            context.Response.Body = originalBody;
        }
    }

    private static async Task WriteEnvelope(HttpContext context, string correlationId,
        int statusCode, string? codeOverride, string message)
    {
        var envelope = new ApiErrorEnvelope(codeOverride ?? ApiErrorCodes.ForStatus(statusCode),
            message, correlationId);
        var allowedMethods = statusCode == StatusCodes.Status405MethodNotAllowed
            ? context.Response.Headers.Allow.ToString() : null;
        context.Response.Clear();
        context.Response.Body.SetLength(0);
        context.Response.Body.Position = 0;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers[Correlation.HeaderName] = correlationId;
        if (!string.IsNullOrEmpty(allowedMethods)) context.Response.Headers.Allow = allowedMethods;
        await context.Response.WriteAsync(ApiErrorJson.Write(envelope), Encoding.UTF8,
            context.RequestAborted);
    }
}
