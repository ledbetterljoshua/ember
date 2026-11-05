( lantern.fs: "Lantern", a music-box waltz in D major and 3/4, played on
  a pool of twelve bell voices over a slow gliding pad and the crackle of
  an old record. Uses the studio in lib/tracker.fs.

      ./ember core.fs lib/tracker.fs demos/lantern.fs > lantern.wav
      afplay lantern.wav                                                  )

15750 STEPLEN !                   ( one eighth note: 84 beats a minute )
6 METER !                         ( six eighths: three beats to the bar )
STEPLEN @ 2 * DELAY !  150 FEEDBACK !     ( a long, soft echo, one beat )

( Arrangement letters: c the waltz chords, f final bar (one chord only),
  m the melody, n the record crackle, p the pad. )

( ======================== the score ======================== )

ARRANGE[
    n
    nc   nc   ncp  ncp
    ncpm ncpm ncpm ncpm  ncpm ncpm ncpm ncpm
    ncpm ncpm ncpm ncpm  ncpm ncpm ncpm ncpm
    ncpm ncpm ncpmf
    n    n
]

CHORDS[
    D
    D  Bm  G   A
    D  F#m Bm  G   D  Em  A   D
    D  F#m Bm  G   D  Em  A   D
    Bm G   D
    D  D
]

TRACK[
A-4 ... D-5 ... F#5 ... |
E-5 ... ... ... C#5 ... |
D-5 ... B-4 ... F#4 ... |
G-4 ... ... ... B-4 A-4 |
A-4 ... D-5 ... F#5 ... |
G-5 ... F#5 ... E-5 ... |
E-5 ... A-4 ... C#5 ... |
D-5 ... ... ... ... ... |

A-5 ... D-6 ... F#6 ... |
E-6 ... ... ... C#6 ... |
D-6 ... B-5 ... F#5 ... |
G-5 ... ... ... B-5 A-5 |
A-5 ... D-6 ... F#6 ... |
G-6 ... F#6 ... E-6 ... |
E-6 ... C#6 ... B-5 C#6 |
D-6 ... ... ... A-5 ... |

B-5 ... ... ... F#5 ... |
G-5 ... F#5 ... E-5 ... |
D-5 F#5 A-5 D-6 ... ... |
] DROP CONSTANT MELODY

( ======================== the instruments ======================== )

( Bells: a pool of voices, each a sine plus the inharmonic partial at 6.27
  times the fundamental that makes a struck steel tine go "tink". VOICES
  builds a field with one cell per voice:  i V.AMP  is voice i's address. )
12 CONSTANT NV
: VOICES ( "name" -- ) <BUILDS NV CELLS ALLOT DOES> SWAP CELLS + ;
VOICES V.PH  VOICES V.INC  VOICES V.AMP  VOICES V.TINK  VOICES V.PAN
VARIABLE NEXTV  VARIABLE SEND

: BELL ( i -- )
    DUP V.AMP @ 0= IF DROP EXIT THEN
    DUP V.INC @ OVER V.PH +!
    DUP V.PH @ DUP SIN 2 PICK V.AMP @ SCALE
    SWAP 627 * 100 / SIN 2 PICK V.TINK @ SCALE +
    DUP SEND +!  OVER V.PAN @ MIX
    DUP V.AMP 65534 DECAY  DUP V.TINK 65486 DECAY
    DUP V.AMP @ 200000 < IF 0 SWAP V.AMP ! ELSE DROP THEN ;

( Strike the next voice in the pool, a little louder or softer each time,
  the way a pin catches a tine. )
: RING ( midi amp pan -- )
    NEXTV @ >R
    R@ V.PAN !
    DUP NOISE * 16384 / 8 / +
    DUP R@ V.AMP !  2 / R@ V.TINK !
    HZ R@ V.INC !  0 R@ V.PH !
    R> 1+ NV MOD NEXTV ! ;

( Pad: a root and a just fifth, each on two sines a few cents apart, one
  per side. It glides from chord to chord and swells in and out. )
VARIABLE P.INC  VARIABLE P.TINC  VARIABLE P.AMP  VARIABLE P.TARGET
VARIABLE P.PH1  VARIABLE P.PH2  VARIABLE P.PH3  VARIABLE P.PH4
: WARM ( phase -- s ) DUP SIN SWAP 2 * SIN 4 / + ;
: PAD
    P.TINC @ P.INC @ - 4096 / P.INC +!
    P.TARGET @ P.AMP @ - 16384 / P.AMP +!
    P.INC @  DUP P.PH1 +!  DUP 1003 * 1000 / P.PH2 +!
    3 * 2 /  DUP P.PH3 +!  997 * 1000 / P.PH4 +!
    P.PH1 @ WARM P.PH3 @ WARM + P.AMP @ SCALE LEFT +!
    P.PH2 @ WARM P.PH4 @ WARM + P.AMP @ SCALE RIGHT +! ;

( Record crackle: rare pops, softened, over a whisper of hiss. )
VARIABLE POP  VARIABLE POP.Y  VARIABLE NIGHT
: CRACKLE ( -- s )
    NOISE 16376 > IF NOISE 2 / POP ! THEN
    POP @ NOISE 200 / +  POP.Y 90 SMOOTH
    POP 50000 DECAY ;

( ---- One stereo frame. ---- )
: FRAME
    0 SEND !
    NV 0 DO I BELL LOOP
    PAD
    NIGHT @ IF CRACKLE 128 MIX THEN
    SEND @ 3 / ECHO ;

( ---- The sequencer: once per eighth note. ---- )
VARIABLE MSTEP
( Chord tones voiced between F3 and E4, so the melody sits above them. )
: CHORD-NOTE ( t -- midi ) TONE ROOT 48 + DUP 53 < IF 12 + THEN + ;
: OOM  0 CHORD-NOTE 12 -  28 PCT 128 RING ;
: PAH  1 CHORD-NOTE 11 PCT 70 RING  2 CHORD-NOTE 11 PCT 186 RING
       3 CHORD-NOTE 8 PCT 128 RING ;
: PLAY-MELODY
    MELODY MSTEP @ + C@  1 MSTEP +!
    DUP 2 < IF DROP EXIT THEN
    42 PCT 150 RING ;

: TRIGGER
    PART n ON? NIGHT !
    ROOT 48 + HZ P.TINC !
    P.INC @ 0= IF P.TINC @ P.INC ! THEN
    PART p ON? IF 14 PCT ELSE 0 THEN P.TARGET !
    PART c ON? IF
        PART f ON? IF
            POS 0= IF OOM PAH THEN
        ELSE
            POS 0= IF OOM THEN
            POS 2 = POS 4 = OR IF PAH THEN
        THEN
    THEN
    PART m ON? IF PLAY-MELODY THEN ;

' TRIGGER ' FRAME RENDER
