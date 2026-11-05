\ Every stack primitive, plus the words that read the stack as memory
\ (DSP@ for .S, DEPTH, PICK; DSP! for RESET). Each line starts empty.
1 2 3 .S CR RESET
1 2 3 DROP .S  1 DUP .S  1 2 SWAP .S CR RESET
1 2 OVER .S  1 2 3 ROT .S CR RESET
1 2 3 -ROT .S  1 2 NIP .S  1 2 TUCK .S CR RESET
1 2 2DUP .S  1 2 3 4 2SWAP .S  1 2 3 2DROP .S CR RESET
0 ?DUP .S  7 ?DUP .S CR RESET
10 20 30 2 PICK . 0 PICK . DEPTH . CR RESET
10 20 DSP@ @ . DSP@ 8+ @ . CR RESET
: RT 5 >R 6 R@ R> + ; RT . DEPTH . CR
17 5 /MOD . . -17 5 /MOD . . CR
3 4 + 2 * 1- 8+ 8- NEGATE INVERT . CR
3 4 < . 4 3 < . 0 0= . 1 0= . -1 0< . 1 0> . CR
HERE @ 99 OVER ! @ . CR
HERE @ 1 OVER ! 41 OVER +! @ . CR
HERE @ 200 OVER C! C@ . CR
.S DEPTH . CR
