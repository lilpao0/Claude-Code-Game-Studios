using System.Text.Json;
using System.Text.RegularExpressions;

namespace CurioDistrict.Tests.Unit.Fnd;

/// <summary>
/// Table ownership, loaded from <c>backend/Data/module-ownership.json</c>.
///
/// The JSON file is the machine-readable form of
/// <c>docs/architecture/module-boundaries.md</c> §3. A separate test asserts
/// the two agree, so the document cannot drift away from what is enforced.
/// </summary>
internal sealed class TableOwnership
{
    private readonly Dictionary<string, HashSet<string>> _tablesByModule;

    private TableOwnership(Dictionary<string, HashSet<string>> tablesByModule)
    {
        _tablesByModule = tablesByModule;
    }

    /// <summary>Loads the ownership map from the backend root.</summary>
    public static TableOwnership Load(string backendRoot)
    {
        var path = Path.Combine(backendRoot, "Data", "module-ownership.json");
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var module in doc.RootElement.GetProperty("modules").EnumerateObject())
        {
            var tables = module.Value.GetProperty("tables")
                .EnumerateArray()
                .Select(t => t.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            map[module.Name] = tables;
        }

        return new TableOwnership(map);
    }

    /// <summary>Module names that own at least one table.</summary>
    public IReadOnlyCollection<string> Modules => _tablesByModule.Keys;

    /// <summary>True when <paramref name="module"/> owns <paramref name="table"/>.</summary>
    public bool OwnsTable(string module, string table)
    {
        return _tablesByModule.TryGetValue(module, out var tables) && tables.Contains(table);
    }

    /// <summary>The table names a module owns.</summary>
    public IReadOnlyCollection<string> TablesOf(string module) =>
        _tablesByModule.TryGetValue(module, out var tables) ? tables : Array.Empty<string>();
}

/// <summary>Extracts candidate table names from Data-layer source text.</summary>
internal static partial class TableNames
{
    // Matches a table name used in a write path: DbSet<T> mapping, raw SQL,
    // or a .FromSql/ExecuteSql call naming a table.
    [GeneratedRegex(@"(?i)\b(?:FROM|INTO|UPDATE|JOIN)\s+[a-z_][a-z0-9_]*")]
    private static partial Regex RawSqlPattern();

    [GeneratedRegex(@"\bDbSet\s*<\s*([A-Za-z0-9_]+)\s*>")]
    private static partial Regex DbSetPattern();

    /// <summary>Table names referenced by write-shaped SQL in the given text.</summary>
    public static IEnumerable<string> ExtractedFrom(string text)
    {
        foreach (Match match in RawSqlPattern().Matches(text))
        {
            // The keyword is followed by the table name.
            var keyword = match.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            var table = match.Value[keyword.Length..].Trim();
            if (table.Length > 0) yield return table.ToLowerInvariant();
        }
    }

    /// <summary>Entity type names used as DbSet roots (informational).</summary>
    public static IEnumerable<string> DbSetEntities(string text)
    {
        foreach (Match match in DbSetPattern().Matches(text))
        {
            yield return match.Groups[1].Value;
        }
    }
}
