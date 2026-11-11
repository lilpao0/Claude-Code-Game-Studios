namespace CurioDistrict.Tests.Unit;

/// <summary>
/// Locates the repository root from the test assembly's location.
///
/// The check reads the backend project files and the boundary document, so it
/// needs the real tree — not a fixture — and must find it from any working
/// directory (CI checks out elsewhere than the author).
/// </summary>
internal static class TestPaths
{
    /// <summary>The <c>backend/</c> directory.</summary>
    public static string BackendRoot { get; } = FindUp("backend", from: AppContext.BaseDirectory);

    /// <summary>The repository root (the parent of <c>backend/</c>).</summary>
    public static string RepositoryRoot { get; } = Directory.GetParent(BackendRoot)!.FullName;

    private static string FindUp(string childDirectoryName, string from)
    {
        var directory = new DirectoryInfo(from);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, childDirectoryName);
            if (Directory.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate '{childDirectoryName}/' walking up from '{from}'.");
    }
}
