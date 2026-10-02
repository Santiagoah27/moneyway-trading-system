using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionCorrectionTurnReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionCorrectionTurnReducer reducer = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MaterializesContinuationAndDojiWithExactConsumedFactsWithoutAppendingAgain(bool bearish, bool doji)
    {
        var source = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var incoming = Candle(44, doji ? 123 : 125, 180, 90, doji ? 123 : 120, bearish);
        var turn = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection>(Turn(source, incoming));
        var state = Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.ActiveCorrection>(reducer.Reduce(turn)).State;
        Assert.NotSame(source, state);
        Assert.Same(turn, state.CorrectionProgression);
        Assert.Same(turn.TurnResult.CorrectionTurnCandles, state.CorrectionTurnCandles);
        Assert.Equal(2, state.CorrectionTurnCandles.Count);
        Assert.Same(source.CorrectionStartCandle, state.CorrectionStartCandle);
        Assert.Same(source.CorrectionStartCandle, state.CorrectionTurnCandles[0]);
        Assert.Same(incoming, state.CorrectionTurnCandles[1]);
        Assert.Single(state.CorrectionTurnCandles, member => ReferenceEquals(member, incoming));
        Assert.Same(turn.MarketCursor, state.MarketCursor);
        Assert.Same(incoming, state.MarketCursor);
        Assert.Same(source.ActivePair, state.ActivePair);
        Assert.Same(source.Episode, state.Episode);
        Assert.Same(source.GeometryReady, state.GeometryReady);
        Assert.Same(source.Completion, state.Completion);
        Assert.Same(turn.TurnResult.Geometry, state.CorrectionProgression!.TurnResult.Geometry);
        Assert.Null(source.CorrectionProgression);
        Assert.Single(source.CorrectionTurnCandles);
        Assert.Same(source.CorrectionStartCandle, source.MarketCursor);
        var repeated = Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.ActiveCorrection>(reducer.Reduce(turn)).State;
        Assert.Same(state.ActivePair, repeated.ActivePair);
        Assert.Same(state.Episode, repeated.Episode);
        Assert.Same(state.CorrectionProgression, repeated.CorrectionProgression);
        Assert.Same(state.CorrectionTurnCandles, repeated.CorrectionTurnCandles);
        Assert.Same(state.MarketCursor, repeated.MarketCursor);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)state.CorrectionTurnCandles).Add(incoming));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(state, incoming));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializesProvisionalCandidateAfterProgressionWithReferencePairAndCompleteHistoryUnchanged(bool bearish)
    {
        var seed = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var first = Candle(32, 125, 180, 90, 120, bearish);
        var continuedTurn = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection>(Turn(seed, first));
        var source = Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.ActiveCorrection>(reducer.Reduce(continuedTurn)).State;
        var terminal = Candle(48, 120, 190, 80, 125, bearish);
        var turn = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional>(Turn(source, terminal));
        var state = Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(reducer.Reduce(turn)).State;
        Assert.Same(turn, state.Provisional);
        Assert.Same(turn.TurnResult, state.CandidateFacts);
        Assert.Same(turn.TurnResult.Geometry, state.CandidateGeometry);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, state.CandidateSide);
        Assert.Same(source, state.SourceCorrection);
        Assert.Same(seed.Episode, state.Episode);
        Assert.Same(seed.ActivePair, state.ActivePair);
        Assert.Same(seed.ActivePair.ActiveExtreme, state.ActivePair.ActiveExtreme);
        Assert.Same(seed.ActivePair.ProtectedTurn, state.ActivePair.ProtectedTurn);
        Assert.NotSame(state.ActivePair.ProtectedTurnGeometry, state.CandidateGeometry);
        Assert.Same(turn.TurnResult.CorrectionTurnCandles, state.CorrectionTurnCandles);
        Assert.Equal(new[] { seed.CorrectionStartCandle, first }, state.CorrectionTurnCandles);
        Assert.Same(seed.CorrectionStartCandle, state.CorrectionStartCandle);
        Assert.DoesNotContain(terminal, state.CorrectionTurnCandles);
        Assert.Same(terminal, state.TerminalCandle);
        Assert.Same(terminal, state.MarketCursor);
        Assert.Same(turn.MarketCursor, state.MarketCursor);
        Assert.Same(continuedTurn, state.SourceCorrection.CorrectionProgression);
        Assert.Same(seed.GeometryReady, state.SourceCorrection.GeometryReady);
        Assert.Same(seed.Completion, state.SourceCorrection.Completion);
        Assert.Same(seed.GeometryReady.ExtremeGeometry.MemberResolution.Selection,
            state.SourceCorrection.GeometryReady.ExtremeGeometry.MemberResolution.Selection);
        Assert.Same(first, source.MarketCursor);
        Assert.Equal(2, source.CorrectionTurnCandles.Count);
        Assert.Single(seed.CorrectionTurnCandles);
        var repeated = Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(reducer.Reduce(turn)).State;
        Assert.Same(state.Provisional, repeated.Provisional);
        Assert.Same(state.CandidateFacts, repeated.CandidateFacts);
        Assert.Same(state.ActivePair, repeated.ActivePair);
        Assert.Same(state.Episode, repeated.Episode);
        Assert.Same(state.MarketCursor, repeated.MarketCursor);
    }

    [Fact]
    public void RejectsInconsistentCanonicalSideCursorAndMembers()
    {
        var source = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(false, false, false));
        var incoming = Candle(32, 125, 180, 90, 120, false);
        var valid = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection>(Turn(source, incoming));
        var coreConstructor = Assert.Single(typeof(CorrectionCandidateTurnResult.ContinuingCorrection).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        var wrapperConstructor = Assert.Single(typeof(NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        foreach (var mismatch in new[] { 0, 1, 2 })
        {
            var facts = (CorrectionCandidateTurnResult.ContinuingCorrection)coreConstructor.Invoke([
                mismatch == 2 ? source.CorrectionTurnCandles : valid.TurnResult.CorrectionTurnCandles,
                valid.TurnResult.Geometry,
                mismatch == 0 ? StructuralCandidateExtremeSide.Upper : valid.TurnResult.CandidateSide,
                mismatch == 1 ? source.MarketCursor : incoming, valid.TurnResult.BodyDirection]);
            var result = (NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection)wrapperConstructor.Invoke([valid.Observation, facts]);
            Assert.Throws<ArgumentException>(() => reducer.Reduce(result));
        }
    }

    [Fact]
    public void ReducerAcceptsOnlyConsumedResultsAndStatesExposeNoNextCandleLifecycle()
    {
        Assert.Throws<ArgumentNullException>(() => reducer.Reduce(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionCorrectionTurnReducer).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionCorrectionTurnResult), Assert.Single(method.GetParameters()).ParameterType);
        var union = typeof(NasdaqPostCompletionCorrectionTurnReductionResult);
        Assert.Equal(union, method.ReturnType);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)).IsPrivate);
        Assert.Equal(new[] { "ActiveCorrection", "Candidate" }, union.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.Null(Assert.Single(branch.GetProperties()).SetMethod);
        }
        var candidate = typeof(NasdaqPostCompletionCandidateState);
        Assert.Empty(candidate.GetConstructors());
        Assert.All(candidate.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "ActivePair", "CandidateFacts", "CandidateGeometry", "CandidateSide", "Continuation", "CorrectionStartCandle", "CorrectionTurnCandles", "Episode", "MarketCursor", "Provisional", "SourceCorrection", "TerminalCandle" },
            candidate.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(candidate.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
    }

    private static NasdaqPostCompletionCorrectionTurnResult Turn(NasdaqPostCompletionActiveCorrectionState source, Candle candle) =>
        new NasdaqPostCompletionCorrectionTurnCalculator().Evaluate(
            Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
                new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(source, candle)));

    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 145, 180, 100, 135, bearish);
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
