using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyEvaluation;

/// <summary>
/// Captures the strategy identity, version, and completed rule evaluations for deterministic coordination.
/// </summary>
public sealed class EvaluateStrategyRequest
{
    public EvaluateStrategyRequest(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(evaluations);

        var snapshot = evaluations.ToArray();
        if (snapshot.Any(static evaluation => evaluation is null))
        {
            throw new ArgumentException("Evaluations cannot contain null elements.", nameof(evaluations));
        }

        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        Evaluations = new ReadOnlyCollection<RuleEvaluation>(snapshot);
    }

    public StrategyId StrategyId { get; }

    public StrategyVersion StrategyVersion { get; }

    public IReadOnlyList<RuleEvaluation> Evaluations { get; }
}
