using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Records workflow eligibility separately from the raw rule evaluation.</summary>
public sealed class StrategyReplayRuleEligibility
{
    public StrategyReplayRuleEligibility(
        RuleId ruleId,
        DateTimeOffset asOfUtc,
        bool isEligible,
        IEnumerable<RuleId> prerequisiteRuleIds,
        IEnumerable<RuleId> missingPrerequisiteRuleIds,
        bool establishesProgression)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(prerequisiteRuleIds);
        ArgumentNullException.ThrowIfNull(missingPrerequisiteRuleIds);
        var prerequisites = prerequisiteRuleIds.ToArray();
        var missing = missingPrerequisiteRuleIds.ToArray();
        ValidateUnique(prerequisites, nameof(prerequisiteRuleIds));
        ValidateUnique(missing, nameof(missingPrerequisiteRuleIds));
        if (missing.Any(ruleId => !prerequisites.Contains(ruleId)))
            throw new ArgumentException("Missing prerequisites must belong to the declared prerequisite set.", nameof(missingPrerequisiteRuleIds));
        if (isEligible != (missing.Length == 0))
            throw new ArgumentException("Eligibility must match the missing prerequisite set.", nameof(isEligible));
        if (establishesProgression && !isEligible)
            throw new ArgumentException("An ineligible rule cannot establish progression.", nameof(establishesProgression));

        RuleId = ruleId;
        AsOfUtc = asOfUtc;
        IsEligible = isEligible;
        PrerequisiteRuleIds = new ReadOnlyCollection<RuleId>(prerequisites);
        MissingPrerequisiteRuleIds = new ReadOnlyCollection<RuleId>(missing);
        EstablishesProgression = establishesProgression;
    }

    public RuleId RuleId { get; }
    public DateTimeOffset AsOfUtc { get; }
    public bool IsEligible { get; }
    public IReadOnlyList<RuleId> PrerequisiteRuleIds { get; }
    public IReadOnlyList<RuleId> MissingPrerequisiteRuleIds { get; }
    public bool EstablishesProgression { get; }

    private static void ValidateUnique(IReadOnlyList<RuleId> values, string parameterName)
    {
        if (values.Any(ruleId => ruleId is null) || values.Distinct().Count() != values.Count)
            throw new ArgumentException("Rule identifiers must be non-null and unique.", parameterName);
    }
}
