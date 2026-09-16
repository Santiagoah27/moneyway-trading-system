using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports correction-turn membership and semantic disposition after a terminal transition,
/// or an explicitly established normal pre-start context.
/// </summary>
public sealed class CorrectionTurnLifecycleResult
{
    internal CorrectionTurnLifecycleResult(
        IEnumerable<Candle> resultingTurnCandles,
        bool isCorrectionTurnActive,
        bool wasPreviousTurnTerminated,
        CorrectionTurnCurrentCandleMembership currentCandleMembership,
        CorrectionTurnLifecycleDisposition disposition,
        Candle? lastProcessedCandle)
    {
        ArgumentNullException.ThrowIfNull(resultingTurnCandles);
        if (!Enum.IsDefined(currentCandleMembership))
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentCandleMembership),
                currentCandleMembership,
                "The current-candle correction-turn membership is not supported.");
        }

        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "The correction-turn lifecycle disposition is not supported.");
        }

        var snapshot = resultingTurnCandles.ToArray();
        if (snapshot.Any(candle => candle is null))
        {
            throw new ArgumentException("Resulting correction-turn candles cannot contain null elements.", nameof(resultingTurnCandles));
        }

        if (isCorrectionTurnActive != (disposition == CorrectionTurnLifecycleDisposition.ActiveCorrection)
            || isCorrectionTurnActive != (currentCandleMembership != CorrectionTurnCurrentCandleMembership.NoCorrectionTurn))
        {
            throw new ArgumentException("Correction-turn activity must match disposition and current-candle membership.", nameof(isCorrectionTurnActive));
        }

        if (currentCandleMembership == CorrectionTurnCurrentCandleMembership.ExistingTurn && wasPreviousTurnTerminated
            || disposition == CorrectionTurnLifecycleDisposition.StructureInvalidated && !wasPreviousTurnTerminated)
        {
            throw new ArgumentException("Previous-turn termination must match current-candle membership.", nameof(wasPreviousTurnTerminated));
        }

        if (isCorrectionTurnActive != (snapshot.Length > 0))
        {
            throw new ArgumentException("Correction-turn activity must match candle membership.", nameof(resultingTurnCandles));
        }

        ResultingTurnCandles = new ReadOnlyCollection<Candle>(snapshot);
        IsCorrectionTurnActive = isCorrectionTurnActive;
        WasPreviousTurnTerminated = wasPreviousTurnTerminated;
        CurrentCandleMembership = currentCandleMembership;
        Disposition = disposition;
        LastProcessedCandle = lastProcessedCandle;
    }

    /// <summary>Represents a valid structural context awaiting its first correction without a prior reset event.</summary>
    public static CorrectionTurnLifecycleResult CreateAwaitingCorrectionStart() =>
        new([], false, false, CorrectionTurnCurrentCandleMembership.NoCorrectionTurn, CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, null);

    public IReadOnlyList<Candle> ResultingTurnCandles { get; }

    public bool IsCorrectionTurnActive { get; }

    public bool WasPreviousTurnTerminated { get; }

    public CorrectionTurnCurrentCandleMembership CurrentCandleMembership { get; }

    public CorrectionTurnLifecycleDisposition Disposition { get; }

    internal Candle? LastProcessedCandle { get; }
}
