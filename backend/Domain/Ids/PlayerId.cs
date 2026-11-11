namespace CurioDistrict.Domain.Ids;

/// <summary>
/// Identifies a player (the owner of a shop). Story 007, FND-008.
///
/// A distinct struct over <see cref="IEntityId"/>, not a <c>Guid</c>: a
/// <c>PlayerId</c> is not interchangeable with a <c>StoreId</c> or an
/// <c>ItemId</c>, and the compiler enforces that. ADR-0006 keeps the Steam
/// account and the primary shop mapped one-to-one to a player — this is the
/// player's own key, and it stays distinct from any Steam-side id.
/// </summary>
public readonly record struct PlayerId(Guid Value) : IEntityId
{
    /// <summary>An id that represents no player; use only at construction time.</summary>
    public static readonly PlayerId None = new(Guid.Empty);

    /// <remarks>
    /// A new id is a *randomness* decision, so it is made outside Domain —
    /// the host or Data layer mints the <c>Guid</c> (or replaying a
    /// persisted seed) and hands the id in. Domain carries ids but does
    /// not generate them: a <c>Guid.NewGuid()</c> call here would be
    /// ambient randomness bypassing <see cref="Ports.IRng"/>, which the
    /// purity scan for this story forbids.
    /// </remarks>

    /// <summary>
    /// Explicit conversions only: a raw <c>Guid</c> must be cast deliberately
    /// (hydration from a persisted value), never coerced, and a <c>PlayerId</c>
    /// must be explicitly widened back. No implicit path between them, so a
    /// raw id can never leak into a Domain signature unexamined.
    /// </summary>
    public static explicit operator Guid(PlayerId id) => id.Value;
    public static explicit operator PlayerId(Guid value) => new(value);
}
