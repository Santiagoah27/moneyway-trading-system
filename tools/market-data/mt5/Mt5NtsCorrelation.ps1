Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1')

function Get-Mt5NtsHostObservation {
    [pscustomobject]@{ utc_candidate = [datetimeoffset]::UtcNow.ToString('o')
        stopwatch_ticks = [Diagnostics.Stopwatch]::GetTimestamp(); stopwatch_frequency = [Diagnostics.Stopwatch]::Frequency
        monotonic_domain = 'windows_stopwatch'; realtime_domain = 'unix_utc_candidate' }
}

function Wait-Mt5NtsTask($Task, [long]$Deadline, [Threading.CancellationToken]$CancellationToken) {
    while (-not $Task.IsCompleted) {
        if ($CancellationToken.IsCancellationRequested) { throw 'nts_cancelled' }
        if ([Diagnostics.Stopwatch]::GetTimestamp() -ge $Deadline) { throw 'nts_timeout' }
        [Threading.Thread]::Sleep(10)
    }
    if ($CancellationToken.IsCancellationRequested) { throw 'nts_cancelled' }
    $Task.GetAwaiter().GetResult()
}

function Invoke-Mt5NtsWslCommand([string]$Arguments, [string]$InputText = '', [int]$TimeoutMilliseconds = 10000) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $env:SystemRoot 'System32/wsl.exe'
    $info.Arguments = $Arguments; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($info)
    try {
        $out = $process.StandardOutput.ReadToEndAsync(); $err = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputText); $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutMilliseconds)) { $process.Kill(); $process.WaitForExit(); throw 'wsl_control_timeout' }
        $text = $out.GetAwaiter().GetResult(); $errorText = $err.GetAwaiter().GetResult()
        if ($text.Length -gt 65536 -or $errorText.Length -gt 65536) { throw 'wsl_control_output_bound' }
        [pscustomobject]@{ exit_code = $process.ExitCode; text = $text.Replace([string][char]0, ''); stderr_present = $errorText.Length -gt 0 }
    } finally { $process.Dispose() }
}

function Test-Mt5NtsDistribution([string]$Inventory, [string]$Distribution) {
    $Distribution -ceq 'MoneyWay-NTS-Ubuntu-24.04' -and
        @($Inventory -split '\r?\n' | Where-Object { $_.Trim() -ceq $Distribution }).Count -eq 1
}

function ConvertFrom-Mt5NtsLine([string]$Line, [string]$Kind, [string]$Session, [int]$Fields) {
    if ($Session -cnotmatch '^[0-9a-f]{64}$') { throw 'invalid_correlation_id' }
    if ($null -eq $Line -or $Line.Length -gt 1048576) { throw 'partial_or_oversized_protocol_output' }
    $parts = $Line.Split('|')
    if ($parts.Count -ne $Fields -or $parts[0] -cne $Kind -or $parts[1] -cne $Session) { throw 'session_or_protocol_mismatch' }
    return ,$parts
}

function Invoke-Mt5NtsLiveCapture {
    param([string]$Distribution, [string]$Session, [int]$QueryTimeoutSeconds, [int]$SessionTimeoutSeconds,
        [string]$LinuxScript, [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None)
    if ($Session -cnotmatch '^[0-9a-f]{64}$' -or $Distribution -cne 'MoneyWay-NTS-Ubuntu-24.04') { throw 'invalid_session_or_distribution' }
    $state = [pscustomobject]@{ session = $Session; error = $null; initialization_before = $null; initialization_after = $null; launch = $null; ready = $null; before = $null
        after = $null; completion = $null; result = $null; exit_code = $null; stderr_present = $false; cleanup = 'not_started' }
    $process = $null
    $dir = '/tmp/mw-nts-' + $Session
    try {
        if ($CancellationToken.IsCancellationRequested) { throw 'nts_cancelled' }
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($LinuxScript.Replace("`r`n", "`n")))
        $stage = "set -eu`numask 077`nmkdir $dir`nprintf %s '$encoded' | base64 -d > $dir/run.sh`nchmod 700 $dir/run.sh`n"
        $state.initialization_before = Get-Mt5NtsHostObservation
        $staged = Invoke-Mt5NtsWslCommand "--distribution $Distribution --exec /bin/sh -s" $stage
        $state.initialization_after = Get-Mt5NtsHostObservation
        if ($staged.exit_code -ne 0) { throw 'linux_staging_failed' }
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = Join-Path $env:SystemRoot 'System32/wsl.exe'
        $info.Arguments = "--distribution $Distribution --exec /usr/bin/timeout --signal=TERM --kill-after=2s ${SessionTimeoutSeconds}s /bin/sh $dir/run.sh $Session $QueryTimeoutSeconds"
        $info.UseShellExecute = $false; $info.CreateNoWindow = $true
        $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
        $state.launch = Get-Mt5NtsHostObservation
        $deadline = $state.launch.stopwatch_ticks + [long](($SessionTimeoutSeconds + 5) * [Diagnostics.Stopwatch]::Frequency)
        $process = [Diagnostics.Process]::Start($info)
        $process.StandardInput.NewLine = "`n"
        $errors = $process.StandardError.ReadToEndAsync()
        $ready = ConvertFrom-Mt5NtsLine (Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken) READY $Session 3
        $state.ready = Get-Mt5NtsHostObservation
        foreach ($phase in @('BEFORE','AFTER')) {
            if ($phase -ceq 'AFTER') {
                $done = ConvertFrom-Mt5NtsLine (Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken) QUERYDONE $Session 3
            }
            $sent = Get-Mt5NtsHostObservation
            $process.StandardInput.WriteLine("$phase $Session"); $process.StandardInput.Flush()
            $line = Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken
            $received = Get-Mt5NtsHostObservation
            $observation = ConvertFrom-Mt5NtsLine $line OBS $Session 6
            if ($observation[2] -cne $phase) { throw 'observation_phase_mismatch' }
            $exchange = [pscustomobject]@{ sent = $sent; received = $received
                linux = [pscustomobject]@{ monotonic_before = $observation[3]; utc_unix_seconds = $observation[4]; monotonic_after = $observation[5]
                    monotonic_domain = 'linux_proc_uptime_boottime'; realtime_domain = 'unix_utc_candidate' } }
            if ($phase -ceq 'BEFORE') {
                $state.before = $exchange
                $process.StandardInput.WriteLine("RUN $Session"); $process.StandardInput.Flush()
            } else { $state.after = $exchange }
        }
        $resultLine = Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken
        $selection = $null
        if ($null -ne $resultLine -and $resultLine.StartsWith('SELECTION|', [StringComparison]::Ordinal)) {
            $parts = ConvertFrom-Mt5NtsLine $resultLine SELECTION $Session 11
            $selection = [pscustomobject]@{ session = $parts[1]; availability = $parts[2]; sha256 = $parts[3]; raw_base64 = $parts[4]
                start_boottime = $parts[5]; end_boottime = $parts[6]; start_realtime_candidate = $parts[7]; end_realtime_candidate = $parts[8]
                command = (New-Object Text.UTF8Encoding($false, $true)).GetString([Convert]::FromBase64String($parts[9]))
                os_release = (New-Object Text.UTF8Encoding($false, $true)).GetString([Convert]::FromBase64String($parts[10])) }
            $resultLine = Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken
        }
        $result = ConvertFrom-Mt5NtsLine $resultLine RESULT $Session 10
        if ($ready[2] -cne $result[3] -or $done[2] -cne $result[2]) { throw 'query_binding_mismatch' }
        $decoded = @()
        foreach ($part in $result[4..9]) { $decoded += (New-Object Text.UTF8Encoding($false, $true)).GetString([Convert]::FromBase64String($part)) }
        $state.result = [pscustomobject]@{ session = $result[1]; query_exit_code = [int]$result[2]; configuration_sha256 = $result[3]
            configuration = $decoded[0]; authdata = $decoded[1]; ntpdata = $decoded[2]; capabilities = $decoded[3]; client_stderr = $decoded[4]; client_stdout = $decoded[5] }
        if ($null -ne $selection) { $state.result | Add-Member selection $selection }
        $process.StandardInput.WriteLine("ACK $Session"); $process.StandardInput.Close()
        $null = ConvertFrom-Mt5NtsLine (Wait-Mt5NtsTask ($process.StandardOutput.ReadLineAsync()) $deadline $CancellationToken) DONE $Session 2
        while (-not $process.WaitForExit(10)) {
            if ($CancellationToken.IsCancellationRequested) { throw 'nts_cancelled' }
            if ([Diagnostics.Stopwatch]::GetTimestamp() -ge $deadline) { throw 'nts_timeout' }
        }
        $state.exit_code = $process.ExitCode
        $state.stderr_present = (Wait-Mt5NtsTask $errors $deadline $CancellationToken).Length -gt 0
        if ($process.ExitCode -ne 0) { throw 'linux_process_failed' }
        $state.cleanup = 'private_session_removed_by_linux_trap'
    } catch {
        $state.error = $_.Exception.Message
    } finally {
        if ($null -ne $process) {
            $process.StandardInput.Close()
            if (-not $process.HasExited) {
                # Notify only this session's loop; closing stdin also releases its post-query read.
                try { $null = Invoke-Mt5NtsWslCommand "--distribution $Distribution --exec /usr/bin/touch $dir/cancel" '' 5000 } catch { }
                if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
            }
            $process.Dispose()
        }
        if ($state.cleanup -ne 'private_session_removed_by_linux_trap') {
            # Inner GNU timeout bounds remaining children even if the host transport dies.
            $state.cleanup = 'inner_timeout_and_trap_bound_cleanup_requires_reinspection'
            try {
                $absence = Invoke-Mt5NtsWslCommand "--distribution $Distribution --exec /usr/bin/test ! -e $dir" '' 5000
                if ($absence.exit_code -eq 0) { $state.cleanup = 'private_session_absence_verified_after_failure' }
            } catch { }
        }
        $state.completion = Get-Mt5NtsHostObservation
    }
    $state
}

function Get-Mt5NtsDecimal([string]$Value) {
    if ($Value -cnotmatch '^[+-]?[0-9]+(?:\.[0-9]+)?$') { throw 'invalid_clock_or_measurement_number' }
    [decimal]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Get-Mt5NtsUnixCandidate([string]$Value) {
    $stamp = [datetimeoffset]::ParseExact($Value, 'o', [Globalization.CultureInfo]::InvariantCulture)
    if ($stamp.Offset -ne [timespan]::Zero) { throw 'invalid_host_utc_candidate' }
    ([decimal]$stamp.UtcTicks - [decimal]621355968000000000) / [decimal]10000000
}

function Get-Mt5NtsCorrelationAssessment($Capture, [string]$Session) {
    $reasons = New-Object 'Collections.Generic.List[string]'
    $auth = $false; $ke = $false; $accepted = $null; $offset = $null; $metrics = [ordered]@{}
    $exchanges = @(); $timing = $null
    try {
        if ($Session -cnotmatch '^[0-9a-f]{64}$' -or $Capture.session -cne $Session) { throw 'session_mismatch' }
        if ($null -ne $Capture.error) { throw $Capture.error }
        $r = $Capture.result
        if ($null -eq $r -or $r.session -cne $Session -or $Capture.exit_code -ne 0 -or $r.query_exit_code -ne 0) { throw 'incomplete_or_failed_query' }
        $dir = '/tmp/mw-nts-' + $Session
        $expected = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`n"
        $withSelection = $expected + "log selection`nlogdir $dir`n"
        if (($r.configuration -cne $expected -and $r.configuration -cne $withSelection) -or
            $r.configuration_sha256 -cne (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($r.configuration)))) { throw 'configuration_or_source_binding_mismatch' }
        if ($r.client_stderr -notmatch 'chronyd version 4\.5 starting \([^\r\n]*\+NTS' -or $r.client_stderr -notmatch 'Disabled control of system clock') { throw 'client_profile_unverified' }
        if ($r.capabilities -notmatch '(?m)^Uid:\s+100\s+100\s+100\s+100\s*$' -or
            $r.capabilities -notmatch '(?m)^CapEff:\s+0000000000000000\s*$' -or $r.capabilities -notmatch '(?m)^CapBnd:\s+0000000000000000\s*$' -or
            $r.capabilities -notmatch '(?m)^NoNewPrivs:\s+1\s*$') { throw 'clock_privileges_unverified' }
        $remote = [regex]::Match($r.ntpdata, '(?m)^Remote address\s+: ([0-9a-fA-F:.]+)(?:\s|$)')
        if (-not $remote.Success) { throw 'missing_remote_endpoint' }
        $address = [regex]::Escape($remote.Groups[1].Value)
        $row = [regex]::Match($r.authdata, "(?m)^$address\s+NTS\s+(?<key>[0-9]+)\s+(?<type>[0-9]+)\s+(?<bits>[0-9]+)\s+(?<last>[0-9]+[smhd]?)\s+[0-9]+\s+[0-9]+\s+(?<cookies>[0-9]+)\s+[0-9]+\s*$")
        $ke = $row.Success -and [long]$row.Groups['key'].Value -gt 0 -and [long]$row.Groups['cookies'].Value -gt 0
        if (-not $ke) { throw 'nts_ke_unverified' }
        $good = [regex]::Match($r.ntpdata, '(?m)^Total good RX\s+: ([0-9]+)\s*$')
        if (-not $good.Success -or [long]$good.Groups[1].Value -eq 0 -or $r.ntpdata -notmatch '(?m)^Authenticated\s+: Yes\s*$') { throw 'accepted_authenticated_packets_unverified' }
        $accepted = [long]$good.Groups[1].Value
        $printed = [regex]::Match($r.client_stderr, 'System clock wrong by ([+-]?[0-9]+\.[0-9]+) seconds \(ignored\)')
        if (-not $printed.Success) { throw 'missing_final_offset' }
        $offset = $printed.Groups[1].Value
        foreach ($label in @('Offset','Peer delay','Peer dispersion','Root delay','Root dispersion')) {
            $match = [regex]::Match($r.ntpdata, "(?m)^$label\s+: ([+-]?[0-9]+\.[0-9]+) seconds\s*$")
            $metrics[$label] = if ($match.Success) { $match.Groups[1].Value } else { $null }
        }
        $auth = $true
    } catch { $reasons.Add($_.Exception.Message) }
    try {
        if ($null -eq $Capture.PSObject.Properties['launch'] -or $null -eq $Capture.launch -or
            $null -eq $Capture.before -or $null -eq $Capture.after -or $null -eq $Capture.completion) { throw 'timing_observations_incomplete' }
        $frequency = [decimal]$Capture.launch.stopwatch_frequency
        $previous = [long]$Capture.initialization_before.stopwatch_ticks
        $previousUtc = [decimal]-1
        foreach ($hostStamp in @($Capture.initialization_before,$Capture.initialization_after,$Capture.launch,$Capture.ready,$Capture.before.sent,$Capture.before.received,$Capture.after.sent,$Capture.after.received,$Capture.completion)) {
            if ($hostStamp.monotonic_domain -cne 'windows_stopwatch' -or $hostStamp.realtime_domain -cne 'unix_utc_candidate' -or
                [decimal]$hostStamp.stopwatch_frequency -ne $frequency -or $frequency -le 0 -or [long]$hostStamp.stopwatch_ticks -lt $previous) { throw 'host_monotonic_domain_or_order_mismatch' }
            $currentUtc = Get-Mt5NtsUnixCandidate $hostStamp.utc_candidate
            if ($currentUtc -lt $previousUtc) { throw 'windows_realtime_discontinuity' }
            $previousUtc = $currentUtc
            $previous = [long]$hostStamp.stopwatch_ticks
        }
        $lastLinux = [decimal]-1
        foreach ($exchange in @($Capture.before,$Capture.after)) {
            $linux = $exchange.linux
            if ($linux.monotonic_domain -cne 'linux_proc_uptime_boottime' -or $linux.realtime_domain -cne 'unix_utc_candidate') { throw 'linux_clock_domain_mismatch' }
            if ($linux.monotonic_before -cnotmatch '^[0-9]+\.[0-9]{2}$' -or $linux.monotonic_after -cnotmatch '^[0-9]+\.[0-9]{2}$') { throw 'unsupported_linux_monotonic_resolution' }
            $before = Get-Mt5NtsDecimal $linux.monotonic_before; $after = Get-Mt5NtsDecimal $linux.monotonic_after
            $real = Get-Mt5NtsDecimal $linux.utc_unix_seconds
            if ($before -lt 0 -or $real -le 0 -or $before -lt $lastLinux -or $after -lt $before) { throw 'linux_monotonic_order_mismatch' }
            $lastLinux = $after
            $sent = Get-Mt5NtsUnixCandidate $exchange.sent.utc_candidate; $received = Get-Mt5NtsUnixCandidate $exchange.received.utc_candidate
            if ($received -lt $sent) { throw 'windows_realtime_discontinuity' }
            $elapsed = ([decimal]$exchange.received.stopwatch_ticks - [decimal]$exchange.sent.stopwatch_ticks) / $frequency
            $exchanges += [pscustomobject]@{ host_elapsed_seconds = $elapsed; host_realtime_elapsed_seconds = $received - $sent
                realtime_minus_monotonic_elapsed_seconds = $received - $sent - $elapsed
                linux_minus_windows_candidate_interval_seconds = @(($real - $received), ($real - $sent))
                interval_scope = 'candidate_realtime_read_bracket_includes_transport_and_scheduling_not_certified_clock_error' }
        }
        $linuxElapsed = (Get-Mt5NtsDecimal $Capture.after.linux.utc_unix_seconds) - (Get-Mt5NtsDecimal $Capture.before.linux.utc_unix_seconds)
        $hostElapsed = (Get-Mt5NtsUnixCandidate $Capture.after.received.utc_candidate) - (Get-Mt5NtsUnixCandidate $Capture.before.sent.utc_candidate)
        if ($linuxElapsed -lt 0 -or $hostElapsed -lt 0) { throw 'realtime_discontinuity' }
        $timing = [pscustomobject]@{ status = 'unresolved'; launch_to_ready_seconds = ([decimal]$Capture.ready.stopwatch_ticks - [decimal]$Capture.launch.stopwatch_ticks) / $frequency
            initialization_and_staging_seconds = ([decimal]$Capture.initialization_after.stopwatch_ticks - [decimal]$Capture.initialization_before.stopwatch_ticks) / $frequency
            host_execution_seconds = ([decimal]$Capture.completion.stopwatch_ticks - [decimal]$Capture.launch.stopwatch_ticks) / $frequency
            linux_realtime_elapsed_seconds = $linuxElapsed
            linux_boottime_elapsed_bracket_seconds = @(((Get-Mt5NtsDecimal $Capture.after.linux.monotonic_before) - (Get-Mt5NtsDecimal $Capture.before.linux.monotonic_after) - [decimal]'0.01'),
                ((Get-Mt5NtsDecimal $Capture.after.linux.monotonic_after) - (Get-Mt5NtsDecimal $Capture.before.linux.monotonic_before) + [decimal]'0.01'))
            linux_boottime_report_resolution_seconds = '0.01'; exchanges = $exchanges }
    } catch { $reasons.Add($_.Exception.Message) }
    [pscustomobject]@{ status = $(if ($auth) { 'unresolved' } else { 'data_unavailable' }); nts_ke_observed = $ke
        nts_authenticated_measurement_observed = $auth; accepted_authenticated_packets = $accepted
        chrony_final_offset_seconds = $offset; packet_metrics_seconds = [pscustomobject]$metrics; timing = $timing
        overall_utc_uncertainty = 'unresolved'; total_utc_error_bound_seconds = $null; windows_clock_authenticated = $false
        mt5_native_utc_authenticated = $false; canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
        diagnostics = @($reasons); limitations = @('Windows_and_Linux_monotonic_epochs_and_rates_are_separate',
            'HyperV_implicit_sync_means_clocks_are_not_independent','candidate_read_brackets_do_not_bound_network_asymmetry_or_clock_error',
            'clock_rate_read_sequencing_and_subsample_discontinuities_unresolved','Linux_proc_uptime_includes_suspend_and_is_quantized',
            'provider_DST_mapping_closure_availability_and_MT5_event_correlation_unresolved') }
}
