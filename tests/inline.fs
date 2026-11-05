\ needs: lib/tracker.fs
\ An inlined word's body is copied into its caller.
: SQ DUP * ;  INLINE SQ
: F 7 SQ 1+ ;  F . SEE F
\ At the prompt it still runs.
5 SQ . CR
\ Branches and loops keep their offsets when copied.
: CLAMP9 ( n -- n' ) 9 MIN 0 MAX ;  INLINE CLAMP9
: SUMTO ( n -- sum ) 0 SWAP 0 DO I + LOOP ;  INLINE SUMTO
: G -3 CLAMP9 . 4 CLAMP9 . 99 CLAMP9 . 10 SUMTO . ;  G CR
\ A constant made after the tracker loads compiles as a literal.
1234 CONSTANT K  : H K 1+ ;  H . SEE H
\ INLINE refuses what is not a colon word.
VARIABLE V  INLINE V
