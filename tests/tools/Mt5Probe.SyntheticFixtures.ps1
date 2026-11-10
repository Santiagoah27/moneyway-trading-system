# Explicitly synthetic fixtures shared by standalone MT5 tooling checks.
function Native($Value) {
    $time = [datetime]::ParseExact($Value, 'yyyy.MM.dd HH:mm:ss', [cultureinfo]::InvariantCulture)
    return @{ raw = [long]($time - [datetime]'1970-01-01').TotalSeconds; display = $Value }
}

function Fixture {
    $frames = @()
    foreach ($pair in @(@('M1', 60), @('M5', 300), @('H1', 3600), @('H4', 14400))) {
        $latest = Native '2026.01.05 12:00:00'
        $olderTime = ([datetime]'2026-01-05T12:00:00').AddSeconds(-$pair[1]).ToString('yyyy.MM.dd HH:mm:ss')
        $bars = @()
        for ($i = 0; $i -lt 2; $i++) {
            $bars += @{
                series_position = $i; open_time_native = $(if ($i -eq 0) { $latest } else { Native $olderTime })
                open = 100.25; high = 102.5; low = 99.75; close = 101.25; ohlc_valid = $true
                role = $(if ($i -eq 0) { 'current_bar_completion_unverified' } else { 'historical_preceding_bar' })
                newer_than_next_seconds = $(if ($i -eq 0) { $pair[1] } else { $null })
                open_time_utc = $null; provider_confirmed_closed = $null; historical_available_at_utc = $null
            }
        }
        $frames += @{
            timeframe = $pair[0]; period_seconds = $pair[1]; requested_count = 2
            status = 'sample_returned'; copy_return = 2; copy_error_code = 0
            copy_started_elapsed_us = 10; copy_finished_elapsed_us = 20
            synchronized_before = @{ value = 1; error_code = 0 }; synchronized_after = @{ value = 1; error_code = 0 }
            last_bar_before = @{ value = $latest.raw; error_code = 0 }; last_bar_after = @{ value = $latest.raw; error_code = 0 }
            terminal_bar_count = @{ value = 2; error_code = 0 }
            terminal_first_date = @{ value = $bars[1].open_time_native.raw; error_code = 0 }
            server_first_date = @{ value = $bars[1].open_time_native.raw; error_code = 0 }
            head_consistent = $true; bars = $bars
        }
    }
    return @{
        schema = 'moneyway.mt5-read-only-probe'; version = 1; diagnostic_only = $true; canonical_ready = $false
        generated_at_utc = $null; generation_time_basis = 'TimeGMT_computer_derived_not_authenticated'
        generated_gmt_candidate = (Native '2026.01.05 12:00:00')
        utc_conversion_authenticated = $false; feed_equivalence_verified = $false
        source = @{ platform = 'MetaTrader 5'; server = 'OANDA_Global-Demo-1'; company = 'SYNTHETIC FIXTURE'
            demo_verified = $true; mode_error_code = 0; connected_before = $true; terminal_build = 0 }
        gate = 'passed'
        symbol = @{ native = 'US100'; query_attempted = $true; exists = $true; custom = $false; selected = $true
            error_code = 0; description = 'SYNTHETIC'; description_error_code = 0; profit_currency = 'USD'
            profit_currency_error_code = 0; digits = 2; digits_error_code = 0 }
        server_quote_before = $null; server_quote_after = $null; connected_after = $true
        limitations = @('synthetic_not_provider_evidence'); timeframes = $frames
    }
}
