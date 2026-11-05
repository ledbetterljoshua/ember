#!/bin/sh
# Round trip for lib/see.fs: decompile every colon word in core.fs and
# lib/see.fs, compile each printout again under a new name, and compare the
# two bodies cell by cell.
cd "$(dirname "$0")/.." || exit 1
make -s ember || exit 1
tmp=$(mktemp -d) || exit 1
trap 'rm -rf "$tmp"' EXIT
run() { ./ember core.fs lib/see.fs "$@" 2>&1; }

cat > "$tmp/names.fs" <<'EOF'
: NAMES LATEST @ BEGIN ?DUP WHILE
    DUP HIDDEN? 0= OVER >CFA @ DOCOL = AND OVER DICT0 U< 0= AND
    IF DUP ID. CR THEN @ REPEAT ;
NAMES
EOF
run "$tmp/names.fs" | grep -vx NAMES | sort -u > "$tmp/names"
sed 's/^/SEE /' "$tmp/names" > "$tmp/see.fs"
run "$tmp/see.fs" > "$tmp/seen"

# ": NAME body ;" becomes ": RTn body ;". core has no ['], which SEE prints.
# SEE prints the kernel's ' (still inside words compiled before core.fs
# replaced it) as ', so K' compiles that one back.
echo ": ['] IMMEDIATE WORD FIND >CFA ' LIT , , ;" > "$tmp/defs.fs"
echo ": K' IMMEDIATE KTICK , WORD FIND >CFA , ;" >> "$tmp/defs.fs"
paste -d'\t' "$tmp/names" "$tmp/seen" | awk -F'\t' -v map="$tmp/map" '
  { pre = ": " $1 " "
    if (index($2, pre) != 1) next
    body = " " substr($2, length(pre) + 1)
    gsub(" \047 ", " K\047 ", body)
    printf(": RT%d%s\n", NR, body)
    print NR "\t" $1 > map }' >> "$tmp/defs.fs"

# A printout that can't be compiled again (a word that used [COMPILE]) eats
# the input after it. Drop such lines one at a time until 42 gets through.
i=3
while [ "$i" -le "$(wc -l < "$tmp/defs.fs")" ]; do
  { head -"$i" "$tmp/defs.fs"; printf '\n42 . CR\n'; } > "$tmp/part.fs"
  if [ "$( (run "$tmp/part.fs") 2>/dev/null | tail -1)" = "42 " ]; then i=$((i + 1)); else
    echo "cannot recompile: $(sed -n "${i}p" "$tmp/defs.fs")"
    sed -i '' "${i}d" "$tmp/defs.fs"
  fi
done

cat > "$tmp/cmp.fs" <<'EOF'
: SPAN ( h -- body cells ) >CFA 8+ DUP DUP SCAN END @ SWAP - 8 / ;
: SAME? ( h1 h2 -- f )
    SPAN ROT SPAN ROT OVER <> IF 2DROP DROP FALSE EXIT THEN
    0 DO 2DUP I CELLS + @ SWAP I CELLS + @ <> IF 2DROP FALSE UNLOOP EXIT THEN
    LOOP 2DROP TRUE ;
VARIABLE OK  VARIABLE BAD
: CHK ( "orig" "rt" -- )
    WORD FIND WORD FIND 2DUP AND 0= IF 2DROP EXIT THEN
    2DUP SAME? IF 2DROP 1 OK +! ELSE 1 BAD +! ." differs: " DROP ID. CR THEN ;
EOF
awk -F'\t' '{ print "CHK " $2 " RT" $1 }' "$tmp/map" > "$tmp/chk.fs"
echo 'OK @ . ." identical, " BAD @ . ." differ" CR' >> "$tmp/chk.fs"
run "$tmp/defs.fs" "$tmp/cmp.fs" "$tmp/chk.fs"
