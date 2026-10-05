using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Explicit target selection, with no runtime TP decision or target ranking.</summary>
public abstract class NasdaqHumanTakeProfitSelection
{
    private NasdaqHumanTakeProfitSelection(IEnumerable<NasdaqHumanTakeProfitObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanTakeProfitObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanTakeProfitObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanTakeProfitSelection
    {
        internal Missing(IEnumerable<NasdaqHumanTakeProfitObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanTakeProfitSelection
    {
        internal Unique(IEnumerable<NasdaqHumanTakeProfitObservation> support,
            IEnumerable<NasdaqHumanTakeProfitObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanTakeProfitObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanTakeProfitObservation> SupportingObservations { get; }
        public NasdaqHumanTakeProfitObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanTakeProfitSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanTakeProfitObservation> alternatives,
            IEnumerable<NasdaqHumanTakeProfitObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanTakeProfitObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanTakeProfitObservation> Alternatives { get; }
    }
}
