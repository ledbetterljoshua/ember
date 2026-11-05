\ VARIABLE NAME n ALLOT used to put the allotted space after NAME's code,
\ so filling it overwrote the dictionary (segfault).
VARIABLE F 64 ALLOT
F 72 1 FILL  F @ 72340172838076673 = .  F 71 + C@ . CR
VARIABLE A  VARIABLE B  5 A !  6 B !  A @ B @ + . CR
: USE-VAR A @ 10 * ; USE-VAR . CR
SEE A
