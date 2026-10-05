using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public abstract class NasdaqHumanM1RealignmentSelection
{
    private NasdaqHumanM1RealignmentSelection(IEnumerable<NasdaqHumanM1RealignmentObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanM1RealignmentObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHumanM1RealignmentObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHumanM1RealignmentSelection
    {
        internal Missing(IEnumerable<NasdaqHumanM1RealignmentObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHumanM1RealignmentSelection
    {
        internal Unique(IEnumerable<NasdaqHumanM1RealignmentObservation> support,
            IEnumerable<NasdaqHumanM1RealignmentObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHumanM1RealignmentObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM1RealignmentObservation> SupportingObservations { get; }
        public NasdaqHumanM1RealignmentObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHumanM1RealignmentSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanM1RealignmentObservation> alternatives,
            IEnumerable<NasdaqHumanM1RealignmentObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHumanM1RealignmentObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHumanM1RealignmentObservation> Alternatives { get; }
    }
}
