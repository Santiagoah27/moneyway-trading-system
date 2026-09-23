using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4HumanStructuralPriceBreakoutEvidenceSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4HumanStructuralPriceBreakoutEvidenceSnapshotReducer reducer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndFutureEvidencePreserveExactFrozenSnapshotForBothOrigins(bool rebuildOrigin)
    {
        var input = Snapshot008(rebuildOrigin);
        var future = Observation(input.State, 111.2345m, 32, "review:future");

        var withoutFuture = reducer.Reduce(input, Context(28));
        var withFuture = reducer.Reduce(input, Context(28, future));

        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Missing, withFuture.Kind);
        Assert.Equal(withoutFuture.Kind, withFuture.Kind);
        Assert.Same(input, withFuture.Snapshot);
        Assert.Same(input.State, Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(withFuture.Snapshot).State);
        Assert.Same(input.State.ValidatingCandle, withFuture.Snapshot.MarketCursor);
        Assert.Same(input.Episode, withFuture.Snapshot.Episode);
        Assert.Empty(withFuture.Selection.SupportingObservations);
        Assert.Null(withFuture.CompletionResult);
        Assert.Equal(withoutFuture.Selection.DistinctStructuralPrices, withFuture.Selection.DistinctStructuralPrices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterUniqueCompletesOriginalBreakoutWithoutChangingEarlierFrame(bool rebuildOrigin)
    {
        var input = Snapshot008(rebuildOrigin);
        var observation = Observation(input.State, 111.2345m, 32, "review:later");
        var early = reducer.Reduce(input, Context(28, observation));

        var later = reducer.Reduce(input, Context(32, observation));

        var completed = Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice>(later.Snapshot);
        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Missing, early.Kind);
        Assert.Same(input, early.Snapshot);
        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Completed, later.Kind);
        Assert.Same(completed.Result, later.CompletionResult);
        Assert.Same(input.State, completed.Result.Breakout);
        Assert.Same(input.State.ValidatingCandle, completed.MarketCursor);
        Assert.Same(input.Episode, completed.Episode);
        Assert.Equal(observation.StructuralPrice, completed.Result.CandidateGeometry.StructuralPrice);
        Assert.Equal(input.State.EffectiveProtectionAnchor, completed.Result.CandidateGeometry.ProtectionAnchor);
        Assert.Same(observation, Assert.Single(later.Selection.SupportingObservations));
        if (rebuildOrigin)
            Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(completed.Result.Breakout.Origin);
        else
            Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(completed.Result.Breakout.Origin);
    }

    [Fact]
    public void SameExactPriceDuplicatesCompleteAndKeepEverySource()
    {
        var input = Snapshot008(false);
        var first = Observation(input.State, 111.2345m, 32, "review:first");
        var second = Observation(input.State, 111.2345m, 36, "review:second");

        var result = reducer.Reduce(input, Context(36, second, first));

        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Completed, result.Kind);
        Assert.Equal([first, second], result.Selection.SupportingObservations);
        Assert.Same(result.Selection, result.CompletionResult!.HumanPriceSelection);
        Assert.Equal(111.2345m, result.CompletionResult.CandidateGeometry.StructuralPrice);
        Assert.Same(input.State.ValidatingCandle, result.Snapshot.MarketCursor);
    }

    [Fact]
    public void DifferentPricesRemainConflictEvenAfterLaterThirdObservation()
    {
        var input = Snapshot008(true);
        var first = Observation(input.State, 111m, 32, "review:first");
        var second = Observation(input.State, 112m, 36, "review:second");
        var third = Observation(input.State, 113m, 40, "review:third");

        var conflict = reducer.Reduce(input, Context(36, first, second, third));
        var later = reducer.Reduce(input, Context(40, first, second, third));

        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Conflict, conflict.Kind);
        Assert.Same(input, conflict.Snapshot);
        Assert.Same(input.State.ValidatingCandle, conflict.Snapshot.MarketCursor);
        Assert.Equal([first, second], conflict.Selection.SupportingObservations);
        Assert.Equal([111m, 112m], conflict.Selection.DistinctStructuralPrices);
        Assert.Null(conflict.CompletionResult);
        Assert.Equal(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Conflict, later.Kind);
        Assert.Same(input, later.Snapshot);
        Assert.Equal([111m, 112m, 113m], later.Selection.DistinctStructuralPrices);
        Assert.Equal([first, second, third], later.Selection.SupportingObservations);
        Assert.Null(later.CompletionResult);
    }

    [Fact]
    public void Rejects007Ordinary009AndNonBreakoutSnapshots()
    {
        var candidate = Candidate();
        var directional = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 50, 131));
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(Candidate(), Candle(20, 100, 140, 50, 90));
        var ordinary = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 50, 131)).Breakout!;
        var input008 = Snapshot008(false);
        var completed = reducer.Reduce(input008, Context(32, Observation(input008.State, 111m, 32, "review:price"))).Snapshot;
        var context = Context(32);

        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(directional), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(ordinary), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(completed, context));
    }

    private static NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion Snapshot008(bool rebuildOrigin)
    {
        var candidate = Candidate();
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout;
        if (rebuildOrigin)
        {
            var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
                .Evaluate(candidate, Candle(20, 100, 140, 50, 90));
            breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
                .Evaluate(pending, Candle(24, 140, 145, 49, 131)).Breakout!;
        }
        else
        {
            breakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
                .Evaluate(candidate, Candle(20, 140, 150, 50, 131));
        }
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired,
            breakout.CollisionKind);
        return new(breakout);
    }

    private static NasdaqHumanCollisionStructuralPriceObservation Observation(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout, decimal price, int observedAtHour, string source) =>
        new(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide,
            price, Start.AddHours(observedAtHour), source);

    private static StrategyReplayContext Context(int asOfHour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var candle = new Candle(Provider, Symbol, minute, Start.AddHours(asOfHour).AddMinutes(-1),
            Start.AddHours(asOfHour), 100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, minute, [candle])]);
        cursor.TryAdvance(out var frame);
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static NasdaqPostInvalidationCandidateState Candidate()
    {
        var origin = Candle(0, 100, 110, 85, 90);
        var prior = Candle(4, 105, 115, 100, 110);
        var invalidating = Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12, 130, 145, 95, 115)));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
