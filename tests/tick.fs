\ ' at top level used to crash (segfault). It now reads the next word.
' DUP '' DUP = . CR
3 ' DUP EXECUTE * . CR
: SQUARER ' DUP ;
4 SQUARER EXECUTE * . CR
SEE SQUARER
\ Ticking an immediate word inside a definition used to run it instead.
: TICK-THEN ' THEN ;
TICK-THEN '' THEN = . CR
