using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed partial class LocalNasdaqExecutionEvidenceStore
{
    private sealed record SessionEnvelope(NasdaqSupervisedSessionEvent Event, string Sha256, int SchemaVersion = 1);
    private static string SessionKey(NasdaqDemoSessionIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join('\n', identity.StrategyId.Value, identity.StrategyVersion.Value, identity.ProviderId.Value, identity.Symbol.Value, identity.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))));
    private static void SessionText(string? text)
    { if (string.IsNullOrWhiteSpace(text) || text != text.Trim() || text.Length > 2048 || text.Any(char.IsControl)) throw new ArgumentException("Explicit bounded operational identity/provenance required."); }
    private List<SessionEnvelope> ReadSessions(List<Envelope> captures)
    {
        var files = Directory.EnumerateFiles(root, "session-*.session").Take(1001).ToArray();
        if (files.Length > 1000) throw new InvalidDataException("Bounded session event limit exceeded.");
        var all = new List<SessionEnvelope>();
        foreach (var file in files)
        {
            Leaf(file); using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Invalid operational event size.");
            try
            {
                using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
                var value = JsonSerializer.Deserialize<SessionEnvelope>(doc.RootElement, Json) ?? throw new InvalidDataException();
                var e = value.Event; var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(doc.RootElement.GetProperty("event").GetRawText())));
                if (value.SchemaVersion != 1 || e is null || e.Identity is null || e.Command is null || e.Command.SubmissionId == Guid.Empty
                    || e.RegisteredAtUtc.Offset != TimeSpan.Zero || e.Revision is < 1 or > 100 || e.SessionKey != SessionKey(e.Identity)
                    || Path.GetFileName(file) != $"session-{e.SessionKey}.{e.Revision:D3}.session" || digest != value.Sha256
                    || doc.RootElement.GetProperty("event").GetProperty("brokerVerified").GetBoolean()) throw new InvalidDataException("Invalid session identity/hash.");
                ValidateSessionCommand(e.Command); SessionText(e.SessionId);
                if (e.Path is not ("Standard" or "WickfillScenario2") || e.Identity.StrategyId.Value != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId.Value
                    || e.Identity.Symbol.Value is not ("US100" or "USTEC")) throw new InvalidDataException("Invalid Nasdaq session scope.");
                all.Add(value);
            }
            catch (Exception e) when (e is JsonException or ArgumentException or KeyNotFoundException or NullReferenceException)
            { throw new InvalidDataException("Invalid stored operational session; originals preserved.", e); }
        }
        if (all.Select(v => v.Event.Command.SubmissionId).Distinct().Count() != all.Count) throw new InvalidDataException("Duplicate session submission identity.");
        foreach (var group in all.GroupBy(v => v.Event.SessionKey))
        {
            SessionEnvelope? previous = null; var chain = new List<NasdaqSupervisedSessionEvent>();
            foreach (var item in group.OrderBy(v => v.Event.Revision))
            {
                var e = item.Event;
                if (e.Revision != chain.Count + 1 || e.PreviousHash != previous?.Sha256 || e.RegisteredAtUtc < previous?.Event.RegisteredAtUtc)
                    throw new InvalidDataException("Incomplete session revision chain.");
                try { if (e.Command.Execution is not null) ValidateOperationalEvidence(e.Identity, e.SessionId, e.Path, e.Command, captures, chain); }
                catch (ArgumentException error) { throw new InvalidDataException("Stored session evidence conflicts with its immutable capture.", error); }
                chain.Add(e); previous = item;
            }
            try { _ = NasdaqSupervisedSessionPolicy.Project(chain, previous!.Sha256, clock.GetUtcNow()); }
            catch (ArgumentException e) { throw new InvalidDataException("Conflicting session events.", e); }
        }
        return all;
    }
    private static void ValidateSessionCommand(NasdaqSupervisedSessionCommand c)
    {
        SessionText(c.Reviewer); NasdaqSupervisedExecutionEvidenceDraft.Reference(c.SourceReference);
        if (c.SubmissionId == Guid.Empty || c.Action is not ("Open" or "ActivateSetup" or "InvalidateSetup" or "EntryIntent" or "ConfirmExecution" or "ConfirmClosure")) throw new ArgumentException("Invalid session command.");
        if (c.Action is "ActivateSetup" or "InvalidateSetup" or "EntryIntent") SessionText(c.SetupReference);
        if (c.Action is not ("ConfirmExecution" or "ConfirmClosure") && (c.Execution is not null || c.MoneyWayOwnedExecutionConfirmed || c.MoneyWayTradeClosedConfirmed || c.ClosureKind is not null))
            throw new ArgumentException("Operational execution/closure claims belong only to explicit confirmation actions.");
        if (c.Action is "ConfirmExecution" or "ConfirmClosure")
        {
            if (c.Execution is null || c.Execution.RecordId == Guid.Empty || c.Execution.Revision <= 0 || c.Execution.RevisionHash is null
                || c.Execution.RevisionHash.Length != 64 || !c.Execution.RevisionHash.All(char.IsAsciiHexDigit)) throw new ArgumentException("Exact saved execution evidence required.");
        }
        if (c.Action == "ConfirmExecution" && !c.MoneyWayOwnedExecutionConfirmed) throw new ArgumentException("Explicit MoneyWay-owned supervised execution confirmation required.");
        if (c.Action == "ConfirmClosure" && (!c.MoneyWayTradeClosedConfirmed || c.ClosureKind is not ("TakeProfit" or "StopLoss" or "BreakEven" or "ProtectedOneToOne" or "OtherEvidencedExit")))
            throw new ArgumentException("Explicit closure confirmation and source-described closure kind required.");
        if (c.Action == "ConfirmClosure") SessionText(c.Reason);
    }
    private static void ValidateOperationalEvidence(NasdaqDemoSessionIdentity identity, string sessionId, string path,
        NasdaqSupervisedSessionCommand command, List<Envelope> captures, IReadOnlyList<NasdaqSupervisedSessionEvent> history)
    {
        var reference = command.Execution!;
        var record = captures.SingleOrDefault(v => v.Record.RecordId == reference.RecordId && v.Record.Revision == reference.Revision && v.Sha256 == reference.RevisionHash)?.Record
            ?? throw new ArgumentException("Exact persisted execution record/revision/hash not found.");
        var d = record.Evidence;
        if (d.SessionId != sessionId || d.TradingDay != identity.TradingDay || d.SourceProviderId != identity.ProviderId.Value || d.OriginalSymbol != identity.Symbol.Value
            || d.Direction is null || d.Entry is null || d.Entry.EventKind == "OrderPlacement" || d.Entry.Symbol is not null && d.Entry.Symbol != identity.Symbol.Value
            || d.Entry.Direction is not null && d.Entry.Direction != d.Direction || path == "Standard" && d.Scenario == "WickfillScenario2"
            || path == "WickfillScenario2" && (d.Scenario != "WickfillScenario2" || d.Direction != "Sell"))
            throw new ArgumentException("Execution evidence must explicitly belong to this exact MoneyWay session/source/instrument/path; documentary observation alone does not establish ownership.");
        if (d.Entry.DocumentedUtc is { } original && NasdaqSupervisedSessionPolicy.TradingDay(original) != identity.TradingDay)
            throw new ArgumentException("Documented opening date conflicts with the America/Bogota session date.");
        if (command.Action == "ConfirmClosure")
        {
            var entry = history.SingleOrDefault(e => e.Command.Action == "ConfirmExecution");
            if (entry?.Command.Execution?.RecordId != reference.RecordId) throw new ArgumentException("No matching prior MoneyWay-owned execution.");
            var opening = captures.Single(v => v.Record.RecordId == entry.Command.Execution.RecordId && v.Record.Revision == entry.Command.Execution.Revision).Record.Evidence;
            if (opening.Entry != d.Entry || opening.SourcePlatform != d.SourcePlatform || d.Exit is null || d.Exit.EventKind == "ReportedStopProtection"
                || d.Exit.Symbol != identity.Symbol.Value || d.Association == "None" || d.AssociationSourceReference is null
                || d.Exit.Direction is not null && d.Exit.Direction != d.Direction
                || d.Entry.PositionId is not null && d.Exit.PositionId is not null && d.Entry.PositionId != d.Exit.PositionId
                || d.Entry.DocumentedUtc > d.Exit.DocumentedUtc && d.Entry.TimePrecision != "Approximate" && d.Exit.TimePrecision != "Approximate")
                throw new ArgumentException("Closure requires an explicit same-trade association and closing event, without instrument/position/time conflicts. Protection or monetary commentary is insufficient.");
        }
    }
    private NasdaqSupervisedSessionState SessionState(IEnumerable<SessionEnvelope> all, string key)
    {
        var values = all.Where(v => v.Event.SessionKey == key).OrderBy(v => v.Event.Revision).ToArray();
        if (values.Length == 0) throw new KeyNotFoundException("Supervised session not found.");
        return NasdaqSupervisedSessionPolicy.Project(values.Select(v => v.Event).ToArray(), values[^1].Sha256, clock.GetUtcNow());
    }
    public NasdaqSupervisedSessionState OpenSession(Guid submissionId, NasdaqSupervisedSessionOpen input)
    {
        ArgumentNullException.ThrowIfNull(input);
        SessionText(input.SessionId); SessionText(input.ProviderId); SessionText(input.Symbol);
        if (input.TradingDay == default || input.Symbol is not ("US100" or "USTEC") || input.Path is not ("Standard" or "WickfillScenario2")
            || input.ProviderId.Equals("unknown", StringComparison.OrdinalIgnoreCase) || input.ProviderId == "documentary-provider-unknown") throw new ArgumentException("Explicit supported Nasdaq scope/date/path and known declared provider required.");
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var identity = new NasdaqDemoSessionIdentity(definition.StrategyId, definition.Version, new(input.ProviderId), new(input.Symbol), input.TradingDay);
        var key = SessionKey(identity); var command = new NasdaqSupervisedSessionCommand(submissionId, "Open", input.Reviewer, input.SourceReference); ValidateSessionCommand(command);
        using var gate = Lock(); var captures = ReadAll(); var all = ReadSessions(captures);
        var retry = all.SingleOrDefault(v => v.Event.Command.SubmissionId == submissionId);
        if (retry is not null && (retry.Event.SessionKey != key || retry.Event.SessionId != input.SessionId || retry.Event.Path != input.Path || retry.Event.Command != command)) throw new NasdaqEvidenceConflictException("SessionSubmissionConflict");
        if (retry is not null) return SessionState(all, key);
        if (all.Any(v => v.Event.SessionKey == key))
        {
            var state = SessionState(all, key);
            if (state.SessionId != input.SessionId || state.Path != input.Path) throw new NasdaqEvidenceConflictException("ExistingSessionScope_CannotBypassUsingAliasOrAnotherPath");
            return state;
        }
        return PublishSession(new(key, identity, input.SessionId, input.Path, 1, null, clock.GetUtcNow(), command), all);
    }
    private NasdaqSupervisedSessionState PublishSession(NasdaqSupervisedSessionEvent value, List<SessionEnvelope> all)
    {
        if (all.Count >= 1000 || value.Revision > 100) throw new InvalidDataException("Bounded operational event limit reached.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
        var envelope = new SessionEnvelope(value, hash);
        var prospective = all.Append(envelope).ToList(); _ = SessionState(prospective, value.SessionKey);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Json); if (bytes.Length > 65536) throw new ArgumentException("Operational event too large.");
        Publish($"session-{value.SessionKey}.{value.Revision:D3}.session", bytes);
        return SessionState(ReadSessions(ReadAll()), value.SessionKey);
    }
    public NasdaqSupervisedSessionState Session(string key)
    { using var gate = Lock(); return SessionState(ReadSessions(ReadAll()), key); }
    public IReadOnlyList<NasdaqSupervisedSessionState> Sessions(DateOnly day)
    {
        using var gate = Lock(); var all = ReadSessions(ReadAll());
        return all.Where(v => v.Event.Identity.TradingDay == day).Select(v => v.Event.SessionKey).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).Select(k => SessionState(all, k)).ToArray();
    }
    public NasdaqSupervisedSessionState SessionAction(string key, NasdaqSupervisedSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateSessionCommand(command); if (command.Action == "Open") throw new ArgumentException("Use session creation to open a scope.");
        using var gate = Lock(); var captures = ReadAll(); var all = ReadSessions(captures); var current = SessionState(all, key);
        var retry = all.SingleOrDefault(v => v.Event.Command.SubmissionId == command.SubmissionId);
        if (retry is not null)
        {
            if (retry.Event.SessionKey != key || retry.Event.Command != command) throw new NasdaqEvidenceConflictException("SessionSubmissionConflict");
            return current;
        }
        if (current.Terminal) throw new NasdaqEvidenceConflictException("TerminalSession_NoReentryNoNewSetupNoSecondTrade");
        if (command.Action is "ActivateSetup" or "EntryIntent" && !current.LocalEntryIntentPermitted) throw new NasdaqEvidenceConflictException(current.LocalGuardReason);
        if (command.Action == "ConfirmExecution" && current.ExecutionConfirmation is not null) throw new NasdaqEvidenceConflictException("FirstMoneyWayTradeAlreadyRegistered_NoSecondTrade");
        if (command.Execution is not null) ValidateOperationalEvidence(current.Identity, current.SessionId, current.Path, command, captures, current.Events);
        var at = clock.GetUtcNow(); if (at < current.Events[^1].RegisteredAtUtc) throw new IOException("Registration clock regressed; no operational state was published.");
        return PublishSession(new(key, current.Identity, current.SessionId, current.Path, current.Revision + 1, current.RevisionHash, at, command), all);
    }
}
