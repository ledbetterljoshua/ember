\ Pushing past the 8192-cell data stack used to write into the kernel's
\ variables below it. The guard page below the stack now faults, and the
\ fault is reported and ABORTs, so the session carries on.
: X 8193 0 DO 7 LOOP ; X
1 2 + . CR
X
DEPTH . CR
