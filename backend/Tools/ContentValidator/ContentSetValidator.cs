using System.Text.Json;

namespace CurioDistrict.Tools.ContentValidator;

/// <summary>Validates a complete set of full or partial DEV_SEED content documents without I/O.</summary>
public sealed class ContentSetValidator
{
    /// <summary>
    /// Enumerates all inputs and all discoverable defects. Malformed documents do not prevent
    /// checking the remaining documents. The input documents and caller's objects are never changed.
    /// </summary>
    public ContentValidationResult Validate(IEnumerable<ContentSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var state = new ContentValidationState();
        foreach (var source in sources.OrderBy(s => s?.FilePath, StringComparer.Ordinal)
                     .ThenBy(s => s?.Json, StringComparer.Ordinal))
        {
            if (source is null)
            {
                state.Add("Input", "<input>", "$", "Content source must not be null.");
                continue;
            }
            var file = string.IsNullOrWhiteSpace(source.FilePath) ? "<input>" : source.FilePath;
            if (source.Json is null)
            {
                state.Add("Input", file, "$", "Content JSON must not be null.");
                continue;
            }
            ReadDocument(source.Json, file, state);
        }
        return state.Complete();
    }

    private static void ReadDocument(string json, string file, ContentValidationState state)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            new ContentDocumentReader(file, state).Read(document.RootElement);
        }
        catch (JsonException exception)
        {
            state.Add("Parse", file, exception.Path ?? "$", $"Malformed JSON: {exception.Message}");
        }
    }
}

internal sealed class ContentValidationState
{
    private readonly List<ContentViolation> _violations = [];
    private readonly Dictionary<string, List<Declaration>> _declarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _typedIds = new(StringComparer.Ordinal);
    private readonly List<Reference> _references = [];

    internal int PoolCount { get; set; }

    internal void Add(string kind, string file, string path, string message) =>
        _violations.Add(new(kind, file, path, message));

    internal void Declare(string type, string id, string file, string path)
    {
        if (!_declarations.TryGetValue(id, out var declarations))
            _declarations[id] = declarations = [];
        declarations.Add(new(type, file, path));
        if (!_typedIds.TryGetValue(type, out var ids))
            _typedIds[type] = ids = new(StringComparer.Ordinal);
        ids.Add(id);
    }

    internal void Refer(string type, string? target, string file, string path, string description) =>
        _references.Add(new(type, target, file, path, description));

    internal ContentValidationResult Complete()
    {
        if (PoolCount != 4)
            Add("PoolCount", "<content-set>", "$.lootPools",
                FormattableString.Invariant($"Expected exactly 4 loot pools, found {PoolCount}."));
        FindDuplicateDeclarations();
        foreach (var reference in _references)
            if (string.IsNullOrWhiteSpace(reference.Target) ||
                !_typedIds.TryGetValue(reference.Type, out var ids) || !ids.Contains(reference.Target))
                Add("Reference", reference.File, reference.Path,
                    $"{reference.Description}: unresolved {reference.Type} '{reference.Target ?? "<missing>"}'.");
        return new(PoolCount, Array.AsReadOnly(ContentValidationResult.Ordered(_violations).ToArray()));
    }

    private void FindDuplicateDeclarations()
    {
        foreach (var (id, declarations) in _declarations)
        {
            var ordered = declarations.OrderBy(d => d.File, StringComparer.Ordinal)
                .ThenBy(d => d.Path, StringComparer.Ordinal).ThenBy(d => d.Type, StringComparer.Ordinal).ToArray();
            for (var i = 1; i < ordered.Length; i++)
            {
                var first = ordered[0];
                var next = ordered[i];
                Add("DuplicateId", next.File, next.Path,
                    $"Duplicate ID '{id}' declared by {first.Type} at '{first.File}:{first.Path}' " +
                    $"and {next.Type} at '{next.File}:{next.Path}'; declarations share one global namespace.");
            }
        }
    }

    private sealed record Declaration(string Type, string File, string Path);
    private sealed record Reference(string Type, string? Target, string File, string Path, string Description);
}
