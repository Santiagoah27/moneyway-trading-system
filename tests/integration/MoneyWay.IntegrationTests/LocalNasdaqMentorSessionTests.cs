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
        NasdaqMentorSessionReplayInput? input = null;
        ProvideValidatedInput(ref input);
        Assert.NotNull(input);
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(input!);
        var reportPath = Environment.GetEnvironmentVariable("MONEYWAY_MENTOR_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(Path.GetTempPath(), $"moneyway-mentor-{Guid.NewGuid():N}.json");
        using (var stream = File.Create(reportPath)) NasdaqMentorSessionReportJson.Write(stream, report);
        output.WriteLine($"Session report: {Path.GetFullPath(reportPath)}");
        output.WriteLine($"Frames: {report.Frames.Count}; final canonical verdict: {report.Canonical?.Frames.LastOrDefault()?.Verdict}");
        Assert.Empty(report.InputDiagnostics);
        Assert.NotNull(report.Canonical);
        Assert.NotEmpty(report.Frames);
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
