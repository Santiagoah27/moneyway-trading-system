using System.Text.Json;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Candidate-only, current-boundary detector. Selects no swing and scans no earlier candles.</summary>
public sealed class NasdaqM5StrictCloseDetector
{
    internal static bool QualifiesReviewedTrigger(NasdaqHumanM5TriggerObservation trigger) => trigger.Event is not NasdaqHumanM5TriggerEvent.StructuralChange sc
        || new StructuralBodyCloseBreakCalculator().Evaluate(sc.ConfirmationCandle, sc.PriorSwingPrice,
            sc.Direction == NasdaqHumanH4PermittedDirection.Buy ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower).IsConfirmed;
    public sealed record Detection(NasdaqHumanM5TriggerObservation? Observation, RuleEvaluationResult UnconfirmedResult, string Evidence);
    public Detection Evaluate(StrategyReplayContext context, NasdaqLiquidityTakeRuleFact take)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(take);
        if (context.StrategyId != StrategyDefinitions.Nasdaq.MoneyWayNasdaqM5StrictCloseCandidate.Definition.StrategyId
            || context.StrategyVersion != StrategyDefinitions.Nasdaq.MoneyWayNasdaqM5StrictCloseCandidate.Version) throw new ArgumentException("Detector is isolated to the opt-in candidate.");
        if (!take.Session.Matches(context)) throw new ArgumentException("Exact setup identity required.");
        var refs = context.InputObservations.OfType<NasdaqValidatedM5SwingObservation>()
            .Where(r => r.Session.Matches(context) && r.DecisiveTake.SameFact(take.Take)).ToArray();
        var proof = JsonSerializer.Serialize(new { SwingSelections = refs, context.AsOfUtc, Scope = "CandidateCurrentClosedM5Boundary" });
        if (refs.Length == 0) return new(null, RuleEvaluationResult.HumanValidationRequired, proof);
        if (refs.Any(r => !r.SameSelection(refs[0]))) return new(null, RuleEvaluationResult.HumanValidationRequired, proof);
        var selected = refs[0];
        if (selected.Validation != NasdaqM5SwingValidation.Validated || (selected.Side == NasdaqStructuralLiquiditySide.High) != (take.Direction == NasdaqHumanH4PermittedDirection.Buy))
            return new(null, RuleEvaluationResult.HumanValidationRequired, proof);
        foreach (var source in selected.SourceCandles)
        {
            if (!context.TryGetFrame(source.Timeframe, out var frame) || !frame!.AvailableCandles.Any(c => NasdaqStructuralLiquidityReference.SameCandle(c, source)))
                return new(null, RuleEvaluationResult.DataUnavailable, proof);
        }
        var m5 = NasdaqHumanM5TriggerEvent.M5;
        if (!context.TryGetFrame(m5, out _)) return new(null, RuleEvaluationResult.DataUnavailable, proof);
        if (!context.WasUpdated(m5)) return new(null, RuleEvaluationResult.Waiting, proof);
        var direction = take.Direction == NasdaqHumanH4PermittedDirection.Buy ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower;
        var result = new StructuralBodyCloseBreakCalculator().EvaluateCurrentBoundary(context, m5, selected.Price, direction);
        if (result.Candle is null || result.Candle.CloseTimeUtc < selected.ObservedAtUtc || result.Candle.CloseTimeUtc < take.Take.ObservedAtUtc)
            return new(null, RuleEvaluationResult.Waiting, proof);
        return new(result.IsConfirmed ? NasdaqHumanM5TriggerObservation.Detected(refs, result.Candle, take.Direction, context.AsOfUtc) : null,
            RuleEvaluationResult.Waiting, JsonSerializer.Serialize(new { References = refs, Comparison = result, context.AsOfUtc }));
    }
}
