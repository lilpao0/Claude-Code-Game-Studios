namespace CurioDistrict.Domain.Ports;

/// <summary>
/// The time source Domain code is allowed to read (story 007, FND-008).
///
/// Domain never asks the system for the current time itself — it receives the
/// instant through this port. That is what makes a run reproducible: a
/// test or a worker supplies a fixed clock and the same inputs yield the
/// same outputs (AC2), while production supplies the real system time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The current instant, as a UTC <see cref="DateTimeOffset"/>. The
    /// implementation decides what "current" means — the system clock in
    /// production, a frozen value in tests.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
