using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.Pkcs;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed record Rfc3161TransportResult(int? HttpStatus, string? ContentType, byte[] Response,
    long StartedCounter, long FinishedCounter, long CounterFrequency, string? Error);

/// <summary>One POST, no retries/redirects/cookies. The public endpoint is a reviewed diagnostic candidate only.</summary>
public static class Rfc3161WitnessTransport
{
    public static bool SupportedEndpoint(Uri uri) => uri.AbsoluteUri == "http://timestamp.digicert.com/";

    public static async Task<Rfc3161TransportResult> SendAsync(Uri endpoint, byte[] request, int timeoutSeconds,
        CancellationToken cancellation = default)
    {
        if (!Rfc3161TimestampRequest.TryDecode(request, out var decoded, out var used) || used != request.Length
            || decoded.HashAlgorithmId.Value != Rfc3161WitnessVerifier.Sha256Oid || decoded.GetMessageHash().Length != 32
            || decoded.GetNonce() is not { Length: >= 16 } || !decoded.RequestSignerCertificate || decoded.GetExtensions().Count != 0)
            throw new ArgumentException("Only a digest-only nonce-bound RFC3161 request may be transmitted.");
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 10))
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return await SendCoreAsync(client, endpoint, request, timeoutSeconds, cancellation);
    }

    internal static async Task<Rfc3161TransportResult> SendCoreAsync(HttpClient client, Uri endpoint, byte[] request,
        int timeoutSeconds, CancellationToken cancellation = default)
    {
        if (!SupportedEndpoint(endpoint) || timeoutSeconds is < 1 or > 30 || request.Length > 8192)
            throw new ArgumentException("Unsupported endpoint or request limits.");
        var started = Stopwatch.GetTimestamp();
        int? status = null; string? type = null; using var buffer = new MemoryStream();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Content = new ByteArrayContent(request);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/timestamp-reply"));
            using var reply = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            status = (int)reply.StatusCode; type = reply.Content.Headers.ContentType?.MediaType;
            if (reply.Content.Headers.ContentLength > Rfc3161WitnessVerifier.MaxResponseBytes) return Finish("response_size_limit");
            using var stream = await reply.Content.ReadAsStreamAsync(deadline.Token);
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, deadline.Token)) != 0)
            {
                if (buffer.Length + read > Rfc3161WitnessVerifier.MaxResponseBytes) return Finish("response_size_limit");
                buffer.Write(chunk, 0, read);
            }
            return Finish(!reply.IsSuccessStatusCode ? "http_response_not_successful" :
                type != "application/timestamp-reply" ? "unexpected_timestamp_content_type" : null);
        }
        catch (OperationCanceledException) { return Finish("request_timeout_or_cancelled"); }
        catch (HttpRequestException) { return Finish("timestamp_network_error"); }
        catch (IOException) { return Finish("timestamp_response_io_error"); }

        Rfc3161TransportResult Finish(string? error) => new(status, type, buffer.ToArray(), started,
            Stopwatch.GetTimestamp(), Stopwatch.Frequency, error);
    }
}
