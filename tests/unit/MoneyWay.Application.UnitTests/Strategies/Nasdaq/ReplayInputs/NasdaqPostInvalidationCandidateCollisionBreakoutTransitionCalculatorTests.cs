using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131, 60, 50)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 69, 130, 140)]
    public void DirectionalCollisionPreservesCandidateOriginAndCompletes007(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal previousAnchor, decimal effectiveAnchor)
    {
        var candidate = Candidate(side);
        var validating = Candle(20, open, high, low, close);

        var breakout = calculator.Evaluate(candidate, validating);
        var origin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(breakout.Origin);
        var completion = new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(breakout);
        var awaiting = new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout);
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.Directional(completion);

        Assert.Same(candidate, origin.State);
        Assert.Equal(previousAnchor, breakout.PreviousProtectionAnchor);
        Assert.Equal(effectiveAnchor, breakout.EffectiveProtectionAnchor);
        Assert.True(breakout.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody, breakout.CollisionKind);
        Assert.Same(candidate.Episode, breakout.Episode);
        Assert.Same(candidate.FrozenImpulseTerminal, breakout.FrozenImpulseTerminal);
        Assert.Same(validating, breakout.ValidatingCandle);
        Assert.Same(validating, breakout.LastProcessedCandle);
        Assert.Null(breakout.FirstTurnCandle);
        Assert.Same(validating, awaiting.MarketCursor);
        Assert.Same(candidate.Episode, awaiting.Episode);
        Assert.Same(validating, completed.MarketCursor);
        Assert.Same(breakout, completion.Breakout);
        Assert.Equal(effectiveAnchor, completion.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(open, completion.CandidateGeometry.StructuralPrice);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 150, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 150, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 140, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 140, 50, 69)]
    public void OppositeOrDojiCollisionAwaitsHumanPriceAndCompletes008(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);
        var validating = Candle(20, open, high, low, close);
        var breakout = calculator.Evaluate(candidate, validating);
        var episode = new NasdaqHumanCollisionStructuralPriceEpisode(
            breakout.Episode, validating.OpenTimeUtc, side);
        var observation = new NasdaqHumanCollisionStructuralPriceObservation(
            candidate.Episode, validating.OpenTimeUtc, side, 111.2345m,
            validating.CloseTimeUtc, "review:candidate-collision");
        var origin = side == StructuralCandidateExtremeSide.Lower
            ? Candle(0, 100, 110, 95, 110)
            : Candle(0, 100, 110, 85, 90);
        var oldCandidate = side == StructuralCandidateExtremeSide.Lower
            ? Candle(4, 105, 115, 100, 110)
            : Candle(4, 95, 100, 90, 96);
        var context = Context([origin, oldCandidate, candidate.InvalidatingCandle, validating], [observation]);
        var selection = new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(context, episode);
        var completion = new NasdaqHumanStructuralPriceBreakoutCompletionCalculator().Evaluate(breakout, selection);
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice(completion);

        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(breakout.Origin);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, breakout.CollisionKind);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? 50m : 140m, breakout.EffectiveProtectionAnchor);
        Assert.Null(breakout.GetType().GetProperty("StructuralPrice"));
        Assert.True(episode.Matches(observation));
        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, selection.Kind);
        Assert.Equal(observation.StructuralPrice, completion.CandidateGeometry.StructuralPrice);
        Assert.Equal(breakout.EffectiveProtectionAnchor, completion.CandidateGeometry.ProtectionAnchor);
        Assert.Same(observation, Assert.Single(completion.HumanPriceSelection.SupportingObservations));
        Assert.Same(validating, completed.MarketCursor);
        Assert.Same(candidate.Episode, completed.Episode);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 70)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 100)]
    public void RejectsEqualityMigrationOnlyAndDirectBreakoutRows(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Fact]
    public void RejectsRepeatedEarlierAndWrongSeriesCandlesButAllowsNonContiguousLaterCandle()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var values = (Open: 100m, High: 140m, Low: 50m, Close: 131m);
        var same = Candle(16, values.Open, values.High, values.Low, values.Close);
        var earlier = Candle(12, values.Open, values.High, values.Low, values.Close);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(20), Start.AddHours(24), values.Open, values.High, values.Low, values.Close, null);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), values.Open, values.High, values.Low, values.Close, null);
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(20), Start.AddHours(21), values.Open, values.High, values.Low, values.Close, null);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, same));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, earlier));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongProvider));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongSymbol));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongTimeframe));
        var later = Candle(24, values.Open, values.High, values.Low, values.Close);
        var first = calculator.Evaluate(candidate, later);
        var repeated = calculator.Evaluate(candidate, later);
        Assert.Same(later, first.ValidatingCandle);
        Assert.Equal(first.PreviousProtectionAnchor, repeated.PreviousProtectionAnchor);
        Assert.Equal(first.CollisionKind, repeated.CollisionKind);
        Assert.Same(candidate.LastProcessedCandle, candidate.TerminalCandle);
    }

    [Fact]
    public void UsesLastProcessedCursorRatherThanFrozenTerminalForChronology()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var consumed = Candle(20, 100, 120, 60, 100);
        var advanced = candidate.WithLastProcessedCandle(consumed);
        var collision = Candle(24, 100, 140, 50, 131);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(advanced, consumed));
        Assert.Same(collision, calculator.Evaluate(advanced, collision).LastProcessedCandle);
        Assert.Same(candidate.TerminalCandle, advanced.TerminalCandle);
        Assert.Same(consumed, advanced.LastProcessedCandle);
    }

    private static NasdaqPostInvalidationCandidateState Candidate(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower
            : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var origin = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, oldCandidate, invalidating], [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
    }

    private static StrategyReplayContext Context(Candle[] candles, IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
