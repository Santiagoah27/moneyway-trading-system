using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MoneyWay.Worker;

public sealed class NasdaqValidationProtocolCommand
{
    private readonly Func<bool, string> revision;

    public NasdaqValidationProtocolCommand() : this(RepositoryRevision) { }

    internal NasdaqValidationProtocolCommand(Func<bool, string> revision) => this.revision = revision;

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Count != 6 || args[0] != "nasdaq-validation-protocol" || args[1] is not ("freeze" or "assess") ||
                args[2] != "--request" || args[4] != "--output" || !Path.IsPathFullyQualified(args[5]))
                throw new ArgumentException("nasdaq-validation-protocol freeze|assess --request <absolute-json> --output <absolute-directory>");
            var bytes = NasdaqEvidenceSources.Read(args[3]);
            var freeze = args[1] == "freeze";
            var commit = revision(freeze);
            var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[5]));
            var result = freeze ? Freeze(bytes, destination, commit) : Assess(bytes, destination, commit);
            output.WriteLine(result);
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or System.ComponentModel.Win32Exception)
        {
            error.WriteLine("Nasdaq validation protocol rejected: " + e.Message);
            return 2;
        }
    }

    private static string Freeze(byte[] bytes, string directory, string commit)
    {
        var request = NasdaqValidationProtocol.Read<NasdaqValidationFreezeRequest>(bytes);
        var manifest = NasdaqValidationProtocol.Freeze(request, commit);
        NasdaqEvidenceReadinessCommand.Publish(directory, new Dictionary<string, string>
        {
            ["protocol.json"] = Text(manifest),
            ["protocol.sha256"] = NasdaqValidationProtocol.Hash(manifest) + "\n"
        }, allowIdentical: true);
        return Path.Combine(directory, "protocol.json");
    }

    private static string Assess(byte[] bytes, string directory, string commit)
    {
        var request = NasdaqValidationProtocol.Read<NasdaqValidationAssessmentRequest>(bytes);
        NasdaqSessionLiquidityResearchCommand.Guard(directory);
        var previousRuns = Directory.Exists(directory)
            ? Directory.GetFileSystemEntries(directory).Order(StringComparer.Ordinal).ToArray() : [];
        for (var i = 0; i < previousRuns.Length; i++)
            if (Path.GetFileName(previousRuns[i]) != RevisionDirectory(i + 1) || !Directory.Exists(previousRuns[i]))
                throw new ArgumentException("Use a dedicated assessment ledger with contiguous immutable revisions.");
        var requestHash = NasdaqValidationProtocol.Hash(new
        {
            Request = request,
            Commit = commit,
            Configuration = NasdaqValidationProtocol.Hash(NasdaqValidationProtocol.Configuration())
        });
        if (previousRuns.LastOrDefault() is { } last)
        {
            var pin = AssessmentPin(last);
            var snapshot = NasdaqValidationProtocol.ReadCanonical<NasdaqValidationAssessment>(pin);
            if (ReadText(Path.Combine(last, "request.sha256")) == requestHash + "\n")
            {
                var predecessor = previousRuns.Length > 1 ? AssessmentPin(previousRuns[^2]) : null;
                if (snapshot.PreviousAssessmentSha256 != predecessor?.Sha256 ||
                    NasdaqValidationProtocol.Hash(NasdaqValidationAssessmentService.Assess(
                        request with { PreviousAssessment = predecessor }, commit)) != pin.Sha256)
                    throw new ArgumentException("The ledger revision differs from its verified inputs.");
                return pin.Path;
            }
            if (request.PreviousAssessment is { } supplied && supplied != pin)
                throw new ArgumentException("The previous assessment must be the latest ledger revision.");
            request = request with { PreviousAssessment = pin };
        }
        else if (request.PreviousAssessment is not null)
            throw new ArgumentException("Continue an assessment in its existing ledger directory.");

        var report = NasdaqValidationAssessmentService.Assess(request, commit);
        var target = Path.Combine(directory, RevisionDirectory(previousRuns.Length + 1));
        NasdaqEvidenceReadinessCommand.Publish(target, new Dictionary<string, string>
        {
            ["assessment.json"] = Text(report),
            ["assessment.sha256"] = NasdaqValidationProtocol.Hash(report) + "\n",
            ["request.sha256"] = requestHash + "\n",
            ["next-actions.md"] = NextActions(report)
        }, allowIdentical: true);
        return Path.Combine(target, "assessment.json");
    }

    private static NasdaqEvidenceFile AssessmentPin(string directory) =>
        new(Path.Combine(directory, "assessment.json"), ReadText(Path.Combine(directory, "assessment.sha256")).TrimEnd('\n'));

    private static string RevisionDirectory(int revision) => "assessment-" + revision.ToString("D6", CultureInfo.InvariantCulture);

    private static string NextActions(NasdaqValidationAssessment report)
    {
        var text = new StringBuilder("# Nasdaq pre-registered validation\n\n")
            .Append("Protocol SHA256 `").Append(report.ProtocolSha256).Append("`\n\n")
            .Append("Evaluation status **NotEvaluated**. Agreement, latency and economic outcomes are unavailable.\n");
        foreach (var session in report.Sessions)
        {
            text.Append("\n- ").Append(session.OperatingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" · ").Append(session.Partition?.ToString() ?? "OutsideSelection").Append(" · ").Append(session.Exposure)
                .Append(" · ").Append(session.EligibleForIndependentEvaluation ? "Eligible" : "Blocked");
            foreach (var blocker in session.Blockers) text.Append("\n  - ").Append(blocker);
            text.Append('\n');
        }
        return text.Append("\nResolve the recorded source, exposure and label blockers before a separately authorized evaluation.\n").ToString();
    }

    private static string Text<T>(T value) => Encoding.UTF8.GetString(NasdaqValidationProtocol.Bytes(value));
    private static string ReadText(string path) => Encoding.UTF8.GetString(NasdaqEvidenceSources.Read(path));

    private static string RepositoryRevision(bool freeze)
    {
        var head = Git("rev-parse", "HEAD").Trim();
        var compiled = typeof(NasdaqValidationProtocolCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+').Last();
        if (!NasdaqValidationProtocol.ValidCommit(head) || !NasdaqValidationProtocol.ValidCommit(compiled ?? ""))
            throw new ArgumentException("Run the source-built Worker from the MoneyWay repository with Git provenance.");
        if (head != compiled || Git("status", "--porcelain").Length != 0)
            throw new ArgumentException($"{(freeze ? "Freeze" : "Assess")} from a clean repository and a Worker built at its current HEAD.");
        return head;
    }

    private static string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot read Git provenance.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new ArgumentException("Git repository provenance is unavailable.");
        return output;
    }
}
