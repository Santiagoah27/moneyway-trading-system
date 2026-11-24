using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum H4CausalStructureState { Unavailable, Provisional, Ambiguous, Confirmed }
public enum H4CausalContextState { Unavailable, NoQualifiedEvent, Ambiguous, Observed }
public enum H4ResearchDirection { Buy, Sell }
public enum H4CausalContextKind { Breakout, ImmediateFakeout, DelayedFakeout }

public sealed record H4CausalReference(H4PivotKind Side, decimal Price, DateTimeOffset ExtremeOpenUtc,
    DateTimeOffset ExtremeNominalCloseUtc, DateTimeOffset? ConfirmedAtUtc, string? ConfirmedPivotId,
    IReadOnlyList<Candle> EvidenceCandles)
{
    public TimeSpan? ConfirmationDelay => ConfirmedAtUtc - ExtremeNominalCloseUtc;
}

public sealed record H4CausalContextEvent(H4CausalContextKind Kind, H4ResearchDirection Direction,
    H4ConfirmedPivot Reference, DateTimeOffset ObservedAtUtc, Candle ObservationCandle,
    DateTimeOffset? OriginalBreakoutOpenUtc);

public sealed record H4CausalContextObservation(DateTimeOffset AsOfUtc, string ConfigurationId,
    H4MarketStructureObservation StructureEvidence, H4CausalStructureState StructureState,
    IReadOnlyList<H4CausalReference> ConfirmedReferences, IReadOnlyList<H4CausalReference> ProvisionalReferences,
    H4ResearchDirection? ConfirmedStructureDirection, H4CausalContextState ContextState,
    string ContextReason, H4ResearchDirection? ContextAlignedDirection,
    H4CandleContext? LatestClosedCandle, IReadOnlyList<Candle> ClosedSourceCandles, IReadOnlyList<H4CausalContextEvent> ContextEvents,
    IReadOnlyList<H4DelayedObservation> DelayedEvidence)
{
    public const string ResearchId = "EXP-NQ-H4-CAUSAL-CONTEXT-V2";
    public const string ResearchVersion = "2.0.0";
    public string CandidateId => ResearchId;
    public string Version => ResearchVersion;
    public IReadOnlyList<DateTimeOffset> SimultaneousExtremeOpensUtc => ConfirmedReferences.Concat(ProvisionalReferences)
        .GroupBy(r => r.ExtremeOpenUtc).Where(g => g.Select(r => r.Side).Distinct().Count() == 2).Select(g => g.Key).Order().ToArray();
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Separates documentary references, directional structure and current context using the V1 predicates.</summary>
public sealed class EvaluateH4CausalContextResearchUseCase
{
    public H4CausalContextObservation Execute(H4MarketStructureRequest request, int horizon)
    {
        ArgumentNullException.ThrowIfNull(request);
        var evidence = new EvaluateH4ContextCandidateUseCase().Execute(request with
        {
            DocumentedClosures = request.DocumentedClosures?.Where(c => c.EndUtc <= request.AsOfUtc).ToArray()
        }, horizon);
        var market = evidence.MarketStructure;
        var candles = request.Series.Candles.Where(c => c.OpenTimeUtc >= request.RequestedStartUtc &&
            c.CloseTimeUtc <= request.AsOfUtc).ToArray();
        var indexes = candles.Select((c, index) => (c.OpenTimeUtc, index)).ToDictionary(x => x.OpenTimeUtc, x => x.index);
        var left = request.Profile.LeftBars;
        var right = request.Profile.RightBars;
        var confirmed = market.ConfirmedPivots.Select(p =>
        {
            var index = indexes[p.ExtremeOpenUtc];
            return new H4CausalReference(p.Kind, p.Price, p.ExtremeOpenUtc, p.ExtremeNominalCloseUtc,
                p.ConfirmedAtUtc, p.CandidateId, Array.AsReadOnly(candles[(index - left)..(index + right + 1)]));
        }).ToArray();
        var sourceUnavailable = candles.Length == 0 || market.SourceGaps.Any(g => !g.DocumentedClosure);
        var pending = sourceUnavailable ? [] : Provisional(candles, left, right).ToArray();
        var structure = market.Classification switch
        {
            H4StructureClassification.Bullish or H4StructureClassification.Bearish => H4CausalStructureState.Confirmed,
            H4StructureClassification.Mixed or H4StructureClassification.Indeterminate => H4CausalStructureState.Ambiguous,
            _ when confirmed.Length > 0 || pending.Length > 0 => H4CausalStructureState.Provisional,
            _ => H4CausalStructureState.Unavailable
        };
        H4ResearchDirection? direction = market.Classification switch
        {
            H4StructureClassification.Bullish => H4ResearchDirection.Buy,
            H4StructureClassification.Bearish => H4ResearchDirection.Sell,
            _ => null
        };
        var latest = evidence.LatestClosedCandle;
        var events = ContextEvents(evidence, candles, indexes).OrderBy(e => e.ObservedAtUtc)
            .ThenBy(e => e.Kind).ThenBy(e => e.Reference.Kind).ToArray();
        var currentEvents = events.Where(e => e.ObservedAtUtc == latest?.ObservedAtUtc).ToArray();
        var (state, reason) = (sourceUnavailable, latest, currentEvents.Length) switch
        {
            (true, _, _) => (H4CausalContextState.Unavailable, market.ReasonCode),
            (_, { IntrabarOrderingUnknown: true }, _) => (H4CausalContextState.Ambiguous, "BothSidesPenetratedWithoutIntrabarOrdering"),
            (_, _, > 1) => (H4CausalContextState.Ambiguous, "MultipleQualifiedEventsAtTheSameClose"),
            (_, _, 1) => (H4CausalContextState.Observed, "SingleQualifiedEventAtLatestClose"),
            (_, { Upper.Reference: not null } or { Lower.Reference: not null }, _) =>
                (H4CausalContextState.NoQualifiedEvent, "ConfirmedReferenceWithoutNewQualifiedEventAtLatestClose"),
            _ => (H4CausalContextState.Unavailable, "NoConfirmedReferenceAtLatestCandleOpen")
        };
        var aligned = state == H4CausalContextState.Observed && currentEvents[0].Direction == direction ? direction : null;
        return new(request.AsOfUtc, evidence.ConfigurationId, market, structure, Array.AsReadOnly(confirmed),
            Array.AsReadOnly(pending), direction, state, reason, aligned, latest,
            Array.AsReadOnly(candles), Array.AsReadOnly(events), evidence.DelayedHistory);
    }

    private static IEnumerable<H4CausalReference> Provisional(Candle[] candles, int left, int right)
    {
        for (var i = Math.Max(left, candles.Length - right); i < candles.Length; i++)
        {
            var neighborhood = candles[(i - left)..];
            if (neighborhood.Zip(neighborhood.Skip(1)).Any(p => p.First.CloseTimeUtc != p.Second.OpenTimeUtc))
                continue;
            var extreme = candles[i];
            var neighbors = neighborhood.Where(c => c.OpenTimeUtc != extreme.OpenTimeUtc).ToArray();
            var owners = Array.AsReadOnly(neighborhood);
            if (neighbors.All(c => c.High < extreme.High))
                yield return new(H4PivotKind.High, extreme.High, extreme.OpenTimeUtc, extreme.CloseTimeUtc, null, null, owners);
            if (neighbors.All(c => c.Low > extreme.Low))
                yield return new(H4PivotKind.Low, extreme.Low, extreme.OpenTimeUtc, extreme.CloseTimeUtc, null, null, owners);
        }
    }

    private static IEnumerable<H4CausalContextEvent> ContextEvents(H4ContextObservation evidence, Candle[] candles,
        IReadOnlyDictionary<DateTimeOffset, int> indexes)
    {
        foreach (var frame in evidence.CandleHistory)
        {
            var candle = candles[indexes[frame.CandleOpenUtc]];
            foreach (var level in new[] { frame.Upper, frame.Lower })
            {
                var context = level.Outcome switch
                {
                    H4LevelOutcome.BullishBreakout => (H4CausalContextKind.Breakout, H4ResearchDirection.Buy),
                    H4LevelOutcome.BearishBreakout => (H4CausalContextKind.Breakout, H4ResearchDirection.Sell),
                    H4LevelOutcome.UpperImmediateRejection => (H4CausalContextKind.ImmediateFakeout, H4ResearchDirection.Sell),
                    H4LevelOutcome.LowerImmediateRejection => (H4CausalContextKind.ImmediateFakeout, H4ResearchDirection.Buy),
                    _ => ((H4CausalContextKind, H4ResearchDirection)?)null
                };
                if (context is { } observed && level.Reference is { } reference)
                    yield return new(observed.Item1, observed.Item2, reference, frame.ObservedAtUtc, candle, null);
            }
        }
        var byClose = candles.ToDictionary(c => c.CloseTimeUtc);
        foreach (var track in evidence.DelayedHistory.Where(x => x.Status == H4DelayedStatus.Invalidated))
            yield return new(H4CausalContextKind.DelayedFakeout,
                track.Reference.Kind == H4PivotKind.High ? H4ResearchDirection.Sell : H4ResearchDirection.Buy,
                track.Reference, track.ObservedAtUtc, byClose[track.ObservedAtUtc], track.BreakoutOpenUtc);
    }
}
