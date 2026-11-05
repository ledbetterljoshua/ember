#!/bin/sh
# Each tests/NAME.fs runs after core.fs; stdout+stderr must match tests/NAME.expected.
cd "$(dirname "$0")/.." || exit 1
fail=0
# tests/NAME.next1, .next2, ... are read after NAME.fs as further sources.
for t in tests/*.fs; do
  exp="${t%.fs}.expected"
  set -- "$t"
  for x in "${t%.fs}".next*; do [ -e "$x" ] && set -- "$@" "$x"; done
  if ./ember core.fs "$@" 2>&1 | diff -u "$exp" - > /dev/null; then
    echo "ok   $t"
  else
    echo "FAIL $t"; ./ember core.fs "$@" 2>&1 | diff -u "$exp" - | head -20; fail=1
  fi
done
exit $fail
