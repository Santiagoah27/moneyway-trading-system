using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Maps validated human structural selections to NQ-LIQ-002; discovers no points and detects no takes.</summary>
public sealed class MoneyWayNasdaqHumanStructuralLiquidityEvaluator : IReplayRuleEvaluator
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition Rule = Definition.Rules.Single(item => item.RuleId.Value == "NQ-LIQ-002");
    private readonly NasdaqHumanStructuralLiquidityObservationSelector selector = new();

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
            NasdaqHumanStructuralLiquiditySelection.Missing => new(RuleEvaluationResult.HumanValidationRequired,
                "No usable same-session human structural liquidity selection is observable; human validation is required (ADR 0025).", null),
            NasdaqHumanStructuralLiquiditySelection.Conflict conflict => new(RuleEvaluationResult.HumanValidationRequired,
                "Visible human structural liquidity selections conflict; no reference set has priority (ADR 0025).", Evidence(conflict.SupportingObservations)),
            NasdaqHumanStructuralLiquiditySelection.Unique unique => new(RuleEvaluationResult.Passed,
                "A source-observable same-session structural liquidity reference set is selected and validated; human coordinate ownership remains explicit and no liquidity take is implied.",
                Evidence(unique.SupportingObservations)),
            _ => throw new InvalidOperationException("Unknown structural liquidity evidence selection."),
        };
    }

    // Use the existing diagnostic string, retaining all support and exact source links without serializing H4 state graphs.
    private static string Evidence(IReadOnlyList<NasdaqHumanStructuralLiquidityObservation> observations) =>
        JsonSerializer.Serialize(observations.Select(observation => new
        {
            observation.Session,
            observation.EffectiveAtUtc,
            observation.ObservedAtUtc,
            observation.SourceReference,
            References = observation.References.Select(reference => new
            {
                reference.Role,
                reference.Model,
                reference.Timeframe,
                reference.Side,
                reference.PriceOwnership,
                reference.StructuralPrice,
                reference.SourceReference,
                MemberOpenTimesUtc = reference.Members.Select(member => member.OpenTimeUtc),
                Sources = reference.SourceCandles.Select(source => new { source.OpenTimeUtc, source.CloseTimeUtc }),
                Episode = reference.EpisodeIdentity,
                Validation = reference.Validation is not { } validation ? null : new
                {
                    validation.ReferenceLevel,
                    validation.Direction,
                    validation.AsOfUtc,
                    CandleOpenTimeUtc = validation.Candle!.OpenTimeUtc,
                },
                Completion = reference.ActiveExtremeGeometry?.MemberResolution.MembershipEvent.Episode.ConfirmingCandleOpenTimeUtc,
                Turn = reference.ActiveExtremeGeometry?.MemberResolution.MembershipEvent.CorrectionStartCandleOpenTimeUtc,
                // This existing compact key retains per-reference and embedded human support provenance.
                Provenance = JsonSerializer.Deserialize<JsonElement>(reference.ProvenanceKey),
            }),
        }));
}
