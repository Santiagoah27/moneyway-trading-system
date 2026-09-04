namespace MoneyWay.Api.Contracts.StrategyDefinitions;

public sealed record StrategyRuleDefinitionResponse(
    string RuleId,
    string Name,
    string Stage,
    int Sequence,
    bool IsRequired,
    string DefinitionStatus,
    string Description,
    string SourceReference);
