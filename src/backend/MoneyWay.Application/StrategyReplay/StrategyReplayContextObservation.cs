using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Records rule evaluations produced for one synchronized strategy replay context. It contains no single global
/// current candle and no strategy verdict.
/// </summary>
public sealed class StrategyReplayContextObservation
{
    public StrategyReplayContextObservation(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(evaluations);
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        var snapshot = evaluations.ToArray();
        if (snapshot.Any(x => x is null)) throw new ArgumentException("Evaluations cannot contain null.", nameof(evaluations));
        if (snapshot.GroupBy(x => x.RuleId).Any(x => x.Count() > 1) || snapshot.GroupBy(x => x.Sequence).Any(x => x.Count() > 1))
            throw new ArgumentException("Evaluation identifiers and sequences must be unique.", nameof(evaluations));
        if (snapshot.Where((x, i) => i > 0 && x.Sequence <= snapshot[i - 1].Sequence).Any())
            throw new ArgumentException("Evaluations must be ordered by sequence.", nameof(evaluations));
        if (snapshot.Any(x => x.EvaluatedAtUtc != asOfUtc))
            throw new ArgumentException("Evaluation timestamps must match the context.", nameof(evaluations));
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        ProviderId = providerId;
        Symbol = symbol;
        Step = step;
        AsOfUtc = asOfUtc;
        Evaluations = new ReadOnlyCollection<RuleEvaluation>(snapshot);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<RuleEvaluation> Evaluations { get; }
    public int EvaluationCount => Evaluations.Count;
}
