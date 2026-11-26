using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed record NasdaqPreparationReceiptReference(string LocalReferenceId, string? NativeReferenceId,
    string? OriginalTime, string? OriginalClockDomain, string TimePrecision, string? SourceReference, string Status,
    Mt5NativeTimestamp? MarketEventNativeTime, Mt5LifecycleReference? FirstUsableHistoricalReceipt,
    DateTimeOffset? ReceiptHostClockCandidate);

public sealed record NasdaqPreparationReceiptBinding(string SessionKey, string SessionId, NasdaqDemoSessionIdentity Identity,
    Guid PreparationRecordId, int PreparationRevision, string PreparationRevisionSha256, string PreparationSourceReference,
    string ReceiptCutId, string ReceiptCutReportSha256, long CutSequence, string CutReceiptSha256, string PrefixManifestSha256,
    string DirectoryScope, Mt5ReceiptPrefixBoundary? Anchor, Mt5TemporalSourceIdentity Source,
    DateTimeOffset ReceiptHostClockCandidate, DateTimeOffset PreparationReceivedAtUtc, DateTimeOffset PreparationRegisteredAtUtc,
    string PreparationClockSource, string? HumanReportedObservationTime, string? PreparationOriginalTime,
    string? PreparationOriginalClockDomain, string PreparationTimePrecision, DateTimeOffset AssociationCreatedAtUtc,
    IReadOnlyList<NasdaqPreparationReceiptReference> References, IReadOnlyList<string> Diagnostics)
{
    public string Scope => "DevelopmentStandard_DocumentaryOnly";
    public string MatchStatus => References.Count > 0 && References.All(r => r.Status == "ExactNativeCoordinateInCut")
        ? "NativeCoordinatesMatched_DocumentaryOnly" : "Partial_UnresolvedNativeReferences";
    public string PreparationAvailabilityAtCut => "Unresolved";
    public string AssociationClockMeaning => "OrdinaryApplicationWallClock_BeforePublication_NotMarketAvailability";
    public string WallClockOrdering => PreparationRegisteredAtUtc > ReceiptHostClockCandidate
        ? "PreparationRegistrationAfterCutLabel" : "PreparationRegistrationAtOrBeforeCutLabel_Unverified";
    public string StandardEvaluation => "NotEvaluated";
    public bool CanonicalReady => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

public sealed record NasdaqPreparationReceiptBindingView(NasdaqPreparationReceiptBinding Binding, string AssociationSha256,
    IReadOnlyList<NasdaqPreparationArtifact> Artifacts);

public sealed partial class LocalNasdaqExecutionEvidenceStore
{
    private sealed record PreparationBindingEnvelope(NasdaqPreparationReceiptBinding Binding, string Sha256, int SchemaVersion = 1);
    private static string BindingStem(Guid id, int revision) => $"preparation-cut-{id:N}.{revision:D3}-";
    private static string BindingName(NasdaqPreparationReceiptBinding value) =>
        BindingStem(value.PreparationRecordId, value.PreparationRevision) + value.ReceiptCutReportSha256 + ".binding";

    public IReadOnlyList<NasdaqPreparationReceiptBindingView> PreparationReceiptBindings(string key, Guid id, int revision)
    {
        using var gate = Lock();
        var session = PreparationSession(key);
        var preparation = BindingPreparation(session, id, revision);
        var files = Directory.EnumerateFiles(root, BindingStem(id, revision) + "*.binding").Take(33).ToArray();
        if (files.Length > 32) throw new InvalidDataException("Bounded preparation binding limit exceeded.");
        if (files.Length > 0) CheckBindingArtifacts(preparation.Record);
        return files.Select(f => ReadBinding(f, preparation, session)).OrderBy(x => x.Binding.CutSequence)
            .ThenBy(x => x.Binding.ReceiptCutReportSha256, StringComparer.Ordinal).ToArray();
    }

    public NasdaqPreparationReceiptBindingView BindPreparationReceiptCut(string key, Guid id, int revision, string expectedRevision,
        NasdaqReceiptCutReportView receiptCut)
    {
        using var gate = Lock();
        var session = PreparationSession(key);
        var preparation = BindingPreparation(session, id, revision);
        if (preparation.RevisionHash != expectedRevision) throw new NasdaqEvidenceConflictException("PreparationBindingRevisionHashMismatch");
        var record = preparation.Record;
        var report = receiptCut.Diagnostic;
        if (session.Identity.ProviderId.Value != receiptCut.ProviderId || session.Identity.Symbol.Value != report.ExpectedSource.NativeSymbol
            || report.ExpectedSource != Mt5ReceiptCutDiagnostic.SupportedSource)
            throw new NasdaqEvidenceConflictException("PreparationBindingSourceIdentityMismatch");
        if (report.CutLocalReceiptClockLabel is not { Offset: { Ticks: 0 } } label
            || NasdaqSupervisedSessionPolicy.TradingDay(label) != session.Identity.TradingDay)
            throw new NasdaqEvidenceConflictException("PreparationBindingCutDayMissingOrDifferent");
        CheckBindingArtifacts(record);

        var instant = clock.GetUtcNow();
        if (instant.Offset != TimeSpan.Zero || instant < record.RegisteredAtUtc || instant < label)
            throw new IOException("Association clock precedes a retained local label; nothing published.");
        var references = record.Evidence.References.Select(r => MatchReference(r, report, session)).ToArray();
        var diagnostics = preparation.MissingDocumentaryInputs.Concat([
            "DocumentaryAssociationDoesNotEstablishHumanAvailabilityAtCut",
            "HostClockLabelsAreUnauthenticated",
            "NoIndependentCanonicalPreparationOrH4Evaluation"
        ]).ToList();
        if (references.Length == 0 || references.Any(r => r.Status != "ExactNativeCoordinateInCut"))
            diagnostics.Add("ExactNativeCandleReferencesUnresolved");
        if (record.Artifacts.Count == 0) diagnostics.Add("NoOriginalArtifactBytesAttachedToRevision");
        var cut = report.CutReceipt!;
        var binding = new NasdaqPreparationReceiptBinding(key, session.SessionId, session.Identity, id, revision, expectedRevision,
            record.Evidence.SourceReference, receiptCut.Id, receiptCut.ReportSha256, cut.Sequence, cut.Sha256, report.PrefixManifestSha256!,
            report.DirectoryScope, report.Anchor, report.ExpectedSource, label, record.ReceivedAtUtc, record.RegisteredAtUtc,
            record.ReceiptClockSource, record.Evidence.OperatorReportedObservationTime, record.Evidence.OriginalTime,
            record.Evidence.OriginalClockDomain, record.Evidence.TimePrecision, instant, references, diagnostics.ToArray());
        var name = BindingName(binding);
        var path = Path.Combine(root, name);
        if (File.Exists(path))
        {
            var existing = ReadBinding(path, preparation, session);
            if (existing.Binding.ReceiptCutId != receiptCut.Id)
                throw new NasdaqEvidenceConflictException("PreparationBindingCutRegistrationMismatch");
            return existing;
        }
        if (Directory.EnumerateFiles(root, BindingStem(id, revision) + "*.binding").Take(32).Count() >= 32)
            throw new InvalidDataException("Bounded preparation binding limit exceeded.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(binding, Json)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PreparationBindingEnvelope(binding, hash), Json);
        if (bytes.Length > 131072) throw new ArgumentException("Preparation binding exceeds its local evidence limit.");
        Publish(name, bytes);
        return new(binding, hash, record.Artifacts);
    }

    private NasdaqProspectivePreparationView BindingPreparation(NasdaqSupervisedSessionState session, Guid id, int revision) =>
        CheckedPreparation(ReadPreparations().SingleOrDefault(p => p.Record.Evidence.SessionKey == session.SessionKey
            && p.Record.RecordId == id && p.Record.Revision == revision) ?? throw new KeyNotFoundException(), session);

    private void CheckBindingArtifacts(NasdaqProspectivePreparationRecord record)
    {
        foreach (var artifact in record.Artifacts) _ = ReadArtifactBytes(artifact);
    }

    private static NasdaqPreparationReceiptReference MatchReference(NasdaqPreparationReferenceReport reference, Mt5ReceiptCutReport report,
        NasdaqSupervisedSessionState session)
    {
        Mt5ReceiptCutSample? sample = null;
        var status = "ExactNativeReferenceUnreported";
        if (reference.OriginalClockDomain == "MT5Native" && reference.TimePrecision == "ExactNativeRecord"
            && reference.OriginalTime is not null && reference.SourceReference is not null
            && reference.SourceProviderId == session.Identity.ProviderId.Value && reference.OriginalSymbol == session.Identity.Symbol.Value
            && reference.Timeframe is "4h" or "1h")
        {
            var frame = reference.Timeframe == "4h" ? "H4" : "H1";
            var matches = report.Samples.Where(s => s.Timeframe == frame && s.OpenTimeNative.Display == reference.OriginalTime).Take(2).ToArray();
            sample = matches.Length == 1 ? matches[0] : null;
            status = matches.Length switch
            {
                0 => "ExactNativeReferenceNotInCut",
                1 => "ExactNativeCoordinateInCut",
                _ => "ExactNativeReferenceAmbiguous"
            };
        }
        return new(reference.LocalReferenceId, reference.NativeReferenceId, reference.OriginalTime, reference.OriginalClockDomain,
            reference.TimePrecision, reference.SourceReference, status, sample?.OpenTimeNative, sample?.FirstUsableHistorical.Receipt,
            sample?.LocalReceiptClockLabel);
    }

    private static NasdaqPreparationReceiptBindingView ReadBinding(string path, NasdaqProspectivePreparationView preparation,
        NasdaqSupervisedSessionState session)
    {
        Leaf(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 131072) throw new InvalidDataException("Invalid preparation binding size.");
        try
        {
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32, AllowDuplicateProperties = false });
            var envelope = doc.RootElement.Deserialize<PreparationBindingEnvelope>(Json) ?? throw new InvalidDataException();
            var binding = envelope.Binding; var record = preparation.Record;
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(doc.RootElement.GetProperty("binding").GetRawText())));
            if (envelope.SchemaVersion != 1 || envelope.Sha256 != hash || binding is null || binding.Identity != session.Identity
                || binding.SessionKey != session.SessionKey || binding.SessionId != session.SessionId
                || binding.PreparationRecordId != record.RecordId || binding.PreparationRevision != record.Revision
                || binding.PreparationRevisionSha256 != preparation.RevisionHash || binding.Source != Mt5ReceiptCutDiagnostic.SupportedSource
                || !ReadNasdaqReceiptCutReport.IsDigest(binding.ReceiptCutReportSha256) || !ReadNasdaqReceiptCutReport.IsDigest(binding.CutReceiptSha256)
                || binding.AssociationCreatedAtUtc.Offset != TimeSpan.Zero || binding.AssociationCreatedAtUtc < record.RegisteredAtUtc
                || binding.PreparationReceivedAtUtc != record.ReceivedAtUtc || binding.PreparationRegisteredAtUtc != record.RegisteredAtUtc
                || Path.GetFileName(path) != BindingName(binding))
                throw new InvalidDataException("Invalid preparation binding identity or hash.");
            return new(binding, hash, record.Artifacts);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or KeyNotFoundException)
        { throw new InvalidDataException("Invalid stored preparation binding.", e); }
    }
}
