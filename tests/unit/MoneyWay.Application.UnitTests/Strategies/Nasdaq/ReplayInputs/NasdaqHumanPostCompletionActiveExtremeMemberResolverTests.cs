using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanPostCompletionActiveExtremeMemberResolverTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanPostCompletionActiveExtremeMemberResolver resolver = new();

    [Fact]
    public void ResolvesExactChronologicalMembersAndPreservesAllEvidence()
    {
        var evt = Event();
        var a = Member(16); var b = Member(20);
        var first = Observation(evt, [20, 16], "review:first");
        var second = Observation(evt, [16, 20], "review:second");
        var context = MarketContext([a, b, evt.CorrectionStartCandle], 40, first, second);
        var unique = Select(evt, context);
        var resolved = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(unique, context));
        Assert.Same(unique, resolved.Selection);
        Assert.Same(evt, resolved.MembershipEvent);
        Assert.Equal([a, b], resolved.SelectedMembers);
        Assert.Same(a, resolved.SelectedMembers[0]); Assert.Same(b, resolved.SelectedMembers[1]);
        Assert.Equal([first, second], resolved.Selection.SupportingObservations);
        Assert.Equal("review:second", resolved.Selection.SupportingObservations[1].SourceReference);
        Assert.Equal(Start.AddHours(40), resolved.Selection.SupportingObservations[1].ObservedAtUtc);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)resolved.SelectedMembers).Clear());
        Assert.Equal(resolved.SelectedMembers,
            Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(unique, context)).SelectedMembers);
        Assert.Equal([Start.AddHours(16), Start.AddHours(20)], first.SelectedMemberOpenTimesUtc);
        Assert.True(context.TryGetFrame(H4, out var sourceFrame));
        Assert.Equal(3, sourceFrame!.AvailableCandles.Count);
    }

    [Fact]
    public void RecoversMissingHistoricalMemberAtSameAsOfWithoutPartialSuccess()
    {
        var evt = Event();
        var a = Member(16); var b = Member(20);
        var observation = Observation(evt, [16, 20]);
        var missingContext = MarketContext([a, evt.CorrectionStartCandle], 40, observation);
        var unique = Select(evt, missingContext);
        var missing = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable>(resolver.Evaluate(unique, missingContext));
        Assert.Equal([b.OpenTimeUtc], missing.UnavailableCandleOpenTimesUtc);
        Assert.Same(unique, missing.Selection);
        var recoveredContext = MarketContext([a, b, evt.CorrectionStartCandle], 40, observation);
        Assert.Equal([a, b], Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(unique, recoveredContext)).SelectedMembers);
        Assert.Equal([b.OpenTimeUtc], missing.UnavailableCandleOpenTimesUtc);
    }

    [Fact]
    public void MissingExactSeriesOrBoundaryReturnsDataUnavailable()
    {
        var evt = Event(); var observation = Observation(evt, [16]);
        var absent = MarketContext([], 40, observation);
        var unique = Select(evt, absent);
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable>(resolver.Evaluate(unique, absent));
        var noTurn = MarketContext([Member(16)], 40, observation);
        Assert.Equal([evt.CorrectionStartCandleOpenTimeUtc],
            Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable>(resolver.Evaluate(unique, noTurn)).UnavailableCandleOpenTimesUtc);
    }

    [Fact]
    public void NonContiguousMembershipIsRejectedWithoutInferenceEvenWhenAnotherMemberIsMissing()
    {
        var evt = Event(); var observation = Observation(evt, [12, 20]);
        var context = MarketContext([Member(12), Member(16), Member(20), evt.CorrectionStartCandle], 40, observation);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(Select(evt, context), context));
        var missing = MarketContext([Member(16), Member(20), evt.CorrectionStartCandle], 40, observation);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(Select(evt, missing), missing));
        Assert.Equal([Start.AddHours(12), Start.AddHours(20)], observation.SelectedMemberOpenTimesUtc);
    }

    [Fact]
    public void AvailableAdjacencyAllowsTimestampGapsWithoutTurnInclusion()
    {
        var evt = Event(); var observation = Observation(evt, [8, 20]);
        var a = Member(8); var b = Member(20);
        var context = MarketContext([a, b, evt.CorrectionStartCandle], 40, observation);
        Assert.Equal([a, b], Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(Select(evt, context), context)).SelectedMembers);
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 40)]
    [InlineData(false, 80)]
    [InlineData(true, 80)]
    public void PostTurnMemberIsInvalidRegardlessOfDataPresenceAndTime(bool present, int asOf)
    {
        var evt = Event(); var observation = Observation(evt, [36]);
        var context = MarketContext(present ? [evt.CorrectionStartCandle, Member(36)] : [evt.CorrectionStartCandle], asOf, observation);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(Select(evt, context), context));
    }

    [Fact]
    public void ActualSourceCloseAfterHorizonIsInvalidWithoutAssumingFourHourDuration()
    {
        var evt = Event(); var observation = Observation(evt, [20]);
        var longCandle = new Candle(Provider, Symbol, H4, Start.AddHours(20), Start.AddHours(36), 100, 110, 90, 100, null);
        // A selected real interval can disprove the horizon even when the exact turn is unavailable.
        var context = MarketContext([longCandle], 40, observation);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(Select(evt, context), context));
    }

    [Fact]
    public void UnderlyingFutureDataDoesNotChangeResolutionAtSameHistoricalFrame()
    {
        var evt = Event(); var observation = Observation(evt, [16, 20]);
        var history = new[] { Member(16), Member(20), evt.CorrectionStartCandle };
        var without = MarketContext(history, 40, observation);
        var withFuture = MarketContext([.. history, Member(48), Member(52)], 40, observation);
        var unique = Select(evt, without);
        Assert.Equal(Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(unique, without)).SelectedMembers,
            Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(unique, withFuture)).SelectedMembers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WrongMarketIdentityCannotSubstituteForExactSeries(int mismatch)
    {
        var evt = Event(); var observation = Observation(evt, [16]);
        var correctContext = MarketContext([Member(16), evt.CorrectionStartCandle], 40, observation);
        var unique = Select(evt, correctContext);
        var wrong = new Candle(mismatch == 0 ? new("other") : Provider, mismatch == 1 ? new("other") : Symbol,
            mismatch == 2 ? new(1, TimeframeUnit.Hour) : H4, Start.AddHours(16), Start.AddHours(20), 100, 110, 90, 100, null);
        if (mismatch < 2)
        {
            var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(wrong.ProviderId, wrong.Symbol, wrong.Timeframe, [wrong])]);
            cursor.TryAdvance(out var frame);
            var context = new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!);
            Assert.Throws<ArgumentException>(() => resolver.Evaluate(unique, context));
        }
        else
        {
            var context = MarketContext([wrong], 40, observation);
            Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable>(resolver.Evaluate(unique, context));
        }
    }

    [Fact]
    public void RejectsInvisibleSelectionAndChangedTurnSourceAndNullInputs()
    {
        var evt = Event(); var observation = Observation(evt, [16]);
        var context = MarketContext([Member(16), evt.CorrectionStartCandle], 40, observation);
        var unique = Select(evt, context);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(unique, MarketContext([Member(16)], 40)));
        var changed = Candle(28, 141, 150, 120, 130);
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(unique, MarketContext([Member(16), changed], 40, observation)));
        Assert.Throws<ArgumentNullException>(() => resolver.Evaluate(null!, context));
        Assert.Throws<ArgumentNullException>(() => resolver.Evaluate(unique, null!));
    }

    private static NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique Select(
        NasdaqPostCompletionActiveExtremeMembershipEvent evt, StrategyReplayContext context) =>
        Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(
            new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, evt));

    private static NasdaqHumanPostCompletionActiveExtremeObservation Observation(
        NasdaqPostCompletionActiveExtremeMembershipEvent evt, int[] hours, string source = "review:members") =>
        new(evt, hours.Select(hour => Start.AddHours(hour)), Start.AddHours(40), source);

    private static Candle Member(int hour) => Candle(hour, 100, 110, 90, 100);

    private static StrategyReplayContext MarketContext(Candle[] candles, int hour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var close = Start.AddHours(hour);
        var trigger = new Candle(Provider, Symbol, minute, close.AddMinutes(-1), close, 100, 110, 90, 100, null);
        var series = candles.Length == 0 ? Array.Empty<CandleSeries>()
            : [new CandleSeries(Provider, Symbol, candles[0].Timeframe, candles)];
        var cursor = new MultiTimeframeCandleReplayCursor([.. series, new CandleSeries(Provider, Symbol, minute, [trigger])]);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == close)
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        throw new InvalidOperationException("Historical frame was not found.");
    }
    [Fact]
    public void SameCandleTurnResolvesWithoutDuplicationOrRequiredInclusion()
    {
        var evt = Event(immediate: true);
        var observation = Observation(evt, [20]);
        var context = MarketContext([evt.CorrectionStartCandle], 40, observation);
        var resolved = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(resolver.Evaluate(Select(evt, context), context));
        Assert.Same(evt.Episode.ConfirmingCandle, Assert.Single(resolved.SelectedMembers));
    }

    private static NasdaqPostCompletionActiveExtremeMembershipEvent Event(int offset = 0, bool immediate = false)
    {
        var origin = Candle(offset, 100, 110, 85, 90);
        var prior = Candle(4 + offset, 105, 115, 100, 110);
        var invalidating = Candle(8 + offset, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12 + offset, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, Candle(16 + offset, 80, 100, 60, 90)).Candidate!;
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate,
                immediate ? Candle(20 + offset, 140, 150, 60, 131) : Candle(20 + offset, 130, 140, 60, 131)));
        return new(NasdaqPostCompletionEpisode.FromCompleted(completed),
            immediate ? completed.MarketCursor : Candle(28 + offset, 140, 150, 120, 130));
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
