namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Prepares active-extreme geometry from resolved members without advancing the pending lifecycle.</summary>
public sealed class NasdaqPostCompletionExtremeGeometryPreparer
{
    private readonly NasdaqHumanPostCompletionActiveExtremeGeometryCalculator geometryCalculator = new();

    public NasdaqPostCompletionExtremeGeometryReady Prepare(
        NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved membersResolved)
    {
        ArgumentNullException.ThrowIfNull(membersResolved);
        var geometry = geometryCalculator.Evaluate(membersResolved.Resolution);
        return new(membersResolved, geometry);
    }
}
