( lib/tracker.fs: a little studio for writing songs in ember. It provides
  a sine table, equal-temperament tuning, noise, readers for a tracker
  score, a chord chart and an arrangement, envelope and mixing helpers, a
  ping-pong echo, and a renderer that writes 44.1 kHz 16-bit stereo WAV to
  stdout. A song supplies its own instruments and two words:

      TRIGGER  runs once per step and decides who plays
      FRAME    computes one stereo frame into LEFT and RIGHT

  and then ends with   ' TRIGGER ' FRAME RENDER
  Run as:  ./ember core.fs lib/tracker.fs demos/song.fs > out.wav

  Nothing else may print while it renders: every byte on stdout is audio. )

44100 CONSTANT RATE
1 30 LSHIFT CONSTANT FIX          ( fixed-point 1.0 for tables and amplitudes )
VARIABLE STEPLEN                  ( samples per step )
VARIABLE METER                    ( steps per bar )

( ---- Bytes out, little-endian ---- )
: LE16 ( n -- ) DUP 255 AND EMIT 8 RSHIFT 255 AND EMIT ;
: LE32 ( n -- ) DUP LE16 16 RSHIFT LE16 ;

( ---- A sine table from s[n+1] = 2cos(w) s[n] - s[n-1], w = 2pi/1024.
  The two literals are 2cos(w) and sin(w), times 2^30. ---- )
1024 CONSTANT TABN
HERE @ TABN CELLS ALLOT CONSTANT SINE
2147443222 CONSTANT KCOS
: BUILD-SINE
    0 6588356                     ( s[i] s[i+1] )
    TABN 0 DO
        OVER 65536 / I CELLS SINE + !     ( stored as +-16384 )
        SWAP OVER KCOS * FIX / SWAP -
    LOOP 2DROP ;
BUILD-SINE
( A phase is 32 bits of a full turn; its top 10 bits index the table. )
: SIN ( phase -- s ) 22 RSHIFT 1023 AND CELLS SINE + @ ;

( ---- Tuning: a phase increment per MIDI note. A4 = 440 Hz, and each
  semitone is 2^(1/12) = 1137589835 / 2^30 away from its neighbour. ---- )
HERE @ 128 CELLS ALLOT CONSTANT PITCH
1137589835 CONSTANT SEMI
: HZ ( midi -- inc ) CELLS PITCH + @ ;
: TUNE
    440 1 32 LSHIFT * RATE /  69 CELLS PITCH + !
    128 70 DO I 1- HZ SEMI * FIX /  I CELLS PITCH + ! LOOP
    69 0 DO 68 I - DUP 1+ HZ FIX * SEMI / SWAP CELLS PITCH + ! LOOP ;
TUNE

( ---- Noise: xorshift64, top 15 bits, centred. ---- )
VARIABLE SEED  2463534242 SEED !
: NOISE ( -- n )
    SEED @ DUP 13 LSHIFT XOR DUP 7 RSHIFT XOR DUP 17 LSHIFT XOR
    DUP SEED ! 49 RSHIFT 16384 - ;

( ---- Reading the score. Notes are spelled like an old tracker: C-4, F#5.
  "..." holds, "^^^" releases, "|" is a bar line for the reader. ---- )
CHAR ] CONSTANT CLOSE
CHAR | CONSTANT BARLINE
CHAR ^ CONSTANT CARET
CHAR a CONSTANT 'a'
CHAR z CONSTANT 'z'
CHAR # CONSTANT SHARP
CHAR m CONSTANT MINOR
CHAR b CONSTANT FLAT

( C D E F G A B sit 0 2 4 5 7 9 11 semitones up: twice the letter's place
  in the scale, less one from F onward. )
: SEMITONE ( letter -- n ) [ CHAR C ] LITERAL - 7 + 7 MOD DUP 2 * SWAP 2 > + ;
: NOTE ( addr -- midi )
    DUP C@ SEMITONE
    OVER 1+ C@ DUP SHARP = IF DROP 1+ ELSE FLAT = IF 1- THEN THEN
    SWAP 2 + C@ '0' - 1+ 12 * + ;
( A track holds one byte per step: 0 hold, 1 release, else a MIDI note. )
: STEP-BYTE ( addr -- byte )
    DUP C@ '.' = IF DROP 0 EXIT THEN
    DUP C@ CARET = IF DROP 1 EXIT THEN
    NOTE ;
: TRACK[ ( -- addr n )
    HERE @
    BEGIN WORD DROP DUP C@ CLOSE <> WHILE
        DUP C@ BARLINE = IF DROP ELSE STEP-BYTE C, THEN
    REPEAT DROP HERE @ OVER - ALIGN ;
( NOTES[ is TRACK[ with a loudness: two bytes per step, the note byte and a
  velocity 1-9 from an optional fourth character, as in C-58. Default 7. )
: NOTES[ ( -- addr steps )
    HERE @
    BEGIN WORD OVER C@ CLOSE <> WHILE
        OVER C@ BARLINE = IF 2DROP ELSE
            OVER STEP-BYTE C,
            4 = IF 3 + C@ '0' - ELSE DROP 7 THEN C,
        THEN
    REPEAT 2DROP HERE @ OVER - 2 / ALIGN ;
: NOTE@ ( track i -- note vel ) 2 * + DUP C@ SWAP 1+ C@ ;

( Chords are written as on a lead sheet: Am, F#m7, Dbmaj7, Bbm9, C7sus,
  Gsus2, Eb/G, Fadd9, Bm7b5. Each becomes one cell: the root's pitch class
  in bits 0-3, a 12-bit mask of the intervals above the root in bits 4-15,
  and the bass note's pitch class in bits 16-19. The chart repeats. )
VARIABLE CHORDS  VARIABLE CHORD#
VARIABLE C.P  VARIABLE C.END  VARIABLE C.MASK  VARIABLE C.MAJ  VARIABLE C.ADD
VARIABLE C.ROOT  VARIABLE C.BASS
: C@+ ( -- c ) C.P @ C.END @ < IF C.P @ C@ 1 C.P +! ELSE 0 THEN ;
: PEEK ( -- c ) C.P @ C.END @ < IF C.P @ C@ ELSE 0 THEN ;
: SKIP ( n -- ) C.P +! ;
: PITCH ( -- pc )
    C@+ SEMITONE PEEK
    DUP SHARP = IF DROP 1+ 1 SKIP ELSE FLAT = IF 11 + 1 SKIP THEN THEN 12 MOD ;
: BIT+ ( n -- ) 1 SWAP LSHIFT C.MASK @ OR C.MASK ! ;
: BIT- ( n -- ) 1 SWAP LSHIFT INVERT C.MASK @ AND C.MASK ! ;
: SEVENTH C.MAJ @ IF 11 ELSE 10 THEN BIT+ ;
: SUS ( -- ) 2 SKIP  PEEK [ CHAR 2 ] LITERAL = IF 1 SKIP 2 ELSE
    PEEK [ CHAR 4 ] LITERAL = IF 1 SKIP THEN 5 THEN  3 BIT- 4 BIT- BIT+ ;
: QUALITY ( c -- )
    DUP MINOR = IF DROP PEEK [ CHAR a ] LITERAL = IF 1 C.MAJ ! 2 SKIP
        ELSE 4 BIT- 3 BIT+ THEN EXIT THEN
    DUP [ CHAR M ] LITERAL = IF DROP 1 C.MAJ ! EXIT THEN
    DUP [ CHAR 7 ] LITERAL = IF DROP SEVENTH EXIT THEN
    DUP [ CHAR 9 ] LITERAL = IF DROP C.ADD @ 0= IF SEVENTH THEN 2 BIT+ EXIT THEN
    DUP [ CHAR 6 ] LITERAL = IF DROP 9 BIT+ EXIT THEN
    DUP [ CHAR s ] LITERAL = IF DROP SUS EXIT THEN
    DUP [ CHAR a ] LITERAL = IF DROP 2 SKIP 1 C.ADD ! EXIT THEN
    DUP FLAT = IF DROP C@+ [ CHAR 5 ] LITERAL = IF 7 BIT- 6 BIT+
        ELSE 1 BIT+ THEN EXIT THEN
    DUP [ CHAR / ] LITERAL = IF DROP PITCH C.BASS ! EXIT THEN
    DROP ;
: CHORD-CELL ( addr len -- cell )
    OVER + C.END !  C.P !
    145 C.MASK !  0 C.MAJ !  0 C.ADD !      ( 145: bits 0, 4, 7, a major triad )
    PITCH DUP C.ROOT ! C.BASS !
    BEGIN C@+ ?DUP WHILE QUALITY REPEAT
    C.ROOT @  C.MASK @ 4 LSHIFT OR  C.BASS @ 16 LSHIFT OR ;
: CHORDS[
    HERE @
    BEGIN WORD OVER C@ CLOSE <> WHILE CHORD-CELL , REPEAT 2DROP
    DUP CHORDS !  HERE @ SWAP - 8 / CHORD# ! ;

( The arrangement: one word per bar, each letter switching a part on; the
  song decides what the letters mean. a-z set bits 0-25 of a cell and A-Z
  bits 26-51; a word with no letters, like "-", is a silent bar. )
VARIABLE SHEET  VARIABLE BARS
: LETTER-BIT ( c -- mask )
    DUP 'a' >= OVER 'z' <= AND IF 'a' - 1 SWAP LSHIFT EXIT THEN
    DUP [ CHAR A ] LITERAL >= OVER [ CHAR Z ] LITERAL <= AND IF
        [ CHAR A ] LITERAL - 26 + 1 SWAP LSHIFT EXIT THEN
    DROP 0 ;
: PARTS ( addr len -- mask )
    0 -ROT BEGIN DUP 0> WHILE
        1- 2DUP + C@ LETTER-BIT >R ROT R> OR -ROT
    REPEAT 2DROP ;
: ARRANGE[
    HERE @
    BEGIN WORD OVER C@ CLOSE <> WHILE PARTS , REPEAT 2DROP
    DUP SHEET !  HERE @ SWAP - 8 / BARS ! ;
( PART k is the mask for letter k, folded to a literal when compiled. )
: PART ( "letter" -- mask ) IMMEDIATE
    CHAR LETTER-BIT STATE @ IF [COMPILE] LITERAL THEN ;

( ---- Where the song is. ---- )
VARIABLE STEP#
: BAR ( -- n ) STEP# @ METER @ / ;
: POS ( -- n ) STEP# @ METER @ MOD ;
: ON? ( mask -- f ) BAR CELLS SHEET @ + @ AND ;
: CHORD-AT ( bar -- cell ) CHORD# @ MOD CELLS CHORDS @ + @ ;
: CHORD ( -- cell ) BAR CHORD-AT ;
: ROOT ( -- n ) CHORD 15 AND ;
: BASSNOTE ( -- n ) CHORD 16 RSHIFT 15 AND ;
: INTERVALS ( -- mask ) CHORD 4 RSHIFT 4095 AND ;
: THIRD ( -- n ) INTERVALS 8 AND IF 3 ELSE 4 THEN ;
( Chord tones: 0 root, 1 third, 2 fifth, 3 octave, 4 tenth, and so on. )
: TONE ( t -- semis )
    0 SWAP BEGIN DUP 3 >= WHILE 3 - SWAP 12 + SWAP REPEAT
    DUP 1 = IF DROP THIRD ELSE 2 = IF 7 ELSE 0 THEN THEN + ;
( The character for this step in a pattern string's address. )
: STEPS ( addr -- c ) POS + C@ ;
CHAR x CONSTANT 'x'
CHAR o CONSTANT 'o'

( ---- Envelopes. Amplitudes are fractions of FIX; DECAY multiplies by
  k/65536 per sample. ---- )
: PCT ( n -- amp ) FIX * 100 / ;
: SCALE ( s amp -- s' ) * FIX / ;
: DECAY ( var k -- ) OVER @ * 65536 / SWAP ! ;
( One-pole lowpass: y += (x - y) * c / 256. )
: SMOOTH ( x var c -- y ) >R DUP @ ROT SWAP - R> * 256 / OVER +! @ ;

( ---- Mixing. Pan runs 0 (left) to 256 (right). ---- )
VARIABLE LEFT  VARIABLE RIGHT
: MIX ( s pan -- ) 2DUP * 256 / RIGHT +!  256 SWAP - * 256 / LEFT +! ;
: CLIP ( n -- n ) -32767 MAX 32767 MIN ;

( ---- Ping-pong echo: the left line feeds the right, the right feeds back
  into the left. Set DELAY (up to two seconds) and FEEDBACK (of 256). ---- )
RATE 2 * CONSTANT MAXDELAY
HERE @ MAXDELAY CELLS ALLOT CONSTANT EL
HERE @ MAXDELAY CELLS ALLOT CONSTANT ER
EL MAXDELAY CELLS 0 FILL  ER MAXDELAY CELLS 0 FILL
VARIABLE DELAY  VARIABLE FEEDBACK
VARIABLE EI  VARIABLE DL  VARIABLE DR
: ECHO ( send -- )
    EI @ CELLS DUP EL + @ DL !  ER + @ DR !
    DR @ FEEDBACK @ * 256 / +  EI @ CELLS EL + !
    DL @ FEEDBACK @ * 256 / EI @ CELLS ER + !
    EI @ 1+ DUP DELAY @ >= IF DROP 0 THEN EI !
    DL @ LEFT +!  DR @ RIGHT +! ;

( ---- The file. ---- )
: FRAMES ( -- n ) BARS @ METER @ * STEPLEN @ * ;
: HEADER
    S" RIFF" TELL  FRAMES 4 * 36 + LE32  S" WAVE" TELL
    S" fmt " TELL  16 LE32  1 LE16  2 LE16
    RATE LE32  RATE 4 * LE32  4 LE16  16 LE16
    S" data" TELL  FRAMES 4 * LE32 ;
( SWING lengthens every even step by that many samples and shortens the odd
  ones to match, so the bar keeps its length. )
VARIABLE SWING
: STEP-SAMPLES ( -- n ) STEPLEN @ STEP# @ 1 AND IF SWING @ - ELSE SWING @ + THEN ;
VARIABLE 'TRIGGER  VARIABLE 'FRAME
: RENDER ( trigger-xt frame-xt -- )
    'FRAME !  'TRIGGER !
    HEADER
    BARS @ METER @ * 0 DO
        I STEP# !  'TRIGGER @ EXECUTE
        STEP-SAMPLES 0 DO
            0 LEFT !  0 RIGHT !
            'FRAME @ EXECUTE
            LEFT @ CLIP LE16  RIGHT @ CLIP LE16
        LOOP
    LOOP FLUSH ;
