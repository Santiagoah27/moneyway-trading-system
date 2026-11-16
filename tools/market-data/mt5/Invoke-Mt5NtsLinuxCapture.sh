#!/bin/sh
# Private one-session protocol. All output values are bound to the coordinator's random ID.
set -eu
sid=$1
query_seconds=$2
case "$sid" in *[!0-9a-f]*|'') exit 92 ;; esac
[ "${#sid}" -eq 64 ]
case "$query_seconds" in *[!0-9]*|'') exit 92 ;; esac
[ "$query_seconds" -ge 1 ] && [ "$query_seconds" -le 60 ]
dir=/tmp/mw-nts-$sid
[ -d "$dir" ] && [ ! -L "$dir" ]
query_pid=
cleanup() {
    if [ -n "$query_pid" ]; then
        kill "$query_pid" 2>/dev/null || true
        wait "$query_pid" 2>/dev/null || true
    fi
    rm -rf -- "$dir"
}
trap cleanup EXIT
trap 'exit 93' HUP INT TERM

# Check the installed, previously exercised profile before any network query.
[ "$(dpkg-query -W -f='${Version}' chrony)" = '4.5-1ubuntu4.2' ]
[ "$(sha256sum /usr/sbin/chronyd | cut -d' ' -f1)" = '7a834e478d8a904c39a348606a5d0ac58fd1ccbca24f333a8170c786af8ca508' ]
[ "$(sha256sum /usr/bin/chronyc | cut -d' ' -f1)" = '271ea54c67206559437bd299d0e08c9e3d42ee6be322b1722e76311cd5d9df8d' ]
[ "$(cat /proc/1/comm)" != systemd ]
grep -qx 'systemd=false' /etc/wsl.conf
set +e
/usr/sbin/policy-rc.d chrony start
policy_result=$?
set -e
[ "$policy_result" -eq 101 ]
for unit in chrony chronyd systemd-timesyncd ntp ntpd ntpsec openntpd phc2sys ptp4l; do
    [ "$(readlink /etc/systemd/system/$unit.service)" = /dev/null ]
done
if ps -eo comm= | grep -Eq '^(chronyd|ntpd|systemd-timesyn|ptp4l|phc2sys)$'; then exit 94; fi

cat > "$dir/chrony.conf" <<EOF
server time.cloudflare.com nts iburst
authselectmode require
port 0
cmdport 0
bindcmdaddress $dir/chronyd.sock
pidfile $dir/chronyd.pid
log selection
logdir $dir
EOF
chown _chrony:_chrony "$dir" "$dir/chrony.conf"
chmod 700 "$dir"
chmod 600 "$dir/chrony.conf"
cfg=$(sha256sum "$dir/chrony.conf" | cut -d' ' -f1)
observe() {
    phase=$1
    read -r command received
    [ "$command" = "$phase" ] && [ "$received" = "$sid" ]
    read -r mono_before ignored < /proc/uptime
    realtime=$(date -u +%s.%N)
    read -r mono_after ignored < /proc/uptime
    printf 'OBS|%s|%s|%s|%s|%s\n' "$sid" "$phase" "$mono_before" "$realtime" "$mono_after"
}
printf 'READY|%s|%s\n' "$sid" "$cfg"
observe BEFORE
read -r command received
[ "$command" = RUN ] && [ "$received" = "$sid" ]
# A newly staged private directory must never supply a previous log.
[ ! -e "$dir/selection.log" ] && [ ! -L "$dir/selection.log" ]
: > "$dir/authdata.txt"
: > "$dir/ntpdata.txt"
: > "$dir/capabilities.txt"
: > "$dir/monitor.stderr"
read -r query_start ignored < /proc/uptime
query_start_real=$(date -u +%s.%N)
timeout --signal=TERM --kill-after=2s "$((query_seconds + 5))s" \
    setpriv --bounding-set=-all --reuid=_chrony --regid=_chrony --init-groups --no-new-privs \
    /usr/sbin/chronyd -Q -t "$query_seconds" -f "$dir/chrony.conf" \
    > "$dir/client.stdout" 2> "$dir/client.stderr" &
query_pid=$!
i=0
while kill -0 "$query_pid" 2>/dev/null && [ "$i" -lt "$((query_seconds * 5))" ]; do
    [ ! -e "$dir/cancel" ] || exit 95
    i=$((i + 1))
    if [ -s "$dir/chronyd.pid" ] && [ ! -s "$dir/capabilities.txt" ]; then
        pid=$(cat "$dir/chronyd.pid")
        if [ -r "/proc/$pid/status" ]; then
            grep -E '^(Uid|CapEff|CapBnd|NoNewPrivs):' "/proc/$pid/status" > "$dir/capabilities.txt"
        fi
    fi
    if [ -S "$dir/chronyd.sock" ]; then
        # Pair observations from this private socket before the instance exits.
        timeout 1s /usr/bin/chronyc -n -h "$dir/chronyd.sock" authdata -a > "$dir/auth.tmp" 2>> "$dir/monitor.stderr" || true
        timeout 1s /usr/bin/chronyc -n -h "$dir/chronyd.sock" ntpdata > "$dir/ntp.tmp" 2>> "$dir/monitor.stderr" || true
        if grep -Eq '^Authenticated[[:space:]]+: Yes' "$dir/ntp.tmp"; then
            cp "$dir/auth.tmp" "$dir/authdata.txt"
            cp "$dir/ntp.tmp" "$dir/ntpdata.txt"
        elif [ ! -s "$dir/ntpdata.txt" ]; then
            cp "$dir/auth.tmp" "$dir/authdata.txt"
            cp "$dir/ntp.tmp" "$dir/ntpdata.txt"
        fi
    fi
    sleep 0.2
done
set +e
wait "$query_pid"
result=$?
set -e
query_pid=
query_end_real=$(date -u +%s.%N)
read -r query_end ignored < /proc/uptime
if [ -s "$dir/monitor.stderr" ]; then
    printf '\nMonitoring stderr:\n' >> "$dir/client.stderr"
    cat "$dir/monitor.stderr" >> "$dir/client.stderr"
fi
printf 'QUERYDONE|%s|%s\n' "$sid" "$result"
observe AFTER
# Remove the local interface address from retained client output.
sed -i 's/^Local address[[:space:]]*:.*$/Local address   : [withheld]/' "$dir/ntpdata.txt"
encode() { base64 -w0 "$1"; }
# Retain the complete log only after the bounded writer has terminated.
selection_state=missing
selection_hash=-
selection_data=
if [ -f "$dir/selection.log" ] && [ ! -L "$dir/selection.log" ]; then
    selection_state=present
    selection_size=$(wc -c < "$dir/selection.log")
    if [ "$selection_size" -le 65536 ]; then
        selection_hash=$(sha256sum "$dir/selection.log" | cut -d' ' -f1)
        selection_data=$(encode "$dir/selection.log")
    else
        selection_state=oversized
    fi
fi
printf 'SELECTION|%s|%s|%s|%s|%s|%s|%s|%s|' "$sid" "$selection_state" "$selection_hash" "$selection_data" "$query_start" "$query_end" "$query_start_real" "$query_end_real"
printf '/usr/sbin/chronyd -Q -t %s -f %s/chrony.conf' "$query_seconds" "$dir" | base64 -w0
printf '|'
base64 -w0 /etc/os-release
printf '\n'
printf 'RESULT|%s|%s|%s|' "$sid" "$result" "$cfg"
encode "$dir/chrony.conf"; printf '|'
encode "$dir/authdata.txt"; printf '|'
encode "$dir/ntpdata.txt"; printf '|'
encode "$dir/capabilities.txt"; printf '|'
encode "$dir/client.stderr"; printf '|'
encode "$dir/client.stdout"; printf '\n'
read -r command received
[ "$command" = ACK ] && [ "$received" = "$sid" ]
if ps -eo comm= | grep -Eq '^(chronyd|ntpd|systemd-timesyn|ptp4l|phc2sys)$'; then exit 96; fi
printf 'DONE|%s\n' "$sid"
