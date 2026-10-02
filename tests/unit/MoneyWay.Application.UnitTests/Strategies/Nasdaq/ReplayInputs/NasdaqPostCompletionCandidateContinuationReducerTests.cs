using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionCandidateContinuationReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionCandidateContinuationReducer reducer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchesLegacyContinuationAndPreservesExactFactsAndProvenanceAcrossTwoConsumedSteps(bool bearish)
    {
        var source = Candidate(bearish);
        var legacy = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        Assert.Null(source.Continuation);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var incoming = Candle(80, 150, 180, 90, 123, bearish);
        var result = calculator.Evaluate(source, incoming);
        var decision = Assert.IsType<NasdaqCandidateLifecycleDecision.CandidateContinues>(result.Decision);
        var state = reducer.Reduce(result);
        var old = Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(legacy), incoming)).State;
        Assert.Equal(old.CandidateGeometry, state.CandidateGeometry);
        Assert.Equal(old.CandidateSide, state.CandidateSide);
        Assert.Equal(old.CorrectionTurnCandles, state.CorrectionTurnCandles);
        Assert.Same(old.LastProcessedCandle, state.MarketCursor);
        Assert.Same(old.TerminalCandle, state.TerminalCandle);
        Assert.NotSame(source, state);
        Assert.Same(result, state.Continuation);
        Assert.Same(source, state.Continuation!.SourceState);
        Assert.Same(decision, state.Continuation.Decision);
        Assert.Same(decision.CandidateGeometry, state.CandidateGeometry);
        Assert.Same(source.CandidateFacts, state.CandidateFacts);
        Assert.Same(source.CorrectionTurnCandles, state.CorrectionTurnCandles);
        Assert.Same(source.TerminalCandle, state.TerminalCandle);
        Assert.Same(source.Provisional, state.Provisional);
        Assert.Same(source.SourceCorrection, state.SourceCorrection);
        Assert.Same(source.SourceCorrection.GeometryReady, state.SourceCorrection.GeometryReady);
        Assert.Same(source.Episode, state.Episode);
        Assert.Same(source.ActivePair, state.ActivePair);
        Assert.Same(source.ActivePair.ActiveExtreme, state.ActivePair.ActiveExtreme);
        Assert.Same(source.ActivePair.ProtectedTurn, state.ActivePair.ProtectedTurn);
        Assert.Same(incoming, state.MarketCursor);
        Assert.Same(result.MarketCursor, state.MarketCursor);
        Assert.True(source.MarketCursor.CloseTimeUtc < state.MarketCursor.CloseTimeUtc);
        Assert.DoesNotContain(incoming, state.CorrectionTurnCandles);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        Assert.Null(source.Continuation);
        var repeated = reducer.Reduce(result);
        Assert.Same(state.Continuation, repeated.Continuation);
        Assert.Same(state.CandidateGeometry, repeated.CandidateGeometry);
        Assert.Same(state.CorrectionTurnCandles, repeated.CorrectionTurnCandles);
        Assert.Same(state.MarketCursor, repeated.MarketCursor);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, incoming));
        var next = Candle(96, 120, 180, 90, 131, bearish);
        var nextResult = calculator.Evaluate(state, next);
        var nextState = reducer.Reduce(nextResult);
        Assert.Same(state, nextState.Continuation!.SourceState);
        Assert.Same(result, nextState.Continuation.SourceState.Continuation);
        Assert.Same(next, nextState.MarketCursor);
        Assert.Same(incoming, state.MarketCursor);
        Assert.Same(source.CandidateGeometry, nextState.CandidateGeometry);
        Assert.Same(source.CorrectionTurnCandles, nextState.CorrectionTurnCandles);
        Assert.DoesNotContain(next, nextState.CorrectionTurnCandles);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)nextState.CorrectionTurnCandles).Add(next));
    }

    [Theory]
    [InlineData(130, 90, 132)]
    [InlineData(130, 80, 132)]
    [InlineData(140, 80, 132)]
    [InlineData(125, 80, 123)]
    [InlineData(120, 80, 123)]
    public void RejectsEveryOtherCanonicalBranchWithoutMaterializingIt(decimal open, decimal low, decimal close)
    {
        var source = Candidate(false);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, Candle(48, open, 180, low, close, false));
        Assert.IsNotType<NasdaqCandidateLifecycleDecision.CandidateContinues>(result.Decision);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(result));
        Assert.Null(source.Continuation);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
    }

    [Fact]
    public void RejectsDetachedDecisionAndExposesOnlyResultToStateMaterialization()
    {
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(null!));
        var source = Candidate(false);
        var another = Candidate(false);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, Candle(48, 120, 180, 90, 123, false));
        var constructor = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionCandidateLifecycleResult)constructor.Invoke([another, result.Decision]);
        Assert.Throws<ArgumentException>(() => reducer.Reduce(detached));
        var method = Assert.Single(typeof(NasdaqPostCompletionCandidateContinuationReducer).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionCandidateLifecycleResult), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionCandidateState), method.ReturnType);
        Assert.All(typeof(NasdaqPostCompletionCandidateState).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    private static NasdaqPostCompletionCandidateState Candidate(bool bearish)
    {
        var correction = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var terminal = Candle(32, 120, 180, 90, 125, bearish);
        var observation = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
            new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(correction, terminal));
        var provisional = new NasdaqPostCompletionCorrectionTurnCalculator().Evaluate(observation);
        return Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(new NasdaqPostCompletionCorrectionTurnReducer().Reduce(provisional)).State;
    }

    private static NasdaqPostInvalidationCandidateState EquivalentCandidate(bool bearish, Candle start, Candle terminal)
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
        impulse = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(24, 125, 190, 100, 131)).ResultingState;
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
    }
    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 130, 180, 100, 120, bearish);
        var started = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(started);
        var a = source.ConfirmingCandle;
        var b = ties ? Candle(24, 130, 140, 60, 131, bearish) : Candle(24, 125, 190, 100, 120, bearish);
        var selected = overlap ? new[] { a.OpenTimeUtc, b.OpenTimeUtc, turn.OpenTimeUtc } : new[] { a.OpenTimeUtc, b.OpenTimeUtc };
        var observations = new[]
        {
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected, Start.AddHours(40), "review:first"),
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected.Reverse().ToArray(), Start.AddHours(48), "review:second"),
        };
        var context = Context([a, b, turn, Candle(44, 135, 180, 100, 145, bearish)], observations);
        var evidence = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            new NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer().Reduce(pending, context));
        var members = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(
            new NasdaqPostCompletionExtremeMembershipPendingResolutionReducer().Reduce(evidence, context));
        return new NasdaqPostCompletionExtremeGeometryPreparer().Prepare(members);
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
