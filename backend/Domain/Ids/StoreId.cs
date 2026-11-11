namespace CurioDistrict.Domain.Ids;

/// <summary>
/// Identifies a player's shop (the curio district's display case).
/// Story 007, FND-008.
///
/// Distinct from every other entity's key: a <c>StoreId</c> passed where a
/// <c>PlayerId</c> is expected is a compile error, not a silently-wrong row
/// lookup. ADR-0006 maps one Steam account to one primary shop — the shop is
/// its own entity, keyed by its own id, never by the player's.
/// </summary>
public readonly record struct StoreId(Guid Value) : IEntityId
{
    /// <summary>An id that represents no shop; use only at construction time.</summary>
    public static readonly StoreId None = new(Guid.Empty);

    /// <remarks>
    /// A new id is a *randomness* decision, so it is made outside Domain —
    /// the host or Data layer mints the <c>Guid</c> (or replays a
    /// persisted seed) and hands the id in. Domain carries ids but does
    /// not generate them: a <c>Guid.NewGuid()</c> call here would be
    /// ambient randomness bypassing <see cref="Ports.IRng"/>, which the
    /// purity scan for this story forbids.
    /// </remarks>

    /// <summary>Explicit conversions only — see <see cref="PlayerId"/>.</summary>
    public static explicit operator Guid(StoreId id) => id.Value;
    public static explicit operator StoreId(Guid value) => new(value);
}
