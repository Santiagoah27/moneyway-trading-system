using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionCorrectionTurnCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionCorrectionTurnCalculator calculator = new();

    [Theory]
    [InlineData(false, 125, 120, false)]
    [InlineData(true, 125, 120, false)]
    [InlineData(false, 120, 125, true)]
    [InlineData(true, 120, 125, true)]
    [InlineData(false, 123, 123, false)]
    [InlineData(true, 123, 123, false)]
    public void MatchesCanonicalReconstructionCalculationAndConsumesOnlyTheObservedCandle(
        bool bearish, decimal open, decimal close, bool formsCandidate)
    {
        var source = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var incoming = Candle(44, open, 180, 90, close, bearish);
        var observation = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
            new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(source, incoming));
        var oldCorrection = OldCorrection(bearish, source.CorrectionStartCandle);
        Assert.Equal(source.CorrectionTurnCandles, oldCorrection.CorrectionTurnCandles);
        var canonical = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(oldCorrection, incoming);
        var result = calculator.Evaluate(observation);
        CorrectionCandidateTurnResult turn;
        if (formsCandidate)
        {
            turn = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional>(result).TurnResult;
            Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, canonical.Kind);
            Assert.Equal(canonical.Candidate!.CandidateGeometry, turn.Geometry);
            Assert.Equal(canonical.Candidate.CorrectionTurnCandles, turn.CorrectionTurnCandles);
            Assert.Same(incoming, Assert.IsType<CorrectionCandidateTurnResult.CandidateProvisional>(turn).TerminalCandle);
            Assert.DoesNotContain(incoming, turn.CorrectionTurnCandles);
            Assert.Equal(oldCorrection.CorrectionGeometry.StructuralPrice, turn.Geometry.StructuralPrice);
            Assert.Equal(bearish ? 110m : 90m, turn.Geometry.ProtectionAnchor);
        }
        else
        {
            turn = Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection>(result).TurnResult;
            Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.ContinuingCorrection, canonical.Kind);
            Assert.Equal(canonical.ContinuingCorrection!.CorrectionGeometry, turn.Geometry);
            Assert.Equal(canonical.ContinuingCorrection.CorrectionTurnCandles, turn.CorrectionTurnCandles);
            Assert.Equal(2, turn.CorrectionTurnCandles.Count);
            Assert.Same(incoming, turn.CorrectionTurnCandles[1]);
            Assert.Single(turn.CorrectionTurnCandles, c => ReferenceEquals(c, incoming));
        }
        Assert.Same(source.CorrectionStartCandle, turn.CorrectionTurnCandles[0]);
        Assert.Same(incoming, turn.MarketCursor);
        Assert.Same(incoming, result.MarketCursor);
        Assert.Same(observation, result.Observation);
        Assert.Equal(observation.BodyDirection, turn.BodyDirection);
        Assert.Same(source, result.SourceState);
        Assert.Same(source.Episode, result.Episode);
        Assert.Same(source.ActivePair, result.ActivePair);
        Assert.Same(source.ActivePair.ActiveExtreme, result.ActivePair.ActiveExtreme);
        Assert.Same(source.ActivePair.ProtectedTurn, result.ActivePair.ProtectedTurn);
        Assert.Same(source.GeometryReady, result.SourceState.GeometryReady);
        Assert.Same(source.Completion, result.SourceState.Completion);
        Assert.Same(source.GeometryReady.MembersResolved, result.SourceState.GeometryReady.MembersResolved);
        Assert.Single(source.CorrectionTurnCandles);
        Assert.Same(source.CorrectionStartCandle, source.MarketCursor);
        Assert.Same(source, observation.SourceState);
        Assert.Same(incoming, observation.IncomingCandle);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)turn.CorrectionTurnCandles).Add(incoming));
        var repeated = calculator.Evaluate(observation);
        Assert.Equal(result.GetType(), repeated.GetType());
        var repeatedTurn = repeated is NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection continuing
            ? (CorrectionCandidateTurnResult)continuing.TurnResult
            : Assert.IsType<NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional>(repeated).TurnResult;
        Assert.Equal(turn.Geometry, repeatedTurn.Geometry);
        Assert.Equal(turn.CorrectionTurnCandles, repeatedTurn.CorrectionTurnCandles);
        Assert.Same(incoming, repeatedTurn.MarketCursor);
        Assert.Equal(bearish ? 200 - open : open, incoming.Open);
        Assert.Equal(bearish ? 200 - close : close, incoming.Close);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtractedCorePreservesMultipleMembersAndRejectsReconsumption(bool bearish)
    {
        var correction = OldCorrection(bearish);
        var core = new CorrectionCandidateTurnCalculator();
        var first = Candle(32, 125, 180, 90, 120, bearish);
        var continued = Assert.IsType<CorrectionCandidateTurnResult.ContinuingCorrection>(core.Evaluate(
            correction.CorrectionTurnCandles, correction.CorrectionGeometry, correction.CandidateSide,
            first, new CandleBodyDirectionCalculator().Evaluate(first)));
        var terminal = Candle(48, 120, 190, 80, 125, bearish);
        var provisional = Assert.IsType<CorrectionCandidateTurnResult.CandidateProvisional>(core.Evaluate(
            continued.CorrectionTurnCandles, continued.Geometry, continued.CandidateSide,
            terminal, new CandleBodyDirectionCalculator().Evaluate(terminal)));
        var oldContinued = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, first).ContinuingCorrection!;
        var oldCandidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(oldContinued, terminal).Candidate!;
        Assert.Equal(oldCandidate.CandidateGeometry, provisional.Geometry);
        Assert.Equal(oldCandidate.CorrectionTurnCandles, provisional.CorrectionTurnCandles);
        Assert.Equal(2, provisional.CorrectionTurnCandles.Count);
        Assert.DoesNotContain(terminal, provisional.CorrectionTurnCandles);
        Assert.Equal(continued.Geometry.StructuralPrice, provisional.Geometry.StructuralPrice);
        Assert.Throws<ArgumentException>(() => core.Evaluate(continued.CorrectionTurnCandles,
            continued.Geometry, continued.CandidateSide, first, continued.BodyDirection));
    }

    [Fact]
    public void ExtractedCoreRejectsInvalidCorrectionInputsWithoutProducingFacts()
    {
        var state = OldCorrection(false);
        var core = new CorrectionCandidateTurnCalculator();
        var candle = Candle(32, 125, 180, 90, 120, false);
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(null!, state.CorrectionGeometry, state.CandidateSide, candle, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(state.CorrectionTurnCandles, null!, state.CandidateSide, candle, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(state.CorrectionTurnCandles, state.CorrectionGeometry, state.CandidateSide, null!, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentException>(() => core.Evaluate([], state.CorrectionGeometry, state.CandidateSide, candle, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentException>(() => core.Evaluate([null!], state.CorrectionGeometry, state.CandidateSide, candle, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.Evaluate(state.CorrectionTurnCandles, state.CorrectionGeometry, (StructuralCandidateExtremeSide)999, candle, CandleBodyDirection.Bearish));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.Evaluate(state.CorrectionTurnCandles, state.CorrectionGeometry, state.CandidateSide, candle, (CandleBodyDirection)999));
        Assert.Throws<ArgumentException>(() => core.Evaluate(state.CorrectionTurnCandles, state.CorrectionGeometry, StructuralCandidateExtremeSide.Upper, candle, CandleBodyDirection.Bearish));
        var wrongSeries = new Candle(Provider, new("other"), H4, candle.OpenTimeUtc, candle.CloseTimeUtc, candle.Open, candle.High, candle.Low, candle.Close, null);
        Assert.Throws<ArgumentException>(() => core.Evaluate(state.CorrectionTurnCandles, state.CorrectionGeometry, state.CandidateSide, wrongSeries, CandleBodyDirection.Bearish));
    }

    [Fact]
    public void IntegrationAcceptsOnlyInsideObservationAndReturnsClosedImmutableFactsWithoutCandidateState()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionCorrectionTurnCalculator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionCorrectionTurnResult), method.ReturnType);
        foreach (var type in new[] { typeof(NasdaqPostCompletionCorrectionTurnResult), typeof(CorrectionCandidateTurnResult) })
        {
            Assert.True(Assert.Single(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
            Assert.Equal(new[] { "CandidateProvisional", "ContinuingCorrection" }, type.GetNestedTypes().Select(t => t.Name).Order());
            foreach (var branch in type.GetNestedTypes())
            {
                Assert.True(branch.IsSealed);
                Assert.Empty(branch.GetConstructors());
                Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
                Assert.DoesNotContain(branch.GetProperties(), p => p.PropertyType.Name.Contains("CandidateState") || p.Name == "ResultingState");
            }
        }
    }

    private static NasdaqPostInvalidationCorrectionState OldCorrection(bool bearish, Candle? correctionStart = null)
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
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, correctionStart ?? C(28, 145, 180, 100, 135)));
    }
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
