namespace CurioDistrict.Domain.Ids;

/// <summary>
/// Identifies a server-side order (a transaction in the district economy).
/// Story 007, FND-008.
///
/// The order is the unit the server owns (ADR-0008): its id is the key the
/// transaction port's source key references when a request is retried — the
/// idempotency guard from ADR-0021 hangs off the order, so a replayed request
/// lands on the same order rather than opening a second one.
/// </summary>
public readonly record struct OrderId(Guid Value) : IEntityId
{
    /// <summary>An id that represents no order; use only at construction time.</summary>
    public static readonly OrderId None = new(Guid.Empty);

    /// <remarks>
    /// A new id is a *randomness* decision, so it is made outside Domain —
    /// the host or Data layer mints the <c>Guid</c> (or replays a
    /// persisted seed) and hands the id in. Domain carries ids but does
    /// not generate them: a <c>Guid.NewGuid()</c> call here would be
    /// ambient randomness bypassing <see cref="Ports.IRng"/>, which the
    /// purity scan for this story forbids.
    /// </remarks>

    /// <summary>Explicit conversions only — see <see cref="PlayerId"/>.</summary>
    public static explicit operator Guid(OrderId id) => id.Value;
    public static explicit operator OrderId(Guid value) => new(value);
}
