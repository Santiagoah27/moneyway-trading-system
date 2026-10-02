namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes the pending phase from an established correction start without reevaluating or consuming candles.</summary>
public sealed class NasdaqPostCompletionExtremeMembershipPendingInitializer
{
    public NasdaqPostCompletionExtremeMembershipPendingState Initialize(
        NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted correctionStarted)
    {
        ArgumentNullException.ThrowIfNull(correctionStarted);
        return new(correctionStarted.MembershipEvent);
    }
}
