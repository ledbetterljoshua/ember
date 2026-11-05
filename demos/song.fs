( song.fs: "Kindling", a 53-second chiptune that ember plays by computing
  every sample itself. It builds a sine table from one recurrence, tunes
  128 pitches from A440 by repeated twelfth roots of two, reads a tracker
  score and a lead sheet, and writes a 44.1 kHz 16-bit stereo WAV to stdout.

      ./ember core.fs demos/song.fs > kindling.wav && afplay kindling.wav

  Nothing else may print while it runs: every byte on stdout is audio. )

44100 CONSTANT RATE
5600 CONSTANT STEPLEN             ( one sixteenth note: about 118 bpm )
1 30 LSHIFT CONSTANT FIX          ( fixed-point 1.0 for tables and amplitudes )

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

( C D E F G A B sit 0 2 4 5 7 9 11 semitones up: twice the letter's place
  in the scale, less one from F onward. )
: SEMITONE ( letter -- n ) [ CHAR C ] LITERAL - 7 + 7 MOD DUP 2 * SWAP 2 > + ;
: NOTE ( addr -- midi )
    DUP C@ SEMITONE
    OVER 1+ C@ SHARP = IF 1+ THEN
    SWAP 2 + C@ '0' - 1+ 12 * + ;
: STEP-BYTE ( addr -- byte )
    DUP C@ '.' = IF DROP 0 EXIT THEN
    DUP C@ CARET = IF DROP 1 EXIT THEN
    NOTE ;
: TRACK[ ( -- addr n )
    HERE @
    BEGIN WORD DROP DUP C@ CLOSE <> WHILE
        DUP C@ BARLINE = IF DROP ELSE STEP-BYTE C, THEN
    REPEAT DROP HERE @ OVER - ALIGN ;

( Chords: a root letter, then # for sharp and m for minor. One byte each:
  the root's semitone, plus 16 when minor. )
: CHORD-BYTE ( addr len -- byte )
    OVER C@ SEMITONE -ROT
    BEGIN 1- DUP 0> WHILE
        2DUP + C@
        DUP SHARP = IF DROP ROT 1+ -ROT ELSE
            MINOR = IF ROT 16 + -ROT THEN THEN
    REPEAT 2DROP ;
: CHORDS[ ( -- addr n )
    HERE @
    BEGIN WORD OVER C@ CLOSE <> WHILE CHORD-BYTE C, REPEAT 2DROP
    HERE @ OVER - ALIGN ;

( The arrangement: one word per bar, each letter switching a part on.
  a arp, b bass, c crash, f final hit, h hats, k kick, l lead, r snare
  roll, s snare. Letter n sets bit n of a cell; "-" is a silent bar. )
: PARTS ( addr len -- mask )
    0 -ROT BEGIN DUP 0> WHILE
        1- 2DUP + C@
        DUP 'a' >= OVER 'z' <= AND IF
            'a' - 1 SWAP LSHIFT >R ROT R> OR -ROT
        ELSE DROP THEN
    REPEAT 2DROP ;
: ARRANGE[ ( -- addr bars )
    HERE @
    BEGIN WORD OVER C@ CLOSE <> WHILE PARTS , REPEAT 2DROP
    HERE @ OVER - 8 / ;
( PART k is the mask for letter k, folded to a literal when compiled. )
: PART ( "letter" -- mask ) IMMEDIATE
    CHAR 'a' - 1 SWAP LSHIFT STATE @ IF [COMPILE] LITERAL THEN ;

( ======================== the score ======================== )

ARRANGE[
    a       ah      ahkb    ahkb
    cahkbsl ahkbsl  ahkbsl  ahkbsl
    ahkbsl  ahkbsl  ahkbsl  ahkbsl
    ab      ab      abk     abkr
    cahkbsl ahkbsl  ahkbsl  ahkbsl
    ahkbsl  ahkbsl  ahkbsl  ahkbsl
    fcakbl  -
] CONSTANT BARS CONSTANT SHEET

CHORDS[ Am F C G ] CONSTANT CHORD# CONSTANT CHORDS

TRACK[
A-4 ... C-5 ... E-5 ... ... ... D-5 ... E-5 ... C-5 ... ... ... |
A-4 ... C-5 ... F-5 ... ... ... E-5 ... D-5 ... C-5 ... A-4 ... |
G-4 ... C-5 ... E-5 ... ... ... G-5 ... E-5 ... D-5 ... C-5 ... |
D-5 ... ... ... B-4 ... ... ... G-4 ... A-4 ... B-4 ... D-5 ... |
E-5 ... ... ... A-5 ... ... ... G-5 ... E-5 ... ... ... C-5 ... |
F-5 ... ... ... E-5 ... D-5 ... C-5 ... ... ... A-4 ... C-5 ... |
E-5 ... G-5 ... C-6 ... ... ... B-5 ... G-5 ... E-5 ... G-5 ... |
B-5 ... ... ... A-5 ... ... ... ... ... ... ... ^^^ ... ... ... |

A-4 ... C-5 ... E-5 ... ... ... D-5 ... E-5 ... C-5 ... ... ... |
A-4 ... C-5 ... F-5 ... ... ... E-5 ... D-5 ... C-5 ... A-4 ... |
G-4 ... C-5 ... E-5 ... ... ... G-5 ... E-5 ... D-5 ... C-5 ... |
D-5 ... ... ... B-4 ... ... ... G-4 ... A-4 ... B-4 ... D-5 ... |
E-5 ... ... ... A-5 ... ... ... G-5 ... E-5 ... ... ... C-5 ... |
F-5 ... ... ... E-5 ... D-5 ... C-5 ... ... ... A-4 ... C-5 ... |
E-5 ... G-5 ... C-6 ... ... ... B-5 ... C-6 ... D-6 ... ... ... |
B-5 ... ... ... G-5 ... ... ... A-5 ... B-5 ... D-6 ... C-6 B-5 |

A-5 ... ... ... ... ... ... ... ... ... ... ... ... ... ... ... |
] DROP CONSTANT MELODY

( One bar of each drum and the bass, sixteen steps. )
: KICKS  S" x...x...x...x..." DROP ;
: SNARES S" ....x.......x..." DROP ;
: HATS   S" -.x.-.x.-.x.-.xx" DROP ;
: BASSES S" x..x..o.x..x.o.x" DROP ;
( Arp steps pick chord tones: 0 root, 1 third, 2 fifth, 3 octave, 4 tenth. )
: ARPS   S" 0123432101234321" DROP ;

( ======================== the synthesizer ======================== )

( Amplitudes are fractions of FIX; DECAY multiplies by k/65536 per sample. )
: PCT ( n -- amp ) FIX * 100 / ;
: SCALE ( s amp -- s' ) * FIX / ;
: DECAY ( var k -- ) OVER @ * 65536 / SWAP ! ;
( One-pole lowpass: y += (x - y) * c / 256. )
: SMOOTH ( x var c -- y ) >R DUP @ ROT SWAP - R> * 256 / OVER +! @ ;

( Arp: a sine and its octave, plucked, bouncing left and right. )
VARIABLE A.PH  VARIABLE A.INC  VARIABLE A.AMP  VARIABLE A.PAN
: ARP ( -- s )
    A.INC @ A.PH +!
    A.PH @ SIN  A.PH @ 2 * SIN 3 / +  A.AMP @ SCALE
    A.AMP 65524 DECAY ;

( Lead: a square wave with a slow vibrato, through a lowpass. )
VARIABLE L.PH  VARIABLE L.INC  VARIABLE L.AMP  VARIABLE L.K  VARIABLE L.Y
VARIABLE VIB
: LEAD ( -- s )
    535653 VIB +!                                   ( 5.5 Hz )
    L.INC @ DUP VIB @ SIN * 16384 / 300 / + L.PH +!
    L.PH @ 31 RSHIFT 1 AND IF 16384 ELSE -16384 THEN
    L.AMP @ SCALE  L.Y 70 SMOOTH
    L.AMP L.K @ DECAY ;

( Bass: a sawtooth, darker. )
VARIABLE B.PH  VARIABLE B.INC  VARIABLE B.AMP  VARIABLE B.Y
: BASS ( -- s )
    B.INC @ B.PH +!
    B.PH @ 17 RSHIFT 32767 AND 16384 -  B.AMP @ SCALE  B.Y 30 SMOOTH
    B.AMP 65530 DECAY ;

( Kick: a sine whose pitch falls from 160 Hz toward 45 Hz. )
VARIABLE K.PH  VARIABLE K.INC  VARIABLE K.AMP
: KICK ( -- s )
    K.INC @ K.PH +!
    K.PH @ SIN K.AMP @ SCALE
    K.AMP 65526 DECAY
    K.INC @ 65509 * 65536 / 4382620 MAX K.INC ! ;

( Snare: noise over a 190 Hz body. )
VARIABLE S.PH  VARIABLE S.AMP
: SNARE ( -- s )
    18504091 S.PH +!
    NOISE S.PH @ SIN 2 / +  S.AMP @ SCALE
    S.AMP 65511 DECAY ;

( Hats and crash: differenced noise, so only the fizz is left. )
VARIABLE H.AMP  VARIABLE H.K  VARIABLE H.PREV
: HAT ( -- s )
    NOISE DUP H.PREV @ - SWAP H.PREV !  H.AMP @ SCALE
    H.AMP H.K @ DECAY ;

( Ping-pong echo, a dotted eighth long: the left line feeds the right. )
STEPLEN 3 * CONSTANT DELAY
HERE @ DELAY CELLS ALLOT CONSTANT EL
HERE @ DELAY CELLS ALLOT CONSTANT ER
EL DELAY CELLS 0 FILL  ER DELAY CELLS 0 FILL
VARIABLE EI  VARIABLE DL  VARIABLE DR
: ECHO ( send -- )
    EI @ CELLS DUP EL + @ DL !  ER + @ DR !
    DR @ 115 * 256 / +  EI @ CELLS EL + !
    DL @ 115 * 256 / EI @ CELLS ER + !
    EI @ 1+ DUP DELAY = IF DROP 0 THEN EI ! ;

( ---- Mixing one stereo frame. Pan runs 0 (left) to 256 (right). ---- )
VARIABLE LEFT  VARIABLE RIGHT
: MIX ( s pan -- ) 2DUP * 256 / RIGHT +!  256 SWAP - * 256 / LEFT +! ;
: CLIP ( n -- n ) -32767 MAX 32767 MIN ;
: FRAME
    0 LEFT !  0 RIGHT !
    KICK 128 MIX  BASS 128 MIX  SNARE 128 MIX  HAT 170 MIX
    ARP DUP A.PAN @ MIX 3 /
    LEAD DUP 128 MIX 2 / +  ECHO
    DL @ LEFT +!  DR @ RIGHT +!
    LEFT @ CLIP LE16  RIGHT @ CLIP LE16 ;

( ---- The sequencer: runs once per sixteenth, deciding who plays. ---- )
VARIABLE STEP#  VARIABLE LSTEP
: BAR ( -- n ) STEP# @ 16 / ;
: POS ( -- n ) STEP# @ 16 MOD ;
: ON? ( mask -- f ) BAR CELLS SHEET + @ AND ;
: HIT? ( "on this step" ) PART f ON? NOT POS 0= OR ;
: CHORD ( -- byte ) BAR CHORD# MOD CHORDS + C@ ;
: ROOT ( -- n ) CHORD 15 AND ;
: THIRD ( -- n ) CHORD 16 AND IF 3 ELSE 4 THEN ;
: TONE ( t -- semis )
    DUP 3 >= IF 12 SWAP 3 - ELSE 0 SWAP THEN
    DUP 1 = IF DROP THIRD ELSE 2 = IF 7 ELSE 0 THEN THEN + ;
: STEPS ( addr -- c ) POS + C@ ;
CHAR x CONSTANT 'x'
CHAR o CONSTANT 'o'

: PLAY-ARP
    ARPS STEPS '0' - TONE ROOT + 60 + HZ A.INC !
    30 PCT A.AMP !  POS 1 AND IF 60 ELSE 196 THEN A.PAN ! ;
: PLAY-BASS
    BASSES STEPS DUP 'x' = IF DROP 36 ELSE 'o' = IF 48 ELSE EXIT THEN THEN
    ROOT + HZ B.INC !  70 PCT B.AMP ! ;
: PLAY-KICK  0 K.PH !  15582647 K.INC !  100 PCT K.AMP ! ;
: PLAY-HAT ( pct k -- ) H.K ! PCT H.AMP ! ;
: PLAY-LEAD
    MELODY LSTEP @ + C@  1 LSTEP +!
    DUP 0= IF DROP EXIT THEN
    DUP 1 = IF DROP 65300 L.K ! EXIT THEN
    HZ L.INC !  45 PCT L.AMP !  65534 L.K ! ;

: TRIGGER
    HIT? IF
        PART a ON? IF PLAY-ARP THEN
        PART b ON? IF PLAY-BASS THEN
        PART k ON? IF KICKS STEPS 'x' = IF PLAY-KICK THEN THEN
    THEN
    PART s ON? IF SNARES STEPS 'x' = IF 0 S.PH ! 50 PCT S.AMP ! THEN THEN
    PART r ON? IF 0 S.PH ! POS 3 * 8 + PCT S.AMP ! THEN
    PART h ON? IF HATS STEPS DUP 'x' = IF DROP 22 65462 PLAY-HAT ELSE
        [ CHAR - ] LITERAL = IF 10 65462 PLAY-HAT THEN THEN THEN
    PART c ON? POS 0= AND IF 32 65532 PLAY-HAT THEN
    PART l ON? IF PLAY-LEAD THEN ;

( ---- The file. ---- )
BARS 16 * STEPLEN * CONSTANT FRAMES
: HEADER
    S" RIFF" TELL  FRAMES 4 * 36 + LE32  S" WAVE" TELL
    S" fmt " TELL  16 LE32  1 LE16  2 LE16
    RATE LE32  RATE 4 * LE32  4 LE16  16 LE16
    S" data" TELL  FRAMES 4 * LE32 ;
: SONG
    HEADER
    BARS 16 * 0 DO
        I STEP# !  TRIGGER
        STEPLEN 0 DO FRAME LOOP
    LOOP FLUSH ;

SONG
