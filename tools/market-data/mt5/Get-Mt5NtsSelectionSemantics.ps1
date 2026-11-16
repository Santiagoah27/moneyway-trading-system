[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CorrelationPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedCorrelationSha256,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSelectionLogSha256,
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5NtsSelectionSemantics.ps1')
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $folder = Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/NtsSelectionSemantics'
    $ancestor = [IO.DirectoryInfo]$folder
    while (-not $ancestor.Exists) { $ancestor = $ancestor.Parent }
    Assert-Mt5TickLocalPath $ancestor.FullName
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $ReportPath = Join-Path $folder ('semantics-' + [guid]::NewGuid().ToString('N') + '.json')
}
$outputPath = [IO.Path]::GetFullPath($ReportPath)
Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($outputPath))
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
foreach ($protected in @($repo, (Join-Path $env:APPDATA 'MetaQuotes/Terminal/Common/Files'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/NtsCorrelation'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/Mt5CorrelatedTick'), (Join-Path $env:LOCALAPPDATA 'MoneyWay/NtsEnvironment'))) {
    $root = [IO.Path]::GetFullPath($protected).TrimEnd('\')
    if ($outputPath.Equals($root,[StringComparison]::OrdinalIgnoreCase) -or
        $outputPath.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'protected_output_location' }
}
if ((Test-Path -LiteralPath $outputPath) -or (Test-Path -LiteralPath ($outputPath + '.pending'))) { throw 'diagnostic_path_already_exists' }
$verified = Read-Mt5NtsSelectionEvidence $CorrelationPath $ExpectedCorrelationSha256
$source = $null
if ($verified.status -ceq 'passed') {
    $source = (Read-Mt5TickFile $CorrelationPath -Json).data
    if ($verified.assessment.log_sha256 -cne $ExpectedSelectionLogSha256) {
        $verified.status = 'data_unavailable'
        $verified.diagnostics += 'independent_selection_log_hash_mismatch'
    }
}
$assessment = Get-Mt5NtsSelectionSemantics $verified $source
$artifact = [pscustomobject][ordered]@{
    schema = 'moneyway.nts-selection-semantics-report'; version = 1; evidence_kind = 'offline_retained_evidence_semantics_audit'
    original_source_path = [IO.Path]::GetFullPath($CorrelationPath)
    original_log_path = [IO.Path]::GetFullPath($CorrelationPath + '.selection.log')
    expected_correlation_sha256 = $ExpectedCorrelationSha256; expected_log_sha256 = $ExpectedSelectionLogSha256
    input_integrity_status = $verified.status; input_diagnostics = $verified.diagnostics; assessment = $assessment
    source_contract = [pscustomobject]@{
        ubuntu_source = '4.5-1ubuntu4.2'; source_archive_sha256 = '19fe1d9f4664d445a69a96c71e8fdb60bcd8df24c73d1386e02287f7366ad422'
        debian_archive_sha256 = 'ad09d3f40cfb28ce22cbc0e266d0a4818d8e4f98ee24634a8cc851065f37a29b'
        compatibility = 'nine_reviewed_C_files_match_upstream_quilt_patches_touch_only_examples_official_binaries_match_capture_pins'
        limitation = 'no_reproducible_build_or_historical_libc_and_fenv_snapshot'; source_access_date = '2026-11-16'
        references = @('https://archive.ubuntu.com/ubuntu/pool/main/c/chrony/chrony_4.5-1ubuntu4.2.dsc',
            'https://chrony-project.org/doc/4.5/chrony.conf.html','https://chrony-project.org/doc/4.5/chronyc.html',
            'https://raw.githubusercontent.com/mlichvar/chrony/4.5/ntp_core.c',
            'https://raw.githubusercontent.com/mlichvar/chrony/4.5/sourcestats.c',
            'https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c',
            'https://raw.githubusercontent.com/mlichvar/chrony/4.5/reference.c',
            'https://raw.githubusercontent.com/mlichvar/chrony/4.5/sys_null.c',
            'https://raw.githubusercontent.com/bminor/glibc/glibc-2.39/stdio-common/printf_fp.c',
            'https://raw.githubusercontent.com/bminor/glibc/glibc-2.39/include/rounding-mode.h')
    }
    method_sources = @(@($PSCommandPath, (Join-Path $PSScriptRoot 'Mt5NtsSelectionSemantics.ps1'),
        (Join-Path $PSScriptRoot 'Mt5NtsSelectionEvidence.ps1'), (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1'),
        (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1'), (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')) | ForEach-Object {
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant() }
    })
}
Publish-Mt5TickFile $outputPath (($artifact | ConvertTo-Json -Depth 18 -Compress) + "`n")
[pscustomobject]@{ ReportPath = $outputPath; SHA256 = (Get-FileHash -LiteralPath $outputPath).Hash.ToLowerInvariant()
    InputIntegrityStatus = $verified.status; Status = $assessment.status; PrintedInterval = $assessment.printed_interval
    FormattingAwareInterval = $assessment.formatting_aware_interval; OverallUtcUncertainty = $assessment.overall_utc_uncertainty
    CanonicalReady = $false } | ConvertTo-Json -Depth 6
