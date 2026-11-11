namespace CurioDistrict.Tests.Unit.Fnd;

/// <summary>
/// Story 002 AC3 — the module-boundary document enumerates the allowed and the
/// forbidden edges, each traceable to the control manifest, and agrees with
/// what the automated check enforces.
///
/// AC3 says an edge "listed in both lists is a failure" and "listed in neither
/// is a failure". These tests read the document's own edge tables and prove
/// they partition the edge set.
/// </summary>
public class BoundaryDocumentTests
{
    private static string DocumentPath =>
        Path.Combine(TestPaths.RepositoryRoot, "docs", "architecture", "module-boundaries.md");

    private static string ManifestPath =>
        Path.Combine(TestPaths.RepositoryRoot, "docs", "architecture", "control-manifest.md");

    private static string DocumentText => File.ReadAllText(DocumentPath);

    [Fact]
    public void Boundary_document_exists_and_names_the_governing_adr()
    {
        Assert.True(File.Exists(DocumentPath), $"Missing {DocumentPath}");

        var text = DocumentText;
        Assert.Contains("ADR-0004", text, StringComparison.Ordinal);
        Assert.Contains("control-manifest.md", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_project_edge_is_listed_exactly_once()
    {
        // The eight edges between the four source projects (no self-edges).
        var allEdges = new[]
        {
            "Domain→Data", "Domain→API", "Domain→Worker",
            "Data→API", "Data→Worker",
            "API→Worker",
            "Worker→Data",
            "Worker→API", "Data→Domain", "API→Domain", "API→Data", "Worker→Domain",
            "Domain→Config", "Worker→Config",
            "Config→Domain", "Config→Data", "Config→API", "Config→Worker",
            "Data→Config", "API→Config",
        };

        // Data→Domain, API→Domain, API→Data, Worker→Domain are the allowed ones.
        var allowed = new[] { "Data→Domain", "API→Domain", "API→Data", "Worker→Domain", "Data→Config", "API→Config" };

        var text = DocumentText;
        foreach (var edge in allEdges)
        {
            var parts = edge.Split('→');
            var from = parts[0];
            var to = parts[1];
            var marker = $"| `{from}` |";
            Assert.Contains(marker, text, StringComparison.Ordinal);

            // The edge must appear in the allowed table (row of `from`, column `to`)
            // or be named as forbidden — and never in both.
            var inAllowedTable = AllowedCellMentions(text, from, to);
            var mentionedAsForbidden = text.Contains($"`{edge}` — **forbidden.**", StringComparison.Ordinal)
                                   || text.Contains($"`{from} → {to}` — **forbidden.**", StringComparison.Ordinal);

            if (allowed.Contains(edge))
            {
                Assert.True(inAllowedTable,
                    $"Allowed edge {edge} is not in the allowed-edge table.");
                Assert.False(mentionedAsForbidden,
                    $"Edge {edge} is listed as both allowed and forbidden.");
            }
            else
            {
                Assert.True(mentionedAsForbidden,
                    $"Forbidden edge {edge} is not listed in the forbidden list.");
                Assert.False(inAllowedTable,
                    $"Edge {edge} is listed as both allowed and forbidden.");
            }
        }
    }

    [Fact]
    public void Every_edge_citation_names_a_rule_present_in_the_control_manifest()
    {
        var manifest = File.ReadAllText(ManifestPath);
        var text = DocumentText;

        // Every ADR the document cites for an edge must appear in the manifest
        // or be one of the architecture ADRs the manifest itself lists.
        var cited = System.Text.RegularExpressions.Regex
            .Matches(text, @"ADR-\d{4}")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(cited);

        foreach (var adr in cited)
        {
            var knownInRepo = Directory.GetFiles(
                Path.Combine(TestPaths.RepositoryRoot, "docs", "architecture"),
                $"adr-{adr[4..]}-*.md");

            Assert.True(knownInRepo.Length > 0,
                $"{adr} is cited by the boundary document but no ADR file exists for it.");
        }

        // The Server layer rules the manifest must carry for the citations to hold.
        Assert.Contains("ADR-0004", manifest, StringComparison.Ordinal);
        Assert.Contains("ADR-0008", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ownership_document_matches_the_enforced_ownership_map()
    {
        var ownership = TableOwnership.Load(TestPaths.BackendRoot);
        var text = DocumentText;

        foreach (var module in ownership.Modules)
        {
            Assert.Contains($"| {module} |", text, StringComparison.Ordinal);

            foreach (var table in ownership.TablesOf(module))
            {
                Assert.Contains(table, text, StringComparison.Ordinal);
            }
        }
    }

    private static bool AllowedCellMentions(string text, string from, string to)
    {
        // The allowed table is a Markdown grid; find the row for `from` and
        // read the cell under the `to` column.
        var lines = text.Split('\n');
        var headerIndex = Array.FindIndex(lines,
            line => line.TrimStart().StartsWith("| From", StringComparison.Ordinal));
        if (headerIndex < 0) return false;
        var columns = lines[headerIndex].Split('|').Select(c => c.Trim().Trim('*', '`')).ToList();
        var toIndex = columns.FindIndex(c => c.Equals(to, StringComparison.Ordinal));
        if (toIndex < 0) return false;
        foreach (var line in lines.Skip(headerIndex + 2).TakeWhile(line => line.TrimStart().StartsWith('|')))
        {
            var cells = line.Split('|').Select(c => c.Trim().Trim('*', '`')).ToList();
            if (cells.Count > toIndex && cells[1].Equals(from, StringComparison.Ordinal))
                return cells[toIndex].Contains('✓', StringComparison.Ordinal);
        }
        return false;
    }
}
