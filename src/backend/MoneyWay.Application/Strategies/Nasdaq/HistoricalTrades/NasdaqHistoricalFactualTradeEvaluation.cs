using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

public enum NasdaqHistoricalFactualTradeDiagnostic
{
    MissingDocumentedExit,
    ConflictingDocumentedExits,
    UnresolvedExitCausality,
    UnavailableExitSources,
    ContactEvidenceNotProvided,
    NoRelevantContacts,
    UnresolvedContactOrdering,
    EntryBoundaryAmbiguous,
    ContactExecutionComparisonNotDefined
}

/// <summary>Immutable factual evidence composition, never a strategy verdict or economic outcome.</summary>
public abstract class NasdaqHistoricalFactualTradeEvaluation
{
    private NasdaqHistoricalFactualTradeEvaluation(NasdaqHistoricalTradeSnapshot snapshot,
        NasdaqHistoricalDocumentedExitSelection exitSelection, DateTimeOffset asOfUtc,
        NasdaqHistoricalTradeContactResolution? contactResolution, IEnumerable<NasdaqHistoricalFactualTradeDiagnostic> diagnostics)
    {
        Snapshot = snapshot;
        ExitSelection = exitSelection;
        AsOfUtc = asOfUtc;
        ContactResolution = contactResolution;
        Diagnostics = new ReadOnlyCollection<NasdaqHistoricalFactualTradeDiagnostic>(diagnostics.Distinct().Order().ToArray());
    }

    public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    public NasdaqHistoricalDocumentedExitSelection ExitSelection { get; }
    public DateTimeOffset AsOfUtc { get; }
    public NasdaqHistoricalTradeContactResolution? ContactResolution { get; }
    public IReadOnlyList<NasdaqHistoricalFactualTradeDiagnostic> Diagnostics { get; }
    /// <summary>Factual review requirement; contact review does not invalidate a documented execution.</summary>
    public bool RequiresHumanValidation => Diagnostics.Any(d => d is NasdaqHistoricalFactualTradeDiagnostic.ConflictingDocumentedExits
        or NasdaqHistoricalFactualTradeDiagnostic.UnresolvedExitCausality or NasdaqHistoricalFactualTradeDiagnostic.UnresolvedContactOrdering
        or NasdaqHistoricalFactualTradeDiagnostic.EntryBoundaryAmbiguous or NasdaqHistoricalFactualTradeDiagnostic.ContactExecutionComparisonNotDefined);

    public sealed class Available : NasdaqHistoricalFactualTradeEvaluation
    {
        internal Available(NasdaqHistoricalTradeSnapshot snapshot, NasdaqHistoricalDocumentedExitSelection.Unique exitSelection,
            DateTimeOffset asOfUtc, NasdaqHistoricalTradeContactResolution? contacts,
            IEnumerable<NasdaqHistoricalFactualTradeDiagnostic> diagnostics) : base(snapshot, exitSelection, asOfUtc, contacts, diagnostics) { }
        public NasdaqHistoricalDocumentedExitObservation ExitObservation => ((NasdaqHistoricalDocumentedExitSelection.Unique)ExitSelection).Fact;
        public NasdaqHistoricalDocumentedExit DocumentedExit => ExitObservation.Exit;
    }

    public sealed class Unavailable : NasdaqHistoricalFactualTradeEvaluation
    {
        internal Unavailable(NasdaqHistoricalTradeSnapshot snapshot, NasdaqHistoricalDocumentedExitSelection exitSelection,
            DateTimeOffset asOfUtc, NasdaqHistoricalTradeContactResolution? contacts,
            IEnumerable<NasdaqHistoricalFactualTradeDiagnostic> diagnostics, string reason) : base(snapshot, exitSelection, asOfUtc, contacts, diagnostics) => Reason = reason;
        public string Reason { get; }
    }
}
