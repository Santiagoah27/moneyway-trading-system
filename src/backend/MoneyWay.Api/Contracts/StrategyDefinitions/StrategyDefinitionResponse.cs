namespace MoneyWay.Api.Contracts.StrategyDefinitions;

public sealed record StrategyDefinitionResponse(
    string StrategyId,
    string Version,
    string DisplayName,
    string SpecificationReference,
    int RuleCount,
    int RequiredRuleCount,
    IReadOnlyList<StrategyRuleDefinitionResponse> Rules);
