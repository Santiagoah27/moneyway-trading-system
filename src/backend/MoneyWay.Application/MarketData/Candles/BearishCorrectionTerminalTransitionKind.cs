namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Identifies the structural transition of an already-active bearish correction.</summary>
public enum BearishCorrectionTerminalTransitionKind
{
    ContinueExistingCorrection = 0,
    ResetAndStartNewCorrection = 1,
    ResetAndAwaitCorrectionStart = 2,
    InvalidateBearishStructure = 3,
}
