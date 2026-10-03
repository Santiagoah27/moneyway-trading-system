using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuildPendingMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuildPendingMaterializer materializer = new();

    [Theory]
    [InlineData(false, 125)]
    [InlineData(true, 125)]
    [InlineData(false, 123)]
    [InlineData(true, 123)]
    public void PreservesCanonicalMigrationOnlyFactsAndSupersededSourceWithoutAdvancingAgain(bool bearish, decimal open)
    {
        var candidate = Candidate(bearish);
        var incoming = Candle(80, open, 180, 80, 123, bearish);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, incoming);
        var decision = Assert.IsType<NasdaqCandidateLifecycleDecision.RebuildPending>(result.Decision);
        var pair = candidate.ActivePair;
        var members = candidate.CorrectionTurnCandles.ToArray();
        var ready = candidate.SourceCorrection.GeometryReady;
        var evidence = ready.MembersResolved.EvidenceReady;

        var pending = materializer.Materialize(result);

        Assert.Same(result, pending.LifecycleResult);
        Assert.Same(decision, pending.Decision);
        Assert.Same(candidate, pending.SourceCandidate);
        Assert.Same(candidate.Provisional, pending.SourceCandidate.Provisional);
        Assert.Same(candidate.CandidateFacts, pending.SourceCandidate.CandidateFacts);
        Assert.Same(candidate.SourceCorrection, pending.SourceCandidate.SourceCorrection);
        Assert.Same(ready, pending.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Same(evidence, pending.SourceCandidate.SourceCorrection.GeometryReady.MembersResolved.EvidenceReady);
        Assert.Same(candidate.Episode, pending.Episode);
        Assert.Same(pair, pending.ActivePair);
        Assert.Same(pair.ProtectedTurn, pending.PreviousProtectedTurn);
        Assert.Same(pair.ActiveExtremeGeometry, pending.FrozenBreakoutTerminal);
        Assert.Same(decision.Migration, pending.Migration);
        Assert.True(pending.Migration.WasReplaced);
        Assert.Equal(candidate.CandidateGeometry.ProtectionAnchor, pending.Migration.PreviousExtreme);
        Assert.Equal(bearish ? 120m : 80m, pending.KnownProtectionAnchor);
        Assert.Equal(decision.EffectiveProtectionAnchor, pending.KnownProtectionAnchor);
        Assert.Equal(pending.Migration.ResultingExtreme, pending.KnownProtectionAnchor);
        Assert.NotEqual(candidate.CandidateGeometry.ProtectionAnchor, pending.KnownProtectionAnchor);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, pending.CandidateSide);
        Assert.Same(decision.Breakout, pending.BoundaryObservation);
        Assert.False(pending.BoundaryObservation.IsConfirmed);
        Assert.Equal(pending.FrozenBreakoutTerminal.StructuralPrice, pending.BoundaryObservation.ReferenceLevel);
        Assert.Equal(open == 123 ? CandleBodyDirection.Neutral : bearish ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish, pending.BodyDirection);
        Assert.Equal(decision.BodyDirection, pending.BodyDirection);
        Assert.Same(incoming, pending.MigrationCandle);
        Assert.Same(incoming, pending.MarketCursor);
        Assert.Same(result.MarketCursor, pending.MarketCursor);
        Assert.True(candidate.MarketCursor.OpenTimeUtc < pending.MarketCursor.OpenTimeUtc);
        Assert.Same(candidate.TerminalCandle, candidate.MarketCursor);
        Assert.Equal(members, candidate.CorrectionTurnCandles);
        Assert.DoesNotContain(incoming, candidate.CorrectionTurnCandles);
        Assert.Same(pair, candidate.ActivePair);
        Assert.Same(candidate.CandidateGeometry, decision.CandidateGeometry);
        var repeated = materializer.Materialize(result);
        Assert.NotSame(pending, repeated); // State follows existing reference-equality conventions.
        Assert.Same(result, repeated.LifecycleResult);
        Assert.Same(decision, repeated.Decision);
        Assert.Same(pair, repeated.ActivePair);
        Assert.Same(pending.MarketCursor, repeated.MarketCursor);
        Assert.Equal(pending.EvidenceContext, repeated.EvidenceContext);

        var legacy = EquivalentCandidate(bearish, candidate.CorrectionStartCandle, candidate.TerminalCandle);
        var old = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.RebuildPending>(
            new NasdaqPostInvalidationCandidateTransitionCalculator().Evaluate(legacy, incoming)).State;
        Assert.Equal(old.CandidateSide, pending.CandidateSide);
        Assert.Equal(old.KnownProtectionAnchor, pending.KnownProtectionAnchor);
        Assert.Equal(old.FrozenImpulseTerminal, pending.FrozenBreakoutTerminal);
        Assert.Same(old.MigrationCandle, pending.MigrationCandle);
        Assert.Same(old.LastProcessedCandle, pending.MarketCursor);
    }

    public static TheoryData<bool, decimal, decimal, decimal> OtherDecisions
    {
        get
        {
            var cases = new TheoryData<bool, decimal, decimal, decimal>();
            foreach (var bearish in new[] { false, true })
            {
                cases.Add(bearish, 120, 90, 123); // Equality at the wick does not migrate.
                cases.Add(bearish, 130, 90, 132); // Direct.
                cases.Add(bearish, 130, 80, 132); // 007.
                cases.Add(bearish, 140, 80, 132); // 008 opposite body.
                cases.Add(bearish, 132, 80, 132); // 008 doji.
                cases.Add(bearish, 120, 80, 123); // RebuiltTracking.
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(OtherDecisions))]
    public void RejectsEveryOtherCanonicalBranch(bool bearish, decimal open, decimal low, decimal close)
    {
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(Candidate(bearish), Candle(80, open, 180, low, close, bearish));
        Assert.IsNotType<NasdaqCandidateLifecycleDecision.RebuildPending>(result.Decision);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvidenceContextUsesCurrentEpisodeExactMigrationEventAndSide(bool bearish)
    {
        var candidate = Candidate(bearish);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var pending = materializer.Materialize(calculator.Evaluate(candidate, Candle(80, 125, 180, 80, 123, bearish)));
        var equivalent = materializer.Materialize(calculator.Evaluate(Candidate(bearish), Candle(80, 125, 180, 80, 123, bearish)));
        var later = materializer.Materialize(calculator.Evaluate(candidate, Candle(84, 125, 180, 80, 123, bearish)));
        var opposite = materializer.Materialize(calculator.Evaluate(Candidate(!bearish), Candle(80, 125, 180, 80, 123, !bearish)));
        Assert.Same(candidate.Episode, pending.EvidenceContext.Episode);
        Assert.Equal(pending.MigrationCandle.OpenTimeUtc, pending.EvidenceContext.MigrationCandleOpenTimeUtc);
        Assert.Equal(pending.CandidateSide, pending.EvidenceContext.CandidateSide);
        Assert.Equal(pending.EvidenceContext, equivalent.EvidenceContext);
        Assert.Equal(pending.EvidenceContext.GetHashCode(), equivalent.EvidenceContext.GetHashCode());
        Assert.NotEqual(pending.EvidenceContext, later.EvidenceContext);
        Assert.NotEqual(pending.EvidenceContext, opposite.EvidenceContext);
    }

    [Fact]
    public void RejectsDetachedCanonicalDecisionWithoutRecomputingMigration()
    {
        var candidate = Candidate(false);
        var result = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, Candle(80, 125, 180, 80, 123, false));
        var ctor = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionCandidateLifecycleResult)ctor.Invoke([Candidate(false), result.Decision]);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(detached));
    }

    [Fact]
    public void PureImmutableBoundaryDoesNotExposeDefinitiveMembersGeometryTrackingOrCompletion()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize(null!));
        var type = typeof(NasdaqPostCompletionRebuildPendingMaterializer);
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionCandidateLifecycleResult), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionRebuildPendingState), method.ReturnType);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        Assert.Empty(Assert.Single(type.GetConstructors()).GetParameters());
        var state = typeof(NasdaqPostCompletionRebuildPendingState);
        Assert.Empty(state.GetConstructors());
        Assert.All(state.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "ActivePair", "BodyDirection", "BoundaryObservation", "CandidateSide", "Decision", "Episode", "EvidenceContext", "FrozenBreakoutTerminal", "KnownProtectionAnchor", "LifecycleResult", "MarketCursor", "Migration", "MigrationCandle", "PreviousProtectedTurn", "SourceCandidate" }, state.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(state.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
        var context = typeof(NasdaqPostCompletionRebuildContext);
        Assert.Empty(context.GetConstructors());
        Assert.All(context.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "CandidateSide", "Episode", "MigrationCandleOpenTimeUtc" }, context.GetProperties().Select(p => p.Name).Order());
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
