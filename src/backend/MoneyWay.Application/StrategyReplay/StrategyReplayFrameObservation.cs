using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Records rule evaluations produced for one replay frame; it is not a complete strategy verdict.</summary>
public sealed class StrategyReplayFrameObservation
{
    public StrategyReplayFrameObservation(StrategyId strategyId, StrategyVersion strategyVersion, int step, DateTimeOffset asOfUtc, Candle currentCandle, IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(currentCandle); ArgumentNullException.ThrowIfNull(evaluations);
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero || asOfUtc != currentCandle.CloseTimeUtc) throw new ArgumentException("Timestamp must be UTC and equal candle close.", nameof(asOfUtc));
        var snapshot = evaluations.ToArray();
        if (snapshot.Any(x => x is null)) throw new ArgumentException("Evaluations cannot contain null.", nameof(evaluations));
        if (snapshot.GroupBy(x => x.RuleId).Any(x => x.Count() > 1) || snapshot.GroupBy(x => x.Sequence).Any(x => x.Count() > 1)) throw new ArgumentException("Evaluation identifiers and sequences must be unique.", nameof(evaluations));
        if (snapshot.Where((x, i) => i > 0 && x.Sequence <= snapshot[i - 1].Sequence).Any()) throw new ArgumentException("Evaluations must be ordered by sequence.", nameof(evaluations));
        if (snapshot.Any(x => x.EvaluatedAtUtc != asOfUtc)) throw new ArgumentException("Evaluation timestamps must match the frame.", nameof(evaluations));
        StrategyId = strategyId; StrategyVersion = strategyVersion; Step = step; AsOfUtc = asOfUtc; CurrentCandle = currentCandle;
        Evaluations = new ReadOnlyCollection<RuleEvaluation>(snapshot);
    }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public Candle CurrentCandle { get; }
    public IReadOnlyList<RuleEvaluation> Evaluations { get; }
    public int EvaluationCount => Evaluations.Count;
}
