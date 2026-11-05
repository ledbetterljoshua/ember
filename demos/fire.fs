( fire.fs: a fire in 24-bit ANSI color. Heat rises from a white-hot floor,
  cooling and drifting at random; each character cell shows two rows of it
  with a half block, foreground on top and background below. After a while
  the floor goes out and the flames die down.

      ./ember core.fs demos/fire.fs                                       )

80 CONSTANT W   48 CONSTANT H     ( H rows of heat make H/2 lines )
HERE @ W H * ALLOT CONSTANT HEAT
HEAT W H * 0 FILL
: AT ( x y -- addr ) W * + HEAT + ;

VARIABLE SEED  2463534242 SEED !
: RAND ( -- u ) SEED @ DUP 13 LSHIFT XOR DUP 7 RSHIFT XOR DUP 17 LSHIFT XOR
    DUP SEED ! 40 RSHIFT ;

( The floor flickers below its current heat, which drops when the fire
  is going out. )
VARIABLE FUEL
: FLOOR W 0 DO FUEL @ RAND 63 AND - 0 MAX 255 MIN I H 1- AT C! LOOP ;

( Each cell takes the heat of the one below it, loses a little, and lands
  up to one column left or right, so flames lick sideways. )
: SPREAD
    H 1 DO W 0 DO
        RAND DUP 3 MOD SWAP 2 RSHIFT 15 AND  ( drift cool )
        I J AT C@ SWAP - 0 MAX               ( drift heat )
        SWAP I + 1- W + W MOD  J 1-  AT C!
    LOOP LOOP ;

( Black to red to orange to yellow to white. )
: RAMP ( n -- 0..255 ) 0 MAX 255 MIN ;
: RGB ( heat -- r g b ) DUP 3 * RAMP  OVER 85 - 3 * RAMP  ROT 170 - 3 * RAMP ;

: ESC 27 EMIT ;
: ;N ( n -- ) [ CHAR ; ] LITERAL EMIT U. ;
: COLOR ( heat which -- ) ESC [ CHAR [ ] LITERAL EMIT U. ." 8;2" RGB ROT ;N SWAP ;N ;N
    [ CHAR m ] LITERAL EMIT ;
: FG ( heat -- ) 3 COLOR ;
: BG ( heat -- ) 4 COLOR ;

( Upper half block, U+2580. )
: TOP 226 EMIT 150 EMIT 128 EMIT ;

: SHOW
    ESC ." [H"
    H 0 DO W 0 DO
        I J AT C@ FG  I J 1+ AT C@ BG  TOP
    LOOP ESC ." [0m" CR 2 +LOOP ;

: FIRE
    ESC ." [2J" ESC ." [?25l"
    300 FUEL !
    160 0 DO FLOOR SPREAD SHOW 30 MS LOOP
    80 0 DO -4 FUEL +! FLOOR SPREAD SHOW 30 MS LOOP
    ESC ." [0m" ESC ." [?25h" ;

FIRE
