using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Represents one immutable normalized historical market-price observation. The price and source descriptors preserve
/// market-data evidence; they do not imply bid, ask, trade, midpoint, fill, or strategy semantics.
/// </summary>
public sealed record HistoricalMarketPriceObservation
{
    public HistoricalMarketPriceObservation(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateTimeOffset observedAtUtc,
        decimal observedPrice,
        string observationKind,
        string sourceResolution,
        long? sourceSequence = null)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedAtUtc));
        ValidateDescriptor(observationKind, nameof(observationKind));
        ValidateDescriptor(sourceResolution, nameof(sourceResolution));

        ObservedAtUtc = observedAtUtc;
        ObservedPrice = observedPrice;
        ObservationKind = observationKind;
        SourceResolution = sourceResolution;
        SourceSequence = sourceSequence;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public decimal ObservedPrice { get; }
    public string ObservationKind { get; }
    public string SourceResolution { get; }
    public long? SourceSequence { get; }

    private static void ValidateDescriptor(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw new ArgumentException("Source descriptors must be non-empty and have no surrounding whitespace.", parameterName);
    }
}
