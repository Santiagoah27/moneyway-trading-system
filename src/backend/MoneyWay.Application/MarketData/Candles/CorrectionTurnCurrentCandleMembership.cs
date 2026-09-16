namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Identifies how the current candle participates in the correction turn after a terminal transition.</summary>
public enum CorrectionTurnCurrentCandleMembership
{
    ExistingTurn = 0,
    NewTurn = 1,
    NoCorrectionTurn = 2,
}
