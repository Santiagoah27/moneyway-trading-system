using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionActiveImpulseTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionActiveImpulseTransitionCalculator calculator = new();

    [Theory]
    [InlineData(false, 135, 145, 0)]
    [InlineData(true, 135, 145, 0)]
    [InlineData(false, 145, 135, 1)]
    [InlineData(true, 145, 135, 1)]
    [InlineData(false, 125, 115, 1)]
    [InlineData(true, 125, 115, 1)]
    [InlineData(false, 105, 125, 0)]
    [InlineData(true, 105, 125, 0)]
    [InlineData(false, 125, 125, 0)]
    [InlineData(true, 125, 125, 0)]
    [InlineData(false, 125, 105, 2)]
    [InlineData(true, 125, 105, 2)]
    [InlineData(false, 95, 105, 2)]
    [InlineData(true, 95, 105, 2)]
    [InlineData(false, 105, 105, 2)]
    [InlineData(true, 105, 105, 2)]
    public void RoutesStrictInvalidationBeforeBodyAndPreservesEveryConsumedFact(bool bearish, decimal open, decimal close, int branch)
    {
        var state = State(bearish);
        Assert.Equal(bearish ? 85m : 115m, state.ValidatedProtectedTurn.StructuralPrice);
        // Both wick extremes cross the protected level; only Close determines invalidation.
        var incoming = Candle(28, open, 180, 90, close, bearish);
        var previous = state.MarketCursor;
        var result = calculator.Evaluate(state, incoming);
        Assert.Same(state, result.SourceState);
        Assert.Same(incoming, result.MarketCursor);
        Assert.Same(incoming, result.ProtectedTurnBreak.Candle);
        Assert.Equal(new CandleBodyDirectionCalculator().Evaluate(incoming), result.BodyDirection);
        Assert.Equal(bearish ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower, result.ProtectedTurnBreak.Direction);
        Assert.Equal(state.ValidatedProtectedTurn.StructuralPrice, result.ProtectedTurnBreak.ReferenceLevel);
        Assert.Equal(branch == 2, result.ProtectedTurnBreak.IsConfirmed);
        Assert.Same(previous, state.MarketCursor);
        var repeated = calculator.Evaluate(state, incoming);
        Assert.Equal(result.GetType(), repeated.GetType());
        Assert.Equal(result.ProtectedTurnBreak, repeated.ProtectedTurnBreak);
        Assert.Equal(result.BodyDirection, repeated.BodyDirection);
        if (branch == 2)
            Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.ProtectedTurnInvalidated>(result);
        else if (branch == 1)
        {
            var correction = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(result);
            Assert.Same(incoming, correction.CorrectionStartCandle);
            Assert.Same(state.Episode, correction.MembershipEvent.Episode);
            Assert.Equal(new NasdaqPostCompletionActiveExtremeMembershipEvent(state.Episode, incoming), correction.MembershipEvent);
            Assert.Equal(correction.MembershipEvent,
                Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(repeated).MembershipEvent);
        }
        else
        {
            var next = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.ImpulseRemainsOpen>(result).ResultingState;
            Assert.NotSame(state, next);
            Assert.Same(incoming, next.MarketCursor);
            Assert.Same(state.Episode, next.Episode);
            Assert.Same(state.Completion, next.Completion);
            Assert.Same(state.ValidatedProtectedTurn, next.ValidatedProtectedTurn);
            Assert.Same(state.ProtectedTurnGeometry, next.ProtectedTurnGeometry);
            Assert.Same(state.ConfirmingCandle, next.ConfirmingCandle);
        }
        Assert.Equal(bearish ? 200 - open : open, incoming.Open);
        Assert.Equal(bearish ? 200 - close : close, incoming.Close);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdvancesAcrossGapsAndValidatesAgainstUpdatedCursorAndDistinctTurnEvents(bool bearish)
    {
        var original = State(bearish);
        var first = Candle(28, 135, 180, 100, 145, bearish);
        var next = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.ImpulseRemainsOpen>(calculator.Evaluate(original, first)).ResultingState;
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(next, first));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(next, original.MarketCursor));
        var turn = Candle(44, 145, 180, 100, 135, bearish);
        var result = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(calculator.Evaluate(next, turn));
        Assert.Same(next, result.SourceState);
        Assert.Same(original.Completion, result.SourceState.Completion);
        Assert.Same(turn, result.MarketCursor);
        var other = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(calculator.Evaluate(next, Candle(48, 145, 180, 100, 135, bearish)));
        Assert.NotEqual(result.MembershipEvent, other.MembershipEvent);
        Assert.Same(first, next.MarketCursor);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(22)]
    public void RejectsEarlierSameAndOverlappingMarketCandles(int hour) =>
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(State(false), Candle(hour, 100, 150, 90, 110, false)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RejectsAnotherMarketSeries(int mismatch)
    {
        var candle = new Candle(mismatch == 0 ? new("other") : Provider, mismatch == 1 ? new("other") : Symbol,
            mismatch == 2 ? new(1, TimeframeUnit.Hour) : H4, Start.AddHours(28), Start.AddHours(32), 100, 150, 90, 110, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(State(false), candle));
    }

    [Fact]
    public void RejectsNullInputsAndExposesOnlyClosedImmutableOutcomes()
    {
        var state = State(false);
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, state.MarketCursor));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(state, null!));
        var union = typeof(NasdaqPostCompletionActiveImpulseTransitionResult);
        Assert.Empty(union.GetConstructors());
        Assert.All(union.GetNestedTypes(), branch =>
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), property => Assert.Null(property.SetMethod));
        });
        Assert.DoesNotContain(typeof(NasdaqPostCompletionActiveImpulseTransitionResult.ProtectedTurnInvalidated).GetProperties(),
            property => property.PropertyType == typeof(NasdaqPostCompletionActiveExtremeMembershipEvent));
        Assert.DoesNotContain(typeof(NasdaqPostCompletionActiveImpulseTransitionResult.ImpulseRemainsOpen).GetProperties(),
            property => property.PropertyType == typeof(NasdaqPostCompletionActiveExtremeMembershipEvent));
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
