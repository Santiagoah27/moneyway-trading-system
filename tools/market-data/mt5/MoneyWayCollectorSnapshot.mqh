// Acquisition/JSON logic reused from the manual probe; that source remains unchanged.
const string PROBE_SYMBOL = "US100";
const string EXPECTED_SERVER = "OANDA_Global-Demo-1";

long captured_m1_head = 0;
bool captured_m1_usable = false;
bool snapshot_complete = false;

string BoolJson(const bool value) { return value ? "true" : "false"; }

string Quote(const string value)
{
    string result = "\"";
    for(int i = 0; i < StringLen(value); i++)
    {
        ushort c = StringGetCharacter(value, i);
        if(c == 34) result += "\\\"";
        else if(c == 92) result += "\\\\";
        else if(c < 32) result += StringFormat("\\u%04x", (int)c);
        else result += ShortToString(c);
    }
    return result + "\"";
}

// The integer is MQL5's raw datetime representation, NOT an authenticated UTC epoch.
string NativeTime(const datetime value)
{
    if(value <= 0) return "null";
    return "{\"raw\":" + (string)(long)value + ",\"display\":" +
        Quote(TimeToString(value, TIME_DATE | TIME_SECONDS)) + "}";
}

string NumberJson(const double value)
{
    return MathIsValidNumber(value) ? DoubleToString(value, 16) : "null";
}

bool ValidOhlc(const MqlRates &bar)
{
    return MathIsValidNumber(bar.open) && MathIsValidNumber(bar.high) &&
        MathIsValidNumber(bar.low) && MathIsValidNumber(bar.close) &&
        bar.high >= bar.open && bar.high >= bar.close && bar.high >= bar.low &&
        bar.low <= bar.open && bar.low <= bar.close;
}

// Keep API success separate from a legitimate zero value (e.g. unsynchronized).
string SeriesProperty(const ENUM_TIMEFRAMES timeframe, const ENUM_SERIES_INFO_INTEGER property,
    long &value, bool &ok)
{
    ResetLastError();
    ok = SeriesInfoInteger(PROBE_SYMBOL, timeframe, property, value);
    int error = GetLastError();
    return "{\"value\":" + (ok ? (string)value : "null") +
        ",\"error_code\":" + (string)error + "}";
}

string ProbeTimeframe(const ENUM_TIMEFRAMES timeframe, const string name, const bool allowed)
{
    string result = "{\"timeframe\":" + Quote(name) + ",\"period_seconds\":" +
        (string)PeriodSeconds(timeframe) + ",\"requested_count\":" + (string)SampleBars;
    if(!allowed || !SourceSafe())
    {
        snapshot_complete = false;
        return result + ",\"status\":\"not_attempted\",\"copy_return\":null," +
            "\"copy_error_code\":null,\"bars\":[]}";
    }

    long before = 0, after = 0, sync = 0, count = 0, first = 0;
    bool before_ok = false, after_ok = false, ok = false;
    result += ",\"synchronized_before\":" + SeriesProperty(timeframe, SERIES_SYNCHRONIZED, sync, ok);
    result += ",\"last_bar_before\":" + SeriesProperty(timeframe, SERIES_LASTBAR_DATE, before, before_ok);
    MqlRates rates[];
    ArraySetAsSeries(rates, true); // Logical index zero is the latest/current series bar.
    ulong started = GetMicrosecondCount();
    ResetLastError();
    int copied = CopyRates(PROBE_SYMBOL, timeframe, 0, SampleBars, rates);
    int copy_error = GetLastError();
    ulong finished = GetMicrosecondCount();
    result += ",\"copy_started_elapsed_us\":" + (string)started +
        ",\"copy_finished_elapsed_us\":" + (string)finished;
    result += ",\"synchronized_after\":" + SeriesProperty(timeframe, SERIES_SYNCHRONIZED, sync, ok);
    bool synchronized_after = ok && sync == 1;
    result += ",\"last_bar_after\":" + SeriesProperty(timeframe, SERIES_LASTBAR_DATE, after, after_ok);
    result += ",\"terminal_bar_count\":" + SeriesProperty(timeframe, SERIES_BARS_COUNT, count, ok);
    result += ",\"terminal_first_date\":" + SeriesProperty(timeframe, SERIES_TERMINAL_FIRSTDATE, first, ok);
    result += ",\"server_first_date\":" + SeriesProperty(timeframe, SERIES_SERVER_FIRSTDATE, first, ok);
    bool consistent = copied > 0 && before_ok && after_ok && before > 0 &&
        before == after && rates[0].time == after;
    string status = copied < 0 ? "copy_failed" : copied == 0 ? "empty" :
        copied < SampleBars ? "partial_sample" : "sample_returned";
    result += ",\"status\":" + Quote(status) + ",\"copy_return\":" + (string)copied +
        ",\"copy_error_code\":" + (string)copy_error +
        ",\"head_consistent\":" + BoolJson(consistent) + ",\"bars\":[";
    bool payload_valid = true;
    for(int i = 0; i < copied; i++)
    {
        if(!ValidOhlc(rates[i]) || rates[i].time <= 0 || (i > 0 && rates[i].time >= rates[i-1].time)) payload_valid = false;
        if(i > 0) result += ",";
        // Even a stable position zero can be stale or belong to a closed market.
        string role = !consistent ? "unverified_head" : i == 0 ?
            "current_bar_completion_unverified" : rates[i].time < after ?
            "historical_preceding_bar" : "unverified_head";
        long delta = i + 1 < copied ? (long)(rates[i].time - rates[i + 1].time) : 0;
        result += "{\"series_position\":" + (string)i + ",\"open_time_native\":" + NativeTime(rates[i].time) +
            ",\"open\":" + NumberJson(rates[i].open) + ",\"high\":" + NumberJson(rates[i].high) +
            ",\"low\":" + NumberJson(rates[i].low) + ",\"close\":" + NumberJson(rates[i].close) +
            ",\"ohlc_valid\":" + BoolJson(ValidOhlc(rates[i])) + ",\"role\":" + Quote(role) +
            ",\"newer_than_next_seconds\":" + (i + 1 < copied ? (string)delta : "null") +
            ",\"open_time_utc\":null,\"provider_confirmed_closed\":null," +
            "\"historical_available_at_utc\":null}";
    }
    bool usable = copied > 0 && copy_error == 0 && synchronized_after && consistent && payload_valid;
    if(timeframe == PERIOD_M1) { captured_m1_head = copied > 0 ? (long)rates[0].time : 0; captured_m1_usable = usable; }
    bool complete = usable && copied == SampleBars;
    snapshot_complete = snapshot_complete && complete;
    if(!complete) Print("MoneyWay collector: incomplete ", name, " CopyRates=", copied, " error=", copy_error);
    return result + "]}";
}

string CaptureProbe()
{
    captured_m1_head = 0; captured_m1_usable = false; snapshot_complete = true;
    bool tester = (bool)MQLInfoInteger(MQL_TESTER);
    bool connected_before = (bool)TerminalInfoInteger(TERMINAL_CONNECTED);
    ResetLastError();
    long mode = AccountInfoInteger(ACCOUNT_TRADE_MODE);
    int mode_error = GetLastError();
    string server = AccountInfoString(ACCOUNT_SERVER);
    string company = AccountInfoString(ACCOUNT_COMPANY);
    bool demo = mode_error == 0 && mode == ACCOUNT_TRADE_MODE_DEMO;
    bool allowed = !tester && connected_before && demo && server == EXPECTED_SERVER && company == "OANDA Global Markets Limited" && !(bool)TerminalInfoInteger(TERMINAL_TRADE_ALLOWED) &&
        SampleBars >= 2 && SampleBars <= 128;
    string gate = tester ? "tester_rejected" : !connected_before ? "terminal_disconnected" :
        !demo ? "demo_unverified" : server != EXPECTED_SERVER ? "server_mismatch" :
        SampleBars < 2 || SampleBars > 128 ? "sample_bound_rejected" : "passed";

    bool custom = false, exists = false, selected = false;
    int symbol_error = 0;
    string description = "", currency = "";
    long digits = 0;
    bool description_ok = false, currency_ok = false, digits_ok = false;
    int description_error = 0, currency_error = 0, digits_error = 0;
    if(allowed)
    {
        ResetLastError();
        exists = SymbolExist(PROBE_SYMBOL, custom);
        if(exists && !custom) selected = SymbolSelect(PROBE_SYMBOL, true);
        symbol_error = GetLastError();
        if(selected)
        {
            ResetLastError();
            description_ok = SymbolInfoString(PROBE_SYMBOL, SYMBOL_DESCRIPTION, description);
            description_error = GetLastError();
            ResetLastError();
            currency_ok = SymbolInfoString(PROBE_SYMBOL, SYMBOL_CURRENCY_PROFIT, currency);
            currency_error = GetLastError();
            ResetLastError();
            digits_ok = SymbolInfoInteger(PROBE_SYMBOL, SYMBOL_DIGITS, digits);
            digits_error = GetLastError();
        }
    }
    datetime generated_gmt = TimeGMT();
    datetime server_quote_before = TimeCurrent();
    string json = "{\"schema\":\"moneyway.mt5-read-only-probe\",\"version\":1," +
        "\"diagnostic_only\":true,\"canonical_ready\":false,\"generated_at_utc\":null," +
        "\"generation_time_basis\":\"TimeGMT_computer_derived_not_authenticated\"," +
        "\"generated_gmt_candidate\":" + NativeTime(generated_gmt) +
        ",\"utc_conversion_authenticated\":false,\"feed_equivalence_verified\":false," +
        "\"source\":{\"platform\":\"MetaTrader 5\",\"server\":" + Quote(server) +
        ",\"company\":" + Quote(company) + ",\"demo_verified\":" + BoolJson(demo) +
        ",\"mode_error_code\":" + (string)mode_error + ",\"connected_before\":" + BoolJson(connected_before) +
        ",\"terminal_build\":" + (string)TerminalInfoInteger(TERMINAL_BUILD) + "},\"gate\":" + Quote(gate) +
        ",\"symbol\":{\"native\":" + Quote(PROBE_SYMBOL) + ",\"query_attempted\":" + BoolJson(allowed) +
        ",\"exists\":" + BoolJson(exists) + ",\"custom\":" + BoolJson(custom) +
        ",\"selected\":" + BoolJson(selected) + ",\"error_code\":" + (string)symbol_error +
        ",\"description\":" + (description_ok ? Quote(description) : "null") +
        ",\"description_error_code\":" + (selected ? (string)description_error : "null") +
        ",\"profit_currency\":" + (currency_ok ? Quote(currency) : "null") +
        ",\"profit_currency_error_code\":" + (selected ? (string)currency_error : "null") +
        ",\"digits\":" + (digits_ok ? (string)digits : "null") +
        ",\"digits_error_code\":" + (selected ? (string)digits_error : "null") + "},\"server_quote_before\":" +
        NativeTime(server_quote_before) + ",\"timeframes\":[";
    bool acquire = allowed && exists && !custom && selected;
    json += ProbeTimeframe(PERIOD_M1, "M1", acquire) + ",";
    json += ProbeTimeframe(PERIOD_M5, "M5", acquire) + ",";
    json += ProbeTimeframe(PERIOD_H1, "H1", acquire) + ",";
    json += ProbeTimeframe(PERIOD_H4, "H4", acquire);
    json += "],\"server_quote_after\":" + NativeTime(TimeCurrent()) +
        ",\"connected_after\":" + BoolJson((bool)TerminalInfoInteger(TERMINAL_CONNECTED)) +
        ",\"limitations\":[\"native_server_time_offset_unresolved\",\"no_source_close_confirmation\"," +
        "\"no_historical_availability_timestamp\",\"single_pass_no_reconnect_or_revision_detection\"," +
        "\"sample_does_not_establish_strategy_warmup_or_freshness\",\"not_an_atomic_multi_timeframe_snapshot\"]}";
    return json;
}
