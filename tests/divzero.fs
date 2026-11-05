\ Dividing by zero used to give 0 (sdiv doesn't trap): 7 0 /MOD . . printed 0 7.
7 0 /MOD . . CR
1 2 7 0 /
DEPTH . CR
7 0 MOD . CR
: AVG ( sum n -- avg ) / ;
10 0 AVG . CR
\ Words compiled before the handler was installed are covered too: U. divides by BASE.
5 0 BASE ! .
DECIMAL 42 . CR
\ Ordinary division is unchanged, truncating toward zero.
7 2 /MOD . . 7 2 / . 7 2 MOD . -7 2 / . -7 2 MOD . CR
