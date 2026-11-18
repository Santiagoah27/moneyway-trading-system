namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

// Source annotations are human assertions. All byte identity and receipt fields are server-owned.
public sealed record NasdaqPreparationArtifactAnnotation(string OriginalFilename, string ContentType,
    string SourceProviderId, string OriginalSymbol, string SourceDescription, string RevisionReason)
{
    public string? ObservationReference { get; init; }
    public string? OriginalTime { get; init; }
    public string? OriginalClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    public void Validate(NasdaqProspectivePreparationRecord preparation)
    {
        foreach (var value in new[] { OriginalFilename, SourceProviderId, OriginalSymbol, SourceDescription, RevisionReason }) NasdaqProspectivePreparationDraft.Text(value, true);
        if (OriginalFilename.Length > 120 || OriginalFilename.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not (' ' or '.' or '_' or '-'))
            || OriginalFilename.StartsWith('.') || OriginalFilename.EndsWith(' ') || !OriginalFilename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || OriginalFilename.Contains("..", StringComparison.Ordinal)
            || new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(OriginalFilename.Split('.')[0].TrimEnd(), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use a safe display filename ending in .png; no paths or reserved names.");
        if (ContentType != "image/png") throw new ArgumentException("Only original image/png screenshots supported.");
        if (SourceProviderId != preparation.Identity.ProviderId.Value || OriginalSymbol != preparation.Identity.Symbol.Value)
            throw new ArgumentException("Artifact source must match the exact session provider/instrument; no aliases.");
        NasdaqProspectivePreparationDraft.Text(ObservationReference);
        if (ObservationReference is not null && ObservationReference != "H4" && !preparation.Evidence.References.Any(r => r.LocalReferenceId == ObservationReference))
            throw new ArgumentException("Select H4 or an existing documentary reference in this preparation revision.");
        (preparation.Evidence with { OriginalTime = OriginalTime, OriginalClockDomain = OriginalClockDomain, TimePrecision = TimePrecision }).Validate(preparation.Identity);
    }
}
public sealed record NasdaqPreparationArtifact(Guid ArtifactId, string SessionKey, Guid PreparationRecordId,
    int AttachedInRevision, NasdaqPreparationArtifactAnnotation Source, long ByteLength, string Sha256,
    DateTimeOffset ReceivedAtUtc, string ReceiptClockSource)
{
    public string IdentityMeaning => "InternalMoneyWayArtifact_NotBrokerNativeObservation";
    public bool OriginalSourceIndependentlyVerified => false;
    public bool UtcAuthenticated => false;
    public DateTimeOffset? VerifiedHistoricalAvailableAtUtc => null;
    public bool CanonicalAdmissionPermitted => false;
    public string Availability => "DataUnavailable_OriginalMarketAvailabilityNotEstablished";
    public string ReceiptMeaning => "OrdinaryServiceClockAfterBoundedUploadParsing_NotSourceObservationTime";
}
