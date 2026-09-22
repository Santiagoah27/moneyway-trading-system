using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanRebuiltCandidateVertexObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanRebuiltCandidateVertexObservationSelector selector = new();

    [Fact]
    public void ReturnsMissingForNoMatchingRebuiltEvidenceOrUnrelatedInputTypes()
    {
        var episode = Episode();
        var preparation = new NasdaqPreparationCompletionObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, DateOnly.FromDateTime(Start.DateTime), Start.AddHours(20), "review:preparation");
        var otherMigration = Observation(12, [12]);

        var noInputs = selector.Select(Context(20), episode);
        var unrelated = selector.Select(Context(20, preparation, otherMigration), episode);

        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing, noInputs.Kind);
        Assert.Empty(noInputs.SemanticMemberOpenTimesUtc);
        Assert.Empty(noInputs.SupportingObservations);
        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing, unrelated.Kind);
        Assert.Empty(unrelated.DistinctMemberships);
    }

    [Fact]
    public void SelectsOneExactMembershipAndPreservesCompatibleEvidenceWithoutAuthorityChoice()
    {
        var first = Observation(8, [8, 12], observedAtHour: 16, source: "review:z");
        var duplicate = Observation(8, [12, 8], observedAtHour: 20, source: "review:a");

        var result = selector.Select(Context(20, duplicate, first), Episode());

        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique, result.Kind);
        Assert.Equal([Start.AddHours(8), Start.AddHours(12)], result.SemanticMemberOpenTimesUtc);
        Assert.Equal([first, duplicate], result.SupportingObservations);
        Assert.Single(result.DistinctMemberships);
        Assert.Equal([Start.AddHours(8), Start.AddHours(12)], result.DistinctMemberships.Single());
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanRebuiltCandidateVertexObservation>)result.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData(new[] { 8 }, new[] { 8, 12 })]
    [InlineData(new[] { 8, 12 }, new[] { 8, 12, 16 })]
    public void DifferentIncludingSubsetAndSupersetMembershipsProduceConflictWithoutWinner(int[] firstMembers, int[] secondMembers)
    {
        var first = Observation(8, firstMembers, source: "review:first");
        var conflict = Observation(8, secondMembers, source: "review:conflict", observedAtHour: 24);

        var result = selector.Select(Context(24, first, conflict), Episode());

        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict, result.Kind);
        Assert.Empty(result.SemanticMemberOpenTimesUtc);
        Assert.Equal(2, result.SupportingObservations.Count);
        Assert.Equal(2, result.DistinctMemberships.Count);
    }

    [Fact]
    public void CompatibleMajorityAndInputOrderDoNotResolveOrChangeOneContradictoryMembership()
    {
        var first = Observation(8, [8, 12], source: "review:z");
        var duplicate = Observation(8, [12, 8], source: "review:a");
        var conflict = Observation(8, [8], source: "review:conflict");

        var result = selector.Select(Context(20, conflict, duplicate, first), Episode());
        var repeated = selector.Select(Context(20, first, conflict, duplicate), Episode());

        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict, result.Kind);
        Assert.Empty(result.SemanticMemberOpenTimesUtc);
        Assert.Equal([duplicate, conflict, first], result.SupportingObservations);
        Assert.Equal(result.SupportingObservations, repeated.SupportingObservations);
        Assert.Equal(result.DistinctMemberships, repeated.DistinctMemberships);
    }

    [Fact]
    public void EpisodeIdentityPreventsOtherMigrationInvalidationSideAndContextIdentitiesFromParticipating()
    {
        var observation = Observation(8, [8]);
        var context = Context(20, observation);
        var otherMigration = selector.Select(context, Episode(migrationHour: 12));
        var otherInvalidation = selector.Select(context, Episode(invalidationHour: 2));
        var otherSide = selector.Select(context, Episode(side: StructuralCandidateExtremeSide.Upper));
        var otherVersion = selector.Select(context, Episode(version: new("other")));
        var otherProvider = selector.Select(context, Episode(provider: new("other")));
        var otherSymbol = selector.Select(context, Episode(symbol: new("OTHER")));

        Assert.All([otherMigration, otherInvalidation, otherSide, otherVersion, otherProvider, otherSymbol], result =>
            Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing, result.Kind));
    }

    [Fact]
    public void FutureConflictingObservationDoesNotAlterEarlierSelectionButConflictsWhenVisible()
    {
        var first = Observation(8, [8], observedAtHour: 16, source: "review:early");
        var futureConflict = Observation(8, [8, 12], observedAtHour: 20, source: "review:later");

        var early = selector.Select(Context(16, first, futureConflict), Episode());
        var later = selector.Select(Context(20, first, futureConflict), Episode());

        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique, early.Kind);
        Assert.Equal([Start.AddHours(8)], early.SemanticMemberOpenTimesUtc);
        Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict, later.Kind);
    }

    private static NasdaqHumanRebuiltCandidateVertexEpisode Episode(int invalidationHour = 4, int migrationHour = 8,
        StructuralCandidateExtremeSide side = StructuralCandidateExtremeSide.Lower, StrategyVersion? version = null,
        MarketDataProviderId? provider = null, MarketSymbol? symbol = null) => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, version ?? MoneyWayNasdaqStrategyDefinition.Instance.Version,
            provider ?? Provider, symbol ?? Symbol, Start.AddHours(invalidationHour), Start.AddHours(migrationHour), side);

    private static NasdaqHumanRebuiltCandidateVertexObservation Observation(int migrationHour, int[] members,
        int invalidationHour = 4, StructuralCandidateExtremeSide side = StructuralCandidateExtremeSide.Lower,
        int observedAtHour = 20, string source = "review:rebuilt") => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, Start.AddHours(invalidationHour), Start.AddHours(migrationHour), side,
            members.Select(hour => Start.AddHours(hour)), Start.AddHours(observedAtHour), source);

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
