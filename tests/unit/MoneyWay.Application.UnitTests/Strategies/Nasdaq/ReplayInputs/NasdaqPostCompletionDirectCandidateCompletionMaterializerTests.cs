using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionDirectCandidateCompletionMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionDirectCandidateCompletionMaterializer materializer = new();

    [Theory]
    [InlineData(false, 130)]
    [InlineData(true, 130)]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void MaterializesExactValidatedTurnAndConfirmationWithLegacyEquivalentStructuralFacts(bool bearish, decimal open)
    {
        var source = Candidate(bearish);
        var legacy = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        var confirming = Candle(80, open, 180, 90, 132, bearish);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, confirming);
        var decision = Assert.IsType<NasdaqCandidateLifecycleDecision.DirectCompleted>(result.Decision);
        var completion = materializer.Materialize(result);
        var provenance = Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.Direct>(completion.Source);
        Assert.Same(result, provenance.LifecycleResult);
        Assert.Same(decision, provenance.Decision);
        Assert.Same(decision.Validation, provenance.ValidatedTurn);
        var old = Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(legacy), confirming));
        Assert.Equal(old.ValidatedCandidate, completion.ValidatedTurn);
        Assert.Same(old.MarketCursor, completion.MarketCursor);
        Assert.Same(decision.Validation, completion.ValidatedTurn);
        Assert.True(completion.ValidatedTurn.IsValidated);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, completion.ValidatedTurn.CandidateSide);
        Assert.Same(source.CandidateGeometry, completion.ValidatedTurn.CandidateGeometry);
        Assert.Same(decision.Breakout, completion.ValidatedTurn.BreakObservation);
        Assert.Same(source.ActivePair.ActiveExtremeGeometry, completion.FrozenBreakoutTerminal);
        Assert.Equal(source.ActivePair.ActiveExtremeGeometry.StructuralPrice, completion.ValidatedTurn.BreakObservation.ReferenceLevel);
        Assert.Same(source.ActivePair.ProtectedTurn, completion.PreviousProtectedTurn);
        Assert.NotSame(completion.PreviousProtectedTurn, completion.ValidatedTurn);
        Assert.Same(source.Episode, completion.Episode);
        Assert.Same(result, completion.LifecycleResult);
        Assert.Same(source, completion.SourceCandidate);
        Assert.Same(source.ActivePair, completion.SourceCandidate.ActivePair);
        Assert.Same(source.Provisional, completion.SourceCandidate.Provisional);
        Assert.Same(source.CorrectionTurnCandles, completion.SourceCandidate.CorrectionTurnCandles);
        Assert.Same(source.SourceCorrection.GeometryReady, completion.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Same(source.SourceCorrection.Completion, completion.SourceCandidate.SourceCorrection.Completion);
        Assert.Same(confirming, completion.ConfirmingCandle);
        Assert.Same(confirming, completion.MarketCursor);
        Assert.Equal(decision.BodyDirection, completion.BodyDirection);
        Assert.DoesNotContain(confirming, source.CorrectionTurnCandles);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        var repeated = materializer.Materialize(result);
        Assert.Same(completion.LifecycleResult, repeated.LifecycleResult);
        Assert.Same(completion.ValidatedTurn, repeated.ValidatedTurn);
        Assert.Same(completion.Episode, repeated.Episode);
        Assert.Same(completion.ConfirmingCandle, repeated.ConfirmingCandle);
        Assert.Same(completion.FrozenBreakoutTerminal, repeated.FrozenBreakoutTerminal);
        Assert.Equal(completion.BodyDirection, repeated.BodyDirection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletionRetainsEarlierCandidateContinuationAndDoesNotCreateASuccessorEpisode(bool bearish)
    {
        var original = Candidate(bearish);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var continuation = calculator.Evaluate(original, Candle(48, 120, 180, 90, 123, bearish));
        var continued = new NasdaqPostCompletionCandidateContinuationReducer().Reduce(continuation);
        var direct = calculator.Evaluate(continued, Candle(80, 130, 180, 90, 132, bearish));
        var completion = materializer.Materialize(direct);
        Assert.Same(continued, completion.SourceCandidate);
        Assert.Same(continuation, completion.SourceCandidate.Continuation);
        Assert.Same(original, completion.SourceCandidate.Continuation!.SourceState);
        Assert.Same(original.Episode, completion.Episode);
        Assert.Same(original.CandidateGeometry, completion.ValidatedTurn.CandidateGeometry);
        Assert.Same(direct.MarketCursor, completion.MarketCursor);
        Assert.Same(continuation.MarketCursor, continued.MarketCursor);
    }

    [Theory]
    [InlineData(120, 90, 123)]
    [InlineData(130, 80, 132)]
    [InlineData(140, 80, 132)]
    [InlineData(125, 80, 123)]
    [InlineData(120, 80, 123)]
    public void RejectsOtherBranchesWithoutCompletingThem(decimal open, decimal low, decimal close)
    {
        var source = Candidate(false);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, Candle(48, open, 180, low, close, false));
        Assert.IsNotType<NasdaqCandidateLifecycleDecision.DirectCompleted>(result.Decision);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(result));
        Assert.Same(source.TerminalCandle, source.MarketCursor);
    }

    [Fact]
    public void NarrowImmutableCompletionShapeExcludesSuccessorExtremePairEpisodeAndHandoff()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize(null!));
        var source = Candidate(false);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, Candle(48, 130, 180, 90, 132, false));
        var constructor = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionCandidateLifecycleResult)constructor.Invoke([Candidate(false), result.Decision]);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(detached));
        var method = Assert.Single(typeof(NasdaqPostCompletionDirectCandidateCompletionMaterializer).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionCandidateLifecycleResult), Assert.Single(method.GetParameters()).ParameterType);
        var type = typeof(NasdaqPostCompletionStructuralCompletion);
        Assert.Equal(type, method.ReturnType);
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "BodyDirection", "ConfirmingCandle", "Episode", "FrozenBreakoutTerminal", "LifecycleResult", "MarketCursor", "PreviousProtectedTurn", "Source", "SourceCandidate", "ValidatedTurn" },
            type.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
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
