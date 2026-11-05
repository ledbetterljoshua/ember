\ An unknown word inside a definition used to be dropped while the rest
\ compiled, so FOO existed without BAR. Now FOO is forgotten.
: FOO 1 BAR 2 ;
FOO
: SQ DUP * ; 7 SQ . CR
\ A multi-line definition with an error used to keep compiling the lines
\ after it. Now the rest of it, up to its ";", is skipped.
: BAD
  1 2 NOSUCH
  3 4 + . ;
5 6 + . CR
\ An error empties the stack.
1 2 3 NOPE
DEPTH . CR
\ Underflow is reported and repaired, instead of DEPTH going negative.
DROP
DROP
DEPTH . CR
.S CR
\ The forgotten definition gives its space back.
VARIABLE H0 HERE @ H0 !
: JUNK 1 2 OOPS ;
HERE @ H0 @ = . CR
: CUBE DUP DUP * * ; 3 CUBE . CR
