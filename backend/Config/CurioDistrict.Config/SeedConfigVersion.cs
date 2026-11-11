namespace CurioDistrict.Config;

/// <summary>
/// A configuration profile's explicit version — the <c>configVersion</c>
/// field of a DEV_SEED profile (story 006, FND-006, AC1).
///
/// The version is always <em>stated</em> by the profile. It is never
/// inferred from any other field: story 006 AC1 requires an explicit version
/// under ADR-0020, so a profile that does not carry it is rejected
/// rather than guessed at. The runtime never derives <c>0.1</c> from
/// <c>metadata.version</c> ("0.1.0") or from a file name.
/// </summary>
/// <param name="Major">The major component of the version.</param>
/// <param name="Minor">The minor component of the version.</param>
public sealed record SeedConfigVersion(int Major, int Minor) : IComparable<SeedConfigVersion>
{
    /// <summary>
    /// The DEV_SEED v0.1 profile version — the version the imported doc 20 /
    /// doc 29 fixture carries.
    /// </summary>
    public static SeedConfigVersion V0_1 { get; } = new(0, 1);

    /// <summary>
    /// Parses a version string of the exact form <c>MAJOR.MINOR</c> (both
    /// components non-negative decimal integers, e.g. <c>"0.1"</c>).
    /// </summary>
    /// <param name="value">The raw text to parse.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="value"/> is not a parseable <c>MAJOR.MINOR</c> version.
    /// </exception>
    public static SeedConfigVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException(
                $"'{value}' is not a parseable config version (expected MAJOR.MINOR).");
        }

        return version;
    }

    /// <summary>
    /// Attempts to parse a version string of the exact form <c>MAJOR.MINOR</c>.
    /// </summary>
    /// <param name="value">The raw text to parse; may be null.</param>
    /// <param name="version">The parsed version, or <c>default</c> on failure.</param>
    /// <returns><c>true</c> when <paramref name="value"/> is a parseable version.</returns>
    public static bool TryParse(string? value, out SeedConfigVersion version)
    {
        version = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryParseComponent(parts[0], out var major) || !TryParseComponent(parts[1], out var minor))
        {
            return false;
        }

        version = new SeedConfigVersion(major, minor);
        return true;
    }

    /// <summary>
    /// Orders versions by major, then minor, so a loader can report which
    /// version it read and compare two profiles' versions.
    /// </summary>
    /// <param name="other">The version to compare against.</param>
    /// <returns>A negative number, zero, or a positive number.</returns>
    public int CompareTo(SeedConfigVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Major != other.Major)
        {
            return Major.CompareTo(other.Major);
        }

        return Minor.CompareTo(other.Minor);
    }

    /// <summary>Renders the version in its canonical <c>MAJOR.MINOR</c> form.</summary>
    public override string ToString() => $"{Major}.{Minor}";

    private static bool TryParseComponent(string part, out int value)
    {
        value = 0;

        if (part.Length == 0)
        {
            return false;
        }

        foreach (var character in part)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(
            part,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }
}
