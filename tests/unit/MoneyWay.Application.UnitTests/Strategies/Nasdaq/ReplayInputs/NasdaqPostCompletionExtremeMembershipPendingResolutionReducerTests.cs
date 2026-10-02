using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionExtremeMembershipPendingResolutionReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionExtremeMembershipPendingResolutionReducer reducer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvesExactChronologicalMembersAndPreservesFullFrozenAuditChain(bool bearish)
    {
        var pending = Pending(bearish);
        var a = Candle(16, 80, 100, 60, 90, bearish);
        var b = pending.Completion.MarketCursor;
        var first = Observation(pending, [20, 16], 40, "review:first");
        var second = Observation(pending, [16, 20], 48, "review:second");
        var context = ResolutionContext(pending, [a, b], 48, [first, second]);
        var ready = Ready(pending, context);
        var result = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(reducer.Reduce(ready, context));
        AssertFrozen(ready, result);
        Assert.Same(ready.Selection, result.Resolution.Selection);
        Assert.Same(pending.MembershipEvent, result.Resolution.MembershipEvent);
        Assert.Equal([a, b], result.Resolution.SelectedMembers);
        Assert.Same(a, result.Resolution.SelectedMembers[0]);
        Assert.Same(b, result.Resolution.SelectedMembers[1]);
        Assert.Equal([first, second], result.Resolution.Selection.SupportingObservations);
        Assert.Equal("review:second", result.Resolution.Selection.SupportingObservations[1].SourceReference);
        Assert.Equal(Start.AddHours(48), result.Resolution.Selection.SupportingObservations[1].ObservedAtUtc);
        var expected = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(
            new NasdaqHumanPostCompletionActiveExtremeMemberResolver().Evaluate(ready.Selection, context));
        Assert.Equal(expected.SelectedMembers, result.Resolution.SelectedMembers);
        var repeated = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(reducer.Reduce(ready, context));
        Assert.Equal(result.Resolution.SelectedMembers, repeated.Resolution.SelectedMembers);
        Assert.Same(ready.Selection, repeated.Resolution.Selection);
        Assert.Equal([first, second], context.InputObservations.Cast<NasdaqHumanPostCompletionActiveExtremeObservation>());
        Assert.True(context.TryGetFrame(H4, out var frame));
        Assert.Equal(4, frame!.AvailableCandles.Count);
    }

    [Fact]
    public void DataUnavailableRecoversSameHistoricalMemberAtSameAsOfWithoutMovingCursor()
    {
        var pending = Pending(false);
        var a = Candle(16, 80, 100, 60, 90, false);
        var b = pending.Completion.MarketCursor;
        var observation = Observation(pending, [16, 20], 40, "review:members");
        var absentContext = ResolutionContext(pending, [a], 48, [observation]);
        var ready = Ready(pending, absentContext);
        var unavailable = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.DataUnavailable>(reducer.Reduce(ready, absentContext));
        AssertFrozen(ready, unavailable);
        Assert.Same(ready.Selection, unavailable.Resolution.Selection);
        Assert.Equal([b.OpenTimeUtc], unavailable.Resolution.UnavailableCandleOpenTimesUtc);
        var expected = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable>(
            new NasdaqHumanPostCompletionActiveExtremeMemberResolver().Evaluate(ready.Selection, absentContext));
        Assert.Equal(expected.UnavailableCandleOpenTimesUtc, unavailable.Resolution.UnavailableCandleOpenTimesUtc);
        var recoveredContext = ResolutionContext(pending, [a, b], 48, [observation]);
        var recovered = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(reducer.Reduce(ready, recoveredContext));
        Assert.Equal(absentContext.AsOfUtc, recoveredContext.AsOfUtc);
        AssertFrozen(ready, recovered);
        Assert.Equal([a, b], recovered.Resolution.SelectedMembers);
        Assert.Equal([b.OpenTimeUtc], unavailable.Resolution.UnavailableCandleOpenTimesUtc);
    }

    [Fact]
    public void InvalidPostTurnMembershipPropagatesRegardlessOfPresenceOrLaterContext()
    {
        var pending = Pending(false);
        var observation = Observation(pending, [36], 48, "review:invalid");
        var absent = ResolutionContext(pending, [], 48, [observation]);
        var ready = Ready(pending, absent);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(ready, absent));
        var present = ResolutionContext(pending, [Candle(36, 135, 180, 100, 145, false)], 64, [observation]);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(ready, present));
        Assert.Same(pending, ready.PendingState);
    }

    [Fact]
    public void NonContiguousMembershipPropagatesInvalidInputInsteadOfUnavailable()
    {
        var pending = Pending(false);
        var observation = Observation(pending, [12, 20], 40, "review:skipped");
        var context = ResolutionContext(pending, [Candle(12, 130, 145, 95, 115, false),
            Candle(16, 80, 100, 60, 90, false), pending.Completion.MarketCursor], 48, [observation]);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(Ready(pending, context), context));
    }

    [Fact]
    public void FutureUnderlyingMarketDataDoesNotChangeHistoricalResolution()
    {
        var pending = Pending(false);
        var members = new[] { Candle(16, 80, 100, 60, 90, false), pending.Completion.MarketCursor };
        var observation = Observation(pending, [16, 20], 40, "review:historical");
        var original = ResolutionContext(pending, members, 48, [observation]);
        var withFuture = ResolutionContext(pending, [.. members, Candle(52, 190, 200, 170, 195, false)], 48, [observation]);
        var ready = Ready(pending, original);
        var before = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(reducer.Reduce(ready, original));
        var after = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(reducer.Reduce(ready, withFuture));
        Assert.Equal(before.Resolution.SelectedMembers, after.Resolution.SelectedMembers);
        AssertFrozen(ready, before);
        AssertFrozen(ready, after);
    }

    [Fact]
    public void NarrowInputAndClosedOutputExcludeGeometryAndOtherEvidenceBranches()
    {
        var pending = Pending(false);
        var context = ResolutionContext(pending, [], 48, [Observation(pending, [16], 40, "review:members")]);
        var ready = Ready(pending, context);
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(null!, context));
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(ready, null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionExtremeMembershipPendingResolutionReducer).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady), method.GetParameters()[0].ParameterType);
        var union = typeof(NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult);
        Assert.Empty(union.GetConstructors());
        Assert.All(union.GetNestedTypes(), branch =>
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), property => Assert.Null(property.SetMethod));
            Assert.Equal(new[] { "EvidenceReady", "MarketCursor", "PendingState", "Resolution" }, branch.GetProperties().Select(property => property.Name).Order());
        });
    }

    private static void AssertFrozen(NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady ready,
        NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult result)
    {
        Assert.Same(ready, result.EvidenceReady);
        Assert.Same(ready.PendingState, result.PendingState);
        Assert.Same(ready.PendingState.CorrectionStartCandle, result.MarketCursor);
        Assert.Same(ready.PendingState.Episode, result.PendingState.Episode);
        Assert.Same(ready.PendingState.Completion, result.PendingState.Completion);
        Assert.Same(ready.PendingState.ValidatedProtectedTurn, result.PendingState.ValidatedProtectedTurn);
    }

    private static NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady Ready(
        NasdaqPostCompletionExtremeMembershipPendingState pending, StrategyReplayContext context) =>
        Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            new NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer().Reduce(pending, context));

    private static NasdaqHumanPostCompletionActiveExtremeObservation Observation(
        NasdaqPostCompletionExtremeMembershipPendingState pending, int[] members, int hour, string source) =>
        new(pending.MembershipEvent, members.Select(member => Start.AddHours(member)), Start.AddHours(hour), source);

    private static StrategyReplayContext ResolutionContext(NasdaqPostCompletionExtremeMembershipPendingState pending,
        Candle[] historical, int asOfHour, IStrategyReplayInputObservation[] observations)
    {
        var candles = historical.Append(pending.CorrectionStartCandle)
            .Append(Candle(asOfHour - 4, 135, 180, 100, 145, false)).OrderBy(candle => candle.OpenTimeUtc).ToArray();
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        throw new InvalidOperationException("Historical frame not found.");
    }

    private static NasdaqPostCompletionExtremeMembershipPendingState Pending(bool bearish)
    {
        var start = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(State(bearish), Candle(28, 145, 180, 100, 135, bearish)));
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
