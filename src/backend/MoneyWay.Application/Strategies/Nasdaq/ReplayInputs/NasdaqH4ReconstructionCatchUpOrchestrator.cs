using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Folds the visible, unconsumed H4 history from one existing reconstruction snapshot.</summary>
public sealed class NasdaqH4ReconstructionCatchUpOrchestrator
{
    private readonly NasdaqH4InvalidatedOriginEvidenceSnapshotReducer originReducer = new();
    private readonly NasdaqH4OppositeImpulseSnapshotReducer impulseReducer = new();
    private readonly NasdaqH4CorrectionSnapshotReducer correctionReducer = new();
    private readonly NasdaqH4CandidateSnapshotReducer candidateReducer = new();
    private readonly NasdaqH4RebuildPendingSnapshotReducer pendingReducer = new();
    private readonly NasdaqH4RebuiltTrackingSnapshotReducer trackingReducer = new();
    private readonly NasdaqH4BreakoutAwaitingCompletionReducer breakoutReducer = new();

    public NasdaqH4ReconstructionCatchUpResult CatchUp(
        NasdaqH4ReconstructionSnapshot snapshot,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (snapshot.StrategyId != context.StrategyId || snapshot.StrategyVersion != context.StrategyVersion
            || snapshot.ProviderId != context.ProviderId || snapshot.Symbol != context.Symbol
            || context.AsOfUtc < snapshot.MarketCursor.CloseTimeUtc)
            throw new ArgumentException("Replay context must match the closed snapshot and its strategy/market identity.", nameof(context));

        var current = snapshot;
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? originResolution = null;
        ReplayFrame? frame = null;
        var nextIndex = -1;

        while (true)
        {
            switch (current)
            {
                case NasdaqH4ReconstructionSnapshot.Completed completed:
                    return new NasdaqH4ReconstructionCatchUpResult.Completed(completed, Origin: originResolution);

                case NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin:
                    var origin = originReducer.Reduce(current, context);
                    switch (origin)
                    {
                        case NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing missing:
                            return new NasdaqH4ReconstructionCatchUpResult.OriginEvidenceMissing(missing);
                        case NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict conflict:
                            return new NasdaqH4ReconstructionCatchUpResult.OriginEvidenceConflict(conflict);
                        case NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved resolved:
                            EnsureSameCursor(current.MarketCursor, resolved.Snapshot.MarketCursor);
                            originResolution = resolved;
                            current = resolved.Snapshot;
                            continue;
                        default:
                            throw new InvalidOperationException("Unsupported origin evidence outcome.");
                    }

                case NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion:
                    var breakout = breakoutReducer.Reduce(current, context);
                    switch (breakout)
                    {
                        case NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing missing:
                            return new NasdaqH4ReconstructionCatchUpResult.BreakoutEvidenceMissing(missing, originResolution);
                        case NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict conflict:
                            return new NasdaqH4ReconstructionCatchUpResult.BreakoutEvidenceConflict(conflict, originResolution);
                        case NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed completed:
                            EnsureSameCursor(current.MarketCursor, completed.Snapshot.MarketCursor);
                            return new NasdaqH4ReconstructionCatchUpResult.Completed(
                                (NasdaqH4ReconstructionSnapshot.Completed)completed.Snapshot, completed, originResolution);
                        default:
                            throw new InvalidOperationException("Unsupported breakout evidence outcome.");
                    }
            }

            if (frame is null)
            {
                if (!context.TryGetFrame(NasdaqHumanOriginVertexObservation.H4, out frame) || frame is null)
                    throw new InvalidOperationException("The current replay context has no observable H4 series.");
                nextIndex = FindNextIndex(frame, current.MarketCursor);
            }

            if (nextIndex == frame.AvailableCandles.Count)
                return new NasdaqH4ReconstructionCatchUpResult.UpToDate(current, originResolution);

            var candle = frame.AvailableCandles[nextIndex];
            var previousCursor = current.MarketCursor;
            current = current switch
            {
                NasdaqH4ReconstructionSnapshot.OppositeImpulse => impulseReducer.Reduce(current, candle),
                NasdaqH4ReconstructionSnapshot.Correction => correctionReducer.Reduce(current, candle),
                NasdaqH4ReconstructionSnapshot.Candidate => candidateReducer.Reduce(current, candle),
                NasdaqH4ReconstructionSnapshot.RebuildPending => pendingReducer.Reduce(current, candle),
                NasdaqH4ReconstructionSnapshot.RebuiltTracking => trackingReducer.Reduce(current, candle),
                _ => throw new InvalidOperationException("Unsupported market-progression snapshot."),
            };
            if (!ReferenceEquals(current.MarketCursor, candle)
                || candle.OpenTimeUtc < previousCursor.CloseTimeUtc)
                throw new InvalidOperationException("An H4 reduction must consume exactly the next closed candle.");
            nextIndex++;
        }
    }

    private static int FindNextIndex(ReplayFrame frame, Candle cursor)
    {
        var candles = frame.AvailableCandles;
        for (var index = 0; index < candles.Count; index++)
        {
            var candidate = candles[index];
            if (candidate.OpenTimeUtc != cursor.OpenTimeUtc) continue;
            if (candidate.CloseTimeUtc != cursor.CloseTimeUtc
                || candidate.Open != cursor.Open || candidate.High != cursor.High
                || candidate.Low != cursor.Low || candidate.Close != cursor.Close
                || candidate.Volume != cursor.Volume)
                throw new InvalidOperationException("The H4 replay series disagrees with the structural market cursor.");
            return index + 1;
        }
        throw new InvalidOperationException("The H4 replay series does not contain the structural market cursor.");
    }

    private static void EnsureSameCursor(Candle before, Candle after)
    {
        if (before.OpenTimeUtc != after.OpenTimeUtc || before.CloseTimeUtc != after.CloseTimeUtc
            || before.ProviderId != after.ProviderId || before.Symbol != after.Symbol
            || before.Timeframe != after.Timeframe || before.Open != after.Open
            || before.High != after.High || before.Low != after.Low
            || before.Close != after.Close || before.Volume != after.Volume)
            throw new InvalidOperationException("An evidence-only transition cannot advance the H4 market cursor.");
    }
}
