\ A fault that isn't on a stack guard page still ends the program with
\ 128 + the signal number, but output written before it is flushed first.
1 2 + . CR
0 @ .
9 . CR
