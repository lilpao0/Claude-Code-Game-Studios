using System.Globalization;
using System.Text;

namespace CurioDistrict.Tools.ContentValidator;

/// <summary>One in-memory content document; validation itself performs no file I/O.</summary>
public sealed record ContentSource(string FilePath, string Json);

/// <summary>A machine-readable finding with source location and an identifier-bearing explanation.</summary>
public sealed record ContentViolation(string Kind, string FilePath, string JsonPath, string Message);

/// <summary>The complete result for the supplied content set, including its observed pool count.</summary>
public sealed record ContentValidationResult(int PoolCount, IReadOnlyList<ContentViolation> Violations)
{
    /// <summary>True only when no input, schema or semantic violation was found.</summary>
    public bool IsValid => Violations.Count == 0;

    /// <summary>Renders all findings grouped and ordered ordinally, independent of the current culture.</summary>
    public string Format()
    {
        var output = new StringBuilder();
        output.Append(IsValid ? "PASS" : "FAIL")
            .Append(": content set; pools=").Append(PoolCount.ToString(CultureInfo.InvariantCulture))
            .Append("; violations=").Append(Violations.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var group in Ordered(Violations).GroupBy(v => v.Kind, StringComparer.Ordinal))
        {
            output.Append('\n').Append('[').Append(group.Key).Append(']');
            foreach (var violation in group)
                output.Append('\n').Append(violation.FilePath).Append(':').Append(violation.JsonPath)
                    .Append(": ").Append(violation.Message);
        }
        return output.ToString();
    }

    internal static IOrderedEnumerable<ContentViolation> Ordered(IEnumerable<ContentViolation> violations) =>
        violations.OrderBy(v => v.Kind, StringComparer.Ordinal)
            .ThenBy(v => v.FilePath, StringComparer.Ordinal)
            .ThenBy(v => v.JsonPath, StringComparer.Ordinal)
            .ThenBy(v => v.Message, StringComparer.Ordinal);
}
