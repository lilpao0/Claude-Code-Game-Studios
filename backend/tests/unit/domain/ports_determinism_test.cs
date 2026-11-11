using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurioDistrict.Domain.Ids;
using CurioDistrict.Domain.Ports;
using CurioDistrict.Tests.Unit;
using Xunit.Abstractions;

namespace CurioDistrict.Tests.Unit.Domain;

/// <summary>
/// Story 007 (FND-008) AC1 + AC2 — the typed IDs and the clock/RNG/transaction
/// ports are the seam between "real time and real randomness" and Domain
/// code. These tests pin that seam:
///
///  AC1 — Domain source contains no ambient time, no unseeded randomness and
///        no ambient transaction access.
///  AC2 — a fixed clock, a seeded RNG and a fixed source key, fed the same
///        operations, produce byte-identical outputs on two independent
///        executions; and a deliberately ambient (unseeded) implementation
///        is shown to break that property, so the test would fail if the
///        ports ever leaked ambient state.
///
/// The execution is in-process but state-isolated: each "run" gets its own
/// fresh fixtures (the QA case's "fresh runner process or a reset fixture"),
/// and the determinism property itself — seeded input → identical output —
/// is what makes that isolation sufficient. The transaction fixture records
/// commit/rollback per source key and exposes only the committed ones, so
/// the determinism check is against real port effects: a rolled-back unit
/// of work leaves no persisted value behind.
/// </summary>
public class PortsDeterminismTests(ITestOutputHelper output)
{
    private const int SequenceLength = 64;
    private const int RngBound = 1_000;

    /// <summary>
    /// One execution of the deterministic sequence: draw N values from the
    /// RNG, read the clock between draws, and for each value open a unit of
    /// work under a deterministic source key, committing every second one
    /// and rolling back the rest.
    /// </summary>
    private static (List<int> Draws, List<DateTimeOffset> Instants, RecordingUnitOfWork Store)
        ExecuteSequence(
            IRng rng,
            IClock clock,
            RecordingUnitOfWork store,
            PlayerId actingAs,
            OrderId order)
    {
        var draws = new List<int>(SequenceLength);
        var instants = new List<DateTimeOffset>(SequenceLength);

        // The acting-as and order ids round-trip through their explicit
        // conversions — the only raw path — and are what a real use case
        // would key the work to.
        var playerKey = (Guid)actingAs;
        var orderKey = (Guid)order;

        for (var i = 0; i < SequenceLength; i++)
        {
            var draw = rng.Next(RngBound);
            draws.Add(draw);

            instants.Add(clock.UtcNow);

            // The source key is deterministic: same player, order and
            // position → same key. The idempotency guard (ADR-0021) is
            // exactly the "same inputs, same key" property. The drawn value
            // is STAGED on the fixture's transaction — it reaches the
            // committed record only if this unit of work commits. The
            // Domain port itself carries no staging member; that shape
            // belongs to the concrete (here, test-local) implementation.
            var sourceKey = $"order-{orderKey:N}-by-{playerKey:N}-seq-{i}";
            var transaction = store.Begin(sourceKey);
            ((RecordingTransaction)transaction).Stage(draw);

            if (i % 2 == 0)
            {
                transaction.CommitAsync().GetAwaiter().GetResult();
            }
            else
            {
                transaction.RollbackAsync().GetAwaiter().GetResult();
            }
        }

        return (draws, instants, store);
    }

    /// <summary>
    /// The determinism property itself, shared by the positive and negative
    /// cases: the two runs' drawn sequences, observed instants and the
    /// committed/rolled-back transaction records must match element for
    /// element — including the VALUES that ended up committed, not only
    /// their source keys.
    /// </summary>
    private static bool AreRunsIdentical(
        (List<int> Draws, List<DateTimeOffset> Instants, RecordingUnitOfWork Store) run1,
        (List<int> Draws, List<DateTimeOffset> Instants, RecordingUnitOfWork Store) run2)
    {
        return run1.Draws.SequenceEqual(run2.Draws)
            && run1.Instants.SequenceEqual(run2.Instants)
            && run1.Store.CommittedValues.SequenceEqual(run2.Store.CommittedValues)
            && run1.Store.RolledBackKeys.SequenceEqual(run2.Store.RolledBackKeys);
    }

    [Fact]
    public void Two_state_isolated_runs_with_seeded_inputs_are_identical()
    {
        const int seed = 0xC0FFEE;
        var start = DateTimeOffset.UnixEpoch.AddMinutes(1234);
        var step = TimeSpan.FromMilliseconds(7);

        var player = new PlayerId(new Guid(0x01010101, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var order = new OrderId(new Guid(0x02020202, 2, 0, 0, 0, 0, 0, 0, 0, 0, 2));

        // Run 1 — a fresh set of fixtures.
        var run1 = ExecuteSequence(
            new SeededRng(seed),
            new SteppingClock(start, step),
            new RecordingUnitOfWork(),
            player,
            order);

        // Run 2 — another, disjoint set of fixtures: no state may carry
        // over from run 1 (the QA case's reset-fixture edge).
        var run2 = ExecuteSequence(
            new SeededRng(seed),
            new SteppingClock(start, step),
            new RecordingUnitOfWork(),
            player,
            order);

        // Every output of the two runs matches element for element — the
        // QA case's observable AC2 property.
        Assert.True(AreRunsIdentical(run1, run2),
            "Seeded, state-isolated runs produced different outputs — ambient " +
            "time or randomness has leaked into the ports.");

        // The transaction effects are real and deterministic: exactly the
        // even-seq units of work are committed, the odd ones rolled back,
        // a rollback left no staged value behind, and every committed value
        // is the drawn value its sequence position recorded.
        var committed = run1.Store.CommittedValues;
        var rolledBack = run1.Store.RolledBackKeys;

        Assert.Equal(SequenceLength / 2, committed.Count);
        Assert.Equal(SequenceLength / 2, rolledBack.Count);

        for (var i = 0; i < SequenceLength; i++)
        {
            var seq = i.ToString();
            if (i % 2 == 0)
            {
                Assert.Contains(committed, pair =>
                    pair.SourceKey.EndsWith($"-seq-{seq}", StringComparison.Ordinal) &&
                    pair.Value == run1.Draws[i]);
            }
            else
            {
                Assert.Contains(rolledBack, key => key.EndsWith($"-seq-{seq}", StringComparison.Ordinal));
            }
        }

        Assert.DoesNotContain(committed, pair => ParseSeq(pair.SourceKey) % 2 != 0);

        // The verification runner compares this complete deterministic trace
        // across separate testhost processes, as well as the reset fixtures above.
        output.WriteLine($"RUNNER_PROCESS_ID:{Environment.ProcessId}");
        output.WriteLine("DETERMINISM_TRACE:" + JsonSerializer.Serialize(new
        {
            seed, ClockStart = start, ClockStepTicks = step.Ticks,
            Player = (Guid)player, Order = (Guid)order,
            run1.Draws, run1.Instants,
            Committed = committed.Select(pair => new { pair.SourceKey, pair.Value }),
            RolledBackKeys = rolledBack
        }));
    }

    [Fact]
    public void A_frozen_clock_yields_identical_instants_across_runs()
    {
        // The clock's degenerate form: it never advances. Two runs through
        // a frozen clock and a seeded RNG must agree on every observed
        // instant — the clock can only make a run MORE reproducible, never
        // less — and on the committed values, which it must not perturb.
        const int seed = 0xBEEF;
        var frozen = DateTimeOffset.UnixEpoch.AddSeconds(99);

        var player = new PlayerId(new Guid(0x05050505, 5, 0, 0, 0, 0, 0, 0, 0, 0, 5));
        var order = new OrderId(new Guid(0x06060606, 6, 0, 0, 0, 0, 0, 0, 0, 0, 6));

        var run1 = ExecuteSequence(
            new SeededRng(seed),
            new FrozenClock(frozen),
            new RecordingUnitOfWork(),
            player,
            order);

        var run2 = ExecuteSequence(
            new SeededRng(seed),
            new FrozenClock(frozen),
            new RecordingUnitOfWork(),
            player,
            order);

        Assert.True(AreRunsIdentical(run1, run2),
            "Seeded runs under a frozen clock produced different outputs.");

        Assert.All(run1.Instants, instant => Assert.Equal(frozen, instant));
        Assert.Equal(run1.Draws, run2.Draws);
    }

    [Fact]
    public void Ambient_unseeded_randomness_breaks_determinism()
    {
        // The negative control for AC2, run through the SAME property the
        // positive case asserts: two state-isolated runs through an RNG that
        // reads an ambient, unseeded source (Random.Shared) must disagree
        // somewhere in the sequence. If the ports ever started hiding
        // ambient state behind a "deterministic" implementation, this
        // shared assertion is what fails — the property is pinned, not
        // assumed.
        var player = new PlayerId(new Guid(0x03030303, 3, 0, 0, 0, 0, 0, 0, 0, 0, 3));
        var order = new OrderId(new Guid(0x03030303, 4, 0, 0, 0, 0, 0, 0, 0, 0, 3));

        var run1 = ExecuteSequence(
            new SharedRng(),
            new SteppingClock(DateTimeOffset.UnixEpoch, TimeSpan.FromMilliseconds(1)),
            new RecordingUnitOfWork(),
            player,
            order);
        var run2 = ExecuteSequence(
            new SharedRng(),
            new SteppingClock(DateTimeOffset.UnixEpoch, TimeSpan.FromMilliseconds(1)),
            new RecordingUnitOfWork(),
            player,
            order);

        Assert.False(AreRunsIdentical(run1, run2),
            "Ambient (Random.Shared) RNGs produced identical 64-draw " +
            "sequences — the determinism property no longer detects ambient " +
            "state leaking into the ports.");
    }

    [Fact]
    public void The_rng_contract_rejects_a_non_positive_bound()
    {
        // IRng.Next([0, bound)) — a bound of zero is a programming error,
        // and the seeded implementation fails cleanly on it instead of
        // swallowing the input.
        var rng = new SeededRng(1234);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(0));
    }

    [Fact]
    public void Domain_source_contains_no_ambient_time_randomness_or_transaction()
    {
        // AC1, run as a scan over the real Domain tree (not a fixture): zero
        // occurrences of the forbidden ambient access. Test files are
        // outside the scanned directory by construction — this is the
        // production-Source half of the QA edge case; the broader boundary
        // check family (story 002) owns the rest. Fresh-gid generation is
        // included: an id's *generation* is a randomness decision and
        // belongs to the host/Data layer, not to Domain's id wrappers.
        var domainRoot = Path.Combine(TestPaths.BackendRoot, "Domain");
        var sourceFiles = Directory
            .EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var segments = Path.GetDirectoryName(f)!.Split(Path.DirectorySeparatorChar);
                return !segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
                    && !segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
            })
            .ToList();

        Assert.NotEmpty(sourceFiles);

        // Scan the de-commented source, not the raw text: a token like
        // Guid.NewGuid() may legitimately appear in a doc comment that
        // explains what is forbidden, and the prohibition still applies to
        // real code.
        var violations = sourceFiles
            .Select(f => (file: f, code: StripComments(File.ReadAllText(f))))
            .SelectMany(pair =>
            {
                var hits = ScanForForbidden(pair.code);
                return hits.Select(hit => $"{Path.GetRelativePath(domainRoot, pair.file)}: '{hit}'");
            })
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void The_purity_scanner_detects_spaced_and_fully_qualified_forms()
    {
        // Targeted scanner cases (the 007 review gap): the scan must not
        // depend on exact spacing or qualification. Each snippet below is a
        // plausible way the forbidden access would be written in real code —
        // a spaced member access, a fully-qualified constructor — and every
        // one of them must produce at least one hit.
        var codeSnippets = new[]
        {
            "var now = DateTime.Now;",
            "var now = DateTime . Now;",
            "var now = DateTime.UtcNow;",
            "var now = DateTime . UtcNow;",
            "var now = DateTimeOffset.UtcNow;",
            "var now = DateTimeOffset . UtcNow;",
            "var n = Random.Shared.Next(10);",
            "var n = Random . Shared.Next(10);",
            "var r = new Random(123);",
            "var r = new Random (123);",
            "var r = new System.Random(123);",
            "var id = Guid.NewGuid();",
            "var id = Guid . NewGuid();",
            "using var ts = new TransactionScope();",
        };

        foreach (var snippet in codeSnippets)
        {
            var hits = ScanForForbidden(snippet).ToList();
            Assert.True(
                hits.Count > 0,
                $"The scanner missed a forbidden form: \"{snippet}\" (hits: [{string.Join(", ", hits)}])");
        }
    }

    [Fact]
    public void The_purity_scanner_bridges_a_comment_spliced_between_tokens()
    {
        // The combined ScanForForbidden(StripComments(...)) case the 007
        // review asks for: an inline comment physically spliced between the
        // parts of a forbidden expression — <c>DateTime/*note*/.UtcNow</c>
        // — must still be caught. Without the space (not NUL) fill this
        // strips to <c>DateTime\0.UtcNow</c> and the whitespace-tolerant
        // pattern stops matching; with it, the scan bridges the gap and
        // flags the real access. Every member access in the list is a
        // genuine forbidden use with a comment wedged into it.
        var spliced = new[]
        {
            "var n = DateTime/*note*/.UtcNow;",
            "var n = Guid/*x*/.NewGuid();",
            "var r = new /*ctor*/System.Random(0);",
            "var s = Random/*shared*/.Shared.Next(1);",
        };

        foreach (var snippet in spliced)
        {
            var hits = ScanForForbidden(StripComments(snippet)).ToList();
            Assert.True(
                hits.Count > 0,
                $"The scanner missed a comment-spliced forbidden form: \"{snippet}\" " +
                "(strips to a gap the whitespace-tolerant patterns should bridge).");
        }
    }

    [Fact]
    public void The_purity_scanner_ignores_prose_mentions_in_comments()
    {
        // The other half of the gap: a token that exists only in comment
        // prose — including the exact spelled-out warnings a doc comment
        // would carry — must not count. The prohibition still applies to
        // code: the scanner is run on the de-commented text, and the
        // detection test above proves it is not so aggressive that it
        // stops finding real usages.
        var commented =
            "// Guid.NewGuid() must not be called in Domain.\n" +
            "/* Random.Shared is forbidden; DateTime.UtcNow too.\n" +
            "   new Random(0), new System.Random(0) and TransactionScope as well. */\n" +
            "/// <remarks>DateTime . Now stays out of Domain code.</remarks>\n" +
            "public int Draw() => 1;";

        var hits = ScanForForbidden(StripComments(commented)).ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void Typed_ids_expose_no_implicit_conversions_and_no_generation()
    {
        // The property the compiler enforces (AC1's "two different typed
        // IDs cannot be passed where one is expected") has a runtime
        // shadow: no id type may carry an implicit operator at all — and
        // no member that mints a fresh one, because that is ambient
        // randomness outside the injected IRng. Explicit conversions exist
        // on every id — deliberately, for hydration and persistence — but
        // if an implicit one or a NewId-style factory ever slips back in,
        // this test names it.
        var idTypes = new[] { typeof(PlayerId), typeof(StoreId), typeof(ItemId), typeof(OrderId) };

        foreach (var type in idTypes)
        {
            var implicitOperators = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.IsSpecialName && m.Name == "op_Implicit");
            Assert.Empty(implicitOperators);

            var generationMembers = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "NewId" or "New");
            Assert.Empty(generationMembers);

            // Every id is the IEntityId marker: Domain signatures can take
            // the abstraction without widening to a raw Guid.
            Assert.True(typeof(IEntityId).IsAssignableFrom(type));
        }
    }

    [Fact]
    public void An_explicit_guid_round_trip_is_the_only_raw_path()
    {
        // Hydration (Guid -> Id) and persistence (Id -> Guid) go through
        // the explicit operators, and survive a round trip unchanged —
        // while the typed value keeps its identity as a record struct.
        var raw = Guid.Parse("04040404-0404-0404-0404-040404040404");
        var store = (StoreId)raw;
        Assert.Equal(raw, (Guid)store);
        Assert.Equal(store, (StoreId)((Guid)store));

        // And distinct types never compare equal: a StoreId and an OrderId
        // built from the same raw value are different values, not aliases.
        var storeFrom = (StoreId)raw;
        var orderFrom = (OrderId)raw;
        Assert.False(storeFrom.Equals(orderFrom));
    }

    // ------------------------------------------------------------------
    // Test-local port implementations — the deterministic fixtures the QA
    // case asks for. They live here (the test side of the boundary),
    // never in Domain: Domain ships the ports and nothing that implements
    // them. The RNGs deliberately include the anti-patterns the ports
    // exist to keep out (the ambient shared source), so the negative case
    // is exercised against the real property, not a stub that never
    // consults the port.
    // ------------------------------------------------------------------

    private sealed class SeededRng(int seed) : IRng
    {
        private readonly Random _random = new(seed);

        public int Next(int bound)
        {
            if (bound <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bound), bound, "bound must be positive — Next draws from [0, bound).");
            }

            return _random.Next(bound);
        }
    }

    private sealed class SharedRng : IRng
    {
        public int Next(int bound)
        {
            if (bound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bound));
            }

            // The ambient, unseeded source the ports exist to keep OUT of
            // Domain. Each run continues the shared stream, so two runs
            // disagree: exactly the leak AC2's assertion must catch.
            return Random.Shared.Next(bound);
        }
    }

    private sealed class SteppingClock(DateTimeOffset start, TimeSpan step) : IClock
    {
        private DateTimeOffset _instant = start;

        public DateTimeOffset UtcNow
        {
            get
            {
                var observed = _instant;
                _instant += step;
                return observed;
            }
        }
    }

    /// <summary>
    /// Stages values on the transaction; only a COMMIT publishes the staged
    /// value to the record, under its source key. The IUnitOfWork port
    /// Domain carries has no staging member — the fixture extends it the way
    /// the Data layer's concrete implementation would, so the determinism
    /// check asserts real port effects: a rolled-back unit of work leaves
    /// no staged value behind, and a commit persists the exact value that
    /// was drawn for that position.
    /// </summary>
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        private readonly List<(string SourceKey, int Value)> _committed = new();
        private readonly List<string> _rolledBackKeys = new();

        public ITransaction Begin(string sourceKey)
        {
            return new RecordingTransaction(sourceKey, _committed, _rolledBackKeys);
        }

        internal IReadOnlyList<(string SourceKey, int Value)> CommittedValues => _committed;
        internal IReadOnlyList<string> RolledBackKeys => _rolledBackKeys;
    }

    private sealed class RecordingTransaction : ITransaction
    {
        private readonly string _sourceKey;
        private readonly List<(string SourceKey, int Value)> _committed;
        private readonly List<string> _rolledBackKeys;
        private int _stagedValue;
        private bool _staged;

        public RecordingTransaction(
            string sourceKey,
            List<(string SourceKey, int Value)> committed,
            List<string> rolledBackKeys)
        {
            _sourceKey = sourceKey;
            _committed = committed;
            _rolledBackKeys = rolledBackKeys;
        }

        public string SourceKey => _sourceKey;

        /// <summary>Fixture-only: what the concrete Data implementation would stage.</summary>
        internal void Stage(int value)
        {
            _stagedValue = value;
            _staged = true;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            // Only a commit with staged value publishes to the record,
            // under the source key that idempotency replays against.
            if (_staged)
            {
                _committed.Add((_sourceKey, _stagedValue));
            }

            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            // A rollback leaves NO staged value in the committed record —
            // only a key marker, so the determinism check can see which
            // positions rolled back and that they published nothing.
            _rolledBackKeys.Add(_sourceKey);
            _staged = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FrozenClock(DateTimeOffset instant) : IClock
    {
        public DateTimeOffset UtcNow => instant;
    }

    private static int ParseSeq(string sourceKey)
    {
        // ...-seq-N — the last dash-delimited segment.
        return int.Parse(sourceKey.AsSpan(sourceKey.LastIndexOf('-') + 1));
    }

    /// <summary>
    /// The forbidden ambient-access patterns. Each is written to tolerate
    /// spacing inside the expression (<c>DateTime . Now</c>) and, where
    /// relevant, a fully-qualified form (<c>new System.Random(…)</c>):
    /// a 007 review gap showed an exact-string scan misses the spaced and
    /// qualified spellings that a real edit would produce. The string the
    /// scanner returns is the human-readable form of the pattern, for the
    /// violation message.
    /// </summary>
    private static readonly (string Label, Regex Pattern)[] ForbiddenPatterns =
    {
        ("DateTime.Now", new Regex(@"DateTime\s*\.\s*Now\b", RegexOptions.Compiled)),
        ("DateTime.UtcNow", new Regex(@"DateTime\s*\.\s*UtcNow\b", RegexOptions.Compiled)),
        ("DateTimeOffset.UtcNow", new Regex(@"DateTimeOffset\s*\.\s*UtcNow\b", RegexOptions.Compiled)),
        ("Random.Shared", new Regex(@"Random\s*\.\s*Shared\b", RegexOptions.Compiled)),
        ("new Random(...)", new Regex(@"new\s+(?:System\s*\.\s*)?Random\s*\(", RegexOptions.Compiled)),
        ("Guid.NewGuid()", new Regex(@"Guid\s*\.\s*NewGuid\s*\(", RegexOptions.Compiled)),
        ("TransactionScope", new Regex(@"TransactionScope\b", RegexOptions.Compiled)),
    };

    /// <summary>
    /// Returns one label per forbidden pattern that occurs in
    /// <paramref name="deCommentedSource"/>.
    /// </summary>
    private static IEnumerable<string> ScanForForbidden(string deCommentedSource)
    {
        foreach (var (label, pattern) in ForbiddenPatterns)
        {
            if (pattern.IsMatch(deCommentedSource))
            {
                yield return label;
            }
        }
    }

    /// <summary>
    /// Removes C# comments while preserving the content of every string,
    /// verbatim-string and character literal: a forbidden token inside a
    /// <c>"..."</c> literal must survive the strip (a string that carries
    /// <c>DateTime.Now</c> is still code), while a token that exists only in
    /// a <c>//</c> or <c>/* */</c> comment — or in XML-doc prose like
    /// <c>/// ... a <c>Guid.NewGuid()</c> call here would be ...</c> — does
    /// not. The scan's subject is this project's Domain source, which uses
    /// no raw-string (<c>"""</c>) or interpolated-form expressions; those
    /// shapes are outside the stripper's guarantee by design.
    /// </summary>
    private static string StripComments(string source)
    {
        // Every unwritten position (comment span) is a SPACE, not a NUL:
        // the char-array default zero would otherwise leave \0 where a
        // comment was stripped, and the whitespace-tolerant scan patterns
        // cannot bridge a NUL. Filling with spaces up front means the only
        // characters ever in the result are copied source characters or
        // spaces/newlines — so DateTime/*note*/.UtcNow strips to
        // "DateTime .UtcNow" and still matches.
        var result = new char[source.Length];
        Array.Fill(result, ' ');
        var state = StripState.Code;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            switch (state)
            {
                case StripState.Code:
                    switch (c)
                    {
                        case '/' when next == '/':
                            state = StripState.LineComment;
                            i++;
                            break;
                        case '/' when next == '*':
                            state = StripState.BlockComment;
                            i++;
                            break;
                        case '@' when next == '"':
                            // Verbatim string open: keep both characters.
                            state = StripState.VerbatimString;
                            result[i] = c;
                            i++;
                            result[i] = next;
                            break;
                        case '"':
                            state = StripState.NormalString;
                            result[i] = c;
                            break;
                        case '\'':
                            state = StripState.CharLiteral;
                            result[i] = c;
                            break;
                        default:
                            result[i] = c;
                            break;
                    }
                    break;

                case StripState.LineComment:
                    if (c == '\n')
                    {
                        result[i] = '\n';
                        state = StripState.Code;
                    }
                    else
                    {
                        // A space, not a NUL: comment spans are bridged by
                        // whitespace in the stripped text so the scan's
                        // whitespace-tolerant patterns still match
                        // DateTime/*note*/.UtcNow, and the result buffer
                        // (char-array default zero) cannot leak NULs.
                        result[i] = ' ';
                    }
                    break;

                case StripState.BlockComment:
                    if (c == '*' && next == '/')
                    {
                        result[i] = ' ';
                        i++;
                        result[i] = ' ';
                        state = StripState.Code;
                    }
                    else
                    {
                        result[i] = c == '\n' ? '\n' : ' ';
                    }
                    break;

                case StripState.NormalString:
                    result[i] = c;
                    if (c == '\\')
                    {
                        // Escape sequence — keep both characters verbatim.
                        if (i + 1 < source.Length)
                        {
                            i++;
                            result[i] = source[i];
                        }
                    }
                    else if (c == '"')
                    {
                        state = StripState.Code;
                    }
                    break;

                case StripState.VerbatimString:
                    result[i] = c;
                    if (c == '"')
                    {
                        // A doubled quote is content (stay in the string);
                        // a single un-doubled quote closes it.
                        if (next == '"')
                        {
                            i++;
                            result[i] = '"';
                        }
                        else
                        {
                            state = StripState.Code;
                        }
                    }
                    break;

                case StripState.CharLiteral:
                    result[i] = c;
                    if (c == '\\')
                    {
                        if (i + 1 < source.Length)
                        {
                            i++;
                            result[i] = source[i];
                        }
                    }
                    else if (c == '\'')
                    {
                        state = StripState.Code;
                    }
                    break;
            }
        }

        return new string(result);
    }

    private enum StripState
    {
        Code,
        LineComment,
        BlockComment,
        NormalString,
        VerbatimString,
        CharLiteral,
    }
}
