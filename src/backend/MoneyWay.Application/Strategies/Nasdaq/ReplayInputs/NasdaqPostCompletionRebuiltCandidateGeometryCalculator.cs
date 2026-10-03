namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates geometry only from authoritative resolved members; no evidence lookup or market step occurs.</summary>
public sealed class NasdaqPostCompletionRebuiltCandidateGeometryCalculator
{
    private readonly NasdaqRebuiltCandidateGeometryCalculator geometryCalculator = new();

    public NasdaqPostCompletionRebuiltCandidateGeometry Evaluate(NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved membersResolved)
    {
        ArgumentNullException.ThrowIfNull(membersResolved);
        var pending = membersResolved.PendingState;
        var geometry = geometryCalculator.Evaluate(membersResolved.SelectedMembers, pending.CandidateSide, pending.KnownProtectionAnchor);
        return new(membersResolved, geometry);
    }
}
