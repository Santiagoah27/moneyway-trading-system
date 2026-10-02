using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionExtremeGeometryPreparerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionExtremeGeometryPreparer preparer = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PreparesExistingGeometryWithAllOwnersAndProvenanceWhileLifecycleStaysFrozen(bool bearish, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 145, 180, 100, 135, bearish);
        var started = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(started);
        var a = source.ConfirmingCandle;
        var b = ties ? Candle(24, 130, 140, 60, 131, bearish) : Candle(24, 125, 190, 100, 120, bearish);
        var observations = new[]
        {
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, [b.OpenTimeUtc, a.OpenTimeUtc], Start.AddHours(40), "review:first"),
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, [a.OpenTimeUtc, b.OpenTimeUtc], Start.AddHours(48), "review:second"),
        };
        var context = Context([a, b, turn, Candle(44, 135, 180, 100, 145, bearish)], observations);
        var evidence = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            new NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer().Reduce(pending, context));
        var members = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(
            new NasdaqPostCompletionExtremeMembershipPendingResolutionReducer().Reduce(evidence, context));
        var ready = preparer.Prepare(members);
        var expected = new NasdaqHumanPostCompletionActiveExtremeGeometryCalculator().Evaluate(members.Resolution);
        Assert.Equal(expected.Geometry, ready.ExtremeGeometry.Geometry);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper, ready.ExtremeGeometry.Geometry.Side);
        Assert.Same(members, ready.MembersResolved);
        Assert.Same(members.Resolution, ready.ExtremeGeometry.MemberResolution);
        Assert.Same(pending, ready.PendingState);
        Assert.Same(pending.Episode, ready.PendingState.Episode);
        Assert.Same(source.Completion, ready.PendingState.Completion);
        Assert.Same(source.ValidatedProtectedTurn, ready.PendingState.ValidatedProtectedTurn);
        Assert.Same(source.ProtectedTurnGeometry, ready.PendingState.ProtectedTurnGeometry);
        Assert.Same(turn, ready.MarketCursor);
        Assert.Same(turn, ready.PendingState.CorrectionStartCandle);
        Assert.Same(pending.MembershipEvent, ready.ExtremeGeometry.MemberResolution.MembershipEvent);
        Assert.Same(evidence.Selection, ready.ExtremeGeometry.MemberResolution.Selection);
        Assert.Equal([a, b], ready.ExtremeGeometry.MemberResolution.SelectedMembers);
        Assert.DoesNotContain(turn, ready.ExtremeGeometry.MemberResolution.SelectedMembers);
        Assert.Equal(observations, ready.ExtremeGeometry.MemberResolution.Selection.SupportingObservations);
        Assert.Equal("review:second", ready.ExtremeGeometry.MemberResolution.Selection.SupportingObservations[1].SourceReference);
        Assert.Equal(Start.AddHours(48), ready.ExtremeGeometry.MemberResolution.Selection.SupportingObservations[1].ObservedAtUtc);
        Assert.Equal(a.Close, ready.ExtremeGeometry.Geometry.StructuralPrice);
        Assert.Equal(bearish ? b.Low : b.High, ready.ExtremeGeometry.Geometry.ProtectionAnchor);
        if (ties)
        {
            Assert.Equal(a.Close, b.Close);
            Assert.Equal(bearish ? a.Low : a.High, ready.ExtremeGeometry.Geometry.ProtectionAnchor);
            Assert.Same(a, ready.ExtremeGeometry.MemberResolution.SelectedMembers[0]);
            Assert.Same(b, ready.ExtremeGeometry.MemberResolution.SelectedMembers[1]);
        }
        else
            Assert.NotEqual(bearish ? a.Low : a.High, ready.ExtremeGeometry.Geometry.ProtectionAnchor);
        var repeated = preparer.Prepare(members);
        Assert.Equal(ready.ExtremeGeometry.Geometry, repeated.ExtremeGeometry.Geometry);
        Assert.Same(members, repeated.MembersResolved);
        Assert.Same(turn, pending.MarketCursor);
        Assert.Same(a, members.Resolution.SelectedMembers[0]);
        Assert.Same(b, members.Resolution.SelectedMembers[1]);
        Assert.Equal(observations, evidence.Selection.SupportingObservations);
    }

    [Fact]
    public void PureNarrowApiAndImmutableResultExcludeDownstreamLifecycleOutcomes()
    {
        Assert.Throws<ArgumentNullException>(() => preparer.Prepare(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionExtremeGeometryPreparer).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved), Assert.Single(method.GetParameters()).ParameterType);
        var type = typeof(NasdaqPostCompletionExtremeGeometryReady);
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "ExtremeGeometry", "MarketCursor", "MembersResolved", "PendingState" }, type.GetProperties().Select(property => property.Name).Order());
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
