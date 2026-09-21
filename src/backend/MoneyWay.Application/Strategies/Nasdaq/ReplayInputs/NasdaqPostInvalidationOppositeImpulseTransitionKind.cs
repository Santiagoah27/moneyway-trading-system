namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One-candle outcome while advancing a provisional opposite impulse.</summary>
public enum NasdaqPostInvalidationOppositeImpulseTransitionKind
{
    ContinuingImpulse = 0,
    CorrectionStarted = 1,
}
