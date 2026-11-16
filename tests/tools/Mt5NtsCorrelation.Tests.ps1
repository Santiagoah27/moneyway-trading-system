# Synthetic protocol/authentication/timing cases. No network, WSL or real NTS query in this suite.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5NtsCorrelation.ps1')
$passed = 0; $failed = 0
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
function Reject([scriptblock]$Body) { $caught = $false; try { & $Body | Out-Null } catch { $caught = $true }; Check $caught }
$sid = 'a' * 64
function HostStamp([int]$Milliseconds) {
    [pscustomobject]@{ utc_candidate = ([datetimeoffset]'2026-11-16T20:00:00Z').AddMilliseconds($Milliseconds).ToString('o')
        stopwatch_ticks = [long](1000000 + $Milliseconds * 1000); stopwatch_frequency = 1000000
        monotonic_domain = 'windows_stopwatch'; realtime_domain = 'unix_utc_candidate' }
}
function Fixture {
    $dir = '/tmp/mw-nts-' + $sid
    $cfg = "server time.cloudflare.com nts iburst`nauthselectmode require`nport 0`ncmdport 0`nbindcmdaddress $dir/chronyd.sock`npidfile $dir/chronyd.pid`n"
    $utc = Get-Mt5NtsUnixCandidate (HostStamp 100).utc_candidate
    [pscustomobject]@{ session = $sid; error = $null; exit_code = 0
        initialization_before = HostStamp -500; initialization_after = HostStamp -1
        launch = HostStamp 0; ready = HostStamp 90; completion = HostStamp 6200
        before = [pscustomobject]@{ sent = HostStamp 100; received = HostStamp 110
            linux = [pscustomobject]@{ monotonic_before = '25.00'; monotonic_after = '25.01'; utc_unix_seconds = ($utc + [decimal]'0.005').ToString([Globalization.CultureInfo]::InvariantCulture)
                monotonic_domain = 'linux_proc_uptime_boottime'; realtime_domain = 'unix_utc_candidate' } }
        after = [pscustomobject]@{ sent = HostStamp 6100; received = HostStamp 6110
            linux = [pscustomobject]@{ monotonic_before = '31.00'; monotonic_after = '31.01'; utc_unix_seconds = ($utc + [decimal]'6.005').ToString([Globalization.CultureInfo]::InvariantCulture)
                monotonic_domain = 'linux_proc_uptime_boottime'; realtime_domain = 'unix_utc_candidate' } }
        result = [pscustomobject]@{ session = $sid; query_exit_code = 0; configuration = $cfg
            configuration_sha256 = Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($cfg))
            authdata = '192.0.2.1 NTS 1 30 128 3 0 0 8 64'
            ntpdata = "Remote address  : 192.0.2.1 ()`nAuthenticated   : Yes`nTotal good RX   : 2`nOffset          : -0.002948930 seconds`nPeer delay      : 0.007207788 seconds`nPeer dispersion : 0.000014729 seconds`nRoot delay      : 0.091110 seconds`nRoot dispersion : 0.000626 seconds"
            capabilities = "Uid:`t100`t100`t100`t100`nCapEff:`t0000000000000000`nCapBnd:`t0000000000000000`nNoNewPrivs:`t1"
            client_stderr = "chronyd version 4.5 starting (+CMDMON +NTS)`nDisabled control of system clock`nSystem clock wrong by -0.003012 seconds (ignored)"
            client_stdout = '' } }
}
function Assessment($Capture) { Get-Mt5NtsCorrelationAssessment $Capture $sid }
Case 'Exact selected distribution is discovered' { Check (Test-Mt5NtsDistribution "Other`nMoneyWay-NTS-Ubuntu-24.04`n" 'MoneyWay-NTS-Ubuntu-24.04') }
Case 'Missing distribution is rejected' { Check (-not (Test-Mt5NtsDistribution 'Other' 'MoneyWay-NTS-Ubuntu-24.04')) }
Case 'Docker cannot be selected' { Check (-not (Test-Mt5NtsDistribution 'docker-desktop' 'docker-desktop')) }
Case 'Session protocol validates count kind and identifier' { Check ((ConvertFrom-Mt5NtsLine "READY|$sid|hash" READY $sid 3)[2] -ceq 'hash') }
Case 'POSIX protocol writer sends LF without a trailing carriage return' {
    $memory = New-Object IO.MemoryStream
    $writer = New-Object IO.StreamWriter($memory, (New-Object Text.UTF8Encoding($false)))
    try { $writer.NewLine = "`n"; $writer.WriteLine("BEFORE $sid"); $writer.Flush()
        Check ([Text.Encoding]::UTF8.GetString($memory.ToArray()) -ceq "BEFORE $sid`n")
    } finally { $writer.Dispose(); $memory.Dispose() }
    Check ([IO.File]::ReadAllText((Join-Path $tools 'Mt5NtsCorrelation.ps1')).Contains('$process.StandardInput.NewLine = "`n"'))
}
Case 'Invalid correlation ID is rejected' { Reject { ConvertFrom-Mt5NtsLine 'READY|bad|hash' READY bad 3 } }
Case 'Private Unix socket directory leaves room for chronyc reply sockets' {
    Check (('/tmp/mw-nts-' + $sid + '/chronyc.2147483647.sock').Length -lt 108)
    Check ([IO.File]::ReadAllText((Join-Path $tools 'Invoke-Mt5NtsLinuxCapture.sh')).Contains('dir=/tmp/mw-nts-$sid'))
}
Case 'Mismatched protocol session is rejected' { Reject { ConvertFrom-Mt5NtsLine ('READY|' + ('b' * 64) + '|hash') READY $sid 3 } }
Case 'Partial protocol is rejected' { Reject { ConvertFrom-Mt5NtsLine "RESULT|$sid" RESULT $sid 10 } }
Case 'Oversized protocol is rejected' { Reject { ConvertFrom-Mt5NtsLine ('x' * 1048577) READY $sid 3 } }
Case 'Complete current profile authenticates Linux observation' {
    $a = Assessment (Fixture); Check $a.nts_authenticated_measurement_observed; Check $a.nts_ke_observed
    Check ($a.accepted_authenticated_packets -eq 2 -and $a.diagnostics.Count -eq 0)
}
Case 'Unsupported Chrony version does not authenticate' {
    $f = Fixture; $f.result.client_stderr = $f.result.client_stderr.Replace('4.5','4.7'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'NTS key establishment without cookies is rejected' {
    $f = Fixture; $f.result.authdata = '192.0.2.1 NTS 0 30 128 - 1 0 0 0'; Check (-not (Assessment $f).nts_ke_observed)
}
Case 'Symmetric or ordinary NTP fallback is rejected' {
    $f = Fixture; $f.result.authdata = $f.result.authdata.Replace('NTS','SK'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Key establishment without authenticated packet is insufficient' {
    $f = Fixture; $f.result.ntpdata = $f.result.ntpdata.Replace('Yes','No'); $a = Assessment $f; Check $a.nts_ke_observed; Check (-not $a.nts_authenticated_measurement_observed)
}
Case 'No accepted authenticated packets is insufficient' {
    $f = Fixture; $f.result.ntpdata = $f.result.ntpdata.Replace('RX   : 2','RX   : 0'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Remote endpoint must match the NTS row' {
    $f = Fixture; $f.result.authdata = $f.result.authdata.Replace('192.0.2.1','192.0.2.2'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Different server configuration is rejected' {
    $f = Fixture; $f.result.configuration = $f.result.configuration.Replace('time.cloudflare.com','wrong.example'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Stale previous session configuration is rejected' {
    $f = Fixture; $f.result.configuration = $f.result.configuration.Replace($sid,('b' * 64)); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Mismatched result session is rejected' {
    $f = Fixture; $f.result.session = 'b' * 64; Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Client process failure is preserved' {
    $f = Fixture; $f.result.query_exit_code = 1; Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Unexpected transport process termination is rejected' {
    $f = Fixture; $f.exit_code = 9; Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Missing result cannot be promoted from timing observations' {
    $f = Fixture; $f.result = $null; Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Timeout produces unavailable evidence' {
    $f = Fixture; $f.error = 'nts_timeout'; Check ((Assessment $f).diagnostics -contains 'nts_timeout')
}
Case 'Cancellation produces unavailable evidence' {
    $f = Fixture; $f.error = 'nts_cancelled'; Check ((Assessment $f).diagnostics -contains 'nts_cancelled')
}
Case 'Actual task waiting honors cancellation' {
    $cancel = New-Object Threading.CancellationTokenSource; $cancel.Cancel()
    try { Reject { Wait-Mt5NtsTask ([Threading.Tasks.Task]::Delay(100)) ([long]::MaxValue) $cancel.Token } } finally { $cancel.Dispose() }
}
Case 'Actual task waiting enforces deadline' {
    Reject { Wait-Mt5NtsTask ([Threading.Tasks.Task]::Delay(100)) 0 ([Threading.CancellationToken]::None) }
}
Case 'Completed task returns without an unnecessary wait' { Wait-Mt5NtsTask ([Threading.Tasks.Task]::FromResult('done')) ([long]::MaxValue) ([Threading.CancellationToken]::None) | ForEach-Object { Check ($_ -ceq 'done') } }
Case 'Missing final offset cannot authenticate a measurement' {
    $f = Fixture; $f.result.client_stderr = 'chronyd version 4.5 starting (+NTS) Disabled control of system clock'; Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Capability restriction is required' {
    $f = Fixture; $f.result.capabilities = $f.result.capabilities.Replace('CapEff:' + "`t" + '0000000000000000','CapEff:' + "`t" + '0000000000000001'); Check (-not (Assessment $f).nts_authenticated_measurement_observed)
}
Case 'Exact offset and latency literals are retained' {
    $a = Assessment (Fixture); Check ($a.chrony_final_offset_seconds -ceq '-0.003012'); Check ($a.packet_metrics_seconds.'Peer delay' -ceq '0.007207788')
}
Case 'Global uncertainty remains unresolved after authentic measurement' {
    $a = Assessment (Fixture); Check ($a.nts_authenticated_measurement_observed -and $a.overall_utc_uncertainty -ceq 'unresolved' -and $null -eq $a.total_utc_error_bound_seconds)
}
Case 'Missing uncertainty component remains null' {
    $f = Fixture; $f.result.ntpdata = $f.result.ntpdata.Replace('Root dispersion : 0.000626 seconds',''); $a = Assessment $f
    Check ($a.nts_authenticated_measurement_observed -and $null -eq $a.packet_metrics_seconds.'Root dispersion')
}
Case 'WSL initialization duration is separate from the exchange' {
    $a = Assessment (Fixture); Check ($a.timing.launch_to_ready_seconds -eq [decimal]'0.09'); Check ($a.timing.exchanges[0].host_elapsed_seconds -eq [decimal]'0.01')
    Check ($a.timing.initialization_and_staging_seconds -eq [decimal]'0.499')
}
Case 'Linux elapsed bracket includes displayed timestamp quantization' {
    $a = Assessment (Fixture); Check ($a.timing.linux_boottime_elapsed_bracket_seconds[0] -eq [decimal]'5.98')
    Check ($a.timing.linux_boottime_elapsed_bracket_seconds[1] -eq [decimal]'6.02')
}
Case 'Unrecognized Linux monotonic resolution is rejected' {
    $f = Fixture; $f.after.linux.monotonic_after = '31.001'; Check ((Assessment $f).diagnostics -contains 'unsupported_linux_monotonic_resolution')
}
Case 'Candidate read bracket includes full transport interval' {
    $a = Assessment (Fixture); Check ($a.timing.exchanges[0].linux_minus_windows_candidate_interval_seconds[0] -eq [decimal]'-0.005')
    Check ($a.timing.exchanges[0].linux_minus_windows_candidate_interval_seconds[1] -eq [decimal]'0.005')
}
Case 'Windows Stopwatch frequency mismatch is rejected' {
    $f = Fixture; $f.after.sent.stopwatch_frequency = 2; Check ((Assessment $f).diagnostics -contains 'host_monotonic_domain_or_order_mismatch')
}
Case 'Windows Stopwatch ordering is checked' {
    $f = Fixture; $f.after.sent.stopwatch_ticks = 1; Check ((Assessment $f).diagnostics -contains 'host_monotonic_domain_or_order_mismatch')
}
Case 'Linux monotonic ordering is checked' {
    $f = Fixture; $f.after.linux.monotonic_before = '1.00'; Check ((Assessment $f).diagnostics -contains 'linux_monotonic_order_mismatch')
}
Case 'Different monotonic epochs cannot be substituted' {
    $f = Fixture; $f.after.linux.monotonic_domain = 'windows_stopwatch'; Check ((Assessment $f).diagnostics -contains 'linux_clock_domain_mismatch')
}
Case 'Windows realtime decrease is explicit' {
    $f = Fixture; $f.after.sent.utc_candidate = (HostStamp 1).utc_candidate; Check ((Assessment $f).diagnostics -contains 'windows_realtime_discontinuity')
}
Case 'Linux realtime discontinuity is explicit' {
    $f = Fixture; $f.after.linux.utc_unix_seconds = '1.00'; Check ((Assessment $f).diagnostics -contains 'realtime_discontinuity')
}
Case 'Missing UTC observation is unavailable' {
    $f = Fixture; $f.after.linux.utc_unix_seconds = ''; Check ($null -eq (Assessment $f).timing)
}
Case 'Windows and MT5 authentication and canonical readiness remain false' {
    $a = Assessment (Fixture); Check (-not $a.windows_clock_authenticated -and -not $a.mt5_native_utc_authenticated -and -not $a.canonical_ready -and -not $a.canonical_utc_conversion_authorized -and -not $a.utc_conversion_authenticated)
}
Case 'Stable UTF8 hashes and immutable publication are reused' {
    Check ((Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes('test'))) -ceq (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes('test'))))
    $dir = Join-Path $env:TEMP ('moneyway-nts-test-' + [guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $dir | Out-Null
    try { $path = Join-Path $dir 'report.json'; Publish-Mt5TickFile $path '{}'; Reject { Publish-Mt5TickFile $path 'changed' }; Check ([IO.File]::ReadAllText($path) -ceq '{}') }
    finally { Remove-Item -LiteralPath (Join-Path $dir 'report.json'); Remove-Item -LiteralPath $dir }
}
Case 'Production scripts retain non-adjusting mode and private cleanup' {
    $sh = [IO.File]::ReadAllText((Join-Path $tools 'Invoke-Mt5NtsLinuxCapture.sh'))
    Check ($sh.Contains('/usr/sbin/chronyd -Q -t') -and $sh.Contains('setpriv --bounding-set=-all'))
    Check (-not ($sh -cmatch '\s-q(?:\s|$)|\nmakestep\s'))
    Check ($sh.Contains('rm -rf -- "$dir"') -and $sh.Contains('trap cleanup EXIT'))
    $ps = [IO.File]::ReadAllText((Join-Path $tools 'Get-Mt5NtsTimeCorrelation.ps1')) + [IO.File]::ReadAllText((Join-Path $tools 'Mt5NtsCorrelation.ps1'))
    Check (-not ($ps -match 'wsl --shutdown|--terminate|Restart-Service|Set-Date|w32tm.exe|apt-get|checkpoint.*Commit'))
    Check ($ps.Contains('Publish-Mt5TickFile') -and $ps.Contains('selected_input_hashes_unchanged'))
}
Case 'PowerShell AST parses the coordinator and helper' {
    foreach ($name in @('Mt5NtsCorrelation.ps1','Get-Mt5NtsTimeCorrelation.ps1')) {
        $errors = $null; $tokens = $null; $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $tools $name), [ref]$tokens, [ref]$errors)
        Check ($errors.Count -eq 0)
    }
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { exit 1 }
