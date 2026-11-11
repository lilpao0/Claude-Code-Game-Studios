using System.Security;

namespace CurioDistrict.Tools.ContentValidator;

internal static class Program
{
    private const string USAGE = "Usage: ContentValidator [--file] <content.json> [[--file] <more.json> ...]";

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine(USAGE);
            return 0;
        }
        if (!TryReadPaths(args, out var paths))
        {
            Console.Error.WriteLine(USAGE);
            Console.Error.WriteLine("Supply at least one JSON file; --file requires one path. Unknown options are errors.");
            return 2;
        }

        var sources = new List<ContentSource>();
        var inputErrors = new List<ContentViolation>();
        foreach (var path in paths)
        {
            try { sources.Add(new(path, File.ReadAllText(path))); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             ArgumentException or NotSupportedException or SecurityException)
            {
                inputErrors.Add(new("Input", path, "$", $"Cannot read content file: {exception.Message}"));
            }
        }
        var result = new ContentSetValidator().Validate(sources);
        if (inputErrors.Count != 0)
            result = result with
            {
                Violations = Array.AsReadOnly(ContentValidationResult.Ordered(result.Violations.Concat(inputErrors)).ToArray())
            };
        Console.WriteLine(result.Format());
        return result.IsValid ? 0 : 1;
    }

    private static bool TryReadPaths(string[] args, out List<string> paths)
    {
        paths = [];
        for (var i = 0; i < args.Length; i++)
        {
            var path = args[i];
            if (path == "--file")
            {
                if (++i == args.Length) return false;
                path = args[i];
            }
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith('-')) return false;
            paths.Add(path);
        }
        return paths.Count != 0;
    }
}
