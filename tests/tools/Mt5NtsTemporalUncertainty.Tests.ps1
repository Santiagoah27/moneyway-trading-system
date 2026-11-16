# Synthetic retained-report fixtures. This suite launches no WSL process or network query.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5NtsTemporalUncertainty.ps1')
$passed = 0; $failed = 0; $sequence = 0
$testRoot = Join-Path $env:TEMP ('moneyway-nts-uncertainty-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Reject([scriptblock]$Body) { $caught = $false; try { & $Body | Out-Null } catch { $caught = $true }; Check $caught }
function Stamp([int]$Ms) {
    [pscustomobject]@{ utc_candidate = ([datetimeoffset]'2026-11-16T20:00:00Z').AddMilliseconds($Ms).ToString('o')
        stopwatch_ticks = [long](1000000 + $Ms * 1000); stopwatch_frequency = 1000000
        monotonic_domain = 'windows_stopwatch'; realtime_domain = 'unix_utc_candidate' }
}
function Fixture {
    $sid = 'a' * 64; $dir = '/tmp/mw-nts-' + $sid
    $cfg = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`n"
    $real = Get-Mt5NtsUnixCandidate (Stamp 100).utc_candidate
    $capture = [pscustomobject]@{ session = $sid; error = $null; exit_code = 0
        initialization_before = Stamp -500; initialization_after = Stamp -1; launch = Stamp 0; ready = Stamp 90; completion = Stamp 6200
        before = [pscustomobject]@{ sent = Stamp 100; received = Stamp 110
            linux = [pscustomobject]@{ monotonic_before = '25.00'; monotonic_after = '25.01'; utc_unix_seconds = ($real + [decimal]'0.005').ToString([Globalization.CultureInfo]::InvariantCulture)
                monotonic_domain = 'linux_proc_uptime_boottime'; realtime_domain = 'unix_utc_candidate' } }
        after = [pscustomobject]@{ sent = Stamp 6100; received = Stamp 6110
            linux = [pscustomobject]@{ monotonic_before = '31.00'; monotonic_after = '31.01'; utc_unix_seconds = ($real + [decimal]'6.005').ToString([Globalization.CultureInfo]::InvariantCulture)
                monotonic_domain = 'linux_proc_uptime_boottime'; realtime_domain = 'unix_utc_candidate' } }
        result = [pscustomobject]@{ session = $sid; query_exit_code = 0; configuration = $cfg
            configuration_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($cfg))
            authdata = '192.0.2.1 NTS 1 30 128 3 0 0 8 64'
            ntpdata = "Remote address  : 192.0.2.1 ()`nLeap status : Normal`nReference time : Mon Nov 16 19:59:00 2026`nPrecision : -26 (0.000000015 seconds)`nAuthenticated : Yes`nTotal good RX : 2`nOffset : -0.012779095 seconds`nPeer delay : 0.025116688 seconds`nPeer dispersion : 0.000050441 seconds`nRoot delay : 0.091095 seconds`nRoot dispersion : 0.000626 seconds"
            capabilities = "Uid:`t100`t100`t100`t100`nCapEff:`t0000000000000000`nCapBnd:`t0000000000000000`nNoNewPrivs:`t1"
            client_stderr = "chronyd version 4.5 starting (+CMDMON +NTS)`nDisabled control of system clock`nSystem clock wrong by -0.005158 seconds (ignored)"
            client_stdout = '' } }
    [pscustomobject]@{ schema = 'moneyway.windows-wsl-nts-correlation'; version = 1; correlation_id = $sid
        reference_server = 'time.cloudflare.com'; client_profile = 'synthetic_authentic_format'; capture = $capture
        retained_evidence_hashes = @(); tool_sources = @() }
}
function Evidence($Source) {
    [pscustomobject]@{ status = 'passed'; capture = $Source; source_sha256 = '1' * 64; raw_fields_verified = 6
        reassessment = $null; reassessment_sha256 = $null; reassessment_status = 'data_unavailable'; diagnostics = @('reassessment_unavailable') }
}
function Audit($Source) { Get-Mt5NtsTemporalUncertainty (Evidence $Source) }
function Save-Fixture($Source) {
    $script:sequence++
    $Source.retained_evidence_hashes = @(@('configuration','authdata','ntpdata','capabilities','client_stderr','client_stdout') | ForEach-Object {
        [pscustomobject]@{ field = $_; sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($Source.capture.result.$_)) }
    })
    $path = Join-Path $testRoot ("capture-$sequence.json")
    [IO.File]::WriteAllText($path, ($Source | ConvertTo-Json -Depth 15 -Compress), (New-Object Text.UTF8Encoding($false)))
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $review = [pscustomobject]@{ schema = 'moneyway.windows-wsl-nts-correlation-reassessment'; version = 1
        source_report_sha256 = $hash; correlation_id = $Source.correlation_id; assessment = 'synthetic_summary_not_trusted_for_derivation' }
    $reviewPath = Join-Path $testRoot ("review-$sequence.json")
    [IO.File]::WriteAllText($reviewPath, ($review | ConvertTo-Json -Compress), (New-Object Text.UTF8Encoding($false)))
    [pscustomobject]@{ path = $path; hash = $hash; review = $reviewPath; review_hash = (Get-FileHash -LiteralPath $reviewPath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
function Tracking {
    [pscustomobject]@{ source = 'chronyc_tracking_4.5'; system_time_offset_seconds = '-0.001'; root_delay_seconds = '0.02'; root_dispersion_seconds = '0.003'
        leap_status = 'Normal'; reference_time = '2026-11-16T20:00:00Z'; observation_id = 'synthetic-tracking-1'
        assumptions = [pscustomobject]@{ ultimate_reference_correct = $true; same_observation_and_clock = $true; mode_semantics_reviewed = $true; applies_only_at_observation = $true } }
}
try {
    Case 'Authentic-format report verifies six raw fields and reassessment binding' {
        $f = Save-Fixture (Fixture); $e = Read-Mt5NtsUncertaintyEvidence $f.path $f.hash $f.review $f.review_hash
        Check ($e.status -ceq 'passed' -and $e.raw_fields_verified -eq 6 -and $e.reassessment_status -ceq 'passed')
        Check (Get-Mt5NtsTemporalUncertainty $e).linux_nts_authentication.nts_authenticated_measurement_observed
    }
    Case 'Incorrect source hash blocks derivation' {
        $f = Save-Fixture (Fixture); $e = Read-Mt5NtsUncertaintyEvidence $f.path ('0' * 64)
        Check ($e.status -ceq 'data_unavailable' -and $null -eq (Get-Mt5NtsTemporalUncertainty $e).recorded_inputs)
    }
    Case 'Missing reassessment is explicit and does not erase Linux authentication' {
        $f = Save-Fixture (Fixture); $e = Read-Mt5NtsUncertaintyEvidence $f.path $f.hash
        Check ($e.diagnostics -contains 'reassessment_unavailable'); Check (Get-Mt5NtsTemporalUncertainty $e).linux_nts_authentication.nts_authenticated_measurement_observed
    }
    Case 'Wrong reassessment hash is explicit' {
        $f = Save-Fixture (Fixture); $e = Read-Mt5NtsUncertaintyEvidence $f.path $f.hash $f.review ('0' * 64)
        Check ($e.diagnostics -contains 'reassessment_hash_mismatch')
    }
    Case 'Reassessment for another source is rejected' {
        $f = Save-Fixture (Fixture); $g = Save-Fixture (Fixture)
        $review = Get-Content -LiteralPath $g.review -Raw | ConvertFrom-Json; $review.source_report_sha256 = '0' * 64
        [IO.File]::WriteAllText($g.review, ($review | ConvertTo-Json -Compress))
        $e = Read-Mt5NtsUncertaintyEvidence $f.path $f.hash $g.review ((Get-FileHash -LiteralPath $g.review).Hash)
        Check ($e.diagnostics -contains 'reassessment_source_binding_mismatch')
    }
    Case 'Raw field digest mismatch is rejected even with a matching enclosing file pin' {
        $f = Save-Fixture (Fixture); $s = Get-Content -LiteralPath $f.path -Raw | ConvertFrom-Json; $s.capture.result.authdata = 'changed'
        [IO.File]::WriteAllText($f.path, ($s | ConvertTo-Json -Depth 15 -Compress))
        $e = Read-Mt5NtsUncertaintyEvidence $f.path ((Get-FileHash -LiteralPath $f.path).Hash)
        Check ($e.diagnostics -contains 'raw_field_hash_mismatch')
    }
    Case 'Host monotonic order is independently validated' { $s = Fixture; $s.capture.after.sent.stopwatch_ticks = 1; Check ((Audit $s).ordering.host_monotonic_status -ceq 'data_unavailable') }
    Case 'Linux monotonic order is independently validated' { $s = Fixture; $s.capture.after.linux.monotonic_before = '1.00'; Check ((Audit $s).ordering.linux_monotonic_status -ceq 'data_unavailable') }
    Case 'Cross-domain counter substitution is rejected' { $s = Fixture; $s.capture.after.linux.monotonic_domain = 'windows_stopwatch'; Check ((Audit $s).contradictions -contains 'linux_clock_domain_mismatch') }
    Case 'Request response ordering yields two conditional read intervals' {
        $a = Audit (Fixture); Check ($a.ordering.status -ceq 'passed' -and $a.conditional_cross_clock_intervals.Count -eq 2)
        Check ($a.conditional_cross_clock_intervals[0].lower_seconds -eq [decimal]'-0.005' -and $a.conditional_cross_clock_intervals[0].upper_seconds -eq [decimal]'0.005')
        Check (-not $a.conditional_cross_clock_intervals[0].assumptions_verified)
    }
    Case 'Invalid request response counter order cannot form intervals' {
        $s = Fixture; $s.capture.before.received.stopwatch_ticks = 1; Check ((Audit $s).conditional_cross_clock_intervals.Count -eq 0)
    }
    Case 'Linux truncation expands the elapsed bracket in both directions' {
        $a = Audit (Fixture); Check ($a.local_elapsed_constraints.linux_boottime_elapsed_seconds[0] -eq [decimal]'5.98')
        Check ($a.local_elapsed_constraints.linux_boottime_elapsed_seconds[1] -eq [decimal]'6.02')
    }
    Case 'Same quantized coordinates retain the causal nonnegative elapsed constraint' {
        $s = Fixture; $s.capture.before.linux.monotonic_after = '25.00'; $s.capture.after.linux.monotonic_before = '25.00'; $s.capture.after.linux.monotonic_after = '25.00'
        $a = Audit $s; Check ($a.local_elapsed_constraints.linux_boottime_elapsed_seconds[0] -eq 0)
        Check ($a.local_elapsed_constraints.linux_boottime_elapsed_seconds[1] -eq [decimal]'0.01')
    }
    Case 'Missing Windows wall reading preserves counter ordering' {
        $s = Fixture; $s.capture.before.sent.utc_candidate = ''; $a = Audit $s
        Check ($a.ordering.status -ceq 'passed' -and $a.conditional_cross_clock_intervals.Count -eq 0)
        Check ($a.local_elapsed_constraints.windows_capture_execution_seconds -eq [decimal]'6.2')
    }
    Case 'Missing Linux wall reading preserves boottime ordering' {
        $s = Fixture; $s.capture.before.linux.utc_unix_seconds = ''; $a = Audit $s
        Check ($a.ordering.status -ceq 'passed' -and $a.conditional_cross_clock_intervals.Count -eq 0)
    }
    Case 'Authenticated packets do not create a global time error bound' {
        $a = Audit (Fixture); Check $a.linux_nts_authentication.nts_authenticated_measurement_observed
        Check ($null -eq $a.windows_host_utc_error_bound_seconds -and $a.overall_utc_uncertainty -ceq 'unresolved')
    }
    Case 'Packet root fields are inventoried with remote provenance' {
        $a = Audit (Fixture); $field = @($a.chrony_field_inventory | Where-Object { $_.field -ceq 'Root delay' -and $_.present })
        Check ($field.Count -eq 1 -and $field[0].clock_scope -ceq 'remote_response_NTP_fields')
    }
    Case 'Missing packet root dispersion remains absent' {
        $s = Fixture; $s.capture.result.ntpdata = $s.capture.result.ntpdata.Replace('Root dispersion : 0.000626 seconds','')
        Check (@((Audit $s).chrony_field_inventory | Where-Object { $_.field -ceq 'Root dispersion' -and $_.present }).Count -eq 0)
    }
    Case 'Missing tracking root dispersion never becomes zero' {
        $t = Tracking; $t.root_dispersion_seconds = $null; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Missing tracking root delay never uses peer delay' {
        $t = Tracking; $t.root_delay_seconds = $null; $t | Add-Member peer_delay_seconds '0.001'
        Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Remote packet values cannot masquerade as tracking values' {
        $t = Tracking; $t.source = 'chronyc_ntpdata_4.5'; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Documented conditional formula uses same-observation tracking inputs' {
        $bound = Get-Mt5ChronyConditionalErrorBound (Tracking); Check ($bound.conditional_bound_seconds -eq [decimal]'0.014')
        Check ($bound.status -ceq 'candidate' -and -not $bound.unconditional_utc_bound_established)
    }
    Case 'Formula requires explicit reference correctness assumption' {
        $t = Tracking; $t.assumptions.ultimate_reference_correct = $false; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Formula requires reviewed non-adjusting mode semantics' {
        $t = Tracking; $t.assumptions.mode_semantics_reviewed = $false; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Formula refuses unsynchronized tracking state' {
        $t = Tracking; $t.leap_status = 'Not synchronised'; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Q printed offset cannot fill missing System time' {
        $t = Tracking; $t.system_time_offset_seconds = $null; $t | Add-Member final_Q_offset_seconds '-0.005158'
        Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Last offset is not substituted for System time' {
        $t = Tracking; $t | Add-Member last_offset_seconds '1000'; Check ((Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds -eq [decimal]'0.014')
    }
    Case 'Negative uncertainty components are rejected' {
        $t = Tracking; $t.root_delay_seconds = '-1'; Check ($null -eq (Get-Mt5ChronyConditionalErrorBound $t).conditional_bound_seconds)
    }
    Case 'Backward Windows wall change remains a temporal finding with valid counters' {
        $s = Fixture; $s.capture.after.sent.utc_candidate = (Stamp 1).utc_candidate; $a = Audit $s
        Check ($a.temporal_findings -contains 'windows_realtime_discontinuity'); Check ($a.ordering.status -ceq 'passed')
    }
    Case 'Forward Linux wall jump fails only the unit-rate no-step hypothesis' {
        $s = Fixture; $s.capture.after.linux.utc_unix_seconds = ([decimal]$s.capture.after.linux.utc_unix_seconds + 1).ToString([Globalization.CultureInfo]::InvariantCulture)
        $a = Audit $s; Check (-not $a.local_elapsed_constraints.linux_unit_rate_no_step_hypothesis_feasible)
        Check ($a.contradictions.Count -eq 0)
    }
    Case 'Disjoint intervals reject constant offset without inventing data contradiction' {
        $s = Fixture; $s.capture.after.linux.utc_unix_seconds = ([decimal]$s.capture.after.linux.utc_unix_seconds + 1).ToString([Globalization.CultureInfo]::InvariantCulture)
        $a = Audit $s; Check ($a.constant_offset_hypothesis.status -ceq 'failed' -and $a.constant_offset_hypothesis.intersection.empty)
        Check ($a.contradictions.Count -eq 0)
    }
    Case 'Reversed interval endpoints are rejected' { Reject { Get-Mt5NtsIntervalIntersection 1 0 0 1 } }
    Case 'Touching intervals intersect at one conditional coordinate' {
        $i = Get-Mt5NtsIntervalIntersection -1 0 0 1; Check (-not $i.empty -and $i.lower_seconds -eq 0 -and $i.upper_seconds -eq 0)
    }
    Case 'Overlapping observations do not verify constant drift or future behavior' {
        $a = Audit (Fixture); Check (-not $a.constant_offset_hypothesis.additional_assumption_verified)
        Check (-not $a.constant_offset_hypothesis.future_or_intermediate_event_propagation_authorized)
        Check (-not $a.local_elapsed_constraints.UTC_rate_calibration_or_drift_bound_established)
    }
    Case 'HyperV supplies no equality guarantee or actual UTC event coordinate' {
        $a = Audit (Fixture); Check ($null -eq $a.mt5_event_utc_interval -and -not $a.windows_clock_authenticated)
        Check ($a.clock_domains.Count -eq 8 -and $a.missing_evidence -contains 'provider_historical_timezone_DST_mapping')
    }
    Case 'Closure and availability remain separate missing evidence' {
        Check ((Audit (Fixture)).missing_evidence -contains 'provider_closure_and_historical_availability')
    }
    Case 'All canonical and Windows authentication flags remain false' {
        $a = Audit (Fixture); Check (-not $a.windows_clock_authenticated -and -not $a.mt5_native_utc_authenticated -and -not $a.canonical_utc_conversion_authorized -and -not $a.utc_conversion_authenticated -and -not $a.canonical_ready)
    }
    Case 'Assessment is deterministic for identical validated input' {
        $e = Evidence (Fixture); $a = Get-Mt5NtsTemporalUncertainty $e | ConvertTo-Json -Depth 18 -Compress
        Check ($a -ceq (Get-Mt5NtsTemporalUncertainty $e | ConvertTo-Json -Depth 18 -Compress))
    }
    Case 'Command uses immutable reports and preserves capture reassessment and checkpoint sentinel' {
        $f = Save-Fixture (Fixture); $checkpoint = Join-Path $testRoot 'checkpoint.json'; [IO.File]::WriteAllText($checkpoint,'synthetic-checkpoint')
        $before = @($f.path,$f.review,$checkpoint | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        $path = Join-Path $testRoot 'audit.json'
        $json = & (Join-Path $tools 'Get-Mt5NtsTemporalUncertainty.ps1') -CapturePath $f.path -ExpectedCaptureSha256 $f.hash -ReassessmentPath $f.review -ExpectedReassessmentSha256 $f.review_hash -ReportPath $path
        $summary = $json -join "`n" | ConvertFrom-Json; Check ($summary.InputIntegrityStatus -ceq 'passed')
        Reject { & (Join-Path $tools 'Get-Mt5NtsTemporalUncertainty.ps1') -CapturePath $f.path -ExpectedCaptureSha256 $f.hash -ReportPath $path }
        $after = @($f.path,$f.review,$checkpoint | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        Check (($before -join ',') -ceq ($after -join ','))
    }
    Case 'Offline entry points parse and contain no acquisition or trading invocation' {
        foreach ($name in @('Mt5NtsTemporalUncertainty.ps1','Get-Mt5NtsTemporalUncertainty.ps1')) {
            $errors = $null; $tokens = $null; $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $tools $name), [ref]$tokens, [ref]$errors)
            Check ($errors.Count -eq 0)
            $commands = @($ast.FindAll({param($n) $n -is [Management.Automation.Language.CommandAst]}, $true) | ForEach-Object { $_.GetCommandName() })
            Check (@($commands | Where-Object { $_ -match '^(wsl|chronyd|Invoke-Mt5NtsLiveCapture|Invoke-Mt5NtsWslCommand|Start-Process|Set-Date|Restart-Service|Submit-Order|Commit-Checkpoint)$' }).Count -eq 0)
        }
    }
} finally {
    # Only this suite's owned, flat temporary directory and files.
    foreach ($file in Get-ChildItem -LiteralPath $testRoot -File) { Remove-Item -LiteralPath $file.FullName }
    Remove-Item -LiteralPath $testRoot
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { exit 1 }
