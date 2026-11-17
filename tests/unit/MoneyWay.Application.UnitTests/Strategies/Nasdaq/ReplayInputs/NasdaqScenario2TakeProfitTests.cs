using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// Synthetic targets only. None of these prices or reviews constitute authentic Replay #2 evidence.
public sealed partial class NasdaqHumanWickfillScenario2StopLossTests
{
    private static NasdaqHumanTakeProfitTarget SelectedLow(bool high = false, bool london = false)
    {
        var context = Frame(BuildStopSetup(), At(13, 5));
        var endpoint = london ? high ? NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh : NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow
            : high ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow;
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            endpoint, context.AsOfUtc, "synthetic:human-chosen-source");
        return new(reference, reference.ReferencePrice);
    }
    private static NasdaqWickfillScenario2TakeProfitObservation Tp(RiskSetup s, NasdaqHumanTakeProfitTarget? target = null,
        DateTimeOffset? observed = null, string source = "synthetic:human-important-low") =>
        new(s.StopOwner, target ?? SelectedLow(), At(13, 15), observed ?? At(13, 34), source);
    private static ReplayRuleEvaluationDecision EvaluateTp(StrategyReplayContext c) => new MoneyWayNasdaqHumanTakeProfitEvaluator().Evaluate(c);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HumanSelectedLowPassesUnderExactPriorSlWithoutRiskOrEntry(bool london)
    {
        var s = BuildRiskSetup(); var input = Tp(s, SelectedLow(london: london)); var context = RiskFrame(s, input);
        var d = EvaluateTp(context); Assert.Equal(RuleEvaluationResult.Passed, d.Result);
        var fact = Assert.IsType<NasdaqWickfillScenario2TakeProfitRuleFact>(d.Fact);
        Assert.Same(input.Target, fact.Target); Assert.Same(input.StopLoss, fact.StopLoss);
        Assert.Equal(fact.TargetReferencePrice, fact.TakeProfitPrice);
        Assert.Equal(At(13, 15), fact.EffectiveAtUtc); Assert.Equal(At(13, 34), input.ObservedAtUtc);
        Assert.Contains("synthetic:human-important-low", d.EvidenceReference); Assert.Contains("TargetSource", d.EvidenceReference);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateRisk(context).Result);
        Assert.Equal(JsonSerializer.Serialize(d), JsonSerializer.Serialize(EvaluateTp(context)));
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var evaluated = raw.Evaluations.Single(e => e.RuleId.Value == "NQ-TP-001");
        Assert.Equal(LiquidityFixture.Definition.Rules.Single(r => r.RuleId.Value == "NQ-TP-001").Sequence, evaluated.Sequence);
        Assert.Equal(context.AsOfUtc, evaluated.EvaluatedAtUtc); Assert.False(evaluated.IsRequired);
        var next = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(),
            new MoneyWayNasdaqReplayLifecyclePolicy(), raw, s.StopOwner.LifecycleProgression);
        Assert.True(next.WorkflowProgression.RuleEligibility.Single(e => e.RuleId.Value == "NQ-TP-001").EstablishesProgression);
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition, raw.WithProgressions(next.WorkflowProgression, next.LifecycleProgression));
        Assert.NotEqual(StrategyVerdict.Ready, outcome.Verdict);
        Assert.DoesNotContain(raw.RuleFacts, f => f.Fact is NasdaqHumanTakeProfitRuleFact);
    }

    [Fact]
    public void MissingSlSameFrameSlAndMissingHumanSelectionCannotPass()
    {
        var s = BuildRiskSetup(); var input = Tp(s);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateTp(RiskFrame(s)).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateTp(RiskFrame(s, input).WithPriorObservations(s.Setup.History)).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateTp(Frame(s.Setup, null, s.Stop, input)).Result);
        Assert.Throws<ArgumentException>(() => new NasdaqWickfillScenario2TakeProfitObservation(s.Setup.Owner, input.Target, At(13, 15), At(13, 34), "synthetic:wrong-owner"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqWickfillScenario2TakeProfitObservation(s.StopOwner, null!, At(13, 15), At(13, 34), "synthetic:missing-target"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanTakeProfitTarget(null!, 0));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanTakeProfitTarget(input.Target.Reference, input.Target.TakeProfitPrice + .00001m));
    }

    [Theory]
    [InlineData("direction")]
    [InlineData("source")]
    [InlineData("effective")]
    [InlineData("observed")]
    [InlineData("non-utc")]
    [InlineData("reversed")]
    public void InvalidTargetEvidenceIsRejectedWithoutDefaults(string invalid)
    {
        var s = BuildRiskSetup();
        Assert.ThrowsAny<ArgumentException>(() => new NasdaqWickfillScenario2TakeProfitObservation(s.StopOwner,
            SelectedLow(high: invalid == "direction"), invalid == "effective" ? default : At(13, 15),
            invalid == "observed" ? default : invalid == "non-utc" ? At(13, 34).ToOffset(TimeSpan.FromHours(-5))
                : invalid == "reversed" ? At(13, 14) : At(13, 34), invalid == "source" ? "" : "synthetic:target"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ForeignNativeProviderOrUstecCannotSupplyTarget(bool provider)
    {
        var s = BuildRiskSetup(); var native = Frame(s.Setup, At(13, 5));
        var feed = provider ? new MarketDataProviderId("foreign") : native.ProviderId;
        var symbol = provider ? native.Symbol : new MarketSymbol("USTEC");
        var series = native.AvailableTimeframes.Select(t =>
        {
            native.TryGetFrame(t, out var f);
            return new CandleSeries(feed, symbol, t, f!.AvailableCandles.Select(c => new Candle(feed, symbol, t,
                c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, null)));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var f)) if (f!.AsOfUtc == native.AsOfUtc)
        {
            var foreign = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, f);
            var reference = new NasdaqLiquidityTakeReference.SessionLevel(foreign, new NasdaqSessionLiquidityCalculator().Calculate(foreign),
                NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow, foreign.AsOfUtc, "synthetic:foreign-reference");
            Assert.Throws<ArgumentException>(() => Tp(s, new(reference, reference.ReferencePrice))); return;
        }
        Assert.Fail("Synthetic foreign frame missing.");
    }

    [Fact]
    public void ConflictsDuplicatesForeignOwnersAndChangedStopRetainSafeStatuses()
    {
        var s = BuildRiskSetup(); var first = Tp(s); var other = Tp(s, SelectedLow(london: true), source: "synthetic:other-selection");
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateTp(RiskFrame(s, first, other)).Result);
        var duplicate = Tp(s, observed: At(13, 35), source: "synthetic:second-review");
        var fact = Assert.IsType<NasdaqWickfillScenario2TakeProfitRuleFact>(EvaluateTp(RiskFrame(s, duplicate, first)).Fact);
        Assert.Equal(2, fact.Selection.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqWickfillScenario2TakeProfitObservation>)fact.Selection.SupportingObservations).Clear());
        Assert.Null(EvaluateTp(RiskFrame(s, Tp(BuildRiskSetup()))).Fact);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateTp(RiskFrame(s, first, Stop(s.Setup, price: 117, source: "synthetic:later-sl"))).Result);
        var unavailableStop = Stop(s.Setup, reference: Reference(sources: [LiquidityFixture.Candle(10, high: 900)]));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateTp(RiskFrame(s, first, unavailableStop)).Result);
    }

    private static NasdaqHumanTakeProfitTarget StructuralLow(StrategyReplayContext context, decimal price, Candle? source = null)
    {
        var member = LiquidityFixture.Human(source ?? LiquidityFixture.Candle(8), true, price);
        var observation = new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(At(13)), [member], At(12), At(13), "synthetic:reviewed-structure");
        var series = context.AvailableTimeframes.Select(t =>
        {
            context.TryGetFrame(t, out var frame); var candles = frame!.AvailableCandles.AsEnumerable();
            if (source is not null && source.Timeframe == t && candles.All(c => c.OpenTimeUtc != source.OpenTimeUtc)) candles = candles.Append(source);
            return new CandleSeries(context.ProviderId, context.Symbol, t, candles.OrderBy(c => c.OpenTimeUtc));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame)) if (frame!.AsOfUtc == context.AsOfUtc)
        {
            var upstream = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, [observation]);
            var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(
                new NasdaqHumanStructuralLiquidityObservationSelector().Select(upstream, observation.Session));
            return new(new NasdaqLiquidityTakeReference.Structural(selection, member), price);
        }
        throw new InvalidOperationException("Synthetic structural selection frame missing.");
    }

    [Fact]
    public void ReviewedStructuralLowPreservesExactDecimalAndMissingSourcesCannotPass()
    {
        var s = BuildRiskSetup(); var context = RiskFrame(s);
        var value = decimal.Parse("77.123456789012345678901234567", CultureInfo.InvariantCulture);
        var fact = Assert.IsType<NasdaqWickfillScenario2TakeProfitRuleFact>(EvaluateTp(RiskFrame(s, Tp(s, StructuralLow(context, value)))).Fact);
        Assert.Equal(value, fact.TakeProfitPrice); Assert.Equal(value, fact.TargetReferencePrice);
        var absent = Tp(s, StructuralLow(context, value, LiquidityFixture.Candle(-10)));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateTp(RiskFrame(s, absent)).Result);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateTp(RiskFrame(s, Tp(s), absent)).Result);
    }

    [Fact]
    public void CurrentCanonicalTerminalTakeBlocksOtherwiseValidTp()
    {
        var s = BuildRiskSetup(); var upstream = Frame(s.Setup, At(13, 5));
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "synthetic:opposite-level");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "synthetic:opposite", reference.ReferencePrice - 1, At(14, 1), "synthetic:opposite-event"),
            upstream.AsOfUtc, At(14, 1), "synthetic:opposite-review");
        var selected = new NasdaqHumanRelevantLiquidityTakeObservation(opposite, At(14, 1), "synthetic:opposite-selected",
            s.Stop.PreEntryEligibility.Reversal.Applicability.Fact.InitialTake.Take);
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition,
            RiskFrame(s, Tp(s), opposite, selected));
        Assert.Equal(RuleEvaluationResult.Failed, raw.Evaluations.Single(e => e.RuleId.Value == "NQ-TP-001").Result);
        Assert.DoesNotContain(raw.RuleFacts, f => f.Fact is NasdaqWickfillScenario2TakeProfitRuleFact);
    }

    [Fact]
    public void FutureTargetAndStopRevisionsAndFutureCandleTailCannotRewriteFrozenTp()
    {
        var s = BuildRiskSetup(); var input = Tp(s); var atT = RiskFrame(s, input);
        var expected = JsonSerializer.Serialize(EvaluateTp(atT));
        var future = Tp(s, SelectedLow(london: true), At(15), "synthetic:future-target");
        var stop = Stop(s.Setup, price: 117, observed: At(15), source: "synthetic:future-stop");
        Assert.Equal(expected, JsonSerializer.Serialize(EvaluateTp(RiskFrame(s, input, future, stop))));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateTp(RiskFrame(s, future)).Result);
        var series = atT.AvailableTimeframes.Select(t =>
        {
            atT.TryGetFrame(t, out var f); var last = f!.AvailableCandles.Last();
            return new CandleSeries(atT.ProviderId, atT.Symbol, t, f.AvailableCandles.Append(new Candle(atT.ProviderId, atT.Symbol, t,
                At(18), At(18).Add(last.CloseTimeUtc - last.OpenTimeUtc), 100, 120, 80, 100, null)));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame)) if (frame!.AsOfUtc == atT.AsOfUtc)
        {
            var expanded = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame,
                s.Setup.Inputs.Concat([s.Stop, input, future, stop])).WithPriorObservations(atT.PriorObservations);
            Assert.Equal(expected, JsonSerializer.Serialize(EvaluateTp(expanded))); return;
        }
        Assert.Fail("Synthetic earlier TP frame missing.");
    }
}
