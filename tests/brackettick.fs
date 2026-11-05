\ SEE prints ['] X for a literal execution token; that output now reads back in.
: SQ DUP * ;
: APPLY-SQ ['] SQ EXECUTE ;
5 APPLY-SQ . CR
SEE APPLY-SQ
: APPLY-SQ2 ['] SQ EXECUTE ;
'' APPLY-SQ 8+ @ '' LIT = . '' APPLY-SQ 16 + @ '' SQ = . CR
