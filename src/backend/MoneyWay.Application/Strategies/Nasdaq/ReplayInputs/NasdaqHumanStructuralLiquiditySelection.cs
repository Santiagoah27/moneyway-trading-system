using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed evidence outcomes; no RuleStatus, winner or liquidity-take fact.</summary>
public abstract class NasdaqHumanStructuralLiquiditySelection
{
    private NasdaqHumanStructuralLiquiditySelection() { }

    public sealed class Missing : NasdaqHumanStructuralLiquiditySelection
    {
        internal Missing() { }
    }

    public sealed class Unique : NasdaqHumanStructuralLiquiditySelection
    {
        internal Unique(IEnumerable<NasdaqHumanStructuralLiquidityObservation> supporting)
        {
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanStructuralLiquidityObservation>(supporting.ToArray());
            References = SupportingObservations[0].References;
            EffectiveAtUtc = SupportingObservations[0].EffectiveAtUtc;
        }

        public IReadOnlyList<NasdaqStructuralLiquidityReference> References { get; }
        public DateTimeOffset EffectiveAtUtc { get; }
        public IReadOnlyList<NasdaqHumanStructuralLiquidityObservation> SupportingObservations { get; }
    }

    public sealed class Conflict : NasdaqHumanStructuralLiquiditySelection
    {
        internal Conflict(IEnumerable<NasdaqHumanStructuralLiquidityObservation> supporting) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanStructuralLiquidityObservation>(supporting.ToArray());

        public IReadOnlyList<NasdaqHumanStructuralLiquidityObservation> SupportingObservations { get; }
    }
}
