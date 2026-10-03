using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuiltCandidateMemberResolverTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuiltCandidateMemberResolver resolver = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvesExactHumanMembersChronologicallyWithoutChangingCursorOrLineage(bool bearish)
    {
        var pending = Pending(bearish);
        var first = Observation(pending, [88, 80], 92, "review:first");
        var second = Observation(pending, [80, 88], 92, "review:second");
        var selection = Unique(pending, first, second);
        var adjacent = Candle(88, 120, 180, 85, 123, bearish);
        var unselected = Candle(84, 120, 180, 1, 123, bearish);
        var context = Snapshot([pending.MigrationCandle, unselected, adjacent], 92, first, second);
        var result = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(resolver.Evaluate(selection, context));
        Assert.Equal(new[] { pending.MigrationCandle, adjacent }, result.SelectedMembers);
        Assert.Same(adjacent, result.SelectedMembers[1]);
        Assert.DoesNotContain(unselected, result.SelectedMembers);
        Assert.Same(selection, result.Selection);
        Assert.Same(first, result.Selection.SupportingObservations[0]);
        Assert.Same(second, result.Selection.SupportingObservations[1]);
        Assert.Equal(new[] { "review:first", "review:second" }, result.Selection.SupportingObservations.Select(o => o.SourceReference));
        Assert.All(result.Selection.SupportingObservations, o => Assert.Equal(Start.AddHours(92), o.ObservedAtUtc));
        Assert.Equal(new[] { Start.AddHours(80), Start.AddHours(88) }, result.Selection.SemanticMemberOpenTimesUtc);
        AssertFrozen(selection, result);
        var repeated = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(resolver.Evaluate(selection, context));
        Assert.Equal(result.SelectedMembers, repeated.SelectedMembers);
        Assert.Equal(new[] { first, second }, context.InputObservations);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Candle>)result.SelectedMembers).Clear());
        var old = OldResolve(pending, selection, context);
        Assert.Equal(old.SelectedMembers, result.SelectedMembers);
        Assert.Same(old.MigrationCandle, result.SelectedMembers[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingHistoricalMemberRecoversWithSameSelectionSameEventAndSameAsOf(bool bearish)
    {
        var pending = Pending(bearish);
        var observation = Observation(pending, [80, 88], 92, "review");
        var selection = Unique(pending, observation);
        var a = Snapshot([pending.MigrationCandle], 92, observation);
        var missing = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.DataUnavailable>(resolver.Evaluate(selection, a));
        Assert.Equal(new[] { Start.AddHours(88) }, missing.UnavailableMemberOpenTimesUtc);
        AssertFrozen(selection, missing);
        Assert.Throws<InvalidOperationException>(() => OldResolve(pending, selection, a));
        var adjacent = Candle(88, 120, 180, 85, 123, bearish);
        var b = Snapshot([pending.MigrationCandle, adjacent], 92, observation);
        var resolved = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(resolver.Evaluate(selection, b));
        Assert.Equal(a.AsOfUtc, b.AsOfUtc);
        AssertFrozen(selection, resolved);
        Assert.Equal(new[] { pending.MigrationCandle, adjacent }, resolved.SelectedMembers);
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)missing.UnavailableMemberOpenTimesUtc).Clear());
    }

    [Fact]
    public void AbsentH4FrameReturnsExactUnavailableIdentitiesWithoutPartialResolution()
    {
        var pending = Pending(false);
        var selection = Unique(pending, Observation(pending, [80, 88], 92, "review"));
        var result = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.DataUnavailable>(resolver.Evaluate(selection, Snapshot([], 92)));
        Assert.Equal(selection.SemanticMemberOpenTimesUtc, result.UnavailableMemberOpenTimesUtc);
        AssertFrozen(selection, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidSelectedWickIsRejectedUnderOld009ContractInsteadOfUnavailable(bool bearish)
    {
        var pending = Pending(bearish);
        var observation = Observation(pending, [80, 88], 92, "review");
        var selection = Unique(pending, observation);
        var wrong = Candle(88, 120, 180, 70, 123, bearish);
        var context = Snapshot([pending.MigrationCandle, wrong], 92, observation);
        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(selection, context));
        Assert.Throws<InvalidOperationException>(() => OldResolve(pending, selection, context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureSourceCandlesDoNotChangeHistoricalMembersOrUnavailableDiagnostics(bool bearish)
    {
        var pending = Pending(bearish);
        var observation = Observation(pending, [80], 92, "review");
        var selection = Unique(pending, observation);
        var future = Candle(96, 120, 180, 1, 123, bearish);
        var a = Snapshot([pending.MigrationCandle], 92, observation);
        var b = Snapshot([pending.MigrationCandle, future], 92, observation);
        var first = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(resolver.Evaluate(selection, a));
        var second = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved>(resolver.Evaluate(selection, b));
        Assert.Equal(first.SelectedMembers, second.SelectedMembers);
        Assert.Equal(OldResolve(pending, selection, a).SelectedMembers, OldResolve(pending, selection, b).SelectedMembers);
        var missingSelection = Unique(pending, Observation(pending, [80, 88], 92, "review:missing"));
        var missingA = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.DataUnavailable>(resolver.Evaluate(missingSelection, a));
        var missingB = Assert.IsType<NasdaqPostCompletionRebuiltCandidateMemberResolution.DataUnavailable>(resolver.Evaluate(missingSelection, b));
        Assert.Equal(missingA.UnavailableMemberOpenTimesUtc, missingB.UnavailableMemberOpenTimesUtc);
    }

    [Fact]
    public void RejectsMismatchedMigrationIntervalAndInconsistentUniqueContextOrDuplicateIdentities()
    {
        var pending = Pending(false);
        var observation = Observation(pending, [80], 92, "review");
        var selection = Unique(pending, observation);
        var migration = pending.MigrationCandle;
        var changedInterval = new Candle(Provider, Symbol, H4, migration.OpenTimeUtc, migration.CloseTimeUtc.AddMinutes(1), migration.Open, migration.High, migration.Low, migration.Close, null);
        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(selection, Snapshot([changedInterval], 92, observation)));
        var other = Pending(false, 84);
        var otherObservation = Observation(other, [84], 92, "review:other");
        var constructor = Assert.Single(typeof(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var wrongContext = (NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady)constructor.Invoke([pending, Start.AddHours(92), new[] { otherObservation }, new[] { Start.AddHours(80) }]);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(wrongContext, Snapshot([migration], 92)));
        var duplicate = (NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady)constructor.Invoke([pending, Start.AddHours(92), new[] { observation }, new[] { Start.AddHours(80), Start.AddHours(80) }]);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(duplicate, Snapshot([migration], 92)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WrongProviderOrSymbolCannotSupplyHistoricalMemberData(bool wrongProvider)
    {
        var pending = Pending(false);
        var selection = Unique(pending, Observation(pending, [80], 92, "review"));
        var provider = wrongProvider ? new MarketDataProviderId("other") : Provider;
        var symbol = wrongProvider ? Symbol : new MarketSymbol("OTHER");
        var candle = new Candle(provider, symbol, H4, Start.AddHours(88), Start.AddHours(92), 120, 180, 80, 123, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(provider, symbol, H4, [candle])]);
        cursor.TryAdvance(out var frame);
        var context = new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(selection, context));
    }

    [Fact]
    public void TypedClosedApiAcceptsOnlyUniqueAndExposesMembersOrDataAvailability()
    {
        var pending = Pending(false);
        var unique = Unique(pending, Observation(pending, [80], 92, "review"));
        Assert.Throws<ArgumentNullException>(() => resolver.Evaluate(null!, Snapshot([], 92)));
        Assert.Throws<ArgumentNullException>(() => resolver.Evaluate(unique, null!));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(unique, Snapshot([pending.MigrationCandle], 88)));
        var method = Assert.Single(typeof(NasdaqPostCompletionRebuiltCandidateMemberResolver).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { typeof(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady), typeof(StrategyReplayContext) }, method.GetParameters().Select(p => p.ParameterType));
        var union = typeof(NasdaqPostCompletionRebuiltCandidateMemberResolution);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "DataUnavailable", "MembersResolved" }, union.GetNestedTypes().Select(t => t.Name).Order());
        Assert.Equal(new[] { "Context", "MarketCursor", "PendingState", "Selection" }, union.GetProperties().Select(p => p.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
        }
    }

    private static void AssertFrozen(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection, NasdaqPostCompletionRebuiltCandidateMemberResolution result)
    {
        Assert.Same(selection, result.Selection);
        Assert.Same(selection.PendingState, result.PendingState);
        Assert.Same(selection.Context, result.Context);
        Assert.Same(selection.PendingState.MigrationCandle, result.MarketCursor);
        Assert.Same(selection.PendingState.Episode, result.PendingState.Episode);
        Assert.Same(selection.PendingState.ActivePair, result.PendingState.ActivePair);
        Assert.Same(selection.PendingState.PreviousProtectedTurn, result.PendingState.PreviousProtectedTurn);
        Assert.Same(selection.PendingState.SourceCandidate, result.PendingState.SourceCandidate);
        Assert.Same(selection.PendingState.Migration, result.PendingState.Migration);
    }
    private static NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady Unique(NasdaqPostCompletionRebuildPendingState pending, params NasdaqHumanPostCompletionRebuiltCandidateObservation[] observations) =>
        Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(new NasdaqHumanPostCompletionRebuiltCandidateObservationSelector().Select(pending, At(92, observations)));
    private static NasdaqHumanRebuiltCandidateVertexMemberResolution OldResolve(NasdaqPostCompletionRebuildPendingState pending,
        NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection, StrategyReplayContext context)
    {
        var parent = pending.Episode.PreviousCompletedEpisode;
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(parent.StrategyId, parent.StrategyVersion, parent.ProviderId, parent.Symbol,
            parent.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide);
        var observations = selection.SupportingObservations.Select(o => new NasdaqHumanRebuiltCandidateVertexObservation(o.StrategyId, o.StrategyVersion, o.ProviderId, o.Symbol,
            parent.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide, o.SelectedMemberOpenTimesUtc, o.ObservedAtUtc, o.SourceReference)).ToArray();
        var oldContext = Assert.Single(typeof(NasdaqHumanRebuiltCandidateVertexResolutionContext).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).Invoke([episode, pending.MigrationCandle, pending.KnownProtectionAnchor]);
        var ctor = Assert.Single(typeof(NasdaqHumanRebuiltCandidateVertexObservationSelection).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var oldSelection = (NasdaqHumanRebuiltCandidateVertexObservationSelection)ctor.Invoke([NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique, selection.SemanticMemberOpenTimesUtc, observations, new[] { selection.SemanticMemberOpenTimesUtc }]);
        return new NasdaqHumanRebuiltCandidateVertexMemberResolver().Evaluate((NasdaqHumanRebuiltCandidateVertexResolutionContext)oldContext, oldSelection, context);
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
