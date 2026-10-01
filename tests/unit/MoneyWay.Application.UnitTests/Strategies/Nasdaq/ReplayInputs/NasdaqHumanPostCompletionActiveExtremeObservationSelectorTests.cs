using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanPostCompletionActiveExtremeObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanPostCompletionActiveExtremeObservationSelector selector = new();

    [Fact]
    public void MissingWithoutEvidenceAndWithOnlyFutureEvidence()
    {
        var evt = Event();
        var future = Observation(evt, [16, 20], 44, "review:future");
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing>(selector.Select(Context(40), evt));
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing>(selector.Select(Context(40, future), evt));
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(44, future), evt));
    }

    [Fact]
    public void IgnoresOtherTurnsEpisodesAndEvidenceDomains()
    {
        var evt = Event();
        var otherTurn = new NasdaqPostCompletionActiveExtremeMembershipEvent(evt.Episode, Candle(36, 140, 150, 120, 130));
        var otherEpisode = Event(offset: 4);
        var old = evt.Episode.PreviousCompletedEpisode;
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        IStrategyReplayInputObservation[] unrelated =
        [
            Observation(otherTurn, [16, 20], 44, "review:turn"),
            Observation(otherEpisode, [16, 20], 44, "review:episode"),
            new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
                old.InvalidatingCandleOpenTimeUtc, [Start], Start.AddHours(44), "review:origin"),
            new NasdaqHumanRebuiltCandidateVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
                old.InvalidatingCandleOpenTimeUtc, Start.AddHours(20), StructuralCandidateExtremeSide.Lower,
                [Start.AddHours(20)], Start.AddHours(44), "review:rebuilt"),
            new NasdaqHumanCollisionStructuralPriceObservation(old, Start.AddHours(20), StructuralCandidateExtremeSide.Lower,
                111, Start.AddHours(44), "review:price"),
        ];
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing>(selector.Select(Context(48, unrelated), evt));
        var matching = Observation(evt, [16, 20], 44, "review:match");
        var unique = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(48, [.. unrelated, matching]), evt));
        Assert.Same(matching, Assert.Single(unique.SupportingObservations));
    }

    [Fact]
    public void UniquePreservesExactMembersAndAllCompatibleProvenanceRegardlessOfInputOrder()
    {
        var evt = Event();
        var a = Observation(evt, [20, 16], 36, "review:a");
        var b = Observation(Event(), [16, 20], 40, "review:b");
        var first = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(44, b, a), evt));
        var second = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(44, a, b), evt));
        Assert.Equal([Start.AddHours(16), Start.AddHours(20)], first.SemanticMemberOpenTimesUtc);
        Assert.Equal(first.SemanticMemberOpenTimesUtc, second.SemanticMemberOpenTimesUtc);
        Assert.Equal(first.SupportingObservations, second.SupportingObservations);
        Assert.Same(a, first.SupportingObservations[0]);
        Assert.Same(b, first.SupportingObservations[1]);
        Assert.Equal("review:b", b.SourceReference);
        Assert.Equal(Start.AddHours(40), b.ObservedAtUtc);
        Assert.Equal([Start.AddHours(16), Start.AddHours(20)], a.SelectedMemberOpenTimesUtc);
        Assert.Throws<NotSupportedException>(() => ((IList<DateTimeOffset>)first.SemanticMemberOpenTimesUtc)[0] = Start);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanPostCompletionActiveExtremeObservation>)first.SupportingObservations).Clear());
    }

    [Fact]
    public void SingleUniqueDoesNotResolveAbsentOrNonContiguousCandleReferences()
    {
        var evt = Event();
        var observation = Observation(evt, [0, 20], 40, "review:members");
        var result = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(40, observation), evt));
        Assert.Equal(observation.SelectedMemberOpenTimesUtc, result.SemanticMemberOpenTimesUtc);
        Assert.Same(observation, Assert.Single(result.SupportingObservations));
    }

    [Fact]
    public void FutureConflictDoesNotChangeEarlierUniqueAndLaterDuplicatesCannotSupersede()
    {
        var evt = Event();
        var a = Observation(evt, [16, 20], 36, "review:a");
        var b = Observation(evt, [20], 44, "review:b");
        var c = Observation(evt, [20], 48, "review:c");
        var early = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(40, c, b, a), evt));
        var withoutFuture = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(selector.Select(Context(40, a), evt));
        Assert.Equal(early.SemanticMemberOpenTimesUtc, withoutFuture.SemanticMemberOpenTimesUtc);
        Assert.Equal(early.SupportingObservations, withoutFuture.SupportingObservations);
        var conflict = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(selector.Select(Context(44, c, b, a), evt));
        Assert.Equal([a, b], conflict.SupportingObservations);
        var later = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(selector.Select(Context(48, c, a, b), evt));
        Assert.Equal([a, b, c], later.SupportingObservations);
        Assert.Equal(later.SupportingObservations,
            Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(selector.Select(Context(48, b, c, a), evt)).SupportingObservations);
        Assert.Same(a, Assert.Single(early.SupportingObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanPostCompletionActiveExtremeObservation>)later.SupportingObservations).Clear());
        Assert.Same(evt, a.MembershipEvent);
    }

    [Fact]
    public void SameSourceAndTimeStillConflictDeterministicallyByDifferentMembership()
    {
        var evt = Event();
        var a = Observation(evt, [16, 20], 40, "review");
        var b = Observation(evt, [20], 40, "review");
        var first = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(selector.Select(Context(44, a, b), evt));
        var second = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(selector.Select(Context(44, b, a), evt));
        Assert.Equal(first.SupportingObservations, second.SupportingObservations);
    }

    [Fact]
    public void RejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, Event()));
        Assert.Throws<ArgumentNullException>(() => selector.Select(Context(40), null!));
    }

    private static NasdaqHumanPostCompletionActiveExtremeObservation Observation(
        NasdaqPostCompletionActiveExtremeMembershipEvent evt, int[] hours, int observedHour, string source) =>
        new(evt, hours.Select(hour => Start.AddHours(hour)), Start.AddHours(observedHour), source);

    private static StrategyReplayContext Context(int hour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var close = Start.AddHours(hour);
        var trigger = new Candle(Provider, Symbol, minute, close.AddMinutes(-1), close, 100, 110, 90, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, minute, [trigger])]);
        cursor.TryAdvance(out var frame);
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static NasdaqPostCompletionActiveExtremeMembershipEvent Event(int offset = 0)
    {
        var origin = Candle(offset, 100, 110, 85, 90);
        var prior = Candle(4 + offset, 105, 115, 100, 110);
        var invalidating = Candle(8 + offset, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12 + offset, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, Candle(16 + offset, 80, 100, 60, 90)).Candidate!;
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, Candle(20 + offset, 130, 140, 60, 131)));
        return new(NasdaqPostCompletionEpisode.FromCompleted(completed), Candle(28 + offset, 140, 150, 120, 130));
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
