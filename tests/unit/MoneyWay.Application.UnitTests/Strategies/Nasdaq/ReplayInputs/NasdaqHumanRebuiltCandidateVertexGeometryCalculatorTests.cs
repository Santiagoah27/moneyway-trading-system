using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanRebuiltCandidateVertexGeometryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanRebuiltCandidateVertexGeometryCalculator calculator = new();

    [Fact]
    public void LowerSingleMemberUsesItsBodyAndLowAsGeometry()
    {
        var migration = Candle(20, 105, 120, 50, 110);

        var geometry = calculator.Evaluate(Resolution(StructuralCandidateExtremeSide.Lower, [migration]));

        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, geometry.Side);
        Assert.Equal(105m, geometry.StructuralPrice);
        Assert.Equal(50m, geometry.ProtectionAnchor);
    }

    [Fact]
    public void LowerMembersUseOnlyLowestSelectedBodyAndWickIncludingDifferentSources()
    {
        var migration = Candle(20, 110, 140, 50, 120);
        var bodySource = Candle(24, 85, 130, 80, 115);
        var unselected = Candle(28, 1, 999, 1, 2);
        var resolution = Resolution(StructuralCandidateExtremeSide.Lower, [migration, bodySource]);

        var geometry = calculator.Evaluate(resolution);

        Assert.Equal(85m, geometry.StructuralPrice);
        Assert.Equal(50m, geometry.ProtectionAnchor);
        Assert.Equal(2, resolution.SelectedMembers.Count);
        Assert.DoesNotContain(unselected, resolution.SelectedMembers);
    }

    [Fact]
    public void UpperSingleAndMultipleMembersUseHighestSelectedBodyAndWick()
    {
        var migration = Candle(20, 100, 140, 70, 95);
        var bodySource = Candle(24, 115, 120, 80, 110);

        var geometry = calculator.Evaluate(Resolution(StructuralCandidateExtremeSide.Upper, [migration, bodySource]));

        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, geometry.Side);
        Assert.Equal(115m, geometry.StructuralPrice);
        Assert.Equal(140m, geometry.ProtectionAnchor);
    }

    [Fact]
    public void MembershipOrderAndFutureDataDoNotChangeDeterministicGeometry()
    {
        var migration = Candle(20, 110, 140, 50, 120);
        var member = Candle(24, 85, 130, 80, 115);
        var future = Candle(28, 1, 999, 1, 2);
        var first = Resolution(StructuralCandidateExtremeSide.Lower, [migration, member]);
        var reversed = Resolution(StructuralCandidateExtremeSide.Lower, [member, migration]);

        var firstGeometry = calculator.Evaluate(first);

        Assert.Equal(firstGeometry, calculator.Evaluate(reversed));
        Assert.DoesNotContain(future, first.SelectedMembers);
        Assert.Equal(firstGeometry, calculator.Evaluate(first));
    }

    private static NasdaqHumanRebuiltCandidateVertexMemberResolution Resolution(
        StructuralCandidateExtremeSide side, IReadOnlyList<Candle> members)
    {
        var migration = members.Single(candle => candle.OpenTimeUtc == Start.AddHours(20));
        var pending = Pending(side, migration);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanRebuiltCandidateVertexObservation(
            definition.StrategyId, definition.Version, Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc,
            migration.OpenTimeUtc, side, members.Select(candle => candle.OpenTimeUtc), members.Max(candle => candle.CloseTimeUtc), "review:rebuilt");
        var context = Context(members, members.Max(candle => candle.CloseTimeUtc), observation);
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            definition.StrategyId, definition.Version, Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc, migration.OpenTimeUtc, side);
        var selection = new NasdaqHumanRebuiltCandidateVertexObservationSelector().Select(context, episode);
        return new NasdaqHumanRebuiltCandidateVertexMemberResolver().Evaluate(
            NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), selection, context);
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(StructuralCandidateExtremeSide side, Candle migration)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        return new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, migration);
    }

    private static NasdaqPostInvalidationCorrectionState InitialCorrection(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var originObservation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([originCandle, oldCandidate, invalidating], invalidating.CloseTimeUtc, originObservation);
        var originMembers = new NasdaqHumanOriginVertexMemberResolver().Evaluate(originObservation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(originMembers, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(originMembers, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
    }

    private static StrategyReplayContext Context(IReadOnlyList<Candle> candles, DateTimeOffset asOfUtc,
        IStrategyReplayInputObservation observation)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles.OrderBy(candle => candle.OpenTimeUtc))]);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, [observation]);
        }
        throw new InvalidOperationException("Test replay frame was not found.");
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) => new(
        Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}
