using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanLiquidityTakeTests
{
    private readonly NasdaqHumanLiquidityTakeObservationSelector selector = new();
    private static readonly Candle[] SessionSources = Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h)).ToArray();

    [Theory]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh)]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow)]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh)]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow)]
    public void EstablishedSessionEndpointIsConsumedWithoutRecalculatingOrMergingEndpoints(NasdaqLiquidityTakeReference.SessionEndpoint endpoint)
    {
        var reference = SessionReference(endpoint);
        var observation = Take(reference);
        var selected = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [observation]), reference));
        Assert.Same(observation, selected.Fact);
        Assert.Same(reference, selected.Fact.Reference);
        Assert.Equal(reference.Side == NasdaqStructuralLiquiditySide.High ? 110 : 90, reference.ReferencePrice);
        Assert.Equal(14, reference.SourceCandles.Count);
        Assert.Equal("review:selected endpoint", reference.SourceReference);
        Assert.Equal(LiquidityFixture.At(13), observation.ReferenceEligibleAtUtc);
        Assert.Equal(LiquidityFixture.At(13).AddMinutes(30), observation.EffectiveAtUtc);
        Assert.Equal(LiquidityFixture.At(14), observation.ObservedAtUtc);
        Assert.Equal("review:take", observation.SourceReference);
        Assert.Equal("source:event", ((NasdaqHumanLiquidityTakeEvent.Documented)observation.Event).EventId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StructuralMemberPreservesBothCoordinateOwnershipsAndUpstreamSupport(bool human, bool low)
    {
        var (reference, upstream, source) = StructuralReference(human, low);
        var observation = Take(reference);
        var selected = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(source, [upstream, observation]), reference));
        Assert.Same(reference.Member, ((NasdaqLiquidityTakeReference.Structural)selected.Fact.Reference).Member);
        Assert.Equal(human ? NasdaqStructuralLiquidityPriceOwnership.HumanDocumented : NasdaqStructuralLiquidityPriceOwnership.FormulaBacked,
            reference.Member.PriceOwnership);
        Assert.Equal(reference.Member.StructuralPrice, observation.ReferencePrice);
        Assert.Same(upstream, reference.Selection.SupportingObservations[0]);
        Assert.Equal(human ? 21500 : 100, observation.ReferencePrice);
    }

    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 0)]
    public void WrongSideAndEqualityCannotConstructPositiveEvidence(bool high, int offset)
    {
        var reference = SessionReference(high ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow);
        Assert.Throws<ArgumentException>(() => Take(reference, price: reference.ReferencePrice + offset));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SmallestDecimalDifferenceAtSelectedPriceNeedsNoTolerance(bool high)
    {
        var (reference, upstream, source) = StructuralReference(true, !high, 0m);
        var delta = 0.0000000000000000000000000001m;
        var observation = Take(reference, price: high ? delta : -delta);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(source, [upstream, observation]), reference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ImpossibleTimeOrderIsRejected(int scenario)
    {
        var reference = SessionReference();
        var eligible = LiquidityFixture.At(13);
        var effective = eligible.AddMinutes(30);
        var observed = LiquidityFixture.At(14);
        if (scenario == 0) effective = eligible.AddTicks(-1);
        if (scenario == 1) observed = effective.AddTicks(-1);
        if (scenario == 2) eligible = eligible.AddTicks(-1);
        if (scenario == 3) observed = observed.ToOffset(TimeSpan.FromHours(1));
        Assert.Throws<ArgumentException>(() => Take(reference, effective: effective, observed: observed, eligible: eligible));
    }

    [Fact]
    public void TemporalEqualityIsAllowedWhilePriceEqualityIsNot()
    {
        var reference = SessionReference();
        var observation = Take(reference, effective: LiquidityFixture.At(13), observed: LiquidityFixture.At(13));
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [observation], 13), reference));
    }

    [Fact]
    public void FutureAssertionIsInvisibleAndNeverChangesEarlierSelection()
    {
        var reference = SessionReference();
        var observation = Take(reference, observed: LiquidityFixture.At(15));
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, [observation]), reference));
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [observation], 15), reference));
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, [observation]), reference));
    }

    [Fact]
    public void DifferentSessionAndEqualPriceEndpointCannotSatisfyRequestedReference()
    {
        var asia = SessionReference();
        var london = SessionReference(NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh);
        Assert.Equal(asia.ReferencePrice, london.ReferencePrice);
        var observation = Take(asia);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, [observation]), london));
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, [observation], 40), asia));
    }

    [Fact]
    public void CompatibleDuplicateSupportIsLosslessImmutableAndIndependentOfInputOrder()
    {
        var reference = SessionReference();
        var a = Take(reference, source: "a");
        var b = Take(reference, observed: LiquidityFixture.At(15), source: "b");
        var forwardContext = Context(SessionSources, [a, b], 15);
        var forward = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(forwardContext, reference));
        var reverse = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [b, a], 15), reference));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(new[] { a, b }, forward.SupportingObservations);
        Assert.Equal(new[] { a, b }, forwardContext.InputObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanLiquidityTakeObservation>)forward.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CompetingValidClaimsConflictAndDuplicatesCannotEraseConflict(int scenario)
    {
        var reference = SessionReference();
        var a = Take(reference, source: "a");
        var b = Take(reference, source: "b", price: scenario == 0 ? 112 : null,
            effective: scenario == 1 ? LiquidityFixture.At(13).AddMinutes(31) : null,
            eventId: scenario == 2 ? "another:event" : "source:event",
            eligible: scenario == 3 ? LiquidityFixture.At(13).AddMinutes(1) : null);
        var duplicate = Take(reference, source: "c");
        var forward = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Conflict>(selector.Select(Context(SessionSources, [b, a, duplicate]), reference));
        var reverse = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Conflict>(selector.Select(Context(SessionSources, [duplicate, a, b]), reference));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(3, forward.SupportingObservations.Count);
    }

    [Fact]
    public void UnavailableSourcesRemainDiagnosticsWithoutAFourthOutcomeOrConflict()
    {
        var reference = SessionReference();
        var observation = Take(reference);
        var missing = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources.Skip(1).ToArray(), [observation]), reference));
        Assert.Same(observation, Assert.Single(missing.UnavailableSourceObservations));
        var empty = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, []), reference));
        Assert.Empty(empty.UnavailableSourceObservations);
    }

    [Fact]
    public void AlteredSourceFactsAreUnusableNotAConflictWithValidEvidence()
    {
        var reference = SessionReference();
        var altered = SessionSources.Select(c => c.OpenTimeUtc == LiquidityFixture.At(0) ? LiquidityFixture.Candle(0, close: 101) : c).ToArray();
        var missing = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(altered, [Take(reference)]), reference));
        Assert.Empty(missing.UnavailableSourceObservations);
    }

    [Fact]
    public void LaterOHLCAndPenetratingCandleCannotDiscoverOrChangeTakeFacts()
    {
        var reference = SessionReference();
        var eventObservation = Take(reference);
        var extremes = SessionSources.Append(LiquidityFixture.Candle(13, 100, 300, 20, 250)).Append(LiquidityFixture.Candle(15, 100, 1000, 0, 200)).ToArray();
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(extremes, []), reference));
        var before = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [eventObservation]), reference));
        var withPaths = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(extremes, [eventObservation]), reference));
        Assert.Same(before.Fact, withPaths.Fact);
    }

    [Fact]
    public void StructuralSelectionMustStillBeUniqueAndExactAtConsumption()
    {
        var (reference, upstream, source) = StructuralReference(true, false);
        var conflicting = LiquidityFixture.Observation([LiquidityFixture.Human(source[0], price: 200)], observed: 15);
        var take = Take(reference);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(source, [upstream, take, conflicting]), reference));
        var after = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(source, [upstream, take, conflicting], 15), reference));
        Assert.Empty(after.UnavailableSourceObservations);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(source, [take]), reference));
        var alienMember = LiquidityFixture.Human(source[0], price: 200);
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.Structural(reference.Selection, alienMember));
    }

    [Fact]
    public void StructuralEligibilityCannotPrecedeAuthenticUpstreamAvailability()
    {
        var (reference, _, _) = StructuralReference(true, false);
        Assert.Throws<ArgumentException>(() => Take(reference, eligible: LiquidityFixture.At(12), effective: LiquidityFixture.At(12).AddMinutes(30)));
    }

    [Fact]
    public void CanonicalSourceMustResolveExactlyInBoundedSnapshot()
    {
        var reference = SessionReference();
        var quote = new HistoricalMarketPriceObservation(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            LiquidityFixture.At(13).AddMinutes(30), 111, "source-quote", "documented-source-resolution", 1);
        var observation = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(quote),
            LiquidityFixture.At(13), LiquidityFixture.At(14), "human quote interpretation");
        var missing = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(Context(SessionSources, [observation]), reference));
        Assert.Single(missing.UnavailableSourceObservations);
        var cursor = new CanonicalMultiTimeframeReplayCursor([
            new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Hour), SessionSources.Append(LiquidityFixture.Candle(13)))],
            new HistoricalMarketPriceObservationSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, [quote]));
        StrategyReplayContext? visible = null;
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == LiquidityFixture.At(14)) visible = new CreateStrategyReplayContextUseCase().ExecuteCanonical(LiquidityFixture.Definition, frame, [observation]);
        Assert.NotNull(visible);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(visible!, reference));
    }

    [Fact]
    public void SupportingOHLCIsProvenanceAndDoesNotSupplyEventTime()
    {
        var reference = SessionReference();
        var support = LiquidityFixture.Candle(12, 100, 300, 0, 100, hours: 2);
        var eventTime = LiquidityFixture.At(13).AddMinutes(30);
        var marketEvent = new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            "authentic:event", 111, eventTime, "source:exact event record", [support]);
        var observation = new NasdaqHumanLiquidityTakeObservation(reference, marketEvent, LiquidityFixture.At(13), LiquidityFixture.At(14), "review");
        Assert.NotEqual(support.CloseTimeUtc, observation.EffectiveAtUtc);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources.Append(support).ToArray(), [observation]), reference));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanLiquidityTakeObservation(reference, marketEvent, LiquidityFixture.At(13), eventTime, "review"));
    }

    [Fact]
    public void NullUnknownEndpointAndUnavailableUpstreamResultAreRejected()
    {
        var context = Context(SessionSources, [], 13);
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, SessionReference()));
        Assert.Throws<ArgumentNullException>(() => selector.Select(context, null!));
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.SessionLevel(context, new(null, "missing"),
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(13), "review"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionReference((NasdaqLiquidityTakeReference.SessionEndpoint)999));
        Assert.Throws<ArgumentException>(() => Take(SessionReference(), source: " "));
    }

    [Fact]
    public void ProviderSymbolAndStrategyVersionAreExact()
    {
        var reference = SessionReference();
        Assert.Throws<ArgumentException>(() => new NasdaqHumanLiquidityTakeObservation(reference,
            new NasdaqHumanLiquidityTakeEvent.Documented(new("another provider"), LiquidityFixture.Symbol, "event", 111,
                LiquidityFixture.At(14), "source"), LiquidityFixture.At(13), LiquidityFixture.At(14), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanLiquidityTakeObservation(reference,
            new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, new("another symbol"), "event", 111,
                LiquidityFixture.At(14), "source"), LiquidityFixture.At(13), LiquidityFixture.At(14), "review"));
        var definition = LiquidityFixture.Definition;
        var other = new MoneyWay.Domain.Strategies.StrategyDefinition(definition.StrategyId, new("other version"), definition.DisplayName,
            definition.SpecificationReference, definition.Rules);
        var cursor = new MoneyWay.Domain.MarketData.Replay.MultiTimeframeCandleReplayCursor([
            new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Hour), SessionSources)]);
        StrategyReplayContext? context = null;
        while (cursor.TryAdvance(out var frame)) context = new CreateStrategyReplayContextUseCase().Execute(other, frame!);
        Assert.IsType<NasdaqHumanLiquidityTakeSelection.Missing>(selector.Select(context!, reference));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingH4SpecialModelsRetainTheirCompleteHumanProvenance(bool cluster)
    {
        NasdaqStructuralLiquidityReference member;
        Candle[] sources;
        if (cluster)
        {
            var fixture = LiquidityFixture.Extreme(false);
            member = NasdaqStructuralLiquidityReference.PostCompletionExtreme(fixture.Geometry, "cluster selection");
            sources = fixture.Source;
        }
        else
        {
            var fixture = LiquidityFixture.HumanCompletion(false);
            member = NasdaqStructuralLiquidityReference.HumanCollision(fixture.Completed, "collision selection");
            sources = fixture.Source;
        }
        var upstream = LiquidityFixture.Observation([member], effective: 40, observed: 41);
        var context = Context(sources, [upstream], 41);
        var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(new NasdaqHumanStructuralLiquidityObservationSelector()
            .Select(context, LiquidityFixture.Session(41)));
        var reference = new NasdaqLiquidityTakeReference.Structural(selection, member);
        var take = Take(reference, effective: LiquidityFixture.At(41).AddMinutes(30), observed: LiquidityFixture.At(42), eligible: LiquidityFixture.At(41));
        var result = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(sources, [upstream, take], 42), reference));
        var retained = ((NasdaqLiquidityTakeReference.Structural)result.Fact.Reference).Member;
        Assert.Same(member, retained);
        if (cluster) Assert.Equal(2, retained.ActiveExtremeGeometry!.MemberResolution.Selection.SupportingObservations.Count);
        else Assert.Equal(2, retained.HumanCollisionCompletion!.HumanPriceSelection.SupportingObservations.Count);
    }

    [Fact]
    public void MissingCanonicalSourceDoesNotConflictWithAValidDocumentedClaim()
    {
        var reference = SessionReference();
        var good = Take(reference);
        var quote = new HistoricalMarketPriceObservation(reference.Session.ProviderId, reference.Session.Symbol,
            LiquidityFixture.At(13).AddMinutes(30), 112, "quote", "source", 1);
        var unavailable = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(quote),
            LiquidityFixture.At(13), LiquidityFixture.At(14), "unresolvable canonical event");
        var result = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [good, unavailable]), reference));
        Assert.Same(good, Assert.Single(result.SupportingObservations));
        Assert.Same(unavailable, Assert.Single(result.UnavailableSourceObservations));
    }

    [Fact]
    public void SameTimeAuditVariantsHaveStableOrderAndRetainAllSourceLinks()
    {
        var context = Context(SessionSources, [], 13);
        var calculation = new NasdaqSessionLiquidityCalculator().Calculate(context);
        var firstReference = new NasdaqLiquidityTakeReference.SessionLevel(context, calculation,
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(13), "review:reference z");
        var secondReference = new NasdaqLiquidityTakeReference.SessionLevel(context, calculation,
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(13), "review:reference a");
        var a = Take(firstReference);
        var b = Take(secondReference);
        var forward = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [a, b]), firstReference));
        var reverse = Assert.IsType<NasdaqHumanLiquidityTakeSelection.Unique>(selector.Select(Context(SessionSources, [b, a]), firstReference));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(2, forward.SupportingObservations.Count);
        Assert.Same(firstReference, forward.SupportingObservations.Single(o => ReferenceEquals(o, a)).Reference);
        Assert.Same(secondReference, forward.SupportingObservations.Single(o => ReferenceEquals(o, b)).Reference);
    }

    [Fact]
    public void SessionBindingRejectsInventedOutputWrongCalculationContextAndBackdatedSelection()
    {
        var context = Context(SessionSources, [], 13);
        var actual = new NasdaqSessionLiquidityCalculator().Calculate(context);
        var unbound = new NasdaqSessionLiquidityCalculationResult(actual.Levels, actual.Reason);
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.SessionLevel(context, unbound,
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(13), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.SessionLevel(Context(SessionSources, [], 14), actual,
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(14), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqLiquidityTakeReference.SessionLevel(context, actual,
            NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, LiquidityFixture.At(12), "review"));
    }

    [Fact]
    public void CanonicalHumanAdapterIsRegisteredWithoutFullStrategyCoverage()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Equal(12, evaluators.Count);
        Assert.Single(evaluators, e => e.RuleId.Value == "NQ-LIQ-003");
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 2, false), (report.TotalRuleCount, report.RequiredRuleCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }

    private static NasdaqLiquidityTakeReference.SessionLevel SessionReference(NasdaqLiquidityTakeReference.SessionEndpoint endpoint = NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh)
    {
        var context = Context(SessionSources, [], 13);
        return new(context, new NasdaqSessionLiquidityCalculator().Calculate(context), endpoint, LiquidityFixture.At(13), "review:selected endpoint");
    }
    private static (NasdaqLiquidityTakeReference.Structural, NasdaqHumanStructuralLiquidityObservation, Candle[]) StructuralReference(bool human, bool low, decimal humanPrice = 21500)
    {
        var candle = LiquidityFixture.Candle(8);
        var member = human ? LiquidityFixture.Human(candle, low, humanPrice) : NasdaqStructuralLiquidityReference.OrdinaryTurn(
            low ? NasdaqHumanH4StructuralRole.HigherLow : NasdaqHumanH4StructuralRole.LowerHigh, [candle], "formula source");
        var upstream = LiquidityFixture.Observation([member]);
        var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(new NasdaqHumanStructuralLiquidityObservationSelector()
            .Select(Context([candle], [upstream], 13), LiquidityFixture.Session(13)));
        return (new(selection, member), upstream, [candle]);
    }
    private static NasdaqHumanLiquidityTakeObservation Take(NasdaqLiquidityTakeReference reference, decimal? price = null,
        DateTimeOffset? effective = null, DateTimeOffset? observed = null, DateTimeOffset? eligible = null, string source = "review:take", string eventId = "source:event") =>
        new(reference, new NasdaqHumanLiquidityTakeEvent.Documented(reference.Session.ProviderId, reference.Session.Symbol, eventId,
            price ?? reference.ReferencePrice + (reference.Side == NasdaqStructuralLiquiditySide.High ? 1 : -1),
            effective ?? LiquidityFixture.At(13).AddMinutes(30), "retained:authentic event record"),
            eligible ?? LiquidityFixture.At(13), observed ?? LiquidityFixture.At(14), source);
    private static StrategyReplayContext Context(Candle[] source, IStrategyReplayInputObservation[] observations, int hour = 14) =>
        LiquidityFixture.Context(source, observations, hour);
}
