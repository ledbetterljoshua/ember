\ */ ( a b c -- a*b/c ): multiply then divide, truncating toward zero.
10 3 4 */ . -10 3 4 */ . 1000000 1000000 7 */ . CR
\ Same result as * then / , which lib/synth.fs's G* relied on.
-12345 40000 65536 */ -12345 40000 * 65536 / = . CR
1 2 3 4 0 */ DEPTH . CR
