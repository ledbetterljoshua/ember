( Mandelbrot set in 12-bit fixed point: 4096 means 1.0 )
4096 CONSTANT ONE
: F* ( a b -- a*b ) * ONE / ;

VARIABLE ZR  VARIABLE ZI  VARIABLE CRE  VARIABLE CIM
48 CONSTANT MAXITER

: ESCAPE ( -- n )
    0 ZR !  0 ZI !  0
    BEGIN
        DUP MAXITER <
        ZR @ DUP F*  ZI @ DUP F*  +  4 ONE * <  AND
    WHILE
        ZR @ DUP F*  ZI @ DUP F*  -  CRE @ +
        ZR @ ZI @ F* 2 *  CIM @ +  ZI !
        ZR !
        1+
    REPEAT ;

: SHADE ( n -- c )
    DUP MAXITER = IF DROP BL EXIT THEN
    S"  .,:;-=+*#%@" ( n addr len ) ROT OVER MOD ROT + C@ NIP ;

: MANDEL
    24 0 DO
        I 427 * -4915 + CIM !
        78 0 DO
            I 154 * -8602 + CRE !
            ESCAPE SHADE EMIT
        LOOP CR
    LOOP ;

MANDEL
