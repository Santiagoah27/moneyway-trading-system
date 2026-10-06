namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Composes already-produced canonical artifacts; no observation selection or market scanning.</summary>
public sealed class NasdaqHistoricalFactualTradeEvaluationComposer
{
    public NasdaqHistoricalFactualTradeEvaluation Compose(NasdaqHistoricalTradeSnapshot snapshot,
        NasdaqHistoricalDocumentedExitSelection exitSelection, DateTimeOffset asOfUtc,
        NasdaqHistoricalTradeContactResolution? contactResolution = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(exitSelection);
        if (asOfUtc.Offset != TimeSpan.Zero || asOfUtc < snapshot.AsOfUtc)
            throw new ArgumentException("Factual composition must use UTC and cannot precede snapshot availability.", nameof(asOfUtc));
        if (!ReferenceEquals(exitSelection.Snapshot, snapshot) || exitSelection.AsOfUtc != asOfUtc)
            throw new ArgumentException("Exit selection must belong to the exact snapshot and current composition boundary.", nameof(exitSelection));
        if (contactResolution is not null && (!ReferenceEquals(contactResolution.Snapshot, snapshot) || contactResolution.AsOfUtc > asOfUtc))
            throw new ArgumentException("Contact diagnostics must belong to this snapshot and be observable by the composition boundary.", nameof(contactResolution));

        var diagnostics = new List<NasdaqHistoricalFactualTradeDiagnostic>();
        if (exitSelection is NasdaqHistoricalDocumentedExitSelection.Missing)
            diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.MissingDocumentedExit);
        if (exitSelection is NasdaqHistoricalDocumentedExitSelection.Conflict)
            diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.ConflictingDocumentedExits);
        if (exitSelection.UnresolvedCausalityObservations.Count > 0)
            diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.UnresolvedExitCausality);
        if (exitSelection.UnavailableSourceObservations.Count > 0)
            diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.UnavailableExitSources);
        switch (contactResolution)
        {
            case null: diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.ContactEvidenceNotProvided); break;
            case NasdaqHistoricalTradeContactResolution.NoRelevantContacts:
                diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.NoRelevantContacts); break;
            case NasdaqHistoricalTradeContactResolution.OrderingUnresolved:
                diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.UnresolvedContactOrdering); break;
            case NasdaqHistoricalTradeContactResolution.EntryBoundaryAmbiguous:
                diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.EntryBoundaryAmbiguous); break;
        }
        // No source-defined execution/contact comparator exists. Preserve both domains for human review.
        if (contactResolution is not null && exitSelection is NasdaqHistoricalDocumentedExitSelection.Unique)
            diagnostics.Add(NasdaqHistoricalFactualTradeDiagnostic.ContactExecutionComparisonNotDefined);
        if (exitSelection is NasdaqHistoricalDocumentedExitSelection.Unique unique
            && exitSelection.UnavailableSourceObservations.Count == 0 && exitSelection.UnresolvedCausalityObservations.Count == 0)
            return new NasdaqHistoricalFactualTradeEvaluation.Available(snapshot, unique, asOfUtc, contactResolution, diagnostics);
        return new NasdaqHistoricalFactualTradeEvaluation.Unavailable(snapshot, exitSelection, asOfUtc, contactResolution, diagnostics,
            "A unique causally resolved documented exit with available required sources is needed.");
    }
}
