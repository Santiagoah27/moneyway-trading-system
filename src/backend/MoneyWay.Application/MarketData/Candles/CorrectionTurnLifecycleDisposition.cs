namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Distinguishes an active correction from waiting with valid structure and invalidated structure.</summary>
public enum CorrectionTurnLifecycleDisposition
{
    ActiveCorrection = 0,
    AwaitingCorrectionStart = 1,
    StructureInvalidated = 2,
}
