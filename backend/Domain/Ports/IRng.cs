namespace CurioDistrict.Domain.Ports;

/// <summary>
/// The source of randomness Domain code is allowed to draw from
/// (story 007, FND-008).
///
/// The pack reward is decided by a *server-side* RNG from a versioned
/// published pool (ADR-0003, ADR-0021): the client never decides an
/// outcome. This port is what makes that deterministic under test — a
/// seeded implementation replays the same sequence every run, so a
/// reward table can be asserted element-for-element (AC2).
///
/// How an implementation is seeded is the implementor's choice, and it
/// includes the two sanctioned forms:
///  * a fresh seed drawn once at startup (the common "production" shape);
///  * a *persisted* seed — the seed is written to durable state, and a
///    worker restart re-executes already-persisted ticks under that same
///    seed. Replaying a completed tick therefore yields the same result
///    without re-advancing (ADR-0018's durable tick; the replay guard of
///    ADR-0021). This is replay, not re-drawing: the sequence is a
///    function of the seed plus the tick's position, never of "how many
///    times anyone has run since".
/// Domain never picks the form: it only asks the port for the next value.
/// The point of the port is that Domain never touches an unseeded,
/// ambient source of randomness itself.
///
/// The contract is the minimal surface Domain needs: an integer draw from
/// <c>[0, bound)</c>. Nothing more.
/// </summary>
public interface IRng
{
    /// <summary>
    /// Draws the next non-negative integer in the range <c>[0, bound)</c>.
    /// A <c>bound</c> of zero or below is a programming error and may throw
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    int Next(int bound);
}
