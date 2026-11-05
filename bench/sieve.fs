( Sieve of Eratosthenes over 8192 flags, 2000 times: memory and loops. )
8192 CONSTANT SIZE
HERE @ SIZE ALLOT CONSTANT FLAGS
: PRIMES ( -- count )
    FLAGS SIZE 1 FILL
    0 SIZE 2 DO
        FLAGS I + C@ IF
            1+ I DUP * SIZE < IF
                SIZE I DUP * DO 0 FLAGS I + C! J +LOOP
            THEN
        THEN
    LOOP ;
: RUN 0 2000 0 DO DROP PRIMES LOOP . CR ;
RUN
