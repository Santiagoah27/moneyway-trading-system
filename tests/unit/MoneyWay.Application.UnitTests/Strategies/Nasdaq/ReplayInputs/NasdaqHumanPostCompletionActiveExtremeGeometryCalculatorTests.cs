using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanPostCompletionActiveExtremeGeometryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanPostCompletionActiveExtremeGeometryCalculator calculator = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DerivesSideAndIndependentBodyWickExtremaAcrossExactMembers(bool bearish)
    {
        var evt = Event(bearish);
        var wickOwner = Candle(12, 110, 180, 90, 120, bearish);
        var bodyOwner = Candle(16, 150, 160, 100, 140, bearish);
        var third = Candle(24, 125, 155, 100, 130, bearish);
        var members = Resolve(evt, [wickOwner, bodyOwner, third], [Candle(8, 190, 195, 50, 185, bearish)]);
        var result = calculator.Evaluate(members);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper, result.Geometry.Side);
        Assert.Equal(bearish ? 50m : 150m, result.Geometry.StructuralPrice);
        Assert.Equal(bearish ? 20m : 180m, result.Geometry.ProtectionAnchor);
        Assert.Same(members, result.MemberResolution);
        Assert.Same(evt, result.MemberResolution.MembershipEvent);
        Assert.Equal([wickOwner, bodyOwner, third], result.MemberResolution.SelectedMembers);
        Assert.Equal(bearish ? wickOwner.Low : wickOwner.High, result.Geometry.ProtectionAnchor);
        Assert.NotEqual(bearish ? bodyOwner.Low : bodyOwner.High, result.Geometry.ProtectionAnchor);
        Assert.True(bearish ? result.Geometry.ProtectionAnchor <= result.Geometry.StructuralPrice
            : result.Geometry.ProtectionAnchor >= result.Geometry.StructuralPrice);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BodyCoordinateUsesOpenOrCloseAccordingToSide(bool bearish, bool closeOwns)
    {
        var member = Candle(16, closeOwns ? 120 : 150, 170, 100, closeOwns ? 150 : 120, bearish);
        var result = calculator.Evaluate(Resolve(Event(bearish), [member]));
        Assert.Equal(bearish ? 50m : 150m, result.Geometry.StructuralPrice);
        Assert.Equal(closeOwns ? member.Close : member.Open, result.Geometry.StructuralPrice);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SelectedConfirmingCandleHasNoSpecialPriority(bool bearish, bool confirmationOwns)
    {
        var evt = Event(bearish);
        var other = confirmationOwns ? Candle(16, 110, 135, 100, 120, bearish)
            : Candle(16, 150, 180, 100, 140, bearish);
        var result = calculator.Evaluate(Resolve(evt, [other, evt.Episode.ConfirmingCandle]));
        Assert.Equal(bearish ? (confirmationOwns ? 69m : 50m) : (confirmationOwns ? 131m : 150m), result.Geometry.StructuralPrice);
        Assert.Equal(bearish ? (confirmationOwns ? 60m : 20m) : (confirmationOwns ? 140m : 180m), result.Geometry.ProtectionAnchor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DojiAndAllTiedSupportingMembersRemainAuditable(bool bearish)
    {
        var a = Candle(12, 150, 170, 100, 150, bearish);
        var b = Candle(16, 140, 170, 100, 150, bearish);
        var c = Candle(24, 150, 160, 100, 140, bearish);
        var result = calculator.Evaluate(Resolve(Event(bearish), [a, b, c]));
        Assert.Equal(bearish ? 50m : 150m, result.Geometry.StructuralPrice);
        Assert.Equal(bearish ? 30m : 170m, result.Geometry.ProtectionAnchor);
        Assert.Equal([a, b, c], result.MemberResolution.SelectedMembers);
        var supportingWicks = result.MemberResolution.SelectedMembers.Where(member =>
            (bearish ? member.Low : member.High) == result.Geometry.ProtectionAnchor);
        Assert.Equal([a, b], supportingWicks);
        var supportingBodies = result.MemberResolution.SelectedMembers.Where(member =>
            (bearish ? decimal.Min(member.Open, member.Close) : decimal.Max(member.Open, member.Close)) == result.Geometry.StructuralPrice);
        Assert.Equal([a, b, c], supportingBodies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesAllProvenanceAndIsDeterministicWithoutMutatingResolution(bool bearish)
    {
        var evt = Event(bearish);
        var a = Candle(12, 110, 180, 90, 120, bearish);
        var b = Candle(16, 150, 160, 100, 140, bearish);
        var resolved = Resolve(evt, [a, b]);
        var originalMembers = resolved.SelectedMembers.ToArray();
        var observations = resolved.Selection.SupportingObservations.ToArray();
        var first = calculator.Evaluate(resolved);
        var second = calculator.Evaluate(resolved);
        Assert.Equal(first.Geometry, second.Geometry);
        Assert.Same(resolved.Selection, first.MemberResolution.Selection);
        Assert.Equal(observations, first.MemberResolution.Selection.SupportingObservations);
        Assert.Equal(2, observations.Length);
        Assert.Equal("review:first", observations[0].SourceReference);
        Assert.Equal(Start.AddHours(40), observations[1].ObservedAtUtc);
        Assert.Equal(originalMembers, resolved.SelectedMembers);
        Assert.Same(a, resolved.SelectedMembers[0]);
        Assert.Same(b, resolved.SelectedMembers[1]);
        var reverseEvidence = Resolve(evt, [a, b], reverse: true);
        Assert.Equal(first.Geometry, calculator.Evaluate(reverseEvidence).Geometry);
        Assert.Equal(resolved.SelectedMembers, reverseEvidence.SelectedMembers);
    }

    [Fact]
    public void RejectsNullSuccessInput() => Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!));

    private static NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved Resolve(
        NasdaqPostCompletionActiveExtremeMembershipEvent evt, Candle[] selected, Candle[]? outside = null, bool reverse = false)
    {
        var ids = selected.Select(candle => candle.OpenTimeUtc).ToArray();
        var first = new NasdaqHumanPostCompletionActiveExtremeObservation(evt, reverse ? ids : ids.Reverse(), Start.AddHours(40), "review:first");
        var second = new NasdaqHumanPostCompletionActiveExtremeObservation(evt, reverse ? ids.Reverse() : ids, Start.AddHours(40), "review:second");
        var source = selected.Concat(outside ?? []).Append(evt.CorrectionStartCandle).Append(Candle(40, 100, 110, 90, 100))
            .OrderBy(candle => candle.OpenTimeUtc).ToArray();
        var context = Context(source, first, second);
        var unique = Assert.IsType<NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique>(
            new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, evt));
        return Assert.IsType<NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved>(
            new NasdaqHumanPostCompletionActiveExtremeMemberResolver().Evaluate(unique, context));
    }

    private static NasdaqPostCompletionActiveExtremeMembershipEvent Event(bool bearish)
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
        return new(NasdaqPostCompletionEpisode.FromCompleted(completed), C(28, 140, 150, 120, 130));
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close, bool bearish = false) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            bearish ? 200 - open : open, bearish ? 200 - low : high, bearish ? 200 - high : low,
            bearish ? 200 - close : close, null);
}
