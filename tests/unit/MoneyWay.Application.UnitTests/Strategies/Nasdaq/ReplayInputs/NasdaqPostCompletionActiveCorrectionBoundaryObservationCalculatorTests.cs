using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator calculator = new();

    public static TheoryData<bool, decimal, decimal, int> BoundaryCases
    {
        get
        {
            var cases = new TheoryData<bool, decimal, decimal, int>();
            foreach (var bearish in new[] { false, true })
            {
                cases.Add(bearish, 130, 132, 1); // Continuation with trend body.
                cases.Add(bearish, 133, 132, 1); // Continuation with opposite body and opening gap.
                cases.Add(bearish, 132, 132, 1); // Doji beyond continuation.
                cases.Add(bearish, 120, 114, 2); // Invalidation with corrective body.
                cases.Add(bearish, 110, 114, 2); // Invalidation with opposite body and opening gap.
                cases.Add(bearish, 114, 114, 2); // Doji beyond invalidation.
                cases.Add(bearish, 120, 123, 0); // Both wicks cross, close stays inside.
                cases.Add(bearish, 125, 123, 0); // Opposite body inside.
                cases.Add(bearish, 123, 123, 0); // Exact doji inside.
                cases.Add(bearish, 120, 131, 0); // Equality with active extreme.
                cases.Add(bearish, 120, 115, 0); // Equality with protected turn.
                cases.Add(bearish, 150, 123, 0); // Open beyond continuation, close inside.
                cases.Add(bearish, 100, 123, 0); // Open beyond invalidation, close inside.
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void ObservesStrictCloseBoundariesIndependentlyOfBodyWicksAndOpenWithoutChangingState(
        bool bearish, decimal open, decimal close, int branch)
    {
        var state = Correction(bearish);
        var pair = state.ActivePair;
        var cursor = state.MarketCursor;
        var members = state.CorrectionTurnCandles;
        var incoming = Candle(32, open, 180, 90, close, bearish);
        Assert.Equal(bearish ? 69m : 131m, pair.ActiveExtremeGeometry.StructuralPrice);
        Assert.Equal(bearish ? 85m : 115m, pair.ProtectedTurnGeometry.StructuralPrice);
        var result = calculator.Evaluate(state, incoming);
        if (branch == 1)
            Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.ContinuationBreak>(result);
        else if (branch == 2)
            Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.ProtectedTurnInvalidated>(result);
        else
            Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(result);
        Assert.Same(state, result.SourceState);
        Assert.Same(incoming, result.IncomingCandle);
        Assert.Same(incoming, result.ActiveExtremeBreak.Candle);
        Assert.Same(incoming, result.ProtectedTurnBreak.Candle);
        Assert.Equal(pair.ActiveExtremeSide, result.ActiveExtremeSide);
        var continuationDirection = bearish ? StructuralBreakDirection.Lower : StructuralBreakDirection.Upper;
        var invalidationDirection = bearish ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower;
        var pure = new StructuralBodyCloseBreakCalculator();
        Assert.Equal(pure.Evaluate(incoming, pair.ActiveExtremeGeometry.StructuralPrice, continuationDirection), result.ActiveExtremeBreak);
        Assert.Equal(pure.Evaluate(incoming, pair.ProtectedTurnGeometry.StructuralPrice, invalidationDirection), result.ProtectedTurnBreak);
        Assert.Equal(branch == 1, result.ActiveExtremeBreak.IsConfirmed);
        Assert.Equal(branch == 2, result.ProtectedTurnBreak.IsConfirmed);
        Assert.Equal(new CandleBodyDirectionCalculator().Evaluate(incoming), result.BodyDirection);
        Assert.Equal(incoming.CloseTimeUtc, result.ActiveExtremeBreak.AsOfUtc);
        var repeated = calculator.Evaluate(state, incoming);
        Assert.Equal(result.GetType(), repeated.GetType());
        Assert.Equal(result.ActiveExtremeBreak, repeated.ActiveExtremeBreak);
        Assert.Equal(result.ProtectedTurnBreak, repeated.ProtectedTurnBreak);
        Assert.Equal(result.BodyDirection, repeated.BodyDirection);
        Assert.Same(cursor, state.MarketCursor);
        Assert.Same(pair, state.ActivePair);
        Assert.Same(members, state.CorrectionTurnCandles);
        Assert.Same(cursor, Assert.Single(state.CorrectionTurnCandles));
        Assert.DoesNotContain(incoming, state.CorrectionTurnCandles);
        Assert.Same(state.GeometryReady.PendingState.Episode, state.Episode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservesLaterCandleAcrossTimestampGapWithoutAdvancingCursor(bool bearish)
    {
        var state = Correction(bearish);
        var incoming = Candle(80, 120, 180, 90, 132, bearish);
        var result = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.ContinuationBreak>(calculator.Evaluate(state, incoming));
        Assert.Same(incoming, result.IncomingCandle);
        Assert.Same(state.CorrectionStartCandle, state.MarketCursor);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(28)]
    [InlineData(30)]
    public void RejectsEarlierSameTimestampAndOverlappingCandles(int hour) =>
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Correction(false), Candle(hour, 120, 180, 90, 123, false)));

    [Fact]
    public void RejectsExactAlreadyConsumedCursorCandle()
    {
        var state = Correction(false);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, state.MarketCursor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RejectsDifferentMarketIdentity(int mismatch)
    {
        var incoming = new Candle(mismatch == 0 ? new("other") : Provider, mismatch == 1 ? new("other") : Symbol,
            mismatch == 2 ? new(1, TimeframeUnit.Hour) : H4, Start.AddHours(32), Start.AddHours(36), 120, 180, 90, 123, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Correction(false), incoming));
    }

    [Fact]
    public void NarrowPureApiAndClosedImmutableUnionExcludeLifecycleOutputs()
    {
        var state = Correction(false);
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, state.MarketCursor));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(state, null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { typeof(NasdaqPostCompletionActiveCorrectionState), typeof(Candle) }, method.GetParameters().Select(p => p.ParameterType));
        var type = typeof(NasdaqPostCompletionActiveCorrectionBoundaryObservationResult);
        Assert.Equal(type, method.ReturnType);
        Assert.True(Assert.Single(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "ContinuationBreak", "InsideStructuralRange", "ProtectedTurnInvalidated" }, type.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var outcome in type.GetNestedTypes().Append(type))
        {
            Assert.Empty(outcome.GetConstructors());
            Assert.All(outcome.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.Equal(new[] { "ActiveExtremeBreak", "ActiveExtremeSide", "BodyDirection", "IncomingCandle", "ProtectedTurnBreak", "SourceState" },
                outcome.GetProperties().Select(p => p.Name).Order());
        }
        Assert.All(type.GetNestedTypes(), outcome => Assert.True(outcome.IsSealed));
    }

    private static NasdaqPostCompletionActiveCorrectionState Correction(bool bearish) =>
        new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));

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
