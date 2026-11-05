\ Pushing past the 8192-cell data stack used to write into the kernel's
\ variables below it. One cell too many printed stray bytes and exited 1;
\ five too many hung. Now the guard page below the stack stops it (exit 138).
: X 8193 0 DO 7 LOOP ; X
1 2 + . CR
