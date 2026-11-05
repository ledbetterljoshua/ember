( song.fs: "Kindling", a 53-second chiptune that ember plays by computing
  every sample itself, using the studio in lib/tracker.fs.

      ./ember core.fs lib/tracker.fs demos/song.fs > kindling.wav
      afplay kindling.wav                                                 )

5600 STEPLEN !                    ( one sixteenth note: about 118 bpm )
16 METER !
STEPLEN @ 3 * DELAY !  115 FEEDBACK !     ( a dotted-eighth echo )

( Arrangement letters: a arp, b bass, c crash, f final hit, h hats,
  k kick, l lead, r snare roll, s snare. )

( ======================== the score ======================== )

ARRANGE[
    a       ah      ahkb    ahkb
    cahkbsl ahkbsl  ahkbsl  ahkbsl
    ahkbsl  ahkbsl  ahkbsl  ahkbsl
    ab      ab      abk     abkr
    cahkbsl ahkbsl  ahkbsl  ahkbsl
    ahkbsl  ahkbsl  ahkbsl  ahkbsl
    fcakbl  -
]

CHORDS[ Am F C G ]

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
( Arp steps pick chord tones, see TONE. )
: ARPS   S" 0123432101234321" DROP ;

( ======================== the synthesizer ======================== )

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

( ---- One stereo frame. ---- )
: FRAME
    KICK 128 MIX  BASS 128 MIX  SNARE 128 MIX  HAT 170 MIX
    ARP DUP A.PAN @ MIX 3 /
    LEAD DUP 128 MIX 2 / +  ECHO ;

( ---- The sequencer: runs once per sixteenth, deciding who plays. ---- )
VARIABLE LSTEP   ( the lead's own cursor: it advances only while the lead plays )
: HIT? ( "on this step" ) PART f ON? NOT POS 0= OR ;
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

' TRIGGER ' FRAME RENDER
