using System.Xml.Linq;
using CurioDistrict.Tests.Unit;

namespace CurioDistrict.Tests.Unit.Fnd;

/// <summary>
/// Story 002 AC2 + AC4 — the module-boundary check (docs/architecture/module-boundaries.md).
///
/// These tests make the boundary an *automated* rule, not a convention:
///  1. Project-reference edges must match the allowed table (AC4).
///  2. Domain must be BCL-only — no EF / Steam / hosting (AC2).
///  3. Data-layer table writes must stay inside the owning module (AC4, the
///     epic Definition-of-Done item). Vacuously true until repositories land.
///
/// A seeded forbidden edge turns this suite red and names the edge.
/// </summary>
public class ModuleBoundaryTests
{
    // Resolve the backend root relative to the test assembly so the check runs
    // from any working directory (CI checks out elsewhere).
    private static string BackendRoot =>
        Path.GetFullPath(Path.Combine(TestPaths.BackendRoot));

    // ADR-0004 allowed project-reference edges: From -> set of allowed To.
    // Anything else is a forbidden edge.
    private static readonly Dictionary<string, HashSet<string>> AllowedProjectEdges = new()
    {
        ["Domain"] = new HashSet<string>(),
        ["Config/CurioDistrict.Config"] = new HashSet<string>(),
        ["Tools/ContentValidator"] = new HashSet<string>(),
        ["Data"]   = new HashSet<string> { "Domain", "Config" },
        ["API"]    = new HashSet<string> { "Domain", "Data", "Config" },
        ["Worker"] = new HashSet<string> { "Domain" },
    };

    // Infrastructure that must never leak into Domain (AC2 / manifest Forbidden).
    private static readonly string[] ForbiddenDomainReferences =
    {
        "EntityFrameworkCore",
        "Npgsql",
        "Sqlite",
        "Steam",
        "Microsoft.Extensions.Hosting",
    };

    [Fact]
    public void Project_reference_edges_match_the_allowed_table()
    {
        var violations = new List<string>();

        foreach (var kv in AllowedProjectEdges)
        {
            var projectDir = Path.Combine(BackendRoot, kv.Key);
            var csproj = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
            Assert.NotNull(csproj);

            var doc = XDocument.Load(csproj!);
            var references = doc.Descendants("ProjectReference")
                .Select(r => r.Attribute("Include")?.Value)
                .Where(v => v != null)
                .ToList();

            foreach (var reference in references)
            {
                // "..\Domain\CurioDistrict.Domain.csproj" -> the target module.
                var targetFile = Path.GetFileName(reference!.Replace('\\', '/'));
                var target = targetFile.Replace("CurioDistrict.", "").Replace(".csproj", "");

                if (target == kv.Key) continue; // self-reference: impossible, but explicit

                if (!kv.Value.Contains(target))
                {
                    violations.Add($"{kv.Key} -> {target}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Forbidden project-reference edge(s) introduced — not in the allowed table in " +
            "docs/architecture/module-boundaries.md:\n  " +
            string.Join("\n  ", violations));
    }

    [Fact]
    public void Domain_references_nothing_but_the_BCL()
    {
        var csproj = Path.Combine(BackendRoot, "Domain", "CurioDistrict.Domain.csproj");
        var doc = XDocument.Load(csproj);

        // AC2: no PackageReference and no ProjectReference at all in Domain.
        var packageRefs = doc.Descendants("PackageReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(v => v != null)
            .ToList();
        var projectRefs = doc.Descendants("ProjectReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(v => v != null)
            .ToList();

        Assert.True(
            packageRefs.Count == 0 && projectRefs.Count == 0,
            "Domain references infrastructure it must not (ADR-0004: Domain is BCL-only).\n" +
            "PackageReferences: " + string.Join(", ", packageRefs) + "\n" +
            "ProjectReferences: " + string.Join(", ", projectRefs));

        // No forbidden namespace in Domain source, even without the assembly reference.
        var hits = new List<string>();
        foreach (var sourceFile in new DirectoryInfo(Path.Combine(BackendRoot, "Domain"))
                     .EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadAllLines(sourceFile.FullName))
            {
                if (!line.TrimStart().StartsWith("using")) continue;
                foreach (var token in ForbiddenDomainReferences)
                {
                    if (line.Contains(token, StringComparison.Ordinal))
                    {
                        hits.Add($"{Path.GetFileName(sourceFile.FullName)}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            hits.Count == 0,
            "Domain source imports an infrastructure namespace:\n  " +
            string.Join("\n  ", hits));
    }

    [Fact]
    public void Data_table_writes_stay_inside_the_owning_module()
    {
        // Epic Definition-of-Done item. Until repositories are written this
        // passes vacuously — the check still owns the rule and will fire the
        // moment a repository lands.
        var ownership = TableOwnership.Load(BackendRoot);

        var violations = new List<string>();
        var repositoriesRoot = Path.Combine(BackendRoot, "Data", "Repositories");

        // Until story 007 writes the first repository the directory does not
        // exist: the rule holds vacuously, and this check returns without
        // throwing. The moment a repository lands under Data/Repositories,
        // the enumeration below runs and the rule is enforced for real.
        if (!Directory.Exists(repositoriesRoot))
        {
            return;
        }

        foreach (var repoFile in new DirectoryInfo(repositoriesRoot)
                     .EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repositoriesRoot, repoFile.FullName);
            var module = relative.Split(Path.DirectorySeparatorChar)[0];

            var text = File.ReadAllText(repoFile.FullName);
            foreach (var table in TableNames.ExtractedFrom(text))
            {
                if (!ownership.OwnsTable(module, table))
                {
                    violations.Add($"{module} repository writes table '{table}', owned by another module");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Cross-module table write outside the sanctioned repository (module-boundaries.md §3):\n  " +
            string.Join("\n  ", violations));
    }
}
