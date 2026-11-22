using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal enum NasdaqReviewerExposure { Unknown, SelfDeclaredIndependent, PreviouslyExposed }
internal enum NasdaqValidationLabelKind { Reference, NoReference, Ambiguous, InsufficientSourceEvidence, Skip }
internal sealed record NasdaqValidationLabel(int Revision, string TargetId, [property: JsonRequired] NasdaqValidationLabelKind Kind,
    DateTimeOffset ReviewedAtUtc, NasdaqEvidenceFile SourceReference);
internal sealed record NasdaqValidationReview(string ReviewerId, [property: JsonRequired] NasdaqReviewerExposure Exposure,
    DateTimeOffset DeclaredAtUtc, [property: JsonRequired] DateTimeOffset? OutcomesRevealedAtUtc,
    IReadOnlyList<NasdaqValidationLabel> Labels);
internal sealed record NasdaqValidationAdmission(DateOnly OperatingDate, DateTimeOffset CutoffUtc,
    string ReadinessSha256, DateTimeOffset LastObservedAvailableAtUtc, DateTimeOffset VerifiedAtUtc, NasdaqEvidenceFile Evidence);
internal sealed record NasdaqValidationRegistration(DateOnly OperatingDate, DateTimeOffset CutoffUtc,
    NasdaqEvidenceFile Registration, NasdaqEvidenceFile Readiness, NasdaqEvidenceFile? Availability, NasdaqEvidenceFile? Review);
internal sealed record NasdaqValidationAssessmentRequest(NasdaqEvidenceFile Protocol, DateTimeOffset AssessedAtUtc,
    IReadOnlyList<NasdaqValidationRegistration> Registrations, NasdaqEvidenceFile? PreviousAssessment);
internal sealed record NasdaqValidationLabelRecord(NasdaqValidationLabel Label, string Classification, string ReadinessSha256);
internal sealed record NasdaqValidationReviewRecord(string ReviewerId, NasdaqReviewerExposure Exposure,
    DateTimeOffset DeclaredAtUtc, DateTimeOffset? OutcomesRevealedAtUtc, string EvidenceSha256,
    IReadOnlyList<NasdaqValidationLabelRecord> History);
internal sealed record NasdaqValidationSourceRecord(string DatasetId, string RegistrationSha256, string ReadinessSha256,
    DateTimeOffset RegisteredAtUtc, IReadOnlyList<NasdaqEvidenceSource> Sources);
internal sealed record NasdaqValidationSessionRecord(DateOnly OperatingDate, DateTimeOffset CutoffUtc,
    NasdaqValidationPartition? Partition, NasdaqEvidenceExposure Exposure, NasdaqValidationSourceRecord? Source,
    bool? SynchronizedInputReady, bool? H4ContextAvailable, NasdaqValidationAdmission? Availability,
    IReadOnlyList<NasdaqValidationReviewRecord> Reviews, bool EligibleForIndependentEvaluation,
    IReadOnlyList<string> Blockers)
{
    public string EvaluationStatus => "NotEvaluated";
}

internal sealed record NasdaqValidationAssessment(string ProtocolId, int ProtocolVersion, string ProtocolSha256,
    string? PreviousAssessmentSha256, DateTimeOffset AssessedAtUtc, string CurrentRepositoryCommit,
    string CurrentConfigurationSha256, IReadOnlyList<string> ConfigurationDiagnostics,
    IReadOnlyList<NasdaqValidationSessionRecord> Sessions)
{
    public string Schema => "moneyway.nasdaq-validation-assessment.v1";
    public string EvaluationStatus => "NotEvaluated";
    public string EvaluationMetricsStatus => "Unavailable_NotEvaluated";
    public int TotalSelectedSessions => Sessions.Count(s => s.Partition is not null);
    public int? SelectedSessionsWithAdequateEvidence => Sessions.Any(s => s.Partition is not null && s.SynchronizedInputReady is not null)
        ? Sessions.Count(s => s.Partition is not null && s.SynchronizedInputReady == true) : null;
    public int? SelectedSessionsWithH4Context => Sessions.Any(s => s.Partition is not null && s.H4ContextAvailable is not null)
        ? Sessions.Count(s => s.Partition is not null && s.H4ContextAvailable == true) : null;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

internal static class NasdaqValidationAssessmentService
{
    internal static NasdaqValidationAssessment Assess(NasdaqValidationAssessmentRequest request, string commit)
    {
        ArgumentNullException.ThrowIfNull(request.Protocol);
        var protocol = NasdaqValidationProtocol.ReadManifest(request.Protocol);
        if (request.AssessedAtUtc < protocol.FrozenAtUtc || request.Registrations is null ||
            request.Registrations.Any(r => r is null || r.OperatingDate == default || r.CutoffUtc == default) ||
            request.Registrations.Select(r => r.OperatingDate).Distinct().Count() != request.Registrations.Count)
            throw new ArgumentException("Assess after freeze with at most one exact cutoff per operating date.");

        var previous = request.PreviousAssessment is { } prior
            ? NasdaqValidationProtocol.ReadCanonical<NasdaqValidationAssessment>(prior) : null;
        if (previous is not null && (previous.ProtocolId != protocol.ProtocolId ||
            previous.ProtocolVersion > protocol.Version || previous.AssessedAtUtc > request.AssessedAtUtc ||
            (previous.ProtocolVersion == protocol.Version ? previous.ProtocolSha256 != request.Protocol.Sha256 :
                !protocol.Amendments.Any(a => a.Version == previous.ProtocolVersion && a.Sha256 == previous.ProtocolSha256))))
            throw new ArgumentException("The previous assessment must belong to this frozen protocol lineage.");
        var configHash = NasdaqValidationProtocol.Hash(NasdaqValidationProtocol.Configuration());
        var diagnostics = new List<string>();
        if (protocol.RepositoryCommit != commit) diagnostics.Add("RepositoryCommitMismatch");
        if (protocol.ConfigurationSha256 != configHash) diagnostics.Add("CandidateConfigurationOrVersionMismatch");

        var registrations = request.Registrations.ToDictionary(r => r.OperatingDate);
        var selections = protocol.Selections.ToDictionary(s => s.OperatingDate);
        var dates = selections.Keys.Concat(registrations.Keys)
            .Concat((previous?.Sessions ?? []).Select(s => s.OperatingDate)).Distinct().Order();
        var sessions = dates.Select(day =>
        {
            selections.TryGetValue(day, out var selection);
            registrations.TryGetValue(day, out var registration);
            var earlier = previous?.Sessions.SingleOrDefault(s => s.OperatingDate == day);
            var cutoff = selection?.CutoffUtc ?? registration?.CutoffUtc ?? earlier!.CutoffUtc;
            if (registration is not null && (registration.CutoffUtc != cutoff ||
                earlier is not null && earlier.CutoffUtc != cutoff))
                throw new ArgumentException($"Use the exact frozen UTC cutoff for {day:yyyy-MM-dd}.");
            return AssessSession(day, cutoff, selection?.Partition, registration, earlier, request, protocol, diagnostics);
        }).ToArray();
        return new(protocol.ProtocolId, protocol.Version, request.Protocol.Sha256, request.PreviousAssessment?.Sha256,
            request.AssessedAtUtc, commit, configHash, diagnostics, sessions);
    }

    private static NasdaqValidationSessionRecord AssessSession(DateOnly day, DateTimeOffset cutoff, NasdaqValidationPartition? partition,
        NasdaqValidationRegistration? registration, NasdaqValidationSessionRecord? previous, NasdaqValidationAssessmentRequest request,
        NasdaqValidationManifest protocol, IReadOnlyList<string> diagnostics)
    {
        var blockers = new List<string>(diagnostics);
        if (partition is null) blockers.Add("SessionOutsideFrozenSelection");
        var exposure = NasdaqEvidenceReadinessCommand.KnownExposure(day) ?? previous?.Exposure ?? NasdaqEvidenceExposure.Unknown;
        NasdaqEvidenceSessionReadiness? session = null;
        var source = previous?.Source;
        NasdaqValidationAdmission? availability = null;
        var reviews = (previous?.Reviews ?? []).ToList();
        if (registration is not null)
        {
            var registrationBytes = Pinned(registration.Registration);
            var submitted = NasdaqEvidenceReadinessCommand.ReadRegistration(registrationBytes);
            var sourceSession = submitted.Request.Sessions?.SingleOrDefault(s => s?.OperatingDate == day && s.CutoffUtc == cutoff)
                ?? throw new ArgumentException("No exact session identity in the pinned registration.");
            var exposureLocked = exposure != NasdaqEvidenceExposure.Prospective &&
                (exposure != NasdaqEvidenceExposure.Unknown || previous?.Source is not null);
            if (exposureLocked && sourceSession.Exposure != exposure)
                throw new ArgumentException("The registered operating date retains its previous exposure.");
            var evidence = NasdaqEvidenceReadinessCommand.VerifyReadiness(registrationBytes, Pinned(registration.Readiness));
            session = evidence.Sessions.SingleOrDefault(s => s.Session.OperatingDate == day && s.Session.CutoffUtc == cutoff)
                ?? throw new ArgumentException("No exact session identity in verified readiness.");
            if (evidence.RegisteredAtUtc > request.AssessedAtUtc || cutoff > evidence.RegisteredAtUtc)
                throw new ArgumentException("Assessment chronology must include the source cutoff and registration.");
            exposure = PreserveExposure(exposure, session.Session.Exposure, previous?.Source is not null);
            if (exposure != session.Session.Exposure)
                blockers.Add("RetainedExposurePreventsReclassification");
            // Record only sources with observable prefix coverage for the selected day.
            var sources = evidence.Sources.Where(s => session.Frames.Any(f => f.Timeframe == s.Timeframe &&
                s.FirstOpenUtc + NasdaqEvidenceCoverage.Duration(NasdaqEvidenceCoverage.Timeframes[s.Timeframe]) <= f.RequestedEndUtc &&
                s.LastNominalCloseUtc > f.RequestedStartUtc)).ToArray();
            source = new(evidence.DatasetId, registration.Registration.Sha256, registration.Readiness.Sha256,
                evidence.RegisteredAtUtc, sources);
            blockers.AddRange(session.Blockers);
            if (exposure == NasdaqEvidenceExposure.Prospective &&
                (sources.Length == 0 || sources.Any(s => s.LocalAcquisition is null ||
                    s.LocalAcquisition.AcquiredAtUtc <= protocol.FrozenAtUtc)))
                blockers.Add("ActualPostFreezeSourceAcquisitionRequired");
            if (registration.Availability is { } pin)
            {
                availability = NasdaqValidationProtocol.Read<NasdaqValidationAdmission>(Pinned(pin));
                var lastNominalClose = session.Frames.Max(f => f.LastNominalCloseUtc);
                if (availability.OperatingDate != day || availability.CutoffUtc != cutoff ||
                    availability.ReadinessSha256 != registration.Readiness.Sha256 ||
                    lastNominalClose is null || availability.LastObservedAvailableAtUtc < lastNominalClose ||
                    availability.LastObservedAvailableAtUtc > cutoff ||
                    availability.VerifiedAtUtc < cutoff || availability.VerifiedAtUtc > request.AssessedAtUtc)
                    throw new ArgumentException("Availability attestation must bind to this readiness, session and observed cutoff.");
                _ = Pinned(availability.Evidence);
            }
            if (registration.Review is { } reviewPin)
            {
                var review = ReadReview(reviewPin, day, cutoff, protocol.FrozenAtUtc, request.AssessedAtUtc,
                    registration.Readiness.Sha256, reviews);
                reviews.RemoveAll(r => r.ReviewerId == review.ReviewerId);
                reviews.Add(review);
            }
        }
        else blockers.Add("RegisteredSessionEvidenceMissing");

        if (exposure != NasdaqEvidenceExposure.Prospective)
            blockers.Add(exposure == NasdaqEvidenceExposure.Unknown ? "ExposureUnknown" : $"ExposureProtectedForIndependentEvaluation:{exposure}");
        if (availability is null) blockers.Add("IndependentTimestampAvailabilityEvidenceMissing");
        var hasIndependentLabel = registration is not null && reviews.Any(r =>
            r.Exposure == NasdaqReviewerExposure.SelfDeclaredIndependent && r.History
                .Where(l => l.Classification == "IndependentPreReveal" && l.ReadinessSha256 == source?.ReadinessSha256)
                .GroupBy(l => l.Label.TargetId, StringComparer.Ordinal)
                .Any(g => g.Last().Label.Kind is NasdaqValidationLabelKind.Reference or NasdaqValidationLabelKind.NoReference));
        if (!hasIndependentLabel) blockers.Add("IndependentPreRevealLabelsMissing");
        if (reviews.Any(r => r.Exposure == NasdaqReviewerExposure.PreviouslyExposed))
            blockers.Add("PreviouslyExposedReviewerExcluded");
        var h4 = session?.H4Profiles.Any(p => p.ContextEvaluationCompleted) == true
            ? session.H4Profiles.Any(p => p.ContextAvailable) : (bool?)null;
        var eligible = blockers.All(b => b == "PreviouslyExposedReviewerExcluded") &&
            session?.SynchronizedInputReady == true && h4 == true;
        return new(day, cutoff, partition, exposure, source, session?.SynchronizedInputReady, h4, availability,
            reviews.OrderBy(r => r.ReviewerId, StringComparer.Ordinal).ToArray(), eligible, blockers.Distinct().ToArray());
    }

    private static NasdaqEvidenceExposure PreserveExposure(NasdaqEvidenceExposure previous, NasdaqEvidenceExposure current,
        bool previouslyRegistered)
    {
        if (previous == current) return previous;
        if (previous == NasdaqEvidenceExposure.Unknown && !previouslyRegistered) return current;
        if (previous == NasdaqEvidenceExposure.Prospective) return current;
        return previous;
    }

    private static byte[] Pinned(NasdaqEvidenceFile pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        return NasdaqEvidenceSources.ReadPinned(pin.Path, pin.Sha256);
    }

    private static NasdaqValidationReviewRecord ReadReview(NasdaqEvidenceFile pin, DateOnly day, DateTimeOffset cutoff,
        DateTimeOffset frozenAt, DateTimeOffset assessedAt, string readinessSha256, IReadOnlyList<NasdaqValidationReviewRecord> reviews)
    {
        var document = NasdaqValidationProtocol.Read<NasdaqValidationBoundReview>(Pinned(pin));
        if (document.OperatingDate != day || document.CutoffUtc != cutoff || document.ReadinessSha256 != readinessSha256)
            throw new ArgumentException("Review must bind to the exact registered source and cutoff.");
        var review = document.Review;
        if (review is null || string.IsNullOrWhiteSpace(review.ReviewerId) || review.ReviewerId.Trim() != review.ReviewerId ||
            review.DeclaredAtUtc == default || review.DeclaredAtUtc > assessedAt || review.Labels is null ||
            review.OutcomesRevealedAtUtc < review.DeclaredAtUtc || review.OutcomesRevealedAtUtc > assessedAt)
            throw new ArgumentException("Explicit reviewer, exposure declaration, visibility and chronology required.");
        var earlier = reviews.SingleOrDefault(r => r.ReviewerId == review.ReviewerId);
        if (earlier is not null && (earlier.DeclaredAtUtc != review.DeclaredAtUtc ||
            earlier.OutcomesRevealedAtUtc is not null && earlier.OutcomesRevealedAtUtc != review.OutcomesRevealedAtUtc ||
            earlier.History.Select(r => r.Label).SequenceEqual(review.Labels.Take(earlier.History.Count)) is false))
            throw new ArgumentException("Review revisions and first revelation are append-only, including across re-imports.");
        var exposure = earlier?.Exposure is NasdaqReviewerExposure.PreviouslyExposed ? earlier.Exposure : review.Exposure;
        if (earlier?.Exposure == NasdaqReviewerExposure.Unknown && exposure == NasdaqReviewerExposure.SelfDeclaredIndependent)
            throw new ArgumentException("A previous unknown reviewer exposure cannot be silently upgraded.");
        var history = new List<NasdaqValidationLabelRecord>();
        var last = cutoff > review.DeclaredAtUtc ? cutoff : review.DeclaredAtUtc;
        foreach (var label in review.Labels)
        {
            if (label is null || label.Revision != history.Count + 1 || string.IsNullOrWhiteSpace(label.TargetId) ||
                label.ReviewedAtUtc < last || label.ReviewedAtUtc > assessedAt)
                throw new ArgumentException("Label revisions require exact source references and chronological revision numbers.");
            _ = Pinned(label.SourceReference);
            var classification = review.OutcomesRevealedAtUtc <= label.ReviewedAtUtc ? "PostRevealCorrection" :
                exposure == NasdaqReviewerExposure.PreviouslyExposed ? "PreviouslyExposed" :
                exposure == NasdaqReviewerExposure.SelfDeclaredIndependent && review.DeclaredAtUtc >= frozenAt
                    ? "IndependentPreReveal" : "ReviewNotEvaluable";
            history.Add(new(label, classification,
                earlier is not null && label.Revision <= earlier.History.Count
                    ? earlier.History[label.Revision - 1].ReadinessSha256 : readinessSha256));
            last = label.ReviewedAtUtc;
        }
        return new(review.ReviewerId, exposure, review.DeclaredAtUtc, review.OutcomesRevealedAtUtc, pin.Sha256, history);
    }
}

internal sealed record NasdaqValidationBoundReview(DateOnly OperatingDate, DateTimeOffset CutoffUtc, string ReadinessSha256,
    NasdaqValidationReview Review);
