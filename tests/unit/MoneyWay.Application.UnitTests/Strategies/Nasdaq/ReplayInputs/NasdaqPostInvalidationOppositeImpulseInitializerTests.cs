using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationOppositeImpulseInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationOppositeImpulseInitializer initializer = new();

    [Fact]
    public void BullishInvalidationCreatesBearishImpulseFromHighOriginAndLowTerminal()
    {
        var originBody = Candle(0, 100, 110, 95, 110);
        var originWick = Candle(4, 107, 120, 100, 108);
        var candidate = Candle(8, 95, 100, 90, 96);
        var invalidating = Candle(12, 70, 82, 60, 80);
        var boundary = Boundary([originBody, originWick, candidate, invalidating], StructuralCandidateExtremeSide.Lower);
        var members = Resolution(invalidating, originBody, originWick);
        var origin = new StructuralTurnGeometryCalculator().Evaluate([originBody, originWick], StructuralTurnBodyCoordinateSide.Upper);

        var result = initializer.Initialize(members, boundary, origin);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, boundary.Kind);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.TerminalSide);
        Assert.Same(origin, result.OriginGeometry);
        Assert.Equal(110m, result.OriginGeometry.StructuralPrice);
        Assert.Equal(120m, result.OriginGeometry.ProtectionAnchor);
        Assert.Equal(70m, result.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(60m, result.ProvisionalTerminal.ProtectionAnchor);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.ProvisionalTerminal.Side);
        Assert.Same(invalidating, result.InvalidatingCandle);
        Assert.Same(members.Episode, result.Episode);
        Assert.Same(invalidating, result.LastProcessedCandle);
        Assert.NotEqual(result.OriginGeometry.Side, result.ProvisionalTerminal.Side);
    }

    [Fact]
    public void BearishInvalidationCreatesBullishImpulseFromLowOriginAndHighTerminal()
    {
        var originBody = Candle(0, 100, 110, 85, 90);
        var originWick = Candle(4, 92, 105, 80, 95);
        var candidate = Candle(8, 105, 115, 100, 110);
        var invalidating = Candle(12, 130, 140, 116, 120);
        var boundary = Boundary([originBody, originWick, candidate, invalidating], StructuralCandidateExtremeSide.Upper);
        var members = Resolution(invalidating, originBody, originWick);
        var origin = new StructuralTurnGeometryCalculator().Evaluate([originBody, originWick], StructuralTurnBodyCoordinateSide.Lower);

        var result = initializer.Initialize(members, boundary, origin);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, boundary.Kind);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.TerminalSide);
        Assert.Same(origin, result.OriginGeometry);
        Assert.Equal(90m, result.OriginGeometry.StructuralPrice);
        Assert.Equal(80m, result.OriginGeometry.ProtectionAnchor);
        Assert.Equal(130m, result.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(140m, result.ProvisionalTerminal.ProtectionAnchor);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.ProvisionalTerminal.Side);
        Assert.Same(invalidating, result.InvalidatingCandle);
        Assert.Same(members.Episode, result.Episode);
        Assert.Same(invalidating, result.LastProcessedCandle);
        Assert.NotEqual(result.OriginGeometry.Side, result.ProvisionalTerminal.Side);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 80, StructuralTurnBodyCoordinateSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 115, StructuralTurnBodyCoordinateSide.Upper)]
    public void StructuralInvalidationSideRatherThanCandleBodyDeterminesTerminalSide(
        StructuralCandidateExtremeSide candidateSide,
        decimal open,
        decimal close,
        StructuralTurnBodyCoordinateSide expectedTerminalSide)
    {
        var origin = candidateSide == StructuralCandidateExtremeSide.Lower
            ? Candle(0, 100, 110, 95, 110)
            : Candle(0, 100, 110, 85, 90);
        var candidate = candidateSide == StructuralCandidateExtremeSide.Lower
            ? Candle(4, 95, 100, 90, 96)
            : Candle(4, 105, 115, 100, 110);
        var invalidating = Candle(8, open, 130, 70, close);
        var boundary = Boundary([origin, candidate, invalidating], candidateSide);
        var geometry = new StructuralTurnGeometryCalculator().Evaluate([origin],
            candidateSide == StructuralCandidateExtremeSide.Lower
                ? StructuralTurnBodyCoordinateSide.Upper
                : StructuralTurnBodyCoordinateSide.Lower);

        var result = initializer.Initialize(Resolution(invalidating, origin), boundary, geometry);

        Assert.Equal(expectedTerminalSide, result.TerminalSide);
    }

    [Fact]
    public void RejectsMismatchedInvalidatingCandleNonInvalidatingBoundaryAndOriginSide()
    {
        var origin = Candle(0, 100, 110, 95, 110);
        var candidate = Candle(4, 95, 100, 90, 96);
        var invalidating = Candle(8, 100, 120, 80, 89);
        var later = Candle(12, 100, 130, 70, 89);
        var boundary = Boundary([origin, candidate, invalidating], StructuralCandidateExtremeSide.Lower);
        var nonInvalidating = Boundary([origin, candidate, Candle(8, 100, 120, 90, 95)], StructuralCandidateExtremeSide.Lower);
        var correctOrigin = new StructuralTurnGeometryCalculator().Evaluate([origin], StructuralTurnBodyCoordinateSide.Upper);
        var wrongOrigin = new StructuralTurnGeometryCalculator().Evaluate([origin], StructuralTurnBodyCoordinateSide.Lower);

        Assert.Throws<ArgumentException>(() => initializer.Initialize(Resolution(later, origin), boundary, correctOrigin));
        Assert.NotEqual(StructuralCandidateTurnBoundaryKind.StructureInvalidated, nonInvalidating.Kind);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(Resolution(invalidating, origin), nonInvalidating, correctOrigin));
        Assert.Throws<ArgumentException>(() => initializer.Initialize(Resolution(invalidating, origin), boundary, wrongOrigin));
    }

    private static NasdaqHumanOriginVertexMemberResolution Resolution(Candle invalidating, params Candle[] originMembers)
    {
        var observation = new NasdaqHumanOriginVertexObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
            MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider,
            Symbol,
            invalidating.OpenTimeUtc,
            originMembers.Select(member => member.OpenTimeUtc),
            invalidating.CloseTimeUtc,
            "review:origin");
        return new NasdaqHumanOriginVertexMemberResolver().Evaluate(
            observation,
            Context([.. originMembers, invalidating], [observation]));
    }

    private static StructuralCandidateTurnBoundaryResult Boundary(Candle[] candles, StructuralCandidateExtremeSide candidateSide)
    {
        var context = Context(candles);
        var bullish = candidateSide == StructuralCandidateExtremeSide.Lower;
        return new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 90 : 110, bullish ? 140 : 75,
            [candles[^2]], candidateSide);
    }

    private static StrategyReplayContext Context(
        Candle[] candles,
        IReadOnlyList<IStrategyReplayInputObservation>? inputObservations = null)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? lastFrame = null;
        while (cursor.TryAdvance(out var frame)) lastFrame = frame;
        return new CreateStrategyReplayContextUseCase().Execute(
            MoneyWayNasdaqStrategyDefinition.Instance,
            lastFrame!,
            inputObservations ?? []);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}
