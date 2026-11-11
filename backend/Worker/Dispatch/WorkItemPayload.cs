namespace CurioDistrict.Worker.Dispatch;

using System.Text.Json;

/// <summary>
/// The Worker's view of one outbox/event work item that crossed the process
/// boundary from the API host (story 008, FND-009, AC4).
///
/// The wire contract is the JSON the API host's outbox row
/// (<c>CurioDistrict.Data.Outbox.WorkRecord</c>) serializes to. This record
/// mirrors those field names and order byte-for-byte, so a payload the API
/// host produced deserializes into it unchanged. The <see cref="CorrelationId"/>
/// is a field ON this payload — that is the whole point of AC4: the id travels
/// WITH the work, not over the HTTP header, so a restarted Worker still
/// correlates its log lines to the originating request.
///
/// This type lives in the Worker, not in Data: the Worker depends on Domain
/// only (ADR-0004) and reaches the API host's persistence through the
/// serialized payload — never through a project reference.
/// </summary>
public sealed record WorkItemPayload(
    string WorkItemId,
    string WorkType,
    string CorrelationId,
    DateTimeOffset CreatedUtc)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Property names travel the wire as-is (camelCase off, PascalCase
        // matching the API host's <c>WorkRecord</c> serialization default),
        // so the two sides share one byte-level shape.
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>
    /// Deserializes one cross-process payload. The payload is the JSON the
    /// API host's outbox row was written as; field names match
    /// <c>WorkRecord</c> so the trip is lossless.
    /// </summary>
    public static WorkItemPayload Deserialize(string json)
    {
        var payload = JsonSerializer.Deserialize<WorkItemPayload>(json, Options);
        if (payload is null || string.IsNullOrWhiteSpace(payload.WorkItemId) ||
            string.IsNullOrWhiteSpace(payload.WorkType) || string.IsNullOrWhiteSpace(payload.CorrelationId) ||
            payload.CreatedUtc == default)
        {
            throw new JsonException("The work-item payload requires its identifiers, type and creation time.");
        }

        return payload;
    }
}

