#!/bin/sh
# Check lib/disasm.fs against objdump, line by line:
#  1. ember's whole __text section, disassembled in place from memory;
#  2. lib/disasm-forms.s, instruction forms the kernel doesn't use yet.
cd "$(dirname "$0")/.." || exit 1
make -s ember || exit 1
tmp=$(mktemp -d) || exit 1
trap 'rm -rf "$tmp"' EXIT
norm='s/[[:space:]]*;.*$//; s/ <[^>]*>//g; s/[[:space:]]+/ /g; s/^ //; s/ $//'
rc=0

objdump -d --no-show-raw-insn ember | grep -E '^[0-9a-f]+:' | sed -E "$norm" > "$tmp/want1"
echo OBJDUMP-TEXT | ./ember core.fs lib/disasm.fs - | sed -E "$norm" > "$tmp/got1"
if diff -u "$tmp/want1" "$tmp/got1"; then
  echo "ok   ember __text: $(wc -l < "$tmp/want1" | tr -d ' ') instructions match objdump"
else rc=1; fi

clang -c -arch arm64 lib/disasm-forms.s -o "$tmp/forms.o" || exit 1
objdump -d "$tmp/forms.o" | grep -E '^ *[0-9a-f]+:' > "$tmp/raw2"
sed -E 's/^ *([0-9a-f]+): ([0-9a-f]{8}).*/\1: \2/' "$tmp/raw2" > "$tmp/words2"
sed -E 's/^ *([0-9a-f]+): [0-9a-f]{8}/\1:/' "$tmp/raw2" | sed -E "$norm" > "$tmp/want2"
{ echo ': X ( insn pc -- ) DUP (HX) ." : " PC ! INS ! 0 MCOL ! DECODE CR ;'
  echo '0 ORIGIN ! FALSE SYMBOLIC ! HEX'
  sed -E 's/^([0-9a-f]+): ([0-9a-f]+)/0\2 0\1 X/' "$tmp/words2"; } > "$tmp/forms.fs"
./ember core.fs lib/disasm.fs "$tmp/forms.fs" | sed -E "$norm" > "$tmp/got2"
if diff -u "$tmp/want2" "$tmp/got2"; then
  echo "ok   lib/disasm-forms.s: $(wc -l < "$tmp/want2" | tr -d ' ') instructions match objdump"
else rc=1; fi
exit $rc
