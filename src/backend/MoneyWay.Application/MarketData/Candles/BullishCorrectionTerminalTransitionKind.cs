namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Identifies the structural transition of an already-active bullish correction.</summary>
public enum BullishCorrectionTerminalTransitionKind
{
    ContinueExistingCorrection = 0,
    ResetAndStartNewCorrection = 1,
    ResetAndAwaitCorrectionStart = 2,
    InvalidateBullishStructure = 3,
}
