\ needs: lib/tracker.fs lib/synth.fs
\ Chord symbols to root, intervals and bass.
: .CH ( cell -- ) DUP 15 AND . ." : " DUP 4 RSHIFT 4095 AND
    12 0 DO DUP I RSHIFT 1 AND IF I . THEN LOOP DROP ." / " 16 RSHIFT 15 AND . CR ;
CHORDS[ Am F#m Fm9 Dbmaj7 Bbm9 C7 C7sus Gsus2 Eb/G Fadd9 Bm7b5 G6 ]
: CHARTS CHORD# @ 0 DO CHORDS @ I CELLS + @ .CH LOOP ; CHARTS
\ Note names with flats and velocities.
NOTES[ C-5 Eb58 ... ^^^ Bb4 | Db63 ] DROP CONSTANT T
: NS 6 0 DO T I NOTE@ SWAP . . LOOP CR ; NS
\ Close voicings from G3, holding common tones from chord to chord.
16 METER !
CHORDS[ Fm9 Dbmaj7 Bbm9 C7 G ]
: SHOW-PADS NPAD 0 DO I PAD >AMP SOUNDING? IF I PAD >NOTE ? THEN LOOP ;
: WALK 5 0 DO I 16 * STEP# ! PAD-CHORD SHOW-PADS CR LOOP ; WALK
\ PolyBLEP saw: mid-cycle, just after the jump, just before it.
: SAWS 2147483648 42852281 SAW . 100 42852281 SAW . 4294000000 42852281 SAW .
    4294000000 42852281 NAIVE-SAW . CR ; SAWS
\ Soft clip: 0, 0.5, 1, 3 and past it.
: CLIPS 0 SOFTCLIP . 16384 SOFTCLIP . 32768 SOFTCLIP . 98304 SOFTCLIP . -200000 SOFTCLIP . CR ; CLIPS
\ An envelope: 1 ms attack, 10 ms decay to 50%, 10 ms release.
HERE @ /ENV ALLOT CONSTANT E
1 10 50 10 E ADSR!  E GATE
: ENVS 6 0 DO 30 0 DO E ENV DROP LOOP E ENV . LOOP E UNGATE 2000 0 DO E ENV DROP LOOP E ENV . E E.STAGE ? CR ; ENVS
\ Twenty thousand frames of the studio: a chord, a bass note, a kick, a bell.
2 STEPLEN !  CHORDS[ Fm9 ]  ARRANGE[ x ]  0 STEP# !
PAD-CHORD 41 9 VEL BASS! 9 VEL KICK! 84 7 VEL BELL! 60 8 VEL PLUCK!
VARIABLE SUMS  VARIABLE PEAK
: RUN 20000 0 DO 0 LEFT ! 0 RIGHT ! STUDIO
    LEFT @ ABS RIGHT @ ABS + DUP SUMS +! PEAK @ MAX PEAK ! LOOP
    SUMS ? PEAK ? CR ; RUN
