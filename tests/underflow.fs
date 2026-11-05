\ Underflowing far past S0 used to write wherever the pointer had got to:
\ here the 5 lands in the dictionary and CELLS silently disappears ("CELLS ?",
\ exit 0). Past the slack above S0 is now a guard page (exit 138).
: U 8210 0 DO DROP LOOP 5 ; U
3 CELLS . CR
