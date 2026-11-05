\ Underflowing far past S0 used to write wherever the pointer had got to:
\ 8210 DROPs and a push made CELLS silently disappear. Past the slack above
\ S0 is a guard page; the fault is reported and the dictionary is intact.
: U 8210 0 DO DROP LOOP 5 ; U
3 CELLS . CR
\ The return stack has a guard above R0 too.
: Z BEGIN R> DROP AGAIN ; Z
4 CELLS . CR
