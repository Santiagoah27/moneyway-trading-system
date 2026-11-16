# Synthetic interpretation and safety tests. No NTS cryptography or network exchange occurs here.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5UtcReferenceCapability.ps1')
$passed = 0; $failed = 0
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Query([string]$Raw = "Leap Indicator: 3(not synchronized)`nStratum: 0`nSource: time.windows.com,0x9") {
    [pscustomobject]@{ arguments = '/query /status /verbose'; raw = $Raw; exit_code = 0; timed_out = $false
        utc_before_candidate = '2026-11-16T18:00:00Z'; utc_after_candidate = '2026-11-16T18:00:01Z'; elapsed_milliseconds = [decimal]'1.2345678' }
}
function Platform {
    [pscustomobject]@{ is_windows = $true; w32tm_present = $true; part_of_domain = $false; service = [pscustomobject]@{ status = 'Running' } }
}
function Https {
    [pscustomobject]@{ outcome = 'passed'; request_attempted = $true; default_os_tls_validation_succeeded = $true; date_header = 'Mon, 16 Nov 2026 18:00:00 GMT' }
}
$ntp = [pscustomobject]@{ status = 'context_specific'; reference_authentication = 'unverified'; reference_host = 'time.cloudflare.com' }
$prior = [pscustomobject]@{ pinned_files_match = $true }
function Decision($HostPlatform = (Platform), $Clients = @(), $Tcp = [pscustomobject]@{connected=$true}, $NtpResult = $ntp, $HttpsResult = (Https)) {
    Get-Mt5UtcCapabilityDecision $HostPlatform (Get-Mt5WindowsTimeState (Query)) $Clients $Tcp $NtpResult $HttpsResult $prior
}
$auth = '192.0.2.1 NTS 1 15 256 1m 0 0 8 100'
$packet = "Remote address : 192.0.2.1`nAuthenticated : Yes`nOffset : +0.000123456 seconds`nPeer delay : 0.001234567 seconds"
function Provenance {
    [pscustomobject]@{ implementation = 'chrony'; version = '4.7'; endpoint = '192.0.2.1'
        authdata_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($auth))
        ntpdata_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($packet)); configuration_sha256 = 'a' * 64
        certificate_name_chain_and_time_checks = 'verified_by_review_of_client_configuration'; tls_minimum = '1.3' }
}
Case 'English Windows status parses leap and stratum without authentication' {
    $state = Get-Mt5WindowsTimeState (Query)
    Check ($state.outcome -eq 'passed' -and $state.leap_indicator -eq 3 -and $state.stratum -eq 0 -and $state.source_authentication -eq 'unverified')
}
Case 'Unsynchronized W32Time state remains explicit' { Check ((Get-Mt5WindowsTimeState (Query)).synchronization -eq 'reported_unsynchronized') }
Case 'Reported synchronized state never proves authenticated source' {
    $s = Get-Mt5WindowsTimeState (Query "Leap Indicator: 0(no warning)`nStratum: 2")
    Check ($s.synchronization -eq 'reported_synchronized' -and $s.source_authentication -eq 'unverified')
}
Case 'Access denied preserves exact public failure text and native exit code' {
    $q = Query 'The following error occurred: Access is denied. (0x80070005)'; $q.exit_code = -2147024891
    $safe = Protect-Mt5TimeQuery $q
    Check ($safe.raw_sanitized -ceq $q.raw -and $safe.exit_code -eq $q.exit_code -and $safe.access_denied)
    Check ((Get-Mt5WindowsTimeState $q).outcome -eq 'data_unavailable')
}
Case 'Unknown localized status and timeout remain unverified' {
    $q = Query 'unrecognized localized status'; Check ((Get-Mt5WindowsTimeState $q).synchronization -eq 'unverified')
    $q = Query; $q.timed_out = $true; Check ((Get-Mt5WindowsTimeState $q).outcome -eq 'data_unavailable')
}
Case 'Missing Windows service observation cannot become an authenticated reference' {
    $p = Platform; $p.service = $null; $p.w32tm_present = $false
    $a = Decision $p
    Check (-not $a.mechanisms[0].client_installed -and -not $a.authenticated_utc_reference_observed)
}
Case 'Private source identifiers and reference IDs are withheld' {
    $q = Query "Leap Indicator: 3`nSource: dc-secret.internal.example`nReferenceId: 0x0A010203 (private-host)`nsecret-user"
    $safe = Protect-Mt5TimeQuery $q
    Check ($safe.raw_sanitized -notmatch 'dc-secret|private-host|0A010203|secret-user' -and $safe.raw_sanitized -match 'redacted_identifier')
}
Case 'Ordinary NTP measurement retains exact decimals and stays unauthenticated' {
    $ft = ([datetimeoffset]'2026-11-16T18:00:00Z').UtcDateTime.ToFileTimeUtc()
    $r = Get-Mt5StripchartEvidence "1, 2, $ft, +00.1234567, +00.0001234" 0 $false 'time.cloudflare.com'
    Check ($r.status -eq 'context_specific' -and $r.samples[0].reported_ntp_offset_seconds -eq [decimal]'0.0001234')
    Check ($r.reference_authentication -eq 'unverified' -and -not (Decision).mechanisms[0].authentication_verified)
}
Case 'Absent external reference remains data unavailable' {
    $a = Decision -NtpResult ([pscustomobject]@{status='data_unavailable';reference_host='time.cloudflare.com'})
    Check (-not $a.mechanisms[0].time_measurement_obtained -and -not $a.authenticated_utc_reference_observed)
}
Case 'Missing NTS client produces a prerequisite decision with scoped discovery' {
    $a = Decision
    Check ($a.recommendation -eq 'REQUIRE_ENVIRONMENT_PREREQUISITE' -and $a.mechanisms[3].discovery -eq 'not_found_in_inspected_locations')
    Check ($null -eq $a.mechanisms[3].client_installed)
}
Case 'Detected executable on unsupported or unverified native platform is not selected' {
    $a = Decision -Clients @([pscustomobject]@{found=$true;role='NTS_candidate';native_windows_NTS_support='unverified'})
    Check ($a.mechanisms[3].client_installed -and $null -eq $a.feasible_candidate_now -and -not $a.authentication_attempted)
}
Case 'Unreachable NTS TCP prerequisite does not produce authentication evidence' {
    $a = Decision -Tcp ([pscustomobject]@{connected=$false})
    Check (-not $a.mechanisms[2].tcp4460_reachable -and -not $a.mechanisms[2].service_reachable -and -not $a.mechanisms[2].authentication_verified)
}
Case 'Successful TCP connection is distinct from NTS service validation' {
    $a = Decision
    Check ($a.mechanisms[2].tcp4460_reachable -and $null -eq $a.mechanisms[2].service_reachable -and -not $a.authenticated_utc_reference_observed)
}
Case 'HTTPS transport authentication and Date header cannot authenticate UTC' {
    $a = Decision
    Check ($a.mechanisms[4].authentication_verified -and $a.mechanisms[4].authentication_scope -eq 'TLS_transport_only')
    Check (-not $a.mechanisms[4].authenticated_time_synchronization -and -not $a.authenticated_utc_reference_observed)
}
Case 'Failed HTTPS verification preserves attempted versus verified distinction' {
    $h = Https; $h.outcome = 'data_unavailable'; $h.default_os_tls_validation_succeeded = $false; $h.date_header = $null
    $a = Decision -HttpsResult $h
    Check ($a.mechanisms[4].authentication_attempted -and -not $a.mechanisms[4].authentication_verified)
}
Case 'NTS key establishment report alone is insufficient without authenticated packet report' {
    $r = Get-Mt5ChronyReportInterpretation $auth 'Remote address : 192.0.2.1' (Provenance)
    Check ($r.outcome -eq 'human_validation_required' -and -not $r.packet_authentication_reported)
}
Case 'Documented chrony client reports with complete synthetic provenance are recognized as claims only' {
    $r = Get-Mt5ChronyReportInterpretation $auth $packet (Provenance)
    Check ($r.outcome -eq 'passed' -and $r.nts_mode_reported -and $r.packet_authentication_reported)
    Check (-not $r.cryptographic_verification_performed_by_parser -and -not $r.authenticated_utc_reference_observed)
}
Case 'Reported packet authentication failure is rejected' {
    $bad = $packet.Replace('Yes','No'); $p = Provenance; $p.ntpdata_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($bad))
    Check ((Get-Mt5ChronyReportInterpretation $auth $bad $p).outcome -eq 'human_validation_required')
}
Case 'Incomplete certificate verification cannot qualify' {
    $p = Provenance; $p.certificate_name_chain_and_time_checks = 'unverified'
    Check (-not (Get-Mt5ChronyReportInterpretation $auth $packet $p).provenance_complete)
}
Case 'Unsupported TLS minimum cannot qualify as RFC8915 verification' {
    $p = Provenance; $p.tls_minimum = '1.2'
    Check ((Get-Mt5ChronyReportInterpretation $auth $packet $p).outcome -eq 'human_validation_required')
}
Case 'Missing provenance does not cause fabricated successful recognition' {
    Check ((Get-Mt5ChronyReportInterpretation $auth $packet $null).outcome -eq 'human_validation_required')
    Check ((Get-Mt5ChronyReportInterpretation $auth $packet ([pscustomobject]@{})).outcome -eq 'human_validation_required')
}
Case 'Mismatched output hashes invalidate client report provenance' {
    $p = Provenance; $p.authdata_sha256 = 'b' * 64
    Check (-not (Get-Mt5ChronyReportInterpretation $auth $packet $p).provenance_complete)
}
Case 'NTS mode and authenticated packet claims must refer to the same endpoint' {
    $other = $packet.Replace('192.0.2.1','192.0.2.2'); $p = Provenance
    $p.ntpdata_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($other))
    Check ((Get-Mt5ChronyReportInterpretation $auth $other $p).outcome -eq 'human_validation_required')
}
Case 'Symmetric-key authenticated packet claim cannot be relabeled NTS' {
    $other = $auth.Replace('NTS','SK'); $p = Provenance
    $p.authdata_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($other))
    Check ((Get-Mt5ChronyReportInterpretation $other $packet $p).outcome -eq 'human_validation_required')
}
Case 'Recognized client authentication claims retain unresolved timing uncertainty' {
    $r = Get-Mt5ChronyReportInterpretation $auth $packet (Provenance)
    Check ($r.outcome -eq 'passed' -and $r.timing_uncertainty -match '^unresolved_')
}
Case 'Domain membership alone cannot demonstrate authenticated hierarchy packets' {
    $p = Platform; $p.part_of_domain = $true; $a = Decision $p
    Check ($a.mechanisms[1].outcome -eq 'human_validation_required' -and -not $a.mechanisms[1].authentication_verified)
}
Case 'Workgroup host has no demonstrated AD DS time hierarchy' { Check ((Decision).mechanisms[1].outcome -eq 'not_applicable') }
Case 'Every mechanism preserves ten separate capability stages' {
    foreach ($m in (Decision).mechanisms) {
        foreach ($field in @('protocol_documented','platform_supported','client_installed','service_reachable','authentication_attempted',
            'authentication_verified','time_measurement_obtained','timing_uncertainty_recorded','related_to_actual_MT5_capture','historical_applicability_established')) {
            Check ($field -in $m.PSObject.Properties.Name)
        }
        Check ('available' -notin $m.PSObject.Properties.Name)
    }
}
Case 'Stable read-only observations yield the same capability decision' {
    Check (((Decision) | ConvertTo-Json -Depth 10 -Compress) -ceq ((Decision) | ConvertTo-Json -Depth 10 -Compress))
}
Case 'The retained MT5 capture relationship cannot retroactively authenticate its reference' {
    $a = Decision
    Check ($a.mechanisms[5].related_to_actual_MT5_capture -and -not $a.mechanisms[5].authentication_verified)
    Check (-not $a.mechanisms[0].related_to_actual_MT5_capture -and -not $a.mechanisms[2].related_to_actual_MT5_capture)
}
Case 'Canonical authorization, historical mapping and provider DST remain blocked' {
    $a = Decision
    Check (-not $a.canonical_utc_conversion_authorized -and -not $a.utc_conversion_authenticated -and -not $a.canonical_ready)
    Check ($a.historical_mapping -eq 'unresolved' -and $a.provider_DST -eq 'unresolved' -and $a.strategy_timezone -eq 'America/Bogota')
}
$directory = Join-Path $env:TEMP ('moneyway-utc-capability-tests-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($directory) | Out-Null
try {
    Case 'Existing source-like receipt and checkpoint outputs cannot be overwritten' {
        foreach ($name in @('record.json','checkpoint.json','retention.json')) {
            $path = Join-Path $directory $name; Publish-Mt5TickFile $path '{"synthetic":"immutable"}'
            $before = (Read-Mt5TickFile $path).sha256; $rejected = $false
            try { & (Join-Path $tools 'Get-Mt5UtcReferenceCapability.ps1') -QueryTimeoutMilliseconds 1000 -ReportPath $path | Out-Null } catch { $rejected = $true }
            Check ($rejected -and (Read-Mt5TickFile $path).sha256 -ceq $before)
        }
    }
    Case 'Queries and report collection contain no clock, service, registry or trading mutations' {
        $text = [IO.File]::ReadAllText((Join-Path $tools 'Mt5UtcReferenceCapability.ps1')) + [IO.File]::ReadAllText((Join-Path $tools 'Get-Mt5UtcReferenceCapability.ps1'))
        Check ($text -notmatch '/resync|/config|Restart-Service|Start-Service|Stop-Service|Set-Service|Set-Date|Set-ItemProperty|OrderSend|CTrade|StrategyReplayContext|RunAs')
        Check ($text -notmatch 'SslStream|AesGcm|Start-Process')
        Check ($text -match "Arguments = '--list --running --quiet'" -and $text -match 'StandardInput.Close\(\)')
        Check ($text -match 'Invoke-Mt5BoundedTimeQuery' -and $text -match 'existing_evidence_changed_during_audit')
    }
} finally {
    Get-ChildItem -LiteralPath $directory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Remove-Item -LiteralPath $directory
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'UTC reference feasibility checks failed.' }
