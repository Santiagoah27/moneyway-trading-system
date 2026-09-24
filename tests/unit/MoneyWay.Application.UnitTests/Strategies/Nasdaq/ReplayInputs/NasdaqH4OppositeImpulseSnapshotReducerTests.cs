using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4OppositeImpulseSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4OppositeImpulseSnapshotReducer reducer = new();

    [Fact]
    public void ContinuationWrapsTheExactAdvancedImpulseState()
    {
        var state = Initial(); var input = new NasdaqH4ReconstructionSnapshot.OppositeImpulse(state); var candle = Candle(12, 90, 95, 78, 85);
        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.OppositeImpulse>(reducer.Reduce(input, candle));
        Assert.Same(candle, next.MarketCursor); Assert.Same(state.Episode, next.Episode); Assert.Equal(85m, next.State.ProvisionalTerminal.StructuralPrice);
        Assert.Same(state.LastProcessedCandle, input.MarketCursor);
        Assert.Equal(90m, state.ProvisionalTerminal.StructuralPrice);
    }

    [Fact]
    public void CorrectionStartStopsAtCorrectionWithTransitionProvenance()
    {
        var state = Initial(); var input = new NasdaqH4ReconstructionSnapshot.OppositeImpulse(state); var candle = Candle(12, 70, 105, 65, 95);
        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.Correction>(reducer.Reduce(input, candle));
        Assert.Same(candle, next.MarketCursor); Assert.Same(state.Episode, next.Episode); Assert.Equal(70m, next.State.FrozenImpulseTerminal.StructuralPrice);
        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Correction, next.Kind); Assert.IsNotType<NasdaqH4ReconstructionSnapshot.Candidate>(next);
    }

    [Fact]
    public void PropagatesTransitionSeriesValidationAndRejectsOtherVariants()
    {
        var state = Initial(); var input = new NasdaqH4ReconstructionSnapshot.OppositeImpulse(state);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4, Start.AddHours(12), Start.AddHours(16), 90, 95, 78, 85, null);
        var correction = reducer.Reduce(input, Candle(12, 70, 105, 65, 95));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, state.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, wrongSymbol));
        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.OppositeImpulse, reducer.Reduce(input, Candle(24, 90, 95, 78, 85)).Kind);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(correction, Candle(16, 100, 110, 80, 105)));
    }

    private static NasdaqPostInvalidationOppositeImpulseState Initial()
    {
        var origin = Candle(0, 100, 110, 95, 110); var candidate = Candle(4, 95, 100, 90, 96); var invalidating = Candle(8, 100, 110, 80, 90);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, candidate, invalidating])]); MultiTimeframeReplayFrame? frame = null; while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]); var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, 130, 95, 140, [candidate], StructuralCandidateExtremeSide.Lower);
        return new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary));
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) => new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
