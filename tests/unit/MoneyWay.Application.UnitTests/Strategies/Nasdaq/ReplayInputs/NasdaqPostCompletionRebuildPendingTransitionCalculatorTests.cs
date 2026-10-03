using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuildPendingTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuildPendingTransitionCalculator calculator = new();

    public static TheoryData<bool, decimal, decimal, decimal, string> Cases
    {
        get
        {
            var cases = new TheoryData<bool, decimal, decimal, decimal, string>();
            foreach (var bearish in new[] { false, true })
            {
                cases.Add(bearish, 125, 80, 123, "PendingContinues"); // Wrong body; anchor equality.
                cases.Add(bearish, 123, 80, 123, "PendingContinues"); // Exact doji.
                cases.Add(bearish, 120, 80, 123, "TrackingStarted"); // Directional body; anchor equality.
                cases.Add(bearish, 125, 79, 123, "PendingReset");
                cases.Add(bearish, 123, 79, 123, "PendingReset"); // Migrating doji.
                cases.Add(bearish, 120, 79, 123, "TrackingStarted"); // Migration/turn dual role.
                cases.Add(bearish, 130, 80, 132, "BreakoutDetected"); // Turn loses to breakout.
                cases.Add(bearish, 140, 80, 132, "BreakoutDetected"); // Contrary body.
                cases.Add(bearish, 132, 80, 132, "BreakoutDetected"); // Breakout doji.
                cases.Add(bearish, 130, 79, 132, "BreakoutDetected"); // 007 collision.
                cases.Add(bearish, 140, 79, 132, "BreakoutDetected"); // 008 collision.
                cases.Add(bearish, 132, 79, 132, "BreakoutDetected"); // 008 doji collision.
                cases.Add(bearish, 130, 80, 131, "TrackingStarted"); // Breakout equality.
                cases.Add(bearish, 140, 80, 130, "PendingContinues"); // Open/wick beyond terminal is insufficient.
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OneCandleMatchesCanonicalRoutingAndPreservesAllLineage(bool bearish, decimal open, decimal low,
        decimal close, string expected)
    {
        var candidate = Candidate(bearish);
        var migrationCandle = Candle(80, 125, 180, 80, 123, bearish);
        var pending = new NasdaqPostCompletionRebuildPendingMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, migrationCandle));
        var candle = Candle(89, open, 180, low, close, bearish); // No invented +4h spacing.
        var members = candidate.CorrectionTurnCandles.ToArray();
        var originalDecision = pending.Decision;
        var oldCandidate = EquivalentCandidate(bearish, candidate.CorrectionStartCandle, candidate.TerminalCandle);
        var oldPending = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.RebuildPending>(
            new NasdaqPostInvalidationCandidateTransitionCalculator().Evaluate(oldCandidate, migrationCandle)).State;
        var old = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(oldPending, candle);

        var result = calculator.Evaluate(pending, candle);
        var repeated = calculator.Evaluate(pending, candle);

        Assert.Equal(expected, result.GetType().Name);
        Assert.Equal(old.Kind.ToString(), result.GetType().Name);
        Assert.Equal(result.GetType(), repeated.GetType());
        Assert.Equal(result.Migration.PreviousExtreme, repeated.Migration.PreviousExtreme);
        Assert.Equal(result.EffectiveProtectionAnchor, repeated.EffectiveProtectionAnchor);
        Assert.Equal(result.BodyDirection, repeated.BodyDirection);
        Assert.Equal(result.Breakout.IsConfirmed, repeated.Breakout.IsConfirmed);
        Assert.Same(candle, repeated.MarketCursor);
        Assert.Same(pending, result.SourceState);
        Assert.Same(candidate, result.SourceState.SourceCandidate);
        Assert.Same(candidate.SourceCorrection, result.SourceState.SourceCandidate.SourceCorrection);
        Assert.Same(candidate.Episode, result.Episode);
        Assert.Same(candidate.ActivePair, result.ActivePair);
        Assert.Same(pending.FrozenBreakoutTerminal, result.FrozenBreakoutTerminal);
        Assert.Equal(pending.CandidateSide, result.CandidateSide);
        Assert.Same(candle, result.MarketCursor);
        Assert.Same(candle, result.Breakout.Candle);
        Assert.Equal(pending.KnownProtectionAnchor, result.PreviousProtectionAnchor);
        Assert.Equal(pending.KnownProtectionAnchor, result.Migration.PreviousExtreme);
        Assert.Equal(result.Migration.WasReplaced ? bearish ? 200 - low : low : pending.KnownProtectionAnchor,
            result.EffectiveProtectionAnchor);
        Assert.Same(result.Migration.WasReplaced ? candle : migrationCandle, result.EffectiveMigrationCandle);
        Assert.Same(migrationCandle, pending.MarketCursor);
        Assert.Same(migrationCandle, pending.MigrationCandle);
        Assert.Same(originalDecision, pending.Decision);
        Assert.Equal(members, candidate.CorrectionTurnCandles);
        Assert.Same(candidate.TerminalCandle, candidate.MarketCursor);

        switch (result)
        {
            case NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted turn:
                Assert.Same(candle, turn.FirstTurnCandle);
                Assert.Same(old.Tracking!.FirstTurnCandle, turn.FirstTurnCandle);
                Assert.Same(old.Tracking.MigrationCandle, turn.EffectiveMigrationCandle);
                Assert.Equal(old.Tracking.KnownProtectionAnchor, turn.EffectiveProtectionAnchor);
                Assert.False(turn.Breakout.IsConfirmed);
                break;
            case NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected breakout:
                Assert.Same(candle, breakout.BreakoutCandle);
                Assert.Equal(old.Breakout!.CollisionKind, breakout.CollisionKind);
                Assert.Equal(old.Breakout.HasStrictMigration, breakout.Migration.WasReplaced);
                Assert.Equal(old.Breakout.EffectiveProtectionAnchor, breakout.EffectiveProtectionAnchor);
                var origin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(old.Breakout.Origin);
                Assert.Same(origin.PriorMigrationCandle, breakout.SourceState.MigrationCandle);
                Assert.True(breakout.Breakout.IsConfirmed);
                break;
            default:
                Assert.Same(old.Pending!.MigrationCandle, result.EffectiveMigrationCandle);
                Assert.Equal(old.Pending.KnownProtectionAnchor, result.EffectiveProtectionAnchor);
                Assert.Same(old.Pending.LastProcessedCandle, result.MarketCursor);
                Assert.False(result.Breakout.IsConfirmed);
                break;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsReprocessingEarlierOverlappingAndWrongSeriesCandles(bool bearish)
    {
        var candidate = Candidate(bearish);
        var pending = new NasdaqPostCompletionRebuildPendingMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, Candle(80, 125, 180, 80, 123, bearish)));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, pending.MarketCursor));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, Candle(76, 120, 180, 80, 123, bearish)));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, Candle(82, 120, 180, 80, 123, bearish)));
        var valid = Candle(84, 120, 180, 80, 123, bearish);
        foreach (var wrong in new[]
        {
            new Candle(new MarketDataProviderId("other"), Symbol, H4, valid.OpenTimeUtc, valid.CloseTimeUtc, 120, 180, 80, 123, null),
            new Candle(Provider, new MarketSymbol("other"), H4, valid.OpenTimeUtc, valid.CloseTimeUtc, 120, 180, 80, 123, null),
            new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour), valid.OpenTimeUtc, valid.CloseTimeUtc, 120, 180, 80, 123, null),
        })
            Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, wrong));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, valid));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(pending, null!));
    }

    [Fact]
    public void ResultIsClosedImmutableAndExposesNoNextStateMembersOrDefinitiveGeometry()
    {
        var resultType = typeof(NasdaqPostCompletionRebuildPendingTransitionResult);
        Assert.Empty(resultType.GetConstructors());
        Assert.Equal(new[] { "BreakoutDetected", "PendingContinues", "PendingReset", "TrackingStarted" },
            resultType.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var type in resultType.GetNestedTypes().Append(resultType))
        {
            Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(type.GetProperties(), p => p.Name is "CandidateGeometry" or "Members" or "ResultingState" or "Completion");
        }
        var method = Assert.Single(typeof(NasdaqPostCompletionRebuildPendingTransitionCalculator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { typeof(NasdaqPostCompletionRebuildPendingState), typeof(Candle) },
            method.GetParameters().Select(p => p.ParameterType));
    }

    private static NasdaqPostCompletionCandidateState Candidate(bool bearish)
    {
        var correction = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var terminal = Candle(32, 120, 180, 90, 125, bearish);
        var observation = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
            new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(correction, terminal));
        var provisional = new NasdaqPostCompletionCorrectionTurnCalculator().Evaluate(observation);
        return Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(new NasdaqPostCompletionCorrectionTurnReducer().Reduce(provisional)).State;
    }

    private static NasdaqPostInvalidationCandidateState EquivalentCandidate(bool bearish, Candle start, Candle terminal)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        impulse = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(24, 125, 190, 100, 131)).ResultingState;
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
    }
    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 130, 180, 100, 120, bearish);
        var started = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(started);
        var a = source.ConfirmingCandle;
        var b = ties ? Candle(24, 130, 140, 60, 131, bearish) : Candle(24, 125, 190, 100, 120, bearish);
        var selected = overlap ? new[] { a.OpenTimeUtc, b.OpenTimeUtc, turn.OpenTimeUtc } : new[] { a.OpenTimeUtc, b.OpenTimeUtc };
        var observations = new[]
        {
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected, Start.AddHours(40), "review:first"),
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected.Reverse().ToArray(), Start.AddHours(48), "review:second"),
        };
        var context = Context([a, b, turn, Candle(44, 135, 180, 100, 145, bearish)], observations);
        var evidence = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            new NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer().Reduce(pending, context));
        var members = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(
            new NasdaqPostCompletionExtremeMembershipPendingResolutionReducer().Reduce(evidence, context));
        return new NasdaqPostCompletionExtremeGeometryPreparer().Prepare(members);
    }
    private static NasdaqPostCompletionActiveImpulseState State(bool bearish)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(12, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, C(16, 80, 100, 60, 90)).Candidate!;
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, C(20, 130, 140, 60, 131)));
        return new NasdaqPostCompletionActiveImpulseInitializer().Initialize(completed);
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close, bool bearish) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            bearish ? 200 - open : open, bearish ? 200 - low : high, bearish ? 200 - high : low,
            bearish ? 200 - close : close, null);
}
