using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Maps ADR 0025 human H4 evidence to runtime results; implements no structural algorithm or order logic.</summary>
public sealed class MoneyWayNasdaqHumanH4ContextEvaluator : IReplayRuleEvaluator
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition Rule = Definition.Rules.Single(item => item.RuleId.Value == "NQ-H4-001");
    private readonly NasdaqHumanH4ContextObservationSelector selector = new();

    public StrategyId StrategyId => Definition.StrategyId;
    public StrategyVersion StrategyVersion => Definition.Version;
    public RuleId RuleId => Rule.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");
        var selection = selector.Select(context, NasdaqDemoSessionIdentity.FromContext(context));
        return selection switch
        {
            NasdaqHumanH4ContextSelection.Missing => new(RuleEvaluationResult.HumanValidationRequired,
                "No usable same-session human H4 context is observable; human validation is required (ADR 0025).", null),
            NasdaqHumanH4ContextSelection.Conflict conflict => new(RuleEvaluationResult.HumanValidationRequired,
                "Visible human H4 context assertions conflict; no assertion has priority (ADR 0025).", Evidence(conflict.SupportingObservations)),
            NasdaqHumanH4ContextSelection.Unique unique => new(
                unique.Fact.PermittedDirection == NasdaqHumanH4PermittedDirection.Unresolved
                    || unique.Fact.ContextKind == NasdaqHumanH4ContextKind.Unresolved
                    ? RuleEvaluationResult.HumanValidationRequired : RuleEvaluationResult.Passed,
                unique.Fact.PermittedDirection == NasdaqHumanH4PermittedDirection.Unresolved
                    || unique.Fact.ContextKind == NasdaqHumanH4ContextKind.Unresolved
                    ? "The unique human H4 review explicitly leaves context or permitted direction unresolved."
                    : "A source-observable same-session human H4 review establishes context and permitted direction; no autonomous H4 algorithm is implied.",
                Evidence(unique.SupportingObservations), new NasdaqHumanH4ContextRuleFact(unique)),
            _ => throw new InvalidOperationException("Unknown H4 evidence selection."),
        };
    }

    // The existing decision supports one string. JSON retains every typed source record without changing diagnostics.
    private static string Evidence(IReadOnlyList<NasdaqHumanH4ContextObservation> observations) => JsonSerializer.Serialize(observations);
}
