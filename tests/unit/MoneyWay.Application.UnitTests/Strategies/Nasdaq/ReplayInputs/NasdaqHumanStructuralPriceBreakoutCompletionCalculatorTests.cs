using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanStructuralPriceBreakoutCompletionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanStructuralPriceBreakoutCompletionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131, 111.2345, StructuralBreakDirection.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 141, 50, 69, 88.2345, StructuralBreakDirection.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 49, 131, 111.2345, StructuralBreakDirection.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 141, 50, 69, 88.2345, StructuralBreakDirection.Lower)]
    public void UniqueHumanPriceCompletesContraryAndDojiCollisionsWithoutMarketBodyOwnership(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal humanPrice, StructuralBreakDirection direction)
    {
        var breakout = Breakout(side, open, high, low, close);
        var evidence = Observation(breakout, humanPrice, 32, "review:price");
        var selection = Select(breakout, 32, evidence);

        var result = calculator.Evaluate(breakout, selection);
        var repeated = calculator.Evaluate(breakout, selection);
        var snapshot = new NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice(result);

        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Completed, snapshot.Kind);
        Assert.Same(result, snapshot.Result);
        Assert.Same(result.Breakout.Episode, snapshot.Episode);
        Assert.Same(result.LastProcessedCandle, snapshot.MarketCursor);
        Assert.Same(result.ValidatedCandidate, snapshot.ValidatedCandidate);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, breakout.CollisionKind);
        Assert.True(result.ValidatedCandidate.IsValidated);
        Assert.Equal(humanPrice, result.CandidateGeometry.StructuralPrice);
        Assert.NotEqual(open, result.CandidateGeometry.StructuralPrice);
        Assert.NotEqual(close, result.CandidateGeometry.StructuralPrice);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, result.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(breakout.EffectiveProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.Same(result.CandidateGeometry, result.ValidatedCandidate.CandidateGeometry);
        Assert.Equal(side, result.ValidatedCandidate.CandidateSide);
        Assert.Equal(direction, result.ValidatedCandidate.BreakObservation.Direction);
        Assert.Equal(breakout.FrozenImpulseTerminal.StructuralPrice, result.ValidatedCandidate.BreakObservation.ReferenceLevel);
        Assert.Same(breakout.ValidatingCandle, result.ValidatedCandidate.BreakObservation.Candle);
        Assert.Same(breakout.ValidatingCandle, result.LastProcessedCandle);
        Assert.Same(breakout.Episode, result.Breakout.Episode);
        Assert.Same(selection, result.HumanPriceSelection);
        Assert.Same(evidence, Assert.Single(result.HumanPriceSelection.SupportingObservations));
        Assert.Equal(result.ValidatedCandidate, repeated.ValidatedCandidate);
        Assert.Null(result.GetType().GetProperty("StructuralPriceSourceCandle"));
        Assert.Null(result.GetType().GetProperty("RuleStatus"));
        Assert.Null(result.GetType().GetProperty("StrategyVerdict"));
    }

    [Fact]
    public void CompatibleHumanEvidencePreservesEverySourceWithoutChangingTheMarketCursor()
    {
        var breakout = Breakout(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131);
        var first = Observation(breakout, 111.2345m, 29, "review:first");
        var second = Observation(breakout, 111.2345m, 32, "review:second");
        var selection = Select(breakout, 32, second, first);

        var result = calculator.Evaluate(breakout, selection);

        Assert.Equal(111.2345m, result.CandidateGeometry.StructuralPrice);
        Assert.Equal([first, second], result.HumanPriceSelection.SupportingObservations);
        Assert.Same(breakout.ValidatingCandle, result.LastProcessedCandle);
        Assert.Equal(Start.AddHours(28), result.LastProcessedCandle.CloseTimeUtc);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 49, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 50, 69)]
    public void OrdinaryAndDirectionalBreakoutsAreIneligible(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var breakout = Breakout(side, open, high, low, close);
        var selection = Select(breakout, 32, Observation(breakout, 111.2345m, 32, "review:price"));

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout, selection));
    }

    [Fact]
    public void MissingAndConflictSelectionsCannotComplete()
    {
        var breakout = Breakout(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131);
        var missing = Select(breakout, 32);
        var first = Observation(breakout, 111m, 29, "review:first");
        var second = Observation(breakout, 112m, 32, "review:second");
        var early = Select(breakout, 29, first, second);
        var conflict = Select(breakout, 32, first, second);

        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, early.Kind);
        Assert.Equal(111m, calculator.Evaluate(breakout, early).CandidateGeometry.StructuralPrice);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout, missing));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout, conflict));
    }

    [Fact]
    public void SelectionMustBelongToTheExactCollisionEpisode()
    {
        var breakout = Breakout(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131);
        var original = Observation(breakout, 111m, 32, "review:price");
        var otherVersion = new NasdaqHumanOriginVertexEpisode(
            breakout.Episode.StrategyId, new("other-version"), Provider, Symbol, breakout.Episode.InvalidatingCandleOpenTimeUtc);
        var otherProvider = new NasdaqHumanOriginVertexEpisode(
            breakout.Episode.StrategyId, breakout.Episode.StrategyVersion, new("other"), Symbol, breakout.Episode.InvalidatingCandleOpenTimeUtc);
        var otherSymbol = new NasdaqHumanOriginVertexEpisode(
            breakout.Episode.StrategyId, breakout.Episode.StrategyVersion, Provider, new("OTHER"), breakout.Episode.InvalidatingCandleOpenTimeUtc);
        var otherInvalidation = new NasdaqHumanOriginVertexEpisode(
            breakout.Episode.StrategyId, breakout.Episode.StrategyVersion, Provider, Symbol, Start.AddHours(12));
        var observations = new[]
        {
            new NasdaqHumanCollisionStructuralPriceObservation(otherVersion, original.CollisionCandleOpenTimeUtc, original.CandidateSide, 111m, original.ObservedAtUtc, "review:other-version"),
            new NasdaqHumanCollisionStructuralPriceObservation(otherProvider, original.CollisionCandleOpenTimeUtc, original.CandidateSide, 111m, original.ObservedAtUtc, "review:other-provider"),
            new NasdaqHumanCollisionStructuralPriceObservation(otherSymbol, original.CollisionCandleOpenTimeUtc, original.CandidateSide, 111m, original.ObservedAtUtc, "review:other-symbol"),
            new NasdaqHumanCollisionStructuralPriceObservation(otherInvalidation, original.CollisionCandleOpenTimeUtc, original.CandidateSide, 111m, original.ObservedAtUtc, "review:other-invalidation"),
            new NasdaqHumanCollisionStructuralPriceObservation(breakout.Episode, Start.AddHours(28), original.CandidateSide, 111m, Start.AddHours(32), "review:other-collision"),
            new NasdaqHumanCollisionStructuralPriceObservation(breakout.Episode, original.CollisionCandleOpenTimeUtc, StructuralCandidateExtremeSide.Upper, 111m, original.ObservedAtUtc, "review:other-side"),
        };

        Assert.All(observations, observation =>
            Assert.Throws<ArgumentException>(() => calculator.Evaluate(breakout, Selection(observation))));
    }

    private static NasdaqHumanCollisionStructuralPriceObservationSelection Selection(
        NasdaqHumanCollisionStructuralPriceObservation observation)
    {
        var constructor = typeof(NasdaqHumanCollisionStructuralPriceObservationSelection)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (NasdaqHumanCollisionStructuralPriceObservationSelection)constructor.Invoke(
            [NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, observation.StructuralPrice,
                new[] { observation }, new[] { observation.StructuralPrice }]);
    }

    private static NasdaqHumanCollisionStructuralPriceObservationSelection Select(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout, int asOfHour,
        params NasdaqHumanCollisionStructuralPriceObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var candle = new Candle(Provider, Symbol, minute, Start.AddHours(asOfHour).AddMinutes(-1), Start.AddHours(asOfHour),
            100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, minute, [candle])]);
        cursor.TryAdvance(out var frame);
        var context = new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
        var episode = new NasdaqHumanCollisionStructuralPriceEpisode(
            breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide);
        return new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(context, episode);
    }

    private static NasdaqHumanCollisionStructuralPriceObservation Observation(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout, decimal price, int observedAtHour, string source) =>
        new(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide, price,
            Start.AddHours(observedAtHour), source);

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
