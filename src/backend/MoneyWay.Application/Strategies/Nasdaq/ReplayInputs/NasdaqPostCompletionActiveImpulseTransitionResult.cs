using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed outcomes of one consumed market candle; no subsequent lifecycle handoff is performed.</summary>
public abstract class NasdaqPostCompletionActiveImpulseTransitionResult
{
    private NasdaqPostCompletionActiveImpulseTransitionResult(NasdaqPostCompletionActiveImpulseState sourceState,
        StructuralBodyCloseBreakResult protectedTurnBreak, CandleBodyDirection bodyDirection)
    {
        SourceState = sourceState;
        ProtectedTurnBreak = protectedTurnBreak;
        BodyDirection = bodyDirection;
    }

    public NasdaqPostCompletionActiveImpulseState SourceState { get; }
    public StructuralBodyCloseBreakResult ProtectedTurnBreak { get; }
    public CandleBodyDirection BodyDirection { get; }
    public Candle MarketCursor => ProtectedTurnBreak.Candle!;

    public sealed class ProtectedTurnInvalidated : NasdaqPostCompletionActiveImpulseTransitionResult
    {
        internal ProtectedTurnInvalidated(NasdaqPostCompletionActiveImpulseState sourceState,
            StructuralBodyCloseBreakResult protectedTurnBreak, CandleBodyDirection bodyDirection)
            : base(sourceState, protectedTurnBreak, bodyDirection) { }
    }

    public sealed class CorrectionStarted : NasdaqPostCompletionActiveImpulseTransitionResult
    {
        internal CorrectionStarted(NasdaqPostCompletionActiveImpulseState sourceState,
            StructuralBodyCloseBreakResult protectedTurnBreak, CandleBodyDirection bodyDirection,
            NasdaqPostCompletionActiveExtremeMembershipEvent membershipEvent)
            : base(sourceState, protectedTurnBreak, bodyDirection) => MembershipEvent = membershipEvent;

        public NasdaqPostCompletionActiveExtremeMembershipEvent MembershipEvent { get; }
        public Candle CorrectionStartCandle => MembershipEvent.CorrectionStartCandle;
    }

    public sealed class ImpulseRemainsOpen : NasdaqPostCompletionActiveImpulseTransitionResult
    {
        internal ImpulseRemainsOpen(NasdaqPostCompletionActiveImpulseState sourceState,
            StructuralBodyCloseBreakResult protectedTurnBreak, CandleBodyDirection bodyDirection,
            NasdaqPostCompletionActiveImpulseState resultingState)
            : base(sourceState, protectedTurnBreak, bodyDirection) => ResultingState = resultingState;

        public NasdaqPostCompletionActiveImpulseState ResultingState { get; }
    }
}
