using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.StructuralBreaks;

/// <summary>Reports whether one candle newly observable at a replay boundary confirms a strict body-close structural break.</summary>
public sealed record StructuralBodyCloseBreakResult
{
    internal StructuralBodyCloseBreakResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        decimal referenceLevel,
        StructuralBreakDirection direction,
        DateTimeOffset asOfUtc,
        Candle? candle,
        bool isConfirmed)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Timeframe = timeframe ?? throw new ArgumentNullException(nameof(timeframe));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Replay timestamp must be UTC.", nameof(asOfUtc));
        if (candle is not null
            && (candle.ProviderId != providerId || candle.Symbol != symbol || candle.Timeframe != timeframe || candle.CloseTimeUtc != asOfUtc))
            throw new ArgumentException("Candle must match the replay boundary.", nameof(candle));
        if (isConfirmed && candle is null) throw new ArgumentException("Confirmed results require a closed candle.", nameof(isConfirmed));

        ReferenceLevel = referenceLevel;
        Direction = direction;
        AsOfUtc = asOfUtc;
        Candle = candle;
        IsConfirmed = isConfirmed;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe { get; }
    public decimal ReferenceLevel { get; }
    public StructuralBreakDirection Direction { get; }
    public DateTimeOffset AsOfUtc { get; }
    public Candle? Candle { get; }
    public bool IsConfirmed { get; }
}
