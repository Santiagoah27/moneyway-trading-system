using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

/// <summary>Accepted synthetic Feature 28 records, with dependencies supplied by a single canonical run.</summary>
internal static class SyntheticNasdaqMentorSessionFixture
{
    private static T? Latest<T>(StrategyReplayContext context, string ruleId) where T : class, IReplayRuleFact =>
        context.PriorObservations.LastOrDefault()?.RuleFacts.Where(f => f.RuleId.Value == ruleId).Select(f => f.Fact).OfType<T>().SingleOrDefault();
    private static Candle Minute(int close) => M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute)
        .Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, close));

    internal static NasdaqMentorSessionEvidenceRecord[] Records(DateTimeOffset? exitObserved = null,
        NasdaqPreEntryEligibilityRuleFact? foreignEligibility = null, bool includeExit = true)
    {
        var direction = NasdaqHumanH4PermittedDirection.Buy;
        var take = M5Fixture.Take(false);
        var reference = M5Fixture.Five(M5Fixture.At(14));
        var trigger = new NasdaqHumanM5TriggerObservation(take,
            new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), direction,
                [reference], [reference], 100), M5Fixture.At(14, 10), "trigger:source");
        var fvg = new NasdaqHumanM5FvgObservation(trigger, new([reference, M5Fixture.Five(M5Fixture.At(14, 5)),
            M5Fixture.Five(M5Fixture.At(14, 10))]), M5Fixture.At(14, 10), "fvg:source");
        var quality = new NasdaqHumanM5FvgQualityObservation(new(fvg, NasdaqHumanM5FvgQualityDecision.Approved,
            M5Fixture.At(14, 10), "Synthetic approved review."), M5Fixture.At(14, 15), "quality:source");
        var pullback = new NasdaqHumanM1CorrectiveRetracementObservation(quality, new([Minute(20)]), M5Fixture.At(14, 20), "review:pullback");
        var records = M5Fixture.Inputs(direction, take).Concat([trigger, fvg, quality, pullback])
            .Select((o, i) => NasdaqMentorSessionEvidenceRecord.FromObservation($"synthetic:{i}", o)).ToList();
        records.Add(new("realignment", M5Fixture.At(14, 20), (c, _) =>
            Latest<NasdaqHumanM1CorrectiveRetracementRuleFact>(c, "NQ-M1-001") is { } fact
                ? new NasdaqHumanM1RealignmentObservation(fact, new(Minute(20), [Minute(20)], 99, direction),
                    M5Fixture.At(14, 20), "review:realignment", true) : null));
        records.Add(new("stop", M5Fixture.At(14, 30), (c, _) =>
            (foreignEligibility ?? Latest<NasdaqPreEntryEligibilityRuleFact>(c, "NQ-M1-003")) is { } fact
                ? new NasdaqHumanStructuralStopLossObservation(fact,
                    new(NasdaqM5ProtectionAnchorKind.HigherLow, [M5Fixture.Five(M5Fixture.At(14, 20))]), 89,
                    M5Fixture.At(14, 30), M5Fixture.At(14, 30), "review:stop") : null));
        records.Add(new("entry", M5Fixture.At(14, 40), (c, _) => Latest<NasdaqPreEntryEligibilityRuleFact>(c, "NQ-M1-003") is { } fact
            ? new NasdaqHistoricalObservedEntryObservation(new(fact, "synthetic:execution:1", 100,
                fact.EligibilityEffectiveAtUtc, "synthetic:entry:record"), M5Fixture.At(14, 40), "entry:review") : null));
        records.Add(new("target", M5Fixture.At(14, 40), (c, _) =>
        {
            if (Latest<NasdaqHumanStructuralStopLossRuleFact>(c, "NQ-SL-001") is not { } stop) return null;
            var referenceContext = M5Fixture.Context(M5Fixture.At(13, 15), []);
            var target = new NasdaqLiquidityTakeReference.SessionLevel(referenceContext,
                new NasdaqSessionLiquidityCalculator().Calculate(referenceContext), NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh,
                referenceContext.AsOfUtc, "synthetic:target:source");
            return new NasdaqHumanTakeProfitObservation(stop, new(target, target.ReferencePrice), M5Fixture.At(14, 40), M5Fixture.At(14, 40), "review:target");
        }));
        records.Add(new("risk", M5Fixture.At(14, 40), (c, _) =>
            Latest<NasdaqHumanStructuralStopLossRuleFact>(c, "NQ-SL-001") is { } stop
            ? new NasdaqRiskExposureObservation(stop.PreEntryEligibility, stop,
                new(10000, "USD", "synthetic balance", 100, 100, "synthetic costs", "synthetic:risk", 2, "contracts"),
                M5Fixture.At(14, 40), M5Fixture.At(14, 40), "risk:review") : null));
        var observed = exitObserved ?? M5Fixture.At(15, 5);
        if (includeExit) records.Add(new("exit", observed, (_, snapshot) => snapshot is null ? null :
            new NasdaqHistoricalDocumentedExitObservation(new(snapshot, "synthetic:exit:1", 105,
                M5Fixture.At(14, 20).AddSeconds(30), "synthetic:exit:record"), observed, "exit:review")));
        return records.ToArray();
    }

}
