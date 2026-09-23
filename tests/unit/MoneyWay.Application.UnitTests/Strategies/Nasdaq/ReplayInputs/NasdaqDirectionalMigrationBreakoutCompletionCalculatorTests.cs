using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqDirectionalMigrationBreakoutCompletionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqDirectionalMigrationBreakoutCompletionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 49, 131, StructuralBreakDirection.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 50, 69, StructuralBreakDirection.Lower)]
    public void DirectionalCollisionUsesTheSameCandleForBothGeometryComponentsAndConfirmation(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        StructuralBreakDirection direction)
    {
        var breakout = Breakout(side, open, high, low, close);

        var result = calculator.Evaluate(breakout);
        var snapshot = new NasdaqH4ReconstructionSnapshot.Completed.Directional(result);

        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Completed, snapshot.Kind);
        Assert.Same(result, snapshot.Result);
        Assert.Same(result.Breakout.Episode, snapshot.Episode);
        Assert.Same(result.LastProcessedCandle, snapshot.MarketCursor);
        Assert.Same(result.ValidatedCandidate, snapshot.ValidatedCandidate);

        Assert.True(breakout.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody, breakout.CollisionKind);
        Assert.True(result.ValidatedCandidate.IsValidated);
        Assert.Same(result.CandidateGeometry, result.ValidatedCandidate.CandidateGeometry);
        Assert.Equal(side, result.ValidatedCandidate.CandidateSide);
        Assert.Equal(open, result.CandidateGeometry.StructuralPrice);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, result.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(breakout.EffectiveProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(direction, result.ValidatedCandidate.BreakObservation.Direction);
        Assert.Equal(breakout.FrozenImpulseTerminal.StructuralPrice, result.ValidatedCandidate.BreakObservation.ReferenceLevel);
        Assert.Same(breakout.ValidatingCandle, result.ValidatedCandidate.BreakObservation.Candle);
        Assert.Same(breakout.ValidatingCandle, result.LastProcessedCandle);
        Assert.Same(breakout.Episode, result.Breakout.Episode);
        Assert.Null(result.GetType().GetProperty("MemberResolution"));
        Assert.Null(result.GetType().GetProperty("RuleStatus"));
        Assert.Null(result.GetType().GetProperty("StrategyVerdict"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 69)]
    public void OrdinaryBreakoutIsNotEligible(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var breakout = Breakout(side, open, high, low, close);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.CollisionKind);
        Assert.False(breakout.HasStrictMigration);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 49, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 141, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 141, 50, 69)]
    public void ContraryOrDojiCollisionRemainsHumanRequired(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var breakout = Breakout(side, open, high, low, close);

        Assert.True(breakout.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, breakout.CollisionKind);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    [Fact]
    public void RepeatedCompletionIsEquivalentAndLeavesBreakoutUnchanged()
    {
        var breakout = Breakout(StructuralCandidateExtremeSide.Lower, 100, 140, 49, 131);
        var originalCursor = breakout.LastProcessedCandle;
        var originalEpisode = breakout.Episode;

        var first = calculator.Evaluate(breakout);
        var second = calculator.Evaluate(breakout);

        Assert.Equal(first.ValidatedCandidate, second.ValidatedCandidate);
        Assert.Same(originalCursor, breakout.LastProcessedCandle);
        Assert.Same(originalEpisode, breakout.Episode);
        Assert.Same(breakout.ValidatingCandle, first.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 49, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 141, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 141, 50, 69)]
    public void InconsistentDirectionalClassificationCannotComplete(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var pending = Pending(side);
        var breakout = InternalBreakout(pending, Candle(24, open, high, low, close),
            side == StructuralCandidateExtremeSide.Lower ? low : high);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 70)]
    public void FrozenTerminalEqualityCannotConfirmAnInconsistentBreakout(
        StructuralCandidateExtremeSide side, decimal close)
    {
        var pending = Pending(side);
        var candle = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 140, 49, close)
            : Candle(24, 100, 141, 50, close);
        var breakout = InternalBreakout(pending, candle,
            side == StructuralCandidateExtremeSide.Lower ? candle.Low : candle.High);

        Assert.Equal(pending.FrozenImpulseTerminal.StructuralPrice, close);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 49)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 141)]
    public void InconsistentMigratedAnchorIsRejectedExactly(StructuralCandidateExtremeSide side, decimal actualAnchor)
    {
        var pending = Pending(side);
        var candle = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 140, actualAnchor, 131)
            : Candle(24, 100, actualAnchor, 50, 69);
        var breakout = InternalBreakout(pending, candle, actualAnchor + 1);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 50)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 140)]
    public void InconsistentStrictMigrationClassificationIsRejected(StructuralCandidateExtremeSide side, decimal equalAnchor)
    {
        var pending = Pending(side);
        var candle = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 140, equalAnchor, 131)
            : Candle(24, 100, equalAnchor, 50, 69);
        var breakout = InternalBreakout(pending, candle, equalAnchor);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout));
    }

    private static NasdaqPostInvalidationCandidateRebuildBreakoutState InternalBreakout(
        NasdaqPostInvalidationCandidateRebuildPendingState pending, Candle candle, decimal effectiveAnchor)
    {
        var constructor = typeof(NasdaqPostInvalidationCandidateRebuildBreakoutState)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(item => item.GetParameters()[0].ParameterType == typeof(NasdaqPostInvalidationCandidateRebuildPendingState));
        return (NasdaqPostInvalidationCandidateRebuildBreakoutState)constructor.Invoke(
            [pending, candle, true, effectiveAnchor,
                NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody]);
    }

    private static NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var pending = Pending(side);
        return new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, open, high, low, close)).Breakout!;
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower
            : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(
            definition.StrategyId, definition.Version, Provider, Symbol, invalidating.OpenTimeUtc,
            [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor(
            [new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75,
            [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, 90)
            : Candle(20, 100, 140, 80, 100);
        return new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(candidate, migration);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            open, high, low, close, null);
}
