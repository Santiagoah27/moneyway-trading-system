using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports terminal facts observable for an active bearish correction at one canonical replay boundary.
/// It does not select event precedence, change structural state, or assign candle membership.
/// </summary>
public sealed record BearishCorrectionTerminalObservationResult
{
    internal BearishCorrectionTerminalObservationResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        DateTimeOffset asOfUtc,
        decimal previousFloor,
        decimal resultingFloor,
        Candle? candle,
        CandleBodyDirection? bodyDirection,
        bool wasNewLowResetObserved,
        bool wasPriorLhInvalidationObserved)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Replay timestamp must be UTC.", nameof(asOfUtc));
        if (bodyDirection is not null && !Enum.IsDefined(bodyDirection.Value))
            throw new ArgumentOutOfRangeException(nameof(bodyDirection));
        if (candle is null && (bodyDirection is not null || wasNewLowResetObserved || wasPriorLhInvalidationObserved))
            throw new ArgumentException("Observed terminal facts require a closed candle.", nameof(candle));
        if (candle is not null
            && (candle.ProviderId != providerId || candle.Symbol != symbol || candle.Timeframe != timeframe || candle.CloseTimeUtc != asOfUtc))
            throw new ArgumentException("Candle must match the replay boundary.", nameof(candle));
        if (candle is not null && bodyDirection is null)
            throw new ArgumentException("A closed candle requires body direction.", nameof(bodyDirection));
        if (wasNewLowResetObserved != (resultingFloor < previousFloor))
            throw new ArgumentException("Reset observation must match the strict floor update.", nameof(wasNewLowResetObserved));

        AsOfUtc = asOfUtc;
        PreviousFloor = previousFloor;
        ResultingFloor = resultingFloor;
        Candle = candle;
        BodyDirection = bodyDirection;
        WasNewLowResetObserved = wasNewLowResetObserved;
        WasPriorLhInvalidationObserved = wasPriorLhInvalidationObserved;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe { get; }
    public DateTimeOffset AsOfUtc { get; }
    public decimal PreviousFloor { get; }
    public decimal ResultingFloor { get; }
    public Candle? Candle { get; }
    public CandleBodyDirection? BodyDirection { get; }
    public bool WasNewLowResetObserved { get; }
    public bool WasPriorLhInvalidationObserved { get; }
}
