using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuiltCandidateGeometryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuiltCandidateGeometryCalculator calculator = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MatchesOldCanonicalGeometryAndPreservesExactMembersAndFullHumanLineage(bool bearish, bool multiple)
    {
        var resolved = Resolved(bearish, multiple);
        var pending = resolved.PendingState;
        var selection = resolved.Selection;
        var members = resolved.SelectedMembers.ToArray();
        var result = calculator.Evaluate(resolved);

        Assert.Equal(OldGeometry(resolved), result.CandidateGeometry);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, result.CandidateSide);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower, result.CandidateGeometry.Side);
        Assert.Equal(pending.KnownProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.NotEqual(pending.SourceCandidate.CandidateGeometry.ProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.Same(resolved, result.MembersResolved);
        Assert.Same(resolved.SelectedMembers, result.MembersResolved.SelectedMembers);
        Assert.Equal(members, result.MembersResolved.SelectedMembers);
        Assert.Same(selection, result.MembersResolved.Selection);
        Assert.Same(selection.SupportingObservations, result.MembersResolved.Selection.SupportingObservations);
        Assert.Equal(new[] { "review:first", "review:second" }, result.MembersResolved.Selection.SupportingObservations.Select(o => o.SourceReference));
        Assert.All(result.MembersResolved.Selection.SupportingObservations, o => Assert.Equal(Start.AddHours(92), o.ObservedAtUtc));
        Assert.Equal(selection.SemanticMemberOpenTimesUtc, result.MembersResolved.SelectedMembers.Select(c => c.OpenTimeUtc));
        Assert.Same(pending, result.MembersResolved.PendingState);
        Assert.Same(pending.EvidenceContext, result.MembersResolved.Context);
        Assert.Same(pending.SourceCandidate, result.MembersResolved.PendingState.SourceCandidate);
        Assert.Same(pending.Decision, result.MembersResolved.PendingState.Decision);
        Assert.Same(pending.Migration, result.MembersResolved.PendingState.Migration);
        Assert.Same(pending.ActivePair, result.MembersResolved.PendingState.ActivePair);
        Assert.Same(pending.PreviousProtectedTurn, result.MembersResolved.PendingState.PreviousProtectedTurn);
        Assert.Same(pending.Episode, result.MembersResolved.PendingState.Episode);
        Assert.Same(pending.MigrationCandle, result.MarketCursor);
        Assert.Same(resolved.MarketCursor, result.MarketCursor);
        if (multiple)
        {
            Assert.NotSame(result.MembersResolved.SelectedMembers[^1], result.MarketCursor);
            var migrationOnly = OldGeometry(Resolved(bearish, false));
            Assert.NotEqual(migrationOnly.StructuralPrice, result.CandidateGeometry.StructuralPrice);
            Assert.Equal(migrationOnly.ProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        }
        var repeated = calculator.Evaluate(resolved);
        Assert.Equal(result.CandidateGeometry, repeated.CandidateGeometry);
        Assert.Same(resolved, repeated.MembersResolved);
        Assert.Equal(members, resolved.SelectedMembers);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Candle>)result.MembersResolved.SelectedMembers).Clear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalHumanMemberEnumerationOrderCannotChangeGeometry(bool bearish)
    {
        var first = Resolved(bearish, true, false);
        var reversed = Resolved(bearish, true, true);
        Assert.Equal(first.Selection.SemanticMemberOpenTimesUtc, reversed.Selection.SemanticMemberOpenTimesUtc);
        Assert.Equal(first.SelectedMembers.Select(c => (c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close)),
            reversed.SelectedMembers.Select(c => (c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close)));
        Assert.Equal(calculator.Evaluate(first).CandidateGeometry, calculator.Evaluate(reversed).CandidateGeometry);
    }

    [Fact]
    public void TypedGeometryOnlyApiHasNoMarketContextIndependentSideOrDownstreamState()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionRebuiltCandidateGeometryCalculator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionRebuiltCandidateGeometry), method.ReturnType);
        var result = typeof(NasdaqPostCompletionRebuiltCandidateGeometry);
        Assert.Empty(result.GetConstructors());
        Assert.All(result.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "CandidateGeometry", "CandidateSide", "MarketCursor", "MembersResolved" }, result.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(result.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
    }

    [Fact]
    public void SharedPrimitiveKeepsExistingKnownAnchorGuardAndRejectsUnsupportedSide()
    {
        var resolved = Resolved(false, true);
        var primitive = new NasdaqRebuiltCandidateGeometryCalculator();
        Assert.Throws<ArgumentNullException>(() => primitive.Evaluate(null!, StructuralCandidateExtremeSide.Lower, 80m));
        Assert.Throws<ArgumentOutOfRangeException>(() => primitive.Evaluate(resolved.SelectedMembers, (StructuralCandidateExtremeSide)99, 80m));
        Assert.Throws<InvalidOperationException>(() => primitive.Evaluate(resolved.SelectedMembers, StructuralCandidateExtremeSide.Lower, 79m));
    }

    private static NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved Resolved(bool bearish, bool multiple, bool reverse = false)
    {
        var pending = Pending(bearish);
        var hours = multiple ? new[] { 80, 88 } : new[] { 80 };
        if (reverse) Array.Reverse(hours);
        var first = Observation(pending, hours, 92, "review:first");
        var second = Observation(pending, hours.Reverse().ToArray(), 92, "review:second");
        var selection = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(
            new NasdaqHumanPostCompletionRebuiltCandidateObservationSelector().Select(pending, At(92, second, first)));
        var members = multiple ? new[] { pending.MigrationCandle, Candle(88, 100, 180, 85, 110, bearish) } : new[] { pending.MigrationCandle };
        return Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(
            new NasdaqPostCompletionRebuiltCandidateMemberResolver().Evaluate(selection, Snapshot(members, 92, first, second)));
    }
    private static StructuralTurnGeometryResult OldGeometry(NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved members)
    {
        var pending = members.PendingState;
        var parent = pending.Episode.PreviousCompletedEpisode;
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(parent.StrategyId, parent.StrategyVersion, parent.ProviderId, parent.Symbol,
            parent.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide);
        var constructor = Assert.Single(typeof(NasdaqHumanRebuiltCandidateVertexMemberResolution).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var old = (NasdaqHumanRebuiltCandidateVertexMemberResolution)constructor.Invoke([episode, pending.MigrationCandle, members.SelectedMembers, pending.KnownProtectionAnchor]);
        return new NasdaqHumanRebuiltCandidateVertexGeometryCalculator().Evaluate(old);
    }

    private static StrategyReplayContext Snapshot(Candle[] members, int asOfHour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var trigger = new Candle(Provider, Symbol, minute, Start.AddHours(asOfHour).AddMinutes(-1), Start.AddHours(asOfHour), 100, 101, 99, 100, null);
        var series = new List<CandleSeries> { new(Provider, Symbol, minute, [trigger]) };
        if (members.Length > 0) series.Add(new(Provider, Symbol, H4, members));
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        throw new InvalidOperationException("Test AsOf frame not found.");
    }

    private static NasdaqPostCompletionRebuildPendingState Pending(bool bearish, int hour = 80, int completionHour = 20) =>
        new NasdaqPostCompletionRebuildPendingMaterializer().Materialize(new NasdaqPostCompletionCandidateLifecycleCalculator()
            .Evaluate(Candidate(bearish, completionHour), Candle(hour, 125, 180, 80, 123, bearish)));
    private static NasdaqHumanPostCompletionRebuiltCandidateObservation Observation(NasdaqPostCompletionRebuildPendingState pending, int[] members, int hour, string source) =>
        new(pending, members.Select(h => Start.AddHours(h)), Start.AddHours(hour), source);

    private static StrategyReplayContext At(int hour, params IStrategyReplayInputObservation[] observations) =>
        Context([Candle(hour - 4, 120, 180, 90, 123, false)], observations);

    private static NasdaqPostCompletionCandidateState Candidate(bool bearish, int completionHour = 20)
    {
        var correction = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false, completionHour));
        var terminal = Candle(completionHour + 12, 120, 180, 90, 125, bearish);
        var observation = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
            new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(correction, terminal));
        var provisional = new NasdaqPostCompletionCorrectionTurnCalculator().Evaluate(observation);
        return Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(new NasdaqPostCompletionCorrectionTurnReducer().Reduce(provisional)).State;
    }

    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties, int completionHour = 20)
    {
        var source = State(bearish, completionHour);
        var turn = Candle(completionHour + 8, 130, 180, 100, 120, bearish);
        var started = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(started);
        var a = source.ConfirmingCandle;
        var b = ties ? Candle(completionHour + 4, 130, 140, 60, 131, bearish) : Candle(completionHour + 4, 125, 190, 100, 120, bearish);
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
    private static NasdaqPostCompletionActiveImpulseState State(bool bearish, int completionHour = 20)
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
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, C(completionHour, 130, 140, 60, 131)));
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
