( lib/disasm.fs: an AArch64 disassembler written in ember itself.
  Load after core.fs:   ./ember core.fs lib/disasm.fs -

  SEE on a primitive now prints its machine code as assembly, with branch
  targets and adrp/add pairs named from the binary's own symbol table:

      SEE HERE      CODE HERE
                       0  adrp    x0, var_HERE@PAGE
                       4  add     x0, x0, var_HERE@PAGEOFF
                       8  str     x0, [x20, #-0x8]!
                       c  NEXT
                      END-CODE

  DIS ( addr -- )        one instruction
  DISASM ( addr n -- )   n instructions
  INSN. ( u -- )         an instruction word, as if at address 0
  OBJDUMP-TEXT ( -- )    the whole __text section in objdump's syntax, for
                         lib/disasm-check.sh to diff against objdump -d )

VARIABLE INS        ( the instruction word being decoded )
VARIABLE PC         ( its address )
VARIABLE REGW       ( true: x registers, false: w registers )
VARIABLE SYMBOLIC   ( true: name targets; false: objdump's unslid addresses )
VARIABLE START      ( first address of the current listing )
VARIABLE START-SYM  ( the symbol that listing starts in )
VARIABLE FARTHEST   ( furthest forward branch target inside the listing )
VARIABLE FUSE-PC    ( address of an add that completes the previous adrp )
VARIABLE FUSE-ADDR  ( the address that pair builds )
VARIABLE MCOL       ( characters of mnemonic printed so far )
VARIABLE ORIGIN     ( objdump mode prints addresses less this )

: L@ ( addr -- u32 ) @ 4294967295 AND ;
: ONES ( n -- mask ) DUP 64 = IF DROP -1 ELSE 1 SWAP LSHIFT 1- THEN ;
: BITS ( x lo n -- field ) >R RSHIFT R> ONES AND ;
: SEXT ( x n -- x' ) 2DUP 1- RSHIFT 1 AND IF 1 SWAP LSHIFT - ELSE DROP THEN ;
: FLD ( lo n -- field ) INS @ -ROT BITS ;
: IS ( lo n v -- f ) >R FLD R> = ;

( Output. U. divides signed, so hex goes out by shifting. )
: HDIGIT ( n -- c ) DUP 10 < IF '0' ELSE 87 THEN + ;
: (HX) ( u -- ) DUP 4 RSHIFT ?DUP IF RECURSE THEN 15 AND HDIGIT EMIT ;
: HXW ( u -- digits ) 1 SWAP BEGIN 4 RSHIFT ?DUP WHILE SWAP 1+ SWAP REPEAT ;
: HX ( u -- ) ." 0x" (HX) ;
: DEC. ( u -- ) BASE @ SWAP DECIMAL U. BASE ! ;
: UIMM. ( u -- ) [ CHAR # ] LITERAL EMIT HX ;
: IMM. ( n -- ) [ CHAR # ] LITERAL EMIT DUP 0< IF '-' EMIT NEGATE THEN HX ;
: SEP ." , " ;
: -TRAILING ( a n -- a n' )
    BEGIN DUP IF 2DUP + 1- C@ BL = ELSE FALSE THEN WHILE 1- REPEAT ;
: ENTRY ( i a n w -- a' n' ) NIP ROT OVER * ROT + SWAP -TRAILING ;
: MT ( a n -- ) DUP MCOL +! TELL ;
: MC ( c -- ) 1 MCOL +! EMIT ;
: M| ( -- ) 8 MCOL @ - 1 MAX SPACES ;
: MN ( a n -- ) MT M| ;

( Registers. Register 31 is sp or the zero register depending on the field. )
: REG ( n sp? -- )
    OVER 31 = IF NIP
        IF REGW @ IF ." sp" ELSE ." wsp" THEN
        ELSE REGW @ IF ." xzr" ELSE ." wzr" THEN THEN EXIT THEN
    DROP REGW @ IF [ CHAR x ] LITERAL ELSE [ CHAR w ] LITERAL THEN EMIT DEC. ;
: RZ. ( n -- ) 0 REG ;
: RS. ( n -- ) 1 REG ;
: XS. ( n -- ) REGW @ SWAP 1 REGW ! RS. REGW ! ;
: XZ. ( n -- ) REGW @ SWAP 1 REGW ! RZ. REGW ! ;
: WZ. ( n -- ) REGW @ SWAP 0 REGW ! RZ. REGW ! ;
: RD 0 5 FLD ;  : RN 5 5 FLD ;  : RM 16 5 FLD ;  : RA 10 5 FLD ;
: SF! 31 1 FLD REGW ! ;
: RRR. RD RZ. SEP RN RZ. SEP RM RZ. ;
: COND$ ( c -- a n ) S" eqnehslomiplvsvchilsgeltgtlealnv" 2 ENTRY ;
: SHIFT. ( -- ) 10 6 FLD ?DUP IF
    SEP 22 2 FLD S" lsllsrasrror" 3 ENTRY TELL ."  #" DEC. THEN ;

( The binary's own symbol table. The Mach-O header sits at the start of the
  page-aligned __TEXT segment, below DOCOL; __LINKEDIT is mapped too. )
VARIABLE SLIDE  VARIABLE SYMTAB  VARIABLE NSYMS  VARIABLE STRTAB
VARIABLE TEXT-START  VARIABLE TEXT-SIZE  VARIABLE LE-BASE
VARIABLE SYMOFF  VARIABLE STROFF
: S= ( a1 a2 n -- f )
    BEGIN ?DUP WHILE
        >R 2DUP C@ SWAP C@ <> IF R> DROP 2DROP FALSE EXIT THEN
        1+ SWAP 1+ SWAP R> 1-
    REPEAT 2DROP TRUE ;
: NAMED? ( a16 a n -- f ) >R >R DUP R> R@ S= SWAP R> + C@ 0= AND ;
: MACH-HEADER ( -- addr )
    DOCOL 16383 INVERT AND BEGIN DUP L@ 4277009103 <> WHILE 16384 - REPEAT ;
: SEGMENT ( cmd -- )
    DUP 8+ S" __TEXT" NAMED? IF
        MACH-HEADER OVER 24 + @ - SLIDE !
        DUP 64 + L@ SWAP 72 + SWAP 0 DO
            DUP S" __text" NAMED? IF
                DUP 32 + @ SLIDE @ + TEXT-START ! DUP 40 + @ TEXT-SIZE ! THEN
            80 +
        LOOP DROP EXIT THEN
    DUP 8+ S" __LINKEDIT" NAMED? IF
        DUP 24 + @ SLIDE @ + OVER 40 + @ - LE-BASE ! THEN DROP ;
: LOAD-CMDS ( -- )
    MACH-HEADER DUP 16 + L@ SWAP 32 + SWAP 0 DO
        DUP L@ 25 = IF DUP SEGMENT THEN
        DUP L@ 2 = IF DUP 8 + L@ SYMOFF ! DUP 12 + L@ NSYMS ! DUP 16 + L@ STROFF ! THEN
        DUP 4 + L@ +
    LOOP DROP
    LE-BASE @ SYMOFF @ + SYMTAB !  LE-BASE @ STROFF @ + STRTAB ! ;
LOAD-CMDS

: NL ( i -- nlist ) 16 * SYMTAB @ + ;
: SYM-OK? ( nl -- f ) 4 + C@ DUP 224 AND 0= SWAP 14 AND 14 = AND ;
: SYM-VAL ( nl -- addr ) 8+ @ SLIDE @ + ;
: SYM-NAME ( nl -- ) L@ STRTAB @ + BEGIN DUP C@ ?DUP WHILE EMIT 1+ REPEAT DROP ;
VARIABLE BEST
: BETTER? ( addr nl -- f )
    DUP SYM-OK? 0= IF 2DROP FALSE EXIT THEN
    SYM-VAL 2DUP U< IF 2DROP FALSE EXIT THEN NIP
    BEST @ ?DUP IF SYM-VAL > ELSE DROP TRUE THEN ;
: NEAREST ( addr -- nl|0 )
    0 BEST ! 0 BEGIN DUP NSYMS @ < WHILE
        2DUP NL BETTER? IF DUP NL BEST ! THEN 1+ REPEAT 2DROP BEST @ ;
: SYM. ( addr -- )
    DUP NEAREST ?DUP IF 2DUP SYM-VAL = IF SYM-NAME DROP EXIT THEN DROP THEN
    DUP CFA> ?DUP IF ID. DROP EXIT THEN
    DUP NEAREST ?DUP IF DUP SYM-NAME SYM-VAL - ?DUP IF ." +" HX THEN
    ELSE HX THEN ;

( Branch targets. Inside the listing they print as the offset column shows
  them and stretch the listing; elsewhere, by symbol. )
: LOCAL? ( addr -- f )
    DUP START @ - 4096 U< SWAP NEAREST START-SYM @ = AND ;
: TGT. ( addr -- )
    SYMBOLIC @ 0= IF ORIGIN @ - HX EXIT THEN
    DUP LOCAL? IF
        DUP FARTHEST @ MAX FARTHEST ! ." +" START @ - (HX)
    ELSE SYM. THEN ;
: PCREL ( words -- addr ) 4 * PC @ + ;

( Decoders. Each checks its encoding class and returns false, printing
  nothing, if the instruction isn't one of its own. )
: D-B ( -- f ) 26 5 5 IS 0= IF FALSE EXIT THEN
    31 1 FLD IF S" bl" ELSE S" b" THEN MN 0 26 FLD 26 SEXT PCREL TGT. TRUE ;
: D-BCOND 24 8 84 IS 4 1 0 IS AND 0= IF FALSE EXIT THEN
    S" b." MT 0 4 FLD COND$ MN 5 19 FLD 19 SEXT PCREL TGT. TRUE ;
: D-CB 25 6 26 IS 0= IF FALSE EXIT THEN SF!
    24 1 FLD IF S" cbnz" ELSE S" cbz" THEN MN
    RD RZ. SEP 5 19 FLD 19 SEXT PCREL TGT. TRUE ;
: D-TB 25 6 27 IS 0= IF FALSE EXIT THEN SF!
    24 1 FLD IF S" tbnz" ELSE S" tbz" THEN MN RD RZ. SEP
    31 1 FLD 5 LSHIFT 19 5 FLD OR UIMM. SEP 5 14 FLD 14 SEXT PCREL TGT. TRUE ;
: D-BR INS @ 4294966303 AND
    DUP 3592355840 = IF DROP S" br" MN 1 REGW ! RN RZ. TRUE EXIT THEN
    DUP 3594452992 = IF DROP S" blr" MN 1 REGW ! RN RZ. TRUE EXIT THEN
    3596550144 = IF S" ret" MT RN 30 <> IF M| 1 REGW ! RN RZ. THEN TRUE EXIT THEN
    FALSE ;
: D-SYS INS @ 4292870175 AND 3556769793 = IF S" svc" MN 5 16 FLD ?DUP IF UIMM. ELSE ." #0" THEN TRUE EXIT THEN
    INS @ 3573751839 = IF S" nop" MT TRUE EXIT THEN FALSE ;

( adrp+add is how the kernel names any address, LOADADDR in ember.s. When
  the add follows, both lines print the symbol the pair builds. )
: PAGEOFF? ( -- off true | false )
    PC @ 4 + L@ DUP 22 RSHIFT 580 = OVER 5 5 BITS RD = AND OVER 0 5 BITS RD = AND
    IF 10 12 BITS TRUE ELSE DROP FALSE THEN ;
: PAGE. ( page -- )
    SYMBOLIC @ 0= IF TGT. EXIT THEN
    PAGEOFF? IF + DUP FUSE-ADDR ! PC @ 4 + FUSE-PC ! SYM. ." @PAGE" ELSE SYM. THEN ;
: D-ADR 24 5 16 IS 0= IF FALSE EXIT THEN 1 REGW !
    5 19 FLD 2 LSHIFT 29 2 FLD OR 21 SEXT
    31 1 FLD 0= IF S" adr" MN RD RZ. SEP PC @ + TGT. TRUE EXIT THEN
    S" adrp" MN RD RZ. SEP 12 LSHIFT PC @ 4095 INVERT AND + PAGE. TRUE ;

: ADDI-IMM ( -- )
    SYMBOLIC @ PC @ FUSE-PC @ = AND IF FUSE-ADDR @ SYM. ." @PAGEOFF" EXIT THEN
    10 12 FLD UIMM. 22 1 FLD IF ." , lsl #12" THEN ;
: D-ADDI 23 6 34 IS 0= IF FALSE EXIT THEN SF!
    29 1 FLD RD 31 = AND IF
        30 1 FLD IF S" cmp" ELSE S" cmn" THEN MN RN RS. SEP ADDI-IMM TRUE EXIT THEN
    29 2 FLD 0= 10 13 FLD 0= AND RD 31 = RN 31 = OR AND IF
        S" mov" MN RD RS. SEP RN RS. TRUE EXIT THEN
    29 2 FLD S" add addssub subs" 4 ENTRY MN
    29 1 FLD IF RD RZ. ELSE RD RS. THEN SEP RN RS. SEP ADDI-IMM TRUE ;
: D-ADDR 24 5 11 IS 21 1 0 IS AND 0= IF FALSE EXIT THEN SF!
    22 2 FLD 3 = IF FALSE EXIT THEN
    29 1 FLD RD 31 = AND IF
        30 1 FLD IF S" cmp" ELSE S" cmn" THEN MN RN RZ. SEP RM RZ. SHIFT. TRUE EXIT THEN
    30 1 FLD RN 31 = AND IF
        29 1 FLD IF S" negs" ELSE S" neg" THEN MN RD RZ. SEP RM RZ. SHIFT. TRUE EXIT THEN
    29 2 FLD S" add addssub subs" 4 ENTRY MN RRR. SHIFT. TRUE ;

( Logical immediates: a run of ones, rotated, repeated across the register. )
: REGSIZE ( -- n ) REGW @ IF 64 ELSE 32 THEN ;
: SIGNED ( u -- n ) REGW @ 0= IF 32 SEXT THEN ;
( orr from the zero register reads as mov, unless movz or movn could say it. )
: WIDE? ( v -- f )
    0 BEGIN DUP REGSIZE < WHILE
        2DUP 65535 SWAP LSHIFT INVERT AND 0= IF 2DROP TRUE EXIT THEN 16 +
    REPEAT 2DROP FALSE ;
: MOVABLE? ( v -- f ) DUP WIDE? SWAP INVERT REGSIZE ONES AND WIDE? OR ;
: HIBIT ( x -- n ) -1 SWAP BEGIN ?DUP WHILE SWAP 1+ SWAP 1 RSHIFT REPEAT ;
: BITMASK ( -- imm )
    22 1 FLD 6 LSHIFT 10 6 FLD INVERT 63 AND OR HIBIT 1 SWAP LSHIFT >R
    10 6 FLD R@ 1- AND 1+ ONES
    16 6 FLD R@ 1- AND ?DUP IF
        2DUP RSHIFT -ROT R@ SWAP - LSHIFT OR R@ ONES AND THEN
    R> BEGIN DUP REGSIZE < WHILE 2DUP LSHIFT ROT OR SWAP 2 * REPEAT DROP ;
: D-LOGI 23 6 36 IS 0= IF FALSE EXIT THEN SF!
    22 1 FLD REGW @ 0= AND IF FALSE EXIT THEN
    29 2 FLD 3 = RD 31 = AND IF S" tst" MN RN RZ. SEP BITMASK UIMM. TRUE EXIT THEN
    29 2 FLD 1 = RN 31 = AND BITMASK MOVABLE? 0= AND IF
        S" mov" MN RD RS. SEP BITMASK SIGNED IMM. TRUE EXIT THEN
    29 2 FLD S" and orr eor ands" 4 ENTRY MN
    29 2 FLD 3 = IF RD RZ. ELSE RD RS. THEN SEP RN RZ. SEP BITMASK UIMM. TRUE ;

: D-MOVW 23 6 37 IS 0= IF FALSE EXIT THEN SF!
    5 16 FLD 21 2 FLD 16 *
    29 2 FLD 3 = IF S" movk" MN RD RZ. SEP SWAP UIMM. ?DUP IF ." , lsl #" DEC. THEN
        TRUE EXIT THEN
    29 2 FLD 2 = IF
        OVER 0= OVER 0<> AND IF S" movz" MN RD RZ. SEP ." #0x0, lsl #" DEC. DROP TRUE EXIT THEN
        S" mov" MN RD RZ. SEP LSHIFT UIMM. TRUE EXIT THEN
    29 2 FLD 0= IF S" mov" MN RD RZ. SEP LSHIFT INVERT
        REGW @ 0= IF 4294967295 AND THEN SIGNED IMM. TRUE EXIT THEN
    2DROP FALSE ;
: D-LOGR 24 5 10 IS 0= IF FALSE EXIT THEN SF!
    29 2 FLD 2 * 21 1 FLD +
    DUP 2 = RN 31 = AND 10 6 FLD 22 2 FLD OR 0= AND IF
        DROP S" mov" MN RD RZ. SEP RM RZ. TRUE EXIT THEN
    DUP 3 = RN 31 = AND IF DROP S" mvn" MN RD RZ. SEP RM RZ. SHIFT. TRUE EXIT THEN
    DUP 6 = RD 31 = AND IF DROP S" tst" MN RN RZ. SEP RM RZ. SHIFT. TRUE EXIT THEN
    S" and bic orr orn eor eon andsbics" 4 ENTRY MN RRR. SHIFT. TRUE ;
: D-DP2 21 10 214 IS 0= IF FALSE EXIT THEN SF!
    10 6 FLD DUP 2 = OVER 3 = OR IF 2 - S" udivsdiv" 4 ENTRY
    ELSE DUP 8 < OVER 11 > OR IF DROP FALSE EXIT THEN 8 - S" lsllsrasrror" 3 ENTRY THEN
    MN RRR. TRUE ;
: D-DP3 24 5 27 IS 29 2 0 IS AND 0= IF FALSE EXIT THEN SF!
    21 3 FLD 2 * 15 1 FLD +
    DUP 1 > IF
        DUP 4 = IF DROP S" smulh" ELSE 12 = IF S" umulh" ELSE FALSE EXIT THEN THEN
        MN RRR. TRUE EXIT THEN
    RA 31 = IF S" mul mneg" 4 ENTRY MN RRR. TRUE EXIT THEN
    S" maddmsub" 4 ENTRY MN RRR. SEP RA RZ. TRUE ;
: INVCOND ( -- a n ) 12 4 FLD 1 XOR COND$ ;
: D-CSEL 21 8 212 IS 29 1 0 IS AND 0= IF FALSE EXIT THEN SF!
    30 1 FLD 2 * 10 2 FLD + DUP 3 > IF DROP FALSE EXIT THEN
    RM RN = 12 4 FLD 14 < AND OVER 0<> AND IF
        RN 31 = OVER 3 < AND IF
            1- S" cset csetm" 5 ENTRY MN RD RZ. SEP INVCOND TELL TRUE EXIT THEN
        1- S" cinccinvcneg" 4 ENTRY MN RD RZ. SEP RN RZ. SEP INVCOND TELL TRUE EXIT THEN
    S" csel csinccsinvcsneg" 5 ENTRY MN RRR. SEP 12 4 FLD COND$ TELL TRUE ;

( Loads and stores. )
: SIZE 30 2 FLD ;  : OPC 22 2 FLD ;
: LDST-NAME ( unscaled? -- ok? )
    SIZE 2 = OPC 3 = AND SIZE 3 = OPC 1 > AND OR IF DROP FALSE EXIT THEN
    OPC IF S" ld" ELSE S" st" THEN MT
    IF S" ur" ELSE S" r" THEN MT
    OPC 1 > IF [ CHAR s ] LITERAL MC THEN
    SIZE 0= IF [ CHAR b ] LITERAL MC THEN
    SIZE 1 = IF [ CHAR h ] LITERAL MC THEN
    SIZE 2 = OPC 2 = AND IF [ CHAR w ] LITERAL MC THEN
    M| SIZE 3 = OPC 2 = OR REGW ! TRUE ;
: [RN ( -- ) ." [" RN XS. ;
: D-LDST-U 27 3 7 IS 26 1 0 IS AND 24 2 1 IS AND 0= IF FALSE EXIT THEN
    0 LDST-NAME 0= IF FALSE EXIT THEN
    RD RZ. SEP [RN 10 12 FLD SIZE LSHIFT ?DUP IF SEP UIMM. THEN ." ]" TRUE ;
: LDST-REG ( -- f )
    14 1 FLD 0= IF FALSE EXIT THEN
    0 LDST-NAME 0= IF FALSE EXIT THEN
    RD RZ. SEP [RN SEP 13 3 FLD DUP 1 AND IF RM XZ. ELSE RM WZ. THEN
    DUP 3 = 12 1 FLD 0= AND IF DROP ." ]" TRUE EXIT THEN
    SEP S" --------uxtwlsl --------sxtwsxtx" 4 ENTRY TELL
    12 1 FLD IF ."  #" SIZE DEC. THEN ." ]" TRUE ;
: D-LDST-I 27 3 7 IS 26 1 0 IS AND 24 2 0 IS AND 0= IF FALSE EXIT THEN
    21 1 FLD IF 10 2 FLD 2 = IF LDST-REG ELSE FALSE THEN EXIT THEN
    10 2 FLD DUP 2 = IF DROP FALSE EXIT THEN
    DUP 0= LDST-NAME 0= IF DROP FALSE EXIT THEN
    RD RZ. SEP [RN 12 9 FLD 9 SEXT SWAP
    DUP 1 = IF DROP ." ], " IMM.
    ELSE 0= IF ?DUP IF SEP IMM. THEN ." ]" ELSE SEP IMM. ." ]!" THEN THEN TRUE ;
: D-LDP 27 3 5 IS 25 2 0 IS AND 0= IF FALSE EXIT THEN
    30 2 FLD DUP 3 = IF DROP FALSE EXIT THEN
    DUP 1 = 22 1 FLD 0= AND IF DROP FALSE EXIT THEN
    22 1 FLD IF S" ld" ELSE S" st" THEN MT
    23 2 FLD 0= IF [ CHAR n ] LITERAL MC THEN S" p" MT
    DUP 1 = IF S" sw" MT THEN M|
    DUP 0<> REGW ! 2 = IF 8 ELSE 4 THEN 15 7 FLD 7 SEXT *
    RD RZ. SEP RA RZ. SEP [RN 23 2 FLD
    DUP 1 = IF DROP ." ], " IMM.
    ELSE 3 = IF SEP IMM. ." ]!" ELSE ?DUP IF SEP IMM. THEN ." ]" THEN THEN TRUE ;
: D-LDLIT 27 3 3 IS 24 3 0 IS AND 0= IF FALSE EXIT THEN
    30 2 FLD DUP 2 > IF DROP FALSE EXIT THEN
    DUP 2 = IF S" ldrsw" ELSE S" ldr" THEN MN 0<> REGW !
    RD RZ. SEP 5 19 FLD 19 SEXT PCREL TGT. TRUE ;

: DECODE ( -- )
    D-B IF EXIT THEN  D-BCOND IF EXIT THEN  D-CB IF EXIT THEN  D-TB IF EXIT THEN
    D-BR IF EXIT THEN  D-SYS IF EXIT THEN  D-ADR IF EXIT THEN
    D-ADDI IF EXIT THEN  D-ADDR IF EXIT THEN  D-LOGI IF EXIT THEN
    D-MOVW IF EXIT THEN  D-LOGR IF EXIT THEN  D-DP2 IF EXIT THEN
    D-DP3 IF EXIT THEN  D-CSEL IF EXIT THEN
    D-LDST-U IF EXIT THEN  D-LDST-I IF EXIT THEN  D-LDP IF EXIT THEN
    D-LDLIT IF EXIT THEN
    S" .word" MN INS @ HX ;
: (DIS) ( addr -- ) DUP PC ! L@ INS ! 0 MCOL ! DECODE ;

( Listings. NEXT, the three-instruction inner interpreter, prints as one
  line. A listing ends at a jump out once no forward branch reaches past it. )
: NEXT? ( addr -- f )
    DUP L@ 4164978272 = OVER 4 + L@ 4181721089 = AND SWAP 8 + L@ 3592355872 = AND ;
: TERMINAL? ( -- f )
    INS @ 4294966303 AND DUP 3592355840 = SWAP 3596550144 = OR 26 6 5 IS OR ;
: BEGIN-LISTING ( addr -- ) DUP START ! NEAREST START-SYM ! 0 FARTHEST ! TRUE SYMBOLIC ! ;
: OFF. ( addr -- ) START @ - DUP HXW 4 SWAP - SPACES (HX) 2 SPACES ;
: LINE ( addr -- addr' done? )
    DUP OFF. DUP NEXT? IF ." NEXT" CR 12 + TRUE EXIT THEN
    DUP (DIS) CR 4 + TERMINAL? ;
: LISTING ( addr -- )
    DUP BEGIN-LISTING
    256 BEGIN >R LINE OVER FARTHEST @ > AND R> 1- TUCK 0= OR UNTIL 2DROP ;
: DIS ( addr -- ) DUP BEGIN-LISTING (DIS) CR ;
: INSN. ( u -- ) 0 ORIGIN ! FALSE SYMBOLIC ! INS ! 0 PC ! 0 MCOL ! DECODE ;
: DISASM ( addr n -- )
    OVER BEGIN-LISTING BEGIN ?DUP WHILE >R LINE DROP R> 1- REPEAT DROP ;
: OBJDUMP-TEXT ( -- )
    FALSE SYMBOLIC ! SLIDE @ ORIGIN ! TEXT-START @ DUP TEXT-SIZE @ + SWAP
    BEGIN 2DUP > WHILE DUP ORIGIN @ - (HX) ." : " DUP (DIS) CR 4 + REPEAT 2DROP ;

( Hook SEE: core's SEE calls SEE-CODE for anything that isn't a colon
  definition. Repoint that one cell of SEE's body at SEE-ASM. )
: SEE-ASM ( header -- ) ." CODE " DUP ID. CR >CFA @ LISTING ." END-CODE" CR ;
: PATCH ( new old body -- )
    200 BEGIN >R 8+ 2DUP @ = IF NIP ! R> DROP EXIT THEN R> 1- DUP 0= UNTIL
    2DROP 2DROP ;
'' SEE-ASM '' SEE-CODE '' SEE PATCH
