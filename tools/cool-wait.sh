#!/usr/bin/env bash
# Thermal gate for heavy jobs (tests, shots, benchmarks).
#
# This laptop's EC hard-powers the machine off about 2.5 minutes after it raises
# its thermal alarm (`asus_wmi: Unknown key code 0x6d` in the kernel log), so a
# long render or test sweep must not start on an already-hot machine. This waits
# for the sensors to drop, then gets out of the way.
#
# Knobs (environment):
#   COOL_READY     start once every sensor is at or below this, in C  (72)
#   COOL_TIMEOUT   seconds to wait before running anyway            (600)
#   COOL_POLL      seconds between samples                          (5)
#
# The dGPU is included because mise.toml renders through it
# (__NV_PRIME_RENDER_OFFLOAD), and its heat shares the CPU's heatsink.
set -euo pipefail

READY=${COOL_READY:-72}
TIMEOUT=${COOL_TIMEOUT:-600}
POLL=${COOL_POLL:-5}

# sysfs temperatures are milli-degrees into these helpers.
zone_temp() { # $1 = thermal zone type
  local f
  for f in /sys/class/thermal/thermal_zone*/type; do
    if [ "$(cat "$f" 2>/dev/null)" = "$1" ]; then
      printf '%d\n' $(( $(cat "${f%type}temp" 2>/dev/null || echo 0) / 1000 ))
      return 0
    fi
  done
}

hwmon_temp() { # $1 = hwmon name
  local d
  for d in /sys/class/hwmon/hwmon*; do
    if [ "$(cat "$d/name" 2>/dev/null)" = "$1" ]; then
      printf '%d\n' $(( $(cat "$d/temp1_input" 2>/dev/null || echo 0) / 1000 ))
      return 0
    fi
  done
}

cpu_temp() { # hottest of the die, the ACPI zone and the chipset
  local max=0 v
  for v in "$(zone_temp x86_pkg_temp)" "$(zone_temp acpitz)" "$(hwmon_temp pch_skylake)"; do
    [[ $v =~ ^[0-9]+$ ]] && [ "$v" -gt "$max" ] && max=$v
  done
  echo "$max"
}

gpu_temp() {
  local t
  t=$(nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits 2>/dev/null | head -1) || return 0
  [[ $t =~ ^[0-9]+$ ]] && echo "$t"
}

started=$(date +%s)
while :; do
  cpu=$(cpu_temp)
  gpu=$(gpu_temp)
  if [ "${cpu:-0}" -le 0 ] && [ -z "$gpu" ]; then
    echo "cool-wait: no readable temperature sensors, running anyway" >&2
    exit 0
  fi
  if [ "${cpu:-0}" -le "$READY" ] && { [ -z "$gpu" ] || [ "$gpu" -le "$READY" ]; }; then
    printf 'cool-wait: cpu %sC%s, ok to run\n' "$cpu" "${gpu:+, gpu ${gpu}C}"
    exit 0
  fi
  waited=$(( $(date +%s) - started ))
  if [ "$waited" -ge "$TIMEOUT" ]; then
    printf 'cool-wait: still warm after %ss (cpu %sC%s), running anyway\n' \
      "$waited" "$cpu" "${gpu:+, gpu ${gpu}C}" >&2
    exit 0
  fi
  printf 'cool-wait: cpu %sC%s, too hot (want <= %sC), waiting %ss/%ss\n' \
    "$cpu" "${gpu:+, gpu ${gpu}C}" "$READY" "$waited" "$TIMEOUT" >&2
  sleep "$POLL"
done
