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

public sealed class NasdaqHumanRebuiltCandidateVertexObservationTests
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObservationPreservesAdr0009PayloadAndUsesH4Only()
    {
        var invalidation = Start;
        var migration = Start.AddHours(4);
        var observation = Observation(invalidation, migration, StructuralCandidateExtremeSide.Lower,
            [migration, Start.AddHours(8)], Start.AddHours(12), "review:rebuilt-1");

        Assert.IsAssignableFrom<IStrategyReplayInputObservation>(observation);
        Assert.Equal(Definition.StrategyId, observation.StrategyId);
        Assert.Equal(Definition.Version, observation.StrategyVersion);
        Assert.Equal(Provider, observation.ProviderId);
        Assert.Equal(Symbol, observation.Symbol);
        Assert.Equal(NasdaqHumanOriginVertexObservation.H4, observation.Timeframe);
        Assert.Equal(invalidation, observation.InvalidatingCandleOpenTimeUtc);
        Assert.Equal(migration, observation.MigrationCandleOpenTimeUtc);
        Assert.Equal(StructuralCandidateExtremeSide.Lower, observation.CandidateSide);
        Assert.Equal([migration, Start.AddHours(8)], observation.SelectedMemberOpenTimesUtc);
        Assert.Equal(Start.AddHours(12), observation.ObservedAtUtc);
        Assert.Equal("review:rebuilt-1", observation.SourceReference);
    }

    [Fact]
    public void MembershipIsAnImmutableDistinctSetThatMustContainMigrationCandle()
    {
        var migration = Start.AddHours(4);
        var input = new List<DateTimeOffset> { Start.AddHours(8), migration };
        var observation = Observation(Start, migration, StructuralCandidateExtremeSide.Lower, input);
        input.Clear();

        Assert.Equal([migration, Start.AddHours(8)], observation.SelectedMemberOpenTimesUtc);
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)observation.SelectedMemberOpenTimesUtc)
            .Add(Start.AddHours(12)));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower, []));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower, [migration, migration]));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower, [Start.AddHours(8)]));
    }

    [Fact]
    public void ObservationRejectsInvalidPayloadAndRequiresCausalObservationTime()
    {
        var migration = Start.AddHours(4);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanRebuiltCandidateVertexObservation(
            new("moneyway-forex"), Definition.Version, Provider, Symbol, Start, migration,
            StructuralCandidateExtremeSide.Lower, [migration], Start.AddHours(8), "source"));
        Assert.Throws<ArgumentException>(() => Observation(Start.ToOffset(TimeSpan.FromHours(-5)), migration,
            StructuralCandidateExtremeSide.Lower, [migration]));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration.ToOffset(TimeSpan.FromHours(-5)),
            StructuralCandidateExtremeSide.Lower, [migration]));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower,
            [migration.ToOffset(TimeSpan.FromHours(-5))]));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower,
            [migration], Start.AddHours(7)));
        Assert.Throws<ArgumentException>(() => Observation(Start, migration, StructuralCandidateExtremeSide.Lower,
            [migration], Start.AddHours(8), " "));
    }

    [Fact]
    public void EqualityIsOrderIndependentButRebuildEpisodeIdentityRemainsDistinct()
    {
        var migration = Start.AddHours(4);
        var first = Observation(Start, migration, StructuralCandidateExtremeSide.Lower, [migration, Start.AddHours(8)]);
        var equivalent = Observation(Start, migration, StructuralCandidateExtremeSide.Lower, [Start.AddHours(8), migration]);
        var differentMigration = Observation(Start, Start.AddHours(8), StructuralCandidateExtremeSide.Lower,
            [Start.AddHours(8)], Start.AddHours(12));
        var differentInvalidation = Observation(Start.AddHours(1), migration, StructuralCandidateExtremeSide.Lower,
            [migration, Start.AddHours(8)]);
        var differentSide = Observation(Start, migration, StructuralCandidateExtremeSide.Upper,
            [migration, Start.AddHours(8)]);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(first, differentMigration);
        Assert.NotEqual(first, differentInvalidation);
        Assert.NotEqual(first, differentSide);
    }

    [Fact]
    public void ObservationOwnsMembershipOnlyAndIsVisibleThroughExistingGenericCausalFiltering()
    {
        var observedAt = Start.AddMinutes(20);
        var migration = Start.AddHours(-4);
        var observation = Observation(Start.AddHours(-8), migration, StructuralCandidateExtremeSide.Lower,
            [migration], observedAt, "review:causal");
        var captured = new List<StrategyReplayContext>();
        var facade = new GenerateCanonicalMultiTimeframeBacktestUseCase(
            new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(
                new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new([new CaptureEvaluator(captured)])), new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

        facade.Execute(Definition, [CandleSeries(10, 20, 30)], [observation]);

        Assert.Empty(captured[0].InputObservations);
        Assert.Same(observation, Assert.Single(captured[1].InputObservations));
        Assert.Same(observation, Assert.Single(captured[2].InputObservations));
        var properties = typeof(NasdaqHumanRebuiltCandidateVertexObservation).GetProperties();
        Assert.Null(typeof(NasdaqHumanRebuiltCandidateVertexObservation).GetProperty("StructuralPrice"));
        Assert.Null(typeof(NasdaqHumanRebuiltCandidateVertexObservation).GetProperty("ProtectionAnchor"));
        Assert.Null(typeof(NasdaqHumanRebuiltCandidateVertexObservation).GetProperty("Verdict"));
        Assert.All(properties, property => Assert.False(property.CanWrite));
    }

    private static NasdaqHumanRebuiltCandidateVertexObservation Observation(
        DateTimeOffset invalidation,
        DateTimeOffset migration,
        StructuralCandidateExtremeSide side,
        IEnumerable<DateTimeOffset> members,
        DateTimeOffset? observedAt = null,
        string source = "review:rebuilt") =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, invalidation, migration, side, members,
            observedAt ?? members.DefaultIfEmpty(migration).Max().AddHours(4), source);

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
