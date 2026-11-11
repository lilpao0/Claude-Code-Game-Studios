using System.Text.Json;
using System.Text.Json.Serialization;

namespace CurioDistrict.API.Errors;

/// <summary>
/// The single documented error envelope (story 008, FND-009, AC1).
///
/// Every error response body — each documented status from
/// <see cref="ApiErrorCodes"/>, and every unhandled exception — serializes
/// to THIS shape, and to nothing else: clients parse the envelope and
/// branch on <see cref="Code"/>. The human <see cref="Message"/> is not
/// part of the contract; a response that adds an extra key for one status
/// is a failure (AC1's edge case), so the key set is fixed here:
///
///   { "code", "message", "correlationId", "details?" }
///
/// <see cref="Details"/> is the only optional member; it is null on every
/// response in this story and is then omitted from the JSON, so the
/// serialized key set stays { code, message, correlationId }. The
/// secret-token response schema — the one place a structured detail is
/// promised — is deliberately left to story 011 (ADR-0008's open item).
///
/// ADR-0003 / ADR-0008: the server authors this body, not the client; and
/// AC3 forbids putting a stack trace, an exception type name, a
/// connection string or any secret-bearing configuration value in it.
/// The only writer is <see cref="ApiErrorJson"/> — the serializer options
/// live in one place so no endpoint can drift the shape.
/// </summary>
public sealed record ApiErrorEnvelope(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("details")] JsonElement? Details = null);

/// <summary>
/// The one serializer options instance every error response uses
/// (AC1: one documented envelope on every error response — "one" means
/// one writer, too). Property names are pinned by the <see cref="
/// JsonPropertyName"/> attributes on the record; null details are omitted
/// so a response never carries an empty <c>"details"</c> key.
/// </summary>
public static class ApiErrorJson
{
    /// <summary>The error-envelope options: camelCase-free, fixed names.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>Writes the envelope body. The only path to a JSON error body.</summary>
    public static string Write(ApiErrorEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, Options);

    /// <summary>Parses an error body back into the envelope.</summary>
    public static ApiErrorEnvelope? Parse(string json) =>
        JsonSerializer.Deserialize<ApiErrorEnvelope>(json, Options);
}
