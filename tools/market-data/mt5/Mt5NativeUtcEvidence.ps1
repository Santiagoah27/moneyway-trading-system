# Pure assessment helpers for the diagnostic workflow. Source timestamps remain native coordinates.
Set-StrictMode -Version Latest

function Get-Mt5PolicyEvidence {
    param([string]$Url, [string]$Html, [string]$RetrievedUtcCandidate, [string]$Sha256)
    $mt5 = 'https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm'
    $mobile = 'https://help.oanda.com/bvi/es/faqs/oanda-mobile-user-guide-bvi.htm'
    if ($Url -cne $mt5 -and $Url -cne $mobile) { throw 'unsupported_policy_source' }
    $plain = [System.Net.WebUtility]::HtmlDecode([regex]::Replace($Html, '<[^>]+>', ' '))
    $pair = $plain -match 'GMT\s*\+\s*2' -and $plain -match 'GMT\s*\+\s*3'
    $demoScoped = $Url -ceq $mt5 -and $plain -cmatch 'OANDA_Global-Demo-1'
    [pscustomobject][ordered]@{
        url = $Url; retrieved_at_host_utc_candidate = $RetrievedUtcCandidate; input_sha256 = $Sha256
        classification = $(if ($pair) { 'confirmed' } else { 'unresolved' })
        published_offset_pair_present = $pair; mt5_demo_scope_identified = $demoScoped
        product_scope = $(if ($Url -ceq $mt5) { 'OANDA_Global_Markets_MT5_guide' } else { 'OANDA_mobile_application_live_account_scope' })
        literal_est_wording_present = $Url -ceq $mt5 -and $plain -match '\bEST\b'
        standard_seconds = $(if ($pair) { 7200 } else { $null })
        daylight_seconds = $(if ($pair) { 10800 } else { $null })
        historical_effective_date = $null; transition_authority = 'unresolved'
    }
}

function Get-Mt5NativeCoordinate {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value.raw -isnot [long] -and $Value.raw -isnot [int]) { throw 'invalid_native_timestamp' }
    $native = [datetime]::ParseExact($Value.display, 'yyyy.MM.dd HH:mm:ss', [cultureinfo]::InvariantCulture)
    $epoch = [datetime]::SpecifyKind([datetime]'1970-01-01', [DateTimeKind]::Unspecified)
    if ($Value.raw -le 0 -or ($native - $epoch).Ticks / [timespan]::TicksPerSecond -ne $Value.raw) { throw 'invalid_native_timestamp' }
    [pscustomobject]@{ raw = [long]$Value.raw; display = [string]$Value.display }
}

function Get-Mt5StripchartEvidence {
    param([string]$Raw, [int]$ExitCode, [bool]$TimedOut, [string]$ReferenceHost)
    $samples = @()
    foreach ($line in ($Raw -split '\r?\n')) {
        if ($line -match '^\s*(\d+),\s*(\d+),\s*(\d+),\s*([+-]?\d+(?:\.\d+)?),\s*([+-]?\d+(?:\.\d+)?)\s*$') {
            try {
                $start = [uint64]::Parse($Matches[1], [cultureinfo]::InvariantCulture)
                $end = [uint64]::Parse($Matches[2], [cultureinfo]::InvariantCulture)
                $fileTime = [long]::Parse($Matches[3], [cultureinfo]::InvariantCulture)
                $delay = [decimal]::Parse($Matches[4], [cultureinfo]::InvariantCulture)
                $offset = [decimal]::Parse($Matches[5], [cultureinfo]::InvariantCulture)
                if ($end -lt $start -or $delay -lt 0) { continue }
                $requestTime = [datetime]::FromFileTimeUtc($fileTime).ToString('o')
                $samples += [pscustomobject]@{ rdtsc_start = $start; rdtsc_end = $end; local_request_filetime = $fileTime
                    local_request_utc_candidate = $requestTime; reported_roundtrip_seconds = $delay; reported_ntp_offset_seconds = $offset }
            } catch { continue }
        }
    }
    [pscustomobject]@{
        reference_host = $ReferenceHost; method = 'w32tm_stripchart_rdtsc'; samples = @($samples)
        status = $(if (-not $TimedOut -and $ExitCode -eq 0 -and $samples.Count -gt 0) { 'context_specific' } else { 'data_unavailable' })
        reference_authentication = 'unverified'; offset_sign_application = 'not_applied'
        uncertainty = 'reported_roundtrip_and_offsets_retained_no_skew_threshold_or_absolute_error_guarantee'
    }
}

function Get-Mt5NativeUtcAssessment {
    param($Probe, $HostEvidence, [object[]]$Policies)
    if ($Probe.source.company -cne 'OANDA Global Markets Limited' -or $Probe.source.server -cne 'OANDA_Global-Demo-1' -or
        $Probe.source.demo_verified -cne $true -or $Probe.symbol.native -cne 'US100') { throw 'source_identity_mismatch' }
    if ($Probe.canonical_ready -cne $false -or $Probe.utc_conversion_authenticated -cne $false) { throw 'unapproved_temporal_authorization' }
    $before = Get-Mt5NativeCoordinate $Probe.server_quote_before
    $after = Get-Mt5NativeCoordinate $Probe.server_quote_after
    $gmt = Get-Mt5NativeCoordinate $Probe.generated_gmt_candidate
    $m1 = @($Probe.timeframes | Where-Object timeframe -CEQ 'M1')
    if ($m1.Count -ne 1) { throw 'missing_m1_frame' }
    $head = if ($m1[0].bars.Count -gt 0) { Get-Mt5NativeCoordinate $m1[0].bars[0].open_time_native } else { $null }
    $differenceBefore = if ($null -ne $before -and $null -ne $gmt) { $before.raw - $gmt.raw } else { $null }
    $differenceAfter = if ($null -ne $after -and $null -ne $gmt) { $after.raw - $gmt.raw } else { $null }
    $policyPairConfirmed = @($Policies | Where-Object { $_.PSObject.Properties.Name -contains 'published_offset_pair_present' -and
        $_.published_offset_pair_present -ceq $true -and $_.mt5_demo_scope_identified -ceq $true }).Count -gt 0
    $matchesPolicy = @(@(7200, 10800) | Where-Object { $policyPairConfirmed -and $null -ne $differenceBefore -and $differenceBefore -eq $_ -and $differenceAfter -eq $_ })
    $hostPresent = $null -ne $HostEvidence.utc_before_candidate -and $null -ne $HostEvidence.utc_after_candidate
    $captureOrdered = $hostPresent -and $HostEvidence.monotonic_finished -ge $HostEvidence.monotonic_started -and
        [datetimeoffset]::Parse($HostEvidence.utc_after_candidate) -ge [datetimeoffset]::Parse($HostEvidence.utc_before_candidate)
    [pscustomobject][ordered]@{
        classification = $(if ($matchesPolicy.Count -gt 0) { 'candidate' } else { 'unresolved' })
        source_identity = [pscustomobject]@{ company = $Probe.source.company; server = $Probe.source.server; demo = $true; native_symbol = 'US100' }
        broker_native = [pscustomobject]@{
            quote_source = 'TimeCurrent_last_known_Market_Watch_quote_any_selected_symbol'; quote_before = $before; quote_after = $after
            us100_m1_head = $head; computer_gmt_candidate = $gmt; gate = $Probe.gate
            timeframe_acquisitions = @($Probe.timeframes | Select-Object timeframe,status,requested_count,copy_return,copy_error_code,
                @{ Name = 'bar_count'; Expression = { $_.bars.Count } })
            connected_before = $Probe.source.connected_before; connected_after = $Probe.connected_after
            quote_changed_during_probe = $null -ne $before -and $null -ne $after -and $before.raw -ne $after.raw
            quote_freshness = 'unverified'; tick_timestamp = $null; tick_symbol_binding = 'unresolved'
        }
        host_utc_reading_present = $hostPresent; host_capture_order_valid = $captureOrdered
        host_synchronization = 'unverified'; independent_reference = $HostEvidence.reference
        observed_native_minus_computer_gmt_seconds_before = $differenceBefore
        observed_native_minus_computer_gmt_seconds_after = $differenceAfter
        exact_published_offset_candidates_matched = @($matchesPolicy)
        provider_policy = @($Policies)
        policy_authority = 'published_GMT_plus_2_plus_3_pair_candidate_selection_rule_unresolved'
        broker_to_independent_utc_relationship = 'insufficient_existing_probe_capture_not_bracketed_by_host_measurement'
        competing_hour_hypotheses_independently_distinguishable = $false
        coverage = [pscustomobject]@{
            standard_offset_observation = $(if ($matchesPolicy -contains 7200) { 'context_specific_computer_derived_difference' } else { 'unresolved' })
            daylight_offset_observation = $(if ($matchesPolicy -contains 10800) { 'context_specific_computer_derived_difference' } else { 'unresolved' })
            provider_dst_transitions = 'unresolved'; historical_applicability = 'unresolved'; ambiguous_or_nonexistent_times = 'unresolved'
        }
        unresolved = @('quote_age_and_US100_quote_identity', 'independent_UTC_to_broker_capture_pairing', 'reference_authentication',
            'provider_DST_authority_and_transition_instants', 'historical_policy_effective_periods', 'ambiguous_and_nonexistent_native_times',
            'provider_candle_closure', 'historical_availability', 'non_atomic_timeframes')
        strategy_timezone = 'America/Bogota'; canonical_utc_conversion_authorized = $false
        utc_conversion_authenticated = $false; canonical_ready = $false; diagnostic_only = $true
    }
}

function Invoke-Mt5BoundedTimeQuery {
    param([string]$Arguments, [int]$TimeoutMilliseconds)
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = Join-Path $env:SystemRoot 'System32\w32tm.exe'
    $startInfo.Arguments = $Arguments; $startInfo.UseShellExecute = $false; $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardOutput = $true; $startInfo.RedirectStandardError = $true
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $before = [datetimeoffset]::UtcNow.ToString('o')
    try {
        $process = [System.Diagnostics.Process]::Start($startInfo)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            $timedOut = -not $process.WaitForExit($TimeoutMilliseconds)
            if ($timedOut) { $process.Kill(); $process.WaitForExit() }
            $raw = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
            if ($raw.Length -gt 65536) { $raw = 'query_output_bound_exceeded' }
            [pscustomobject]@{ arguments = $Arguments; exit_code = $process.ExitCode; timed_out = $timedOut; raw = $raw
                utc_before_candidate = $before; utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o'); elapsed_milliseconds = $clock.Elapsed.TotalMilliseconds }
        } finally { $process.Dispose() }
    } catch {
        [pscustomobject]@{ arguments = $Arguments; exit_code = -1; timed_out = $false; raw = 'w32time_query_unavailable'
            utc_before_candidate = $before; utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o'); elapsed_milliseconds = $clock.Elapsed.TotalMilliseconds }
    }
}

function Write-Mt5EvidenceBytes {
    param([string]$Path, [byte[]]$Bytes)
    $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try { $stream.Write($Bytes, 0, $Bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
