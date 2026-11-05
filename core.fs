\ core.fs: the rest of the language, written in itself.
\ Until "(" exists below, comments can only be backslash lines.

: / /MOD SWAP DROP ;
: MOD /MOD DROP ;
: CELLS 8 * ;
: TRUE -1 ;
: FALSE 0 ;
: NOT 0= ;
: BL 32 ;
: CR 10 EMIT ;
: SPACE BL EMIT ;

\ LITERAL compiles a number that was computed at compile time.
: LITERAL IMMEDIATE ' LIT , , ;

\ Character constants, computed once while compiling.
: ':' [ CHAR : ] LITERAL ;
: ';' [ CHAR ; ] LITERAL ;
: '(' [ CHAR ( ] LITERAL ;
: ')' [ CHAR ) ] LITERAL ;
: '"' [ CHAR " ] LITERAL ;
: '0' [ CHAR 0 ] LITERAL ;
: 'A' [ CHAR A ] LITERAL ;
: '-' [ CHAR - ] LITERAL ;
: '.' [ CHAR . ] LITERAL ;

\ [COMPILE] compiles an immediate word instead of running it.
: [COMPILE] IMMEDIATE WORD FIND >CFA , ;
: RECURSE IMMEDIATE LATEST @ >CFA , ;

\ Control flow. Each leaves a hole address on the stack at compile time
\ and patches it when the matching word arrives.
: IF IMMEDIATE ' 0BRANCH , HERE @ 0 , ;
: THEN IMMEDIATE DUP HERE @ SWAP - SWAP ! ;
: ELSE IMMEDIATE ' BRANCH , HERE @ 0 , SWAP DUP HERE @ SWAP - SWAP ! ;
: UNLESS IMMEDIATE ' NOT , [COMPILE] IF ;

: BEGIN IMMEDIATE HERE @ ;
: UNTIL IMMEDIATE ' 0BRANCH , HERE @ - , ;
: AGAIN IMMEDIATE ' BRANCH , HERE @ - , ;
: WHILE IMMEDIATE ' 0BRANCH , HERE @ 0 , ;
: REPEAT IMMEDIATE ' BRANCH , SWAP HERE @ - , DUP HERE @ SWAP - SWAP ! ;

\ Now the language can describe its own comments.
: ( IMMEDIATE
    1 BEGIN
        KEY DUP '(' = IF DROP 1+ ELSE ')' = IF 1- THEN THEN
    DUP 0= UNTIL DROP ;

( From here on, comments look like this. They nest ( like this ). )

: NIP ( a b -- b ) SWAP DROP ;
: TUCK ( a b -- b a b ) SWAP OVER ;
: PICK ( xn .. x0 n -- xn .. x0 xn ) 1+ CELLS DSP@ + @ ;
: ABS ( n -- |n| ) DUP 0< IF NEGATE THEN ;
: MIN ( a b -- min ) 2DUP > IF SWAP THEN DROP ;
: MAX ( a b -- max ) 2DUP < IF SWAP THEN DROP ;
: SPACES ( n -- ) BEGIN DUP 0> WHILE SPACE 1- REPEAT DROP ;
: ARSHIFT ( n u -- n' ) OVER 0< IF SWAP INVERT SWAP RSHIFT INVERT ELSE RSHIFT THEN ;
( Errors go to stderr, so a program writing binary to stdout stays intact. )
: >ERR FLUSH 2 OUT ! ;
: >OUT FLUSH 1 OUT ! ;
: DECIMAL 10 BASE ! ;
: HEX 16 BASE ! ;

( Counted loops: limit start DO ... LOOP. The index lives on the return stack. )
: DO IMMEDIATE ' SWAP , ' >R , ' >R , HERE @ ;
: LOOP IMMEDIATE ' (LOOP) , HERE @ - , ' UNLOOP , ;
: +LOOP IMMEDIATE ' (+LOOP) , HERE @ - , ' UNLOOP , ;
( LEAVE makes the current pass the last one. )
: LEAVE R> R> DROP R@ >R >R ;

( Unsigned division for d below 2^63. /MOD is signed, so halve u first to
  keep it positive, then correct the remainder, which is less than 2d. )
: U/MOD ( u d -- rem quot )
    >R DUP 1 RSHIFT R@ / 1 LSHIFT
    SWAP OVER R@ * -
    DUP R@ U< IF R> DROP ELSE R> - SWAP 1+ SWAP THEN SWAP ;

( Number output, in the current BASE. )
: U. ( u -- )
    BASE @ U/MOD ?DUP IF RECURSE THEN
    DUP 10 < IF '0' ELSE 10 - 'A' THEN + EMIT ;
: UWIDTH ( u -- digits ) 1 SWAP BEGIN BASE @ U/MOD NIP ?DUP WHILE SWAP 1+ SWAP REPEAT ;
: U.R ( u width -- ) SWAP DUP UWIDTH ROT SWAP - SPACES U. ;
: .R ( n width -- )
    SWAP DUP 0< IF NEGATE 1 SWAP ROT 1- ELSE 0 SWAP ROT THEN
    ( sign u width ) SWAP DUP UWIDTH ROT SWAP - SPACES
    SWAP IF '-' EMIT THEN U. ;
: . ( n -- ) DUP 0< IF '-' EMIT NEGATE THEN U. SPACE ;
: ? ( addr -- ) @ . ;
: DEPTH ( -- n ) S0 @ DSP@ - 8- 8 / ;
: .S ( -- ) [ CHAR < ] LITERAL EMIT DEPTH U. [ CHAR > ] LITERAL EMIT SPACE
    DSP@ S0 @ 8- BEGIN 2DUP <= WHILE DUP @ . 8- REPEAT 2DROP ;

( Memory. )
: C, ( c -- ) HERE @ C! 1 HERE +! ;
: ALIGNED ( addr -- addr' ) 7 + 7 INVERT AND ;
: ALIGN HERE @ ALIGNED HERE ! ;
: ALLOT ( n -- ) HERE +! ;

( Strings. S" leaves addr len; ." prints. Both work compiled or interpreted. )
: S" IMMEDIATE
    STATE @ IF
        ' LITSTRING , HERE @ 0 ,
        BEGIN KEY DUP '"' <> WHILE C, REPEAT DROP
        DUP HERE @ SWAP - 8- SWAP ! ALIGN
    ELSE
        HERE @ BEGIN KEY DUP '"' <> WHILE OVER C! 1+ REPEAT DROP
        HERE @ - HERE @ SWAP
    THEN ;
: ." IMMEDIATE
    STATE @ IF [COMPILE] S" ' TELL ,
    ELSE BEGIN KEY DUP '"' = IF DROP EXIT THEN EMIT AGAIN THEN ;

( Defining words. A constant is a tiny colon definition that pushes its value. )
: CONSTANT ( n -- ) WORD CREATE DOCOL , ' LIT , , ' EXIT , ;

( Defining words that define words. <BUILDS makes a word that pushes the
  address of its data; DOES> gives every word built that way a behavior.
    : CONST <BUILDS , DOES> @ ;   7 CONST SEVEN   SEVEN .  \ 7 )
: <BUILDS ( "name" -- ) WORD CREATE DODOES , 0 , ;
: DOES> IMMEDIATE ' (DOES>) , ;
( A variable is a <BUILDS word with one cell of data after its header, so
  VARIABLE BUF 64 ALLOT gives BUF 72 contiguous bytes. )
: VARIABLE <BUILDS 0 , ;

( Introspection: walk the dictionary. )
: ID. ( header -- ) 8+ DUP 1+ SWAP C@ F_LENMASK AND TELL ;
: HIDDEN? ( header -- f ) 8+ C@ F_HIDDEN AND ;
: WORDS LATEST @ BEGIN ?DUP WHILE
    DUP HIDDEN? NOT IF DUP ID. SPACE THEN @ REPEAT CR ;
: CFA> ( xt -- header|0 )
    LATEST @ BEGIN ?DUP WHILE 2DUP >CFA = IF NIP EXIT THEN @ REPEAT DROP 0 ;
: '' ( "name" -- xt ) WORD FIND >CFA ;

( SEE rebuilds source from compiled threaded code. For a primitive there is
  no Forth to recover, so it prints the machine words up to the first NEXT. )
: .HEX ( u -- ) BASE @ SWAP HEX U. BASE ! ;
VARIABLE FAR   ( furthest forward-branch target seen so far )
: BRANCHY? ( xt -- f ) ( is the next cell a branch offset? )
    DUP ' 0BRANCH = OVER ' BRANCH = OR
    OVER ' (LOOP) = OR SWAP ' (+LOOP) = OR ;
: SEE-CELL ( addr -- addr' )
    DUP @
    DUP ' LIT = IF DROP 8+ DUP @ DUP CFA> ?DUP
        IF ." ['] " ID. SPACE DROP ELSE . THEN 8+ EXIT THEN
    DUP ' LITSTRING = IF DROP [ CHAR S ] LITERAL EMIT '"' EMIT SPACE
        8+ DUP @ SWAP 8+ SWAP 2DUP TELL '"' EMIT SPACE + ALIGNED EXIT THEN
    DUP BRANCHY? IF
        CFA> ID. ."  ( " 8+ DUP @ DUP . ." ) "
        OVER + FAR @ MAX FAR ! 8+ EXIT THEN
    DUP ' ' = IF DROP ." ' " 8+ DUP @ CFA> ID. SPACE 8+ EXIT THEN
    DUP ' (DOES>) = IF DROP ." DOES> " 8+ EXIT THEN
    CFA> ?DUP IF ID. ELSE ." ?" THEN SPACE 8+ ;
: SEE-CODE ( header -- )
    ." ( " DUP ID. ." : arm64"
    >CFA @ 24 0 DO
        DUP @ 4294967295 AND DUP SPACE .HEX
        3592355872 = IF LEAVE THEN 4 +
    LOOP DROP ."  )" CR ;
: SEE-BODY ( addr -- )
    0 FAR !
    BEGIN DUP @ ' EXIT = OVER FAR @ >= AND NOT WHILE SEE-CELL REPEAT
    DROP ';' EMIT CR ;
: SEE-DOES ( header -- )
    ." ( " DUP ID. ."  is a <BUILDS word ) " >CFA 8+ @ ?DUP IF ." DOES> " SEE-BODY ELSE CR THEN ;
: SEE ( "name" -- )
    WORD FIND ?DUP 0= IF >ERR ." not found" CR >OUT EXIT THEN
    DUP >CFA @ DODOES = IF SEE-DOES EXIT THEN
    DUP >CFA @ DOCOL <> IF SEE-CODE EXIT THEN
    ':' EMIT SPACE DUP ID. SPACE
    DUP 8+ C@ F_IMMED AND IF ." IMMEDIATE " THEN
    >CFA 8+ SEE-BODY ;

( A census of the dictionary: kernel headers live in the binary, grown ones
  in DICT0 space above it. )
: CENSUS ( -- grown kernel )
    0 0 LATEST @ BEGIN ?DUP WHILE
        DUP DICT0 U< IF >R 1+ R> ELSE >R SWAP 1+ SWAP R> THEN @
    REPEAT ;
: BANNER CENSUS ." ember: " . ." words in arm64, " . ." grown in Forth" CR ;

( The kernel's ' only works compiled: at top level it pushed the next cell of
  QUIT and skipped it, which crashed. This one reads the next word in both
  states, and compiles it as a literal so an immediate word can be ticked. )
: ' IMMEDIATE WORD FIND >CFA STATE @ IF ' LIT , , THEN ;
( Error recovery. The kernel's INTERPRET prints "name ?" and carries on: a
  half-built definition stays hidden and keeps compiling the following lines,
  and an underflowed stack stays underflowed. From here on the outer
  interpreter is Forth, and an error resets the system to a known state. )
: RESET ( -- ) S0 @ DSP!
    STATE @ IF
        LATEST @ DUP HIDDEN? IF DUP @ LATEST ! HERE ! ELSE DROP THEN
        0 STATE !
    THEN ;
( After an error inside a definition, skip to its ";" rather than run the rest. )
: SKIP-DEF ( -- ) BEGIN WORD 1 = SWAP C@ ';' = AND UNTIL ;
: ?STACK ( -- ) DSP@ S0 @ > IF >ERR ." stack underflow" CR >OUT RESET THEN ;
( INTERPRET compiles or pushes what it can, and hands back a word to run so
  that QUIT runs it with nothing of INTERPRET's on the return stack. )
: INTERPRET ( -- xt true | false )
    WORD 2DUP FIND ?DUP IF
        NIP NIP DUP >CFA SWAP 8+ C@ F_IMMED AND STATE @ 0= OR
        IF TRUE EXIT THEN , FALSE EXIT
    THEN
    2DUP NUMBER 0= IF NIP NIP STATE @ IF ' LIT , , THEN FALSE EXIT THEN
    >ERR DROP TELL ."  ?" CR >OUT
    STATE @ IF LATEST @ HIDDEN? IF SKIP-DEF THEN THEN
    RESET FALSE ;
: QUIT ( -- ) BEGIN R0 RSP! INTERPRET IF EXECUTE THEN ?STACK AGAIN ;
: ABORT ( -- ) RESET QUIT ;
QUIT
