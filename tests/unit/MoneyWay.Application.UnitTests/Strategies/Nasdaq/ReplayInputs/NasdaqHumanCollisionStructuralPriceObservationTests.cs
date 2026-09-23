using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanCollisionStructuralPriceObservationTests
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObservationPreservesTheAdr0010CollisionIdentityAndExactHumanPrice()
    {
        var episode = Episode();
        var collision = Start.AddHours(8);
        var observation = Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 12345.6789m,
            collision.AddHours(4), "review:collision-price");

        Assert.IsAssignableFrom<IStrategyReplayInputObservation>(observation);
        Assert.Same(episode, observation.Episode);
        Assert.Equal(Definition.StrategyId, observation.StrategyId);
        Assert.Equal(Definition.Version, observation.StrategyVersion);
        Assert.Equal(Provider, observation.ProviderId);
        Assert.Equal(Symbol, observation.Symbol);
        Assert.Equal(NasdaqHumanOriginVertexObservation.H4, observation.Timeframe);
        Assert.Equal(Start, observation.InvalidatingCandleOpenTimeUtc);
        Assert.Equal(collision, observation.CollisionCandleOpenTimeUtc);
        Assert.Equal(StructuralCandidateExtremeSide.Lower, observation.CandidateSide);
        Assert.Equal(12345.6789m, observation.StructuralPrice);
        Assert.Equal(collision.AddHours(4), observation.ObservedAtUtc);
        Assert.Equal("review:collision-price", observation.SourceReference);
    }

    [Fact]
    public void ObservationRequiresValidCausalAndProvenancePayload()
    {
        var episode = Episode();
        var collision = Start.AddHours(8);

        Assert.Throws<ArgumentException>(() => Observation(episode, collision.ToOffset(TimeSpan.FromHours(-5)), StructuralCandidateExtremeSide.Lower, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Observation(episode, collision, (StructuralCandidateExtremeSide)99, 1m));
        Assert.Throws<ArgumentException>(() => Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 1m, collision.AddHours(3)));
        Assert.Throws<ArgumentException>(() => Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 1m, collision.AddHours(4).ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 1m, collision.AddHours(4), " "));
        Assert.Throws<ArgumentException>(() => Observation(new NasdaqHumanOriginVertexEpisode(new("moneyway-forex"), Definition.Version, Provider, Symbol, Start), collision, StructuralCandidateExtremeSide.Lower, 1m));
    }

    [Fact]
    public void EqualityRetainsEvidenceFieldsAndKeepsCollisionEpisodesDistinct()
    {
        var episode = Episode();
        var collision = Start.AddHours(8);
        var first = Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 100.1234m);
        var equivalent = Observation(Episode(), collision, StructuralCandidateExtremeSide.Lower, 100.1234m);
        var differentVersion = Observation(new NasdaqHumanOriginVertexEpisode(Definition.StrategyId, new("v-other"), Provider, Symbol, Start), collision, StructuralCandidateExtremeSide.Lower, 100.1234m);
        var differentInvalidation = Observation(Episode(Start.AddHours(4)), collision, StructuralCandidateExtremeSide.Lower, 100.1234m);
        var differentCollision = Observation(episode, collision.AddHours(4), StructuralCandidateExtremeSide.Lower, 100.1234m);
        var differentSide = Observation(episode, collision, StructuralCandidateExtremeSide.Upper, 100.1234m);
        var differentPrice = Observation(episode, collision, StructuralCandidateExtremeSide.Lower, 100.1235m);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(first, differentVersion);
        Assert.NotEqual(first, differentInvalidation);
        Assert.NotEqual(first, differentCollision);
        Assert.NotEqual(first, differentSide);
        Assert.NotEqual(first, differentPrice);
    }

    [Fact]
    public void ObservationUsesGenericCausalTransportWithoutGeometryOrProtectionPayload()
    {
        var observedAt = Start.AddMinutes(20);
        var observation = Observation(Episode(Start.AddHours(-8)), Start.AddHours(-4), StructuralCandidateExtremeSide.Lower,
            100.1234m, observedAt, "review:causal");
        var captured = new List<StrategyReplayContext>();
        var facade = new GenerateCanonicalMultiTimeframeBacktestUseCase(
            new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(
                new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new([new CaptureEvaluator(captured)])), new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

        facade.Execute(Definition, [CandleSeries(10, 20, 30)], [observation]);

        Assert.Empty(captured[0].InputObservations);
        Assert.Same(observation, Assert.Single(captured[1].InputObservations));
        Assert.Same(observation, Assert.Single(captured[2].InputObservations));
        var properties = typeof(NasdaqHumanCollisionStructuralPriceObservation).GetProperties();
        Assert.Null(typeof(NasdaqHumanCollisionStructuralPriceObservation).GetProperty("ProtectionAnchor"));
        Assert.Null(typeof(NasdaqHumanCollisionStructuralPriceObservation).GetProperty("StructuralPriceSourceCandleOpenTimeUtc"));
        Assert.Null(typeof(NasdaqHumanCollisionStructuralPriceObservation).GetProperty("RuleStatus"));
        Assert.Null(typeof(NasdaqHumanCollisionStructuralPriceObservation).GetProperty("StrategyVerdict"));
        Assert.All(properties, property => Assert.False(property.CanWrite));
    }

    private static NasdaqHumanCollisionStructuralPriceObservation Observation(
        NasdaqHumanOriginVertexEpisode episode,
        DateTimeOffset collision,
        StructuralCandidateExtremeSide side,
        decimal price,
        DateTimeOffset? observedAt = null,
        string source = "review:collision") =>
        new(episode, collision, side, price, observedAt ?? collision.AddHours(4), source);

    private static NasdaqHumanOriginVertexEpisode Episode(DateTimeOffset? invalidation = null) =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, invalidation ?? Start);

    private static CandleSeries CandleSeries(params int[] closes) => new(
        Provider, Symbol, new Timeframe(1, TimeframeUnit.Minute), closes.Select(close => new Candle(
            Provider, Symbol, new(1, TimeframeUnit.Minute), Start.AddMinutes(close - 1), Start.AddMinutes(close),
            100, 101, 99, 100, null)));

    private sealed class CaptureEvaluator(List<StrategyReplayContext> captured) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Definition.StrategyId;
        public StrategyVersion StrategyVersion => Definition.Version;
        public RuleId RuleId { get; } = new("NQ-TIME-001");

        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            captured.Add(context);
            return new(RuleEvaluationResult.Waiting, "Generic input visibility probe.", null);
        }
    }
}
