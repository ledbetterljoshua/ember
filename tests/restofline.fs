\ After an error at top level the rest of that line is dropped, as in most
\ Forths. It used to run, so one mistake printed a cascade of errors.
NOPE 1 2 + . CR
3 4 + . CR
\ When the bad word ends its line, the next line is untouched.
NOPE
5 6 + . CR
DROP 99 . CR
7 . CR
\ Inside a definition, the rest of the definition goes instead, across lines.
: BAD 1 NOPE
  2 3 ;
8 . CR
: CHECK ( n -- ) 0= IF ABORT THEN ;
0 CHECK 100 . CR
9 . CR
