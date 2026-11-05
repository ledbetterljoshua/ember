( Doubly recursive Fibonacci: measures DOCOL, EXIT, IF and arithmetic. )
: FIB DUP 2 < IF EXIT THEN DUP 1- RECURSE SWAP 2 - RECURSE + ;
35 FIB . CR
