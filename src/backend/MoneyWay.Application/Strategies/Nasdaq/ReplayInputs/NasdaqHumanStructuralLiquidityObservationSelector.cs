using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Validates causal structural sources and selects exact sets without assigning rule results.</summary>
public sealed class NasdaqHumanStructuralLiquidityObservationSelector
{
    public NasdaqHumanStructuralLiquiditySelection Select(StrategyReplayContext context, NasdaqDemoSessionIdentity session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Matches(context)) return new NasdaqHumanStructuralLiquiditySelection.Missing();

        var comparer = Comparer<NasdaqHumanStructuralLiquidityObservation>.Create((a, b) => a.CompareFact(b));
        var supporting = context.InputObservations.OfType<NasdaqHumanStructuralLiquidityObservation>()
            .Where(item => item.Session == session && item.ObservedAtUtc <= context.AsOfUtc && item.EffectiveAtUtc <= context.AsOfUtc
                && item.References.All(reference => reference.IsObservable(context, item.EffectiveAtUtc)))
            .OrderBy(item => item.ObservedAtUtc).ThenBy(item => item.SourceReference, StringComparer.Ordinal)
            .ThenBy(item => item, comparer)
            .ThenBy(item => item.ProvenanceKey, StringComparer.Ordinal)
            .ToArray();
        if (supporting.Length == 0) return new NasdaqHumanStructuralLiquiditySelection.Missing();
        return supporting.All(item => item.CompareFact(supporting[0]) == 0)
            ? new NasdaqHumanStructuralLiquiditySelection.Unique(supporting)
            : new NasdaqHumanStructuralLiquiditySelection.Conflict(supporting);
    }
}
