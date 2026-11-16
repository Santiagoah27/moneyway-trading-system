[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('MoneyWay-NTS-Ubuntu-24.04')][string]$Distribution,
    [Parameter(Mandatory = $true)][ValidateRange(1, 60)][int]$QueryTimeoutSeconds,
    [Parameter(Mandatory = $true)][ValidateRange(15, 180)][int]$SessionTimeoutSeconds,
    [string]$ReportPath = '',
    [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1')
. (Join-Path $PSScriptRoot 'Mt5NtsSelectionEvidence.ps1')
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'intended_Windows_host_required' }
if ($SessionTimeoutSeconds -lt $QueryTimeoutSeconds + 10) { throw 'session_timeout_must_allow_query_and_cleanup' }
$session = New-Mt5TickNonce
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $folder = Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/NtsCorrelation'
    $ancestor = [IO.DirectoryInfo]$folder
    while (-not $ancestor.Exists) { $ancestor = $ancestor.Parent }
    Assert-Mt5TickLocalPath $ancestor.FullName
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $ReportPath = Join-Path $folder ("correlation-$session.json")
}
$report = [IO.Path]::GetFullPath($ReportPath)
Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($report))
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
foreach ($protected in @($repo, (Join-Path $env:APPDATA 'MetaQuotes/Terminal/Common/Files'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay/NtsEnvironment'), (Join-Path $env:LOCALAPPDATA 'MoneyWay/Evidence/Mt5CorrelatedTick'))) {
    $root = [IO.Path]::GetFullPath($protected).TrimEnd('\')
    if ($report.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $report.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'protected_output_location' }
}
if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath ($report + '.pending'))) { throw 'diagnostic_path_already_exists' }
foreach ($suffix in @('.selection.log','.selection.json','.selection.log.pending','.selection.json.pending')) {
    if (Test-Path -LiteralPath ($report + $suffix)) { throw 'diagnostic_path_already_exists' }
}

function Get-PreservedCorrelationInputs {
    $paths = @((Join-Path $env:APPDATA 'MetaQuotes/Terminal/Common/Files/MoneyWayAutomaticReadOnly/record-000000000643.json'),
        (Join-Path $env:APPDATA 'MetaQuotes/Terminal/Common/Files/MoneyWayAutomaticReadOnly/probe-000000000643.json'),
        (Join-Path $env:LOCALAPPDATA 'MoneyWay/mt5-receipt-observation/checkpoint.json'),
        (Join-Path $env:LOCALAPPDATA 'MoneyWay/NtsEnvironment/provision-d6ab96b1308c47f7aba31aaae87bc27c/provisioning-report.json'))
    foreach ($path in $paths) {
        $hash = $null
        if (Test-Path -LiteralPath $path) {
            Assert-Mt5TickLocalPath $path
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($path); sha256 = $hash }
    }
}

$before = @(Get-PreservedCorrelationInputs)
$inventory = Invoke-Mt5NtsWslCommand '--list --all --quiet'
$running = Invoke-Mt5NtsWslCommand '--list --running --quiet'
$available = $inventory.exit_code -eq 0 -and (Test-Mt5NtsDistribution $inventory.text $Distribution)
$wasRunning = $running.exit_code -eq 0 -and (Test-Mt5NtsDistribution $running.text $Distribution)
$linux = Read-Mt5TickFile (Join-Path $PSScriptRoot 'Invoke-Mt5NtsLinuxCapture.sh')
if ($available -and $running.exit_code -eq 0) {
    $capture = Invoke-Mt5NtsLiveCapture $Distribution $session $QueryTimeoutSeconds $SessionTimeoutSeconds $linux.text $CancellationToken
} else {
    $capture = [pscustomobject]@{ session = $session; error = 'selected_distribution_unavailable'; result = $null; exit_code = $null }
}
$assessment = Get-Mt5NtsCorrelationAssessment $capture $session
$selectionEvidence = Publish-Mt5NtsSelectionEvidence $capture $session $report $Distribution $assessment
$after = @(Get-PreservedCorrelationInputs)
$unchanged = ($before | ConvertTo-Json -Compress) -ceq ($after | ConvertTo-Json -Compress)
$rawHashes = @()
if ($null -ne $capture.result) {
    foreach ($name in @('configuration','authdata','ntpdata','capabilities','client_stderr','client_stdout')) {
        $rawHashes += [pscustomobject]@{ field = $name; sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($capture.result.$name)) }
    }
}
$artifact = [pscustomobject][ordered]@{
    schema = 'moneyway.windows-wsl-nts-correlation'; version = 1; correlation_id = $session
    source_kind = 'actual_environment_diagnostic_attempt'; distribution = $Distribution; distribution_found = $available
    distribution_running_before_capture = $wasRunning; distribution_termination_invoked = $false
    query_timeout_seconds = $QueryTimeoutSeconds; session_timeout_seconds = $SessionTimeoutSeconds
    client_profile = 'Ubuntu_chrony_4.5-1ubuntu4.2_executable_hash_pinned'; reference_server = 'time.cloudflare.com'
    capture = $capture; assessment = $assessment; retained_evidence_hashes = $rawHashes; selection_evidence = $selectionEvidence
    selected_existing_inputs_before = $before; selected_existing_inputs_after = $after; selected_input_hashes_unchanged = $unchanged
    preservation_scope = 'selected_record_probe_checkpoint_and_provisioning_report_not_entire_active_collector'
    raw_client_redaction = 'ntpdata_local_interface_address_withheld_before_retention_other_nonsecret_client_lines_preserved'
    windows_observation_provenance = 'DateTimeOffset.UtcNow_unverified_and_local_Stopwatch'
    linux_observation_provenance = 'GNU_date_realtime_bracketed_by_proc_uptime_CLOCK_BOOTTIME_0.01_second_resolution'
    protocol_scope = 'one_WSL_process_challenge_response_exchanges_separate_from_staging_and_initialization'
    authentication_scope = 'current_Linux_client_NTS_reference_only'; overall_utc_uncertainty = 'unresolved'
    windows_clock_authenticated = $false; mt5_native_utc_authenticated = $false
    canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
    tool_sources = @(@($PSCommandPath, (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1'),
        (Join-Path $PSScriptRoot 'Invoke-Mt5NtsLinuxCapture.sh'), (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1'),
        (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1'), (Join-Path $PSScriptRoot 'Mt5NtsSelectionEvidence.ps1')) | ForEach-Object {
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}
Publish-Mt5TickFile $report (($artifact | ConvertTo-Json -Depth 20 -Compress) + "`n")
[pscustomobject]@{ ReportPath = $report; SHA256 = (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant()
    NtsAuthenticatedMeasurementObserved = $assessment.nts_authenticated_measurement_observed
    SelectionGate = $selectionEvidence.assessment.gate; SelectionIntervalStatus = $selectionEvidence.assessment.chrony_selection_interval_status
    SelectionManifest = $selectionEvidence.manifest_file; SelectionLogSha256 = $selectionEvidence.raw_log_sha256
    WindowsClockCorrelation = 'unresolved'; OverallUtcUncertainty = 'unresolved'; SelectedInputHashesUnchanged = $unchanged
    CanonicalUtcConversionAuthorized = $false; CanonicalReady = $false; Diagnostics = $assessment.diagnostics } | ConvertTo-Json -Depth 4
