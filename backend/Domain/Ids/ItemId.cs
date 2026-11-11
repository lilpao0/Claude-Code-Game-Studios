namespace CurioDistrict.Domain.Ids;

/// <summary>
/// Identifies an item (a collectible or tradeable catalog entry).
/// Story 007, FND-008.
///
/// Distinct from the ids of the entities that *hold* items: an <c>ItemId</c>
/// names the catalog concept, while a player's owned quantity lives under the
/// player/shop key. ADR-0003's server-authoritative economy means item
/// balances are decided server-side by keying them to these ids, never to a
/// client-supplied raw value.
/// </summary>
public readonly record struct ItemId(Guid Value) : IEntityId
{
    /// <summary>An id that represents no item; use only at construction time.</summary>
    public static readonly ItemId None = new(Guid.Empty);

    /// <remarks>
    /// A new id is a *randomness* decision, so it is made outside Domain —
    /// the host or Data layer mints the <c>Guid</c> (or replays a
    /// persisted seed) and hands the id in. Domain carries ids but does
    /// not generate them: a <c>Guid.NewGuid()</c> call here would be
    /// ambient randomness bypassing <see cref="Ports.IRng"/>, which the
    /// purity scan for this story forbids.
    /// </remarks>

    /// <summary>Explicit conversions only — see <see cref="PlayerId"/>.</summary>
    public static explicit operator Guid(ItemId id) => id.Value;
    public static explicit operator ItemId(Guid value) => new(value);
}
