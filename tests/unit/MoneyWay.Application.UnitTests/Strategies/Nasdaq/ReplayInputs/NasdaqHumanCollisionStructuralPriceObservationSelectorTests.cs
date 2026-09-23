using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanCollisionStructuralPriceObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanCollisionStructuralPriceObservationSelector selector = new();

    [Fact]
    public void ReturnsMissingForNoMatchingCollisionEvidenceOrUnrelatedInput()
    {
        var unrelated = new NasdaqPreparationCompletionObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, DateOnly.FromDateTime(Start.DateTime), Start.AddHours(20), "review:preparation");

        var noInputs = selector.Select(Context(20), Episode());
        var result = selector.Select(Context(20, unrelated, Observation(12, 100m)), Episode());

        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing, noInputs.Kind);
        Assert.Null(noInputs.StructuralPrice);
        Assert.Empty(noInputs.SupportingObservations);
        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing, result.Kind);
        Assert.Empty(result.DistinctStructuralPrices);
    }

    [Fact]
    public void SelectsOneExactPriceAndPreservesAllCompatibleEvidenceWithoutAuthorityChoice()
    {
        var first = Observation(8, 100.1234m, observedAtHour: 16, source: "review:z");
        var duplicate = Observation(8, 100.1234m, observedAtHour: 20, source: "review:a");

        var result = selector.Select(Context(20, duplicate, first), Episode());

        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, result.Kind);
        Assert.Equal(100.1234m, result.StructuralPrice);
        Assert.Equal([first, duplicate], result.SupportingObservations);
        Assert.Equal([100.1234m], result.DistinctStructuralPrices);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanCollisionStructuralPriceObservation>)result.SupportingObservations).Clear());
    }

    [Fact]
    public void DifferentPricesConflictWithoutWinnerRegardlessOfMajorityOrInputOrder()
    {
        var first = Observation(8, 100m, source: "review:z");
        var duplicate = Observation(8, 100m, source: "review:a");
        var conflict = Observation(8, 100.0001m, source: "review:conflict");

        var result = selector.Select(Context(20, conflict, duplicate, first), Episode());
        var repeated = selector.Select(Context(20, first, conflict, duplicate), Episode());

        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict, result.Kind);
        Assert.Null(result.StructuralPrice);
        Assert.Equal([duplicate, conflict, first], result.SupportingObservations);
        Assert.Equal([100m, 100.0001m], result.DistinctStructuralPrices);
        Assert.Equal(result.SupportingObservations, repeated.SupportingObservations);
        Assert.Equal(result.DistinctStructuralPrices, repeated.DistinctStructuralPrices);
    }

    [Fact]
    public void FutureConflictDoesNotChangeEarlierUniqueSelection()
    {
        var first = Observation(8, 100m, observedAtHour: 16, source: "review:early");
        var futureConflict = Observation(8, 101m, observedAtHour: 20, source: "review:later");

        var early = selector.Select(Context(16, first, futureConflict), Episode());
        var later = selector.Select(Context(20, first, futureConflict), Episode());
        var repeated = selector.Select(Context(20, first, futureConflict), Episode());

        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique, early.Kind);
        Assert.Equal(100m, early.StructuralPrice);
        Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict, later.Kind);
        Assert.Equal(later.DistinctStructuralPrices, repeated.DistinctStructuralPrices);
    }

    [Fact]
    public void EpisodeIdentityIsolatesCollisionCandleSideAndBaseReconstructionIdentity()
    {
        var observation = Observation(8, 100m);
        var context = Context(20, observation);
        var otherCollision = selector.Select(context, Episode(collisionHour: 12));
        var otherInvalidation = selector.Select(context, Episode(invalidationHour: 2));
        var otherSide = selector.Select(context, Episode(side: StructuralCandidateExtremeSide.Upper));
        var otherVersion = selector.Select(context, Episode(version: new("other")));
        var otherProvider = selector.Select(context, Episode(provider: new("other")));
        var otherSymbol = selector.Select(context, Episode(symbol: new("OTHER")));

        Assert.All([otherCollision, otherInvalidation, otherSide, otherVersion, otherProvider, otherSymbol], result =>
            Assert.Equal(NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing, result.Kind));
    }

    private static NasdaqHumanCollisionStructuralPriceEpisode Episode(int invalidationHour = 4, int collisionHour = 8,
        StructuralCandidateExtremeSide side = StructuralCandidateExtremeSide.Lower, StrategyVersion? version = null,
        MarketDataProviderId? provider = null, MarketSymbol? symbol = null) => new(
            new NasdaqHumanOriginVertexEpisode(
                MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
                version ?? MoneyWayNasdaqStrategyDefinition.Instance.Version,
                provider ?? Provider,
                symbol ?? Symbol,
                Start.AddHours(invalidationHour)),
            Start.AddHours(collisionHour),
            side);

    private static NasdaqHumanCollisionStructuralPriceObservation Observation(int collisionHour, decimal price,
        int invalidationHour = 4, StructuralCandidateExtremeSide side = StructuralCandidateExtremeSide.Lower,
        int observedAtHour = 20, string source = "review:collision") => new(
            new NasdaqHumanOriginVertexEpisode(
                MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
                MoneyWayNasdaqStrategyDefinition.Instance.Version,
                Provider,
                Symbol,
                Start.AddHours(invalidationHour)),
            Start.AddHours(collisionHour),
            side,
            price,
            Start.AddHours(observedAtHour),
            source);

    private static StrategyReplayContext Context(int asOfHour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var candle = new Candle(Provider, Symbol, minute, Start.AddHours(asOfHour - 1), Start.AddHours(asOfHour),
            100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, minute, [candle])]);
        cursor.TryAdvance(out var frame);
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }
}
