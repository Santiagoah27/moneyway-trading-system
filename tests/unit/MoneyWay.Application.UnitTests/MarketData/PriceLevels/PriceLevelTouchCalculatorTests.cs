using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.PriceLevels;

public sealed class PriceLevelTouchCalculatorTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly RuleId Rule = new("R-1");
    private static readonly StrategyDefinition Definition = new(
        Strategy,
        Version,
        "Synthetic",
        "test",
        [new(Rule, "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly PriceLevelTouchCalculator calculator = new();

    [Fact]
    public void LowerCandleWickEstablishesTouchWithoutBodyOrCloseConfirmation()
    {
        var context = CandleContext(Candle(Minute, Start, Start.AddMinutes(1), 105, 106, 99, 104));

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Lower);

        Assert.True(result.IsTouched);
        Assert.Equal((Provider, Symbol, 100m, PriceLevelDirection.Lower),
            (result.ProviderId, result.Symbol, result.TargetPrice, result.Direction));
        Assert.Equal(PriceLevelTouchEvidenceKind.CandleRange, result.EvidenceKind);
        Assert.Equal(Minute, result.CandleTimeframe);
        Assert.Equal((Start, Start.AddMinutes(1)),
            (result.EvidenceWindow!.EarliestPossibleUtc, result.EvidenceWindow.LatestPossibleUtc));
        Assert.False(result.HasExactTimestamp);
        Assert.Null(result.HasAuthoritativeSourceOrder);
    }

    [Fact]
    public void UpperCandleWickEstablishesTouchWithoutBodyOrCloseConfirmation()
    {
        var context = CandleContext(Candle(Minute, Start, Start.AddMinutes(1), 95, 101, 94, 96));

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.True(result.IsTouched);
        Assert.Equal(PriceLevelTouchEvidenceKind.CandleRange, result.EvidenceKind);
        Assert.Equal(96, ContextFrame(context).CurrentCandle.Close);
    }

    [Theory]
    [InlineData(PriceLevelDirection.Lower, 100.0001, 100.0000)]
    [InlineData(PriceLevelDirection.Upper, 99.9999, 100.0000)]
    public void CandleThatApproachesWithoutReachingHasNoTolerance(
        PriceLevelDirection direction,
        decimal extreme,
        decimal target)
    {
        var candle = direction == PriceLevelDirection.Lower
            ? Candle(Minute, Start, Start.AddMinutes(1), 101, 102, extreme, 101)
            : Candle(Minute, Start, Start.AddMinutes(1), 99, extreme, 98, 99);

        var result = calculator.EvaluateCurrentBoundary(CandleContext(candle), target, direction);

        Assert.False(result.IsTouched);
        Assert.Null(result.EvidenceKind);
        Assert.Null(result.EvidenceWindow);
    }

    [Theory]
    [InlineData(PriceLevelDirection.Lower, 99.5, 100.0)]
    [InlineData(PriceLevelDirection.Upper, 100.5, 100.0)]
    public void ActualHighResolutionObservationAtOrBeyondLevelEstablishesExactTouch(
        PriceLevelDirection direction,
        decimal observedPrice,
        decimal target)
    {
        var at = Start.AddSeconds(10);
        var context = EventContext([Observation(at, observedPrice, 42)]);

        var result = calculator.EvaluateCurrentBoundary(context, target, direction);

        Assert.True(result.IsTouched);
        Assert.Equal(PriceLevelTouchEvidenceKind.MarketPriceObservation, result.EvidenceKind);
        Assert.Equal((at, at), (result.EvidenceWindow!.EarliestPossibleUtc, result.EvidenceWindow.LatestPossibleUtc));
        Assert.True(result.HasExactTimestamp);
        Assert.True(result.HasAuthoritativeSourceOrder);
        Assert.Equal(42, result.SourceSequence);
    }

    [Fact]
    public void HighResolutionObservationsDoNotCreateSyntheticTouchWhenNoObservedPriceReachesLevel()
    {
        var context = EventContext(
        [
            Observation(Start.AddSeconds(10), 99.8m, 10),
            Observation(Start.AddSeconds(20), 99.9m, 11),
        ], Start.AddSeconds(20));

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.False(result.IsTouched);
    }

    [Fact]
    public void UnknownEqualTimestampOrderRemainsUnknownWhileTouchOccurrenceIsKnown()
    {
        var at = Start.AddSeconds(10);
        var context = EventContext(
        [
            Observation(at, 99),
            Observation(at, 101),
        ]);

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.True(result.IsTouched);
        Assert.True(result.HasExactTimestamp);
        Assert.False(result.HasAuthoritativeSourceOrder);
        Assert.Null(result.SourceSequence);
    }

    [Fact]
    public void AuthoritativeEqualTimestampSequenceSelectsFirstObservedTouchDeterministically()
    {
        var at = Start.AddSeconds(10);
        var context = EventContext(
        [
            Observation(at, 99, 10),
            Observation(at, 101, 11),
            Observation(at, 102, 12),
        ]);

        var first = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);
        var second = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.Equal(first, second);
        Assert.True(first.HasAuthoritativeSourceOrder);
        Assert.Equal(11, first.SourceSequence);
    }

    [Fact]
    public void HighResolutionEvidenceHasPriorityWhenItAndCandleEvidenceShareBoundary()
    {
        var at = Start.AddMinutes(1);
        var context = CanonicalContext(
            [Series(Candle(Minute, Start, at, 99, 101, 98, 99))],
            new(Provider, Symbol, [Observation(at, 100, 10)]),
            at);

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.Equal(PriceLevelTouchEvidenceKind.MarketPriceObservation, result.EvidenceKind);
        Assert.True(result.HasExactTimestamp);
        Assert.Equal(10, result.SourceSequence);
    }

    [Fact]
    public void CandleEvidencePreservesIntervalSoSameCandleOrderRemainsResolutionInsufficient()
    {
        var context = CandleContext(Candle(Minute, Start, Start.AddMinutes(1), 99, 101, 98, 99));
        var touch = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);
        var anotherSameCandleEvent = new ReplayTemporalEvidenceWindow("another-event", Start, Start.AddMinutes(1));

        var assessment = new AssessReplayTemporalOrderingObservabilityUseCase()
            .Execute(Definition, context, Rule, touch.EvidenceWindow!, anotherSameCandleEvent);

        Assert.True(touch.IsTouched);
        Assert.Equal(ReplayMarketDataObservabilityStatus.ResolutionInsufficient, assessment.Status);
    }

    [Fact]
    public void FutureObservationCannotChangeEarlierResultOrSnapshot()
    {
        var firstBoundary = Start.AddMinutes(1);
        var eventAtFutureBoundary = Start.AddMinutes(1).AddSeconds(1);
        var candles = new[] { Series(Candle(Minute, Start, firstBoundary, 99, 99.9m, 98, 99)) };
        var shortContext = CanonicalContext(candles, new(Provider, Symbol, []), firstBoundary);
        var extendedContext = CanonicalContext(
            candles,
            new(Provider, Symbol, [Observation(eventAtFutureBoundary, 101, 10)]),
            firstBoundary);
        var before = calculator.EvaluateCurrentBoundary(shortContext, 100, PriceLevelDirection.Upper);
        var withFuture = calculator.EvaluateCurrentBoundary(extendedContext, 100, PriceLevelDirection.Upper);

        Assert.Equal(before, withFuture);
        Assert.False(withFuture.IsTouched);
        Assert.Equal(0, extendedContext.MarketPriceObservations.ObservationCount);
    }

    [Fact]
    public void OnlyCurrentBoundaryIsEvaluatedAndCallerOwnsHistoricalProgression()
    {
        var events = new HistoricalMarketPriceObservationSeries(
            Provider,
            Symbol,
            [Observation(Start.AddSeconds(10), 101, 10), Observation(Start.AddSeconds(20), 99, 11)]);
        var cursor = new CanonicalMultiTimeframeReplayCursor(
            [Series(Candle(Minute, Start, Start.AddMinutes(1), 99, 99, 98, 99))],
            events);
        Assert.True(cursor.TryAdvance(out var firstFrame));
        Assert.True(cursor.TryAdvance(out var secondFrame));
        var factory = new CreateStrategyReplayContextUseCase();
        var first = calculator.EvaluateCurrentBoundary(
            factory.ExecuteCanonical(Definition, firstFrame!), 100, PriceLevelDirection.Upper);
        var second = calculator.EvaluateCurrentBoundary(
            factory.ExecuteCanonical(Definition, secondFrame!), 100, PriceLevelDirection.Upper);

        Assert.True(first.IsTouched);
        Assert.False(second.IsTouched);
        Assert.True(first.HasExactTimestamp);
    }

    [Fact]
    public void NarrowestUpdatedCandleProvidesTheMostPreciseCandleWindow()
    {
        var fiveMinutes = new Timeframe(5, TimeframeUnit.Minute);
        var close = Start.AddMinutes(5);
        var context = CandleContext(
            Series(Candle(fiveMinutes, Start, close, 99, 101, 98, 99)),
            Series(Candle(Minute, close.AddMinutes(-1), close, 99, 101, 98, 99)));

        var result = calculator.EvaluateCurrentBoundary(context, 100, PriceLevelDirection.Upper);

        Assert.Equal(Minute, result.CandleTimeframe);
        Assert.Equal((close.AddMinutes(-1), close),
            (result.EvidenceWindow!.EarliestPossibleUtc, result.EvidenceWindow.LatestPossibleUtc));
    }

    [Fact]
    public void RejectsNullContextAndUnknownDirection()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(null!, 100, PriceLevelDirection.Upper));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.EvaluateCurrentBoundary(
            CandleContext(Candle(Minute, Start, Start.AddMinutes(1), 99, 101, 98, 99)),
            100,
            (PriceLevelDirection)99));
    }

    private static StrategyReplayContext CandleContext(params CandleSeries[] series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var current)) frame = current;
        return new CreateStrategyReplayContextUseCase().Execute(Definition, frame!);
    }

    private static StrategyReplayContext CandleContext(params Candle[] candles) =>
        CandleContext(candles.Select(Series).ToArray());

    private static StrategyReplayContext EventContext(
        HistoricalMarketPriceObservation[] observations,
        DateTimeOffset? at = null)
    {
        var boundary = at ?? observations[0].ObservedAtUtc;
        return CanonicalContext(
            [Series(Candle(Minute, Start, Start.AddMinutes(1), 99, 99, 98, 99))],
            new(Provider, Symbol, observations),
            boundary);
    }

    private static StrategyReplayContext CanonicalContext(
        IEnumerable<CandleSeries> candles,
        HistoricalMarketPriceObservationSeries observations,
        DateTimeOffset at)
    {
        var cursor = new CanonicalMultiTimeframeReplayCursor(candles, observations);
        var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == at) return factory.ExecuteCanonical(Definition, frame);
        }

        throw new InvalidOperationException("Requested canonical boundary was not produced.");
    }

    private static ReplayFrame ContextFrame(StrategyReplayContext context)
    {
        Assert.True(context.TryGetFrame(Minute, out var frame));
        return frame!;
    }

    private static HistoricalMarketPriceObservation Observation(
        DateTimeOffset at,
        decimal price,
        long? sequence = null) =>
        new(Provider, Symbol, at, price, "source-price", "100ms", sequence);

    private static CandleSeries Series(Candle candle) =>
        new(Provider, Symbol, candle.Timeframe, [candle]);

    private static Candle Candle(
        Timeframe timeframe,
        DateTimeOffset open,
        DateTimeOffset close,
        decimal openPrice,
        decimal high,
        decimal low,
        decimal closePrice) =>
        new(Provider, Symbol, timeframe, open, close, openPrice, high, low, closePrice, null);
}
