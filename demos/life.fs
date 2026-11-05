( Conway's Life on a 64x32 torus, drawn two rows per line with half blocks. )
64 CONSTANT W   32 CONSTANT H
HERE @ W H * ALLOT CONSTANT GRID
HERE @ W H * ALLOT CONSTANT NEW
GRID W H * 0 FILL

: CELL ( x y -- addr ) H + H MOD W * SWAP W + W MOD + GRID + ;
: ALIVE? ( x y -- 0|1 ) CELL C@ ;
: BIRTH ( x y -- ) CELL 1 SWAP C! ;

VARIABLE CX  VARIABLE CY
: NEIGHBORS ( x y -- n )
    CY ! CX ! 0
    2 -1 DO 2 -1 DO
        I J OR IF CX @ I + CY @ J + ALIVE? + THEN
    LOOP LOOP ;

: STEP
    H 0 DO W 0 DO
        I J NEIGHBORS
        I J ALIVE? IF DUP 2 = SWAP 3 = OR ELSE 3 = THEN 1 AND
        I J CELL GRID - NEW + C!
    LOOP LOOP
    NEW GRID W H * CMOVE ;

: HALF ( top bottom -- ) 2 * +
    DUP 0= IF DROP SPACE EXIT THEN
    226 EMIT 150 EMIT
    DUP 1 = IF DROP 128 EMIT EXIT THEN
    2 = IF 132 ELSE 136 THEN EMIT ;

: ESC 27 EMIT ;
: SHOW ESC ." [H"
    H 0 DO W 0 DO I J ALIVE? I J 1+ ALIVE? HALF LOOP CR 2 +LOOP ;

( Gosper's glider gun. On a torus its gliders come back around and hit it. )
: GUN ( x y -- ) CY ! CX !
    24 0  22 1  24 1  12 2  13 2  20 2  21 2  34 2  35 2
    11 3  15 3  20 3  21 3  34 3  35 3   0 4   1 4  10 4
    16 4  20 4  21 4   0 5   1 5  10 5  14 5  16 5  17 5
    22 5  24 5  10 6  16 6  24 6  11 7  15 7  12 8  13 8
    36 0 DO CY @ + SWAP CX @ + SWAP BIRTH LOOP ;

VARIABLE GENS  300 GENS !
: LIFE
    ESC ." [2J" ESC ." [?25l"
    GENS @ 0 DO SHOW ." generation " I . STEP 30 MS LOOP
    ESC ." [?25h" ;

2 2 GUN LIFE
