using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4DirectionalBreakoutCompletionSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4DirectionalBreakoutCompletionSnapshotReducer reducer = new();

    [Fact]
    public void CompletesCandidateOrigin007WithoutAdvancingMarketCursor()
    {
        var candidate = Candidate();
        var candle = Candle(20, 100, 140, 50, 131);
        var breakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, candle);
        var input = new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout);

        var completed = reducer.Reduce(input);

        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(completed.Result.Breakout.Origin);
        Assert.Same(breakout, completed.Result.Breakout);
        Assert.Equal(breakout.PreviousProtectionAnchor, completed.Result.Breakout.PreviousProtectionAnchor);
        Assert.Equal(breakout.EffectiveProtectionAnchor, completed.Result.CandidateGeometry.ProtectionAnchor);
        Assert.Same(candle, input.MarketCursor);
        Assert.Same(candle, completed.MarketCursor);
        Assert.Same(candidate.Episode, completed.Episode);
        Assert.Equal(candle.Open, completed.Result.CandidateGeometry.StructuralPrice);
    }

    [Fact]
    public void CompletesRebuildOrigin007AndRetainsPriorMigrationProvenance()
    {
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(Candidate(), Candle(20, 100, 140, 50, 90));
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, Candle(24, 100, 140, 40, 131)).Breakout!;

        var completed = reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout));
        var origin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(completed.Result.Breakout.Origin);

        Assert.Same(pending.MigrationCandle, origin.PriorMigrationCandle);
        Assert.Same(breakout.ValidatingCandle, completed.MarketCursor);
        Assert.Same(breakout.Episode, completed.Episode);
    }

    [Fact]
    public void Rejects008OrdinaryAndNonBreakoutSnapshots()
    {
        var candidate = Candidate();
        var collision008 = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, Candle(20, 140, 150, 50, 131));
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(Candidate(), Candle(20, 100, 140, 50, 90));
        var ordinary = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, Candle(24, 100, 140, 50, 131)).Breakout!;

        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(collision008)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(ordinary)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(
            new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(Candidate(), Candle(20, 100, 140, 50, 131))))));
    }

    private static NasdaqPostInvalidationCandidateState Candidate()
    {
        var origin = Candle(0, 100, 110, 85, 90); var prior = Candle(4, 105, 115, 100, 110); var invalidating = Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]); MultiTimeframeReplayFrame? frame = null; while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12, 130, 145, 95, 115)));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) => new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
