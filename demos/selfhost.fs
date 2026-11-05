( selfhost.fs: "Self-Hosting", about two and a half minutes, in F minor and
  then G minor, at 100 bpm with a little swing.

  A compiler is self-hosting when it can build itself. ember starts as a few
  hundred lines of assembly and grows the rest of itself in its own language,
  and this piece does the same with one idea: a four-note figure, C Ab G F,
  played once on a bell into an empty room. Everything after it is grown
  from that figure. The pads are its harmony, the pluck is its harmony
  broken into pieces, the lead is its melody stretched over eight bars, and
  the groove is what happens when you let it repeat. Then the floor drops
  out, the chords turn to the relative major, and the whole thing climbs
  back a whole step higher than where it began. At the end the bell plays
  alone again, and its last note is a major third it never had before.

      ./ember core.fs lib/tracker.fs lib/synth.fs demos/selfhost.fs > out.wav
      afplay out.wav                                                       )

6615 STEPLEN !                    ( a sixteenth at 100 bpm )
16 METER !
600 SWING !                       ( even sixteenths a little long )
80000 MASTER !
57700 ROOM !                      ( a larger room than the default )
STEPLEN @ 3 * DELAY !  96 FEEDBACK !      ( a dotted-eighth echo )

( Arrangement letters:
    b bell figure    p pads          a slow arp     A busy arp
    s sub bass       m groove bass   M lift bass
    k kick           K four-on-the-floor kick       n snare and ghosts
    r snare roll     h hats          H hats with open hats
    l lead           o lead an octave up             u everything up a tone
    c crash          z riser         f the last chord
    I pads opening   D pads dark     O pads wide open )

( ======================== the score ======================== )

ARRANGE[
    b       b       pbI     pbI     pbI     pbI     pbI     pbI
    pbas    pbas    pbas    pbas    pbash   pbash   pbash   pbash
    cpAmknh pAmknh  pAmknh  pAmknh  pAmknh  pAmknh  pAmknh  pAmknh
    pAmknhl pAmknhl pAmknhl pAmknhl pAmknhl pAmknhl pAmknhl pAmknhl
    pbD     pbD     pbDa    pbDa    pbDa    pbDa    pbDz    pbDzr
    cpAMKnHlOu      pAMKnHlOu       pAMKnHlOu       pAMKnHlOu
    pAMKnHlOu       pAMKnHlOu       pAMKnHlOu       pAMKnHlOu
    cpAMKnHlOuob    pAMKnHlOuob     pAMKnHlOuob     pAMKnHlOuob
    pAMKnHlOuob     pAMKnHlOuob     pAMKnHlOuob     pAMKnHlOuob
    cpasbu  pasbu   pasbu   pasbu   psbu    psbu    pbu     pbu
    pfu     -
]

CHORDS[
    Fm9 Dbmaj7 Bbm9 C7   Fm9 Dbmaj7 Bbm9 C7
    Fm9 Dbmaj7 Bbm9 C7   Fm9 Dbmaj7 Bbm9 C7
    Fm9 Dbmaj7 Bbm9 C7   Fm9 Dbmaj7 Bbm9 C7
    Fm9 Dbmaj7 Bbm9 C7   Fm9 Dbmaj7 Bbm9 C7
    Abmaj7 Eb/G Fm7 Dbmaj9   Bbm7 Eb/G Cm7 D7
    Gm9 Ebmaj7 Cm9 D7    Gm9 Ebmaj7 Cm9 D7
    Gm9 Ebmaj7 Cm9 D7    Gm9 Ebmaj7 Cm9 D7
    Gm9 Ebmaj7 Cm9 D7    Ebmaj7 Cm9 D7sus Gsus2
    G G
]

( The figure, two bars, on the bell. )
NOTES[
C-68 ... ... Ab57 ... ... G-56 ... ... ... F-57 ... ... ... ... ... |
... ... ... ... ... ... ... ... Eb56 ... F-56 ... G-55 ... ... ... |
] CONSTANT FIGURE# CONSTANT FIGURE

( The lead: the figure stretched over eight bars. )
NOTES[
... ... C-56 ... Eb57 ... F-58 ... ... ... Ab57 ... G-56 ... F-56 ... |
Eb58 ... ... ... ... ... C-56 ... ... ... Ab46 ... Bb46 ... C-57 ... |
Db58 ... ... ... C-56 ... Bb46 ... ... ... Ab46 ... F-45 ... Ab46 ... |
G-47 ... ... ... ... ... ... ... ^^^ ... ... ... C-56 ... E-57 ... |
F-58 ... ... ... ... ... Ab57 ... G-56 ... F-56 ... Eb56 ... C-56 ... |
... ... Db56 ... Eb57 ... F-57 ... ... ... Ab57 ... C-68 ... ... ... |
Bb58 ... ... ... Ab57 ... F-56 ... ... ... Db56 ... C-56 ... Bb45 ... |
C-57 ... ... ... E-57 ... G-57 ... Bb58 ... ... ... ^^^ ... ... ... |
] CONSTANT MELODY# CONSTANT MELODY

( Sixteen steps a bar. Drums are velocities; bass and arps name chord tones. )
: KICKS     S" 9.....8.9.....6." DROP ;
: FLOOR     S" 9...9...9...9..6" DROP ;
: SNARES    S" ....9..2..3.9..4" DROP ;
: HATS      S" 7.4.7.4.7.4.7.47" DROP ;
: CLOSED    S" 7.3..3.37.3..3.3" DROP ;
: OPENS     S" ..6...6...6...6." DROP ;
: SUB       S" r---------------" DROP ;
: GROOVE    S" R-.r--5.o-.r-5-p" DROP ;
: LIFT      S" R.rR.r5.o.r57.rp" DROP ;
: SLOW-ARP  S" 0..1..2..3..4..2" DROP ;
: BUSY-ARP  S" 0.2.4.1.3.5.2.4." DROP ;

( ======================== the conductor ======================== )

VARIABLE FCUR  VARIABLE LCUR
32 STEPLEN @ * RISE-LEN !

: PAD-COLOR ( -- hz )
    PART I ON? IF BAR 150 * 300 + EXIT THEN
    PART D ON? IF 450 EXIT THEN
    PART O ON? IF 2600 EXIT THEN
    1300 ;

: BAR-START
    PART p ON? IF PAD-CHORD ELSE PADS-OFF THEN
    PAD-COLOR PAD-CUT !
    PART c ON? IF 8 VEL CRASH! THEN
    PART l ON? 0= IF LEAD-OFF THEN
    PART s ON? PART m ON? OR PART M ON? OR 0= IF BASS-OFF THEN
    PART f ON? IF 0 83 CHORD-NOTE 8 VEL BELL! THEN ;

: FIGURE-STEP
    FIGURE FIGURE# FCUR TRACK-STEP
    OVER 2 < IF 2DROP EXIT THEN BELL! ;
: LEAD-STEP
    MELODY MELODY# LCUR TRACK-STEP
    OVER 0= IF 2DROP EXIT THEN
    OVER 1 = IF 2DROP LEAD-OFF EXIT THEN
    SWAP PART o ON? IF 12 + THEN SWAP LEAD! ;

: TRIGGER
    PART u ON? IF 2 ELSE 0 THEN TRANSPOSE !
    PART z ON? RISING !
    POS 0= IF BAR-START THEN
    PART b ON? IF FIGURE-STEP THEN
    PART l ON? IF LEAD-STEP THEN
    PART a ON? IF SLOW-ARP ARP-STEP THEN
    PART A ON? IF BUSY-ARP ARP-STEP THEN
    PART s ON? IF SUB BASS-STEP THEN
    PART m ON? IF GROOVE BASS-STEP THEN
    PART M ON? IF LIFT BASS-STEP THEN
    PART k ON? IF KICKS ' KICK! PLAY THEN
    PART K ON? IF FLOOR ' KICK! PLAY THEN
    PART n ON? IF SNARES ' SNARE! PLAY THEN
    PART r ON? IF POS 2 / 2 + VEL SNARE! THEN
    PART h ON? IF HATS ' HAT! PLAY THEN
    PART H ON? IF CLOSED ' HAT! PLAY  OPENS ' OPEN-HAT! PLAY THEN ;

' TRIGGER ' STUDIO RENDER
