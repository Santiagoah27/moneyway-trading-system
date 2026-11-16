# All rows and client telemetry here are synthetic. No WSL or network execution.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5NtsSelectionEvidence.ps1')
$passed = 0; $failed = 0
$sid = 'a' * 64
$conditions = @('ultimate_reference_correct','chrony_source_selection_model_applies','no_unbounded_clock_change_between_sample_and_selection')
$root = Join-Path $env:TEMP ('moneyway-selection-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Reject([scriptblock]$Body) { $caught = $false; try { & $Body | Out-Null } catch { $caught = $true }; Check $caught }
function Set-Log($Capture, [string]$Text) {
    $Capture.result.selection.raw_base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Text))
    $Capture.result.selection.sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($Text))
}
function Fixture {
    $dir = '/tmp/mw-nts-' + $sid
    $cfg = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`nlog selection`nlogdir $dir`n"
    $c = [pscustomobject]@{ session = $sid; error = $null; exit_code = 0
        before = [pscustomobject]@{ linux = [pscustomobject]@{ monotonic_after = '25.00' } }
        after = [pscustomobject]@{ linux = [pscustomobject]@{ monotonic_before = '31.00' } }
        result = [pscustomobject]@{ session = $sid; query_exit_code = 0; configuration = $cfg
            configuration_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($cfg))
            authdata = '192.0.2.1 NTS 1 30 128 3 0 0 8 64'
            ntpdata = "Remote address  : 192.0.2.1 ()`nAuthenticated   : Yes`nTotal good RX   : 2"
            capabilities = "Uid:`t100`t100`t100`t100`nCapEff:`t0000000000000000`nCapBnd:`t0000000000000000`nNoNewPrivs:`t1"
            client_stderr = "chronyd version 4.5 starting (+CMDMON +NTS)`nDisabled control of system clock`nSystem clock wrong by -0.003012 seconds (ignored)"
            client_stdout = ''; selection = [pscustomobject]@{ session = $sid; availability = 'present'; raw_base64 = ''; sha256 = ''
                start_boottime = '25.00'; end_boottime = '31.00'; start_realtime_candidate = '1794859200.000000000'; end_realtime_candidate = '1794859206.000000000'
                command = "/usr/sbin/chronyd -Q -t 20 -f $dir/chrony.conf"; os_release = 'PRETTY_NAME="Ubuntu 24.04.5 LTS"' } } }
    Set-Log $c "2026-11-16 20:00:05 192.0.2.1 * ----- 3 1.00 1.200e-01 -1.234e-02 +2.345e-02`n"
    $c
}
function Audit($Capture) { Get-Mt5NtsSelectionAssessment $Capture $sid $conditions }
function Bundle($Capture) {
    $path = Join-Path $root ([guid]::NewGuid().ToString('N') + '.json')
    $d = Publish-Mt5NtsSelectionEvidence $Capture $sid $path 'synthetic_distribution' (Get-Mt5NtsCorrelationAssessment $Capture $sid)
    $report = [pscustomobject]@{ schema = 'moneyway.windows-wsl-nts-correlation'; version = 1; correlation_id = $sid; capture = $Capture; selection_evidence = $d }
    Publish-Mt5TickFile $path ($report | ConvertTo-Json -Depth 18 -Compress)
    [pscustomobject]@{ path = $path; hash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() }
}
try {
    Case 'Exact session-specific log configuration remains NTS compatible' { Check (Audit (Fixture)).selected_source_authenticated }
    Case 'Private logdir cannot reference a stale session' {
        $c = Fixture; $c.result.configuration = $c.result.configuration.Replace($sid,('b' * 64))
        Check ((Audit $c).diagnostics -contains 'selection_configuration_or_session_path_mismatch')
    }
    Case 'Present nonempty log is an authenticated source-selection estimate' { $a = Audit (Fixture); Check ($a.gate -ceq 'G_authenticated_selected_interval' -and $a.row_count -eq 1) }
    Case 'Empty log is distinct from missing' { $c = Fixture; Set-Log $c ''; Check ((Audit $c).gate -ceq 'B_empty') }
    Case 'Missing file preserves independent NTS authentication' { $c = Fixture; $c.result.selection.availability = 'missing'; $a = Audit $c; Check ($a.gate -ceq 'A_missing' -and $a.nts_authenticated_measurement_observed) }
    Case 'Header-only file contains no selection rows' {
        $c = Fixture; Set-Log $c "===================`n   Date (UTC) Time     IP Address   S EOpts Reach Score Last sample  Low end   High end`n===================`n"
        Check ((Audit $c).gate -ceq 'C_header_only')
    }
    Case 'Multiple rows are counted and latest selected row retained' {
        $c = Fixture; $line = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($c.result.selection.raw_base64))
        Set-Log $c ($line.Replace(' * ', ' M ') + $line + $line.Replace('20:00:05','20:00:06')); $a = Audit $c
        Check ($a.row_count -eq 3 -and $a.selected_row.source_timestamp_label -ceq '2026-11-16 20:00:06')
    }
    Case 'Later stale row preserves the earlier selection event without claiming final selected state' {
        $c = Fixture; $line = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($c.result.selection.raw_base64))
        Set-Log $c ($line + $line.Replace(' * ', ' S ')); $a = Audit $c
        Check ($a.gate -ceq 'G_authenticated_selected_interval' -and $a.rows[-1].state -ceq 'S' -and $a.selected_row.state -ceq '*')
    }
    Case 'Summary bounds parsed rows while complete log is preserved' {
        $c = Fixture; $line = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($c.result.selection.raw_base64)); Set-Log $c ($line * 20)
        $a = Audit $c; Check ($a.row_count -eq 20 -and $a.rows.Count -eq 16 -and $a.rows_truncated)
    }
    Case 'Insufficient source state is not selected' {
        $c = Fixture; Set-Log $c "2026-11-16 20:00:05 192.0.2.1 M ----- 1 1.00 1e-01 0 0`n"; Check ((Audit $c).gate -ceq 'D_not_selected')
    }
    Case 'Selected row without packet authentication cannot reach G' {
        $c = Fixture; $c.result.ntpdata = $c.result.ntpdata.Replace('Authenticated   : Yes','Authenticated   : No'); Check ((Audit $c).gate -ceq 'F_selected_authentication_unproven')
    }
    Case 'Selected source is matched to same execution NTS telemetry' { Check ((Audit (Fixture)).chrony_selection_interval_status -ceq 'context_specific') }
    Case 'Different selected source is rejected' {
        $c = Fixture; Set-Log $c "2026-11-16 20:00:05 192.0.2.2 * ----- 1 1.00 1e-01 -1e-2 1e-2`n"; Check ((Audit $c).diagnostics -contains 'selection_source_mismatch')
    }
    Case 'Invalid selection session binding is rejected' { $c = Fixture; $c.result.selection.session = 'b' * 64; Check ((Audit $c).diagnostics -contains 'selection_session_mismatch') }
    Case 'Stale command from another directory is rejected' { $c = Fixture; $c.result.selection.command = $c.result.selection.command.Replace($sid,('b' * 64)); Check ((Audit $c).diagnostics -contains 'selection_command_binding_mismatch') }
    Case 'Log query counters must lie inside correlation observations' { $c = Fixture; $c.result.selection.start_boottime = '24.00'; Check ((Audit $c).diagnostics -contains 'selection_query_bounds_mismatch') }
    Case 'Reversed interval is rejected' {
        $c = Fixture; Set-Log $c "2026-11-16 20:00:05 192.0.2.1 * ----- 1 1.00 1e-01 1e-2 -1e-2`n"; Check ((Audit $c).diagnostics -contains 'invalid_selection_interval_or_age')
    }
    Case 'Malformed interval is rejected' {
        $c = Fixture; Set-Log $c "2026-11-16 20:00:05 192.0.2.1 * ----- 1 1.00 1e-01 NaN 1e-2`n"; Check ((Audit $c).diagnostics -contains 'malformed_selection_number')
    }
    Case 'Negative positive and exponent literals retain original precision' {
        $a = Audit (Fixture); Check ($a.conditional_interval.lower_original -ceq '-1.234e-02' -and $a.conditional_interval.upper_original -ceq '+2.345e-02')
        Check ($a.conditional_interval.lower_seconds -eq [decimal]'-0.01234' -and $a.conditional_interval.upper_seconds -eq [decimal]'0.02345')
    }
    Case 'Unrepresentable precision is rejected rather than rounded' { Reject { ConvertFrom-Mt5NtsSelectionNumber '1e-100' } }
    Case 'Tampered transport log is rejected' { $c = Fixture; $c.result.selection.raw_base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('changed')); Check ((Audit $c).diagnostics -contains 'selection_log_hash_mismatch') }
    Case 'Wrong raw log SHA256 is rejected' { $c = Fixture; $c.result.selection.sha256 = '0' * 64; Check ((Audit $c).diagnostics -contains 'selection_log_hash_mismatch') }
    Case 'NTS-KE failure cannot authenticate selection' { $c = Fixture; $c.result.authdata = '192.0.2.1 NTS 0 30 128 3 0 0 0 64'; Check (-not (Audit $c).selected_source_authenticated) }
    Case 'No accepted authenticated packets cannot authenticate selection' { $c = Fixture; $c.result.ntpdata = $c.result.ntpdata.Replace('RX   : 2','RX   : 0'); Check (-not (Audit $c).selected_source_authenticated) }
    Case 'Ordinary NTP fallback is rejected' { $c = Fixture; $c.result.authdata = $c.result.authdata.Replace('NTS','NTP'); Check (-not (Audit $c).selected_source_authenticated) }
    Case 'Nonzero Chrony exit is preserved' { $c = Fixture; $c.result.query_exit_code = 1; Check (-not (Audit $c).selected_source_authenticated) }
    Case 'Timeout cannot promote a selection row' { $c = Fixture; $c.error = 'nts_timeout'; Check (-not (Audit $c).selected_source_authenticated) }
    Case 'Partial or malformed row is rejected' { $c = Fixture; Set-Log $c '2026-11-16 20:00:05 192.0.2.1 *'; Check ((Audit $c).diagnostics -contains 'malformed_selection_row') }
    Case 'Missing conditional assumptions prevents interval promotion' { Check ((Get-Mt5NtsSelectionAssessment (Fixture) $sid).diagnostics -contains 'selection_conditional_assumptions_missing') }
    Case 'Authenticated selection never implies unconditional bounds or canonical facts' {
        $a = Audit (Fixture); Check ($a.overall_utc_uncertainty -ceq 'unresolved' -and $null -eq $a.windows_host_utc_error_bound_seconds -and $null -eq $a.mt5_event_utc_interval)
        Check (-not $a.windows_clock_authenticated -and -not $a.mt5_native_utc_authenticated -and -not $a.utc_conversion_authenticated -and -not $a.canonical_utc_conversion_authorized -and -not $a.canonical_ready)
        Check (-not $a.conditional_interval.rigorous_UTC_bound_established -and -not $a.conditional_interval.assumptions_verified)
    }
    Case 'Authentic-format bundle revalidates immutable raw log and manifest' { $b = Bundle (Fixture); Check ((Read-Mt5NtsSelectionEvidence $b.path $b.hash).status -ceq 'passed') }
    Case 'Changed retained log fails independent read' {
        $b = Bundle (Fixture); [IO.File]::AppendAllText(($b.path + '.selection.log'),'tampered')
        Check ((Read-Mt5NtsSelectionEvidence $b.path $b.hash).diagnostics -contains 'retained_selection_log_hash_mismatch')
    }
    Case 'Wrong report pin fails independent read' { $b = Bundle (Fixture); Check ((Read-Mt5NtsSelectionEvidence $b.path ('0' * 64)).status -ceq 'data_unavailable') }
    Case 'Changed retained manifest fails independent read' {
        $b = Bundle (Fixture); [IO.File]::AppendAllText(($b.path + '.selection.json'),' ')
        Check ((Read-Mt5NtsSelectionEvidence $b.path $b.hash).diagnostics -contains 'selection_manifest_hash_mismatch')
    }
    Case 'Partial log publication blocks reuse' {
        $c = Fixture; $p = Join-Path $root 'partial.json'; [IO.File]::WriteAllText(($p + '.selection.log.pending'),'partial')
        Reject { Publish-Mt5NtsSelectionEvidence $c $sid $p 'synthetic' (Get-Mt5NtsCorrelationAssessment $c $sid) }
    }
    Case 'Legacy captures remain readable without invented selection' {
        $c = Fixture; $c.result.PSObject.Properties.Remove('selection'); $a = Audit $c
        Check ($a.log_availability -ceq 'not_captured_legacy_or_failed_session' -and $a.row_count -eq 0)
        $c.result.configuration = $c.result.configuration.Replace("log selection`nlogdir /tmp/mw-nts-$sid`n",'')
        $c.result.configuration_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($c.result.configuration))
        Check (Get-Mt5NtsCorrelationAssessment $c $sid).nts_authenticated_measurement_observed
    }
    Case 'Selected source remains evidence only with no DST closure or availability claims' { Check ((Audit (Fixture)).conditional_interval.scope -ceq 'Chrony_source_selection_local_clock_offset_estimate_at_selection_not_tracking_formula') }
    Case 'Raw inputs and checkpoint sentinel remain unchanged during publication' {
        $p = Join-Path $root 'checkpoint.json'; [IO.File]::WriteAllText($p,'checkpoint-sentinel'); $pin = (Get-FileHash $p).Hash
        $c = Fixture; $before = $c | ConvertTo-Json -Depth 15 -Compress; $null = Bundle $c
        Check (($c | ConvertTo-Json -Depth 15 -Compress) -ceq $before -and (Get-FileHash $p).Hash -ceq $pin)
    }
    Case 'Empty log can be retained and independently verified' { $c = Fixture; Set-Log $c ''; $b = Bundle $c; Check ((Read-Mt5NtsSelectionEvidence $b.path $b.hash).assessment.gate -ceq 'B_empty') }
    Case 'Missing log yields a manifest without fabricated file' { $c = Fixture; $c.result.selection.availability = 'missing'; $b = Bundle $c; Check (-not (Test-Path -LiteralPath ($b.path + '.selection.log'))); Check ((Read-Mt5NtsSelectionEvidence $b.path $b.hash).status -ceq 'passed') }
    Case 'Shell retains bounded no-capability execution and checks absent stale log' {
        $sh = [IO.File]::ReadAllText((Join-Path $tools 'Invoke-Mt5NtsLinuxCapture.sh'))
        Check ($sh.Contains('log selection') -and $sh.Contains('logdir $dir') -and $sh.Contains('[ ! -e "$dir/selection.log" ]'))
        Check ($sh.Contains('setpriv --bounding-set=-all') -and $sh.Contains('/usr/sbin/chronyd -Q -t'))
        Check (-not ($sh -cmatch '\s-q(?:\s|$)|\nmakestep\s|wsl --shutdown'))
    }
    Case 'Selection and coordinator PowerShell AST validation' {
        foreach ($name in @('Mt5NtsSelectionEvidence.ps1','Mt5NtsCorrelation.ps1','Get-Mt5NtsTimeCorrelation.ps1')) {
            $errors = $null; $tokens = $null; $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $tools $name),[ref]$tokens,[ref]$errors); Check ($errors.Count -eq 0)
        }
    }
} finally {
    foreach ($file in Get-ChildItem -LiteralPath $root -File) { Remove-Item -LiteralPath $file.FullName }
    Remove-Item -LiteralPath $root
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { exit 1 }
