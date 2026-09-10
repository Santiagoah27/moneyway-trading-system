namespace MoneyWay.Application.StrategyReplay.Observability;

/// <summary>Describes whether available historical evidence can establish one required temporal relationship.</summary>
public enum ReplayMarketDataObservabilityStatus
{
    Sufficient = 0,
    ResolutionInsufficient = 1,
}
