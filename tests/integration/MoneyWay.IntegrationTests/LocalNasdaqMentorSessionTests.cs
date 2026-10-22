using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;
using Xunit.Abstractions;

namespace MoneyWay.IntegrationTests;

/// <summary>Local reviewed-data entrypoint. The optional partial implementation supplies data, not strategy logic.</summary>
public sealed partial class LocalNasdaqMentorSessionTests(ITestOutputHelper output)
{
    [LocalMentorSessionFact]
    public void RunValidatedLocalSession()
    {
        var profilePath = Environment.GetEnvironmentVariable("MONEYWAY_MENTOR_PROFILE_PATH");
        var configuredReportPath = Environment.GetEnvironmentVariable("MONEYWAY_MENTOR_REPORT_PATH");
        if (!string.IsNullOrWhiteSpace(profilePath) && !string.IsNullOrWhiteSpace(configuredReportPath)
            && string.Equals(Path.GetFullPath(profilePath), Path.GetFullPath(configuredReportPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Performance diagnostics must use a separate artifact.");
        using var diagnostics = string.IsNullOrWhiteSpace(profilePath) ? null : new NasdaqReplayPerformanceDiagnostics(profilePath);
        var performance = diagnostics?.Profile;
        try
        {
            NasdaqMentorSessionReplayInput? input = null;
            using (performance?.Measure("input.evidence-prepare")) ProvideValidatedInput(ref input);
            Assert.NotNull(input);
            var report = new RunLocalNasdaqMentorSessionUseCase().Execute(input!, performance);
            var reportPath = Environment.GetEnvironmentVariable("MONEYWAY_MENTOR_REPORT_PATH");
            if (string.IsNullOrWhiteSpace(reportPath))
                reportPath = Path.Combine(Path.GetTempPath(), $"moneyway-mentor-{Guid.NewGuid():N}.json");
            if (diagnostics is not null && string.Equals(Path.GetFullPath(reportPath), Path.GetFullPath(profilePath!), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Performance diagnostics must use a separate artifact.");
            using (performance?.Measure("report.json-write"))
            using (var stream = File.Create(reportPath)) NasdaqMentorSessionReportJson.Write(stream, report);
            output.WriteLine($"Session report: {Path.GetFullPath(reportPath)}");
            output.WriteLine($"Frames: {report.Frames.Count}; final canonical verdict: {report.Canonical?.Frames.LastOrDefault()?.Verdict}");
            Assert.Empty(report.InputDiagnostics);
            Assert.NotNull(report.Canonical);
            Assert.NotEmpty(report.Frames);
            performance?.Complete();
        }
        catch { performance?.Fault(); throw; }
    }

    static partial void ProvideValidatedInput(ref NasdaqMentorSessionReplayInput? input);
}

public sealed class LocalMentorSessionFactAttribute : FactAttribute
{
    public LocalMentorSessionFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MONEYWAY_MENTOR_INPUT_SOURCE")))
            Skip = "No validated mentor-session input supplied. Synthetic coverage runs separately.";
    }
}
