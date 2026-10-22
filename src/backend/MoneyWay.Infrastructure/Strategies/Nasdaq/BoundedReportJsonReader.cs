using System.Security.Cryptography;
using System.Text.Json;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Incremental JSON reader retaining at most one bounded property/frame.</summary>
internal sealed class BoundedReportJsonReader(Stream source, CancellationToken cancellationToken) : IDisposable
{
    private const int MaximumValueBytes = 8 * 1024 * 1024;
    private byte[] buffer = new byte[64 * 1024];
    private int offset;
    private int length;
    private bool final;
    private JsonReaderState state;
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    internal long BytesRead { get; private set; }
    internal string Digest() => Convert.ToHexString(hash.GetHashAndReset());

    internal JsonTokenType? Peek()
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(offset, length - offset), final, state);
            if (reader.Read()) return reader.TokenType;
            if (final) return null;
            Fill();
        }
    }

    internal string? Token(JsonTokenType expected)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(offset, length - offset), final, state);
            if (reader.Read())
            {
                if (reader.TokenType != expected) throw new InvalidDataException("Unexpected report token.");
                var name = expected == JsonTokenType.PropertyName ? reader.GetString() : null;
                Consume(reader);
                return name;
            }
            if (final) throw new InvalidDataException("Incomplete report.");
            Fill();
        }
    }

    internal JsonDocument Value()
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reader = new Utf8JsonReader(buffer.AsSpan(offset, length - offset), final, state);
            if (reader.Read() && JsonDocument.TryParseValue(ref reader, out var document))
            {
                Consume(reader);
                return document;
            }
            if (final) throw new InvalidDataException("Incomplete report value.");
            Fill();
        }
    }

    private void Consume(Utf8JsonReader reader)
    {
        offset += checked((int)reader.BytesConsumed);
        state = reader.CurrentState;
    }

    private void Fill()
    {
        var remaining = length - offset;
        buffer.AsSpan(offset, remaining).CopyTo(buffer);
        offset = 0;
        length = remaining;
        if (remaining == buffer.Length)
        {
            if (buffer.Length >= MaximumValueBytes) throw new InvalidDataException("Report value exceeds read limit.");
            Array.Resize(ref buffer, Math.Min(buffer.Length * 2, MaximumValueBytes));
        }
        var count = source.Read(buffer.AsSpan(length));
        if (count == 0) final = true;
        else
        {
            hash.AppendData(buffer.AsSpan(length, count));
            length += count;
            BytesRead += count;
            if (BytesRead > 1024L * 1024 * 1024) throw new InvalidDataException("Report exceeds read limit.");
        }
    }

    public void Dispose() => hash.Dispose();
}
