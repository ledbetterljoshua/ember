\ Counted loops: nesting, I and J, +LOOP both ways, LEAVE, early EXIT.
: T 4 1 DO I 4 0 DO DUP I * 4 .R LOOP DROP CR LOOP ; T
: N 3 0 DO 3 0 DO I J * . LOOP LOOP ; N CR
: D 0 10 DO I . -3 +LOOP ; D CR
: D2 -3 3 DO I . -1 +LOOP ; D2 CR
: U 10 0 DO I . 2 +LOOP ; U CR
: L 10 0 DO I DUP . 4 = IF LEAVE THEN LOOP ; L CR
: L2 100 0 DO I . I 6 > IF LEAVE THEN 3 +LOOP ; L2 CR
: E 0 0 DO I . LOOP ; E CR
: F 10 0 DO I 3 = IF I UNLOOP EXIT THEN LOOP 99 ; F . CR
: SUM 0 1001 1 DO I + LOOP ; SUM . CR
DEPTH . CR
\ SEE prints the inline offset after (LOOP) and (+LOOP), like after BRANCH.
SEE U
SEE SUM
