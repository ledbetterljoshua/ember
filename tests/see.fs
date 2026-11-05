\ needs: lib/see.fs
: A1 DUP 0< IF NEGATE THEN ;
: A2 0> IF 1 ELSE 2 THEN 3 ;
: A3 BEGIN DUP WHILE 1- REPEAT DROP ;
: A4 BEGIN 1- DUP 0= UNTIL ;
: A5 BEGIN KEY EMIT AGAIN ;
: A6 10 0 DO I . LOOP ;
: A7 0 100 DO I . -10 +LOOP ;
: A8 IF IF 1 ELSE 2 THEN ELSE 3 THEN ;
: A9 ." hi there" CR ;
: A10 4 0 DO 3 0 DO I J * . LOOP CR LOOP ;
: A11 BEGIN DUP 0> WHILE DUP 2 MOD IF ." odd" ELSE ." even" THEN 1- REPEAT DROP ;
: A12 DUP IF EXIT THEN DROP ;
SEE A1 SEE A2 SEE A3 SEE A4 SEE A5 SEE A6
SEE A7 SEE A8 SEE A9 SEE A10 SEE A11 SEE A12
\ A branch that fits no shape still prints raw.
: ODD [ ' BRANCH , 8 , ] 1 ; SEE ODD
\ core's own comment word reads back as its source.
SEE (
\ DOES> bodies go through the same printer.
: CNT <BUILDS , DOES> @ 0 DO I . LOOP ; 3 CNT C3 SEE CNT SEE C3
DEPTH . CR
