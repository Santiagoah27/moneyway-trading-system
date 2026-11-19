namespace MoneyWay.Application.Research.Nasdaq;

public sealed record M5PolicyDifference(string ExclusiveAlgorithmId, string InclusiveAlgorithmId,
    string ExclusivePolicy, string InclusivePolicy, bool ComparableConfirmedReferences,
    bool? ReferenceChanged, bool? PriceChanged, decimal? SignedPriceDifference, decimal? AbsolutePriceDifference,
    DateTimeOffset? ExclusiveAvailabilityUtc, DateTimeOffset? InclusiveAvailabilityUtc);
public sealed record M5PairedPolicyReport(M5SwingComparisonReport ExclusiveV1, M5SwingComparisonReport InclusiveV2,
    IReadOnlyList<M5PolicyDifference> Differences)
{
    public string SchemaVersion => "m5-paired-policy-1";
    public string Classification => "ExperimentalMoneyWayHypotheses_NotMentorConfirmed";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string Limitation => "Fixed-anchor bounded experiment, not contextual structural relevance or a winning/activated strategy. NativeInstrumentPrice only. Human review is not an algorithm input.";
}

/// <summary>Two explicit policies over exactly the same immutable input. No human label enters either calculation.</summary>
public sealed class CompareM5SwingPoliciesUseCase
{
    public M5PairedPolicyReport Execute(M5SwingExperimentInput input)
    {
        var runner = new CompareM5SwingHypothesesUseCase();
        var v1 = runner.Execute(input); var v2 = runner.ExecuteInclusive(input);
        var differences = v1.Results.Zip(v2.Results, (a, b) =>
        {
            var comparable = a.IsExperimentalReferenceConfirmed && b.IsExperimentalReferenceConfirmed;
            var priceChanged = a.ProposedReferencePrice != b.ProposedReferencePrice;
            var identityChanged = !a.ReferenceCandles.Select(c => c.OpenTimeUtc).SequenceEqual(b.ReferenceCandles.Select(c => c.OpenTimeUtc));
            decimal? delta = comparable ? b.ProposedReferencePrice!.Value - a.ProposedReferencePrice!.Value : null;
            return new M5PolicyDifference(a.Algorithm.AlgorithmId, b.Algorithm.AlgorithmId, a.SharedPolicyId, b.SharedPolicyId,
                comparable, comparable ? priceChanged || identityChanged : null, comparable ? priceChanged : null,
                delta, delta is { } d ? Math.Abs(d) : null, a.ConfirmationAtUtc, b.ConfirmationAtUtc);
        }).ToArray();
        return new(v1, v2, Array.AsReadOnly(differences));
    }
}
