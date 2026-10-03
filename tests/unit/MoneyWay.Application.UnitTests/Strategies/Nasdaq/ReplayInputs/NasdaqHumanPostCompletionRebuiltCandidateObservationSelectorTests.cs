using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanPostCompletionRebuiltCandidateObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanPostCompletionRebuiltCandidateObservationSelector selector = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingPreservesPendingBoundaryWithoutRequiringH4Members(bool bearish)
    {
        var pending = Pending(bearish);
        var result = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Missing>(selector.Select(pending, At(84)));
        Assert.Empty(result.SupportingObservations);
        AssertFrozen(pending, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompatibleReorderedSetsPreserveEveryObservationAndAllUpstreamProvenance(bool bearish)
    {
        var pending = Pending(bearish);
        var first = Observation(pending, [80, 84], 88, "review:z");
        var second = Observation(pending, [84, 80], 92, "review:a");
        var third = Observation(pending, [80, 84], 92, "review:b");
        var context = At(92, third, second, first, first);
        var snapshot = context.InputObservations.ToArray();
        var result = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(selector.Select(pending, context));
        Assert.Equal(new[] { Start.AddHours(80), Start.AddHours(84) }, result.SemanticMemberOpenTimesUtc);
        Assert.Equal(new[] { first, first, second, third }, result.SupportingObservations);
        Assert.Same(first, result.SupportingObservations[0]);
        Assert.Same(second, result.SupportingObservations[2]);
        Assert.Equal(new[] { "review:z", "review:z", "review:a", "review:b" }, result.SupportingObservations.Select(o => o.SourceReference));
        Assert.Equal(new[] { Start.AddHours(88), Start.AddHours(88), Start.AddHours(92), Start.AddHours(92) }, result.SupportingObservations.Select(o => o.ObservedAtUtc));
        Assert.Equal(snapshot, context.InputObservations);
        var repeated = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(selector.Select(pending, At(92, first, second, first, third)));
        Assert.Equal(result.SemanticMemberOpenTimesUtc, repeated.SemanticMemberOpenTimesUtc);
        Assert.Equal(result.SupportingObservations, repeated.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanPostCompletionRebuiltCandidateObservation>)result.SupportingObservations).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)result.SemanticMemberOpenTimesUtc).Clear());
        AssertFrozen(pending, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompatibleSetsConflictWithoutWinnerAndDuplicatesCannotSupersede(bool bearish)
    {
        var pending = Pending(bearish);
        var a = Observation(pending, [80], 88, "review:a");
        var b = Observation(pending, [80, 84], 88, "review:b");
        var initial = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict>(selector.Select(pending, At(88, b, a)));
        var duplicate = Observation(pending, [80], 92, "review:latest");
        var result = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict>(selector.Select(pending, At(92, duplicate, b, a)));
        Assert.Equal(2, result.ConflictingMemberships.Count);
        Assert.Equal(initial.ConflictingMemberships, result.ConflictingMemberships);
        Assert.Equal(new[] { a, b, duplicate }, result.SupportingObservations);
        var reordered = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict>(selector.Select(pending, At(92, a, duplicate, b)));
        Assert.Equal(result.ConflictingMemberships, reordered.ConflictingMemberships);
        Assert.Equal(result.SupportingObservations, reordered.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((ICollection<IReadOnlyList<DateTimeOffset>>)result.ConflictingMemberships).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)result.ConflictingMemberships[0]).Clear());
        AssertFrozen(pending, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateEvidenceResolvesSameEventAndFutureConflictCannotChangeEarlierReplay(bool bearish)
    {
        var pending = Pending(bearish);
        var a = Observation(pending, [80], 88, "review:a");
        var b = Observation(pending, [80, 84], 92, "review:future");
        var early = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Missing>(selector.Select(pending, At(84, a, b)));
        var unique = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(selector.Select(pending, At(88, a, b)));
        var withoutFuture = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(selector.Select(pending, At(88, a)));
        Assert.Equal(unique.SupportingObservations, withoutFuture.SupportingObservations);
        Assert.Equal(unique.SemanticMemberOpenTimesUtc, withoutFuture.SemanticMemberOpenTimesUtc);
        Assert.Single(unique.SupportingObservations);
        var conflict = Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict>(selector.Select(pending, At(92, a, b)));
        Assert.All(new NasdaqHumanPostCompletionRebuiltCandidateSelection[] { early, unique, conflict }, result => AssertFrozen(pending, result));
    }

    [Fact]
    public void FullContextEqualityExcludesOtherEpisodeMigrationAndSideButAllowsEquivalentContext()
    {
        var pending = Pending(false);
        var anotherEpisode = Pending(false, completionHour: 22);
        var anotherMigration = Pending(false, hour: 84);
        var anotherSide = Pending(true);
        Assert.NotEqual(pending.EvidenceContext, anotherEpisode.EvidenceContext);
        var unrelated = new[] { Observation(anotherEpisode, [80], 92, "review:episode"), Observation(anotherMigration, [84], 92, "review:migration"), Observation(anotherSide, [80], 92, "review:side") };
        Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.Missing>(selector.Select(pending, At(92, unrelated)));
        var equivalent = Pending(false);
        var matching = Observation(equivalent, [80], 92, "review:equivalent");
        Assert.Equal(pending.EvidenceContext, matching.Context);
        Assert.Single(Assert.IsType<NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady>(selector.Select(pending, At(92, matching))).SupportingObservations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OldAndNewSelectionAgreeOnMissingUniqueConflictCutoffAndNoSupersession(int scenario)
    {
        var pending = Pending(false);
        var a = Observation(pending, [80], 88, "review:a");
        var duplicate = Observation(pending, [80], 92, "review:duplicate");
        var b = Observation(pending, [80, 84], 92, "review:b");
        var observations = scenario switch
        {
            0 => Array.Empty<NasdaqHumanPostCompletionRebuiltCandidateObservation>(),
            1 => new[] { a, duplicate },
            2 or 3 => new[] { a, b },
            _ => new[] { a, b, duplicate },
        };
        var hour = scenario == 3 ? 88 : 92;
        var result = selector.Select(pending, At(hour, observations));
        var parent = pending.Episode.PreviousCompletedEpisode;
        var oldObservations = observations.Select(o => new NasdaqHumanRebuiltCandidateVertexObservation(o.StrategyId, o.StrategyVersion,
            o.ProviderId, o.Symbol, parent.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide,
            o.SelectedMemberOpenTimesUtc, o.ObservedAtUtc, o.SourceReference)).ToArray();
        var oldEpisode = new NasdaqHumanRebuiltCandidateVertexEpisode(parent.StrategyId, parent.StrategyVersion, parent.ProviderId,
            parent.Symbol, parent.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc, pending.CandidateSide);
        var old = new NasdaqHumanRebuiltCandidateVertexObservationSelector().Select(At(hour, oldObservations), oldEpisode);
        Assert.Equal(old.SupportingObservations.Select(o => (o.SourceReference, o.ObservedAtUtc)), result.SupportingObservations.Select(o => (o.SourceReference, o.ObservedAtUtc)));
        switch (result)
        {
            case NasdaqHumanPostCompletionRebuiltCandidateSelection.Missing:
                Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing, old.Kind);
                break;
            case NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady unique:
                Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique, old.Kind);
                Assert.Equal(old.SemanticMemberOpenTimesUtc, unique.SemanticMemberOpenTimesUtc);
                break;
            case NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict conflict:
                Assert.Equal(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict, old.Kind);
                Assert.Equal(old.DistinctMemberships, conflict.ConflictingMemberships);
                break;
        }
    }

    [Fact]
    public void ObservationValidatesAuditedMemberIdentityAndCausalEnvelopeAndSnapshotsInputs()
    {
        var pending = Pending(false);
        var input = new[] { Start.AddHours(84), Start.AddHours(80) };
        var observation = new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, input, Start.AddHours(88), "review");
        input[0] = Start.AddHours(100);
        var equivalent = Observation(pending, [80, 84], 88, "review");
        Assert.Equal(equivalent, observation);
        Assert.Equal(equivalent.GetHashCode(), observation.GetHashCode());
        Assert.NotEqual(observation, Observation(pending, [80, 84], 92, "review"));
        Assert.NotEqual(observation, Observation(pending, [80, 84], 88, "review:other"));
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)observation.SelectedMemberOpenTimesUtc).Clear());
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(null!, input, Start.AddHours(108), "review"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, null!, Start.AddHours(88), "review"));
        foreach (var invalid in new[] { Array.Empty<DateTimeOffset>(), new[] { Start.AddHours(84) }, new[] { Start.AddHours(80), Start.AddHours(80) }, new[] { Start.AddHours(80), Start.AddHours(84).ToOffset(TimeSpan.FromHours(1)) } })
            Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, invalid, Start.AddHours(92), "review"));
        foreach (var time in new[] { Start.AddHours(83), Start.AddHours(87), Start.AddHours(88).ToOffset(TimeSpan.FromHours(1)) })
            Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, [Start.AddHours(80), Start.AddHours(84)], time, "review"));
        foreach (var source in new[] { "", " ", " review" })
            Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, [Start.AddHours(80)], Start.AddHours(88), source));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionRebuiltCandidateObservation(pending, [Start.AddHours(80)], Start.AddHours(88), null!));
    }

    [Fact]
    public void SelectorRejectsNullOrPreMigrationReplayContext()
    {
        var pending = Pending(false);
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, At(92)));
        Assert.Throws<ArgumentNullException>(() => selector.Select(pending, null!));
        Assert.Throws<ArgumentException>(() => selector.Select(pending, At(80)));
    }

    [Fact]
    public void ClosedReadinessApiContainsOnlyMembershipEvidenceWithoutDownstreamMarketResults()
    {
        var union = typeof(NasdaqHumanPostCompletionRebuiltCandidateSelection);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "Conflict", "Missing", "UniqueEvidenceReady" }, union.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
        }
        Assert.Equal(new[] { "AsOfUtc", "Context", "PendingState", "SupportingObservations" }, union.GetProperties().Select(p => p.Name).Order());
        var observation = typeof(NasdaqHumanPostCompletionRebuiltCandidateObservation);
        Assert.All(observation.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "Context", "ObservedAtUtc", "ProviderId", "SelectedMemberOpenTimesUtc", "SourceReference", "StrategyId", "StrategyVersion", "Symbol", "Timeframe" }, observation.GetProperties().Select(p => p.Name).Order());
    }

    private static void AssertFrozen(NasdaqPostCompletionRebuildPendingState pending, NasdaqHumanPostCompletionRebuiltCandidateSelection result)
    {
        Assert.Same(pending, result.PendingState);
        Assert.Same(pending.EvidenceContext, result.Context);
        Assert.Same(pending.MigrationCandle, result.PendingState.MarketCursor);
        Assert.Same(pending.Episode, result.PendingState.Episode);
        Assert.Same(pending.ActivePair, result.PendingState.ActivePair);
        Assert.Same(pending.SourceCandidate, result.PendingState.SourceCandidate);
        Assert.Same(pending.Decision, result.PendingState.Decision);
        Assert.Same(pending.Migration, result.PendingState.Migration);
        Assert.Same(pending.SourceCandidate.SourceCorrection.GeometryReady, result.PendingState.SourceCandidate.SourceCorrection.GeometryReady);
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
