#!/bin/sh
# Run each bench/*.fs RUNS times (default 20) and report median CPU cycles
# and instructions retired, from macOS `time -l`. Cycles hold steady when the
# machine is busy; wall time does not.
# Arguments are kernel checkouts, each with its own built ember and core.fs.
# With several, runs interleave so load drift hits them all alike:
#   bench/run.sh                  # this checkout
#   bench/run.sh ../base .        # e.g. a master worktree against this one
here="$(cd "$(dirname "$0")/.." && pwd)"
[ $# -eq 0 ] && set -- "$here"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
printf '%-8s %14s %14s  %s\n' bench 'median cycles' instructions checkout
for b in "$here"/bench/*.fs; do
  i=0
  while [ $i -lt "${RUNS:-20}" ]; do
    n=0
    for k in "$@"; do
      n=$((n+1))
      /usr/bin/time -l "$k/ember" "$k/core.fs" "$b" 2>&1 >/dev/null |
        awk '/cycles elapsed/{c=$1} /instructions retired/{r=$1} END{print c, r}' >> "$tmp/$n"
    done
    i=$((i+1))
  done
  n=0
  for k in "$@"; do
    n=$((n+1))
    [ "$k" = "$here" ] && k=.
    sort -n "$tmp/$n" | awk -v b="$(basename "$b" .fs)" -v k="$k" \
      '{c[NR]=$1; r[NR]=$2} END{m=int((NR+1)/2); printf "%-8s %14d %14d  %s\n", b, c[m], r[m], k}'
    rm "$tmp/$n"
  done
done
