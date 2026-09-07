using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyEvaluation;

/// <summary>
/// Validates manual evaluations against an exact registered definition before generic evaluation.
/// </summary>
public sealed class EvaluateRegisteredStrategyUseCase
{
    private readonly StrategyDefinitionCatalog catalog;
    private readonly EvaluateStrategyUseCase evaluator;

    public EvaluateRegisteredStrategyUseCase(
        StrategyDefinitionCatalog catalog,
        EvaluateStrategyUseCase evaluator)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(evaluator);
        this.catalog = catalog;
        this.evaluator = evaluator;
    }

    public EvaluateRegisteredStrategyResult Execute(EvaluateStrategyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = catalog.Find(request.StrategyId, request.StrategyVersion);
        if (definition is null)
        {
            return EvaluateRegisteredStrategyResult.StrategyNotFound(new(
                "strategy_not_found",
                "The requested strategy definition was not found."));
        }

        var rulesById = definition.Rules.ToDictionary(static rule => rule.RuleId);
        var suppliedRuleIds = request.Evaluations.Select(static evaluation => evaluation.RuleId).ToHashSet();
        var issues = new List<StrategyEvaluationValidationIssue>();

        foreach (var group in request.Evaluations
                     .GroupBy(static evaluation => evaluation.RuleId)
                     .OrderBy(static group => group.Min(evaluation => evaluation.Sequence))
                     .ThenBy(static group => group.Key.Value, StringComparer.Ordinal))
        {
            var orderedEvaluations = group
                .OrderBy(static evaluation => evaluation.Sequence)
                .ThenBy(static evaluation => evaluation.RuleId.Value, StringComparer.Ordinal)
                .ToArray();

            if (orderedEvaluations.Length > 1)
            {
                issues.Add(new("duplicate_rule", "The rule was supplied more than once.", group.Key));
            }

            if (!rulesById.TryGetValue(group.Key, out var rule))
            {
                issues.Add(new("unknown_rule", "The rule does not belong to the requested strategy definition.", group.Key));
                continue;
            }

            foreach (var evaluation in orderedEvaluations)
            {
                if (evaluation.Sequence != rule.Sequence)
                {
                    issues.Add(new("sequence_mismatch", "The sequence does not match the registered rule.", group.Key));
                }

                if (evaluation.IsRequired != rule.IsRequired)
                {
                    issues.Add(new("required_flag_mismatch", "The required flag does not match the registered rule.", group.Key));
                }

                if (evaluation.DefinitionStatus != rule.DefinitionStatus)
                {
                    issues.Add(new(
                        "definition_status_mismatch",
                        "The definition status does not match the registered rule.",
                        group.Key));
                }
            }
        }

        foreach (var missingRule in definition.Rules.Where(rule => rule.IsRequired && !suppliedRuleIds.Contains(rule.RuleId)))
        {
            issues.Add(new("missing_required_rule", "A required rule evaluation was not supplied.", missingRule.RuleId));
        }

        return issues.Count == 0
            ? EvaluateRegisteredStrategyResult.Success(evaluator.Execute(request))
            : EvaluateRegisteredStrategyResult.ValidationFailed(issues);
    }
}
