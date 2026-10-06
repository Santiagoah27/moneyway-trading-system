using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Closed documented-event selection with retained source and causal-order diagnostics.</summary>
public abstract class NasdaqHistoricalDocumentedExitSelection
{
    private NasdaqHistoricalDocumentedExitSelection(IEnumerable<NasdaqHistoricalDocumentedExitObservation> unavailable,
        IEnumerable<NasdaqHistoricalDocumentedExitObservation> unresolved)
    {
        UnavailableSourceObservations = new ReadOnlyCollection<NasdaqHistoricalDocumentedExitObservation>(unavailable.ToArray());
        UnresolvedCausalityObservations = new ReadOnlyCollection<NasdaqHistoricalDocumentedExitObservation>(unresolved.ToArray());
    }

    public IReadOnlyList<NasdaqHistoricalDocumentedExitObservation> UnavailableSourceObservations { get; }
    /// <summary>Visible equal-time assertions without authoritative causal proof; requires human validation.</summary>
    public IReadOnlyList<NasdaqHistoricalDocumentedExitObservation> UnresolvedCausalityObservations { get; }

    public sealed class Missing : NasdaqHistoricalDocumentedExitSelection
    {
        internal Missing(IEnumerable<NasdaqHistoricalDocumentedExitObservation> unavailable,
            IEnumerable<NasdaqHistoricalDocumentedExitObservation> unresolved) : base(unavailable, unresolved) { }
    }

    public sealed class Unique : NasdaqHistoricalDocumentedExitSelection
    {
        internal Unique(IEnumerable<NasdaqHistoricalDocumentedExitObservation> support,
            IEnumerable<NasdaqHistoricalDocumentedExitObservation> unavailable) : base(unavailable, []) =>
            SupportingObservations = new ReadOnlyCollection<NasdaqHistoricalDocumentedExitObservation>(support.ToArray());
        public IReadOnlyList<NasdaqHistoricalDocumentedExitObservation> SupportingObservations { get; }
        public NasdaqHistoricalDocumentedExitObservation Fact => SupportingObservations[0];
    }

    public sealed class Conflict : NasdaqHistoricalDocumentedExitSelection
    {
        internal Conflict(IEnumerable<NasdaqHistoricalDocumentedExitObservation> alternatives,
            IEnumerable<NasdaqHistoricalDocumentedExitObservation> unavailable,
            IEnumerable<NasdaqHistoricalDocumentedExitObservation> unresolved) : base(unavailable, unresolved) =>
            Alternatives = new ReadOnlyCollection<NasdaqHistoricalDocumentedExitObservation>(alternatives.ToArray());
        public IReadOnlyList<NasdaqHistoricalDocumentedExitObservation> Alternatives { get; }
    }
}
