using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence selections only; unavailable named sources remain a dependency for the future evaluator.</summary>
public abstract class NasdaqHumanM5TriggerSelection
{
    private NasdaqHumanM5TriggerSelection(IEnumerable<NasdaqHumanM5TriggerObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanM5TriggerObservation>(unavailable.ToArray());
    public IReadOnlyList<NasdaqHumanM5TriggerObservation> UnavailableSourceObservations { get; }
    public sealed class Missing : NasdaqHumanM5TriggerSelection
    {
        internal Missing(IEnumerable<NasdaqHumanM5TriggerObservation> unavailable) : base(unavailable) { }
    }
    public sealed class Unique : NasdaqHumanM5TriggerSelection
    {
        internal Unique(IEnumerable<NasdaqHumanM5TriggerObservation> support, IEnumerable<NasdaqHumanM5TriggerObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5TriggerObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5TriggerObservation> SupportingObservations { get; }
        public NasdaqHumanM5TriggerObservation Fact => SupportingObservations[0];
    }
    public sealed class Conflict : NasdaqHumanM5TriggerSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanM5TriggerObservation> support, IEnumerable<NasdaqHumanM5TriggerObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5TriggerObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5TriggerObservation> SupportingObservations { get; }
    }
}
