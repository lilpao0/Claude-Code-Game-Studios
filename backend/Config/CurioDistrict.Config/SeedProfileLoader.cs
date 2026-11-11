using System.Text.Json;

namespace CurioDistrict.Config;

/// <summary>Loads any versioned DEV_SEED profile through the same validated JSON boundary.</summary>
public sealed class SeedProfileLoader
{
    /// <summary>Loads JSON, returning its explicitly stated version and all source data.</summary>
    /// <exception cref="SeedValidationException">The document or its content is invalid.</exception>
    public SeedProfile LoadJson(string json)
    {
        SeedProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<SeedProfile>(json, SeedProfileJson.Options);
        }
        catch (JsonException exception)
        {
            throw new SeedValidationException(new SeedValidationError(
                exception.Path ?? "$", $"Invalid profile JSON: {exception.Message}"));
        }

        if (profile is null)
            throw new SeedValidationException(new SeedValidationError("$", "Profile must be an object, not null."));

        var errors = SeedValidator.Validate(profile);
        if (errors.Count != 0) throw new SeedValidationException(errors);
        return profile with { ParsedConfigVersion = SeedConfigVersion.Parse(profile.ConfigVersion) };
    }

    /// <summary>Loads a profile file. File access failures remain ordinary I/O exceptions.</summary>
    public SeedProfile LoadFile(string path) => LoadJson(File.ReadAllText(path));
}
