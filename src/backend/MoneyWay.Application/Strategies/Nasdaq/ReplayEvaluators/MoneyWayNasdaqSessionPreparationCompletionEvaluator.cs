using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Evaluates same-session preparation completion against the 08:00–08:30 America/Bogota window.</summary>
public sealed class MoneyWayNasdaqSessionPreparationCompletionEvaluator : IReplayRuleEvaluator
{
    private const string TimeZoneId = "America/Bogota";
    private static readonly TimeOnly PreparationStart = new(8, 0);
    private static readonly TimeOnly PreparationDeadline = new(8, 30);
    private static readonly TimeZoneInfo StrategyTimeZone = ResolveStrategyTimeZone();
    private static readonly StrategyDefinition StrategyDefinition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition RuleDefinition = StrategyDefinition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-003");

    public StrategyId StrategyId => StrategyDefinition.StrategyId;
    public StrategyVersion StrategyVersion => StrategyDefinition.Version;
    public RuleId RuleId => RuleDefinition.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");

        var localAsOf = TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone);
        var tradingDay = DateOnly.FromDateTime(localAsOf.DateTime);
        var localTime = TimeOnly.FromDateTime(localAsOf.DateTime);
        var observation = context.InputObservations.OfType<NasdaqPreparationCompletionObservation>()
            .SingleOrDefault(item => item.TradingDay == tradingDay);

        if (localTime < PreparationStart)
            return new(RuleEvaluationResult.NotApplicable,
                "The 08:00 America/Bogota preparation phase has not started.", observation?.SourceReference);

        if (observation is not null)
        {
            var completedLocal = TimeZoneInfo.ConvertTime(observation.ObservedAtUtc, StrategyTimeZone);
            var completionTime = TimeOnly.FromDateTime(completedLocal.DateTime);
            if (DateOnly.FromDateTime(completedLocal.DateTime) == tradingDay
                && completionTime >= PreparationStart && completionTime < PreparationDeadline)
                return new(RuleEvaluationResult.Passed,
                    "Same-session preparation completion was observable within [08:00, 08:30) America/Bogota.",
                    observation.SourceReference);
        }

        return localTime < PreparationDeadline
            ? new(RuleEvaluationResult.Waiting,
                "No qualifying same-session preparation completion is observable before the 08:30 America/Bogota deadline.",
                observation?.SourceReference)
            : new(RuleEvaluationResult.Failed,
                "No qualifying same-session preparation completion was observable before the 08:30 America/Bogota deadline.",
                observation?.SourceReference);
    }

    private static TimeZoneInfo ResolveStrategyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(TimeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
