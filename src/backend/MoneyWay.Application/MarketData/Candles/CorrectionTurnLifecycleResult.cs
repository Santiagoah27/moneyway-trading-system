using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports correction-turn membership after applying an already-calculated terminal transition.
/// </summary>
public sealed class CorrectionTurnLifecycleResult
{
    internal CorrectionTurnLifecycleResult(
        IEnumerable<Candle> resultingTurnCandles,
        bool isCorrectionTurnActive,
        bool wasPreviousTurnTerminated,
        CorrectionTurnCurrentCandleMembership currentCandleMembership)
    {
        ArgumentNullException.ThrowIfNull(resultingTurnCandles);
        if (!Enum.IsDefined(currentCandleMembership))
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentCandleMembership),
                currentCandleMembership,
                "The current-candle correction-turn membership is not supported.");
        }

        var snapshot = resultingTurnCandles.ToArray();
        if (snapshot.Any(candle => candle is null))
        {
            throw new ArgumentException("Resulting correction-turn candles cannot contain null elements.", nameof(resultingTurnCandles));
        }

        if (isCorrectionTurnActive != (currentCandleMembership != CorrectionTurnCurrentCandleMembership.NoCorrectionTurn))
        {
            throw new ArgumentException("Correction-turn activity must match current-candle membership.", nameof(isCorrectionTurnActive));
        }

        if (wasPreviousTurnTerminated == (currentCandleMembership == CorrectionTurnCurrentCandleMembership.ExistingTurn))
        {
            throw new ArgumentException("Previous-turn termination must match current-candle membership.", nameof(wasPreviousTurnTerminated));
        }

        if (!isCorrectionTurnActive && snapshot.Length != 0)
        {
            throw new ArgumentException("An inactive correction turn cannot contain candles.", nameof(resultingTurnCandles));
        }

        ResultingTurnCandles = new ReadOnlyCollection<Candle>(snapshot);
        IsCorrectionTurnActive = isCorrectionTurnActive;
        WasPreviousTurnTerminated = wasPreviousTurnTerminated;
        CurrentCandleMembership = currentCandleMembership;
    }

    public IReadOnlyList<Candle> ResultingTurnCandles { get; }

    public bool IsCorrectionTurnActive { get; }

    public bool WasPreviousTurnTerminated { get; }

    public CorrectionTurnCurrentCandleMembership CurrentCandleMembership { get; }
}
