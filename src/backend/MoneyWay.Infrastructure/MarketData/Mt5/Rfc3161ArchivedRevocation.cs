using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed class Rfc3161ArchivedRevocationResult
{
    internal Rfc3161ArchivedRevocationResult(bool verified, string status) { Verified = verified; Status = status; }
    public bool Verified { get; }
    public string Status { get; }
}

/// <summary>
/// Optional offline CRL verification using the existing Git for Windows OpenSSL. No download, system trust-store
/// changes or caller trust flags. .NET's X509Chain does not expose archived CRL input/verification APIs.
/// </summary>
public static class Rfc3161ArchivedRevocation
{
    public static Task<Rfc3161ArchivedRevocationResult> VerifyAsync(Rfc3161ProtocolResult protocol,
        Rfc3161ChainResult chain, string crlDirectory, string outputDirectory, int timeoutSeconds) =>
        VerifyCoreAsync(protocol, chain, crlDirectory, outputDirectory, timeoutSeconds, Rfc3161WitnessVerifier.DigiCertRootSha256);

    internal static async Task<Rfc3161ArchivedRevocationResult> VerifyCoreAsync(Rfc3161ProtocolResult protocol,
        Rfc3161ChainResult chain, string crlDirectory, string outputDirectory, int timeoutSeconds, string rootPin)
    {
        if (timeoutSeconds is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (!protocol.SignatureVerified || !protocol.RequestLinked || !protocol.PolicyRecognized || protocol.Diagnostics.Count != 0
            || protocol.SignedTime is null || !chain.PathToPinnedAnchor || chain.ChainCertificates.Count < 2
            || Mt5TimestampManifest.Hash(chain.ChainCertificates[^1]) != rootPin
            || Mt5TimestampManifest.Hash(chain.ChainCertificates[0]) != protocol.SignerSha256)
            return new(false, "protocol_or_pinned_chain_not_established");
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin", "openssl.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(executable)) return new(false, "existing_git_openssl_unavailable");
        if (!Path.IsPathFullyQualified(crlDirectory) || !Path.IsPathFullyQualified(outputDirectory)) throw new ArgumentException("Absolute evidence paths required.");
        try
        {
            var crls = new[] { "signer.crl", "issuer.crl" }.Select(name =>
            {
                using var file = new FileStream(Path.Combine(crlDirectory, name), FileMode.Open, FileAccess.Read, FileShare.Read);
                if (file.Length > 1024 * 1024) throw new ArgumentException("CRL exceeds diagnostic size limit.");
                var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); return (Name: name, Bytes: bytes);
            }).ToArray();
            foreach (var c in crls) Write("archived-" + c.Name, c.Bytes);
            var combined = string.Join("\n", crls.Select(c => PemEncoding.WriteString("X509 CRL", c.Bytes))) + "\n";
            Write("archived-crls.pem", Encoding.ASCII.GetBytes(combined));
            Write("archived-root.pem", Encoding.ASCII.GetBytes(PemEncoding.WriteString("CERTIFICATE", chain.ChainCertificates[^1])));
            Write("archived-signer.pem", Encoding.ASCII.GetBytes(PemEncoding.WriteString("CERTIFICATE", chain.ChainCertificates[0])));
            var intermediates = string.Join("\n", chain.ChainCertificates.Skip(1).SkipLast(1).Select(c => PemEncoding.WriteString("CERTIFICATE", c)));
            if (intermediates.Length > 0) Write("archived-intermediates.pem", Encoding.ASCII.GetBytes(intermediates));
            Write("openssl-offline.cnf", []);
            var epoch = protocol.SignedTime.GenTime.ToUnixTimeSeconds();
            var epochs = protocol.SignedTime.GenTime.Ticks % TimeSpan.TicksPerSecond == 0 ? new[] { epoch } : new[] { epoch, epoch + 1 };
            var records = new List<object>(); var passed = true;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            foreach (var at in epochs)
            {
                var start = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = outputDirectory
                };
                start.Environment["OPENSSL_CONF"] = Path.Combine(outputDirectory, "openssl-offline.cnf");
                foreach (var arg in new[] { "verify", "-CAfile", "archived-root.pem", "-CRLfile", "archived-crls.pem",
                    "-crl_check_all", "-purpose", "timestampsign", "-attime", at.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-no-CApath", "-no-CAstore" }) start.ArgumentList.Add(arg);
                if (intermediates.Length > 0) { start.ArgumentList.Add("-untrusted"); start.ArgumentList.Add("archived-intermediates.pem"); }
                start.ArgumentList.Add("archived-signer.pem");
                using var process = Process.Start(start) ?? throw new IOException("OpenSSL process unavailable.");
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(deadline.Token); }
                catch (OperationCanceledException)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    await process.WaitForExitAsync(); return new(false, "offline_revocation_verification_timeout");
                }
                var text = await stdout; var errors = await stderr;
                if (text.Length > 16384 || errors.Length > 16384) return new(false, "offline_verifier_output_limit");
                records.Add(new { Arguments = start.ArgumentList.ToArray(), Epoch = at, process.ExitCode, StandardOutput = text, StandardError = errors });
                passed &= process.ExitCode == 0;
            }
            Write("archived-revocation-verification.json", JsonSerializer.SerializeToUtf8Bytes(new
            {
                Schema = "moneyway.rfc3161-offline-crl-verification",
                Version = 1,
                ToolSha256 = Mt5TimestampManifest.Hash(File.ReadAllBytes(executable)),
                ToolFileVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion,
                SignedGenTime = protocol.SignedTime.GenTime,
                SuppliedRootSha256 = rootPin,
                Results = records,
                NetworkDownloadsEnabled = false,
                WindowsClockUsedAsUtcAuthority = false,
                Verified = passed
            }, new JsonSerializerOptions { WriteIndented = true }));
            return new(passed, passed ? "verified_from_archived_crls_at_signed_gen_time" : "archived_crl_verification_failed");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { return new(false, "archived_revocation_evidence_or_tool_unavailable"); }

        void Write(string name, byte[] bytes)
        {
            using var file = new FileStream(Path.Combine(outputDirectory, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(bytes); file.Flush(true);
        }
    }
}
