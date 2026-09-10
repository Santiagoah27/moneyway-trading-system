using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Captures one immutable replay-local workflow progression state after a chronological observation.</summary>
public sealed class StrategyReplayProgressionSnapshot
{
    public StrategyReplayProgressionSnapshot(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<StrategyReplayRuleEligibility> ruleEligibility,
        IEnumerable<RuleId> establishedRuleIds)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(ruleEligibility);
        ArgumentNullException.ThrowIfNull(establishedRuleIds);
        var eligibility = ruleEligibility.ToArray();
        var established = establishedRuleIds.ToArray();
        if (eligibility.Any(item => item is null)
            || eligibility.GroupBy(item => item.RuleId).Any(group => group.Count() > 1)
            || eligibility.Any(item => item.AsOfUtc != asOfUtc))
            throw new ArgumentException("Eligibility items must be non-null, unique by rule, and aligned to the snapshot timestamp.", nameof(ruleEligibility));
        if (established.Any(ruleId => ruleId is null) || established.Distinct().Count() != established.Length)
            throw new ArgumentException("Established rule identifiers must be non-null and unique.", nameof(establishedRuleIds));
        if (eligibility.Where(item => item.EstablishesProgression).Any(item => !established.Contains(item.RuleId)))
            throw new ArgumentException("Rules that establish progression must be present in the established rule snapshot.", nameof(establishedRuleIds));

        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        ProviderId = providerId;
        Symbol = symbol;
        Step = step;
        AsOfUtc = asOfUtc;
        RuleEligibility = new ReadOnlyCollection<StrategyReplayRuleEligibility>(eligibility);
        EstablishedRuleIds = new ReadOnlyCollection<RuleId>(established);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<StrategyReplayRuleEligibility> RuleEligibility { get; }
    public IReadOnlyList<RuleId> EstablishedRuleIds { get; }
}
