namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Composes Feature-25 contact comparisons without rereading market data or deciding economic policy.</summary>
public sealed class NasdaqHistoricalTradeContactOrderResolver
{
    public NasdaqHistoricalTradeContactResolution Resolve(NasdaqHistoricalTradeSnapshot snapshot,
        IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, DateTimeOffset asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(contacts);
        if (asOfUtc.Offset != TimeSpan.Zero || asOfUtc < snapshot.AsOfUtc)
            throw new ArgumentException("Resolution must use UTC and cannot precede snapshot availability.", nameof(asOfUtc));
        var input = contacts.ToArray();
        if (input.Any(c => c is null || !ReferenceEquals(c.Snapshot, snapshot)))
            throw new ArgumentException("Every contact must belong to the exact supplied snapshot.", nameof(contacts));
        // Stable presentation is not causal sorting. Future assertions do not participate at this boundary.
        var visible = input.Where(c => c.ObservedAtUtc <= asOfUtc)
            .OrderBy(c => c.EvidenceWindow.EarliestPossibleUtc).ThenBy(c => c.EvidenceWindow.LatestPossibleUtc)
            .ThenBy(c => c.EvidenceWindow.EvidenceId, StringComparer.Ordinal).ThenBy(c => c.Role)
            .ThenBy(c => c.LevelPrice).ThenBy(c => c.ObservedAtUtc).ToArray();
        var pairs = new List<NasdaqHistoricalTradeContactPairOrder>();
        for (var left = 0; left < visible.Length; left++)
            for (var right = left + 1; right < visible.Length; right++)
                pairs.Add(new(visible[left], visible[right]));
        var after = visible.Where(c => c.EntryRelation == NasdaqHistoricalContactEntryRelation.AfterEntry).ToArray();
        var earliest = after.Where((candidate, index) => after.Where((_, other) => other != index)
            .All(other => candidate.CompareOrder(other) == NasdaqHistoricalContactOrder.Before)).SingleOrDefault();
        if (visible.Any(c => c.EntryRelation == NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary))
            return new NasdaqHistoricalTradeContactResolution.EntryBoundaryAmbiguous(snapshot, asOfUtc, visible, pairs, earliest);
        if (after.Length == 0)
            return new NasdaqHistoricalTradeContactResolution.NoRelevantContacts(snapshot, asOfUtc, visible, pairs);
        return earliest is null
            ? new NasdaqHistoricalTradeContactResolution.OrderingUnresolved(snapshot, asOfUtc, visible, pairs)
            : new NasdaqHistoricalTradeContactResolution.EarliestProvenContact(snapshot, asOfUtc, visible, pairs, earliest);
    }
}
