using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;

namespace MoneyWay.Worker;

internal enum NasdaqValidationPartition { Validation, HeldOut }
internal sealed record NasdaqValidationDate(DateOnly OperatingDate, [property: JsonRequired] NasdaqValidationPartition Partition);
internal sealed record NasdaqValidationSelection(DateOnly OperatingDate, NasdaqValidationPartition Partition, DateTimeOffset CutoffUtc);
internal sealed record NasdaqValidationFreezeRequest(string ProtocolId, int Version, DateTimeOffset CreatedAtUtc,
    DateTimeOffset FrozenAtUtc, IReadOnlyList<NasdaqValidationDate> Dates, NasdaqEvidenceFile? Predecessor);
internal sealed record NasdaqValidationAmendment(int Version, string Sha256, string Reason, IReadOnlyList<DateOnly> SelectedDates);
internal sealed record NasdaqValidationMetric(string Name, string Numerator, string Denominator);
internal sealed record NasdaqValidationManifest(string ProtocolId, int Version, DateTimeOffset CreatedAtUtc,
    DateTimeOffset FrozenAtUtc, string RepositoryCommit, JsonElement CandidateConfiguration, string ConfigurationSha256,
    DateOnly CollectionStartsOn, string PartitionPolicy, string SourcePolicy, string HumanReviewPolicy,
    IReadOnlyList<NasdaqValidationSelection> Selections, IReadOnlyList<NasdaqValidationAmendment> Amendments,
    IReadOnlyList<string> Unresolved, IReadOnlyList<NasdaqValidationMetric> Metrics)
{
    public string Schema => "moneyway.nasdaq-validation-protocol.v1";
    public string Status => "Frozen";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

internal static class NasdaqValidationProtocol
{
    internal static readonly DateOnly FirstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        NasdaqEvidenceReadinessCommand.ProspectiveStart, NasdaqSupervisedSessionPolicy.Zone).DateTime);
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new NasdaqEvidenceReadinessCommand.ExplicitUtcConverter() }
    };

    private static readonly string[] Unresolved =
    [
        "IndependentIntegratedReferenceLabelsAndMatchingUniverse",
        "MentorConfirmedDeterministicH4AndM5StructuralSelection",
        "ExecutableStopLossTakeProfitAndRiskPolicies",
        "SeptemberMentorInventoryFullIntervalUtcMapping",
        "HistoricalPublicationRequiresSeparateSourceEvidence"
    ];

    private static readonly NasdaqValidationMetric[] Metrics =
    [
        new("SelectedSessions", "Distinct frozen operating dates including missing sessions", "All frozen selections"),
        new("AdequateSourceSessions", "Selected sessions with synchronized source coverage and verified UTC mapping", "All frozen selections"),
        new("H4ContextSessions", "Adequate source sessions with observable experimental H4 context", "Adequate source sessions with H4 evaluated"),
        new("LiquidityEpisodes", "Distinct operating date and episode ID with observed liquidity take", "Evaluated sessions per partition"),
        new("EligibleConfirmationEvents", "Distinct operating date and selected eligible confirmation association key", "Evaluated liquidity episodes per configuration"),
        new("M1Realignments", "Distinct operating date, observable timestamp and direction", "Evaluated M1 anchors per configuration"),
        new("CompleteChains", "Context associations with CompletedAtUtc under causal projection", "Evaluated context configuration associations"),
        new("InvalidatedBeforeCompletion", "Context associations with ContextInvalidatedAtRealignment", "Evaluated context configuration associations"),
        new("Ambiguous", "Context associations with Ambiguous", "Evaluated context configuration associations"),
        new("InsufficientEvidence", "Context associations with InsufficientEvidence", "Evaluated context configuration associations"),
        new("HumanLabelCoverage", "Pre-reveal independently reviewed Reference or NoReference target IDs", "Predeclared reference target IDs"),
        new("AgreementAndDisagreement", "Agreements and disagreements per scoped target, separately", "Matched evaluable targets with independent labels"),
        new("FalsePositives", "Detector positive among independently labelled NoReference targets", "Matched evaluable NoReference targets"),
        new("MissedDetections", "Detector absent among independently labelled Reference targets", "Matched evaluable Reference targets"),
        new("ConfirmationLatency", "Detector confirmation close minus matched reviewed confirmation close", "Matched references with both observable close timestamps"),
        new("ConfigurationAssociations", "Distinct configuration associations, including distinct H4 subprofiles", "Evaluated sessions per partition")
    ];

    internal static JsonElement Configuration() => JsonSerializer.SerializeToElement(new
    {
        Candidate = new { Id = NasdaqEndToEndResearchResult.ResearchId, Version = NasdaqEndToEndResearchResult.ResearchVersion },
        Components = new (string Id, string Version)[]
        {
            (H4MarketStructureObservation.ResearchId, H4MarketStructureObservation.ResearchVersion),
            (H4ContextObservation.ResearchId, H4ContextObservation.ResearchVersion),
            (ContextualLiquidityTrackingResult.ResearchId, ContextualLiquidityTrackingResult.ResearchVersion),
            (ContextualSwingScReport.ResearchId, ContextualSwingScReport.ResearchVersion),
            (M5ContextualSequenceReport.ResearchId, M5ContextualSequenceReport.ResearchVersion),
            (M5FvgReport.ResearchId, M5FvgReport.ResearchVersion),
            (M1RealignmentReport.ResearchId, M1RealignmentReport.ResearchVersion),
            (M5StructuralStopResearchReport.ResearchId, M5StructuralStopResearchReport.ResearchVersion),
            (StructuralTakeProfitResearchReport.ResearchId, StructuralTakeProfitResearchReport.ResearchVersion)
        }.Select(x => new { x.Id, x.Version }).ToArray(),
        Provider = NasdaqEvidenceSources.Provider.Value,
        Instrument = NasdaqEvidenceSources.Instrument.Value,
        Timeframes = NasdaqEvidenceCoverage.Timeframes.Keys.ToArray(),
        ReplayStrategy = MoneyWayNasdaqStrategyDefinition.Instance.Version.Value,
        SessionLiquidityCandidate = MoneyWayNasdaqSessionCandidate.Version.Value,
        Window = new
        {
            NasdaqTradingWindowAlignment.ConfigurationId,
            TimeZone = "America/Bogota",
            NasdaqTradingWindowAlignment.Preparation,
            NasdaqTradingWindowAlignment.Opening,
            NasdaqTradingWindowAlignment.Closing,
            Inclusion = "StartInclusive_EndExclusive"
        },
        H4 = (from profile in H4PivotProfile.InitialResearchProfiles
              from horizon in EvaluateNasdaqEndToEndResearchUseCase.H4ContextHorizons
              select new { Id = H4ContextObservation.ProfileId(profile, horizon), Profile = profile, Horizon = horizon }).ToArray(),
        M5EventPolicies = CompareContextualSwingScUseCase.EventPolicies.Select(CompareContextualSwingScUseCase.EventPolicyId).ToArray(),
        M5ScFamilies = CompareContextualSwingScUseCase.SwingFamilies,
        M5ScSwingVariants = CompareM5SwingHypothesesUseCase.InclusiveAlgorithms,
        M5Sequence = EvaluateNasdaqEndToEndResearchUseCase.SequenceConfigurations(),
        M1 = Enum.GetValues<M1RealignmentProfile>(),
        StopLoss = M5StructuralPivotProfile.Initial,
        TakeProfit = CompareNasdaqStructuralTakeProfitUseCase.ProfilesFor(M5StructuralPivotProfile.Initial)
    }, Json);

    internal static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    internal static string Hash<T>(T value) => NasdaqEvidenceSources.Hash(Bytes(value));
    internal static T Read<T>(byte[] bytes) where T : class =>
        JsonSerializer.Deserialize<T>(bytes.AsSpan().StartsWith("\uFEFF"u8) ? bytes.AsSpan(3) : bytes, Json)
        ?? throw new ArgumentException("Explicit validation protocol JSON is required.");

    internal static T ReadCanonical<T>(NasdaqEvidenceFile pin) where T : class
    {
        var bytes = NasdaqEvidenceSources.ReadPinned(pin.Path, pin.Sha256);
        var value = Read<T>(bytes);
        if (!bytes.AsSpan().SequenceEqual(Bytes(value)))
            throw new ArgumentException("Artifact differs from canonical protocol serialization.");
        return value;
    }

    internal static NasdaqValidationManifest Freeze(NasdaqValidationFreezeRequest request, string commit)
    {
        if (string.IsNullOrWhiteSpace(request.ProtocolId) ||
            request.ProtocolId.Any(c => c is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '-')) ||
            !ValidCommit(commit) || request.CreatedAtUtc == default || request.CreatedAtUtc > request.FrozenAtUtc ||
            request.Dates is not { Count: > 0 } || request.Dates.Any(d => d is null) ||
            request.Dates.Select(d => d.OperatingDate).Distinct().Count() != request.Dates.Count)
            throw new ArgumentException("Explicit protocol identity, documentary UTC times, revision and distinct dates are required.");
        var frozenDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.FrozenAtUtc, NasdaqSupervisedSessionPolicy.Zone).DateTime);
        var dates = request.Dates.OrderBy(d => d.OperatingDate).ToArray();
        if (dates.Any(d => d.OperatingDate < FirstDate || d.OperatingDate <= frozenDay || !Enum.IsDefined(d.Partition)) ||
            !dates.Select(d => d.Partition).Order().SequenceEqual(dates.Select(d => d.Partition)))
            throw new ArgumentException("Select future Bogotá operating dates, with Validation preceding HeldOut.");
        var amendments = new List<NasdaqValidationAmendment>();
        if (request.Predecessor is { } pin)
        {
            var prior = ReadManifest(pin);
            var priorDates = prior.Selections.Select(s => s.OperatingDate)
                .Concat(prior.Amendments.SelectMany(a => a.SelectedDates)).ToHashSet();
            if (request.ProtocolId != prior.ProtocolId || request.Version != prior.Version + 1 ||
                request.CreatedAtUtc < prior.FrozenAtUtc || request.FrozenAtUtc <= prior.FrozenAtUtc ||
                dates.Any(d => priorDates.Contains(d.OperatingDate)))
                throw new ArgumentException("Amendments require the next version, a later freeze and fresh selection dates.");
            amendments.AddRange(prior.Amendments);
            amendments.Add(new(prior.Version, pin.Sha256, "PredecessorRetainedWithoutModification",
                prior.Selections.Select(s => s.OperatingDate).ToArray()));
        }
        else if (request.Version != 1)
            throw new ArgumentException("Version 1 starts a protocol. Later versions require a pinned predecessor.");

        var selections = dates.Select(d => new NasdaqValidationSelection(d.OperatingDate, d.Partition,
            TimeZoneInfo.ConvertTimeToUtc(d.OperatingDate.ToDateTime(NasdaqTradingWindowAlignment.Closing),
                NasdaqSupervisedSessionPolicy.Zone))).ToArray();
        var configuration = Configuration();
        return new(request.ProtocolId, request.Version, request.CreatedAtUtc, request.FrozenAtUtc,
            commit, configuration, Hash(configuration), FirstDate,
            "ExplicitFutureBogotaDates_ValidationBeforeHeldOut_NoSubstitution",
            "PinnedOriginalMT5_FullIntervalReviewedUtcMapping_SeparateAvailabilityEvidence",
            "DocumentedAcquisitionAndExposure_IndependentPreRevealJudgment_AppendOnlyRevisions",
            selections, amendments, Unresolved, Metrics);
    }

    internal static NasdaqValidationManifest ReadManifest(NasdaqEvidenceFile pin)
    {
        var manifest = ReadCanonical<NasdaqValidationManifest>(pin);
        if (!ValidCommit(manifest.RepositoryCommit) || Hash(manifest.CandidateConfiguration) != manifest.ConfigurationSha256)
            throw new ArgumentException("Frozen configuration fingerprint mismatch.");
        return manifest;
    }

    internal static bool ValidCommit(string value) =>
        value is { Length: 40 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
