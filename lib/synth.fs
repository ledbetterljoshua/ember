( lib/synth.fs: instruments and effects for lib/tracker.fs, all in integer
  fixed point. Load after the tracker:

      ./ember core.fs lib/tracker.fs lib/synth.fs demos/piece.fs > out.wav

  Units used throughout:
    samples   about +-32768 is full scale, summed in 64-bit cells
    gains     Q16, 65536 = 1.0, applied with G*
    phases    32-bit fractions of a turn; HZ gives a note's increment
    decays    Q20 per-sample factors, made from a time constant by TAU
    envelope  levels are Q24 inside and come out Q16

  A song ends with   ' TRIGGER ' STUDIO RENDER   where TRIGGER strikes
  instruments with the words that end in ! and STUDIO runs them all. )

( ---- Fixed point ---- )
: 2OVER ( a b c d -- a b c d a b ) 3 PICK 3 PICK ;
: G* ( s g -- s' ) * 65536 / ;
: MSEC ( ms -- samples ) RATE * 1000 / ;
: TAU ( ms -- k ) MSEC 1 MAX 1048576 SWAP / 1048576 SWAP - ;
: FADE ( var k -- ) OVER @ * 1048576 / SWAP ! ;
( Velocity 1-9 to a gain, squared so ghost notes really are quiet. )
: VEL ( 1-9 -- g ) DUP * 65536 * 81 / ;
: HZ>INC ( hz -- inc ) 97391 * ;
4294967295 CONSTANT MASK32
( Fields: offset size "name" -- offset'. The word adds its offset. )
: +FIELD <BUILDS OVER , + DOES> @ + ;
: FIELD ( offset "name" -- offset' ) 8 +FIELD ;

( ---- Band-limited oscillators. A naive sawtooth jumps once a cycle and
  that jump aliases. PolyBLEP subtracts a two-sample polynomial step from
  either side of the jump. In Q15: x is the distance to the jump in units
  of one sample's phase step. ---- )
: SAW ( ph inc -- s )
    OVER 16 RSHIFT 32768 - -ROT
    2DUP < IF                              ( just after the jump )
        >R 32768 * R> /
        DUP DUP * 32768 / SWAP 2 * SWAP - 32768 -  -  EXIT
    THEN
    2DUP + MASK32 > IF                     ( just before it )
        >R 4294967296 - 32768 * R> /
        DUP DUP * 32768 / SWAP 2 * + 32768 +  -  EXIT
    THEN 2DROP ;
( The same wave with no correction, kept for the aliasing test. )
: NAIVE-SAW ( ph inc -- s ) DROP 16 RSHIFT 32768 - ;
( A square is a saw minus the same saw half a cycle later. )
: SQUARE ( ph inc -- s )
    2DUP SAW -ROT SWAP 2147483648 + MASK32 AND SWAP SAW - 2 / ;
( Advance a phase variable and read a band-limited saw from it. )
: SAW-OSC ( phvar inc -- s ) TUCK OVER @ + MASK32 AND TUCK SWAP ! SWAP SAW ;

( ---- ADSR. Attack is linear, decay and release are exponential. ---- )
0 FIELD E.LEVEL  FIELD E.STAGE  FIELD E.ATK  FIELD E.DEC  FIELD E.SUS  FIELD E.REL
CONSTANT /ENV
16777216 CONSTANT ENV-TOP
: ADSR! ( attack-ms decay-ms sustain% release-ms env -- )
    >R  TAU R@ E.REL !
    ENV-TOP * 100 / R@ E.SUS !
    TAU R@ E.DEC !
    MSEC 1 MAX ENV-TOP SWAP / R@ E.ATK !
    R> DROP ;
: GATE ( env -- ) 1 SWAP E.STAGE ! ;
: UNGATE ( env -- ) DUP E.STAGE @ IF 3 SWAP E.STAGE ! ELSE DROP THEN ;
: REST ( env -- ) 0 OVER E.STAGE ! 0 SWAP E.LEVEL ! ;
: ATTACKS ( env -- )
    DUP E.ATK @ OVER E.LEVEL +!
    DUP E.LEVEL @ ENV-TOP >= IF ENV-TOP OVER E.LEVEL ! 2 SWAP E.STAGE ! ELSE DROP THEN ;
: DECAYS ( env -- )
    DUP E.LEVEL @ OVER E.SUS @ - OVER E.DEC @ * 20 RSHIFT OVER E.SUS @ +
    DUP 256 < IF DROP REST ELSE SWAP E.LEVEL ! THEN ;
: RELEASES ( env -- )
    DUP E.LEVEL @ OVER E.REL @ * 20 RSHIFT
    DUP 256 < IF DROP REST ELSE SWAP E.LEVEL ! THEN ;
: ENV ( env -- g )
    DUP E.STAGE @
    DUP 2 = IF DROP DUP DECAYS ELSE
    DUP 1 = IF DROP DUP ATTACKS ELSE
    3 = IF DUP RELEASES THEN THEN THEN
    E.LEVEL @ 8 RSHIFT ;
: SOUNDING? ( env -- f ) E.STAGE @ DUP 1 = SWAP 2 = OR ;

( ---- State-variable filter (Chamberlin), resonant, two poles.
  f = 2 sin(pi fc / rate), about fc * 2 pi / rate below a few kHz; q is
  1/Q in Q16, so 65536 is gentle and 10000 rings. ---- )
0 FIELD S.LOW  FIELD S.BAND  FIELD S.HIGH  CONSTANT /SVF
: HZ>F ( hz -- f ) 9337 * 1000 / 20 MAX 65536 MIN ;
VARIABLE SF  VARIABLE SQ
: SVF ( in f q svf -- low )
    >R SQ ! SF !
    R@ S.BAND @ SF @ * 65536 / R@ S.LOW +!
    R@ S.LOW @ -  R@ S.BAND @ SQ @ * 65536 / -
    DUP R@ S.HIGH !
    SF @ * 65536 / R@ S.BAND +!
    R> S.LOW @ ;

( ---- Two-operator FM: a sine whose phase is pushed around by another.
  The index is in hundredths of a radian. ---- )
: FM ( carrier-ph modulator-ph index -- s ) SWAP SIN * 417 * + SIN ;

( ---- Soft clip: x(27 + x^2) / (27 + 9x^2), a rational tanh. Full scale
  in is 1.0; it reaches 1.0 out at three times that. ---- )
: SOFTCLIP ( x -- y )
    -98304 MAX 98304 MIN
    DUP DUP * 32768 /
    DUP 884736 + SWAP 9 * 884736 +
    >R * R> / ;

( ---- Buses. Instruments add to LEFT and RIGHT, and send to the echo and
  the reverb. ---- )
VARIABLE ESEND  VARIABLE RSEND

( ---- Sidechain: the kick pulls DUCK down and it climbs back, and the pads
  and bass are multiplied by it, so the kick breathes through them. ---- )
VARIABLE DUCK  65536 DUCK !
VARIABLE DUCK-DEPTH  30000 DUCK-DEPTH !
VARIABLE DUCK-K  140 TAU DUCK-K !
( A hit sets a floor that DUCK falls to over a few milliseconds, since an
  instant drop in gain is itself a click; then it climbs back. )
VARIABLE DUCK-FLOOR  65536 DUCK-FLOOR !
3 TAU CONSTANT DUCK-ATTACK
: DUCK-HIT ( vel -- ) DUCK-DEPTH @ G* 65536 SWAP - DUCK-FLOOR ! ;
: DUCK~
    DUCK-FLOOR @ 65536 < IF
        DUCK @ DUCK-FLOOR @ - DUCK-ATTACK * 1048576 / DUCK-FLOOR @ + DUCK !
        DUCK @ DUCK-FLOOR @ - 64 < IF 65536 DUCK-FLOOR ! THEN EXIT
    THEN
    65536 DUCK @ - DUCK-K @ * 1048576 / 65536 SWAP - DUCK ! ;
( Half the duck, for things that should only lean back a little. )
: HALF-DUCK ( s -- s' ) DUCK @ 65536 + 2 / G* ;

( ---- Reverb after Freeverb: eight damped combs in parallel, then four
  allpasses in series, per side; the right side's lines are 23 samples
  longer so the two sides decorrelate. ---- )
: COMB ( len "name" -- ) <BUILDS DUP , 0 , 0 , CELLS ALLOT DOES> ;
: ALLPASS ( len "name" -- ) <BUILDS DUP , 0 , CELLS ALLOT DOES> ;
VARIABLE ROOM  55050 ROOM !        ( comb feedback, 0.84 )
VARIABLE DAMP  13107 DAMP !        ( 0.2: how fast highs die in the tail )
VARIABLE RWET  2200 RWET !
VARIABLE CIN
: NEXT-SLOT ( line -- ) DUP 8+ @ 1+ DUP 2 PICK @ >= IF DROP 0 THEN SWAP 8+ ! ;
: COMB-RUN ( in comb -- out )
    SWAP CIN !
    DUP 8+ @ CELLS OVER 24 + +
    DUP @
    DUP 65536 DAMP @ - G*  3 PICK 16 + @ DAMP @ G* +
    DUP 4 PICK 16 + !
    ROOM @ G* CIN @ + ROT !
    SWAP NEXT-SLOT ;
: AP-RUN ( in allpass -- out )
    DUP 8+ @ CELLS OVER 16 + +
    DUP @
    DUP 2 / 4 PICK +
    ROT !
    ROT - SWAP NEXT-SLOT ;
1116 COMB CL1  1188 COMB CL2  1277 COMB CL3  1356 COMB CL4
1422 COMB CL5  1491 COMB CL6  1557 COMB CL7  1617 COMB CL8
1139 COMB CR1  1211 COMB CR2  1300 COMB CR3  1379 COMB CR4
1445 COMB CR5  1514 COMB CR6  1580 COMB CR7  1640 COMB CR8
556 ALLPASS AL1  441 ALLPASS AL2  341 ALLPASS AL3  225 ALLPASS AL4
579 ALLPASS AR1  464 ALLPASS AR2  364 ALLPASS AR3  248 ALLPASS AR4
: REVERB-L ( in -- out )
    DUP CL1 COMB-RUN OVER CL2 COMB-RUN + OVER CL3 COMB-RUN +
    OVER CL4 COMB-RUN + OVER CL5 COMB-RUN + OVER CL6 COMB-RUN +
    OVER CL7 COMB-RUN + SWAP CL8 COMB-RUN +
    AL1 AP-RUN AL2 AP-RUN AL3 AP-RUN AL4 AP-RUN ;
: REVERB-R ( in -- out )
    DUP CR1 COMB-RUN OVER CR2 COMB-RUN + OVER CR3 COMB-RUN +
    OVER CR4 COMB-RUN + OVER CR5 COMB-RUN + OVER CR6 COMB-RUN +
    OVER CR7 COMB-RUN + SWAP CR8 COMB-RUN +
    AR1 AP-RUN AR2 AP-RUN AR3 AP-RUN AR4 AP-RUN ;
: REVERB ( in -- l r ) 4 / DUP REVERB-L SWAP REVERB-R ;

( ---- Voices. Every pitched instrument uses this layout. ---- )
0 FIELD >PH1  FIELD >PH2  FIELD >INC  FIELD >INC2  FIELD >TINC
  FIELD >NOTE  FIELD >VEL  FIELD >TVEL  FIELD >PAN
  /ENV +FIELD >AMP  /ENV +FIELD >FENV  /SVF +FIELD >FILT
CONSTANT /VOICE
: VOICES ( n "name" -- ) <BUILDS /VOICE * ALLOT DOES> SWAP /VOICE * + ;
( Pull a voice's increment toward its target: k/65536 of the gap a sample. )
: GLIDE ( v k -- ) OVER >TINC @ 2 PICK >INC @ - * 65536 / SWAP >INC +! ;
( Legato voices change loudness the same way, over about 6 ms, because a
  sounding note that jumps in gain clicks. )
: EASE-VEL ( v -- ) DUP >TVEL @ OVER >VEL @ - 256 / SWAP >VEL +! ;
( Chord tone n counting up from midi note lo, through the octaves. )
: CHORD-NOTE ( n lo -- midi )
    BEGIN
        DUP ROOT - 12 + 12 MOD 1 SWAP LSHIFT INTERVALS AND IF
            OVER 0= IF NIP EXIT THEN SWAP 1- SWAP
        THEN 1+
    AGAIN ;
: TONES ( -- n ) 0 12 0 DO INTERVALS I RSHIFT 1 AND + LOOP ;
VARIABLE TRANSPOSE

( ---- Pad: up to eight voices, each two saws a few cents apart, one per
  side, through one resonant lowpass per side whose cutoff the song moves.
  New chords keep the notes they share and release the rest. ---- )
8 CONSTANT NPAD
NPAD VOICES PAD
VARIABLE PAD-LO  55 PAD-LO !
VARIABLE PAD-CUT  900 PAD-CUT !
VARIABLE PAD-Q  40000 PAD-Q !
VARIABLE PAD-LEVEL  3600 PAD-LEVEL !
VARIABLE PAD-SEND  20000 PAD-SEND !
VARIABLE PADL  VARIABLE PADR  VARIABLE PAD-F
HERE @ /SVF ALLOT CONSTANT PADFL  HERE @ /SVF ALLOT CONSTANT PADFR
: PAD-SHAPE ( a d s r -- ) NPAD 0 DO 2OVER 2OVER I PAD >AMP ADSR! LOOP 2DROP 2DROP ;
900 1500 80 1800 PAD-SHAPE
: PAD-VOICE ( v -- )
    DUP >AMP DUP E.STAGE @ 0= IF 2DROP EXIT THEN ENV
    OVER >PH1 2 PICK >INC @ SAW-OSC OVER G* PADL +!
    SWAP DUP >PH2 SWAP >INC2 @ SAW-OSC SWAP G* PADR +! ;
: PADS
    0 PADL !  0 PADR !
    NPAD 0 DO I PAD PAD-VOICE LOOP
    PAD-F @ PAD-CUT @ HZ>F OVER - 1024 / + DUP PAD-F !
    PADL @ OVER PAD-Q @ PADFL SVF  SWAP PADR @ SWAP PAD-Q @ PADFR SVF
    PAD-LEVEL @ G* DUCK @ G*  SWAP PAD-LEVEL @ G* DUCK @ G*
    2DUP + PAD-SEND @ G* RSEND +!
    LEFT +!  RIGHT +! ;
HERE @ NPAD CELLS ALLOT CONSTANT WANT  VARIABLE #WANT
: WANTED? ( midi -- f ) 0 #WANT @ 0 DO OVER I CELLS WANT + @ = OR LOOP NIP ;
: HELD? ( midi -- f )
    0 NPAD 0 DO I PAD DUP >AMP SOUNDING? SWAP >NOTE @ 3 PICK = AND OR LOOP NIP ;
: FREE-PAD ( -- v )
    -1 1 40 LSHIFT
    NPAD 0 DO
        I PAD >AMP DUP SOUNDING? IF DROP ELSE
            E.LEVEL @ DUP 2 PICK < IF NIP NIP I SWAP ELSE DROP THEN
        THEN
    LOOP DROP 0 MAX PAD ;
: PAD-NOTE! ( midi v -- )
    2DUP >NOTE !
    SWAP HZ
    DUP DUP 35 * 10000 / - 2 PICK >INC !
    DUP 35 * 10000 / + OVER >INC2 !
    >AMP GATE ;
: PAD-CHORD ( -- )
    TONES NPAD MIN DUP #WANT !
    0 DO I PAD-LO @ CHORD-NOTE I CELLS WANT + ! LOOP
    NPAD 0 DO
        I PAD DUP >AMP SOUNDING? IF
            DUP >NOTE @ WANTED? 0= IF DUP >AMP UNGATE THEN
        THEN DROP
    LOOP
    #WANT @ 0 DO
        I CELLS WANT + @ DUP HELD? IF DROP ELSE FREE-PAD PAD-NOTE! THEN
    LOOP ;
: PADS-OFF NPAD 0 DO I PAD >AMP UNGATE LOOP ;

( ---- Pluck: four voices of saw through a resonant lowpass whose cutoff
  snaps open and closes again with every note. ---- )
4 CONSTANT NPLUCK
NPLUCK VOICES PLUCKV  VARIABLE NEXT-PLUCK
VARIABLE PLUCK-LO  60 PLUCK-LO !
VARIABLE PLUCK-CUT  250 PLUCK-CUT !
VARIABLE PLUCK-ENV  3800 PLUCK-ENV !
VARIABLE PLUCK-Q  26000 PLUCK-Q !
VARIABLE PLUCK-LEVEL  10000 PLUCK-LEVEL !
VARIABLE PLUCK-ECHO  12000 PLUCK-ECHO !
VARIABLE PLUCK-SEND  9000 PLUCK-SEND !
: PLUCK-SHAPE ( a d s r -- ) NPLUCK 0 DO 2OVER 2OVER I PLUCKV >AMP ADSR! LOOP 2DROP 2DROP ;
: PLUCK-FILTER ( a d s r -- ) NPLUCK 0 DO 2OVER 2OVER I PLUCKV >FENV ADSR! LOOP 2DROP 2DROP ;
2 420 0 300 PLUCK-SHAPE  1 150 0 120 PLUCK-FILTER
: PLUCK-VOICE ( v -- )
    DUP >AMP DUP E.STAGE @ 0= IF 2DROP EXIT THEN ENV OVER >VEL @ G*
    OVER >PH1 2 PICK >INC @ SAW-OSC
    2 PICK >FENV ENV PLUCK-ENV @ G* PLUCK-CUT @ + HZ>F
    PLUCK-Q @ 4 PICK >FILT SVF  G*
    DUP PLUCK-ECHO @ G* ESEND +!  DUP PLUCK-SEND @ G* RSEND +!
    PLUCK-LEVEL @ G* SWAP >PAN @ MIX ;
: PLUCKS NPLUCK 0 DO I PLUCKV PLUCK-VOICE LOOP ;
: PLUCK! ( midi vel -- )
    NEXT-PLUCK @ PLUCKV >R
    R@ >VEL !  HZ R@ >INC !  0 R@ >PH1 !
    R@ >AMP GATE  R@ >FENV GATE
    NEXT-PLUCK @ 1+ NPLUCK MOD DUP NEXT-PLUCK !
    1 AND IF 84 ELSE 172 THEN R> >PAN ! ;

( ---- Bass: a saw and a sine at the same pitch, through a lowpass with its
  own envelope, gliding between legato notes. ---- )
1 VOICES BASSV  0 BASSV CONSTANT BV
VARIABLE BASS-LO  33 BASS-LO !
VARIABLE BASS-CUT  140 BASS-CUT !
VARIABLE BASS-ENV  900 BASS-ENV !
VARIABLE BASS-Q  30000 BASS-Q !
VARIABLE BASS-GLIDE  300 BASS-GLIDE !
VARIABLE BASS-LEVEL  48000 BASS-LEVEL !
3 300 70 70 BV >AMP ADSR!   2 220 20 120 BV >FENV ADSR!
: BASS~
    BV >AMP E.STAGE @ 0= IF EXIT THEN
    BV BASS-GLIDE @ GLIDE  BV EASE-VEL
    BV >AMP ENV BV >VEL @ G*
    BV >PH1 BV >INC @ SAW-OSC
    BV >PH1 @ SIN 2 * +
    BV >FENV ENV BASS-ENV @ G* BASS-CUT @ + HZ>F BASS-Q @ BV >FILT SVF
    G* HALF-DUCK BASS-LEVEL @ G* 128 MIX ;
: BASS! ( midi vel -- )
    BV >TVEL !  HZ BV >TINC !
    BV >AMP SOUNDING? 0= IF BV >TINC @ BV >INC !  BV >TVEL @ BV >VEL ! THEN
    BV >AMP GATE  BV >FENV GATE ;
: BASS-OFF BV >AMP UNGATE BV >FENV UNGATE ;

( ---- Lead: two saws a few cents apart with a slow vibrato, through a
  lowpass with an envelope, gliding between notes. ---- )
1 VOICES LEADV  0 LEADV CONSTANT LV
VARIABLE LEAD-CUT  700 LEAD-CUT !
VARIABLE LEAD-ENV  2600 LEAD-ENV !
VARIABLE LEAD-Q  36000 LEAD-Q !
VARIABLE LEAD-GLIDE  250 LEAD-GLIDE !
VARIABLE LEAD-LEVEL  34000 LEAD-LEVEL !
VARIABLE LEAD-ECHO  14000 LEAD-ECHO !
VARIABLE LEAD-SEND  12000 LEAD-SEND !
VARIABLE LEAD-PAN  128 LEAD-PAN !
VARIABLE VIB  VARIABLE LINC
12 420 75 380 LV >AMP ADSR!  8 600 30 400 LV >FENV ADSR!
: LEAD~
    LV >AMP E.STAGE @ 0= IF EXIT THEN
    LV LEAD-GLIDE @ GLIDE  LV EASE-VEL
    5 HZ>INC VIB +!
    LV >INC @ DUP VIB @ SIN * 16384 / 280 / + LINC !
    LV >AMP ENV LV >VEL @ G*
    LV >PH1 LINC @ DUP 25 * 10000 / - SAW-OSC
    LV >PH2 LINC @ DUP 25 * 10000 / + SAW-OSC + 2 /
    LV >FENV ENV LEAD-ENV @ G* LEAD-CUT @ + HZ>F LEAD-Q @ LV >FILT SVF
    G*
    DUP LEAD-ECHO @ G* ESEND +!  DUP LEAD-SEND @ G* RSEND +!
    LEAD-LEVEL @ G* LEAD-PAN @ MIX ;
: LEAD! ( midi vel -- )
    LV >TVEL !  HZ LV >TINC !
    LV >AMP SOUNDING? 0= IF LV >TINC @ LV >INC !  LV >TVEL @ LV >VEL ! THEN
    LV >AMP GATE  LV >FENV GATE ;
: LEAD-OFF LV >AMP UNGATE LV >FENV UNGATE ;

( ---- Bell: three voices of two-operator FM. The modulator runs 3.5 times
  the carrier, which is what makes it a bell and not an organ, and its
  depth falls with the note. ---- )
3 CONSTANT NBELL
NBELL VOICES BELLV  VARIABLE NEXT-BELL
VARIABLE BELL-INDEX  260 BELL-INDEX !
VARIABLE BELL-LEVEL  9000 BELL-LEVEL !
VARIABLE BELL-SEND  30000 BELL-SEND !
VARIABLE BELL-ECHO  8000 BELL-ECHO !
: BELL-SHAPE ( a d s r -- ) NBELL 0 DO 2OVER 2OVER I BELLV >AMP ADSR! LOOP 2DROP 2DROP ;
1 1400 0 900 BELL-SHAPE
: BELL-VOICE ( v -- )
    DUP >AMP DUP E.STAGE @ 0= IF 2DROP EXIT THEN ENV OVER >VEL @ G*
    OVER >PH1 @ 2 PICK >INC @ + MASK32 AND DUP 3 PICK >PH1 !
    2 PICK >PH2 @ 3 PICK >INC2 @ + MASK32 AND DUP 4 PICK >PH2 !
    2 PICK BELL-INDEX @ * 65536 /
    FM 2 * G*
    DUP BELL-SEND @ G* RSEND +!  DUP BELL-ECHO @ G* ESEND +!
    BELL-LEVEL @ G* SWAP >PAN @ MIX ;
: BELLS NBELL 0 DO I BELLV BELL-VOICE LOOP ;
: BELL! ( midi vel -- )
    NEXT-BELL @ BELLV >R
    R@ >VEL !  HZ DUP R@ >INC !  7 * 2 / R@ >INC2 !
    0 R@ >PH1 !  0 R@ >PH2 !  R@ >AMP GATE
    NEXT-BELL @ 1+ NBELL MOD DUP NEXT-BELL !
    3 MOD 60 * 68 + R> >PAN ! ;

( ---- Drums. Amplitudes here are fractions of FIX (2^30) so a long fade
  never stalls on integer truncation. ---- )
: STRIKE ( vel var -- ) SWAP 14 LSHIFT SWAP ! ;

VARIABLE K.PH  VARIABLE K.INC  VARIABLE K.AMP
VARIABLE KICK-LEVEL  26000 KICK-LEVEL !
260 TAU CONSTANT KICK-K
1047807 CONSTANT KICK-DROP        ( 175 Hz falls to 48 in about 40 ms )
: KICK! ( vel -- ) DUP DUCK-HIT K.AMP STRIKE  0 K.PH !  175 HZ>INC K.INC ! ;
: KICK~
    K.AMP @ 0= IF EXIT THEN
    K.INC @ K.PH +!
    K.PH @ SIN 4 * K.AMP @ SCALE SOFTCLIP
    K.AMP KICK-K FADE
    K.INC @ KICK-DROP * 1048576 / 48 HZ>INC MAX K.INC !
    KICK-LEVEL @ G* 128 MIX ;

VARIABLE SN.PH  VARIABLE SN.AMP  VARIABLE SN.BODY
HERE @ /SVF ALLOT CONSTANT SNF
VARIABLE SNARE-LEVEL  15000 SNARE-LEVEL !
VARIABLE SNARE-SEND  14000 SNARE-SEND !
140 TAU CONSTANT SNARE-K   45 TAU CONSTANT BODY-K
: SNARE! ( vel -- ) DUP SN.AMP STRIKE SN.BODY STRIKE 0 SN.PH ! ;
: SNARE~
    SN.AMP @ 0= IF EXIT THEN
    185 HZ>INC SN.PH +!  SN.PH @ SIN SN.BODY @ SCALE
    NOISE 2 * 1900 HZ>F 42000 SNF SVF DROP
    SNF S.BAND @ SNF S.HIGH @ 2 / + SN.AMP @ SCALE +
    SN.AMP SNARE-K FADE  SN.BODY BODY-K FADE
    SNARE-LEVEL @ G* DUP SNARE-SEND @ G* RSEND +! 128 MIX ;

VARIABLE HT.AMP  VARIABLE HT.K
HERE @ /SVF ALLOT CONSTANT HATF
VARIABLE HAT-LEVEL  7000 HAT-LEVEL !
VARIABLE HAT-PAN  176 HAT-PAN !
38 TAU CONSTANT CLOSED  280 TAU CONSTANT OPEN
: HAT! ( vel -- ) HT.AMP STRIKE CLOSED HT.K ! ;
: OPEN-HAT! ( vel -- ) HT.AMP STRIKE OPEN HT.K ! ;
: HAT~
    HT.AMP @ 0= IF EXIT THEN
    NOISE 2 * 7200 HZ>F 50000 HATF SVF DROP HATF S.HIGH @
    HT.AMP @ SCALE  HT.AMP HT.K @ FADE
    HAT-LEVEL @ G* HAT-PAN @ MIX ;

VARIABLE CR.AMP
HERE @ /SVF ALLOT CONSTANT CRF
VARIABLE CRASH-LEVEL  5000 CRASH-LEVEL !
1300 TAU CONSTANT CRASH-K
: CRASH! ( vel -- ) CR.AMP STRIKE ;
: CRASH~
    CR.AMP @ 0= IF EXIT THEN
    NOISE 2 * 4500 HZ>F 40000 CRF SVF DROP CRF S.HIGH @
    CR.AMP @ SCALE  CR.AMP CRASH-K FADE
    CRASH-LEVEL @ G* DUP 2 * RSEND +! 96 MIX ;

( Riser: noise through a ringing bandpass that sweeps up while RISING is
  set, louder as it climbs. RISE-LEN is the sweep in samples. )
VARIABLE RISE  VARIABLE RISING  VARIABLE RISE-LEN
HERE @ /SVF ALLOT CONSTANT RISEF
VARIABLE RISER-LEVEL  18000 RISER-LEVEL !
: RISER~
    RISING @ 0= IF 0 RISE ! EXIT THEN
    1 RISE +!
    RISE @ 65536 * RISE-LEN @ 1 MAX / 65536 MIN
    DUP 5800 * 65536 / 150 + HZ>F >R
    NOISE 2 * R> 16000 RISEF SVF DROP RISEF S.BAND @
    SWAP DUP * 65536 / G*
    RISER-LEVEL @ G* DUP RSEND +! 128 MIX ;

( ---- The whole studio, one stereo frame. ---- )
VARIABLE MASTER  65536 MASTER !
: STUDIO
    0 ESEND !  0 RSEND !
    DUCK~
    PADS PLUCKS BASS~ LEAD~ BELLS
    KICK~ SNARE~ HAT~ CRASH~ RISER~
    ESEND @ ECHO  DL @ 3 / DUP NEGATE LEFT +! RIGHT +!   ( first repeat nearer the middle )
    RSEND @ REVERB RWET @ G* RIGHT +! RWET @ G* LEFT +!
    LEFT @ MASTER @ G* SOFTCLIP LEFT !
    RIGHT @ MASTER @ G* SOFTCLIP RIGHT ! ;

( ---- Reading patterns, for a song's TRIGGER. ---- )
( A drum pattern is sixteen characters: digits 1-9 are hits at that
  velocity, anything else is silence. )
: HIT@ ( pattern -- vel|0 )
    STEPS DUP [ CHAR 1 ] LITERAL >= OVER [ CHAR 9 ] LITERAL <= AND
    IF '0' - VEL ELSE DROP 0 THEN ;
: PLAY ( pattern xt -- ) SWAP HIT@ ?DUP IF SWAP EXECUTE ELSE DROP THEN ;

( A bass pattern names chord tones: r root (or the slash bass), 5 fifth,
  o octave, 3 third, 7 seventh, p a half step under the next bar's bass;
  capitals are accented. "-" holds and "." lets go. )
: LOW ( pc -- midi ) BASS-LO @ - 48 + 12 MOD BASS-LO @ + ;
: SEVENTH-OF ( -- n ) INTERVALS 2048 AND IF 11 ELSE 10 THEN ;
: BASS-TONE ( c -- midi|0 )
    DUP [ CHAR r ] LITERAL = IF DROP BASSNOTE LOW EXIT THEN
    DUP [ CHAR 5 ] LITERAL = IF DROP ROOT LOW 7 + EXIT THEN
    DUP [ CHAR o ] LITERAL = IF DROP BASSNOTE LOW 12 + EXIT THEN
    DUP [ CHAR 3 ] LITERAL = IF DROP ROOT LOW THIRD + EXIT THEN
    DUP [ CHAR 7 ] LITERAL = IF DROP ROOT LOW SEVENTH-OF + EXIT THEN
    DUP [ CHAR p ] LITERAL = IF DROP BAR 1+ CHORD-AT 16 RSHIFT 15 AND LOW 1- EXIT THEN
    DROP 0 ;
: BASS-STEP ( pattern -- )
    STEPS
    DUP [ CHAR . ] LITERAL = IF DROP BASS-OFF EXIT THEN
    DUP [ CHAR a ] LITERAL < IF 32 + 9 ELSE 6 THEN VEL SWAP
    DUP [ CHAR R ] LITERAL 32 + = OVER [ CHAR P ] LITERAL 32 + = OR 0= IF
        DUP [ CHAR a ] LITERAL < IF NIP 6 VEL SWAP 32 - THEN
    THEN
    BASS-TONE ?DUP IF SWAP BASS! ELSE DROP THEN ;

( An arp pattern picks chord tones counting up from PLUCK-LO: 0 the
  lowest; "." is a rest. Notes on the beat are struck a little harder. )
: ARP-STEP ( pattern -- )
    STEPS DUP [ CHAR 0 ] LITERAL < OVER [ CHAR 9 ] LITERAL > OR IF DROP EXIT THEN
    '0' - PLUCK-LO @ CHORD-NOTE
    POS 3 AND IF 6 ELSE 8 THEN VEL PLUCK! ;

( A melody track from NOTES[ with its own cursor, so it can loop. Returns
  the transposed note (0 hold, 1 release) and its velocity as a gain. )
: TRACK-STEP ( track steps var -- note vel )
    >R R@ @ SWAP MOD NOTE@ 1 R> +!
    VEL SWAP DUP 2 < IF SWAP EXIT THEN TRANSPOSE @ + SWAP ;
