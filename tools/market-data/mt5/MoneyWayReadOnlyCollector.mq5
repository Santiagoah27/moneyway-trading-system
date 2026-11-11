#property strict
#property description "Demo-only read-only US100 collector. Keep Algo Trading disabled."

input int TimerSeconds = 5;
input int SampleBars = 16;
input int RetryEveryPolls = 12;

#include "MoneyWayCollectorDecision.mqh"
#include "MoneyWayCollectorSnapshot.mqh"

const string ROOT = "MoneyWayAutomaticReadOnly\\";
int lock_handle = INVALID_HANDLE;
bool running = false;
bool g_has_snapshot = false;
bool incomplete_snapshot = true;
long g_last_head = 0;
long g_last_attempted_head = 0;
long poll_sequence = 0;
long next_retry_poll = 0;
long sequence = 0;
int last_condition = -1;
string previous_record = "";
string previous_hash = "";
string run_id = "";

bool SourceSafe()
{
    ResetLastError();
    long mode = AccountInfoInteger(ACCOUNT_TRADE_MODE);
    int error = GetLastError();
    return error == 0 && mode == ACCOUNT_TRADE_MODE_DEMO &&
        AccountInfoString(ACCOUNT_SERVER) == EXPECTED_SERVER &&
        AccountInfoString(ACCOUNT_COMPANY) == "OANDA Global Markets Limited" &&
        !(bool)MQLInfoInteger(MQL_TESTER) && !(bool)TerminalInfoInteger(TERMINAL_TRADE_ALLOWED);
}

string SequenceText(long value) { return StringFormat("%012I64d", value); }

bool HashBytes(uchar &bytes[], string &hash)
{
    uchar key[], digest[];
    ResetLastError();
    if(CryptEncode(CRYPT_HASH_SHA256, bytes, key, digest) != 32) return false;
    hash = "";
    for(int i = 0; i < 32; i++) hash += StringFormat("%02x", (int)digest[i]);
    return true;
}

bool HashFile(string path, string &hash)
{
    int file = FileOpen(path, FILE_READ | FILE_BIN | FILE_COMMON);
    if(file == INVALID_HANDLE) return false;
    ulong size = FileSize(file);
    if(size == 0 || size > 2097152) { FileClose(file); return false; }
    uchar bytes[];
    uint read = FileReadArray(file, bytes, 0, (int)size);
    FileClose(file);
    return read == size && HashBytes(bytes, hash);
}

bool Publish(string name, string json, string &hash)
{
    uchar bytes[];
    int length = StringToCharArray(json + "\n", bytes, 0, WHOLE_ARRAY, CP_UTF8);
    if(length <= 1) return false;
    ArrayResize(bytes, length - 1); // Exclude the C-string terminator from file and digest.
    if(!HashBytes(bytes, hash)) return false;
    string target = ROOT + name;
    if(FileIsExist(target, FILE_COMMON)) return false;
    string temporary = ROOT + "pending-" + run_id + "-" + SequenceText(sequence) + "-" + (string)GetMicrosecondCount() + ".tmp";
    if(FileIsExist(temporary, FILE_COMMON)) return false;
    int file = FileOpen(temporary, FILE_READ | FILE_WRITE | FILE_BIN | FILE_COMMON);
    if(file == INVALID_HANDLE) return false;
    if(FileSize(file) != 0) { FileClose(file); return false; }
    ResetLastError();
    uint written = FileWriteArray(file, bytes, 0, ArraySize(bytes));
    FileFlush(file);
    int error = GetLastError();
    FileClose(file);
    if(written != ArraySize(bytes) || error != 0) return false;
    // FILE_REWRITE is intentionally absent: a race/collision must fail, never truncate.
    if(!FileMove(temporary, FILE_COMMON, target, FILE_COMMON)) return false;
    string verified = "";
    return HashFile(target, verified) && verified == hash;
}

bool RecoverSequence()
{
    string name;
    long search = FileFindFirst(ROOT + "record-*.json", name, FILE_COMMON);
    if(search == INVALID_HANDLE) return true; // Documented no matching record case.
    int count = 0;
    do
    {
        if(++count > 100000 || StringLen(name) != 24 || StringSubstr(name, 0, 7) != "record-" || StringSubstr(name, 19) != ".json")
        { FileFindClose(search); return false; }
        string token = StringSubstr(name, 7, 12);
        for(int i = 0; i < 12; i++)
            if(StringGetCharacter(token, i) < 48 || StringGetCharacter(token, i) > 57) { FileFindClose(search); return false; }
        long found = StringToInteger(token);
        if(found <= 0) { FileFindClose(search); return false; }
        if(found > sequence) { sequence = found; previous_record = name; }
    } while(FileFindNext(search, name));
    FileFindClose(search);
    return previous_record == "" || HashFile(ROOT + previous_record, previous_hash);
}

bool ReserveSequence()
{
    for(int i = 0; i < 1024; i++)
    {
        if(sequence >= 999999999999) return false;
        sequence++;
        string token = SequenceText(sequence);
        // Orphan probes after a crash are preserved; their number is never reused.
        if(CollectorSequenceFree(sequence, FileIsExist(ROOT + "record-" + token + ".json", FILE_COMMON),
            FileIsExist(ROOT + "probe-" + token + ".json", FILE_COMMON))) return true;
    }
    return false;
}

bool Record(string event_name, int condition, long trigger_head, string probe, bool source_stable, int shutdown_reason)
{
    if(!ReserveSequence()) { Print("MoneyWay collector: sequence reservation failed."); return false; }
    if(run_id == "") run_id = "run-" + SequenceText(sequence);
    string token = SequenceText(sequence), probe_name = "", probe_hash = "";
    if(probe != "")
    {
        probe_name = "probe-" + token + ".json";
        if(!Publish(probe_name, probe, probe_hash))
        { Print("MoneyWay collector: probe publication failed; error ", GetLastError()); return false; }
    }
    string metadata = "{\"schema\":\"moneyway.mt5-read-only-collector\",\"version\":1," +
        "\"diagnostic_only\":true,\"canonical_ready\":false,\"utc_conversion_authenticated\":false," +
        "\"sequence\":" + (string)sequence + ",\"run_id\":" + Quote(run_id) +
        ",\"poll_sequence\":" + (string)poll_sequence + ",\"event\":" + Quote(event_name) +
        ",\"condition\":" + (string)condition + ",\"timer_seconds\":" + (string)TimerSeconds +
        ",\"retry_every_polls\":" + (string)RetryEveryPolls + ",\"sample_bars\":" + (string)SampleBars +
        ",\"source\":{\"company\":\"OANDA Global Markets Limited\",\"server\":" + Quote(EXPECTED_SERVER) +
        ",\"demo\":true,\"symbol\":" + Quote(PROBE_SYMBOL) + "},\"trigger_m1_head_native\":" + NativeTime((datetime)trigger_head) +
        ",\"previous_committed_m1_head_native\":" + NativeTime((datetime)g_last_head) +
        ",\"captured_m1_head_native\":" + (probe == "" ? "null" : NativeTime((datetime)captured_m1_head)) +
        ",\"m1_usable\":" + BoolJson(probe != "" && captured_m1_usable) +
        ",\"sample_complete\":" + BoolJson(probe != "" && snapshot_complete) +
        ",\"source_stable\":" + BoolJson(source_stable) +
        ",\"previous_record\":" + (previous_record == "" ? "null" : Quote(previous_record)) +
        ",\"previous_record_sha256\":" + (previous_record == "" ? "null" : Quote(previous_hash)) +
        ",\"probe_file\":" + (probe == "" ? "null" : Quote(probe_name)) +
        ",\"probe_sha256\":" + (probe == "" ? "null" : Quote(probe_hash)) +
        ",\"shutdown_reason\":" + (event_name == "shutdown" ? (string)shutdown_reason : "null") +
        ",\"sequence_basis\":\"local_single_writer_publication_not_utc\"}";
    string name = "record-" + token + ".json", hash = "";
    if(!Publish(name, metadata, hash))
    { Print("MoneyWay collector: record publication failed; retained probe may be orphaned; error ", GetLastError()); return false; }
    previous_record = name; previous_hash = hash;
    Print("MoneyWay collector: ", name, " event=", event_name, " SHA256=", hash);
    return true;
}

void PollOnce()
{
    if(!running) return;
    poll_sequence++;
    bool safe = SourceSafe();
    if(!safe)
    {
        Print("MoneyWay collector: stopped; demo/server/company changed, tester detected, or Algo Trading enabled. Keep it disabled.");
        EventKillTimer(); ExpertRemove(); return;
    }
    bool connected = (bool)TerminalInfoInteger(TERMINAL_CONNECTED);
    long head = 0, synchronized = 0;
    bool custom = false;
    bool selected = connected && SymbolExist(PROBE_SYMBOL, custom) && !custom && SymbolSelect(PROBE_SYMBOL, true);
    bool head_ready = selected && SeriesInfoInteger(PROBE_SYMBOL, PERIOD_M1, SERIES_LASTBAR_DATE, head) &&
        SeriesInfoInteger(PROBE_SYMBOL, PERIOD_M1, SERIES_SYNCHRONIZED, synchronized) && synchronized == 1;
    int condition = CollectorCondition(safe, connected, head_ready, head, g_last_head);
    bool retry_due = poll_sequence >= next_retry_poll;
    int action = CollectorAction(g_has_snapshot, condition, last_condition, head, g_last_head, retry_due, incomplete_snapshot);
    bool attempt = CollectorAttemptAllowed(action, retry_due, condition != last_condition, head, g_last_attempted_head);
    last_condition = condition;
    if(!attempt) return;
    next_retry_poll = poll_sequence + RetryEveryPolls;
    g_last_attempted_head = head;
    string probe = CaptureProbe();
    bool stable = SourceSafe();
    bool matching_head = captured_m1_usable && (head == 0 || captured_m1_head == head);
    if(Record(CollectorEventName(action), condition, head, probe, stable, 0))
    {
        incomplete_snapshot = !snapshot_complete || !matching_head || !stable;
        if(stable && matching_head && captured_m1_head >= g_last_head)
        { g_has_snapshot = true; g_last_head = captured_m1_head; }
    }
    else incomplete_snapshot = true;
    if(!stable) { EventKillTimer(); ExpertRemove(); }
}

int OnInit()
{
    if(TimerSeconds < 1 || TimerSeconds > 60 || SampleBars < 2 || SampleBars > 128 || RetryEveryPolls < 1 || RetryEveryPolls > 120)
    { Print("MoneyWay collector: invalid technical settings."); return INIT_PARAMETERS_INCORRECT; }
    if(!SourceSafe())
    { Print("MoneyWay collector: requires OANDA_Global-Demo-1 demo, expected company, non-tester and Algo Trading DISABLED."); return INIT_FAILED; }
    lock_handle = FileOpen(ROOT + "collector.lock", FILE_READ | FILE_WRITE | FILE_BIN | FILE_COMMON);
    if(lock_handle == INVALID_HANDLE)
    { Print("MoneyWay collector: single-writer lock unavailable; close another collector or inspect filesystem access."); return INIT_FAILED; }
    if(!RecoverSequence())
    { Print("MoneyWay collector: journal recovery failed; inspect filename/scan bounds and last-record read/hash access."); FileClose(lock_handle); lock_handle = INVALID_HANDLE; return INIT_FAILED; }
    if(!EventSetTimer(TimerSeconds))
    { Print("MoneyWay collector: timer setup failed; error ", GetLastError()); EventKillTimer(); FileClose(lock_handle); lock_handle = INVALID_HANDLE; return INIT_FAILED; }
    running = true;
    PollOnce();
    return INIT_SUCCEEDED;
}

void OnTimer() { PollOnce(); }

void OnDeinit(const int reason)
{
    EventKillTimer();
    bool was_running = running;
    running = false;
    if(was_running && lock_handle != INVALID_HANDLE)
        Record("shutdown", last_condition, g_last_head, "", false, reason);
    if(lock_handle != INVALID_HANDLE) { FileClose(lock_handle); lock_handle = INVALID_HANDLE; }
}
