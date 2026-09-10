using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;

/// <summary>
/// Starts one Nasdaq pre-entry setup when the audited Step-3 gate is newly established and expires an incomplete
/// active setup when the canonical trading-window-end evaluation reaches the operational cutoff.
/// </summary>
public sealed class MoneyWayNasdaqReplayLifecyclePolicy : IStrategyReplayLifecyclePolicy
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly RuleId ActivationRuleId = Rule("NQ-LIQ-003");
    private static readonly RuleId CompletionRuleId = Rule("NQ-M1-003");
    private static readonly RuleId CutoffRuleId = Rule("NQ-TIME-002");
    private static readonly StrategyReplayProgressionStateReference ActivationReference = new(ActivationRuleId.Value);

    public StrategyId StrategyId => Definition.StrategyId;
    public StrategyVersion StrategyVersion => Definition.Version;

    public StrategyReplayLifecycleTransition Decide(StrategyReplayLifecyclePolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Observation.StrategyId != StrategyId || context.Observation.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Lifecycle context identity must exactly match the Nasdaq policy identity.");

        var cutoffEvaluation = context.Observation.Evaluations.SingleOrDefault(item => item.RuleId == CutoffRuleId);
        var cutoffReached = cutoffEvaluation?.Result == RuleEvaluationResult.Failed;
        var active = context.PreviousLifecycle?.ActiveInstance;
        if (active is not null)
        {
            var completedBeforeCurrentFrame = active.WorkflowProgression.EstablishedRuleIds.Contains(CompletionRuleId);
            return cutoffReached && !completedBeforeCurrentFrame
                ? StrategyReplayLifecycleTransition.Expire()
                : StrategyReplayLifecycleTransition.None;
        }

        if (cutoffEvaluation?.Result != RuleEvaluationResult.Passed)
            return StrategyReplayLifecycleTransition.None;

        var activation = context.CandidateProgression.RuleEligibility
            .SingleOrDefault(item => item.RuleId == ActivationRuleId);
        return activation?.EstablishesProgression == true
            ? StrategyReplayLifecycleTransition.Start(ActivationReference)
            : StrategyReplayLifecycleTransition.None;
    }

    private static RuleId Rule(string value) =>
        Definition.Rules.Single(item => item.RuleId.Value == value).RuleId;
}
