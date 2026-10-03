using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence outcomes only. Unavailable named sources remain diagnostics for the future owning evaluator.</summary>
public abstract class NasdaqHumanLiquidityTakeSelection
{
    private NasdaqHumanLiquidityTakeSelection(IEnumerable<NasdaqHumanLiquidityTakeObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanLiquidityTakeObservation>(unavailable.ToArray());
    public IReadOnlyList<NasdaqHumanLiquidityTakeObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanLiquidityTakeSelection
    {
        internal Missing(IEnumerable<NasdaqHumanLiquidityTakeObservation> unavailable) : base(unavailable) { }
    }
    public sealed class Unique : NasdaqHumanLiquidityTakeSelection
    {
        internal Unique(IEnumerable<NasdaqHumanLiquidityTakeObservation> supporting, IEnumerable<NasdaqHumanLiquidityTakeObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanLiquidityTakeObservation>(supporting.ToArray());
        public IReadOnlyList<NasdaqHumanLiquidityTakeObservation> SupportingObservations { get; }
        public NasdaqHumanLiquidityTakeObservation Fact => SupportingObservations[0];
    }
    public sealed class Conflict : NasdaqHumanLiquidityTakeSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanLiquidityTakeObservation> supporting, IEnumerable<NasdaqHumanLiquidityTakeObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanLiquidityTakeObservation>(supporting.ToArray());
        public IReadOnlyList<NasdaqHumanLiquidityTakeObservation> SupportingObservations { get; }
    }
}
