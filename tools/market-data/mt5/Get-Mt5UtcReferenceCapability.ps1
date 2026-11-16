[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateRange(1000, 30000)][int]$QueryTimeoutMilliseconds,
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5UtcReferenceCapability.ps1')
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Run this diagnostic on the intended Windows host; another environment cannot establish its capabilities.'
}

$nonce = 'd4542012e0528790739f573642ec1271eb0151d59758bb1c53dd29467d491dfa'
$original = Join-Path $env:APPDATA ('MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\' + $nonce)
$retained = Join-Path $env:LOCALAPPDATA ('MoneyWay\Evidence\Mt5CorrelatedTick\' + $nonce)
function EvidenceFingerprints {
    foreach ($location in @('original','retained')) {
        $folder = if ($location -eq 'original') { $original } else { $retained }
        $names = @('offer.txt','ready.json','request.txt','context.json','response.json','cancel.txt','evidence.json')
        if ($location -eq 'retained') { $names += @('retention.json','retention.sha256') }
        foreach ($name in $names) {
            $hash = $null
            try { $hash = (Read-Mt5TickFile (Join-Path $folder $name) -Raw).sha256 } catch { }
            [pscustomobject]@{ location = $location; file_name = $name; sha256 = $hash }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    Assert-Mt5TickLocalPath $env:LOCALAPPDATA
    $folder = Join-Path $env:LOCALAPPDATA 'MoneyWay\Evidence\UtcReferenceCapability'
    $ancestor = [IO.DirectoryInfo]$folder
    while (-not $ancestor.Exists) { $ancestor = $ancestor.Parent }
    Assert-Mt5TickLocalPath $ancestor.FullName
    [IO.Directory]::CreateDirectory($folder) | Out-Null
    $ReportPath = Join-Path $folder ('audit-' + [guid]::NewGuid().ToString('N') + '.json')
}
$report = [IO.Path]::GetFullPath($ReportPath)
Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($report))
if ($report -notmatch '^[A-Za-z]:[\\/]' -or $report.Substring(2).Contains(':')) { throw 'absolute_local_report_path_required' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
foreach ($protected in @($repo, (Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files'),
    (Join-Path $env:LOCALAPPDATA 'MoneyWay\Evidence\Mt5CorrelatedTick'))) {
    if ($report.StartsWith($protected.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'report_path_overlaps_protected_evidence_or_repository' }
}
if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath ($report + '.pending'))) { throw 'report_path_already_exists' }
$started = [datetimeoffset]::UtcNow.ToString('o')
$counter = [Diagnostics.Stopwatch]::GetTimestamp()
$beforeHashes = @(EvidenceFingerprints)
$inventory = Get-Mt5UtcHostInventory $QueryTimeoutMilliseconds
$sourceQuery = Invoke-Mt5BoundedTimeQuery '/query /source' $QueryTimeoutMilliseconds
$statusQuery = Invoke-Mt5BoundedTimeQuery '/query /status /verbose' $QueryTimeoutMilliseconds
$state = Get-Mt5WindowsTimeState $statusQuery
$ntpQuery = Invoke-Mt5BoundedTimeQuery '/stripchart /computer:time.cloudflare.com /samples:1 /period:1 /rdtsc /dataonly' $QueryTimeoutMilliseconds
$ntp = Get-Mt5StripchartEvidence $ntpQuery.raw $ntpQuery.exit_code $ntpQuery.timed_out 'time.cloudflare.com'
$tcp = Test-Mt5NtsTcpReachability $QueryTimeoutMilliseconds
$https = Get-Mt5HttpsDateComparison $QueryTimeoutMilliseconds
$pins = @{
    'evidence.json' = 'e8627f3c80c4bae81dcfd17f3397df525e855fbdc12d368ffc252f5f2230379b'
    'response.json' = '4b9760ac783abe49ada999f71a4c7df0e3d7a6481a622ae8a4f202e4badd7557'
    'retention.json' = '306b5fa501307af9385f9baabe4dd71be8cfd2047add79a6e4c74e7b14ec18fa'
}
$matched = @($beforeHashes | Where-Object { $_.location -ceq 'retained' -and $pins.ContainsKey($_.file_name) -and $_.sha256 -ceq $pins[$_.file_name] }).Count -eq 3
$prior = [pscustomobject]@{ nonce = $nonce; pinned_files_match = $matched
    verification_scope = 'three_previously_admitted_content_pins_rechecked_not_a_new_full_lineage_admission'
    retrospectively_authenticated = $false; original_native_progression_milliseconds = $null; original_host_interval_milliseconds = $null
    original_native_minus_computer_GMT_seconds = $null; original_reference_before = $null; original_reference_after = $null }
if ($matched) {
    $previousFile = Read-Mt5TickFile (Join-Path $retained 'evidence.json') -Json
    if ($previousFile.sha256 -cne $pins['evidence.json']) { throw 'admitted_evidence_changed_during_review' }
    $previous = $previousFile.data
    $prior.original_native_progression_milliseconds = $previous.response.tick.time_msc - $previous.response.baseline.time_msc
    $prior.original_host_interval_milliseconds = $previous.assessment.uncorrected_host_interval_milliseconds
    $prior.original_native_minus_computer_GMT_seconds = $previous.assessment.exact_native_tick_minus_computer_gmt_seconds
    $prior.original_reference_before = $previous.reference_before.parsed
    $prior.original_reference_after = $previous.reference_after.parsed
}
$decision = Get-Mt5UtcCapabilityDecision $inventory.platform $state $inventory.clients $tcp $ntp $https $prior
$afterHashes = @(EvidenceFingerprints)
if (($beforeHashes | ConvertTo-Json -Compress) -cne ($afterHashes | ConvertTo-Json -Compress)) { throw 'existing_evidence_changed_during_audit' }
$artifact = [pscustomobject][ordered]@{
    schema = 'moneyway.mt5-utc-reference-capability'; version = 1
    invocation = [pscustomobject]@{ tool = 'Get-Mt5UtcReferenceCapability.ps1'; query_timeout_milliseconds = $QueryTimeoutMilliseconds
        external_endpoints = @('time.cloudflare.com:4460/TCP','time.cloudflare.com:123/UDP','https://developers.cloudflare.com/time-services/nts/') }
    collection_started_at_host_utc_candidate = $started; collection_finished_at_host_utc_candidate = [datetimeoffset]::UtcNow.ToString('o')
    monotonic_started = $counter; monotonic_finished = [Diagnostics.Stopwatch]::GetTimestamp(); monotonic_frequency = [Diagnostics.Stopwatch]::Frequency
    host_clock_provenance = 'DateTimeOffset.UtcNow_computer_clock_unverified'; host_scope = 'local_execution_host_no_device_identifier_collected'
    inventory = $inventory; windows_time_state = $state
    client_documentation = @(
        [pscustomobject]@{ implementation = 'chrony'; assessed_manual_version = '4.7'; native_windows_support = 'not_documented_in_upstream_supported_systems'
            documented_platforms = @('Linux','FreeBSD','NetBSD','OpenBSD','macOS','illumos'); bounded_no_clock_adjustment_mode = 'chronyd -Q -t timeout'
            privilege_scope = 'nonroot_when_configuration_needs_no_privileged_features'; machine_readable_monitoring = 'chronyc -c_reports'
            combined_bounded_authentication_and_uncertainty_capture = 'requires_separate_adapter_validation'
            source = 'https://chrony-project.org/doc/4.7/chronyd.html' },
        [pscustomobject]@{ implementation = 'NTPsec'; assessed_manual_version = 'current_official_platform_policy_reviewed_2026_11_16'
            native_windows_support = 'legacy_Windows_toolchain_support_removed_native_build_not_verified'
            documented_platforms = @('POSIX_Unix_like_systems'); bounded_no_clock_adjustment_NTS_mode = 'unverified'
            clock_setting_quit_mode_eligible = $false; source = 'https://www.ntpsec.org/supported-platforms.html' },
        [pscustomobject]@{ implementation = 'ntpd-rs'; assessed_manual_version = 'latest_documentation_reviewed_2026_11_16'
            native_windows_support = 'not_established_by_reviewed_installation_documentation'; documented_installation_platforms = @('Linux','FreeBSD','macOS_source_build')
            bounded_no_clock_adjustment_NTS_mode = 'unverified'; source = 'https://docs.ntpd-rs.pendulum-project.org/latest/guide/installation/' }
    )
    source_query = Protect-Mt5TimeQuery $sourceQuery; status_query = Protect-Mt5TimeQuery $statusQuery
    ordinary_ntp_query = [pscustomobject]@{ arguments = $ntpQuery.arguments; exit_code = $ntpQuery.exit_code; timed_out = $ntpQuery.timed_out
        utc_before_candidate = $ntpQuery.utc_before_candidate; utc_after_candidate = $ntpQuery.utc_after_candidate
        raw_csv_samples = @($ntpQuery.raw -split '\r?\n' | Where-Object { $_ -match '^\s*\d+,\s*\d+,\s*\d+,\s*[+-]?\d+(?:\.\d+)?,\s*[+-]?\d+(?:\.\d+)?\s*$' }); parsed = $ntp }
    nts_tcp_prerequisite = $tcp; https_date_comparison = $https; decision = $decision
    previous_admitted_capture = $prior; evidence_fingerprints_before_and_after_equal = $true; evidence_fingerprints = $beforeHashes
    policy_review_date = '2026-11-16'
    sources = @('https://www.rfc-editor.org/rfc/rfc8915.html','https://developers.cloudflare.com/time-services/nts/',
        'https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/how-the-windows-time-service-works',
        'https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/Windows-Time-Service-Tools-and-Settings',
        'https://chrony-project.org/','https://chrony-project.org/doc/4.7/chronyd.html','https://chrony-project.org/doc/4.7/chronyc.html',
        'https://chrony-project.org/doc/4.7/chrony.conf.html','https://www.ntpsec.org/supported-platforms.html',
        'https://docs.ntpd-rs.pendulum-project.org/latest/guide/installation/','https://learn.microsoft.com/en-us/windows/wsl/basic-commands')
    tool_sources = @(@($PSCommandPath, (Join-Path $PSScriptRoot 'Mt5UtcReferenceCapability.ps1'),
        (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1'), (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')) | ForEach-Object {
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    authenticated_utc_reference_observed = $false; host_clock_error_bound_seconds = $null; canonical_utc_conversion_authorized = $false
    limitations = @('bounded_discovery_cannot_exclude_clients_in_uninspected_locations', 'WSL_presence_does_not_establish_a_compatible_distribution_or_clock_binding',
        'TCP_and_HTTPS_success_are_not_authenticated_NTP', 'NTS_KE_and_authenticated_NTP_exchange_not_exercised',
        'NTS_cannot_remove_network_asymmetry_or_establish_broker_timezone_closure_or_availability')
}
Publish-Mt5TickFile $report (($artifact | ConvertTo-Json -Depth 20 -Compress) + "`n")
[pscustomobject]@{ ReportPath = $report; SHA256 = (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant()
    Recommendation = $decision.recommendation; AuthenticatedUtcReferenceObserved = $false; CanonicalUtcConversionAuthorized = $false }
