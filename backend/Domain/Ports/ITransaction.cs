namespace CurioDistrict.Domain.Ports;

/// <summary>
/// A unit of work the server can commit or roll back as one
/// (story 007, FND-008).
///
/// <see cref="SourceKey"/> is the idempotency guard from ADR-0021: a
/// retried request carries the same source key, so the implementation
/// (Data layer, or its callers in the Worker/API hosts) can recognize a
/// replay and land it on the same order rather than opening a second one.
/// The port itself carries the key; it does not decide what "same" means —
/// that policy lives in the use case.
/// </summary>
public interface ITransaction
{
    /// <summary>
    /// The source key the work is committed under — the guard against a
    /// retried request double-applying.
    /// </summary>
    string SourceKey { get; }

    /// <summary>
    /// Makes the work durable. After a commit the source key is
    /// considered consumed: a later commit carrying the same key is a
    /// replay and must not re-apply the work.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Undoes the work; nothing is made durable.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Begins a unit of work under a source key (story 007, FND-008).
///
/// Domain code takes this port rather than reaching for a transaction
/// scope, a session or an ambient context: the boundary check family from
/// story 002 verifies that shape, and no Domain code holds a live
/// transaction of its own.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a unit of work that will commit under <paramref name="sourceKey"/>.
    /// </summary>
    ITransaction Begin(string sourceKey);
}
