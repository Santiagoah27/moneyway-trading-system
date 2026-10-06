using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Documented execution selection, without any rule evaluation, aggregation or execution.</summary>
public abstract class NasdaqHistoricalObservedEntrySelection
{
    private NasdaqHistoricalObservedEntrySelection(IEnumerable<NasdaqHistoricalObservedEntryObservation> unavailable) =>
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHistoricalObservedEntryObservation>(unavailable.ToArray());

    public IReadOnlyList<NasdaqHistoricalObservedEntryObservation> UnavailableSourceObservations { get; }

    public sealed class Missing : NasdaqHistoricalObservedEntrySelection
    {
        internal Missing(IEnumerable<NasdaqHistoricalObservedEntryObservation> unavailable) : base(unavailable) { }
    }

    public sealed class Unique : NasdaqHistoricalObservedEntrySelection
    {
        internal Unique(IEnumerable<NasdaqHistoricalObservedEntryObservation> support,
            IEnumerable<NasdaqHistoricalObservedEntryObservation> unavailable) : base(unavailable) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHistoricalObservedEntryObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHistoricalObservedEntryObservation> SupportingObservations { get; }
        public NasdaqHistoricalObservedEntryObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHistoricalObservedEntrySelection
    {
        internal Conflict(IEnumerable<NasdaqHistoricalObservedEntryObservation> alternatives,
            IEnumerable<NasdaqHistoricalObservedEntryObservation> unavailable) : base(unavailable) =>
            Alternatives = new ReadOnlyCollection<NasdaqHistoricalObservedEntryObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHistoricalObservedEntryObservation> Alternatives { get; }
    }
}
