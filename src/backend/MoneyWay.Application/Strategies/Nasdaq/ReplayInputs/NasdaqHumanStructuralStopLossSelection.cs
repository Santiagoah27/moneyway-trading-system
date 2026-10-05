using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selected human SL evidence; no runtime rule status is inferred.</summary>
public abstract class NasdaqHumanStructuralStopLossSelection
{
    private NasdaqHumanStructuralStopLossSelection(IEnumerable<NasdaqHumanStructuralStopLossObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanStructuralStopLossObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanStructuralStopLossObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanStructuralStopLossSelection
    {
        internal Missing(IEnumerable<NasdaqHumanStructuralStopLossObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanStructuralStopLossSelection
    {
        internal Unique(IEnumerable<NasdaqHumanStructuralStopLossObservation> support,
            IEnumerable<NasdaqHumanStructuralStopLossObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanStructuralStopLossObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanStructuralStopLossObservation> SupportingObservations { get; }
        public NasdaqHumanStructuralStopLossObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanStructuralStopLossSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanStructuralStopLossObservation> alternatives,
            IEnumerable<NasdaqHumanStructuralStopLossObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanStructuralStopLossObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanStructuralStopLossObservation> Alternatives { get; }
    }
}
