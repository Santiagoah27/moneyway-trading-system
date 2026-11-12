namespace MoneyWay.Application.MarketData.Receipt;

/// <summary>On-demand local receipt only. Does not construct canonical candles or strategy contexts.</summary>
public interface IMt5CollectorReceiptReader
{
    Mt5ReceiptBatch Read(string sourceDirectory, Mt5ReceiptCheckpoint? checkpoint = null,
        IReadOnlyDictionary<string, string>? expectedRecordHashes = null);
}
