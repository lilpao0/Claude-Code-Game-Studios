namespace CurioDistrict.Config;

/// <summary>
/// One validation defect found in a DEV_SEED profile (story 006, FND-006,
/// AC2).
///
/// A defect always names the <em>field path</em> of the offending value —
/// the pool id and entry id for a loot weight, the item id and the cost
/// field for a negative cost, the referencing entry id and the unresolved
/// SKU id for a missing SKU. A message that does not carry the offending
/// identifier is a failure of this contract.
/// </summary>
/// <param name="FieldPath">
/// The path of the offending field, e.g.
/// <c>lootPools['POOL-DMO-S1'].rewards['DMO-05'].weight</c>.
/// </param>
/// <param name="Message">
/// What is wrong, naming the offending value.
/// </param>
public sealed record SeedValidationError(string FieldPath, string Message)
{
    /// <summary>Renders the defect as <c>path: message</c>.</summary>
    public override string ToString() => $"{FieldPath}: {Message}";
}

/// <summary>
/// The exception a profile load raises when the profile is invalid
/// (story 006, FND-006, AC2).
///
/// The exception carries <em>every</em> defect the validator could
/// enumerate — not only the first — so one load reports the whole picture.
/// </summary>
public sealed class SeedValidationException : Exception
{
    /// <summary>Creates the exception from the full set of defects.</summary>
    /// <param name="errors">Every defect found; at least one.</param>
    public SeedValidationException(IReadOnlyList<SeedValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Creates the exception for a single defect — the shape used for a
    /// malformed document, where parsing stops before validation can run.
    /// </summary>
    /// <param name="error">The defect found.</param>
    public SeedValidationException(SeedValidationError error)
        : this(new[] { error })
    {
    }

    /// <summary>Every defect found, in the order the validator reported them.</summary>
    public IReadOnlyList<SeedValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<SeedValidationError> errors)
    {
        var count = errors.Count;
        var detail = string.Join(
            "\n  ",
            errors.Select(error => $"{error.FieldPath}: {error.Message}"));

        return $"DEV_SEED profile is invalid — {count} defect(s) found:\n  {detail}";
    }
}
