namespace CurioDistrict.Domain.Ids;

/// <summary>
/// The shared shape of a typed identifier (story 007, FND-008).
///
/// A typed ID is the *unit of meaning* in a Domain signature. Callers pass a
/// <c>PlayerId</c> where a player is meant, never a raw <c>Guid</c>, so the
/// reader cannot mistake one entity's key for another's. The raw value is
/// exposed read-only for persistence and serialization; it is not the thing
/// Domain code reasons about.
///
/// Distinctness is the point: <see cref="PlayerId"/> and <see cref="StoreId"/>
/// share this marker but have no implicit conversion between them, so passing
/// one where the other is expected is a compile error.
/// </summary>
public interface IEntityId
{
    /// <summary>The opaque backing value. Never compared across ID types.</summary>
    Guid Value { get; }
}
