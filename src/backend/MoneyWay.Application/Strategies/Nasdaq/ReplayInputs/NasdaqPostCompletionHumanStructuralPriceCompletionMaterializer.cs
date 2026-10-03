namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes the already-resolved 008 candidate without recalculation, market consumption or next-cycle handoff.</summary>
public sealed class NasdaqPostCompletionHumanStructuralPriceCompletionMaterializer
{
    public NasdaqPostCompletionStructuralCompletion Materialize(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate resolvedCandidate)
    {
        ArgumentNullException.ThrowIfNull(resolvedCandidate);
        return new(new NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved(resolvedCandidate));
    }
}
