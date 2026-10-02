using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionExtremeMembershipPendingEvidenceReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer reducer = new();

    [Fact]
    public void MissingPreservesOriginalPendingStateDespiteSeveralLaterMarketCandles()
    {
        var pending = Pending();
        var context = EvidenceContext(pending, 64);
        var result = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Missing>(reducer.Reduce(pending, context));
        AssertFrozen(pending, result);
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing>(result.Selection);
        Assert.True(context.TryGetFrame(H4, out var frame));
        Assert.Equal(4, frame!.AvailableCandles.Count);
        Assert.Equal(Start.AddHours(64), frame.CurrentCandle.CloseTimeUtc);
        Assert.Empty(context.InputObservations);
        Assert.Equal(result.GetType(), reducer.Reduce(pending, context).GetType());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UniqueRetainsSemanticMembershipAndEveryCompatibleObservation(bool duplicates)
    {
        var pending = Pending();
        var a = Observation(pending, [20, 16], 40, "review:a");
        var b = Observation(pending, [16, 20], 48, "review:b");
        var observations = duplicates ? new[] { a, b } : [a];
        var context = EvidenceContext(pending, 64, observations);
        var result = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(reducer.Reduce(pending, context));
        AssertFrozen(pending, result);
        var expected = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(
            new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, pending.MembershipEvent));
        Assert.Equal(expected.SemanticMemberOpenTimesUtc, result.Selection.SemanticMemberOpenTimesUtc);
        Assert.Equal(expected.SupportingObservations, result.Selection.SupportingObservations);
        Assert.Equal(observations, result.Selection.SupportingObservations);
        Assert.Same(a, result.Selection.SupportingObservations[0]);
        Assert.Equal("review:a", result.Selection.SupportingObservations[0].SourceReference);
        Assert.Equal(Start.AddHours(40), result.Selection.SupportingObservations[0].ObservedAtUtc);
        if (duplicates) Assert.Same(b, result.Selection.SupportingObservations[1]);
        Assert.Equal(observations, context.InputObservations.Cast<NasdaqHumanPostCompletionActiveExtremeObservation>());
        var repeated = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(reducer.Reduce(pending, context));
        Assert.Equal(result.Selection.SupportingObservations, repeated.Selection.SupportingObservations);
        Assert.Equal(result.Selection.SemanticMemberOpenTimesUtc, repeated.Selection.SemanticMemberOpenTimesUtc);
    }

    [Fact]
    public void FutureEvidenceBecomesLateUniqueForTheSameOriginalTurnWithoutRewritingEarlierFrame()
    {
        var pending = Pending();
        var late = Observation(pending, [16, 20], 48, "review:late");
        var early = EvidenceContext(pending, 40, late);
        var withNone = reducer.Reduce(pending, EvidenceContext(pending, 40));
        var missing = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Missing>(reducer.Reduce(pending, early));
        Assert.Equal(withNone.GetType(), missing.GetType());
        Assert.Empty(early.InputObservations);
        var ready = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            reducer.Reduce(pending, EvidenceContext(pending, 48, late)));
        Assert.Same(late, Assert.Single(ready.Selection.SupportingObservations));
        AssertFrozen(pending, missing);
        AssertFrozen(pending, ready);
        Assert.Empty(early.InputObservations);
        Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Missing>(reducer.Reduce(pending, early));
    }

    [Fact]
    public void ConflictRetainsAllAssertionsAndDoesNotResolveThroughLaterDuplicate()
    {
        var pending = Pending();
        var a = Observation(pending, [16, 20], 40, "review:a");
        var b = Observation(pending, [20], 48, "review:b");
        var duplicate = Observation(pending, [20], 64, "review:later-b");
        var before = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Conflict>(
            reducer.Reduce(pending, EvidenceContext(pending, 48, a, b, duplicate)));
        var context = EvidenceContext(pending, 64, a, b, duplicate);
        var after = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Conflict>(reducer.Reduce(pending, context));
        Assert.Equal([a, b], before.Selection.SupportingObservations);
        Assert.Equal([a, b, duplicate], after.Selection.SupportingObservations);
        Assert.Equal(Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict>(
            new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, pending.MembershipEvent)).SupportingObservations,
            after.Selection.SupportingObservations);
        AssertFrozen(pending, before);
        AssertFrozen(pending, after);
    }

    [Fact]
    public void AnotherEventAndUnrelatedObservationTypeAreIgnored()
    {
        var pending = Pending();
        var otherEvent = new NasdaqPostCompletionActiveExtremeMembershipEvent(pending.Episode, Candle(52, 145, 180, 100, 135, false));
        var other = new NasdaqHumanPostCompletionActiveExtremeObservation(otherEvent, [Start.AddHours(20)], Start.AddHours(64), "review:other-event");
        var identity = pending.Episode.PreviousCompletedEpisode;
        var unrelated = new NasdaqHumanOriginVertexObservation(identity.StrategyId, identity.StrategyVersion, Provider, Symbol,
            identity.InvalidatingCandleOpenTimeUtc, [Start], Start.AddHours(40), "review:origin");
        var result = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Missing>(
            reducer.Reduce(pending, EvidenceContext(pending, 64, other, unrelated)));
        AssertFrozen(pending, result);
    }

    [Fact]
    public void UniqueMeansSelectionReadyWithoutValidatingOrResolvingMarketReferences()
    {
        var pending = Pending();
        // Upstream semantic agreement does not certify this post-turn timestamp; the resolver owns rejection.
        var observation = Observation(pending, [56], 64, "review:unchecked");
        var result = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            reducer.Reduce(pending, EvidenceContext(pending, 64, observation)));
        Assert.Equal([Start.AddHours(56)], result.Selection.SemanticMemberOpenTimesUtc);
        AssertFrozen(pending, result);
    }

    [Fact]
    public void RejectsNullInputsAndContextBeforeTheEstablishedTurn()
    {
        var pending = Pending();
        var context = EvidenceContext(pending, 40);
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(null!, context));
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(pending, null!));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(pending, Context([pending.Completion.MarketCursor])));
        var wrong = new Candle(new("other"), Symbol, H4, Start.AddHours(60), Start.AddHours(64), 100, 110, 90, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(wrong.ProviderId, Symbol, H4, [wrong])]);
        cursor.TryAdvance(out var frame);
        var wrongContext = new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(pending, wrongContext));
    }

    [Fact]
    public void ResultUnionHasOnlyPendingAndTypedSelectionPayloadsWithoutDownstreamOutcomes()
    {
        var type = typeof(NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult);
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetNestedTypes(), branch =>
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), property => Assert.Null(property.SetMethod));
            Assert.Equal(new[] { "MarketCursor", "PendingState", "Selection" }, branch.GetProperties().Select(property => property.Name).Order());
        });
    }

    private static void AssertFrozen(NasdaqPostCompletionExtremeMembershipPendingState pending,
        NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult result)
    {
        Assert.Same(pending, result.PendingState);
        Assert.Same(pending.CorrectionStartCandle, result.MarketCursor);
        Assert.Same(pending.Episode, result.PendingState.Episode);
        Assert.Same(pending.MembershipEvent, result.PendingState.MembershipEvent);
        Assert.Same(pending.ValidatedProtectedTurn, result.PendingState.ValidatedProtectedTurn);
        Assert.Equal(Start.AddHours(28), pending.MarketCursor.OpenTimeUtc);
    }

    private static NasdaqHumanPostCompletionActiveExtremeObservation Observation(
        NasdaqPostCompletionExtremeMembershipPendingState pending, int[] members, int observedHour, string source) =>
        new(pending.MembershipEvent, members.Select(hour => Start.AddHours(hour)), Start.AddHours(observedHour), source);

    private static StrategyReplayContext EvidenceContext(NasdaqPostCompletionExtremeMembershipPendingState pending,
        int asOfHour, params IStrategyReplayInputObservation[] observations)
    {
        var candles = new[] { pending.CorrectionStartCandle, Candle(36, 135, 180, 100, 145, false),
            Candle(44, 135, 180, 100, 145, false), Candle(60, 135, 180, 100, 145, false) };
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        throw new InvalidOperationException("Requested evidence frame was not found.");
    }

    private static NasdaqPostCompletionExtremeMembershipPendingState Pending()
    {
        var start = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(State(false), Candle(28, 145, 180, 100, 135, false)));
        return new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(start);
    }

    private static NasdaqPostCompletionActiveImpulseState State(bool bearish)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(12, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, C(16, 80, 100, 60, 90)).Candidate!;
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, C(20, 130, 140, 60, 131)));
        return new NasdaqPostCompletionActiveImpulseInitializer().Initialize(completed);
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close, bool bearish) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            bearish ? 200 - open : open, bearish ? 200 - low : high, bearish ? 200 - high : low,
            bearish ? 200 - close : close, null);
}
