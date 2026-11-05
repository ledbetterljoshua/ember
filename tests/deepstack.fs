\ The guard pages leave the usable depths alone: 8000 cells on either stack
\ works, and an underflow within the slack above S0 is still caught
\ by ?STACK and recovered from.
: X 8000 0 DO 7 LOOP ; X DEPTH . CR
: CLEAR BEGIN DEPTH WHILE DROP REPEAT ; CLEAR DEPTH . CR
: R DUP IF 1- RECURSE THEN ; 8000 R . CR
: U 8000 0 DO DROP LOOP 5 ; U
3 CELLS . CR
