using System.Text.Json;
using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Current canonical SL ownership plus the existing target source contract; never ranks important lows.</summary>
public sealed class NasdaqWickfillScenario2TakeProfitObservationSelector
{
    public NasdaqWickfillScenario2TakeProfitSelection Select(StrategyReplayContext context, NasdaqHumanWickfillScenario2StopLossRuleFact stop)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(stop);
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        if (latest is null || latest.AsOfUtc >= context.AsOfUtc || !stop.Session.Matches(context)) return new([], []);
        try { if (!ReferenceEquals(NasdaqHumanWickfillScenario2StopLossRuleFact.RequireOwner(latest), stop)) return new([], []); }
        catch (ArgumentException) { return new([], []); }
        var candidates = context.InputObservations.OfType<NasdaqWickfillScenario2TakeProfitObservation>()
            .Where(o => o.Session == stop.Session && o.EffectiveAtUtc <= context.AsOfUtc && o.ObservedAtUtc <= context.AsOfUtc
                && context.PriorObservations.Contains(o.StopOwner) && o.StopOwner.AsOfUtc < context.AsOfUtc
                && ReferenceEquals(o.StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation,
                    stop.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation)
                && o.StopLoss.Selection.Fact.SameFact(stop.Selection.Fact))
            .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal)
            .ThenBy(o => JsonSerializer.Serialize(o.Target.Reference, o.Target.Reference.GetType()), StringComparer.Ordinal).ToArray();
        var current = new NasdaqHumanWickfillScenario2StopLossObservationSelector().Select(context);
        if (current is not NasdaqHumanWickfillScenario2StopLossSelection.Unique selected
            || !selected.Fact.SameFact(stop.Selection.Fact)
            || selected.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact))) return new([], candidates);
        var valid = new List<NasdaqWickfillScenario2TakeProfitObservation>();
        var unavailable = new List<NasdaqWickfillScenario2TakeProfitObservation>();
        foreach (var observation in candidates)
        {
            var missing = false; var unusable = false;
            foreach (var source in observation.Target.Reference.Sources)
            {
                if (source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            if (missing) unavailable.Add(observation);
            else if (observation.Target.Reference is not NasdaqLiquidityTakeReference.Structural structural
                || structural.Selection.References.All(r => r.IsObservable(context, observation.EffectiveAtUtc))) valid.Add(observation);
        }
        return new(valid, unavailable);
    }
}

public sealed class NasdaqWickfillScenario2TakeProfitSelection
{
    internal NasdaqWickfillScenario2TakeProfitSelection(IEnumerable<NasdaqWickfillScenario2TakeProfitObservation> support,
        IEnumerable<NasdaqWickfillScenario2TakeProfitObservation> unavailable)
    { SupportingObservations = Array.AsReadOnly(support.ToArray()); UnavailableSourceObservations = Array.AsReadOnly(unavailable.ToArray()); }
    public IReadOnlyList<NasdaqWickfillScenario2TakeProfitObservation> SupportingObservations { get; }
    public IReadOnlyList<NasdaqWickfillScenario2TakeProfitObservation> UnavailableSourceObservations { get; }
    public bool IsUnique => SupportingObservations.Count > 0 && SupportingObservations.All(o => o.SameFact(SupportingObservations[0]));
    public bool IsConflict => SupportingObservations.Count > 0 && !IsUnique;
}
