# Synthetic semantic inputs. Real retained evidence is verified separately through the CLI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5NtsSelectionSemantics.ps1')
$passed = 0
$failed = 0
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Reject([scriptblock]$Body) {
    $caught = $false
    try { & $Body | Out-Null } catch { $caught = $true }
    Check $caught
}
function Fixture {
    $session = 'a' * 64
    $lines = @('M','M','*','S') | ForEach-Object { "2026-11-16 23:12:55 192.0.2.1 $_ ----- 7 1.00 4.138e-03 3.768e-03 1.043e-01" }
    $evidence = [pscustomobject]@{ status = 'passed'; diagnostics = @(); assessment = [pscustomobject]@{
        correlation_id = $session; gate = 'G_authenticated_selected_interval'; selected_source_authenticated = $true
        nts_authenticated_measurement_observed = $true
        selected_row = [pscustomobject]@{ source_address = '192.0.2.1'; source_timestamp_label = '2026-11-16 23:12:55'
            lower_original = '3.768e-03'; upper_original = '1.043e-01' }
        assumptions = @('ultimate_reference_correct','chrony_source_selection_model_applies','no_unbounded_clock_change_between_sample_and_selection')
    } }
    $report = [pscustomobject]@{ schema = 'moneyway.windows-wsl-nts-correlation'; version = 1; correlation_id = $session
        client_profile = 'Ubuntu_chrony_4.5-1ubuntu4.2_executable_hash_pinned'; reference_server = 'time.cloudflare.com'
        capture = [pscustomobject]@{ session = $session; result = [pscustomobject]@{
            selection = [pscustomobject]@{ raw_base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")) }
        } }
    }
    [pscustomobject]@{ evidence = $evidence; report = $report }
}
function Audit($Fixture) { Get-Mt5NtsSelectionSemantics $Fixture.evidence $Fixture.report }

function Publish-Fixture([string]$Folder) {
    $f = Fixture
    $capture = $f.report.capture
    $session = $capture.session
    $dir = '/tmp/mw-nts-' + $session
    $configuration = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`nlog selection`nlogdir $dir`n"
    $capture | Add-Member error $null
    $capture | Add-Member exit_code 0
    $capture | Add-Member before ([pscustomobject]@{ linux = [pscustomobject]@{ monotonic_after = '25.00' } })
    $capture | Add-Member after ([pscustomobject]@{ linux = [pscustomobject]@{ monotonic_before = '31.00' } })
    $r = $capture.result
    $r | Add-Member session $session
    $r | Add-Member query_exit_code 0
    $r | Add-Member configuration $configuration
    $r | Add-Member configuration_sha256 (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($configuration)))
    $r | Add-Member authdata '192.0.2.1 NTS 1 30 128 3 0 0 8 64'
    $r | Add-Member ntpdata "Remote address  : 192.0.2.1 ()`nAuthenticated : Yes`nTotal good RX : 2"
    $r | Add-Member capabilities "Uid:`t100`t100`t100`t100`nCapEff:`t0000000000000000`nCapBnd:`t0000000000000000`nNoNewPrivs:`t1"
    $r | Add-Member client_stderr "chronyd version 4.5 starting (+CMDMON +NTS)`nDisabled control of system clock`nSystem clock wrong by -0.054047 seconds (ignored)"
    $r | Add-Member client_stdout ''
    $r.selection | Add-Member session $session
    $r.selection | Add-Member availability 'present'
    $r.selection | Add-Member sha256 (Get-Mt5TickHash ([Convert]::FromBase64String($r.selection.raw_base64)))
    $r.selection | Add-Member start_boottime '25.00'
    $r.selection | Add-Member end_boottime '31.00'
    $r.selection | Add-Member start_realtime_candidate '1794870768.000000000'
    $r.selection | Add-Member end_realtime_candidate '1794870775.000000000'
    $r.selection | Add-Member command "/usr/sbin/chronyd -Q -t 20 -f $dir/chrony.conf"
    $r.selection | Add-Member os_release 'synthetic Ubuntu 24.04.5'
    $path = Join-Path $Folder ('capture-' + [guid]::NewGuid().ToString('N') + '.json')
    $descriptor = Publish-Mt5NtsSelectionEvidence $capture $session $path 'synthetic' (Get-Mt5NtsCorrelationAssessment $capture $session)
    $f.report | Add-Member selection_evidence $descriptor
    Publish-Mt5TickFile $path ($f.report | ConvertTo-Json -Depth 18 -Compress)
    [pscustomobject]@{ path = $path; hash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant(); log_hash = $r.selection.sha256 }
}

Case 'Authentic-format positive selection interval receives reviewed sign interpretation' {
    $a = Audit (Fixture)
    Check ($a.status -ceq 'context_specific' -and $a.sign_interpretation.quantity -ceq 'Chrony_cooked_local_clock_minus_reference')
    Check ($a.sign_interpretation.positive -ceq 'local_clock_ahead_of_reference')
}
Case 'Negative endpoint rounds with exact decimal bounds' {
    $e = Get-Mt5NtsPrintedEndpoint '-1.234e-02'
    Check ($e.formatting_lower_seconds -eq [decimal]'-0.01235' -and $e.formatting_upper_seconds -eq [decimal]'-0.01233')
}
Case 'Mixed sign intervals preserve the captured signs' {
    $f = Fixture; $f.evidence.assessment.selected_row.lower_original = '-3.768e-03'
    Check ((Audit $f).printed_interval.lower_seconds -eq [decimal]'-0.003768')
}
Case 'Signed zero text and sign survive exact zero enclosure' {
    $e = Get-Mt5NtsPrintedEndpoint '-0.000e+00'
    Check ($e.original -ceq '-0.000e+00' -and $e.negative_zero -and $e.formatting_lower_seconds -eq 0 -and $e.formatting_upper_seconds -eq 0)
}
Case 'Unsigned zero has the canonical zero exponent' { Check (-not (Get-Mt5NtsPrintedEndpoint '0.000e+00').negative_zero) }
Case 'Scientific exponent determines the least printed digit' { Check ((Get-Mt5NtsPrintedEndpoint '3.768e-03').quantum_seconds -eq [decimal]'0.000001') }
Case 'All standard rounding modes use a conservative whole-quantum enclosure' {
    $a = Audit (Fixture)
    Check ($a.formatting_aware_interval.lower_seconds -eq [decimal]'0.003767' -and $a.formatting_aware_interval.upper_seconds -eq [decimal]'0.1044')
}
Case 'Nearest-only half-quantum candidate is explicitly conditional' {
    $a = Audit (Fixture)
    Check ($a.nearest_rounding_candidate.lower_seconds -eq [decimal]'0.0037675' -and $a.nearest_rounding_candidate.upper_seconds -eq [decimal]'0.10435')
    Check (-not $a.nearest_rounding_candidate.assumption_verified)
}
Case 'Power of ten carry boundary remains conservatively enclosed' {
    $e = Get-Mt5NtsPrintedEndpoint '1.000e+00'
    Check ($e.formatting_lower_seconds -eq [decimal]'0.999' -and $e.formatting_upper_seconds -eq [decimal]'1.001')
}
Case 'Four significant digits are required by the reviewed contract' { Reject { Get-Mt5NtsPrintedEndpoint '3.7680e-03' } }
Case 'Decimal comma is rejected' { Reject { Get-Mt5NtsPrintedEndpoint '3,768e-03' } }
Case 'Nonfinite endpoint is rejected' { Reject { Get-Mt5NtsPrintedEndpoint 'NaN' }; Reject { Get-Mt5NtsPrintedEndpoint 'inf' } }
Case 'Unrepresentable tiny finite endpoints stay unavailable' { Reject { Get-Mt5NtsPrintedEndpoint '1.000e-100' } }
Case 'Unrepresentable large finite endpoints stay unavailable' { Reject { Get-Mt5NtsPrintedEndpoint '1.000e+100' } }
Case 'Representable large and small endpoint enclosures remain exact' {
    Check ((Get-Mt5NtsPrintedEndpoint '1.234e+20').formatting_upper_seconds -eq [decimal]'123500000000000000000')
    Check ((Get-Mt5NtsPrintedEndpoint '1.234e-20').formatting_lower_seconds -eq [decimal]'0.00000000000000000001233')
}
Case 'Unsupported zero exponent and nonnormalized mantissa are rejected' { Reject { Get-Mt5NtsPrintedEndpoint '0.000e-03' }; Reject { Get-Mt5NtsPrintedEndpoint '0.123e+00' } }
Case 'Reversed lower and upper endpoints cannot create an interval' {
    $f = Fixture; $f.evidence.assessment.selected_row.lower_original = '2.000e+00'
    Check ((Audit $f).diagnostics -contains 'reversed_printed_interval')
}
Case 'Full state sequence retains later stale state without invalidating selected event' {
    $a = Audit (Fixture)
    Check (($a.selection_state_sequence -join ',') -ceq 'M,M,*,S' -and $a.later_states[0] -ceq 'S' -and $a.selected_source_authenticated)
}
Case 'Wrong source profile remains unreviewed' { $f = Fixture; $f.report.reference_server = 'other.invalid'; Check ((Audit $f).diagnostics -contains 'selection_semantics_profile_unreviewed') }
Case 'Wrong session binding prevents semantic promotion' { $f = Fixture; $f.report.capture.session = 'b' * 64; Check ((Audit $f).diagnostics -contains 'selection_semantics_session_mismatch') }
Case 'Missing authenticated selection cannot produce formatting-aware conclusions' {
    $f = Fixture; $f.evidence.assessment.selected_source_authenticated = $false
    Check ($null -eq (Audit $f).formatting_aware_interval)
}
Case 'Unverified input integrity prevents derivation' { $f = Fixture; $f.evidence.status = 'data_unavailable'; Check ((Audit $f).diagnostics -contains 'selection_evidence_integrity_unverified') }
Case 'Cached selection epoch is distinct from the printed second and newest sample age' {
    $a = Audit (Fixture)
    Check ($a.epoch.evaluation -ceq 'cached_cooked_scheduler_event_time' -and $null -eq $a.epoch.exact_subsecond_epoch)
    Check ($a.epoch.displayed_sample_age -ceq 'newest_sample_age_not_necessarily_best_sample_age')
}
Case 'Packet estimate and one-shot output retain different sign and estimator roles' {
    $a = Audit (Fixture)
    Check ($a.sign_interpretation.NTP_sample_offset -ceq 'reference_minus_local_before_SST_negation' -and -not $a.sign_interpretation.Q_and_selection_use_identical_estimator)
}
Case 'Windows and Linux counter domains cannot be subtracted or used for timestamp correction' {
    $a = Audit (Fixture)
    Check (-not $a.clock_domain.cross_domain_counter_subtraction_permitted -and -not $a.timestamp_correction_authorized -and -not $a.temporal_propagation_authorized)
}
Case 'Linux authentication remains true with absolute uncertainty unresolved' {
    $a = Audit (Fixture)
    Check ($a.nts_authenticated_measurement_observed -and $a.overall_utc_uncertainty -ceq 'unresolved')
    Check ($null -eq $a.windows_host_utc_error_bound_seconds -and $null -eq $a.mt5_event_utc_interval)
}
Case 'Canonical flags and historical provider requirements remain explicit' {
    $a = Audit (Fixture)
    Check (-not $a.windows_clock_authenticated -and -not $a.mt5_native_utc_authenticated -and -not $a.canonical_utc_conversion_authorized -and -not $a.utc_conversion_authenticated -and -not $a.canonical_ready)
    Check ($a.unresolved -contains 'provider_historical_timezone_DST_closure_and_availability')
}
Case 'Same validated inputs yield deterministic output' {
    $f = Fixture
    Check (((Audit $f) | ConvertTo-Json -Depth 15 -Compress) -ceq ((Audit $f) | ConvertTo-Json -Depth 15 -Compress))
}
Case 'PowerShell entry points parse and contain no capture or clock commands' {
    foreach ($file in @('Mt5NtsSelectionSemantics.ps1','Get-Mt5NtsSelectionSemantics.ps1')) {
        $errors = $null; $tokens = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $tools $file),[ref]$tokens,[ref]$errors)
        Check ($errors.Count -eq 0)
        $commands = $ast.FindAll({param($n) $n -is [Management.Automation.Language.CommandAst]},$true) | ForEach-Object { $_.GetCommandName() }
        Check (@($commands | Where-Object { $_ -match '^(wsl|Start-Process|Invoke-Mt5NtsLiveCapture|Invoke-Mt5NtsWslCommand|Set-Date|chronyd)$' }).Count -eq 0)
    }
}
$testFolder = Join-Path $env:TEMP ('moneyway-selection-semantics-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder | Out-Null
try {
    Case 'Normal CLI verifies original source and log hashes and preserves inputs and checkpoint sentinel' {
        $f = Publish-Fixture $testFolder
        $checkpoint = Join-Path $testFolder 'checkpoint.json'
        [IO.File]::WriteAllText($checkpoint,'synthetic-checkpoint')
        $paths = @($f.path,($f.path + '.selection.log'),($f.path + '.selection.json'),$checkpoint)
        $before = @($paths | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        $outputPath = Join-Path $testFolder 'semantics.json'
        $json = & (Join-Path $tools 'Get-Mt5NtsSelectionSemantics.ps1') -CorrelationPath $f.path -ExpectedCorrelationSha256 $f.hash -ExpectedSelectionLogSha256 $f.log_hash -ReportPath $outputPath
        $summary = ($json -join "`n") | ConvertFrom-Json
        Check ($summary.InputIntegrityStatus -ceq 'passed' -and $summary.Status -ceq 'context_specific')
        Reject { & (Join-Path $tools 'Get-Mt5NtsSelectionSemantics.ps1') -CorrelationPath $f.path -ExpectedCorrelationSha256 $f.hash -ExpectedSelectionLogSha256 $f.log_hash -ReportPath $outputPath }
        $after = @($paths | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        Check (($before -join ',') -ceq ($after -join ','))
    }
    Case 'Independent wrong log hash blocks CLI semantics' {
        $f = Publish-Fixture $testFolder
        $json = & (Join-Path $tools 'Get-Mt5NtsSelectionSemantics.ps1') -CorrelationPath $f.path -ExpectedCorrelationSha256 $f.hash -ExpectedSelectionLogSha256 ('0' * 64) -ReportPath (Join-Path $testFolder 'wrong-log.json')
        $summary = ($json -join "`n") | ConvertFrom-Json
        Check ($summary.InputIntegrityStatus -ceq 'data_unavailable' -and $null -eq $summary.PrintedInterval)
    }
    Case 'Tampered original log is rejected through the normal CLI' {
        $f = Publish-Fixture $testFolder
        [IO.File]::AppendAllText(($f.path + '.selection.log'),'tampered')
        $json = & (Join-Path $tools 'Get-Mt5NtsSelectionSemantics.ps1') -CorrelationPath $f.path -ExpectedCorrelationSha256 $f.hash -ExpectedSelectionLogSha256 $f.log_hash -ReportPath (Join-Path $testFolder 'tampered-log.json')
        Check (((($json -join "`n") | ConvertFrom-Json).InputIntegrityStatus) -ceq 'data_unavailable')
    }
    Case 'Wrong original report hash blocks CLI semantics' {
        $f = Publish-Fixture $testFolder
        $json = & (Join-Path $tools 'Get-Mt5NtsSelectionSemantics.ps1') -CorrelationPath $f.path -ExpectedCorrelationSha256 ('0' * 64) -ExpectedSelectionLogSha256 $f.log_hash -ReportPath (Join-Path $testFolder 'wrong-report.json')
        Check (((($json -join "`n") | ConvertFrom-Json).InputIntegrityStatus) -ceq 'data_unavailable')
    }
} finally {
    foreach ($file in Get-ChildItem -LiteralPath $testFolder -File) { Remove-Item -LiteralPath $file.FullName }
    Remove-Item -LiteralPath $testFolder
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { exit 1 }
