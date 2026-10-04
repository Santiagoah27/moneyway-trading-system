using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence selections only; unavailable named sources remain a dependency for the future evaluator.</summary>
public abstract class NasdaqHumanM5FvgSelection
{
    private NasdaqHumanM5FvgSelection(IEnumerable<NasdaqHumanM5FvgObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanM5FvgObservation>(unavailable.ToArray());
    public IReadOnlyList<NasdaqHumanM5FvgObservation> UnavailableSourceObservations { get; }
    public sealed class Missing : NasdaqHumanM5FvgSelection
    {
        internal Missing(IEnumerable<NasdaqHumanM5FvgObservation> unavailable) : base(unavailable) { }
    }
    public sealed class Unique : NasdaqHumanM5FvgSelection
    {
        internal Unique(IEnumerable<NasdaqHumanM5FvgObservation> support, IEnumerable<NasdaqHumanM5FvgObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5FvgObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5FvgObservation> SupportingObservations { get; }
        public NasdaqHumanM5FvgObservation Fact => SupportingObservations[0];
    }
    public sealed class Conflict : NasdaqHumanM5FvgSelection
    {
        internal Conflict(IEnumerable<NasdaqHumanM5FvgObservation> support, IEnumerable<NasdaqHumanM5FvgObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5FvgObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5FvgObservation> SupportingObservations { get; }
    }
}
