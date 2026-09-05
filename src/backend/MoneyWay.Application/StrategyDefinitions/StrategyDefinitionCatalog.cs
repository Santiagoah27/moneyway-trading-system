using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyDefinitions;

/// <summary>
/// Provides exact access to built-in strategy definitions without evaluation, file loading, or automatic version selection.
/// </summary>
public sealed class StrategyDefinitionCatalog
{
    private readonly IReadOnlyList<StrategyDefinition> definitions =
        new ReadOnlyCollection<StrategyDefinition>(
            [MoneyWayForexStrategyDefinition.Instance, MoneyWayNasdaqStrategyDefinition.Instance]);

    public IReadOnlyList<StrategyDefinition> GetAll() => definitions;

    public StrategyDefinition? Find(StrategyId strategyId, StrategyVersion version)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(version);

        return definitions.SingleOrDefault(definition =>
            definition.StrategyId == strategyId && definition.Version == version);
    }
}
