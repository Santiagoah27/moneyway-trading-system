using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// Synthetic fixtures only: not authentic account, stop or execution evidence for Replay #2.
public sealed partial class NasdaqHumanWickfillScenario2StopLossTests
{
    private sealed record RiskSetup(Setup Setup, NasdaqHumanWickfillScenario2StopLossObservation Stop,
        StrategyReplayContextObservation StopOwner);
    private static RiskSetup BuildRiskSetup()
    {
        var s = BuildStopSetup(); var stop = Stop(s);
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, Frame(s, null, stop));
        var next = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(),
            new MoneyWayNasdaqReplayLifecyclePolicy(), raw, s.Owner.LifecycleProgression);
        var owner = raw.WithProgressions(next.WorkflowProgression, next.LifecycleProgression);
        Assert.True(owner.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId == SlRule).EstablishesProgression);
        return new(s, stop, owner);
    }
    private static NasdaqRiskExposure Exposure(decimal basis = 10000m, decimal loss = 100m, string currency = "USD") =>
        new(basis, currency, "synthetic:source-named-basis", loss, 110m, "synthetic:costs-included", "synthetic:approved-calculation");
    private static NasdaqWickfillScenario2RiskExposureObservation Risk(RiskSetup s, NasdaqRiskExposure? exposure = null,
        DateTimeOffset? observed = null, string source = "synthetic:planned-exposure") =>
        new(s.StopOwner, LiquidityFixture.Provider, LiquidityFixture.Symbol, exposure ?? Exposure(), At(13, 33), observed ?? At(13, 34), source);
    private static StrategyReplayContext RiskFrame(RiskSetup s, params IStrategyReplayInputObservation[] extra) =>
        FrameAfter14(s.Setup, new IStrategyReplayInputObservation[] { s.Stop }.Concat(extra).ToArray())
            .WithPriorObservations(s.Setup.History.Append(s.StopOwner));
    private static ReplayRuleEvaluationDecision EvaluateRisk(StrategyReplayContext c) => new MoneyWayNasdaqRiskExposureEvaluator().Evaluate(c);

    [Theory]
    [InlineData("10000", "100", true)]
    [InlineData("10000", "99.99999999999999999999999999", true)]
    [InlineData("10000", "100.00000000000000000000000001", false)]
    [InlineData("0.00000000000000000000000001", "0.0000000000000000000000000001", true)]
    [InlineData("0.0000000000000000000000000001", "79228162514264337593543950335", false)]
    [InlineData("79228162514264337593543950335", "792281625142643375935439503.35", true)]
    [InlineData("10000", "0", true)]
    public void Scenario2UsesExistingExactInclusiveComparisonWithoutSizingOrRounding(string basis, string loss, bool passed)
    {
        var s = BuildRiskSetup(); var input = Risk(s, Exposure(decimal.Parse(basis, CultureInfo.InvariantCulture), decimal.Parse(loss, CultureInfo.InvariantCulture)));
        var context = RiskFrame(s, input); var decision = EvaluateRisk(context);
        Assert.Equal(passed ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed, decision.Result);
        var fact = Assert.IsType<NasdaqWickfillScenario2RiskExposureRuleFact>(decision.Fact);
        Assert.Same(input.StopLoss, fact.StopLoss); Assert.Same(input.Exposure, fact.Exposure);
        Assert.Equal(0.01m, fact.Limit); Assert.Equal(passed, fact.IsWithinLimit);
        Assert.Null(fact.Exposure.Quantity); Assert.Null(fact.Exposure.QuantityUnit);
        Assert.Equal(At(13, 33), fact.EffectiveAtUtc); Assert.Equal(At(13, 34), input.ObservedAtUtc);
        Assert.Contains("SlFactReceived", decision.EvidenceReference);
        Assert.Contains("synthetic:approved-calculation", decision.EvidenceReference);
        var projection = new NasdaqHistoricalScenario2Risk(fact.Exposure, fact.StopLoss.StopPrice, fact.RiskRatio,
            fact.RatioExceedsDecimalRange, fact.Limit, fact.IsWithinLimit, fact.EffectiveAtUtc, input.ObservedAtUtc,
            NasdaqHistoricalTradeLifecycleSummary.Source(nameof(NasdaqWickfillScenario2RiskExposureObservation), input.ObservedAtUtc, input.SourceReference));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(projection));
        Assert.Equal(input.Exposure.ReportedMaximumLoss.ToString(CultureInfo.InvariantCulture), json.RootElement.GetProperty("ReportedMaximumLossText").GetString());
        Assert.Equal(input.Exposure.AccountBasisAmount.ToString(CultureInfo.InvariantCulture), json.RootElement.GetProperty("AccountBasisAmountText").GetString());
        Assert.Equal(JsonSerializer.Serialize(decision), JsonSerializer.Serialize(EvaluateRisk(context)));
        Assert.NotEqual(RuleEvaluationResult.Passed, new MoneyWayNasdaqHumanTakeProfitEvaluator().Evaluate(context).Result);
        if (basis == "0.0000000000000000000000000001") { Assert.Null(fact.RiskRatio); Assert.True(fact.RatioExceedsDecimalRange); }
    }

    [Fact]
    public void MissingAndSameFrameSlCannotUseExposureAndNoExposureRequiresHumanInput()
    {
        var s = BuildRiskSetup(); var input = Risk(s);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateRisk(RiskFrame(s)).Result);
        var withoutSl = RiskFrame(s, input).WithPriorObservations(s.Setup.History);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateRisk(withoutSl).Result);
        Assert.Null(EvaluateRisk(withoutSl).Fact);
        var sameFrame = Frame(s.Setup, null, s.Stop, input);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateRisk(sameFrame).Result);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("USTEC")]
    [InlineData("missing-exposure")]
    [InlineData("missing-source")]
    [InlineData("missing-time")]
    [InlineData("non-utc")]
    [InlineData("early-assessment")]
    [InlineData("reversed-time")]
    [InlineData("wrong-owner")]
    public void InvalidRiskEvidenceCannotBeAdmitted(string invalid)
    {
        var s = BuildRiskSetup();
        Assert.ThrowsAny<ArgumentException>(() => new NasdaqWickfillScenario2RiskExposureObservation(
            invalid == "wrong-owner" ? s.Setup.Owner : s.StopOwner,
            invalid == "provider" ? new("foreign") : LiquidityFixture.Provider,
            invalid == "USTEC" ? new("USTEC") : LiquidityFixture.Symbol,
            invalid == "missing-exposure" ? null! : Exposure(),
            invalid == "early-assessment" ? At(13, 31) : At(13, 33),
            invalid == "missing-time" ? default : invalid == "non-utc" ? At(13, 34).ToOffset(TimeSpan.FromHours(-5))
                : invalid == "reversed-time" ? At(13, 32) : At(13, 34),
            invalid == "missing-source" ? "" : "synthetic:risk"));
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("currency")]
    [InlineData("named-basis")]
    [InlineData("costs")]
    [InlineData("provenance")]
    [InlineData("quantity-unit")]
    [InlineData("quantity")]
    [InlineData("loss")]
    public void ExistingExposureContractDoesNotDefaultMissingFinancialFields(string invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NasdaqRiskExposure(invalid == "basis" ? 0 : 10000,
            invalid == "currency" ? "" : "USD", invalid == "named-basis" ? "" : "synthetic:named-basis",
            invalid == "loss" ? -1 : 100, 110, invalid == "costs" ? "" : "synthetic:costs",
            invalid == "provenance" ? "" : "synthetic:calculation", invalid is "quantity-unit" or "quantity" ? 1 : null,
            invalid == "quantity" ? "" : null));
    }

    [Fact]
    public void ConflictingCurrenciesAndExposureAreNotRankedButDuplicatesRetainProvenance()
    {
        var s = BuildRiskSetup(); var first = Risk(s);
        var other = Risk(s, Exposure(currency: "EUR"), source: "synthetic:other-currency");
        var conflict = EvaluateRisk(RiskFrame(s, first, other));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, conflict.Result); Assert.Null(conflict.Fact);
        var different = Risk(s, Exposure(loss: 101), source: "synthetic:other-loss");
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateRisk(RiskFrame(s, first, different)).Result);
        var compatible = Risk(s, observed: At(13, 35), source: "synthetic:second-review");
        var fact = Assert.IsType<NasdaqWickfillScenario2RiskExposureRuleFact>(EvaluateRisk(RiskFrame(s, compatible, first)).Fact);
        Assert.Equal(2, fact.Selection.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqWickfillScenario2RiskExposureObservation>)fact.Selection.SupportingObservations).Clear());
    }

    [Fact]
    public void ForeignCanonicalOwnerAndChangedOrUnavailableStopCannotReuseRisk()
    {
        var s = BuildRiskSetup(); var foreign = BuildRiskSetup();
        Assert.Null(EvaluateRisk(RiskFrame(s, Risk(foreign))).Fact);
        var risk = Risk(s);
        var conflict = EvaluateRisk(RiskFrame(s, risk, Stop(s.Setup, price: 117, source: "synthetic:later-revision")));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, conflict.Result); Assert.Null(conflict.Fact);
        var absent = Stop(s.Setup, reference: Reference(sources: [LiquidityFixture.Candle(10, high: 900)]));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateRisk(RiskFrame(s, risk, absent)).Result);
        var removed = FrameAfter14(s.Setup, risk).WithPriorObservations(s.Setup.History.Append(s.StopOwner));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateRisk(removed).Result);
    }

    [Fact]
    public void SameFrameCanonicalLiquidityInvalidationBlocksOtherwiseCompleteRisk()
    {
        var s = BuildRiskSetup(); var risk = Risk(s);
        var upstream = Frame(s.Setup, At(13, 5));
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new MoneyWay.Application.Strategies.Nasdaq.Liquidity.NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "synthetic:opposite-level");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "synthetic:opposite", reference.ReferencePrice - 1, At(14, 1), "synthetic:opposite-event"),
            upstream.AsOfUtc, At(14, 1), "synthetic:opposite-review");
        var selected = new NasdaqHumanRelevantLiquidityTakeObservation(opposite, At(14, 1), "synthetic:opposite-selected",
            s.Stop.PreEntryEligibility.Reversal.Applicability.Fact.InitialTake.Take);
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition,
            RiskFrame(s, risk, opposite, selected));
        Assert.Equal(RuleEvaluationResult.Failed, raw.Evaluations.Single(e => e.RuleId.Value == "NQ-RISK-001").Result);
        Assert.DoesNotContain(raw.RuleFacts, f => f.Fact is NasdaqWickfillScenario2RiskExposureRuleFact);
    }

    [Fact]
    public void FutureRiskAndStopRevisionsAndFutureCandleTailCannotRewriteEarlierFrame()
    {
        var s = BuildRiskSetup(); var risk = Risk(s); var atT = RiskFrame(s, risk);
        var expected = JsonSerializer.Serialize(EvaluateRisk(atT));
        var futureRisk = Risk(s, Exposure(loss: 101), At(15), "synthetic:future-risk");
        var futureStop = Stop(s.Setup, price: 117, observed: At(15), source: "synthetic:future-stop");
        Assert.Equal(expected, JsonSerializer.Serialize(EvaluateRisk(RiskFrame(s, risk, futureRisk, futureStop))));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateRisk(RiskFrame(s, futureRisk)).Result);
        var series = atT.AvailableTimeframes.Select(t =>
        {
            atT.TryGetFrame(t, out var frame); var last = frame!.AvailableCandles.Last();
            return new CandleSeries(atT.ProviderId, atT.Symbol, t, frame.AvailableCandles.Append(new Candle(atT.ProviderId,
                atT.Symbol, t, At(18), At(18).Add(last.CloseTimeUtc - last.OpenTimeUtc), 100, 120, 80, 100, null)));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == atT.AsOfUtc)
            {
                var expanded = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame,
                    s.Setup.Inputs.Concat([s.Stop, risk, futureRisk, futureStop])).WithPriorObservations(atT.PriorObservations);
                Assert.Equal(expected, JsonSerializer.Serialize(EvaluateRisk(expanded))); return;
            }
        Assert.Fail("Synthetic earlier frame not found.");
    }
}
