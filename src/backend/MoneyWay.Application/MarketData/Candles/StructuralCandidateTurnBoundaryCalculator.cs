using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Composes existing causal observations for one closed candle in a supplied active candidate turn.</summary>
public sealed class StructuralCandidateTurnBoundaryCalculator
{
    private readonly StructuralCandidateValidationCalculator candidateValidationCalculator = new();
    private readonly BullishCorrectionTerminalObservationCalculator bullishObservationCalculator = new();
    private readonly BearishCorrectionTerminalObservationCalculator bearishObservationCalculator = new();
    private readonly BullishCorrectionTerminalTransitionCalculator bullishTransitionCalculator = new();
    private readonly BearishCorrectionTerminalTransitionCalculator bearishTransitionCalculator = new();
    private readonly CorrectionTurnLifecycleCalculator lifecycleCalculator = new();
    private readonly CorrectionBoundaryObservationCalculator boundaryObservationCalculator = new();
    private readonly CorrectionStartTransitionCalculator startTransitionCalculator = new();

    public StructuralCandidateTurnBoundaryResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        Timeframe timeframe,
        decimal priorStructuralReference,
        decimal priorValidatedOppositePoint,
        decimal currentCorrectionOriginExtreme,
        IReadOnlyList<Candle> candidateTurnCandles,
        StructuralCandidateExtremeSide candidateSide)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(candidateTurnCandles);
        if (!Enum.IsDefined(candidateSide)) throw new ArgumentOutOfRangeException(nameof(candidateSide));
        if (candidateSide == StructuralCandidateExtremeSide.Lower
            ? priorValidatedOppositePoint >= priorStructuralReference
            : priorStructuralReference >= priorValidatedOppositePoint)
        {
            throw new ArgumentException("The supplied structural references must be strictly ordered.", nameof(priorValidatedOppositePoint));
        }

        if (!context.WasUpdated(timeframe) || !context.TryGetFrame(timeframe, out var frame))
            throw new ArgumentException("The active-turn boundary requires a current formally closed candle.", nameof(context));

        var currentCandle = frame!.CurrentCandle;
        if (candidateTurnCandles.Count == 0 || candidateTurnCandles.Any(candle =>
                candle is null || candle.CloseTimeUtc >= currentCandle.CloseTimeUtc))
        {
            throw new ArgumentException("The active candidate turn must contain only preceding candles.", nameof(candidateTurnCandles));
        }

        var validation = candidateValidationCalculator.EvaluateCurrentBoundary(
            context, timeframe, priorStructuralReference, candidateTurnCandles, candidateSide);

        return candidateSide == StructuralCandidateExtremeSide.Lower
            ? EvaluateBullish(context, timeframe, priorValidatedOppositePoint, currentCorrectionOriginExtreme,
                candidateTurnCandles, currentCandle, validation)
            : EvaluateBearish(context, timeframe, priorValidatedOppositePoint, currentCorrectionOriginExtreme,
                candidateTurnCandles, currentCandle, validation);
    }

    private StructuralCandidateTurnBoundaryResult EvaluateBullish(
        StrategyReplayContext context, Timeframe timeframe, decimal priorHl, decimal ceiling,
        IReadOnlyList<Candle> turn, Candle candle, StructuralCandidateValidationResult validation)
    {
        var observation = bullishObservationCalculator.EvaluateCurrentBoundary(context, timeframe, ceiling, priorHl);
        if (validation.IsValidated)
            return Confirm(candle, validation, observation.PreviousCeiling, observation.ResultingCeiling,
                observation.WasNewHighResetObserved, CorrectionOriginExtremeSide.Ceiling);

        var transition = bullishTransitionCalculator.Evaluate(observation);
        var lifecycle = lifecycleCalculator.Evaluate(turn, candle, transition);
        var kind = transition.TransitionKind switch
        {
            BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection => StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn,
            BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection => StructuralCandidateTurnBoundaryKind.ResetAndStartNewCorrection,
            BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart => StructuralCandidateTurnBoundaryKind.ResetAndAwaitCorrectionStart,
            BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure => StructuralCandidateTurnBoundaryKind.StructureInvalidated,
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
        return new(kind, candle, validation, lifecycle, observation.ResultingCeiling, null);
    }

    private StructuralCandidateTurnBoundaryResult EvaluateBearish(
        StrategyReplayContext context, Timeframe timeframe, decimal priorLh, decimal floor,
        IReadOnlyList<Candle> turn, Candle candle, StructuralCandidateValidationResult validation)
    {
        var observation = bearishObservationCalculator.EvaluateCurrentBoundary(context, timeframe, floor, priorLh);
        if (validation.IsValidated)
            return Confirm(candle, validation, observation.PreviousFloor, observation.ResultingFloor,
                observation.WasNewLowResetObserved, CorrectionOriginExtremeSide.Floor);

        var transition = bearishTransitionCalculator.Evaluate(observation);
        var lifecycle = lifecycleCalculator.Evaluate(turn, candle, transition);
        var kind = transition.TransitionKind switch
        {
            BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection => StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn,
            BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection => StructuralCandidateTurnBoundaryKind.ResetAndStartNewCorrection,
            BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart => StructuralCandidateTurnBoundaryKind.ResetAndAwaitCorrectionStart,
            BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure => StructuralCandidateTurnBoundaryKind.StructureInvalidated,
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
        return new(kind, candle, validation, lifecycle, observation.ResultingFloor, null);
    }

    private StructuralCandidateTurnBoundaryResult Confirm(
        Candle candle, StructuralCandidateValidationResult validation,
        decimal previousExtreme, decimal resultingExtreme, bool extendedOrigin, CorrectionOriginExtremeSide side)
    {
        if (!extendedOrigin)
        {
            var awaiting = new CorrectionTurnLifecycleResult([], false, true,
                CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart, candle);
            return new(StructuralCandidateTurnBoundaryKind.ConfirmCandidate, candle, validation,
                awaiting, resultingExtreme, null);
        }

        var boundary = boundaryObservationCalculator.Evaluate(previousExtreme, candle, side);
        var start = startTransitionCalculator.Evaluate(boundary, candle);
        var lifecycle = new CorrectionTurnLifecycleResult(
            start.FirstTurnCandle is null ? [] : [candle],
            start.FirstTurnCandle is not null,
            true,
            start.CurrentCandleMembership,
            start.FirstTurnCandle is null
                ? CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart
                : CorrectionTurnLifecycleDisposition.ActiveCorrection,
            candle);
        var kind = start.FirstTurnCandle is null
            ? StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndAwaitNextCorrection
            : StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection;
        var newExtreme = side == CorrectionOriginExtremeSide.Ceiling ? candle.High : candle.Low;
        return new(kind, candle, validation, lifecycle, resultingExtreme, newExtreme);
    }
}
