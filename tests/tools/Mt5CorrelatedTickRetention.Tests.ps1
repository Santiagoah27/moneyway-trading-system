# Offline synthetic bundles only. Authentic evidence is admitted separately from local originals.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
$importer = Join-Path $tools 'Import-Mt5CorrelatedTickEvidence.ps1'
. (Join-Path $tools 'Mt5CorrelatedTickEvidence.ps1')
. (Join-Path $PSScriptRoot 'Mt5Probe.SyntheticFixtures.ps1')
$passed = 0; $failed = 0
$workspace = Join-Path $env:TEMP ('moneyway-retention-tests-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($workspace) | Out-Null
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Reject([scriptblock]$Action) {
    $caught = $false
    try { & $Action | Out-Null } catch { $caught = $true }
    Check $caught
}
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function SaveJson([string]$Path, $Data) {
    [IO.File]::WriteAllText($Path, (($Data | ConvertTo-Json -Depth 20) + "`n"), (New-Object Text.UTF8Encoding($false)))
}
function SaveText([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}
function Stamp([string]$Value, [long]$Counter) { [pscustomobject]@{ utc_candidate = $Value; monotonic = $Counter } }
function Reference([string]$Utc, [string]$Offset) {
    $ft = [datetimeoffset]::Parse($Utc).UtcDateTime.ToFileTimeUtc()
    $raw = "synthetic reference`n1, 2, $ft, +00.1234567, $Offset"
    [pscustomobject]@{ query = [pscustomobject]@{ arguments = '/stripchart /computer:synthetic.test /samples:1 /period:1 /rdtsc /dataonly'
            raw = $raw; exit_code = 0; timed_out = $false; utc_before_candidate = $Utc; utc_after_candidate = $Utc }
        parsed = Get-Mt5StripchartEvidence $raw 0 $false 'synthetic.test' }
}
function Tick([int]$Millisecond) {
    $time = Native '2026.01.05 12:00:10'
    [pscustomobject]@{ time = $time; time_msc = [long]$time.raw * 1000 + $Millisecond
        bid = '1.0012345678901234e+02'; ask = '1.0212345678901234e+02'; last = '0.0000000000000000e+00'
        volume = '0'; flags = 6; volume_real = '0.0000000000000000e+00' }
}
function NewFixture {
    $caseRoot = Join-Path $workspace ([guid]::NewGuid().ToString('N'))
    $nonce = 'a' * 64
    $source = Join-Path $caseRoot ('source\' + $nonce)
    $root = Join-Path $caseRoot 'retained'
    [IO.Directory]::CreateDirectory($source) | Out-Null
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $identity = [pscustomobject]@{ company = 'OANDA Global Markets Limited'; server = 'OANDA_Global-Demo-1'; symbol = 'US100'
        demo = $true; connected = $true; algo_trading_enabled = $false }
    SaveText (Join-Path $source 'offer.txt') "moneyway.mt5-correlated-tick-offer/v1`n$nonce`n300`nUS100`n"
    SaveText (Join-Path $source 'request.txt') "moneyway.mt5-correlated-tick-request/v1`n$nonce`n100-200-300`n200000`nUS100`n"
    SaveText (Join-Path $source 'cancel.txt') "$nonce`n"
    $ready = [pscustomobject]@{ schema = 'moneyway.mt5-correlated-tick-ready'; version = 1; nonce = $nonce
        ready_token = '100-200-300'; source = $identity; offer_sha256 = (Read-Mt5TickFile (Join-Path $source 'offer.txt')).sha256 }
    SaveJson (Join-Path $source 'ready.json') $ready
    $context = [pscustomobject]@{ time_current = Native '2026.01.05 12:00:10'; time_trade_server = Native '2026.01.05 12:00:10'
        time_gmt = Native '2026.01.05 10:00:10'; time_local = Native '2026.01.05 05:00:10'; m1_head = Native '2026.01.05 12:00:00'; m1_error = 0 }
    SaveJson (Join-Path $source 'context.json') $context
    $response = [pscustomobject]@{ schema = 'moneyway.mt5-correlated-tick-response'; version = 1; nonce = $nonce
        ready_token = $ready.ready_token; request_sha256 = (Read-Mt5TickFile (Join-Path $source 'request.txt')).sha256
        context_sha256 = (Read-Mt5TickFile (Join-Path $source 'context.json')).sha256; source = $identity
        outcome = 'captured'; reason = 'subsequent_ontick_timestamp_progressed'; event = 'OnTick'
        started_ms = 100; armed_ms = 200; handler_ms = 210; finished_ms = 220; armed_callback_count = 23; callback_count = 25
        cached_callbacks = 1; baseline = Tick 359; tick = Tick 417; tick_error = 0
        lookup_event_identity = 'unresolved_queued_event_and_latest_lookup'; canonical_utc_conversion_authorized = $false }
    SaveJson (Join-Path $source 'response.json') $response
    $bounds = [pscustomobject]@{
        offer_publish_before = Stamp '2026-01-05T10:00:00Z' 1; offer_publish_after = Stamp '2026-01-05T10:00:00.1Z' 2
        request_publish_before = Stamp '2026-01-05T10:00:10.2482979Z' 3; request_publish_after = Stamp '2026-01-05T10:00:10.3057568Z' 4
        receipt_before = Stamp '2026-01-05T10:00:10.5750488Z' 5; receipt_after = Stamp '2026-01-05T10:00:10.6067635Z' 6
        session_ended = Stamp '2026-01-05T10:00:11Z' 7; monotonic_frequency = 10000000 }
    $before = Reference '2026-01-05T10:00:09Z' '+00.0151833'
    $after = Reference '2026-01-05T10:00:12Z' '+00.0002248'
    $toolNames = @('Invoke-Mt5CorrelatedTickCapture.ps1','Mt5CorrelatedTickEvidence.ps1','Mt5NativeUtcEvidence.ps1','MoneyWayCorrelatedTick.mq5','MoneyWayCorrelatedTickDecision.mqh')
    $evidence = [pscustomobject]@{ schema = 'moneyway.mt5-correlated-tick-evidence'; version = 1; nonce = $nonce
        outcome = 'captured'; reason = $response.reason; session_limit_seconds = 300; query_timeout_milliseconds = 5000
        bounds = $bounds; reference_before = $before; reference_after = $after
        source_query = [pscustomobject]@{ raw = 'synthetic access denied'; exit_code = 5 }
        status_query = [pscustomobject]@{ raw = 'Leap Indicator: 3(not synchronized)'; exit_code = 0 }
        response = $response; context = $context; files = @(); assessment = $null
        tool_sources = @($toolNames | ForEach-Object { [pscustomobject]@{ file_name = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $tools $_)).Hash.ToLowerInvariant() } })
        canonical_utc_conversion_authorized = $false }
    $state = [pscustomobject]@{ Source = $source; Root = $root; Nonce = $nonce; Evidence = $evidence; EvidenceHash = ''; ResponseHash = '' }
    Rebind $state
    return $state
}
function SaveEvidence($State) {
    $path = Join-Path $State.Source 'evidence.json'
    SaveJson $path $State.Evidence
    $State.EvidenceHash = (Read-Mt5TickFile $path).sha256
    $State.ResponseHash = (Read-Mt5TickFile (Join-Path $State.Source 'response.json')).sha256
}
function Rebind($State) {
    $State.Evidence.response = (Read-Mt5TickFile (Join-Path $State.Source 'response.json') -Json).data
    $State.Evidence.context = (Read-Mt5TickFile (Join-Path $State.Source 'context.json') -Json).data
    $State.Evidence.files = @(@('cancel.txt','context.json','offer.txt','ready.json','request.txt','response.json') | ForEach-Object {
        Read-Mt5TickFile (Join-Path $State.Source $_) -Raw | Select-Object file_name,sha256
    })
    $State.Evidence.assessment = Get-Mt5TickAssessment $State.Evidence.response $State.Evidence.context $State.Evidence.bounds $State.Evidence.reference_before.parsed $State.Evidence.reference_after.parsed
    SaveEvidence $State
}
function Import($State) {
    & $importer -SourceDirectory $State.Source -RetentionRoot $State.Root -ExpectedEvidenceSHA256 $State.EvidenceHash -ExpectedResponseSHA256 $State.ResponseHash
}
try {
    Case 'Authentic-format synthetic bundle validates full lineage and retains exact source bytes' {
        $s = NewFixture; $result = Import $s
        Check ($result.Result -ceq 'retained' -and $result.Nonce -ceq $s.Nonce)
        $manifest = (Read-Mt5TickFile (Join-Path $result.RetainedDirectory 'retention.json') -Json).data
        Check ($manifest.verification.raw_ntp_reparsed -and $manifest.verification.assessment_recomputed)
        foreach ($file in $manifest.files) {
            Check ((Read-Mt5TickFile (Join-Path $s.Source $file.file_name) -Raw).sha256 -ceq $file.sha256)
            Check ((Read-Mt5TickFile (Join-Path $result.RetainedDirectory $file.file_name) -Raw).sha256 -ceq $file.sha256)
        }
    }
    Case 'Idempotent import preserves retained manifest bytes and original review time' {
        $s = NewFixture; $first = Import $s; $second = Import $s
        Check ($second.Result -eq 'already_retained' -and $first.RetentionManifestSHA256 -ceq $second.RetentionManifestSHA256)
    }
    Case 'Native progression remains exactly 58 milliseconds with full decimal prices' {
        $s = NewFixture; $r = Import $s
        Check ($r.NativeProgressionMilliseconds -eq 58 -and $r.Assessment.exact_native_tick_minus_computer_gmt_seconds -eq [decimal]'7200.417')
        $retained = (Read-Mt5TickFile (Join-Path $r.RetainedDirectory 'response.json') -Json).data
        Check ($retained.tick.bid -ceq '1.0012345678901234e+02')
    }
    Case 'Seven-digit host timestamp precision and exact interval are preserved' {
        $s = NewFixture; $r = Import $s
        Check ($r.Assessment.uncorrected_host_interval_milliseconds -eq [decimal]'358.4656')
        Check ([IO.File]::ReadAllText((Join-Path $r.RetainedDirectory 'evidence.json')).Contains('10:00:10.2482979Z'))
    }
    Case 'GMT plus two fits while GMT plus three is outside the uncorrected interval' {
        $a = (Import (NewFixture)).Assessment
        Check ($a.policy_hypotheses[0].within_uncorrected_host_observation_interval -and -not $a.policy_hypotheses[1].within_uncorrected_host_observation_interval)
    }
    Case 'Unsynchronized clock, unsigned reference and queued identity remain unresolved' {
        $a = (Import (NewFixture)).Assessment
        Check ($a.host_synchronization -eq 'unverified' -and $a.reference_authentication -eq 'unverified')
        Check (-not $a.source_event_UTC_bounded -and $a.queued_event_to_latest_lookup_identity -eq 'unresolved')
        Check ($a.historical_mapping -eq 'unresolved' -and $a.provider_dst_transitions -eq 'unresolved' -and -not $a.canonical_ready)
    }
    Case 'Incorrect session nonce is rejected' {
        $s = NewFixture; $s.Evidence.nonce = 'b' * 64; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Incorrect source identity is rejected through existing validator' {
        $s = NewFixture; $r = $s.Evidence.response; $r.source.server = 'wrong'; SaveJson (Join-Path $s.Source 'response.json') $r; Rebind $s
        Reject { Import $s }
    }
    Case 'Tampered standalone response fails the independently supplied hash pin' {
        $s = NewFixture; [IO.File]::AppendAllText((Join-Path $s.Source 'response.json'), ' ')
        Reject { Import $s }
    }
    Case 'Tampered evidence fails the independently supplied hash pin' {
        $s = NewFixture; [IO.File]::AppendAllText((Join-Path $s.Source 'evidence.json'), ' ')
        Reject { Import $s }
    }
    Case 'Source manifest mismatch fails even when the outer evidence hash is updated' {
        $s = NewFixture; $s.Evidence.files[0].sha256 = '0' * 64; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Duplicate manifest entries cannot hide a missing file' {
        $s = NewFixture; $s.Evidence.files[0] = $s.Evidence.files[1]; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Missing diagnostic file prevents admission and publication' {
        $s = NewFixture; Remove-Item -LiteralPath (Join-Path $s.Source 'context.json')
        Reject { Import $s }; Check (@(Get-ChildItem -LiteralPath $s.Root -Force).Count -eq 0)
    }
    Case 'Partial JSON bundle prevents admission' {
        $s = NewFixture; SaveText (Join-Path $s.Source 'response.json') '{'
        Reject { Import $s }
    }
    Case 'Orphan pending file prevents admission' {
        $s = NewFixture; SaveText (Join-Path $s.Source 'response.json.pending') 'partial'
        Reject { Import $s }
    }
    Case 'Ready token mismatch prevents admission' {
        $s = NewFixture; $r = $s.Evidence.response; $r.ready_token = '1-2-3'; SaveJson (Join-Path $s.Source 'response.json') $r; Rebind $s
        Reject { Import $s }
    }
    Case 'Context binding mismatch prevents admission' {
        $s = NewFixture; $r = $s.Evidence.response; $r.context_sha256 = '0' * 64; SaveJson (Join-Path $s.Source 'response.json') $r; Rebind $s
        Reject { Import $s }
    }
    Case 'Embedded response content mismatch is rejected' {
        $s = NewFixture; $s.Evidence.response.tick.flags = 123; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Semantic object equality accepts property reordering without recursion in PowerShell 5.1' {
        $s = NewFixture
        $properties = @($s.Evidence.response.PSObject.Properties | Sort-Object Name -Descending)
        $reordered = [ordered]@{}
        foreach ($property in $properties) { $reordered[$property.Name] = $property.Value }
        $s.Evidence.response = [pscustomobject]$reordered; SaveEvidence $s
        Check ((Import $s).Result -eq 'retained')
    }
    Case 'Host UTC order conflict is rejected' {
        $s = NewFixture; $s.Evidence.bounds.request_publish_after.utc_candidate = '2026-01-05T10:00:20Z'; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Host monotonic order conflict is rejected' {
        $s = NewFixture; $s.Evidence.bounds.receipt_after.monotonic = 1; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Raw NTP mismatch is rejected instead of inheriting the reported assessment' {
        $s = NewFixture; $s.Evidence.reference_before.parsed.samples[0].reported_ntp_offset_seconds = [decimal]'0.9'; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'An external measurement following the request cannot be labeled before' {
        $s = NewFixture; $s.Evidence.reference_before.query.utc_after_candidate = '2026-01-05T10:00:15Z'; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Tampered candidate assessment cannot authorize conversion' {
        $s = NewFixture; $s.Evidence.assessment.canonical_utc_conversion_authorized = $true; SaveEvidence $s
        Reject { Import $s }
    }
    Case 'Mismatching recorded tool hash remains an explicit provenance limitation' {
        $s = NewFixture; $s.Evidence.tool_sources[0].sha256 = '0' * 64; SaveEvidence $s
        $r = Import $s
        Check (-not $r.ToolProvenance[0].matches_reviewed_repository -and -not $r.CanonicalUtcConversionAuthorized)
    }
    Case 'An empty supported cancellation marker is preserved byte for byte' {
        $s = NewFixture; SaveText (Join-Path $s.Source 'cancel.txt') ''; Rebind $s
        $r = Import $s; Check ((Get-Item -LiteralPath (Join-Path $r.RetainedDirectory 'cancel.txt')).Length -eq 0)
    }
    Case 'Conflicting retained evidence blocks duplicate import without replacement' {
        $s = NewFixture; $r = Import $s; $path = Join-Path $r.RetainedDirectory 'response.json'
        [IO.File]::AppendAllText($path, ' '); $hash = (Read-Mt5TickFile $path).sha256
        Reject { Import $s }; Check ((Read-Mt5TickFile $path).sha256 -ceq $hash)
    }
    Case 'Tampered retention manifest checksum blocks duplicate import' {
        $s = NewFixture; $r = Import $s; [IO.File]::AppendAllText((Join-Path $r.RetainedDirectory 'retention.json'), ' ')
        Reject { Import $s }
    }
    Case 'Partial retained session blocks import without filling missing files' {
        $s = NewFixture; [IO.Directory]::CreateDirectory((Join-Path $s.Root $s.Nonce)) | Out-Null
        Reject { Import $s }; Check (@(Get-ChildItem -LiteralPath (Join-Path $s.Root $s.Nonce)).Count -eq 0)
    }
    Case 'Conflicting retained capture metadata is rejected even with a recomputed checksum' {
        $s = NewFixture; $r = Import $s
        $path = Join-Path $r.RetainedDirectory 'retention.json'
        $m = (Read-Mt5TickFile $path -Json).data; $m.captured_time_msc++
        SaveJson $path $m
        SaveText (Join-Path $r.RetainedDirectory 'retention.sha256') ((Read-Mt5TickFile $path).sha256 + "`n")
        Reject { Import $s }
    }
    Case 'Retention inside the original source is rejected' {
        $s = NewFixture; $s.Root = $s.Source
        Reject { Import $s }
    }
    Case 'Source-like collector and checkpoint sentinels remain byte-identical' {
        $s = NewFixture; $sentinels = @()
        foreach ($name in @('record-000000000001.json','checkpoint.json')) {
            $path = Join-Path ([IO.Path]::GetDirectoryName($s.Root)) $name
            SaveText $path '{"synthetic":"immutable"}'; $sentinels += $path
        }
        $before = @($sentinels | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        Import $s | Out-Null
        $after = @($sentinels | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        Check (($before -join ',') -ceq ($after -join ','))
    }
    Case 'Admission has no execution, network measurement or canonical ingestion calls' {
        $text = [IO.File]::ReadAllText((Resolve-Path $importer).Path)
        Check ($text -notmatch 'OrderSend|CTrade|StrategyReplayContext|Invoke-Mt5BoundedTimeQuery|Get-Mt5TickReference|checkpoint|MoneyWayAutomaticReadOnly')
        Check ($text -match 'Assert-Mt5TickResponse' -and $text -match 'Get-Mt5TickAssessment' -and $text -match 'FileShare\]::Read')
    }
} finally {
    $resolved = (Resolve-Path -LiteralPath $workspace).Path
    $parent = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'moneyway-retention-tests-*') { throw 'unsafe_test_cleanup_path' }
    Remove-Item -LiteralPath $resolved -Recurse
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'MT5 evidence retention checks failed.' }
