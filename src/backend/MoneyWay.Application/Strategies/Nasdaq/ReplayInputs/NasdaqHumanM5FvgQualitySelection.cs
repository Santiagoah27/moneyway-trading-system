using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence selections only; unavailable named sources remain a dependency for the future evaluator.</summary>
public abstract class NasdaqHumanM5FvgQualitySelection
{
    private NasdaqHumanM5FvgQualitySelection(IEnumerable<NasdaqHumanM5FvgQualityObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHumanM5FvgQualityObservation>(unavailable.ToArray());
    public IReadOnlyList<NasdaqHumanM5FvgQualityObservation> UnavailableSourceObservations { get; }
    public sealed class Missing : NasdaqHumanM5FvgQualitySelection
    {
        internal Missing(IEnumerable<NasdaqHumanM5FvgQualityObservation> unavailable) : base(unavailable) { }
    }
    public sealed class Unique : NasdaqHumanM5FvgQualitySelection
    {
        internal Unique(IEnumerable<NasdaqHumanM5FvgQualityObservation> support, IEnumerable<NasdaqHumanM5FvgQualityObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5FvgQualityObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5FvgQualityObservation> SupportingObservations { get; }
        public NasdaqHumanM5FvgQualityObservation Fact => SupportingObservations[0];
    }
    public sealed class Conflict : NasdaqHumanM5FvgQualitySelection
    {
        internal Conflict(IEnumerable<NasdaqHumanM5FvgQualityObservation> support, IEnumerable<NasdaqHumanM5FvgQualityObservation> unavailable)
            : base(unavailable) => SupportingObservations = new ReadOnlyCollection<NasdaqHumanM5FvgQualityObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHumanM5FvgQualityObservation> SupportingObservations { get; }
    }
}
