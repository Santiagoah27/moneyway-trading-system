using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact prior SL ownership and current native source revalidation, without calculating risk or selecting prices.</summary>
public sealed class NasdaqWickfillScenario2RiskExposureObservationSelector
{
    public NasdaqWickfillScenario2RiskExposureSelection Select(StrategyReplayContext context, NasdaqHumanWickfillScenario2StopLossRuleFact stop)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(stop);
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        if (latest is null || latest.AsOfUtc >= context.AsOfUtc || !stop.Session.Matches(context)) return new([], []);
        try { if (!ReferenceEquals(NasdaqHumanWickfillScenario2StopLossRuleFact.RequireOwner(latest), stop)) return new([], []); }
        catch (ArgumentException) { return new([], []); }
        var candidates = context.InputObservations.OfType<NasdaqWickfillScenario2RiskExposureObservation>()
            .Where(o => o.Session == stop.Session && o.EffectiveAtUtc <= context.AsOfUtc && o.ObservedAtUtc <= context.AsOfUtc
                && context.PriorObservations.Contains(o.StopOwner) && o.StopOwner.AsOfUtc < context.AsOfUtc
                && ReferenceEquals(o.StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation,
                    stop.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation)
                && o.StopLoss.Selection.Fact.SameFact(stop.Selection.Fact))
            .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal).ToArray();
        var current = new NasdaqHumanWickfillScenario2StopLossObservationSelector().Select(context);
        if (current is not NasdaqHumanWickfillScenario2StopLossSelection.Unique selected
            || !selected.Fact.SameFact(stop.Selection.Fact)
            || selected.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
            return new([], candidates);
        return new(candidates, []);
    }
}

public sealed class NasdaqWickfillScenario2RiskExposureSelection
{
    internal NasdaqWickfillScenario2RiskExposureSelection(IEnumerable<NasdaqWickfillScenario2RiskExposureObservation> support,
        IEnumerable<NasdaqWickfillScenario2RiskExposureObservation> unavailable)
    { SupportingObservations = Array.AsReadOnly(support.ToArray()); UnavailableSourceObservations = Array.AsReadOnly(unavailable.ToArray()); }
    public IReadOnlyList<NasdaqWickfillScenario2RiskExposureObservation> SupportingObservations { get; }
    public IReadOnlyList<NasdaqWickfillScenario2RiskExposureObservation> UnavailableSourceObservations { get; }
    public bool IsUnique => SupportingObservations.Count > 0 && SupportingObservations.All(o => o.SameFact(SupportingObservations[0]));
    public bool IsConflict => SupportingObservations.Count > 0 && !IsUnique;
}
