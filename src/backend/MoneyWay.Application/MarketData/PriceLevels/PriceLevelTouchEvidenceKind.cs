namespace MoneyWay.Application.MarketData.PriceLevels;

/// <summary>Identifies the market-data evidence that established a price-level touch.</summary>
public enum PriceLevelTouchEvidenceKind
{
    CandleRange = 0,
    MarketPriceObservation = 1,
}
