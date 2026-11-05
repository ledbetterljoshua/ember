( lib/see.fs: SEE that rebuilds control structures. Load after core.fs.

  Core's SEE prints a colon definition's cells as they are, so IF reads as
  0BRANCH ( 56 ). The compiled shapes are regular enough to read back:

    IF a THEN              0BRANCH >t  a                  t:
    IF a ELSE b THEN       0BRANCH >t  a  BRANCH >u   t:  b   u:
    BEGIN a UNTIL       b: a  0BRANCH <b
    BEGIN a AGAIN       b: a  BRANCH <b
    BEGIN a WHILE b REPEAT
                        b: a  0BRANCH >t  b  BRANCH <b   t:
    DO a LOOP              SWAP >R >R  b: a  (LOOP) <b  UNLOOP

  A first pass marks where BEGIN, DO and THEN go, and which branches are
  ELSE, WHILE and REPEAT; a second prints. A branch that fits none of the
  shapes prints raw, as before. S" ..." TELL reads back as ." ...". )

HERE @ 64 3 * CELLS ALLOT CONSTANT MARKS   ( entries: addr kind arg )
VARIABLE #MARKS
VARIABLE END
: K-BEGIN 1 ;  : K-THEN 2 ;  : K-DO 3 ;  : K-ELSE 4 ;  : K-REPEAT 5 ;  : K-WHILE 6 ;

: MARK ( kind addr -- )
    #MARKS @ 63 > IF 2DROP EXIT THEN
    #MARKS @ 3 * CELLS MARKS + DUP >R ! R> 8+ ! 1 #MARKS +! ;
: ENTRY@ ( i -- e ) 3 * CELLS MARKS + ;
: MARKS@ ( addr kind -- n )   ( how many marks of this kind at addr )
    0 0 BEGIN DUP #MARKS @ < WHILE
        DUP ENTRY@ DUP @ 5 PICK = SWAP 8+ @ 4 PICK = AND
        IF SWAP 1+ SWAP THEN 1+
    REPEAT DROP NIP NIP ;
: MARK? ( addr kind -- f ) MARKS@ 0<> ;

( The kernel's ' compiles the next cell as data. core.fs replaced it with a
  Forth ' by the same name, but words compiled earlier, like IF, still hold
  the kernel's. Find it as the "'" header whose codeword isn't DOCOL. )
: KERNEL' ( -- xt )
    LATEST @ BEGIN ?DUP WHILE
        DUP 8+ C@ F_LENMASK AND 1 = OVER 9 + C@ [ CHAR ' ] LITERAL = AND
        OVER >CFA @ DOCOL <> AND IF >CFA EXIT THEN @
    REPEAT 0 ;
KERNEL' CONSTANT KTICK

: TARGET ( addr -- t ) 8+ DUP @ + ;
: XT? ( addr xt -- f ) SWAP @ = ;
: STEP ( addr -- addr' )   ( to the next cell that is a word, not data )
    DUP @ DUP ' LIT = OVER KTICK = OR SWAP BRANCHY? OR IF 16 + EXIT THEN
    DUP ' LITSTRING XT? IF 8+ DUP @ SWAP 8+ + ALIGNED EXIT THEN 8+ ;
: ON-CELL? ( from addr -- f )   ( does stepping from from land on addr? )
    SWAP BEGIN 2DUP > WHILE STEP REPEAT = ;
: DO? ( b -- f )
    DUP 24 - ' SWAP XT? OVER 16 - ' >R XT? AND SWAP 8 - ' >R XT? AND ;

( Pass one. )
: NOTE-0BRANCH ( addr -- )
    DUP TARGET 2DUP < 0= IF NIP K-BEGIN SWAP MARK EXIT THEN     ( UNTIL )
    ( addr t ) OVER 16 + OVER 16 - 2DUP > 0= -ROT ON-CELL? AND
    OVER 16 - ' BRANCH XT? AND IF
        16 - DUP TARGET ( addr br bt )
        DUP 3 PICK > 0= IF                                     ( WHILE )
            K-BEGIN SWAP MARK K-REPEAT SWAP MARK K-WHILE SWAP MARK EXIT THEN
        DROP K-ELSE SWAP MARK DROP EXIT THEN                    ( IF ELSE )
    NIP K-THEN SWAP MARK ;                                      ( IF THEN )
: NOTE-BRANCH ( addr -- )
    DUP K-ELSE MARK? IF TARGET K-THEN SWAP MARK EXIT THEN
    DUP K-REPEAT MARK? IF DROP EXIT THEN
    DUP TARGET 2DUP > IF NIP K-BEGIN SWAP MARK EXIT THEN 2DROP ;  ( AGAIN )
: NOTE-LOOP ( addr -- ) TARGET DUP DO? IF 24 - K-DO SWAP MARK ELSE DROP THEN ;
: NOTE ( addr -- )
    DUP ' 0BRANCH XT? IF NOTE-0BRANCH EXIT THEN
    DUP ' BRANCH XT? IF NOTE-BRANCH EXIT THEN
    DUP ' (LOOP) XT? OVER ' (+LOOP) XT? OR IF NOTE-LOOP EXIT THEN DROP ;
: SCAN ( addr -- )   ( same extent as SEE-BODY: to an EXIT past every branch )
    0 #MARKS ! 0 FAR !
    BEGIN DUP @ ' EXIT = OVER FAR @ >= AND NOT WHILE
        DUP @ BRANCHY? IF DUP TARGET FAR @ MAX FAR ! DUP NOTE THEN STEP
    REPEAT END ! ;

( Pass two. )
: .N ( n a len -- ) ROT BEGIN ?DUP WHILE >R 2DUP TELL SPACE R> 1- REPEAT 2DROP ;
: PRE ( addr -- )
    DUP K-THEN MARKS@ S" THEN" .N  K-BEGIN MARKS@ S" BEGIN" .N ;
: S-STRING ( addr -- addr' )   ( LITSTRING, folding a following TELL into ." )
    DUP 8+ @ OVER 16 + + ALIGNED DUP ' TELL XT? IF
        ." ." '"' EMIT SPACE SWAP DUP 16 + SWAP 8+ @ TELL '"' EMIT SPACE 8+ EXIT
    THEN DROP SEE-CELL ;
: S-CELL ( addr -- addr' )
    DUP K-DO MARK? IF ." DO " 24 + EXIT THEN
    DUP K-WHILE MARK? IF ." WHILE " 16 + EXIT THEN
    DUP K-ELSE MARK? IF ." ELSE " 16 + EXIT THEN
    DUP K-REPEAT MARK? IF ." REPEAT " 16 + EXIT THEN
    DUP ' 0BRANCH XT? IF
        DUP TARGET OVER > IF ." IF " ELSE ." UNTIL " THEN 16 + EXIT THEN
    DUP ' BRANCH XT? IF
        DUP TARGET OVER < IF ." AGAIN " 16 + EXIT THEN SEE-CELL EXIT THEN
    DUP ' (LOOP) XT? OVER ' (+LOOP) XT? OR IF
        DUP TARGET DO? IF
            DUP ' (LOOP) XT? IF ." LOOP " ELSE ." +LOOP " THEN 16 +
            DUP ' UNLOOP XT? IF 8+ THEN EXIT THEN
        SEE-CELL EXIT THEN
    DUP ' LITSTRING XT? IF S-STRING EXIT THEN
    SEE-CELL ;
: SEE-STRUCT ( addr -- )
    DUP SCAN BEGIN DUP END @ < WHILE DUP PRE S-CELL REPEAT PRE ';' EMIT CR ;

( core's SEE and SEE-DOES call SEE-BODY; point those cells at SEE-STRUCT. )
: REPOINT ( new old xt -- )
    200 0 DO 8+ 2DUP @ = IF >R OVER R@ ! R> THEN LOOP DROP 2DROP ;
' SEE-STRUCT ' SEE-BODY ' SEE REPOINT
' SEE-STRUCT ' SEE-BODY ' SEE-DOES REPOINT
