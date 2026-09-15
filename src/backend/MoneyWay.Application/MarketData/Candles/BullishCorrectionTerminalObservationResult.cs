using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports terminal facts observable for an active bullish correction at one canonical replay boundary.
/// It does not select event precedence, change structural state, or assign candle membership.
/// </summary>
public sealed record BullishCorrectionTerminalObservationResult
{
    internal BullishCorrectionTerminalObservationResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        DateTimeOffset asOfUtc,
        decimal previousCeiling,
        decimal resultingCeiling,
        Candle? candle,
        CandleBodyDirection? bodyDirection,
        bool wasNewHighResetObserved,
        bool wasPriorHlInvalidationObserved)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Replay timestamp must be UTC.", nameof(asOfUtc));
        if (bodyDirection is not null && !Enum.IsDefined(bodyDirection.Value))
            throw new ArgumentOutOfRangeException(nameof(bodyDirection));
        if (candle is null && (bodyDirection is not null || wasNewHighResetObserved || wasPriorHlInvalidationObserved))
            throw new ArgumentException("Observed terminal facts require a closed candle.", nameof(candle));
        if (candle is not null
            && (candle.ProviderId != providerId || candle.Symbol != symbol || candle.Timeframe != timeframe || candle.CloseTimeUtc != asOfUtc))
            throw new ArgumentException("Candle must match the replay boundary.", nameof(candle));
        if (candle is not null && bodyDirection is null)
            throw new ArgumentException("A closed candle requires body direction.", nameof(bodyDirection));
        if (wasNewHighResetObserved != (resultingCeiling > previousCeiling))
            throw new ArgumentException("Reset observation must match the strict ceiling update.", nameof(wasNewHighResetObserved));

        AsOfUtc = asOfUtc;
        PreviousCeiling = previousCeiling;
        ResultingCeiling = resultingCeiling;
        Candle = candle;
        BodyDirection = bodyDirection;
        WasNewHighResetObserved = wasNewHighResetObserved;
        WasPriorHlInvalidationObserved = wasPriorHlInvalidationObserved;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe { get; }
    public DateTimeOffset AsOfUtc { get; }
    public decimal PreviousCeiling { get; }
    public decimal ResultingCeiling { get; }
    public Candle? Candle { get; }
    public CandleBodyDirection? BodyDirection { get; }
    public bool WasNewHighResetObserved { get; }
    public bool WasPriorHlInvalidationObserved { get; }
}
