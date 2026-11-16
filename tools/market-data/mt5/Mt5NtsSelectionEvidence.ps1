Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1')

function ConvertFrom-Mt5NtsSelectionNumber([string]$Text) {
    if ($Text -cnotmatch '^[+-]?(?<whole>[0-9]+)(?:\.(?<fraction>[0-9]+))?(?:[eE](?<exponent>[+-]?[0-9]+))?$') { throw 'malformed_selection_number' }
    $fraction = [string]$Matches['fraction']
    $digits = ($Matches['whole'] + $fraction).TrimStart('0')
    $exponent = if ($Matches['exponent']) { [int]$Matches['exponent'] } else { 0 }
    $scale = $fraction.Length - $exponent
    while ($digits.EndsWith('0') -and $digits.Length -gt 0) { $digits = $digits.Substring(0,$digits.Length - 1); $scale-- }
    if ($digits.Length -gt 28 -or ($digits.Length -gt 0 -and $scale -gt 28)) { throw 'selection_number_not_exactly_representable' }
    [decimal]::Parse($Text, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture)
}

function Get-Mt5NtsSelectionAssessment($Capture, [string]$Session, [string[]]$Assumptions = @()) {
    $a = [pscustomobject][ordered]@{
        schema = 'moneyway.nts-selection-assessment'; version = 1; correlation_id = $Session
        status = 'data_unavailable'; gate = 'A_missing'; log_availability = 'not_captured_legacy_or_failed_session'
        log_sha256 = $null; log_byte_count = $null; row_count = 0; rows = @(); rows_truncated = $false
        selected_row = $null; selected_source_authenticated = $false; nts_authenticated_measurement_observed = $false
        chrony_selection_interval_status = 'unresolved'; conditional_interval = $null; assumptions = @($Assumptions)
        diagnostics = @(); overall_utc_uncertainty = 'unresolved'; windows_host_utc_error_bound_seconds = $null; mt5_event_utc_interval = $null
        windows_clock_authenticated = $false; mt5_native_utc_authenticated = $false
        canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
    }
    try {
        if ($Session -cnotmatch '^[0-9a-f]{64}$' -or $Capture.session -cne $Session) { throw 'selection_session_mismatch' }
        $auth = Get-Mt5NtsCorrelationAssessment $Capture $Session
        $a.nts_authenticated_measurement_observed = $auth.nts_authenticated_measurement_observed
        if ($null -eq $Capture.result -or $null -eq $Capture.result.PSObject.Properties['selection']) { return $a }
        $r = $Capture.result; $s = $r.selection; $dir = '/tmp/mw-nts-' + $Session
        if ($r.session -cne $Session -or $s.session -cne $Session) { throw 'selection_session_mismatch' }
        $expected = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`nlog selection`nlogdir $dir`n"
        if ($r.configuration -cne $expected -or $r.configuration_sha256 -cne (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($expected)))) { throw 'selection_configuration_or_session_path_mismatch' }
        if ($s.command -cnotmatch ('^/usr/sbin/chronyd -Q -t ([1-9]|[1-5][0-9]|60) -f ' + [regex]::Escape($dir) + '/chrony.conf$')) { throw 'selection_command_binding_mismatch' }
        foreach ($value in @($s.start_boottime,$s.end_boottime)) { if ($value -cnotmatch '^[0-9]+\.[0-9]{2}$') { throw 'selection_counter_format' } }
        if ((Get-Mt5NtsDecimal $s.start_boottime) -gt (Get-Mt5NtsDecimal $s.end_boottime) -or
            (Get-Mt5NtsDecimal $s.start_boottime) -lt (Get-Mt5NtsDecimal $Capture.before.linux.monotonic_after) -or
            (Get-Mt5NtsDecimal $s.end_boottime) -gt (Get-Mt5NtsDecimal $Capture.after.linux.monotonic_before)) { throw 'selection_query_bounds_mismatch' }
        $a.log_availability = $s.availability
        if ($s.availability -ceq 'missing') { $a.diagnostics += 'selection_file_not_created_reason_unresolved'; return $a }
        if ($s.availability -cne 'present') { throw 'selection_file_unavailable_or_oversized' }
        $bytes = [Convert]::FromBase64String($s.raw_base64)
        if ($bytes.Length -gt 65536) { throw 'selection_log_size_bound' }
        $a.log_sha256 = Get-Mt5TickHash $bytes; $a.log_byte_count = $bytes.Length
        if ($s.sha256 -cne $a.log_sha256) { throw 'selection_log_hash_mismatch' }
        $text = (New-Object Text.UTF8Encoding($false,$true)).GetString($bytes)
        if ($bytes.Length -eq 0) { $a.gate = 'B_empty'; $a.diagnostics += 'selection_file_empty'; return $a }
        $rows = New-Object 'Collections.Generic.List[object]'
        foreach ($line in ($text -split '\r?\n')) {
            if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^=+$|^\s*Date \(UTC\) Time\s+') { continue }
            $parts = $line.Trim() -split '\s+'
            if ($parts.Count -ne 10) { throw 'malformed_selection_row' }
            $null = [datetime]::ParseExact(($parts[0] + ' ' + $parts[1]), 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
            $ip = $null
            if (-not [Net.IPAddress]::TryParse($parts[2], [ref]$ip) -or $parts[3] -cnotmatch '^[NsMd~wSOTxWPUD+*]$' -or
                $parts[4] -cnotmatch '^-[N-][P-][T-][R-]$' -or $parts[5] -cnotmatch '^[0-7]+$') { throw 'invalid_selection_source_or_state' }
            $score = ConvertFrom-Mt5NtsSelectionNumber $parts[6]; $age = ConvertFrom-Mt5NtsSelectionNumber $parts[7]
            $lower = ConvertFrom-Mt5NtsSelectionNumber $parts[8]; $upper = ConvertFrom-Mt5NtsSelectionNumber $parts[9]
            if ($score -lt 0 -or $age -lt 0 -or $lower -gt $upper) { throw 'invalid_selection_interval_or_age' }
            $rows.Add([pscustomobject]@{ source_timestamp_label = $parts[0] + ' ' + $parts[1]; timestamp_clock_authentication = 'unresolved'
                source_address = $parts[2]; state = $parts[3]; effective_options = $parts[4]; reach = $parts[5]
                score_original = $parts[6]; sample_age_original = $parts[7]; lower_original = $parts[8]; upper_original = $parts[9]
                lower_seconds = $lower; upper_seconds = $upper; text_precision = 'original_literals_retained_no_rounding_expansion_or_error_bound_inferred' })
        }
        $a.row_count = $rows.Count; $a.rows = @($rows | Select-Object -First 16); $a.rows_truncated = $rows.Count -gt 16
        if ($rows.Count -eq 0) { $a.gate = 'C_header_only'; $a.diagnostics += 'selection_file_has_no_rows'; return $a }
        $selected = @($rows | Where-Object { $_.state -ceq '*' })
        if ($selected.Count -eq 0) { $a.gate = 'D_not_selected'; $a.diagnostics += 'no_selected_source'; return $a }
        $a.selected_row = $selected[-1]; $a.gate = 'E_selected_valid_interval'
        $remote = [regex]::Match($r.ntpdata, '(?m)^Remote address\s+: ([0-9a-fA-F:.]+)(?:\s|$)')
        if (-not $remote.Success -or $a.selected_row.source_address -cne $remote.Groups[1].Value -or
            @($rows | Where-Object { $_.source_address -cne $remote.Groups[1].Value }).Count -ne 0) { throw 'selection_source_mismatch' }
        if (-not $auth.nts_authenticated_measurement_observed) { $a.gate = 'F_selected_authentication_unproven'; $a.diagnostics += $auth.diagnostics; return $a }
        $a.selected_source_authenticated = $true
        foreach ($condition in @('ultimate_reference_correct','chrony_source_selection_model_applies','no_unbounded_clock_change_between_sample_and_selection')) {
            if ($Assumptions -cnotcontains $condition) { throw 'selection_conditional_assumptions_missing' }
        }
        $a.gate = 'G_authenticated_selected_interval'; $a.status = 'context_specific'
        $a.selected_source_authenticated = $true; $a.chrony_selection_interval_status = 'context_specific'
        $a.conditional_interval = [pscustomobject]@{ lower_original = $a.selected_row.lower_original; upper_original = $a.selected_row.upper_original
            lower_seconds = $a.selected_row.lower_seconds; upper_seconds = $a.selected_row.upper_seconds
            scope = 'Chrony_source_selection_local_clock_offset_estimate_at_selection_not_tracking_formula'
            assumptions_verified = $false; rigorous_UTC_bound_established = $false }
    } catch { $a.status = 'data_unavailable'; $a.diagnostics += $_.Exception.Message }
    $a
}

function Publish-Mt5NtsSelectionEvidence($Capture, [string]$Session, [string]$ReportPath, [string]$Distribution, $Assessment) {
    $a = Get-Mt5NtsSelectionAssessment $Capture $Session @('ultimate_reference_correct','chrony_source_selection_model_applies','no_unbounded_clock_change_between_sample_and_selection')
    $logPath = $ReportPath + '.selection.log'; $manifestPath = $ReportPath + '.selection.json'; $retained = $false
    foreach ($path in @($logPath,$manifestPath)) {
        if ((Test-Path -LiteralPath $path) -or (Test-Path -LiteralPath ($path + '.pending'))) { throw 'diagnostic_path_already_exists' }
    }
    $selection = if ($null -ne $Capture.result -and $null -ne $Capture.result.PSObject.Properties['selection']) { $Capture.result.selection } else { $null }
    if ($null -ne $selection -and $selection.availability -ceq 'present') {
        $bytes = [Convert]::FromBase64String($selection.raw_base64)
        if ($bytes.Length -gt 65536 -or (Get-Mt5TickHash $bytes) -cne $selection.sha256) { throw 'selection_retention_hash_mismatch' }
        $text = (New-Object Text.UTF8Encoding($false,$true)).GetString($bytes)
        Publish-Mt5TickFile $logPath $text
        if ((Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $selection.sha256) { throw 'retained_selection_hash_mismatch' }
        $retained = $true
    }
    $r = $Capture.result
    $manifest = [pscustomobject][ordered]@{ schema = 'moneyway.nts-selection-evidence'; version = 1; correlation_id = $Session
        evidence_kind = 'same_execution_selection_log_and_client_telemetry'; distribution = $Distribution
        linux_environment = $(if ($null -ne $selection) { $selection.os_release }); query_observations = $selection
        chrony_profile = 'Ubuntu_4.5-1ubuntu4.2_executable_hash_pinned'; reference_server = 'time.cloudflare.com'
        configuration_sha256 = $(if ($null -ne $r) { $r.configuration_sha256 }); query_exit_code = $(if ($null -ne $r) { $r.query_exit_code })
        authentication = [pscustomobject]@{ nts_ke_observed = $Assessment.nts_ke_observed
            nts_authenticated_measurement_observed = $Assessment.nts_authenticated_measurement_observed; accepted_authenticated_packets = $Assessment.accepted_authenticated_packets }
        raw_client_hashes = @($(if ($null -ne $r) { foreach ($field in @('configuration','authdata','ntpdata','capabilities','client_stderr','client_stdout')) {
            [pscustomobject]@{ field = $field; sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($r.$field)) }
        } }))
        capture_binding_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes(($Capture | ConvertTo-Json -Depth 18 -Compress)))
        raw_log_file = $(if ($retained) { [IO.Path]::GetFileName($logPath) }); raw_log_sha256 = $(if ($retained) { $selection.sha256 })
        assessment = $a; linkage_scope = 'reviewed_private_socket_and_protocol_single_nonce_directory_single_bounded_client_not_IP_equality_alone'
        technical_reference_access_date = '2026-11-16'
        method_sha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    # The raw bytes are separately retained; avoid a duplicate base64 log in the compact manifest.
    if ($null -ne $selection) { $manifest.query_observations = $selection | Select-Object * -ExcludeProperty raw_base64 }
    Publish-Mt5TickFile $manifestPath (($manifest | ConvertTo-Json -Depth 18 -Compress) + "`n")
    [pscustomobject]@{ manifest_file = [IO.Path]::GetFileName($manifestPath); manifest_sha256 = (Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant()
        raw_log_file = $manifest.raw_log_file; raw_log_sha256 = $manifest.raw_log_sha256; assessment = $a }
}

function Read-Mt5NtsSelectionEvidence([string]$ReportPath, [string]$ExpectedReportSha256) {
    $state = [pscustomobject]@{ status = 'data_unavailable'; assessment = $null; diagnostics = @() }
    try {
        if ($ExpectedReportSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'explicit_report_hash_required' }
        $report = Read-Mt5TickFile $ReportPath -Json
        if ($report.sha256 -cne $ExpectedReportSha256) { throw 'correlation_report_hash_mismatch' }
        $data = $report.data
        if ($data.schema -cne 'moneyway.windows-wsl-nts-correlation' -or $data.version -ne 1) { throw 'correlation_schema_mismatch' }
        if ($null -eq $data.PSObject.Properties['selection_evidence']) { throw 'legacy_selection_evidence_not_captured' }
        $d = $data.selection_evidence
        if ($d.manifest_file -cne ([IO.Path]::GetFileName($ReportPath) + '.selection.json')) { throw 'selection_manifest_name_mismatch' }
        $manifest = Read-Mt5TickFile ($ReportPath + '.selection.json') -Json
        if ($manifest.sha256 -cne $d.manifest_sha256) { throw 'selection_manifest_hash_mismatch' }
        $m = $manifest.data
        if ($m.schema -cne 'moneyway.nts-selection-evidence' -or $m.version -ne 1 -or $m.correlation_id -cne $data.correlation_id -or
            $m.capture_binding_sha256 -cne (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes(($data.capture | ConvertTo-Json -Depth 18 -Compress))))) { throw 'selection_manifest_capture_binding_mismatch' }
        if (-not [string]::IsNullOrEmpty($m.raw_log_file)) {
            if ($m.raw_log_file -cne ([IO.Path]::GetFileName($ReportPath) + '.selection.log') -or $d.raw_log_file -cne $m.raw_log_file) { throw 'selection_log_name_mismatch' }
            $log = Read-Mt5TickFile ($ReportPath + '.selection.log') -Raw
            if ($log.sha256 -cne $m.raw_log_sha256 -or $log.sha256 -cne $d.raw_log_sha256 -or
                $log.sha256 -cne $data.capture.result.selection.sha256) { throw 'retained_selection_log_hash_mismatch' }
        } elseif ($null -ne $data.capture.result -and $null -ne $data.capture.result.PSObject.Properties['selection'] -and
            $data.capture.result.selection.availability -ceq 'present') { throw 'retained_selection_log_missing' }
        $computed = Get-Mt5NtsSelectionAssessment $data.capture $data.correlation_id $m.assessment.assumptions
        if (($computed | ConvertTo-Json -Depth 15 -Compress) -cne ($m.assessment | ConvertTo-Json -Depth 15 -Compress) -or
            ($computed | ConvertTo-Json -Depth 15 -Compress) -cne ($d.assessment | ConvertTo-Json -Depth 15 -Compress)) { throw 'selection_assessment_mismatch' }
        $state.assessment = $computed; $state.status = 'passed'
    } catch { $state.diagnostics += $_.Exception.Message }
    $state
}
