\ Recursing past the 8192-cell return stack used to run down into the data
\ stack: at depth 8194 .S printed wrong numbers and exit was 0. Now it hits
\ the guard page between the two stacks (exit 138).
: R DUP IF 1- RECURSE THEN ;
11 22 33 8194 R
DROP .S CR
