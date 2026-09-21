using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanOriginVertexObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanOriginVertexObservationSelector selector = new();

    [Fact]
    public void ReturnsMissingForNoMatchingOriginEvidenceOrUnrelatedInputTypes()
    {
        var episode = Episode(12);
        var preparation = new NasdaqPreparationCompletionObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, DateOnly.FromDateTime(Start.DateTime), Start.AddHours(16), "review:preparation");
        var otherInvalidation = Observation(16, [0]);

        var noInputs = selector.Select(Context(16), episode);
        var unrelated = selector.Select(Context(16, preparation, otherInvalidation), episode);

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Missing, noInputs.Kind);
        Assert.Empty(noInputs.SemanticMemberOpenTimesUtc); Assert.Empty(noInputs.SupportingObservations);
        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Missing, unrelated.Kind);
        Assert.Empty(unrelated.SupportingObservations);
    }

    [Fact]
    public void SelectsOneExactMembershipAndPreservesItsEvidence()
    {
        var observation = Observation(12, [4, 0], observedAtHour: 16, source: "review:a");
        var result = selector.Select(Context(16, observation), Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Unique, result.Kind);
        Assert.Equal([Start, Start.AddHours(4)], result.SemanticMemberOpenTimesUtc);
        Assert.Equal([observation], result.SupportingObservations);
        Assert.Equal([Start, Start.AddHours(4)], result.DistinctMemberships.Single());
    }

    [Fact]
    public void CompatibleDuplicatesRemainUniqueAndRetainAllProvenance()
    {
        var first = Observation(12, [0, 4], observedAtHour: 16, source: "review:z");
        var second = Observation(12, [4, 0], observedAtHour: 20, source: "review:a");
        var result = selector.Select(Context(20, second, first), Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Unique, result.Kind);
        Assert.Equal([Start, Start.AddHours(4)], result.SemanticMemberOpenTimesUtc);
        Assert.Equal(2, result.SupportingObservations.Count);
        Assert.Equal([first, second], result.SupportingObservations);
        Assert.Single(result.DistinctMemberships);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanOriginVertexObservation>)result.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData(0, 4, 8)]
    [InlineData(0, 8, 4)]
    public void DifferentMembershipsProduceConflictWithoutAWinningSet(int firstMember, int secondMember, int conflictingMember)
    {
        var first = Observation(12, [firstMember, secondMember], source: "review:first");
        var conflicting = Observation(12, [firstMember, conflictingMember], source: "review:conflict");
        var result = selector.Select(Context(20, first, conflicting), Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, result.Kind);
        Assert.Empty(result.SemanticMemberOpenTimesUtc);
        Assert.Equal(2, result.SupportingObservations.Count);
        Assert.Equal(2, result.DistinctMemberships.Count);
    }

    [Fact]
    public void CompatibleMajorityDoesNotResolveOneConflictingMembership()
    {
        var first = Observation(12, [0, 4], source: "review:a");
        var duplicate = Observation(12, [4, 0], source: "review:b");
        var conflicting = Observation(12, [0, 4, 8], source: "review:c");

        var result = selector.Select(Context(20, first, duplicate, conflicting), Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, result.Kind);
        Assert.Empty(result.SemanticMemberOpenTimesUtc);
        Assert.Equal(3, result.SupportingObservations.Count);
    }

    [Fact]
    public void TargetEpisodeIdentityPreventsDifferentVersionProviderSymbolAndInvalidationFromParticipating()
    {
        var observation = Observation(12, [0]);
        var context = Context(20, observation);
        var otherInvalidation = selector.Select(context, Episode(16));
        var otherVersion = selector.Select(context, Episode(12, version: new("other")));
        var otherProvider = selector.Select(context, Episode(12, provider: new("other")));
        var otherSymbol = selector.Select(context, Episode(12, symbol: new("OTHER")));
        var otherStrategy = selector.Select(context, Episode(12, strategyId: new("moneyway-forex")));

        Assert.All([otherInvalidation, otherVersion, otherProvider, otherSymbol, otherStrategy], result =>
            Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Missing, result.Kind));
    }

    [Fact]
    public void FutureConflictingObservationDoesNotAlterEarlierVisibleSelection()
    {
        var first = Observation(12, [0], observedAtHour: 16, source: "review:early");
        var futureConflict = Observation(12, [4], observedAtHour: 20, source: "review:later");
        var atT1 = Context(16, first, futureConflict);
        var atT2 = Context(20, first, futureConflict);

        var early = selector.Select(atT1, Episode(12));
        var later = selector.Select(atT2, Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Unique, early.Kind);
        Assert.Equal([Start], early.SemanticMemberOpenTimesUtc);
        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, later.Kind);
    }

    [Fact]
    public void InputOrderDoesNotChangeConflictOrDeterministicSupportingEvidenceOrder()
    {
        var older = Observation(12, [0], observedAtHour: 16, source: "review:z");
        var newer = Observation(12, [4], observedAtHour: 20, source: "review:a");
        var first = selector.Select(Context(20, newer, older), Episode(12));
        var second = selector.Select(Context(20, older, newer), Episode(12));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, first.Kind);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.DistinctMemberships, second.DistinctMemberships);
        Assert.Equal([older, newer], first.SupportingObservations);
        Assert.Equal(first.SupportingObservations, second.SupportingObservations);
    }

    private static NasdaqHumanOriginVertexEpisode Episode(int invalidationHour, StrategyVersion? version = null,
        MarketDataProviderId? provider = null, MarketSymbol? symbol = null, StrategyId? strategyId = null) => new(
            strategyId ?? MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
            version ?? MoneyWayNasdaqStrategyDefinition.Instance.Version,
            provider ?? Provider, symbol ?? Symbol, Start.AddHours(invalidationHour));

    private static NasdaqHumanOriginVertexObservation Observation(int invalidationHour, int[] members,
        int observedAtHour = 20, string source = "review:origin") => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
            MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, Start.AddHours(invalidationHour), members.Select(hour => Start.AddHours(hour)),
            Start.AddHours(observedAtHour), source);

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
