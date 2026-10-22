using System.Text.Json;
using MoneyWay.Application.Backtesting.Diagnostics;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Opt-in local checkpoint writer. Replaces one bounded artifact; owns no trading state.</summary>
public sealed class NasdaqReplayPerformanceDiagnostics : IDisposable
{
    private readonly object sync = new();
    private readonly string path;
    private readonly Timer timer;
    private bool disposed;
    public ReplayPerformanceProfile Profile { get; } = new();
    public long CheckpointFailures { get; private set; }

    public NasdaqReplayPerformanceDiagnostics(string path)
    {
        this.path = Path.GetFullPath(path);
        Checkpoint();
        timer = new(_ => Checkpoint(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public void Checkpoint()
    {
        lock (sync)
        {
            if (disposed) return;
            try
            {
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Profile.Snapshot()));
                File.Move(path + ".tmp", path, overwrite: true);
            }
            catch (IOException) { CheckpointFailures++; }
            catch (UnauthorizedAccessException) { CheckpointFailures++; }
        }
    }

    public void Dispose()
    {
        timer.Dispose();
        lock (sync)
        {
            if (disposed) return;
            Checkpoint();
            disposed = true;
        }
    }
}
