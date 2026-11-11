param([Parameter(Mandatory = $true)][string]$Path)

# Offline diagnostic receipt check, not a .NET ingestion adapter or authenticity proof.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$handles = New-Object System.Collections.Generic.List[System.IDisposable]

function Require([bool]$Value, [string]$Reason) { if (-not $Value) { throw "Invalid collector record: $Reason" } }
function CheckNativeStamp($Value) {
    if ($null -eq $Value) { return }
    Require (@($Value.PSObject.Properties).Count -eq 2 -and $Value.PSObject.Properties.Name -ccontains 'raw' -and
        $Value.PSObject.Properties.Name -ccontains 'display') 'Invalid native timestamp shape.'
    Require (($Value.raw -is [int] -or $Value.raw -is [long]) -and $Value.raw -gt 0) 'Invalid native timestamp integer.'
    $native = [datetime]::ParseExact($Value.display, 'yyyy.MM.dd HH:mm:ss', [cultureinfo]::InvariantCulture)
    $epoch = [datetime]::SpecifyKind([datetime]'1970-01-01',[DateTimeKind]::Unspecified)
    Require (($native-$epoch).TotalSeconds -eq $Value.raw) 'Native timestamp representation mismatch.'
}
function HashLockedFile([string]$FilePath) {
    $stream = [System.IO.File]::Open($FilePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $handles.Add($stream)
    Require ($stream.Length -gt 0 -and $stream.Length -le 2097152) 'File size outside diagnostic bounds.'
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    $stream.Position = 0
    return @{ Stream = $stream; Hash = $hash }
}

try {
    $file = Get-Item -LiteralPath $Path
    Require ($file.Name -cmatch '^record-([0-9]{12})\.json$') 'Unexpected record filename.'
    $filenameSequence = [long]$Matches[1]
    $current = HashLockedFile $file.FullName
    $reader = New-Object System.IO.StreamReader($current.Stream, (New-Object System.Text.UTF8Encoding($false,$true)), $true, 4096, $true)
    try { $data = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $allowed = @('schema','version','diagnostic_only','canonical_ready','utc_conversion_authenticated','sequence','run_id',
        'poll_sequence','event','condition','timer_seconds','retry_every_polls','sample_bars','source','trigger_m1_head_native','previous_committed_m1_head_native',
        'captured_m1_head_native','m1_usable','sample_complete','source_stable','previous_record','previous_record_sha256',
        'probe_file','probe_sha256','shutdown_reason','sequence_basis')
    foreach ($key in $data.PSObject.Properties.Name) { Require ($allowed -ccontains $key) 'Unexpected metadata field.' }
    foreach ($key in $allowed) { Require ($data.PSObject.Properties.Name -ccontains $key) "Missing field: $key" }
    foreach ($key in @('diagnostic_only','canonical_ready','utc_conversion_authenticated','m1_usable','sample_complete','source_stable')) {
        Require ($data.$key -is [bool]) 'Metadata flag must be boolean.'
    }
    foreach ($key in @('version','sequence','poll_sequence','condition','timer_seconds','retry_every_polls','sample_bars')) {
        Require ($data.$key -is [int] -or $data.$key -is [long]) 'Metadata counter must be integer.'
    }
    foreach ($key in @('trigger_m1_head_native','previous_committed_m1_head_native','captured_m1_head_native')) { CheckNativeStamp $data.$key }
    Require ($data.schema -ceq 'moneyway.mt5-read-only-collector' -and $data.version -eq 1) 'Unsupported schema.'
    Require ($data.diagnostic_only -ceq $true -and $data.canonical_ready -ceq $false -and $data.utc_conversion_authenticated -ceq $false) 'Read-only/UTC boundary violated.'
    Require ($data.sequence -eq $filenameSequence -and $data.sequence -gt 0) 'Sequence/filename mismatch.'
    Require ($data.run_id -cmatch '^run-([0-9]{12})$') 'Invalid run identity.'
    Require ([long]$Matches[1] -gt 0 -and [long]$Matches[1] -le $data.sequence) 'Run begins after this record.'
    Require ($data.poll_sequence -gt 0 -and $data.timer_seconds -ge 1 -and $data.timer_seconds -le 60 -and
        $data.retry_every_polls -ge 1 -and $data.retry_every_polls -le 120 -and $data.sample_bars -ge 2 -and $data.sample_bars -le 128) 'Invalid technical settings.'
    Require ($data.sequence_basis -ceq 'local_single_writer_publication_not_utc') 'Unapproved sequence time basis.'
    foreach ($key in $data.source.PSObject.Properties.Name) { Require (@('company','server','demo','symbol') -ccontains $key) 'Unexpected source field.' }
    Require ($data.source.company -ceq 'OANDA Global Markets Limited' -and $data.source.server -ceq 'OANDA_Global-Demo-1' -and
        $data.source.demo -ceq $true -and $data.source.symbol -ceq 'US100') 'Unexpected configured source.'
    $events = @('initial_snapshot','new_native_m1','source_recovery','source_unavailable','native_head_regressed','incomplete_snapshot_retry','shutdown')
    Require ($events -ccontains $data.event) 'Unknown acquisition event.'
    Require ($data.condition -ge 0 -and $data.condition -le 4) 'Unknown condition.'
    if ($data.event -ceq 'new_native_m1') {
        Require ($data.condition -eq 0 -and $null -ne $data.trigger_m1_head_native -and $null -ne $data.previous_committed_m1_head_native -and
            $data.trigger_m1_head_native.raw -gt $data.previous_committed_m1_head_native.raw) 'Timer/duplicate/regressed identity labeled new M1.'
    }
    if ($null -ne $data.previous_record) {
        Require ($data.previous_record -cmatch '^record-([0-9]{12})\.json$') 'Unsafe previous-record reference.'
        Require ([long]$Matches[1] -lt $data.sequence) 'Previous sequence is not strictly earlier.'
        $previous = HashLockedFile (Join-Path $file.DirectoryName $data.previous_record)
        Require ($previous.Hash -ceq $data.previous_record_sha256) 'Previous-record hash mismatch.'
    } else { Require ($null -eq $data.previous_record_sha256) 'Hash without a previous record.' }
    $invalid = 0; $bars = 0
    if ($data.event -ceq 'shutdown') {
        Require ($null -eq $data.probe_file -and $null -eq $data.probe_sha256 -and $null -ne $data.shutdown_reason -and
            -not $data.m1_usable -and -not $data.sample_complete -and -not $data.source_stable -and $null -eq $data.captured_m1_head_native) 'Shutdown contains a fabricated acquisition.'
    } else {
        $expectedProbe = 'probe-' + $filenameSequence.ToString('D12') + '.json'
        Require ($data.probe_file -ceq $expectedProbe -and $null -eq $data.shutdown_reason) 'Unsafe/mismatched probe reference.'
        $probePath = Join-Path $file.DirectoryName $expectedProbe
        $probe = HashLockedFile $probePath
        Require ($probe.Hash -ceq $data.probe_sha256) 'Probe hash mismatch.'
        $validation = & (Join-Path $PSScriptRoot 'Test-Mt5ProbeArtifact.ps1') -Path $probePath
        $bars = $validation.Bars; $invalid = $validation.InvalidOhlcBars
        $reader = New-Object System.IO.StreamReader($probe.Stream, (New-Object System.Text.UTF8Encoding($false,$true)), $true, 4096, $true)
        try { $snapshot = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($data.source_stable) {
            Require ($snapshot.source.server -ceq $data.source.server -and $snapshot.source.company -ceq $data.source.company -and
                $snapshot.source.demo_verified -ceq $true) 'Stable source disagrees with probe.'
        }
        $m1 = @($snapshot.timeframes | Where-Object timeframe -CEQ 'M1')[0]
        $actualHead = if ($m1.bars.Count -gt 0) { $m1.bars[0].open_time_native.raw } else { $null }
        if ($null -eq $actualHead) { Require ($null -eq $data.captured_m1_head_native) 'Fabricated captured M1 identity.' }
        else { Require ($null -ne $data.captured_m1_head_native -and $data.captured_m1_head_native.raw -eq $actualHead) 'Captured M1 identity mismatch.' }
        if ($data.m1_usable) {
            Require ($m1.status -cne 'not_attempted' -and $m1.copy_return -gt 0 -and $m1.copy_error_code -eq 0 -and
                $m1.head_consistent -ceq $true -and $m1.synchronized_after.value -eq 1 -and
                @($m1.bars | Where-Object { -not $_.ohlc_valid }).Count -eq 0) 'Unusable M1 labeled usable.'
        }
        if ($data.sample_complete) {
            Require ($data.m1_usable -and $invalid -eq 0 -and $bars -eq 4*$data.sample_bars) 'Incomplete/invalid payload labeled complete.'
            foreach ($frame in $snapshot.timeframes) {
                Require ($frame.status -ceq 'sample_returned' -and $frame.copy_return -eq $data.sample_bars -and
                    $frame.copy_error_code -eq 0 -and $frame.head_consistent -ceq $true -and $frame.synchronized_after.value -eq 1) 'Incomplete timeframe labeled complete.'
            }
        }
    }
    [pscustomobject]@{ ContractValid=$true; DiagnosticOnly=$true; CanonicalReady=$false; RecordSHA256=$current.Hash
        Sequence=$data.sequence; RunId=$data.run_id; Event=$data.event; ProbeBars=$bars; InvalidOhlcBars=$invalid
        SourceStable=$data.source_stable; Complete=$data.sample_complete; AuthenticityAndUtcVerified=$false }
} finally { foreach ($handle in $handles) { $handle.Dispose() } }
