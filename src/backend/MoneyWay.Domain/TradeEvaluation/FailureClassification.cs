namespace MoneyWay.Domain.TradeEvaluation;

/// <summary>
/// Classifies an unexpected result without assuming that every loss means the strategy failed.
/// </summary>
public enum FailureClassification
{
    /// <summary>A loss that occurred even though the strategy rules were respected.</summary>
    ValidStrategyLoss,
    RuleViolation,
    ExecutionError,
    MarketDataError,
    InterpretationError,
    RiskManagementError,
    MarketRegimeMismatch,

    /// <summary>The available evidence does not support a reliable classification.</summary>
    Inconclusive,
}
