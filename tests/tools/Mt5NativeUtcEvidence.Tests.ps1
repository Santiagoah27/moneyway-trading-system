# Synthetic, offline assessment tests. Live evidence is exercised separately.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../tools/market-data/mt5/Mt5NativeUtcEvidence.ps1')
. (Join-Path $PSScriptRoot 'Mt5Probe.SyntheticFixtures.ps1')
$passed = 0
$failed = 0
$directory = Join-Path ([IO.Path]::GetTempPath()) ('moneyway-time-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($directory) | Out-Null
function Check([bool]$Value) { if (-not $Value) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Test) {
    try { & $Test; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Probe([int]$Offset = 7200) {
    $probe = Fixture
    $probe.source.company = 'OANDA Global Markets Limited'
    $probe.server_quote_before = Native '2026.01.05 12:00:00'
    $probe.server_quote_after = Native '2026.01.05 12:00:00'
    $probe.generated_gmt_candidate = Native (([datetime]'2026-01-05T12:00:00').AddSeconds(-$Offset).ToString('yyyy.MM.dd HH:mm:ss'))
    $probe | ConvertTo-Json -Depth 24 | ConvertFrom-Json
}
function HostEvidence {
    [pscustomobject]@{ utc_before_candidate = '2026-11-13T17:00:00Z'; utc_after_candidate = '2026-11-13T17:00:02Z'
        monotonic_started = 10; monotonic_finished = 20
        reference = [pscustomobject]@{ status = 'data_unavailable'; reference_authentication = 'unverified' } }
}
$policy = Get-Mt5PolicyEvidence 'https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm' `
    '<p>OANDA_Global-Demo-1 GMT+2 Standard Time or GMT+3 Daylight Savings Time. 5 PM EST</p>' '2026-11-13T17:00:00Z' ('a' * 64)
$fileTime = ([datetime]::Parse('2026-11-13T17:00:00Z').ToUniversalTime()).ToFileTimeUtc()
$raw = "RdtscStart, RdtscEnd, FileTime, RoundtripDelay, NtpOffset`n10, 20, $fileTime, +00.1000000, -00.7500000`n"
try {
    Case 'MT5 official scope and retrieval provenance are retained' {
        Check ($policy.published_offset_pair_present -and $policy.mt5_demo_scope_identified -and $policy.literal_est_wording_present)
        Check ($policy.input_sha256 -ceq ('a' * 64) -and $policy.historical_effective_date -eq $null)
    }
    Case 'Mobile statement remains in its product scope' {
        $p = Get-Mt5PolicyEvidence 'https://help.oanda.com/bvi/es/faqs/oanda-mobile-user-guide-bvi.htm' 'GMT+2 GMT+3' '2026-11-13T17:00:00Z' ('b' * 64)
        Check ($p.published_offset_pair_present -and -not $p.mt5_demo_scope_identified -and $p.product_scope -match 'live_account')
    }
    Case 'Missing policy pair is unresolved' {
        $p = Get-Mt5PolicyEvidence $policy.url 'No published offset here' '2026-11-13T17:00:00Z' ('c' * 64)
        Check ($p.classification -eq 'unresolved' -and $null -eq $p.standard_seconds)
    }
    Case 'Unapproved policy URL is rejected' {
        $reject = $false; try { Get-Mt5PolicyEvidence 'https://example.test/' 'GMT+2 GMT+3' '' '' | Out-Null } catch { $reject = $true }; Check $reject
    }
    Case 'Native quote and M1 display/raw coordinates remain unchanged' {
        $probe = Probe; $a = Get-Mt5NativeUtcAssessment $probe (HostEvidence) @($policy)
        Check ($a.broker_native.quote_before.raw -eq $probe.server_quote_before.raw -and $a.broker_native.us100_m1_head.display -eq '2026.01.05 12:00:00')
    }
    foreach ($field in @('company','server','demo_verified')) {
        Case "Incorrect source $field is rejected" {
            $probe = Probe
            if ($field -eq 'demo_verified') { $probe.source.$field = $false } else { $probe.source.$field = 'wrong' }
            $reject = $false; try { Get-Mt5NativeUtcAssessment $probe (HostEvidence) @($policy) | Out-Null } catch { $reject = $true }; Check $reject
        }
    }
    Case 'Wrong symbol is rejected' {
        $p = Probe; $p.symbol.native = 'USTEC'; $reject = $false
        try { Get-Mt5NativeUtcAssessment $p (HostEvidence) @($policy) | Out-Null } catch { $reject = $true }; Check $reject
    }
    Case 'Unchanged quote cannot prove freshness' {
        $a = Get-Mt5NativeUtcAssessment (Probe) (HostEvidence) @($policy)
        Check (-not $a.broker_native.quote_changed_during_probe -and $a.broker_native.quote_freshness -eq 'unverified')
        Check ($a.broker_native.quote_source -match 'any_selected_symbol' -and $a.broker_native.tick_symbol_binding -eq 'unresolved')
    }
    Case 'Host UTC readings and monotonic ordering are retained as candidates' {
        $a = Get-Mt5NativeUtcAssessment (Probe) (HostEvidence) @($policy)
        Check ($a.host_utc_reading_present -and $a.host_capture_order_valid -and $a.host_synchronization -eq 'unverified')
    }
    Case 'Missing host reading stays unavailable' {
        $hostData = HostEvidence; $hostData.utc_before_candidate = $null
        $a = Get-Mt5NativeUtcAssessment (Probe) $hostData @($policy); Check (-not $a.host_utc_reading_present -and -not $a.host_capture_order_valid)
    }
    Case 'Clock regression invalidates capture ordering' {
        $hostData = HostEvidence; $hostData.utc_after_candidate = '2026-11-13T16:00:00Z'; $hostData.monotonic_finished = 9
        Check (-not (Get-Mt5NativeUtcAssessment (Probe) $hostData @($policy)).host_capture_order_valid)
    }
    Case 'External offset and measured roundtrip parse exactly' {
        $r = Get-Mt5StripchartEvidence $raw 0 $false 'time.example.test'
        Check ($r.status -eq 'context_specific' -and $r.samples.Count -eq 1)
        Check ($r.samples[0].reported_ntp_offset_seconds -eq [decimal]'-0.75' -and $r.samples[0].reported_roundtrip_seconds -eq [decimal]'0.1')
        Check ($r.reference_authentication -eq 'unverified' -and $r.offset_sign_application -eq 'not_applied')
    }
    Case 'Timeout never claims independent reference success' {
        Check ((Get-Mt5StripchartEvidence $raw 0 $true 'time.example.test').status -eq 'data_unavailable')
    }
    Case 'Failed or malformed reference remains unavailable' {
        Check ((Get-Mt5StripchartEvidence 'error' 5 $false 'time.example.test').status -eq 'data_unavailable')
        Check ((Get-Mt5StripchartEvidence 'malformed' 0 $false 'time.example.test').samples.Count -eq 0)
    }
    Case 'Reversed external sample counters are rejected' {
        Check ((Get-Mt5StripchartEvidence "20, 10, $fileTime, 0.1, 0.2" 0 $false 'time.example.test').samples.Count -eq 0)
    }
    Case 'Standard plus two is candidate only' {
        $a = Get-Mt5NativeUtcAssessment (Probe 7200) (HostEvidence) @($policy)
        Check ($a.classification -eq 'candidate' -and $a.exact_published_offset_candidates_matched -contains 7200 -and -not $a.canonical_utc_conversion_authorized)
    }
    Case 'Daylight plus three is candidate only' {
        $a = Get-Mt5NativeUtcAssessment (Probe 10800) (HostEvidence) @($policy)
        Check ($a.classification -eq 'candidate' -and $a.exact_published_offset_candidates_matched -contains 10800 -and $a.coverage.provider_dst_transitions -eq 'unresolved')
    }
    Case '7199 seconds is preserved without rounding or tolerance' {
        $a = Get-Mt5NativeUtcAssessment (Probe 7199) (HostEvidence) @($policy)
        Check ($a.observed_native_minus_computer_gmt_seconds_before -eq 7199 -and $a.classification -eq 'unresolved' -and $a.exact_published_offset_candidates_matched.Count -eq 0)
    }
    Case 'Unobserved DST, ambiguity and historical applicability remain unresolved' {
        $a = Get-Mt5NativeUtcAssessment (Probe) (HostEvidence) @($policy)
        Check ($a.coverage.historical_applicability -eq 'unresolved' -and $a.coverage.ambiguous_or_nonexistent_times -eq 'unresolved')
        Check ($a.strategy_timezone -eq 'America/Bogota' -and -not $a.competing_hour_hypotheses_independently_distinguishable)
    }
    Case 'Mobile-only or missing policy cannot authorize demo policy selection' {
        $p = Get-Mt5PolicyEvidence 'https://help.oanda.com/bvi/es/faqs/oanda-mobile-user-guide-bvi.htm' 'GMT+2 GMT+3' '' ('a' * 64)
        Check ((Get-Mt5NativeUtcAssessment (Probe) (HostEvidence) @($p)).classification -eq 'unresolved')
        Check ((Get-Mt5NativeUtcAssessment (Probe) (HostEvidence) @()).classification -eq 'unresolved')
    }
    Case 'Invalid native representation is rejected' {
        $p = Probe; $p.server_quote_before.raw++; $reject = $false
        try { Get-Mt5NativeUtcAssessment $p (HostEvidence) @($policy) | Out-Null } catch { $reject = $true }; Check $reject
    }
    Case 'Current external reference does not authenticate old broker timestamps' {
        $h = HostEvidence; $h.reference = Get-Mt5StripchartEvidence $raw 0 $false 'time.example.test'
        $a = Get-Mt5NativeUtcAssessment (Probe) $h @($policy)
        Check ($a.broker_to_independent_utc_relationship -match '^insufficient_' -and -not $a.utc_conversion_authenticated)
        Check (-not $a.canonical_ready -and $a.diagnostic_only)
    }
    Case 'Create-only writing preserves prior output' {
        $path = Join-Path $directory 'immutable.json'; Write-Mt5EvidenceBytes $path ([byte[]]@(1,2,3))
        $reject = $false; try { Write-Mt5EvidenceBytes $path ([byte[]]@(4)) } catch { $reject = $true }
        Check ($reject -and [IO.File]::ReadAllBytes($path).Length -eq 3)
    }
    Case 'Partial or missing source artifacts fail before publishing' {
        $tool = Join-Path $PSScriptRoot '../../tools/market-data/mt5/New-Mt5NativeUtcEvidence.ps1'
        $path = Join-Path $directory 'partial.json'; [IO.File]::WriteAllText($path, '{')
        foreach ($inputFile in @($path, (Join-Path $directory 'missing.json'))) {
            $report = Join-Path $directory ('report-' + [guid]::NewGuid().ToString('N') + '.json')
            $reject = $false; try { & $tool -ProbePath $inputFile -ReferenceHost time.example.test -ReportPath $report | Out-Null } catch { $reject = $true }
            Check ($reject -and -not (Test-Path -LiteralPath $report))
        }
    }
    Case 'Tool has bounded read-only Windows time calls and no broker execution' {
        $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../tools/market-data/mt5/New-Mt5NativeUtcEvidence.ps1'))
        Check ($text -notmatch '/resync|/config|/register|/unregister|StrategyReplayContext|OrderSend|\.Commit\(')
        Check ($text -match '/samples:\$ReferenceSamples' -and $text -match '/rdtsc' -and $text -match 'Test-Mt5CollectorRecord.ps1')
    }
    Case 'Existing checkpoint-shaped output and source files are preserved' {
        $tool = Join-Path $PSScriptRoot '../../tools/market-data/mt5/New-Mt5NativeUtcEvidence.ps1'
        $inputFile = Join-Path $directory 'source.json'; [IO.File]::WriteAllText($inputFile, ((Probe) | ConvertTo-Json -Depth 24))
        $outputFile = Join-Path ([IO.Path]::GetTempPath()) ('moneyway-existing-checkpoint-' + [guid]::NewGuid().ToString('N') + '.json')
        [IO.File]::WriteAllText($outputFile, '{"schema":"existing-checkpoint"}')
        $before = (Get-FileHash -LiteralPath $inputFile).Hash
        try {
            $reject = $false; try { & $tool -ProbePath $inputFile -ReportPath $outputFile -ReferenceHost time.example.test | Out-Null } catch { $reject = $true }
            Check ($reject -and [IO.File]::ReadAllText($outputFile) -ceq '{"schema":"existing-checkpoint"}')
            Check ($before -ceq (Get-FileHash -LiteralPath $inputFile).Hash)
        } finally { Remove-Item -LiteralPath $outputFile }
    }
} finally {
    # Only files created in this explicitly owned synthetic directory; no recursion.
    Get-ChildItem -LiteralPath $directory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Remove-Item -LiteralPath $directory
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'MT5 native UTC evidence checks failed.' }
