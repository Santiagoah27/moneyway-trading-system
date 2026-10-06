using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHistoricalFactualTradeEvaluationTests
{
    private static readonly NasdaqHistoricalTradeSnapshotAssembler Assembler = new();
    private static readonly NasdaqHistoricalTradeLevelContactCollector Collector = new();
    private static readonly NasdaqHistoricalTradeContactOrderResolver Resolver = new();
    private static readonly NasdaqHistoricalDocumentedExitObservationSelector Selector = new();
    private static readonly NasdaqHistoricalFactualTradeEvaluationComposer Composer = new();

    private static Candle Minute(int close) => M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute)
        .Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, close));

    private static IStrategyReplayInputObservation[] Inputs(NasdaqHumanH4PermittedDirection direction,
        params IStrategyReplayInputObservation[] extra)
    {
        var take = M5Fixture.Take(direction == NasdaqHumanH4PermittedDirection.Sell);
        var reference = M5Fixture.Five(M5Fixture.At(14));
        var trigger = new NasdaqHumanM5TriggerObservation(take,
            new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), direction,
                [reference], [reference], 100), M5Fixture.At(14, 10), "trigger:source");
        var fvg = new NasdaqHumanM5FvgObservation(trigger,
            new([M5Fixture.Five(M5Fixture.At(14)), M5Fixture.Five(M5Fixture.At(14, 5)),
                M5Fixture.Five(M5Fixture.At(14, 10))]), M5Fixture.At(14, 10), "fvg:source");
        var quality = new NasdaqHumanM5FvgQualityObservation(new(fvg,
            NasdaqHumanM5FvgQualityDecision.Approved, M5Fixture.At(14, 10), "Approved source review."),
            M5Fixture.At(14, 15), "quality:source");
        var pullback = new NasdaqHumanM1CorrectiveRetracementObservation(quality,
            new([Minute(20)]), M5Fixture.At(14, 20), "review:pullback");
        return M5Fixture.Inputs(direction, take).Concat([trigger, fvg, quality, pullback]).Concat(extra).ToArray();
    }

    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }

    private static (NasdaqPreEntryEligibilityRuleFact Fact, NasdaqHumanM1RealignmentObservation Realignment) Setup(
        NasdaqHumanH4PermittedDirection direction, decimal? swingLevel = null)
    {
        var pullback = Run(Inputs(direction)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().First();
        var realignment = new NasdaqHumanM1RealignmentObservation(pullback,
            new(Minute(20), [Minute(20)], swingLevel ?? (direction == NasdaqHumanH4PermittedDirection.Buy ? 99 : 101), direction),
            M5Fixture.At(14, 20), "review:realignment", pullbackBeforeRealignmentAtSameClose: true);
        var fact = Run(Inputs(direction, realignment)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().First();
        return (fact, realignment);
    }

    private static StrategyReplayContext Frame(NasdaqHumanH4PermittedDirection direction, DateTimeOffset time,
        NasdaqHumanM1RealignmentObservation realignment, params IStrategyReplayInputObservation[] extra)
    {
        var inputs = Inputs(direction, realignment).Concat(extra).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        return M5Fixture.Context(time, inputs).WithPriorObservations(history);
    }

    private static NasdaqHumanStructuralStopLossObservation Stop(NasdaqPreEntryEligibilityRuleFact fact,
        Candle? source = null, decimal? stopPrice = null, DateTimeOffset? observed = null, string reference = "review:stop")
    {
        var anchor = new NasdaqHumanM5ProtectionAnchor(fact.Direction == NasdaqHumanH4PermittedDirection.Buy
            ? NasdaqM5ProtectionAnchorKind.HigherLow : NasdaqM5ProtectionAnchorKind.LowerHigh,
            [source ?? M5Fixture.Five(M5Fixture.At(14, 20))]);
        return new(fact, anchor, stopPrice ?? (fact.Direction == NasdaqHumanH4PermittedDirection.Buy ? 89 : 111),
            M5Fixture.At(14, 30), observed ?? M5Fixture.At(14, 30), reference);
    }

    private static (NasdaqPreEntryEligibilityRuleFact Entry, NasdaqHumanStructuralStopLossRuleFact Sl,
        NasdaqHumanM1RealignmentObservation Realignment, NasdaqHumanStructuralStopLossObservation Stop) TargetSetup(
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy, decimal? swing = null, decimal? stopPrice = null)
    {
        var setup = Setup(direction, swing);
        var stop = Stop(setup.Fact, stopPrice: stopPrice);
        var sl = Run(Inputs(direction, setup.Realignment, stop)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().First();
        return (setup.Fact, sl, setup.Realignment, stop);
    }

    private static NasdaqHumanTakeProfitTarget SessionTarget(bool low = false, bool london = false,
        string source = "review:target source")
    {
        var context = M5Fixture.Context(M5Fixture.At(13, 15), []);
        var endpoint = london ? (low ? NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow : NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh)
            : (low ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            endpoint, context.AsOfUtc, source);
        return new(reference, reference.ReferencePrice);
    }

    private static NasdaqHumanTakeProfitObservation Target(NasdaqHumanStructuralStopLossRuleFact sl,
        NasdaqHumanTakeProfitTarget? target = null, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string source = "review:selected TP") => new(sl, target ?? SessionTarget(sl.Direction == NasdaqHumanH4PermittedDirection.Sell),
            effective ?? M5Fixture.At(15), observed ?? M5Fixture.At(15), source);

    private static NasdaqRiskExposureObservation Risk(NasdaqPreEntryEligibilityRuleFact entry,
        NasdaqHumanStructuralStopLossRuleFact sl, decimal loss = 100) => new(entry, sl,
            new(10000, "USD", "documented balance", loss, 100, "documented costs", "risk:calculation", 2, "contracts"),
            M5Fixture.At(15), M5Fixture.At(15), "risk:review");

    private static NasdaqHistoricalObservedEntryObservation Entry(NasdaqPreEntryEligibilityRuleFact eligibility,
        decimal price = 100, DateTimeOffset? observed = null, IEnumerable<Candle>? sources = null) => new(
            new(eligibility, "mentor:execution:1", price, eligibility.EligibilityEffectiveAtUtc, "entry:retained record",
                supportingCandles: sources), observed ?? M5Fixture.At(15), "entry:review");

    private static NasdaqHistoricalTradeSnapshot Snapshot(NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy,
        decimal? swing = null, DateTimeOffset? entryTime = null)
    {
        var setup = TargetSetup(direction, swing);
        var entry = entryTime is null ? Entry(setup.Entry) : new NasdaqHistoricalObservedEntryObservation(
            new(setup.Entry, "mentor:execution:1", 100, entryTime.Value, "entry:record"), entryTime.Value, "entry:review");
        var context = Frame(direction, entryTime ?? M5Fixture.At(15, 5), setup.Realignment,
            setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl), entry);
        return Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry)).Snapshot;
    }

    private static Candle Source(DateTimeOffset open, DateTimeOffset close, decimal high = 109, decimal low = 91,
        Timeframe? timeframe = null) => new(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            timeframe ?? M5Fixture.Minute, open, close, 100, high, low, 100, null);

    private static StrategyReplayContext Market(NasdaqHistoricalTradeSnapshot snapshot, IEnumerable<Candle> sources,
        DateTimeOffset? asOf = null, HistoricalMarketPriceObservation[]? prices = null)
    {
        var time = asOf ?? M5Fixture.At(15, 5);
        var candles = sources.Concat([Source(time.AddMinutes(-1), time)]).GroupBy(c => c.Timeframe)
            .Select(g => new CandleSeries(snapshot.Session.ProviderId, snapshot.Session.Symbol, g.Key,
                g.OrderBy(c => c.OpenTimeUtc))).ToArray();
        var cursor = new CanonicalMultiTimeframeReplayCursor(candles,
            new(snapshot.Session.ProviderId, snapshot.Session.Symbol, prices ?? []));
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().ExecuteCanonical(LiquidityFixture.Definition, frame);
        throw new InvalidOperationException("Requested boundary was not produced.");
    }

    private static NasdaqHistoricalDocumentedExitObservation Exit(NasdaqHistoricalTradeSnapshot snapshot,
        decimal price = 105, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string id = "mentor:close:1", string reference = "review:exit",
        NasdaqHistoricalEntryBeforeExitEvidence? proof = null, string? reason = null,
        NasdaqHistoricalDocumentedExitScope? scope = null, decimal? quantity = null, string? unit = null,
        IEnumerable<Candle>? sources = null, string record = "retained:close record") => new(
            new(snapshot, id, price, effective ?? snapshot.EntryEffectiveAtUtc.AddSeconds(30), record,
                proof, reason, scope, quantity, unit, sources), observed ?? M5Fixture.At(15, 5), reference);

    private static NasdaqHistoricalEntryBeforeExitEvidence Proof(NasdaqHistoricalTradeSnapshot snapshot,
        string evidence = "source:open-close relation", DateTimeOffset? observed = null,
        string reference = "retained:chronology", string exitId = "mentor:close:1") =>
        new(snapshot, exitId, evidence, observed ?? M5Fixture.At(15, 5), reference);

    private static StrategyReplayContext Context(NasdaqHistoricalTradeSnapshot snapshot,
        DateTimeOffset? time = null, params IStrategyReplayInputObservation[] inputs) =>
        WithInputs(Market(snapshot, [], time ?? snapshot.AsOfUtc), inputs);

    private static StrategyReplayContext WithInputs(StrategyReplayContext context, params IStrategyReplayInputObservation[] inputs)
    {
        var frames = context.AvailableTimeframes.ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes, frames), inputs).WithPriorObservations(context.PriorObservations);
    }

    private static NasdaqHistoricalFactualTradeEvaluation Compose(NasdaqHistoricalTradeSnapshot snapshot,
        StrategyReplayContext context, NasdaqHistoricalTradeContactResolution? contacts = null) =>
        Composer.Compose(snapshot, Selector.Select(context, snapshot), context.AsOfUtc, contacts);

    [Fact]
    public void UniquePreservesExactSnapshotExecutionSupportAndProvenance()
    {
        var snapshot = Snapshot();
        var proof = Proof(snapshot);
        var a = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, proof: proof,
            reason: "source manual close", scope: NasdaqHistoricalDocumentedExitScope.Partial, quantity: 3, unit: "documented contracts");
        var b = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, proof: proof,
            reason: "source manual close", scope: NasdaqHistoricalDocumentedExitScope.Partial, quantity: 3, unit: "documented contracts",
            reference: "second:review", record: "second:retained execution");
        var context = Context(snapshot, inputs: [a, b]);
        var selection = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(context, snapshot));
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Composer.Compose(snapshot, selection, context.AsOfUtc));
        Assert.Same(snapshot, result.Snapshot);
        Assert.Same(selection, result.ExitSelection);
        Assert.Same(a.Exit, result.DocumentedExit);
        Assert.Same(a, result.ExitObservation);
        Assert.Same(proof, result.DocumentedExit.EntryBeforeExitEvidence);
        Assert.Equal(2, selection.SupportingObservations.Count);
        Assert.Equal(a.Exit.ExitPrice, result.DocumentedExit.ExitPrice);
        Assert.Equal(a.Exit.ExitEffectiveAtUtc, result.DocumentedExit.ExitEffectiveAtUtc);
        Assert.Equal(a.ObservedAtUtc, result.ExitObservation.ObservedAtUtc);
        Assert.Equal(a.Exit.ExecutionId, result.DocumentedExit.ExecutionId);
        Assert.Equal(a.Exit.ExecutionSourceReference, result.DocumentedExit.ExecutionSourceReference);
        Assert.Equal(NasdaqHistoricalDocumentedExitScope.Partial, result.DocumentedExit.DocumentedScope);
        Assert.Equal(3, result.DocumentedExit.Quantity);
        Assert.Equal("documented contracts", result.DocumentedExit.QuantityUnit);
        Assert.False(result.RequiresHumanValidation);
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.ContactEvidenceNotProvided, result.Diagnostics);
    }

    [Theory]
    [InlineData("TakeProfit")]
    [InlineData("StopLoss")]
    public void ExplicitReasonAndFullScopeArePreservedAsSourceStatements(string reason)
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, reason: reason, scope: NasdaqHistoricalDocumentedExitScope.Full);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, Context(snapshot, inputs: [exit])));
        Assert.Equal(reason, result.DocumentedExit.DocumentedReason);
        Assert.Equal(NasdaqHistoricalDocumentedExitScope.Full, result.DocumentedExit.DocumentedScope);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PriceEqualityDoesNotInventReasonScopeOrQuantity(bool target)
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, price: target ? snapshot.TakeProfitPrice : snapshot.StopPrice);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, Context(snapshot, inputs: [exit])));
        Assert.Equal(exit.Exit.ExitPrice, result.DocumentedExit.ExitPrice);
        Assert.Null(result.DocumentedExit.DocumentedReason);
        Assert.Null(result.DocumentedExit.DocumentedScope);
        Assert.Null(result.DocumentedExit.Quantity);
    }

    [Fact]
    public void MissingPreservesAbsenceWithoutEconomicDisposition()
    {
        var snapshot = Snapshot();
        var context = Context(snapshot);
        var missing = Selector.Select(context, snapshot);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(Composer.Compose(snapshot, missing, context.AsOfUtc));
        Assert.Same(missing, result.ExitSelection);
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.MissingDocumentedExit, result.Diagnostics);
        Assert.False(result.RequiresHumanValidation);
    }

    [Fact]
    public void ConflictRetainsEveryAlternativeWithoutWinner()
    {
        var snapshot = Snapshot();
        var context = Context(snapshot, inputs: [Exit(snapshot), Exit(snapshot, price: 999)]);
        var selection = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(context, snapshot));
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(Composer.Compose(snapshot, selection, context.AsOfUtc));
        Assert.Same(selection, result.ExitSelection);
        Assert.Equal(2, selection.Alternatives.Count);
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.ConflictingDocumentedExits, result.Diagnostics);
        Assert.True(result.RequiresHumanValidation);
    }

    [Fact]
    public void EqualTimeWithoutProofRetainsExplicitHumanReviewDiagnostic()
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(Compose(snapshot, Context(snapshot, inputs: [exit])));
        Assert.Same(exit, Assert.Single(result.ExitSelection.UnresolvedCausalityObservations));
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.UnresolvedExitCausality, result.Diagnostics);
        Assert.True(result.RequiresHumanValidation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableSourceCannotBecomeSufficientEvenAlongsideUniqueSupport(bool usableAlso)
    {
        var snapshot = Snapshot();
        var unavailable = Exit(snapshot, sources: [Minute(20)]);
        var inputs = usableAlso ? new[] { unavailable, Exit(snapshot) } : [unavailable];
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(Compose(snapshot, Context(snapshot, inputs: inputs)));
        Assert.Same(unavailable, Assert.Single(result.ExitSelection.UnavailableSourceObservations));
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.UnavailableExitSources, result.Diagnostics);
        if (usableAlso) Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(result.ExitSelection);
        else Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(result.ExitSelection);
    }

    [Theory]
    [InlineData(NasdaqHistoricalTradeLevelRole.TakeProfit)]
    [InlineData(NasdaqHistoricalTradeLevelRole.StopLoss)]
    public void EarliestContactNeverSubstitutesForMissingExit(NasdaqHistoricalTradeLevelRole role)
    {
        var snapshot = Snapshot();
        var context = Market(snapshot, [Source(M5Fixture.At(15, 3), M5Fixture.At(15, 4),
            high: role == NasdaqHistoricalTradeLevelRole.TakeProfit ? 111 : 109,
            low: role == NasdaqHistoricalTradeLevelRole.StopLoss ? 89 : 91)]);
        var contacts = Resolver.Resolve(snapshot, Collector.Collect(context, snapshot), context.AsOfUtc);
        Assert.Equal(role, Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(contacts).FirstContact.Role);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(Compose(snapshot, context, contacts));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(result.ExitSelection);
        Assert.Same(contacts, result.ContactResolution);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ContactAndDocumentedReasonRemainIndependentWithoutInventedComparison(bool appearsCompatible)
    {
        var snapshot = Snapshot();
        var market = Market(snapshot, [Source(M5Fixture.At(15, 3), M5Fixture.At(15, 4),
            high: appearsCompatible ? 111 : 109, low: appearsCompatible ? 91 : 89)]);
        var contacts = Resolver.Resolve(snapshot, Collector.Collect(market, snapshot), market.AsOfUtc);
        var exit = Exit(snapshot, price: snapshot.TakeProfitPrice, reason: "TakeProfit", scope: NasdaqHistoricalDocumentedExitScope.Full);
        var context = WithInputs(market, exit);
        var before = JsonSerializer.Serialize(contacts);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, context, contacts));
        Assert.Same(exit.Exit, result.DocumentedExit);
        Assert.Same(contacts, result.ContactResolution);
        Assert.Equal(before, JsonSerializer.Serialize(contacts));
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.ContactExecutionComparisonNotDefined, result.Diagnostics);
        Assert.True(result.RequiresHumanValidation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbiguousContactsDoNotDowngradeDocumentedExecution(bool boundary)
    {
        var snapshot = Snapshot(entryTime: boundary ? M5Fixture.At(15, 5) : null);
        var source = boundary ? Source(M5Fixture.At(15, 4), M5Fixture.At(15, 5), high: 111, low: 89)
            : Source(M5Fixture.At(15, 3), M5Fixture.At(15, 4), high: 111, low: 89);
        var market = Market(snapshot, [source], asOf: M5Fixture.At(15, 6));
        var contacts = Resolver.Resolve(snapshot, Collector.Collect(market, snapshot), market.AsOfUtc);
        var exit = Exit(snapshot, observed: market.AsOfUtc);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, WithInputs(market, exit), contacts));
        Assert.Same(exit.Exit, result.DocumentedExit);
        Assert.Same(contacts, result.ContactResolution);
        Assert.Contains(boundary ? NasdaqHistoricalFactualTradeDiagnostic.EntryBoundaryAmbiguous
            : NasdaqHistoricalFactualTradeDiagnostic.UnresolvedContactOrdering, result.Diagnostics);
        Assert.True(result.RequiresHumanValidation);
    }

    [Fact]
    public void NoRelevantContactDoesNotEraseDocumentedExit()
    {
        var snapshot = Snapshot();
        var context = Context(snapshot, inputs: [Exit(snapshot)]);
        var contacts = Resolver.Resolve(snapshot, [], context.AsOfUtc);
        var result = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, context, contacts));
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.NoRelevantContacts, result.Diagnostics);
        Assert.Contains(NasdaqHistoricalFactualTradeDiagnostic.ContactExecutionComparisonNotDefined, result.Diagnostics);
    }

    [Fact]
    public void FutureObservationDoesNotChangeEarlierImmutableEvaluation()
    {
        var snapshot = Snapshot();
        var time = snapshot.AsOfUtc;
        var exit = Exit(snapshot, observed: time.AddMinutes(1));
        var earlier = Compose(snapshot, Context(snapshot, time, exit));
        var baseline = JsonSerializer.Serialize(earlier);
        Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(earlier);
        var later = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, Context(snapshot, time.AddMinutes(1), exit)));
        Assert.Same(exit.Exit, later.DocumentedExit);
        Assert.Equal(baseline, JsonSerializer.Serialize(earlier));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnotherSnapshotCannotRebindEvenAnEmptySelection(bool hasExit)
    {
        var a = Snapshot();
        var b = Snapshot();
        var selection = Selector.Select(Context(b, inputs: hasExit ? [Exit(b)] : []), b);
        Assert.Same(b, selection.Snapshot);
        Assert.Throws<ArgumentException>(() => Composer.Compose(a, selection, a.AsOfUtc));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureOrStaleSelectionCannotBeReusedAtAnotherBoundary(bool future)
    {
        var snapshot = Snapshot();
        var selectionTime = future ? snapshot.AsOfUtc.AddMinutes(1) : snapshot.AsOfUtc;
        var composeTime = future ? snapshot.AsOfUtc : snapshot.AsOfUtc.AddMinutes(1);
        var selection = Selector.Select(Context(snapshot, selectionTime), snapshot);
        Assert.Equal(selectionTime, selection.AsOfUtc);
        Assert.Throws<ArgumentException>(() => Composer.Compose(snapshot, selection, composeTime));
    }

    [Fact]
    public void WrongSnapshotOrFutureContactResolutionIsRejectedWithoutRewritingIt()
    {
        var snapshot = Snapshot();
        var context = Context(snapshot, inputs: [Exit(snapshot)]);
        var selection = Selector.Select(context, snapshot);
        var other = Snapshot();
        var wrong = Resolver.Resolve(other, [], context.AsOfUtc);
        var future = Resolver.Resolve(snapshot, [], context.AsOfUtc.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => Composer.Compose(snapshot, selection, context.AsOfUtc, wrong));
        Assert.Throws<ArgumentException>(() => Composer.Compose(snapshot, selection, context.AsOfUtc, future));
    }

    [Fact]
    public void InputOrderDoesNotChangeFactualFactsOrDiagnostics()
    {
        var snapshot = Snapshot();
        var a = Exit(snapshot, reference: "a:review");
        var b = Exit(snapshot, reference: "b:review");
        var first = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, Context(snapshot, inputs: [a, b])));
        var second = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(Compose(snapshot, Context(snapshot, inputs: [b, a])));
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(((NasdaqHistoricalDocumentedExitSelection.Unique)first.ExitSelection).SupportingObservations,
            ((NasdaqHistoricalDocumentedExitSelection.Unique)second.ExitSelection).SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalFactualTradeDiagnostic>)first.Diagnostics).Clear());
    }

    [Fact]
    public void InvalidAsOfAndNullInputsAreRejected()
    {
        var snapshot = Snapshot();
        var selection = Selector.Select(Context(snapshot), snapshot);
        Assert.Throws<ArgumentException>(() => Composer.Compose(snapshot, selection, snapshot.AsOfUtc.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Composer.Compose(snapshot, selection, snapshot.AsOfUtc.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentNullException>(() => Composer.Compose(null!, selection, snapshot.AsOfUtc));
        Assert.Throws<ArgumentNullException>(() => Composer.Compose(snapshot, null!, snapshot.AsOfUtc));
    }

    [Fact]
    public void ComposerOnlyAcceptsArtifactsAndDoesNotMutateSourcesOrAddEconomicSurface()
    {
        var snapshot = Snapshot(NasdaqHumanH4PermittedDirection.Sell);
        var context = Context(snapshot, inputs: [Exit(snapshot, price: snapshot.EntryPrice - 100)]);
        var selection = Selector.Select(context, snapshot);
        var baseline = JsonSerializer.Serialize(selection);
        var result = Composer.Compose(snapshot, selection, context.AsOfUtc);
        Assert.Equal(baseline, JsonSerializer.Serialize(selection));
        Assert.Same(snapshot.Entry, result.Snapshot.Entry);
        Assert.Same(snapshot.StopLoss, result.Snapshot.StopLoss);
        Assert.Same(snapshot.TakeProfit, result.Snapshot.TakeProfit);
        Assert.Same(snapshot.Risk, result.Snapshot.Risk);
        Assert.Empty(typeof(NasdaqHistoricalFactualTradeEvaluationComposer).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
        var parameters = typeof(NasdaqHistoricalFactualTradeEvaluationComposer).GetMethod("Compose")!.GetParameters();
        Assert.Equal(new[] { typeof(NasdaqHistoricalTradeSnapshot), typeof(NasdaqHistoricalDocumentedExitSelection),
            typeof(DateTimeOffset), typeof(NasdaqHistoricalTradeContactResolution) }, parameters.Select(p => p.ParameterType));
        foreach (var type in new[] { typeof(NasdaqHistoricalFactualTradeEvaluation.Available), typeof(NasdaqHistoricalFactualTradeEvaluation.Unavailable) })
        {
            Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(type.GetProperties(), p => p.Name is "Profit" or "Loss" or "Win" or "PnL" or "Points" or "Verdict" or "RMultiple");
        }
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }
}
