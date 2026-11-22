using System.Text.Json.Nodes;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed partial class NasdaqEvidenceReadinessTests
{
    private const string ProtocolCommit = "0123456789012345678901234567890123456789";
    private static readonly DateOnly First = new(2026, 11, 23);
    private static readonly DateTimeOffset Frozen = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Assessed = Import.AddHours(2);

    [Fact]
    public void ProtocolFreezeIsCanonicalAndVerifiableAndAmendmentsHaveNewIdentity()
    {
        using var fixture = new Fixture();
        var request = ProtocolRequest();
        var protocol = Freeze(fixture, request);
        Assert.Equal(protocol, Freeze(fixture, request));
        var reordered = request with { Dates = request.Dates.Reverse().ToArray() };
        Assert.Equal(protocol.Sha256, Freeze(fixture, reordered, "copy").Sha256);
        var manifest = NasdaqValidationProtocol.ReadManifest(protocol);
        Assert.Equal("1.1.0", manifest.CandidateConfiguration.GetProperty("candidate").GetProperty("version").GetString());
        Assert.Equal(6, manifest.CandidateConfiguration.GetProperty("h4").GetArrayLength());
        Assert.Equal(EvaluateNasdaqEndToEndResearchUseCase.SequenceConfigurations().Count,
            manifest.CandidateConfiguration.GetProperty("m5Sequence").GetArrayLength());
        Assert.Equal(3, manifest.CandidateConfiguration.GetProperty("stopLoss").GetArrayLength());
        Assert.Equal(4, manifest.CandidateConfiguration.GetProperty("takeProfit").GetArrayLength());
        Assert.Equal(Cutoff, manifest.Selections[0].CutoffUtc);
        Assert.False(manifest.CanonicalAdmissionPermitted);
        Assert.False(manifest.OperationalSignal);
        Assert.Throws<ArgumentException>(() => NasdaqValidationProtocol.ReadManifest(protocol with { Sha256 = new('0', 64) }));
        var wrongConfigurationHash = Write(fixture, "wrong-configuration.json", manifest with { ConfigurationSha256 = new('0', 64) });
        Assert.Throws<ArgumentException>(() => NasdaqValidationProtocol.ReadManifest(wrongConfigurationHash));
        var next = request with
        {
            Version = 2,
            CreatedAtUtc = Frozen.AddMinutes(1),
            FrozenAtUtc = Frozen.AddMinutes(2),
            Dates = [new(First.AddDays(5), NasdaqValidationPartition.Validation)],
            Predecessor = protocol
        };
        var amended = Freeze(fixture, next, "v2");
        Assert.NotEqual(protocol.Sha256, amended.Sha256);
        Assert.Equal(protocol.Sha256, Assert.Single(NasdaqValidationProtocol.ReadManifest(amended).Amendments).Sha256);
        Assert.Equal(protocol, Freeze(fixture, request));
        Assert.Throws<ArgumentException>(() => NasdaqValidationProtocol.Freeze(next with { Version = 1 }, ProtocolCommit));
        Assert.Throws<ArgumentException>(() => NasdaqValidationProtocol.Freeze(next with { Dates = request.Dates }, ProtocolCommit));
    }

    [Fact]
    public void PreservesMissingSelectionsAndRejectsSubstitutionAndCandidateMismatch()
    {
        using var fixture = new Fixture();
        var protocol = Freeze(fixture, ProtocolRequest());
        var missing = Inspect(fixture, new(protocol, Assessed, [], null));
        Assert.Equal(2, missing.TotalSelectedSessions);
        Assert.Null(missing.SelectedSessionsWithAdequateEvidence);
        Assert.All(missing.Sessions, s =>
        {
            Assert.Contains("RegisteredSessionEvidenceMissing", s.Blockers);
            Assert.False(s.EligibleForIndependentEvaluation);
            Assert.Equal("NotEvaluated", s.EvaluationStatus);
        });
        Assert.Equal(First, missing.Sessions[0].OperatingDate);
        Assert.Equal(NasdaqValidationPartition.HeldOut, missing.Sessions[1].Partition);
        var registered = Registered(fixture, "Prospective");
        Assert.Throws<ArgumentException>(() => NasdaqValidationAssessmentService.Assess(
            new(protocol, Assessed, [registered with { CutoffUtc = Cutoff.AddMinutes(1) }], null), ProtocolCommit));
        var manifest = NasdaqValidationProtocol.ReadManifest(protocol);
        var alternate = JsonNode.Parse(manifest.CandidateConfiguration.GetRawText())!;
        alternate["candidate"]!["version"] = "1.0.0";
        var config = System.Text.Json.JsonSerializer.SerializeToElement(alternate);
        var earlier = Write(fixture, "earlier.json", manifest with
        {
            RepositoryCommit = new('a', 40),
            CandidateConfiguration = config,
            ConfigurationSha256 = NasdaqValidationProtocol.Hash(config)
        });
        var changed = Inspect(fixture, new(earlier, Assessed, [registered], null), "changed");
        Assert.Contains("RepositoryCommitMismatch", changed.ConfigurationDiagnostics);
        Assert.Contains("CandidateConfigurationOrVersionMismatch", changed.ConfigurationDiagnostics);
        Assert.False(changed.Sessions[0].EligibleForIndependentEvaluation);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("ChronologicalReview")]
    [InlineData("Validation")]
    [InlineData("HeldOut")]
    [InlineData("MentorExposed")]
    [InlineData("Unknown")]
    public void OnlySourceProspectiveIsEligibleForFuturePartitions(string exposure)
    {
        using var fixture = new Fixture();
        var protocol = Freeze(fixture, ProtocolRequest());
        var registered = Registered(fixture, exposure);
        var report = Inspect(fixture, new(protocol, Assessed, [registered], null));
        var row = report.Sessions[0];
        Assert.Equal(exposure, row.Exposure.ToString());
        Assert.False(row.EligibleForIndependentEvaluation);
        Assert.Equal(NasdaqValidationPartition.Validation, row.Partition);
        if (exposure != "Development") Assert.Null(row.H4ContextAvailable);
        Assert.Contains(exposure == "Unknown" ? "ExposureUnknown" : $"ExposureProtectedForIndependentEvaluation:{exposure}", row.Blockers);
        Assert.Contains("IndependentPreRevealLabelsMissing", row.Blockers);
        Assert.False(report.OperationalSignal);
    }

    [Fact]
    public void UnresolvedSeptemberInventoryRetainsMentorExposureAcrossRenamedRegistrations()
    {
        using var fixture = new Fixture();
        var day = new DateOnly(2026, 9, 8);
        var cutoff = new DateTimeOffset(2026, 9, 8, 16, 30, 0, TimeSpan.Zero);
        var prep = cutoff.AddHours(-3.5);
        var protocol = Freeze(fixture, ProtocolRequest());
        var definitions = new (string Frame, int Minutes, int Count, int Hours)[]
        {
            ("H4", 240, 10, 40), ("H1", 60, 18, 15), ("M5", 5, 114, 6), ("M1", 1, 570, 6)
        };
        var files = definitions.Select(d =>
        {
            var manifest = fixture.Source(d.Frame, d.Minutes, d.Count, prep.AddHours(-d.Hours));
            var folder = Path.GetDirectoryName(manifest["manifestPath"]!.GetValue<string>())!;
            var raw = Pin(Path.Combine(folder, "original.tsv"));
            var normalized = Pin(Path.Combine(folder, "normalized.csv"));
            return new JsonObject
            {
                ["Timeframe"] = d.Frame[1..] + d.Frame[0],
                ["RawPath"] = raw.Path,
                ["RawSHA256"] = raw.Sha256,
                ["NormalizedPath"] = normalized.Path,
                ["NormalizedSHA256"] = normalized.Sha256
            };
        }).ToArray();
        var inventory = Write(fixture, "september-inventory.json", new JsonObject
        {
            ["SessionDate"] = "2026-09-08",
            ["Provider"] = "oanda-global-markets-mt5-demo",
            ["RawSymbol"] = "US100",
            ["Files"] = new JsonArray(files)
        });
        var request = fixture.NewRequest("MentorExposed");
        request["sources"] = new JsonArray(new JsonObject
        {
            ["format"] = "SeptemberMentorInventory",
            ["manifestPath"] = inventory.Path,
            ["manifestSha256"] = inventory.Sha256,
            ["sourceRoot"] = fixture.Root
        });
        request["sessions"] = System.Text.Json.JsonSerializer.SerializeToNode(new[]
        {
            new { operatingDate = day, cutoffUtc = cutoff, exposure = "MentorExposed", requestedStartsUtc =
                definitions.ToDictionary(d => d.Frame, d => prep.AddHours(-d.Hours)) }
        });
        var registered = Bundle(fixture.Register(request, "september")) with { OperatingDate = day, CutoffUtc = cutoff };
        var assessed = Inspect(fixture, new(protocol, Assessed, [registered], null));
        var row = assessed.Sessions.Single(s => s.OperatingDate == day);
        Assert.Equal(NasdaqEvidenceExposure.MentorExposed, row.Exposure);
        Assert.Contains("H4:UnresolvedTimestampOrMissingOriginalSource", row.Blockers);
        Assert.Null(row.H4ContextAvailable);
        Assert.False(row.EligibleForIndependentEvaluation);
        request["datasetId"] = "renamed-september";
        request["sessions"]![0]!["exposure"] = "Prospective";
        Assert.Contains("Exposure classification conflicts", fixture.RegisterError(request, "relabelled"));
    }

    [Theory]
    [InlineData(true, true, "H4:IncompleteRequestedCoverage")]
    [InlineData(false, true, "H4ContextUnavailable")]
    [InlineData(false, false, "ProspectiveLocalAcquisitionEvidenceRequired")]
    public void SeparatesAcquisitionH4CoverageAndConfirmedStructure(bool gap, bool receipt, string blocker)
    {
        using var fixture = new Fixture(h4Gap: gap);
        var protocol = Freeze(fixture, ProtocolRequest());
        var readinessRequest = fixture.NewRequest("Prospective");
        if (!receipt) readinessRequest["localAcquisition"] = null;
        var registered = Bundle(fixture.Register(readinessRequest, "register"));
        var row = Inspect(fixture, new(protocol, Assessed, [registered], null)).Sessions[0];
        Assert.Contains(blocker, row.Blockers);
        Assert.False(row.EligibleForIndependentEvaluation);
        if (!gap) Assert.True(row.SynchronizedInputReady);
        if (!receipt) Assert.Contains("ActualPostFreezeSourceAcquisitionRequired", row.Blockers);
    }

    [Fact]
    public void RetainsReviewerExposureAndTheOriginalJudgmentAcrossPostRevealRevisions()
    {
        using var fixture = new Fixture(structuredH4: true);
        var protocol = Freeze(fixture, ProtocolRequest());
        var registered = Registered(fixture, "Prospective");
        var proof = new NasdaqEvidenceFile(fixture.Evidence.path, fixture.Evidence.sha256);
        var admission = new NasdaqValidationAdmission(First, Cutoff, registered.Readiness.Sha256, Cutoff, Import, proof);
        var label = new NasdaqValidationLabel(1, "synthetic-target", NasdaqValidationLabelKind.NoReference, Import.AddMinutes(15), proof);
        var reviewer = new NasdaqValidationReview("synthetic-independent", NasdaqReviewerExposure.SelfDeclaredIndependent, Import, null, [label]);
        var initially = registered with
        {
            Availability = Write(fixture, "availability.json", admission),
            Review = ReviewPin(fixture, registered, reviewer, "review-1.json")
        };
        var prompt = new NasdaqValidationAssessmentRequest(protocol, Assessed, [initially], null);
        var first = Inspect(fixture, prompt);
        Assert.True(first.Sessions[0].H4ContextAvailable);
        Assert.True(first.Sessions[0].EligibleForIndependentEvaluation);
        Assert.Equal("NotEvaluated", first.EvaluationStatus);
        var revised = reviewer with
        {
            OutcomesRevealedAtUtc = Import.AddMinutes(30),
            Labels = [label, label with { Revision = 2, Kind = NasdaqValidationLabelKind.Reference, ReviewedAtUtc = Import.AddMinutes(45) }]
        };
        var second = Inspect(fixture, prompt with
        {
            Registrations = [initially with
            { Review = ReviewPin(fixture, registered, revised, "review-2.json") }]
        });
        var history = Assert.Single(second.Sessions[0].Reviews).History;
        Assert.Equal("IndependentPreReveal", history[0].Classification);
        Assert.Equal("PostRevealCorrection", history[1].Classification);
        Assert.Equal(NasdaqValidationLabelKind.NoReference, history[0].Label.Kind);
        Assert.True(second.Sessions[0].EligibleForIndependentEvaluation);
        var exposed = revised with { ReviewerId = "synthetic-exposed", Exposure = NasdaqReviewerExposure.PreviouslyExposed };
        var third = Inspect(fixture, prompt with
        {
            Registrations = [initially with
            { Review = ReviewPin(fixture, registered, exposed, "review-3.json") }]
        });
        Assert.Contains("PreviouslyExposedReviewerExcluded", third.Sessions[0].Blockers);
        var reimport = fixture.NewRequest("Prospective");
        reimport["datasetId"] = "renamed-prospective";
        var renamed = Bundle(fixture.Register(reimport, "renamed"));
        var attemptedReset = exposed with { Exposure = NasdaqReviewerExposure.SelfDeclaredIndependent };
        var fourth = Inspect(fixture, prompt with
        {
            Registrations = [renamed with
            { Review = ReviewPin(fixture, renamed, attemptedReset, "review-4.json") }]
        });
        Assert.Equal(NasdaqReviewerExposure.PreviouslyExposed,
            fourth.Sessions[0].Reviews.Single(r => r.ReviewerId == exposed.ReviewerId).Exposure);
        Assert.False(fourth.Sessions[0].EligibleForIndependentEvaluation);
        Assert.Contains("IndependentPreRevealLabelsMissing", fourth.Sessions[0].Blockers);
        Assert.Equal("NotEvaluated", fourth.Sessions[0].EvaluationStatus);
    }

    [Fact]
    public void FutureRowsDoNotChangePrefixReadinessAndFutureEvidenceCannotBeBackdated()
    {
        using var fixture = new Fixture();
        var protocol = Freeze(fixture, ProtocolRequest());
        var request = fixture.NewRequest("Development");
        var first = Bundle(fixture.Register(request, "baseline"));
        var augmented = fixture.NewRequest("Development");
        augmented["sources"]!.AsArray().Add(fixture.Source("M1", 1, 5, Cutoff, price: 300));
        var second = Bundle(fixture.Register(augmented, "future"));
        var initial = Inspect(fixture, new(protocol, Assessed, [first], null), "initial-ledger").Sessions[0];
        var later = Inspect(fixture, new(protocol, Assessed, [second], null), "future-ledger").Sessions[0];
        Assert.Equal(initial.Blockers, later.Blockers);
        Assert.Equal(initial.SynchronizedInputReady, later.SynchronizedInputReady);
        Assert.Equal(initial.H4ContextAvailable, later.H4ContextAvailable);
        Assert.Equal(initial.Source!.Sources, later.Source!.Sources);
        var proof = new NasdaqEvidenceFile(fixture.Evidence.path, fixture.Evidence.sha256);
        var inadmissible = Write(fixture, "future-proof.json",
            new NasdaqValidationAdmission(First, Cutoff, first.Readiness.Sha256, Cutoff.AddTicks(1), Import, proof));
        Assert.Throws<ArgumentException>(() => NasdaqValidationAssessmentService.Assess(
            new(protocol, Assessed, [first with { Availability = inadmissible }], null), ProtocolCommit));
    }

    private static NasdaqValidationFreezeRequest ProtocolRequest() =>
        new("SYNTHETIC-NASDAQ-PROTOCOL", 1, Frozen.AddMinutes(-5), Frozen,
            [new(First, NasdaqValidationPartition.Validation), new(First.AddDays(1), NasdaqValidationPartition.HeldOut)], null);

    private static NasdaqEvidenceFile Freeze(Fixture fixture, NasdaqValidationFreezeRequest request, string name = "frozen") =>
        Pin(Command(fixture, "freeze", request, name));

    private static NasdaqValidationAssessment Inspect(Fixture fixture, NasdaqValidationAssessmentRequest request, string name = "ledger") =>
        NasdaqValidationProtocol.ReadCanonical<NasdaqValidationAssessment>(Pin(Command(fixture, "assess", request, name)));

    private static NasdaqValidationRegistration Registered(Fixture fixture, string exposure) =>
        Bundle(fixture.Register(fixture.NewRequest(exposure), "registration"));

    private static NasdaqValidationRegistration Bundle(string directory) =>
        new(First, Cutoff, Pin(Path.Combine(directory, "registration.json")), Pin(Path.Combine(directory, "readiness.json")), null, null);

    private static NasdaqEvidenceFile ReviewPin(Fixture fixture, NasdaqValidationRegistration source, NasdaqValidationReview review, string name) =>
        Write(fixture, name, new NasdaqValidationBoundReview(First, Cutoff, source.Readiness.Sha256, review));

    private static NasdaqEvidenceFile Write<T>(Fixture fixture, string name, T value)
    {
        var path = Path.Combine(fixture.Root, name);
        File.WriteAllBytes(path, NasdaqValidationProtocol.Bytes(value));
        return Pin(path);
    }

    private static NasdaqEvidenceFile Pin(string path) => new(path, NasdaqEvidenceSources.Hash(File.ReadAllBytes(path)));

    private static string Command<T>(Fixture fixture, string mode, T request, string name)
    {
        var input = Write(fixture, name + "-request.json", request);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = new NasdaqValidationProtocolCommand(_ => ProtocolCommit).Execute(
            ["nasdaq-validation-protocol", mode, "--request", input.Path, "--output", Path.Combine(fixture.Root, name)], output, error);
        Assert.True(code == 0, error.ToString());
        return output.ToString().Trim();
    }
}
