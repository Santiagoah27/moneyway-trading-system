#property strict
#property description "Bounded demo US100 tick diagnostic. Keep Algo Trading disabled."

input string SessionNonce = ""; // Copy the coordinator's 64-character nonce.

#include "MoneyWayCorrelatedTickDecision.mqh"

string root = "";
string ready_token = "";
string request_hash = "";
string context_hash = "";
int session_lock = INVALID_HANDLE;
ulong started_ms = 0, armed_ms = 0, handler_ms = 0;
long limit_ms = 0, capture_limit_ms = 0;
long callback_count = 0, armed_callback_count = 0;
long cached_callbacks = 0;
bool armed = false, finished = false;
MqlTick baseline, captured;
int tick_error = 0;

string Q(string value)
{
    StringReplace(value, "\\", "\\\\");
    StringReplace(value, "\"", "\\\"");
    StringReplace(value, "\r", "\\r");
    StringReplace(value, "\n", "\\n");
    StringReplace(value, "\t", "\\t");
    return "\"" + value + "\"";
}

string B(bool value) { return value ? "true" : "false"; }

string Native(datetime value)
{
    if(value <= 0) return "null";
    return "{\"raw\":" + (string)(long)value + ",\"display\":" +
        Q(TimeToString(value, TIME_DATE | TIME_SECONDS)) + "}";
}

bool HexNonce(string nonce)
{
    if(StringLen(nonce) != 64) return false;
    for(int i = 0; i < 64; i++)
        if(StringFind("0123456789abcdef", StringSubstr(nonce, i, 1)) < 0) return false;
    return true;
}

bool ReadText(string name, string &text, string &hash)
{
    int file = FileOpen(root + name, FILE_READ | FILE_BIN | FILE_COMMON | FILE_SHARE_READ);
    if(file == INVALID_HANDLE) return false;
    ulong size = FileSize(file);
    if(size == 0 || size > 32768) { FileClose(file); return false; }
    uchar bytes[], key[], digest[];
    uint read = FileReadArray(file, bytes, 0, (int)size);
    FileClose(file);
    if(read != size || CryptEncode(CRYPT_HASH_SHA256, bytes, key, digest) != 32) return false;
    hash = "";
    for(int i = 0; i < 32; i++) hash += StringFormat("%02x", (int)digest[i]);
    text = CharArrayToString(bytes, 0, (int)size, CP_UTF8);
    return true;
}

bool Publish(string name, string text)
{
    string target = root + name;
    string pending = target + ".pending";
    if(FileIsExist(target, FILE_COMMON) || FileIsExist(pending, FILE_COMMON)) return false;
    int file = FileOpen(pending, FILE_READ | FILE_WRITE | FILE_BIN | FILE_COMMON);
    if(file == INVALID_HANDLE) return false;
    if(FileSize(file) != 0) { FileClose(file); return false; }
    uchar bytes[];
    int count = StringToCharArray(text + "\n", bytes, 0, WHOLE_ARRAY, CP_UTF8) - 1;
    ResetLastError();
    uint written = FileWriteArray(file, bytes, 0, count);
    FileFlush(file);
    int error = GetLastError();
    FileClose(file);
    if(written != count || error != 0) return false;
    return FileMove(pending, FILE_COMMON, target, FILE_COMMON);
}

string Identity()
{
    return "{\"company\":" + Q(AccountInfoString(ACCOUNT_COMPANY)) +
        ",\"server\":" + Q(AccountInfoString(ACCOUNT_SERVER)) +
        ",\"demo\":" + B(AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO) +
        ",\"symbol\":" + Q(_Symbol) +
        ",\"connected\":" + B((bool)TerminalInfoInteger(TERMINAL_CONNECTED)) +
        ",\"algo_trading_enabled\":" + B((bool)TerminalInfoInteger(TERMINAL_TRADE_ALLOWED)) + "}";
}

bool SourceSafe()
{
    bool custom = false;
    return _Symbol == "US100" && SymbolExist(_Symbol, custom) && !custom &&
        AccountInfoString(ACCOUNT_COMPANY) == "OANDA Global Markets Limited" &&
        AccountInfoString(ACCOUNT_SERVER) == "OANDA_Global-Demo-1" &&
        AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO &&
        !(bool)MQLInfoInteger(MQL_TESTER) &&
        !(bool)TerminalInfoInteger(TERMINAL_TRADE_ALLOWED);
}

string TickJson(const MqlTick &tick)
{
    // Scientific decimal strings retain 17 significant digits independently of host JSON parsing.
    return "{\"time\":" + Native(tick.time) + ",\"time_msc\":" + (string)tick.time_msc +
        ",\"bid\":" + Q(DoubleToString(tick.bid, -16)) +
        ",\"ask\":" + Q(DoubleToString(tick.ask, -16)) +
        ",\"last\":" + Q(DoubleToString(tick.last, -16)) +
        ",\"volume\":" + Q((string)tick.volume) + ",\"flags\":" + (string)tick.flags +
        ",\"volume_real\":" + Q(DoubleToString(tick.volume_real, -16)) + "}";
}

void Finish(string reason, bool success)
{
    if(finished) return;
    finished = true;
    EventKillTimer();
    string response = "{\"schema\":\"moneyway.mt5-correlated-tick-response\",\"version\":1," +
        "\"nonce\":" + Q(SessionNonce) + ",\"ready_token\":" + Q(ready_token) +
        ",\"request_sha256\":" + Q(request_hash) + ",\"source\":" + Identity() +
        ",\"context_sha256\":" + Q(context_hash) +
        ",\"outcome\":" + Q(success ? "captured" : "data_unavailable") + ",\"reason\":" + Q(reason) +
        ",\"event\":" + Q(success ? "OnTick" : "none") +
        ",\"started_ms\":" + (string)started_ms + ",\"armed_ms\":" + (string)armed_ms +
        ",\"handler_ms\":" + (string)handler_ms + ",\"finished_ms\":" + (string)GetTickCount64() +
        ",\"armed_callback_count\":" + (string)armed_callback_count +
        ",\"callback_count\":" + (string)callback_count + ",\"cached_callbacks\":" + (string)cached_callbacks +
        ",\"baseline\":" + (armed ? TickJson(baseline) : "null") +
        ",\"tick\":" + (success ? TickJson(captured) : "null") +
        ",\"tick_error\":" + (string)tick_error +
        ",\"lookup_event_identity\":\"unresolved_queued_event_and_latest_lookup\"," +
        "\"canonical_utc_conversion_authorized\":false}";
    if(!Publish("response.json", response)) Print("Diagnostic response publication failed ", GetLastError());
    ExpertRemove();
}

int OnInit()
{
    if(!HexNonce(SessionNonce)) return INIT_PARAMETERS_INCORRECT;
    root = "MoneyWayCorrelatedTick\\" + SessionNonce + "\\";
    // A previously published ready/request/response makes this nonce permanently ineligible for a restart.
    if(!TickNonceEligible(FileIsExist(root + "ready.json", FILE_COMMON), FileIsExist(root + "request.txt", FILE_COMMON),
        FileIsExist(root + "response.json", FILE_COMMON), FileIsExist(root + "cancel.txt", FILE_COMMON)))
        return INIT_FAILED;
    session_lock = FileOpen(root + "session.lock", FILE_READ | FILE_WRITE | FILE_BIN | FILE_COMMON);
    if(session_lock == INVALID_HANDLE) return INIT_FAILED;
    string offer, hash;
    if(!ReadText("offer.txt", offer, hash)) return INIT_FAILED;
    int end = StringLen(offer) - 1;
    if(end < 1 || StringGetCharacter(offer, end) != '\n') return INIT_FAILED;
    offer = StringSubstr(offer, 0, end);
    string lines[];
    if(StringSplit(offer, '\n', lines) != 4 || lines[0] != "moneyway.mt5-correlated-tick-offer/v1" ||
        lines[1] != SessionNonce || lines[3] != "US100") return INIT_FAILED;
    long seconds = StringToInteger(lines[2]);
    if(seconds < 1 || seconds > 900 || (string)seconds != lines[2]) return INIT_PARAMETERS_INCORRECT;
    limit_ms = seconds * 1000;
    started_ms = GetTickCount64();
    ready_token = (string)started_ms + "-" + (string)GetMicrosecondCount() + "-" + (string)ChartID();
    if(!SourceSafe()) { Finish("invalid_source_or_execution_enabled", false); return INIT_FAILED; }
    if(!TerminalInfoInteger(TERMINAL_CONNECTED)) { Finish("disconnected", false); return INIT_FAILED; }
    string ready = "{\"schema\":\"moneyway.mt5-correlated-tick-ready\",\"version\":1,\"nonce\":" +
        Q(SessionNonce) + ",\"ready_token\":" + Q(ready_token) + ",\"offer_sha256\":" + Q(hash) +
        ",\"source\":" + Identity() + "}";
    if(!Publish("ready.json", ready) || !EventSetMillisecondTimer(100)) return INIT_FAILED;
    return INIT_SUCCEEDED;
}

bool CheckSession()
{
    if(finished) return false;
    int stop = TickStopReason(FileIsExist(root + "cancel.txt", FILE_COMMON), SourceSafe(),
        (bool)TerminalInfoInteger(TERMINAL_CONNECTED), (long)(GetTickCount64() - started_ms),
        limit_ms, armed, (long)(GetTickCount64() - armed_ms), capture_limit_ms);
    if(stop == 1) { Finish("cancelled", false); return false; }
    if(stop == 2) { Finish("session_timeout", false); return false; }
    if(stop == 3) { Finish("source_changed_or_execution_enabled", false); return false; }
    if(stop == 4) { Finish("disconnected", false); return false; }
    if(stop == 5)
    {
        Finish(cached_callbacks > 0 ? "cached_tick_only" : "no_tick", false);
        return false;
    }
    return true;
}

void OnTimer()
{
    if(!CheckSession() || armed || !FileIsExist(root + "request.txt", FILE_COMMON)) return;
    string request;
    if(!ReadText("request.txt", request, request_hash)) { Finish("partial_request", false); return; }
    int end = StringLen(request) - 1;
    if(end < 1 || StringGetCharacter(request, end) != '\n') { Finish("partial_request", false); return; }
    request = StringSubstr(request, 0, end);
    string lines[];
    if(StringSplit(request, '\n', lines) != 5 || lines[0] != "moneyway.mt5-correlated-tick-request/v1" ||
        lines[1] != SessionNonce || lines[2] != ready_token || lines[4] != "US100")
    { Finish("invalid_or_stale_request", false); return; }
    capture_limit_ms = StringToInteger(lines[3]);
    if(capture_limit_ms < 1 || capture_limit_ms > limit_ms || (string)capture_limit_ms != lines[3])
    { Finish("invalid_request_limit", false); return; }
    ResetLastError();
    bool ok = SymbolInfoTick(_Symbol, baseline);
    tick_error = GetLastError();
    if(!ok || tick_error != 0 || baseline.time_msc <= 0) { Finish("source_unavailable", false); return; }
    armed_callback_count = callback_count;
    armed_ms = GetTickCount64();
    armed = true;
}

void OnTick()
{
    callback_count++;
    if(!CheckSession() || !armed) return;
    handler_ms = GetTickCount64();
    ResetLastError();
    bool ok = SymbolInfoTick(_Symbol, captured);
    tick_error = GetLastError();
    if(!ok || tick_error != 0) { Finish("tick_lookup_failed", false); return; }
    // A queued callback and a cached latest lookup cannot qualify as a timestamp-progressing observation.
    if(!TickProgressed(armed, callback_count, armed_callback_count, captured.time_msc, baseline.time_msc))
    { cached_callbacks++; return; }
    datetime current = TimeCurrent(), server = TimeTradeServer(), gmt = TimeGMT(), local = TimeLocal();
    ResetLastError();
    long m1 = 0;
    bool m1_ok = SeriesInfoInteger(_Symbol, PERIOD_M1, SERIES_LASTBAR_DATE, m1);
    int m1_error = GetLastError();
    string context = "{\"time_current\":" + Native(current) + ",\"time_trade_server\":" + Native(server) +
        ",\"time_gmt\":" + Native(gmt) + ",\"time_local\":" + Native(local) +
        ",\"m1_head\":" + (m1_ok ? Native((datetime)m1) : "null") +
        ",\"m1_error\":" + (string)m1_error + "}";
    if(!Publish("context.json", context)) { Finish("context_publication_failed", false); return; }
    string retained_context;
    if(!ReadText("context.json", retained_context, context_hash)) { Finish("context_hash_failed", false); return; }
    Finish("subsequent_ontick_timestamp_progressed", true);
}

void OnDeinit(const int reason)
{
    EventKillTimer();
    if(!finished && started_ms > 0) Finish("ea_removed", false);
    if(session_lock != INVALID_HANDLE) FileClose(session_lock);
}
