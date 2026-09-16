namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Identifies the transition from an inactive, pre-start correction state.</summary>
public enum CorrectionStartTransitionKind
{
    AwaitCorrectionStart = 0,
    StartCorrection = 1,
}
