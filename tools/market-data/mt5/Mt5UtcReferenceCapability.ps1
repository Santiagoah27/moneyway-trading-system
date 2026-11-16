Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1')

function Protect-Mt5TimeQuery($Query) {
    $safe = @()
    foreach ($line in ($Query.raw -split '\r?\n')) {
        if ($line -match '^\s*(?:The following error occurred: )?Access is denied\.?\s*\(0x80070005\)\s*$') { $safe += $line; continue }
        if ($line -match '^\s*(Source|ReferenceId)\s*:') {
            if ($line -match '^\s*Source\s*:\s*(time\.windows\.com|time\.cloudflare\.com)(,0x[0-9a-f]+)?\s*$') { $safe += $line }
            else { $safe += ($Matches[1] + ': [redacted_identifier]') }
            continue
        }
        if ($line -match '^\s*(Leap Indicator|Stratum|Precision|Root Delay|Root Dispersion|Phase Offset|ClockRate|Poll Interval|State Machine|Time Source Flags|Server Role)\s*:\s*[-+0-9.(]') {
            # Preserve scalar measurements, excluding free text which could contain device names.
            $safe += [regex]::Match($line, '^\s*[^:]+:\s*[-+0-9.]+').Value
            continue
        }
        if ($line -match '^\s*Last Sync Error\s*:\s*(\d+)') { $safe += "Last Sync Error: $($Matches[1])"; continue }
        if ($Query.arguments -ceq '/query /source' -and $line -match '^\s*(time\.windows\.com|time\.cloudflare\.com)(,0x[0-9a-f]+)?\s*$') { $safe += $line }
    }
    [pscustomobject]@{ arguments = $Query.arguments; exit_code = $Query.exit_code; timed_out = $Query.timed_out
        raw_sanitized = $safe -join "`n"; identifiers_and_unrecognized_lines_withheld = $true
        access_denied = $Query.raw -match '0x80070005'; utc_before_candidate = $Query.utc_before_candidate
        utc_after_candidate = $Query.utc_after_candidate; elapsed_milliseconds = $Query.elapsed_milliseconds }
}

function Get-Mt5WindowsTimeState($Query) {
    $leap = $null; $stratum = $null
    if ($Query.raw -match '(?m)^\s*Leap Indicator\s*:\s*([0-3])\b') { $leap = [int]$Matches[1] }
    if ($Query.raw -match '(?m)^\s*Stratum\s*:\s*(\d+)\b') { $stratum = [int]$Matches[1] }
    $valid = -not $Query.timed_out -and $Query.exit_code -eq 0 -and $null -ne $leap
    [pscustomobject]@{
        outcome = $(if ($valid) { 'passed' } else { 'data_unavailable' })
        classification = $(if ($valid) { 'context_specific' } else { 'unresolved' })
        leap_indicator = $leap; stratum = $stratum
        synchronization = $(if (-not $valid) { 'unverified' } elseif ($leap -eq 3) { 'reported_unsynchronized' } else { 'reported_synchronized' })
        source_authentication = 'unverified'; access_denied = $Query.raw -match '0x80070005'
        parser_scope = 'English_W32Time_status_unknown_localization_remains_unverified'
    }
}

# Interprets established client reports. It performs no TLS, NTS-KE or packet cryptography.
# This audit does not import client reports or execute an unapproved installed binary.
function Get-Mt5ChronyReportInterpretation([string]$AuthData, [string]$NtpData, $Provenance) {
    $complete = $false
    $required = @('implementation','version','endpoint','authdata_sha256','ntpdata_sha256','configuration_sha256','certificate_name_chain_and_time_checks','tls_minimum')
    $properties = @()
    if ($null -ne $Provenance) { $properties = @($Provenance.PSObject.Properties | ForEach-Object Name) }
    if ($null -ne $Provenance -and @($required | Where-Object { $_ -notin $properties }).Count -eq 0) {
        $complete = $Provenance.implementation -ceq 'chrony' -and $Provenance.version -ceq '4.7' -and
            $Provenance.authdata_sha256 -ceq (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($AuthData))) -and
            $Provenance.ntpdata_sha256 -ceq (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($NtpData))) -and
            $Provenance.configuration_sha256 -cmatch '^[0-9a-f]{64}$' -and
            $Provenance.certificate_name_chain_and_time_checks -ceq 'verified_by_review_of_client_configuration' -and
            $Provenance.tls_minimum -ceq '1.3'
    }
    $endpoint = if ($complete) { [regex]::Escape($Provenance.endpoint) } else { '(?!)' }
    $ntsMode = $AuthData -match ("(?m)^$endpoint\s+NTS\s+\d+\s+\d+\s+\d+\s+\S+\s+\d+\s+\d+\s+\d+\s+\d+\s*$")
    $sameEndpoint = $NtpData -match ("(?m)^Remote address\s*:\s*$endpoint(?:\s|$)")
    $authenticated = $NtpData -cmatch '(?m)^Authenticated\s*:\s*Yes\s*$'
    $measurement = $NtpData -match '(?m)^Offset\s*:\s*[+-]?\d+\.\d+ seconds\s*$'
    [pscustomobject]@{
        outcome = $(if ($complete -and $ntsMode -and $sameEndpoint -and $authenticated -and $measurement) { 'passed' } else { 'human_validation_required' })
        interpretation_scope = 'client_report_claims_only_chrony_4_7_human_readable_reports'
        provenance_complete = $complete; nts_mode_reported = $ntsMode; packet_authentication_reported = $authenticated
        time_measurement_reported = $measurement; certificate_verification_evidence_complete = $complete
        timing_uncertainty = 'unresolved_requires_measured_delay_dispersion_asymmetry_and_clock_model'
        cryptographic_verification_performed_by_parser = $false; authenticated_utc_reference_observed = $false
    }
}

function Test-Mt5NtsTcpReachability([int]$TimeoutMilliseconds) {
    $client = New-Object Net.Sockets.TcpClient
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $before = [datetimeoffset]::UtcNow.ToString('o')
    $connected = $false; $errorCode = $null
    try {
        $task = $client.ConnectAsync('time.cloudflare.com', 4460)
        if ($task.Wait($TimeoutMilliseconds)) { $connected = $client.Connected }
        else { $errorCode = 'connection_timeout' }
    } catch { $errorCode = 'connection_failed'; } finally { $client.Dispose() }
    [pscustomobject]@{ host = 'time.cloudflare.com'; port = 4460; transport = 'TCP'
        outcome = $(if ($connected) { 'passed' } else { 'data_unavailable' }); connected = $connected
        error = $errorCode; elapsed_milliseconds = $watch.Elapsed.TotalMilliseconds
        utc_before_candidate = $before; utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o')
        tls_attempted = $false; nts_ke_attempted = $false; ntp_packet_authentication_attempted = $false }
}

function Get-Mt5HttpsDateComparison([int]$TimeoutMilliseconds) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $before = [datetimeoffset]::UtcNow.ToString('o')
    $uri = 'https://developers.cloudflare.com/time-services/nts/'
    $date = $null; $age = $null; $httpStatus = $null; $success = $false; $errorCode = $null; $attempted = $false
    if ($null -ne [Net.ServicePointManager]::ServerCertificateValidationCallback) {
        $errorCode = 'existing_custom_certificate_validation_policy_unverified'
    } else {
        $request = [Net.HttpWebRequest]::Create($uri)
        $request.Method = 'HEAD'; $request.Timeout = $TimeoutMilliseconds; $request.ReadWriteTimeout = $TimeoutMilliseconds
        $request.AllowAutoRedirect = $false
        $attempted = $true
        try {
            $response = $request.GetResponse()
            try {
                $httpStatus = [int]$response.StatusCode
                $headerDate = $response.Headers['Date']; $headerAge = $response.Headers['Age']
                if ($headerDate -cmatch '^[A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$') { $date = $headerDate }
                if ($headerAge -match '^\d+$') { $age = $headerAge }
                $success = $true
            } finally { $response.Dispose() }
        } catch { $errorCode = 'https_request_failed'; } finally { $request.Abort() }
    }
    [pscustomobject]@{ uri = $uri; method = 'HEAD'; outcome = $(if ($success) { 'passed' } else { 'data_unavailable' })
        request_attempted = $attempted; default_os_tls_validation_succeeded = $success; http_status = $httpStatus; date_header = $date; age_header_seconds = $age
        error = $errorCode; utc_before_candidate = $before; utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o')
        elapsed_milliseconds = $watch.Elapsed.TotalMilliseconds; date_precision = 'HTTP_second_granularity_generation_cache_and_age_uncertain'
        authenticated_time_synchronization = $false; authenticated_utc_reference_observed = $false }
}

function Get-Mt5UtcCapabilityDecision($Platform, $WindowsState, $Clients, $Tcp, $Ntp, $Https, $PriorCapture) {
    $found = @($Clients | Where-Object { $_.found -and $_.role -ceq 'NTS_candidate' })
    $mechanisms = @(
        [pscustomobject]@{ mechanism = 'W32Time_external_NTP'; classification = 'context_specific'; protocol_documented = $true
            platform_supported = $Platform.is_windows; client_installed = $Platform.w32tm_present; service_reachable = $Ntp.status -ceq 'context_specific'
            transient_stripchart_reference_host = $Ntp.reference_host; configured_source_is_a_separate_status_observation = $true
            authentication_attempted = $false; authentication_verified = $false; time_measurement_obtained = $Ntp.status -ceq 'context_specific'
            timing_uncertainty_recorded = $Ntp.status -ceq 'context_specific'; related_to_actual_MT5_capture = $false; historical_applicability_established = $false
            limitation = 'ordinary_NTP_query_does_not_authenticate_the_reference' },
        [pscustomobject]@{ mechanism = 'Windows_AD_DS_time_hierarchy'; classification = 'unresolved'; protocol_documented = $true
            platform_supported = $Platform.is_windows; client_installed = $Platform.w32tm_present; service_reachable = $null
            authentication_attempted = $false; authentication_verified = $false; time_measurement_obtained = $false
            timing_uncertainty_recorded = $false; related_to_actual_MT5_capture = $false; historical_applicability_established = $false
            domain_membership = $Platform.part_of_domain; outcome = $(if ($Platform.part_of_domain -ceq $false) { 'not_applicable' } else { 'human_validation_required' })
            limitation = 'membership_and_synchronization_alone_do_not_demonstrate_authenticated_packets_or_upstream_UTC_authority' },
        [pscustomobject]@{ mechanism = 'RFC8915_NTS_Cloudflare'; classification = 'candidate'; protocol_documented = $true
            platform_supported = $null; client_installed = $(if ($found.Count -gt 0) { $true } else { $null }); service_reachable = $(if (-not $Tcp.connected) { $false } else { $null })
            tcp4460_reachable = $Tcp.connected; nts_negotiated_NTP_endpoint_reachable = $null
            authentication_attempted = $false; authentication_verified = $false; time_measurement_obtained = $false
            timing_uncertainty_recorded = $false; related_to_actual_MT5_capture = $false; historical_applicability_established = $false
            limitation = 'TCP_reachability_is_only_a_prerequisite_for_TLS_1_3_NTS_KE_and_authenticated_NTP_packets' },
        [pscustomobject]@{ mechanism = 'established_NTS_clients'; classification = 'human_validation_required'; protocol_documented = $true
            platform_supported = $null; client_installed = $(if ($found.Count -gt 0) { $true } else { $null }); service_reachable = $null
            authentication_attempted = $false; authentication_verified = $false; time_measurement_obtained = $false
            timing_uncertainty_recorded = $false; related_to_actual_MT5_capture = $false; historical_applicability_established = $false
            discovery = $(if ($found.Count -gt 0) { 'candidate_executable_detected_identity_and_safe_mode_unverified' } else { 'not_found_in_inspected_locations' })
            limitation = 'chrony_and_NTPsec_do_not_document_native_Windows_support_ntpd_rs_native_Windows_support_unverified' },
        [pscustomobject]@{ mechanism = 'HTTPS_Date_comparison'; classification = 'context_specific'; protocol_documented = $true
            platform_supported = $Platform.is_windows; client_installed = $true; service_reachable = $Https.outcome -ceq 'passed'
            authentication_attempted = $Https.request_attempted; authentication_verified = $Https.default_os_tls_validation_succeeded
            authentication_scope = 'TLS_transport_only'; authenticated_time_synchronization = $false
            transport_identity_verified_by_OS_policy = $Https.default_os_tls_validation_succeeded
            time_measurement_obtained = $null -ne $Https.date_header; timing_uncertainty_recorded = $true
            related_to_actual_MT5_capture = $false; historical_applicability_established = $false
            limitation = 'HTTPS_transport_identity_does_not_authenticate_clock_accuracy_or_HTTP_Date_generation_caching_and_delay' },
        [pscustomobject]@{ mechanism = 'existing_MoneyWay_host_measurements'; classification = 'context_specific'; protocol_documented = $true
            platform_supported = $Platform.is_windows; client_installed = $true; service_reachable = $null
            authentication_attempted = $false; authentication_verified = $false; time_measurement_obtained = $PriorCapture.pinned_files_match
            timing_uncertainty_recorded = $PriorCapture.pinned_files_match; related_to_actual_MT5_capture = $PriorCapture.pinned_files_match
            historical_applicability_established = $false; limitation = 'retained_capture_has_unsigned_NTP_and_unverified_host_clock_no_retroactive_authentication' }
    )
    [pscustomobject]@{ mechanisms = $mechanisms; windows_synchronization = $WindowsState.synchronization
        recommendation = 'REQUIRE_ENVIRONMENT_PREREQUISITE'; feasible_candidate_now = $null
        candidate_after_prerequisite = 'established_chrony_NTS_on_an_explicitly_approved_supported_environment'
        next_action = 'approve_a_supported_client_environment_then_verify_a_bounded_no_clock_adjustment_mode_and_retained_per_packet_authentication_output'
        authenticated_utc_reference_observed = $false; authentication_attempted = $false
        canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
        historical_mapping = 'unresolved'; provider_DST = 'unresolved'; source_event_UTC_bounded = $false
        strategy_timezone = 'America/Bogota' }
}

function Get-Mt5UtcClientMetadata([string]$Path) {
    $version = $null; $hash = $null; $status = 'metadata_unavailable'
    try {
        $value = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion
        if ($value -match '^\d[\w.+-]{0,63}$') { $version = $value }
        $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        $status = 'read_only_PE_metadata_binary_not_executed'
    } catch { }
    [pscustomobject]@{ file_version = $version; sha256 = $hash; status = $status }
}

function Get-Mt5RunningWslEnvironments([int]$TimeoutMilliseconds) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $env:SystemRoot 'System32\wsl.exe'
    $start.Arguments = '--list --running --quiet'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $code = $null; $count = $null; $timedOut = $false; $outcome = 'data_unavailable'
    try {
        $process = [Diagnostics.Process]::Start($start)
        try {
            # Closing stdin prevents interaction with any first-run installation/update prompt.
            $process.StandardInput.Close()
            $output = $process.StandardOutput.ReadToEndAsync(); $errors = $process.StandardError.ReadToEndAsync()
            $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
            if ($timedOut) { $process.Kill(); $process.WaitForExit() }
            $code = $process.ExitCode
            $raw = $output.GetAwaiter().GetResult(); $discarded = $errors.GetAwaiter().GetResult()
            if (-not $timedOut -and $code -eq 0 -and $raw.Length -le 65536 -and $discarded.Length -eq 0) {
                $count = @($raw.Replace([string][char]0, '') -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
                $outcome = 'passed'
            }
        } finally { $process.Dispose() }
    } catch { }
    [pscustomobject]@{ command = 'System32/wsl.exe --list --running --quiet'; outcome = $outcome
        exit_code = $code; timed_out = $timedOut; running_distribution_count = $count
        distribution_identifiers_withheld = $true; distribution_started = $false; client_inside_distribution_inspected = $false }
}

function Get-Mt5UtcHostInventory([int]$TimeoutMilliseconds) {
    $errors = @()
    $os = $null; $domain = $null; $service = $null
    $seconds = [int][math]::Ceiling($TimeoutMilliseconds / 1000)
    try {
        $value = Get-CimInstance Win32_OperatingSystem -Property Caption,Version,BuildNumber,OSArchitecture -OperationTimeoutSec $seconds -ErrorAction Stop
        $os = [pscustomobject]@{ caption = $value.Caption; version = $value.Version; build = $value.BuildNumber; architecture = $value.OSArchitecture }
    } catch { $errors += [pscustomobject]@{ check = 'OS_CIM_query'; outcome = 'data_unavailable'; hresult = $_.Exception.HResult } }
    try {
        $value = Get-CimInstance Win32_ComputerSystem -Property PartOfDomain -OperationTimeoutSec $seconds -ErrorAction Stop
        $domain = [bool]$value.PartOfDomain
    } catch { $errors += [pscustomobject]@{ check = 'domain_membership_CIM_query'; outcome = 'data_unavailable'; hresult = $_.Exception.HResult } }
    try {
        $value = Get-Service -Name W32Time -ErrorAction Stop
        $service = [pscustomobject]@{ status = $value.Status.ToString(); start_type = $value.StartType.ToString() }
    } catch { $errors += [pscustomobject]@{ check = 'W32Time_service_query'; outcome = 'data_unavailable'; hresult = $_.Exception.HResult } }
    $admin = $null
    try {
        $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
        $admin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { $errors += [pscustomobject]@{ check = 'privilege_query'; outcome = 'data_unavailable'; hresult = $_.Exception.HResult } }
    $clients = @()
    foreach ($name in @('chronyd','chronyc','ntpd','ntpq','ntp-daemon','ntp-ctl','ntpd-rs','wsl')) {
        $commands = @(Get-Command $name -CommandType Application -ErrorAction SilentlyContinue)
        $metadata = [pscustomobject]@{ file_version = $null; sha256 = $null; status = 'not_attempted' }
        if ($commands.Count -gt 0) {
            $metadata = Get-Mt5UtcClientMetadata $commands[0].Source
        }
        $clients += [pscustomobject]@{ name = $name; role = $(if ($name -eq 'wsl') { 'environment_boundary_only' } else { 'NTS_candidate' })
            discovery_scope = 'PATH'; found = $commands.Count -gt 0; file_version = $metadata.file_version; sha256 = $metadata.sha256; metadata = $metadata.status
            native_windows_NTS_support = $(if ($name -in @('chronyd','chronyc','ntpd','ntpq')) { 'not_established_by_this_executable_name' } else { 'unverified' })
            safe_standalone_mode_verified = $false }
    }
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if ([string]::IsNullOrWhiteSpace($base)) { continue }
        foreach ($name in @('ntpd.exe','ntpq.exe')) {
            $path = Join-Path $base ('NTP\bin\' + $name)
            $found = Test-Path -LiteralPath $path -PathType Leaf
            $metadata = [pscustomobject]@{ file_version = $null; sha256 = $null; status = 'not_attempted' }
            if ($found) { $metadata = Get-Mt5UtcClientMetadata $path }
            $clients += [pscustomobject]@{ name = $name; role = 'NTS_candidate'; discovery_scope = 'known_ProgramFiles_NTP_bin_location'
                found = $found; file_version = $metadata.file_version; sha256 = $metadata.sha256; metadata = $metadata.status
                native_windows_NTS_support = 'unverified'; safe_standalone_mode_verified = $false }
        }
    }
    [pscustomobject]@{ platform = [pscustomobject]@{ is_windows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
            OS = $os; powershell_version = $PSVersionTable.PSVersion.ToString(); powershell_edition = $PSVersionTable.PSEdition
            process_is_64_bit = [Environment]::Is64BitProcess; part_of_domain = $domain; service = $service
            w32tm_present = Test-Path -LiteralPath (Join-Path $env:SystemRoot 'System32\w32tm.exe')
            administrative_token = $admin; service_change_privilege_tested = $false }
        clients = $clients; errors = $errors; running_WSL_environments = Get-Mt5RunningWslEnvironments $TimeoutMilliseconds
        discovery_limit = 'PATH_and_named_ProgramFiles_locations_only_running_WSL_count_queried_distribution_contents_uninspected' }
}
