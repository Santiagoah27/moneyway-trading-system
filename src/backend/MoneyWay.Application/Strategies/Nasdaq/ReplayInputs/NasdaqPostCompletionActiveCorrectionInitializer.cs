namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes the active correction without consuming another market candle.</summary>
public sealed class NasdaqPostCompletionActiveCorrectionInitializer
{
    public NasdaqPostCompletionActiveCorrectionState Initialize(NasdaqPostCompletionExtremeGeometryReady geometryReady)
    {
        ArgumentNullException.ThrowIfNull(geometryReady);
        return new(geometryReady);
    }
}
