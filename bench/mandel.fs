( demos/mandel.fs's inner loop with deeper iteration and no output. )
4096 CONSTANT ONE
: F* ( a b -- a*b ) * ONE / ;
VARIABLE ZR  VARIABLE ZI  VARIABLE CRE  VARIABLE CIM
4000 CONSTANT MAXITER
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
: MANDEL ( -- sum )
    0 48 0 DO
        I 213 * -4915 + CIM !
        156 0 DO
            I 77 * -8602 + CRE !
            ESCAPE +
        LOOP
    LOOP ;
MANDEL . CR
