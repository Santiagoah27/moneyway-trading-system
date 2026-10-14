using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqOptionalFvgM1Tests
{
    private static NasdaqHumanM5TriggerObservation Trigger()
    {
        var prior = M5Fixture.Five(M5Fixture.At(14));
        return new(M5Fixture.Take(false), new NasdaqHumanM5TriggerEvent.StructuralChange(
            M5Fixture.Five(M5Fixture.At(14, 10)), NasdaqHumanH4PermittedDirection.Buy, [prior], [prior], 100),
            M5Fixture.At(14, 10), "review:trigger");
    }
    private static NasdaqHumanM1CorrectiveRetracementObservation Pullback(NasdaqHumanM5TriggerObservation trigger) =>
        new(trigger, new([M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, 20))]),
            M5Fixture.At(14, 20), "review:pullback:no-required-FVG");
    private static NasdaqHumanM5FvgObservation Fvg(NasdaqHumanM5TriggerObservation trigger, int close = 10, int observed = 10) =>
        new(trigger, new([M5Fixture.Five(M5Fixture.At(14, close - 10)), M5Fixture.Five(M5Fixture.At(14, close - 5)),
            M5Fixture.Five(M5Fixture.At(14, close))]), M5Fixture.At(14, observed), "review:fvg");
    private static NasdaqHumanM5FvgQualityObservation Quality(NasdaqHumanM5FvgObservation fvg,
        NasdaqHumanM5FvgQualityDecision decision = NasdaqHumanM5FvgQualityDecision.Approved, int observed = 10) =>
        new(new(fvg, decision, fvg.EffectiveAtUtc, "Human review."), M5Fixture.At(14, observed), "review:quality");
    private static MultiTimeframeStrategyBacktestRun Run(NasdaqHumanM5TriggerObservation trigger, params IStrategyReplayInputObservation[] extra)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var inputs = M5Fixture.Inputs(trigger.Event.Direction, trigger.DecisiveTake).Concat([trigger]).Concat(extra).ToArray();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(), new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()),
            new(new())).Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }
    private static StrategyReplayContextObservation At(MultiTimeframeStrategyBacktestRun run, int minute) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, minute));
    private static RuleEvaluationResult Result(StrategyReplayContextObservation frame, string id = "NQ-M1-001") =>
        frame.Evaluations.Single(e => e.RuleId.Value == id).Result;

    [Fact]
    public void CleanAbsenceAllowsM1ReviewWithoutPassingOrFabricatingFvg()
    {
        var trigger = Trigger(); var frame = At(Run(trigger), 15);
        Assert.True(frame.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(frame));
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(frame, "NQ-FVG-002"));
        Assert.DoesNotContain(frame.RuleFacts, f => f.RuleId.Value is "NQ-FVG-001" or "NQ-FVG-002");
        Assert.NotEqual(StrategyVerdict.NoTrade, new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition, frame).Verdict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PullbackWithoutConfirmationHasTriggerAncestryAndNoSentinel(bool unreviewedFvg)
    {
        var trigger = Trigger(); var pullback = Pullback(trigger);
        var extra = unreviewedFvg ? new IStrategyReplayInputObservation[] { pullback, Fvg(trigger) } : [pullback];
        var frame = At(Run(trigger, extra), 30);
        Assert.Equal(RuleEvaluationResult.Passed, Result(frame));
        var fact = Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(frame.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-001").Fact);
        Assert.False(fact.HasFvgConfirmation); Assert.Null(fact.ApprovedQuality); Assert.Null(fact.Selection.Fact.ApprovedQuality);
        Assert.Equal(trigger.EffectiveAtUtc, fact.Trigger.Selection.Fact.EffectiveAtUtc);
    }

    [Fact]
    public void ApprovedSupportIsRetainedEvenForTriggerBoundInput()
    {
        var trigger = Trigger(); var fvg = Fvg(trigger); var quality = Quality(fvg);
        var frame = At(Run(trigger, Pullback(trigger), fvg, quality), 30);
        Assert.Equal(RuleEvaluationResult.Passed, Result(frame));
        var fact = Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(frame.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-001").Fact);
        Assert.True(fact.HasFvgConfirmation);
        Assert.Same(quality, fact.ApprovedQuality!.Selection.Fact);
        Assert.Contains("review:quality", frame.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference);
    }

    [Fact]
    public void BeforeTriggerCannotConstructPullback()
    {
        var candle = M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, 5));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1CorrectiveRetracementObservation(Trigger(), new([candle]), M5Fixture.At(14, 20), "early"));
    }

    [Fact]
    public void FutureFvgCannotChangeEarlierPullbackOrInjectFutureSupport()
    {
        var trigger = Trigger(); var pullback = Pullback(trigger);
        var baseline = At(Run(trigger, pullback), 20);
        var fvg = Fvg(trigger, 25, 30);
        var future = Run(trigger, pullback, fvg, Quality(fvg, observed: 30));
        var earlier = At(future, 20);
        Assert.Equal(Result(baseline), Result(earlier));
        Assert.Equal(baseline.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference,
            earlier.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference);
        Assert.Null(Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(earlier.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-001").Fact).ApprovedQuality);
    }

    [Theory]
    [InlineData("rejected", RuleEvaluationResult.Waiting)]
    [InlineData("fvg-conflict", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("quality-conflict", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("unavailable", RuleEvaluationResult.DataUnavailable)]
    [InlineData("invalid", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("rejected-replacement", RuleEvaluationResult.Waiting)]
    public void UnsafeSelectedEvidenceCannotUseAbsentPath(string kind, RuleEvaluationResult expected)
    {
        var trigger = Trigger(); var fvg = Fvg(trigger);
        var inputs = new List<IStrategyReplayInputObservation> { Pullback(trigger), fvg };
        if (kind == "rejected") inputs.Add(Quality(fvg, NasdaqHumanM5FvgQualityDecision.Rejected));
        if (kind == "fvg-conflict") inputs.Add(Fvg(trigger, 15, 15));
        if (kind == "quality-conflict") inputs.AddRange([Quality(fvg), Quality(fvg, NasdaqHumanM5FvgQualityDecision.Rejected)]);
        if (kind == "rejected-replacement") inputs.AddRange([Quality(fvg, NasdaqHumanM5FvgQualityDecision.Rejected), Fvg(trigger, 25, 25)]);
        if (kind == "invalid")
        {
            inputs.Remove(fvg);
            var candle = fvg.Event.Candle3;
            var mismatch = new MoneyWay.Domain.MarketData.Candle(candle.ProviderId, candle.Symbol, candle.Timeframe,
                candle.OpenTimeUtc, candle.CloseTimeUtc, 101, candle.High, candle.Low, candle.Close, candle.Volume);
            inputs.Add(new NasdaqHumanM5FvgObservation(trigger, new([fvg.Event.Candle1, fvg.Event.Candle2, mismatch]),
                fvg.ObservedAtUtc, "invalid source"));
        }
        if (kind == "unavailable")
        {
            inputs.Remove(fvg);
            var missing = new MoneyWay.Domain.MarketData.Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol,
                NasdaqHumanM5TriggerEvent.M5, M5Fixture.At(13, 11), M5Fixture.At(13, 16), 100, 110, 90, 100, null);
            inputs.Add(new NasdaqHumanM5FvgObservation(trigger, new([missing, M5Fixture.Five(M5Fixture.At(14, 5)),
                M5Fixture.Five(M5Fixture.At(14, 10))]), M5Fixture.At(14, 10), "missing source"));
        }
        var frame = At(Run(trigger, inputs.ToArray()), 30);
        Assert.Equal(expected, Result(frame));
        Assert.False(frame.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").EstablishesProgression);
    }
}
