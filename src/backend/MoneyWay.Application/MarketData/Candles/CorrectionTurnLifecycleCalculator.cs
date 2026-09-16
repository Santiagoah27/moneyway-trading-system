using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Applies an already-calculated bullish or bearish terminal transition to correction-turn membership.
/// It does not inspect OHLC, detect terminal events, or infer structural state.
/// </summary>
public sealed class CorrectionTurnLifecycleCalculator
{
    public CorrectionTurnLifecycleResult Evaluate(
        IReadOnlyList<Candle> currentTurnCandles,
        Candle currentCandle,
        BullishCorrectionTerminalTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        var action = transition.TransitionKind switch
        {
            BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection => LifecycleAction.Continue,
            BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection => LifecycleAction.ResetAndStart,
            BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart => LifecycleAction.AwaitCorrectionStart,
            BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure => LifecycleAction.InvalidateStructure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition.TransitionKind,
                "The bullish correction terminal transition kind is not supported."),
        };

        return Apply(currentTurnCandles, currentCandle, action);
    }

    public CorrectionTurnLifecycleResult Evaluate(
        IReadOnlyList<Candle> currentTurnCandles,
        Candle currentCandle,
        BearishCorrectionTerminalTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        var action = transition.TransitionKind switch
        {
            BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection => LifecycleAction.Continue,
            BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection => LifecycleAction.ResetAndStart,
            BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart => LifecycleAction.AwaitCorrectionStart,
            BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure => LifecycleAction.InvalidateStructure,
            _ => throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition.TransitionKind,
                "The bearish correction terminal transition kind is not supported."),
        };

        return Apply(currentTurnCandles, currentCandle, action);
    }

    private static CorrectionTurnLifecycleResult Apply(
        IReadOnlyList<Candle> currentTurnCandles,
        Candle currentCandle,
        LifecycleAction action)
    {
        ArgumentNullException.ThrowIfNull(currentTurnCandles);
        ArgumentNullException.ThrowIfNull(currentCandle);

        var previousTurn = currentTurnCandles.ToArray();
        if (previousTurn.Any(candle => candle is null))
        {
            throw new ArgumentException("Correction-turn candles cannot contain null elements.", nameof(currentTurnCandles));
        }

        return action switch
        {
            LifecycleAction.Continue => new(
                [.. previousTurn, currentCandle],
                true,
                false,
                CorrectionTurnCurrentCandleMembership.ExistingTurn,
                CorrectionTurnLifecycleDisposition.ActiveCorrection),
            LifecycleAction.ResetAndStart => new(
                [currentCandle],
                true,
                true,
                CorrectionTurnCurrentCandleMembership.NewTurn,
                CorrectionTurnLifecycleDisposition.ActiveCorrection),
            LifecycleAction.AwaitCorrectionStart => new(
                [],
                false,
                true,
                CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.AwaitingCorrectionStart),
            LifecycleAction.InvalidateStructure => new(
                [],
                false,
                true,
                CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
                CorrectionTurnLifecycleDisposition.StructureInvalidated),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "The correction-turn lifecycle action is not supported."),
        };
    }

    private enum LifecycleAction
    {
        Continue = 0,
        ResetAndStart = 1,
        AwaitCorrectionStart = 2,
        InvalidateStructure = 3,
    }
}
