using System.Diagnostics;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>Opt-in, single-run orchestration timings. Never strategy evidence. Safe to snapshot from a checkpoint timer.</summary>
public sealed class ReplayPerformanceProfile
{
    public const int MaximumMetricCount = 128;
    private readonly object sync = new();
    private readonly Dictionary<string, Counter> counters = new(StringComparer.Ordinal);
    private long frames;
    private long droppedMeasurements;
    private string status = "running";

    public Measurement Measure(string operation)
    {
        lock (sync)
        {
            if (!counters.TryGetValue(operation, out var counter))
            {
                if (counters.Count == MaximumMetricCount) { droppedMeasurements++; return default; }
                counters.Add(operation, counter = new());
            }
            // Each named operation is synchronous and non-reentrant within this replay.
            if (counter.ActiveSince is not null) throw new InvalidOperationException("Profiling operation is already active.");
            var start = Stopwatch.GetTimestamp();
            counter.Started++;
            counter.ActiveSince = start;
            return new(this, operation, start);
        }
    }

    public void FrameProcessed() { lock (sync) frames++; }
    public void Complete() { lock (sync) status = "completed"; }
    public void Fault() { lock (sync) status = "faulted"; }

    public ReplayPerformanceSnapshot Snapshot()
    {
        lock (sync)
        {
            var now = Stopwatch.GetTimestamp();
            return new(status, frames, droppedMeasurements,
                "Inclusive durations overlap with nested operations; do not sum parent and child costs. Closed calls include unwound scopes; Status alone indicates run completion.",
                counters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new ReplayPerformanceMetric(
                    p.Key, p.Value.Started, p.Value.Closed, Milliseconds(p.Value.TotalTicks), Milliseconds(p.Value.MaximumTicks),
                    p.Value.ActiveSince is { } start ? Milliseconds(now - start) : 0, p.Value.ActiveSince is not null)).ToArray());
        }
    }

    private static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    private void Close(string operation, long start)
    {
        var elapsed = Stopwatch.GetTimestamp() - start;
        lock (sync)
        {
            var counter = counters[operation];
            if (counter.ActiveSince != start) return;
            counter.ActiveSince = null;
            counter.Closed++;
            counter.TotalTicks += elapsed;
            counter.MaximumTicks = Math.Max(counter.MaximumTicks, elapsed);
        }
    }

    public readonly struct Measurement : IDisposable
    {
        private readonly ReplayPerformanceProfile? profile;
        private readonly string? operation;
        private readonly long start;
        internal Measurement(ReplayPerformanceProfile profile, string operation, long start)
        { this.profile = profile; this.operation = operation; this.start = start; }
        public void Dispose() => profile?.Close(operation!, start);
    }

    private sealed class Counter
    {
        public long Started;
        public long Closed;
        public long TotalTicks;
        public long MaximumTicks;
        public long? ActiveSince;
    }
}

public sealed record ReplayPerformanceSnapshot(string Status, long FramesProcessed, long DroppedMeasurements,
    string Accounting, IReadOnlyList<ReplayPerformanceMetric> Metrics);

public sealed record ReplayPerformanceMetric(string Operation, long StartedCalls, long ClosedCalls,
    double TotalClosedMilliseconds, double MaximumClosedMilliseconds, double InFlightMilliseconds, bool Active);
