#!/bin/sh
# Each tests/NAME.fs runs after core.fs; stdout+stderr must match tests/NAME.expected.
# A first line "\ needs: lib/a.fs ..." loads those files between the two.
cd "$(dirname "$0")/.." || exit 1
fail=0
# tests/NAME.next1, .next2, ... are read after NAME.fs as further sources.
# The exit status must be 0, or the number in tests/NAME.exit.
tmp=$(mktemp) || exit 1
trap 'rm -f "$tmp"' EXIT
for t in tests/*.fs; do
  exp="${t%.fs}.expected"
  want=0; [ -e "${t%.fs}.exit" ] && want=$(cat "${t%.fs}.exit")
  needs=$(sed -n '1s/^\\ needs: //p' "$t")
  set -- "$t"
  for x in "${t%.fs}".next*; do [ -e "$x" ] && set -- "$@" "$x"; done
  got=$( (./ember core.fs $needs "$@" > "$tmp" 2>&1; echo $?) 2>/dev/null )
  if diff -u "$exp" "$tmp" > /dev/null && [ "$got" = "$want" ]; then
    echo "ok   $t"
  else
    echo "FAIL $t"; diff -u "$exp" "$tmp" | head -20
    [ "$got" = "$want" ] || echo "     exit status $got, expected $want"; fail=1
  fi
done
# The REPL at a terminal: read tests/repl.lines through a pseudo-terminal.
if command -v python3 > /dev/null; then
  set --
  while IFS= read -r line; do set -- "$@" "$line"; done < tests/repl.lines
  if python3 -I tests/pty.py "$@" | diff -u tests/repl.expected - > /dev/null; then
    echo "ok   tests/repl.lines (pty)"
  else
    echo "FAIL tests/repl.lines (pty)"; python3 -I tests/pty.py "$@" | diff -u tests/repl.expected - | head -20; fail=1
  fi
fi
exit $fail
