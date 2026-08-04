namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Describes the overall result of a strategy evaluation, not the result or definition status of an individual rule.
/// </summary>
public enum StrategyVerdict
{
    Ready,
    Wait,
    NoTrade,
    HumanValidationRequired,
    DataUnavailable,
}
