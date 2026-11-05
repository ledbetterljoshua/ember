        .text
        .p2align 2
start:
        movk    x3, #0x1234, lsl #16
        movk    w3, #0xbeef
        mov     x5, #0x50000
        mov     w5, #-2
        mov     x6, #-0x10000
        movz    x7, #0, lsl #32
        ldp     x1, x2, [x3, #16]
        ldp     w1, w2, [x3, #-8]
        stp     w1, w2, [sp, #8]!
        ldpsw   x1, x2, [x0], #8
        ldnp    x1, x2, [x0, #32]
        ldr     w0, [x1, w2, uxtw]
        ldr     x0, [x1, w2, sxtw #3]
        ldr     x0, [x1, x2, sxtx]
        ldrh    w0, [x1, x2, lsl #1]
        ldrb    w0, [x1, x2, lsl #0]
        ldur    x0, [x1, #-3]
        stur    w0, [x1, #5]
        ldursb  x0, [x1, #-1]
        ldrsw   x0, [x1, #4]
        ldrsh   w0, [x1, #2]
        ldrsb   x0, [x1]
        strh    w0, [x1, #6]
        ldr     w9, [x1, #4092]
        tbz     w3, #5, start
        tbnz    x3, #40, fwd
        and     x0, x1, #0x5555555555555555
        orr     w0, w1, #0xff00ff00
        eor     x0, x0, #0x8000000000000000
        ands    x0, x1, #0x3
        tst     x1, #0xf0
        and     w0, w1, #0x7ffffffe
        orr     x0, xzr, #0xaaaaaaaaaaaaaaaa
        orr     x0, xzr, #0xffff0000
        orr     w0, wzr, #0xff00ff00
        orr     w0, wzr, #0xffff
        cset    x0, eq
        cset    w0, hi
        cinc    x0, x1, lt
        cinv    w0, w2, ge
        cneg    x0, x3, mi
        csinc   x0, x1, x2, ne
        csneg   w0, w1, w2, vs
        csel    w0, w1, w2, al
        mul     w0, w1, w2
        mneg    x0, x1, x2
        madd    w0, w1, w2, w3
        umulh   x0, x1, x2
        smulh   x0, x1, x2
        asr     x0, x1, x2
        ror     w0, w1, w2
        udiv    w0, w1, w2
        add     x0, x1, x2, lsl #3
        sub     w0, w1, w2, lsr #2
        subs    x0, x1, x2, asr #63
        cmn     x0, #5
        cmn     w1, w2
        adds    x0, x1, #4095
        negs    x0, x1
        orr     x0, x1, x2, lsl #4
        orn     x0, x1, x2
        bic     w0, w1, w2, ror #7
        eon     x0, x1, x2
        bics    x0, x1, x2
        mvn     w0, w1, lsl #2
        tst     w0, w1
        ands    w0, w1, w2
        mov     sp, x3
        mov     w0, w1
        add     sp, sp, #0x20
        sub     sp, sp, #1, lsl #12
        blr     x8
        ret     x3
        ret
        nop
        adr     x0, start
        adr     x1, fwd
        ldr     x0, fwd
        ldr     w0, start
        ldrsw   x0, fwd
        b.mi    start
        b.vc    fwd
        b.al    fwd
        cbz     w3, fwd
fwd:    b       start
        bl      fwd
        svc     #0
