[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CapturePath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedCaptureSha256,
    [string]$ReassessmentPath = '',
    [string]$ExpectedReassessmentSha256 = '',
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5NtsTemporalUncertainty.ps1')
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $folder = Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/NtsUncertainty'
    $ancestor = [IO.DirectoryInfo]$folder
    while (-not $ancestor.Exists) { $ancestor = $ancestor.Parent }
    Assert-Mt5TickLocalPath $ancestor.FullName
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $ReportPath = Join-Path $folder ('uncertainty-' + [guid]::NewGuid().ToString('N') + '.json')
}
$report = [IO.Path]::GetFullPath($ReportPath)
Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($report))
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
foreach ($protected in @($repo, (Join-Path $env:APPDATA 'MetaQuotes/Terminal/Common/Files'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay/NtsEnvironment'), (Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/NtsCorrelation'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/Mt5CorrelatedTick'))) {
    $root = [IO.Path]::GetFullPath($protected).TrimEnd('\')
    if ($report.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $report.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'protected_output_location' }
}
if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath ($report + '.pending'))) { throw 'diagnostic_path_already_exists' }
$evidence = Read-Mt5NtsUncertaintyEvidence $CapturePath $ExpectedCaptureSha256 $ReassessmentPath $ExpectedReassessmentSha256
$assessment = Get-Mt5NtsTemporalUncertainty $evidence
$toolComparisons = @()
if ($evidence.status -ceq 'passed') {
    foreach ($name in @('Get-Mt5NtsTimeCorrelation.ps1','Mt5NtsCorrelation.ps1','Invoke-Mt5NtsLinuxCapture.sh','Mt5CorrelatedTickEvidence.ps1','Mt5NativeUtcEvidence.ps1')) {
        $pin = @($evidence.capture.tool_sources | Where-Object { $_.file_name -ceq $name })
        $current = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        $toolComparisons += [pscustomobject]@{ file_name = $name; retained_runtime_hash = $(if ($pin.Count -eq 1) { $pin[0].sha256 })
            current_hash = $current; current_bytes_match_retained_hash = $pin.Count -eq 1 -and $pin[0].sha256 -ceq $current }
    }
}
$artifact = [pscustomobject][ordered]@{
    schema = 'moneyway.nts-temporal-uncertainty-report'; version = 1
    evidence_kind = 'offline_assessment_of_retained_evidence'; source_file = [IO.Path]::GetFileName($CapturePath)
    source_sha256 = $evidence.source_sha256; reassessment_sha256 = $evidence.reassessment_sha256
    raw_fields_verified = $evidence.raw_fields_verified; assessment = $assessment
    acquisition_tool_hash_comparison = $toolComparisons
    provenance_scope = 'content_and_recorded_bindings_revalidated_no_reexecution_or_independent_packet_cryptography'
    historical_runtime_source_limit = 'changed_tool_byte_snapshots_are_not_recovered_by_comparing_current_files'
    technical_reference_access_date = '2026-11-16'
    technical_references = @('https://chrony-project.org/doc/4.5/chronyc.html','https://chrony-project.org/doc/4.5/chronyd.html',
        'https://raw.githubusercontent.com/mlichvar/chrony/4.5/reference.c','https://www.rfc-editor.org/rfc/rfc8915.html',
        'https://ubuntu.com/wsl/docs/stable/explanation/time-sync/')
    method_sources = @(@($PSCommandPath, (Join-Path $PSScriptRoot 'Mt5NtsTemporalUncertainty.ps1'),
        (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1'), (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1'),
        (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')) | ForEach-Object {
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}
Publish-Mt5TickFile $report (($artifact | ConvertTo-Json -Depth 20 -Compress) + "`n")
[pscustomobject]@{ ReportPath = $report; SHA256 = (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant()
    InputIntegrityStatus = $evidence.status; LinuxNts = $assessment.linux_nts_authentication
    OverallUtcUncertainty = $assessment.overall_utc_uncertainty; WindowsHostUtcErrorBoundSeconds = $null
    CanonicalUtcConversionAuthorized = $false; CanonicalReady = $false } | ConvertTo-Json -Depth 4
