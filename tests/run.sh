#!/bin/sh
# Each tests/NAME.fs runs after core.fs; stdout+stderr must match tests/NAME.expected.
# A first line "\ needs: lib/a.fs ..." loads those files between the two.
cd "$(dirname "$0")/.." || exit 1
fail=0
for t in tests/*.fs; do
  exp="${t%.fs}.expected"
  needs=$(sed -n '1s/^\\ needs: //p' "$t")
  if ./ember core.fs $needs "$t" 2>&1 | diff -u "$exp" - > /dev/null; then
    echo "ok   $t"
  else
    echo "FAIL $t"; ./ember core.fs $needs "$t" 2>&1 | diff -u "$exp" - | head -20; fail=1
  fi
done
exit $fail
