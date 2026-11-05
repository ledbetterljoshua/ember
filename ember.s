// ember: a Forth kernel in AArch64 assembly for macOS.
//
// Indirect-threaded. The kernel defines only what cannot be said in Forth;
// everything else (IF, loops, comments, strings, printing numbers, SEE) is
// grown from these primitives by core.fs.
//
// Registers:  x19 = IP (next cell to execute)
//             x20 = data stack pointer (full, descending)
//             x21 = return stack pointer (full, descending)
//             x0  = W, the codeword address, on entry to any word
// The machine stack (sp) is only used by the assembly subroutines below.
//
// Build:  clang -o ember ember.s
// Run:    ./ember core.fs -        (load core, then read stdin)

        .set F_IMMED,   0x80
        .set F_HIDDEN,  0x20
        .set F_LENMASK, 0x1f

        .set DSTACK_SIZE, 0x10000
        .set RSTACK_SIZE, 0x10000
        .set DICT_SIZE,   0x400000
        .set BUF_SIZE,    0x1000

        .set SYS_exit,   1
        .set SYS_read,   3
        .set SYS_write,  4
        .set SYS_open,   5
        .set SYS_close,  6
        .set SYS_select, 93

        .zerofill __DATA,__bss,data_stack,DSTACK_SIZE,4
        .zerofill __DATA,__bss,return_stack,RSTACK_SIZE,4
        .zerofill __DATA,__bss,dict_space,DICT_SIZE,4
        .zerofill __DATA,__bss,input_buffer,BUF_SIZE,4
        .zerofill __DATA,__bss,output_buffer,BUF_SIZE,4
        .zerofill __DATA,__bss,word_buffer,64,4

// ---------------------------------------------------------------- macros

        .macro NEXT
        ldr     x0, [x19], #8
        ldr     x1, [x0]
        br      x1
        .endm

        .macro PUSH reg
        str     \reg, [x20, #-8]!
        .endm
        .macro POP reg
        ldr     \reg, [x20], #8
        .endm
        .macro PUSHRSP reg
        str     \reg, [x21, #-8]!
        .endm
        .macro POPRSP reg
        ldr     \reg, [x21], #8
        .endm

        .macro LOADADDR reg, sym
        adrp    \reg, \sym@PAGE
        add     \reg, \reg, \sym@PAGEOFF
        .endm

        .macro ENTER
        stp     x29, x30, [sp, #-16]!
        .endm
        .macro LEAVE
        ldp     x29, x30, [sp], #16
        ret
        .endm

// Dictionary header: link (8) | flags+len (1) | name | pad to 8 | codeword
// Numeric labels 8 and 9 belong to this macro; code bodies use 1-7.
        .set link, 0
        .macro header name, label, flags=0
        .data
        .balign 8
name_\label :
        .quad   link
        .set    link, name_\label
        .byte   \flags + (9f - 8f)
8:      .ascii  "\name"
9:      .balign 8
        .endm

        .macro defword name, label, flags=0
        header  "\name", \label, \flags
\label :
        .quad   DOCOL
        .endm

        .macro defcode name, label, flags=0
        header  "\name", \label, \flags
\label :
        .quad   code_\label
        .text
        .p2align 2
code_\label :
        .endm

        .macro defvar name, label, initial=0
        defcode "\name", \label
        LOADADDR x0, var_\label
        PUSH    x0
        NEXT
        .data
        .balign 8
var_\label :
        .quad   \initial
        .endm

        .macro defconst name, label, value
        defcode "\name", \label
        mov     x0, #\value
        PUSH    x0
        NEXT
        .endm

        .macro defaddr name, label, sym, offset=0
        defcode "\name", \label
        LOADADDR x0, \sym
        add     x0, x0, #\offset
        PUSH    x0
        NEXT
        .endm

        .macro defcmp name, label, cond
        defcode "\name", \label
        POP     x0
        ldr     x1, [x20]
        cmp     x1, x0
        csetm   x1, \cond
        str     x1, [x20]
        NEXT
        .endm

        .macro defcmp0 name, label, cond
        defcode "\name", \label
        ldr     x0, [x20]
        cmp     x0, #0
        csetm   x0, \cond
        str     x0, [x20]
        NEXT
        .endm

        .macro defbin name, label, op
        defcode "\name", \label
        POP     x0
        ldr     x1, [x20]
        \op     x1, x1, x0
        str     x1, [x20]
        NEXT
        .endm

// ---------------------------------------------------------------- entry

        .text
        .globl  _main
        .p2align 2
_main:
        LOADADDR x9, src_argc
        str     x0, [x9]
        LOADADDR x9, src_argv
        str     x1, [x9]
        cmp     x0, #1
        b.ne    1f
        LOADADDR x9, cur_fd             // no arguments: read stdin
        str     xzr, [x9]
1:
        LOADADDR x20, data_stack
        add     x20, x20, #DSTACK_SIZE
        LOADADDR x9, var_SZ
        str     x20, [x9]
        LOADADDR x21, return_stack
        add     x21, x21, #RSTACK_SIZE
        LOADADDR x19, cold_start
        NEXT

DOCOL:
        PUSHRSP x19
        add     x19, x0, #8
        NEXT

// A <BUILDS word: codeword DODOES, then a does-pointer cell, then its data.
// It pushes its data address, then runs the code after DOES> if there is any.
DODOES:
        add     x1, x0, #16
        PUSH    x1
        ldr     x2, [x0, #8]
        cbz     x2, 1f
        PUSHRSP x19
        mov     x19, x2
1:      NEXT

// ---------------------------------------------------------------- I/O

// _key: -> w0 = next input byte. Sources are argv[1..] in order ("-" is
// stdin); with no arguments, stdin alone. Exits when every source is spent.
_key:
        LOADADDR x9, currkey
        ldr     x10, [x9]
        LOADADDR x11, bufftop
        ldr     x12, [x11]
        cmp     x10, x12
        b.hs    1f
        ldrb    w0, [x10], #1
        str     x10, [x9]
        ret
1:      ENTER
        bl      _flush                  // whoever waits on input sees output first
2:      LOADADDR x9, cur_fd
        ldr     x0, [x9]
        tbnz    x0, #63, 4f
        LOADADDR x1, input_buffer
        mov     x2, #BUF_SIZE
        mov     x16, #SYS_read
        svc     #0x80
        b.cs    3f
        cmp     x0, #0
        b.le    3f
        LOADADDR x1, input_buffer
        LOADADDR x9, currkey
        str     x1, [x9]
        add     x1, x1, x0
        LOADADDR x9, bufftop
        str     x1, [x9]
        ldp     x29, x30, [sp], #16
        b       _key
3:      LOADADDR x9, cur_fd             // source exhausted: close, advance
        ldr     x0, [x9]
        mov     x10, #-1
        str     x10, [x9]
        cbz     x0, 4f
        mov     x16, #SYS_close
        svc     #0x80
4:      LOADADDR x9, src_index
        ldr     x10, [x9]
        LOADADDR x11, src_argc
        ldr     x11, [x11]
        cmp     x10, x11
        b.hs    _bye
        add     x12, x10, #1
        str     x12, [x9]
        LOADADDR x11, src_argv
        ldr     x11, [x11]
        ldr     x0, [x11, x10, lsl #3]
        LOADADDR x9, err_path
        str     x0, [x9]
        ldrb    w12, [x0]
        cmp     w12, #45                // "-"
        b.ne    5f
        ldrb    w12, [x0, #1]
        cbnz    w12, 5f
        mov     x0, #0
        b       6f
5:      mov     x1, #0                  // O_RDONLY
        mov     x16, #SYS_open
        svc     #0x80
        b.cs    7f
6:      LOADADDR x9, cur_fd
        str     x0, [x9]
        b       2b
7:      LOADADDR x0, msg_noopen
        bl      _cstr
        LOADADDR x9, err_path
        ldr     x0, [x9]
        bl      _cstr
        mov     w0, #10
        bl      _emit
        bl      _flush
        mov     x0, #1
        mov     x16, #SYS_exit
        svc     #0x80

_bye:
        bl      _flush
        mov     x0, #0
        mov     x16, #SYS_exit
        svc     #0x80

// _cstr: x0 = NUL-terminated string -> emitted
_cstr:
        ENTER
        stp     x24, x25, [sp, #-16]!
        mov     x24, x0
1:      ldrb    w0, [x24], #1
        cbz     w0, 2f
        bl      _emit
        b       1b
2:      ldp     x24, x25, [sp], #16
        LEAVE

// _emit: w0 = byte, appended to the output buffer
_emit:
        LOADADDR x9, outlen
        ldr     x10, [x9]
        cmp     x10, #BUF_SIZE
        b.lo    1f
        ENTER
        stp     x0, x1, [sp, #-16]!
        bl      _flush
        ldp     x0, x1, [sp], #16
        ldp     x29, x30, [sp], #16
        LOADADDR x9, outlen
        ldr     x10, [x9]
1:      LOADADDR x11, output_buffer
        strb    w0, [x11, x10]
        add     x10, x10, #1
        str     x10, [x9]
        ret

_flush:
        LOADADDR x9, outlen
        ldr     x2, [x9]
        cbz     x2, 2f
        str     xzr, [x9]
        LOADADDR x1, output_buffer
1:      mov     x0, #1
        mov     x16, #SYS_write
        stp     x1, x2, [sp, #-16]!
        svc     #0x80
        ldp     x1, x2, [sp], #16
        b.cs    2f
        add     x1, x1, x0
        subs    x2, x2, x0
        b.gt    1b
2:      ret

// _word: -> x0 = addr, x1 = len of the next blank-delimited word.
// A word starting with "\" discards the rest of the line.
_word:
        ENTER
1:      bl      _key
        cmp     w0, #92                 // backslash
        b.eq    4f
        cmp     w0, #32
        b.ls    1b
        LOADADDR x24, word_buffer
        mov     x25, x24
2:      sub     x9, x25, x24
        cmp     x9, #63
        b.hs    3f
        strb    w0, [x25], #1
3:      bl      _key
        cmp     w0, #32
        b.hi    2b
        mov     x0, x24
        sub     x1, x25, x24
        LEAVE
4:      bl      _key
        cmp     w0, #10
        b.ne    4b
        b       1b

// _number: x0 = addr, x1 = len -> x0 = value, x1 = count of unparsed chars
_number:
        mov     x2, x0
        mov     x3, x1
        mov     x0, #0
        cbz     x3, 6f
        LOADADDR x4, var_BASE
        ldr     x4, [x4]
        mov     x5, #0
        ldrb    w6, [x2]
        cmp     w6, #45                 // leading "-"
        b.ne    1f
        mov     x5, #1
        add     x2, x2, #1
        subs    x3, x3, #1
        b.ne    1f
        mov     x3, #1                  // "-" alone is not a number
        b       6f
1:      ldrb    w6, [x2]
        sub     w7, w6, #48             // 0-9
        cmp     w7, #10
        b.lo    3f
        sub     w7, w6, #65             // A-Z
        cmp     w7, #26
        b.lo    2f
        sub     w7, w6, #97             // a-z
        cmp     w7, #26
        b.hs    5f
2:      add     w7, w7, #10
3:      cmp     x7, x4
        b.hs    5f
        madd    x0, x0, x4, x7
        add     x2, x2, #1
        subs    x3, x3, #1
        b.ne    1b
5:      cbz     x5, 6f
        neg     x0, x0
6:      mov     x1, x3
        ret

// _find: x0 = addr, x1 = len -> x0 = header, or 0
_find:
        LOADADDR x2, var_LATEST
        ldr     x2, [x2]
1:      cbz     x2, 5f
        ldrb    w3, [x2, #8]
        and     w3, w3, #(F_HIDDEN | F_LENMASK)
        cmp     x3, x1
        b.ne    4f
        mov     x4, #0
2:      cmp     x4, x1
        b.eq    3f
        ldrb    w5, [x0, x4]
        add     x6, x2, #9
        ldrb    w6, [x6, x4]
        cmp     w5, w6
        b.ne    4f
        add     x4, x4, #1
        b       2b
3:      mov     x0, x2
        ret
4:      ldr     x2, [x2]
        b       1b
5:      mov     x0, #0
        ret

// _tcfa: x0 = header -> x0 = codeword address
_tcfa:
        ldrb    w1, [x0, #8]
        and     x1, x1, #F_LENMASK
        add     x0, x0, #9
        add     x0, x0, x1
        add     x0, x0, #7
        and     x0, x0, #~7
        ret

// _comma: x0 -> stored at HERE, HERE advances
_comma:
        LOADADDR x9, var_HERE
        ldr     x10, [x9]
        str     x0, [x10], #8
        str     x10, [x9]
        ret

// ---------------------------------------------------------------- stack

        defcode "DROP", DROP
        add     x20, x20, #8
        NEXT

        defcode "SWAP", SWAP
        ldp     x0, x1, [x20]
        stp     x1, x0, [x20]
        NEXT

        defcode "DUP", DUP
        ldr     x0, [x20]
        PUSH    x0
        NEXT

        defcode "OVER", OVER
        ldr     x0, [x20, #8]
        PUSH    x0
        NEXT

        defcode "ROT", ROT              // a b c -- b c a
        ldp     x0, x1, [x20]
        ldr     x2, [x20, #16]
        stp     x2, x0, [x20]
        str     x1, [x20, #16]
        NEXT

        defcode "-ROT", NROT            // a b c -- c a b
        ldp     x0, x1, [x20]
        ldr     x2, [x20, #16]
        stp     x1, x2, [x20]
        str     x0, [x20, #16]
        NEXT

        defcode "2DROP", TWODROP
        add     x20, x20, #16
        NEXT

        defcode "2DUP", TWODUP
        ldp     x0, x1, [x20]
        stp     x0, x1, [x20, #-16]!
        NEXT

        defcode "2SWAP", TWOSWAP
        ldp     x0, x1, [x20]
        ldp     x2, x3, [x20, #16]
        stp     x2, x3, [x20]
        stp     x0, x1, [x20, #16]
        NEXT

        defcode "?DUP", QDUP
        ldr     x0, [x20]
        cbz     x0, 1f
        PUSH    x0
1:      NEXT

// ---------------------------------------------------------------- arithmetic

        defcode "1+", INCR
        ldr     x0, [x20]
        add     x0, x0, #1
        str     x0, [x20]
        NEXT

        defcode "1-", DECR
        ldr     x0, [x20]
        sub     x0, x0, #1
        str     x0, [x20]
        NEXT

        defcode "8+", INCR8
        ldr     x0, [x20]
        add     x0, x0, #8
        str     x0, [x20]
        NEXT

        defcode "8-", DECR8
        ldr     x0, [x20]
        sub     x0, x0, #8
        str     x0, [x20]
        NEXT

        defbin  "+", ADD, add
        defbin  "-", SUB, sub
        defbin  "*", MUL, mul
        defbin  "AND", AND, and
        defbin  "OR", OR, orr
        defbin  "XOR", XOR, eor
        defbin  "LSHIFT", LSHIFT, lsl
        defbin  "RSHIFT", RSHIFT, lsr

        defcode "/MOD", DIVMOD          // a b -- rem quot (truncating)
        POP     x0
        POP     x1
        sdiv    x2, x1, x0
        msub    x3, x2, x0, x1
        PUSH    x3
        PUSH    x2
        NEXT

        defcode "NEGATE", NEGATE
        ldr     x0, [x20]
        neg     x0, x0
        str     x0, [x20]
        NEXT

        defcode "INVERT", INVERT
        ldr     x0, [x20]
        mvn     x0, x0
        str     x0, [x20]
        NEXT

        defcmp  "=", EQU, eq
        defcmp  "<>", NEQU, ne
        defcmp  "<", LT, lt
        defcmp  ">", GT, gt
        defcmp  "<=", LE, le
        defcmp  ">=", GE, ge
        defcmp  "U<", ULT, lo
        defcmp0 "0=", ZEQU, eq
        defcmp0 "0<>", ZNEQU, ne
        defcmp0 "0<", ZLT, lt
        defcmp0 "0>", ZGT, gt

// ---------------------------------------------------------------- memory

        defcode "!", STORE
        POP     x0
        POP     x1
        str     x1, [x0]
        NEXT

        defcode "@", FETCH
        ldr     x0, [x20]
        ldr     x0, [x0]
        str     x0, [x20]
        NEXT

        defcode "+!", ADDSTORE
        POP     x0
        POP     x1
        ldr     x2, [x0]
        add     x2, x2, x1
        str     x2, [x0]
        NEXT

        defcode "C!", STOREBYTE
        POP     x0
        POP     x1
        strb    w1, [x0]
        NEXT

        defcode "C@", FETCHBYTE
        ldr     x0, [x20]
        ldrb    w0, [x0]
        str     x0, [x20]
        NEXT

        defcode "CMOVE", CMOVE          // src dst len --
        POP     x2
        POP     x1
        POP     x0
1:      cbz     x2, 2f
        ldrb    w3, [x0], #1
        strb    w3, [x1], #1
        sub     x2, x2, #1
        b       1b
2:      NEXT

        defcode "FILL", FILL            // addr len byte --
        POP     x2
        POP     x1
        POP     x0
1:      cbz     x1, 2f
        strb    w2, [x0], #1
        sub     x1, x1, #1
        b       1b
2:      NEXT

// ---------------------------------------------------------------- return stack

        defcode ">R", TOR
        POP     x0
        PUSHRSP x0
        NEXT

        defcode "R>", FROMR
        POPRSP  x0
        PUSH    x0
        NEXT

        defcode "R@", RFETCH
        ldr     x0, [x21]
        PUSH    x0
        NEXT

        defcode "RSP@", RSPFETCH
        PUSH    x21
        NEXT

        defcode "RSP!", RSPSTORE
        POP     x21
        NEXT

        defcode "RDROP", RDROP
        add     x21, x21, #8
        NEXT

        defcode "DSP@", DSPFETCH
        mov     x0, x20
        PUSH    x0
        NEXT

        defcode "DSP!", DSPSTORE
        ldr     x20, [x20]
        NEXT

// Counted loops keep ( limit index ) on the return stack, index on top.
        defcode "I", I
        ldr     x0, [x21]
        PUSH    x0
        NEXT

        defcode "J", J
        ldr     x0, [x21, #16]
        PUSH    x0
        NEXT

        defcode "(LOOP)", PLOOP         // -- done?
        ldp     x0, x1, [x21]
        add     x0, x0, #1
        str     x0, [x21]
        cmp     x0, x1
        csetm   x2, ge
        PUSH    x2
        NEXT

        defcode "(+LOOP)", PPLOOP       // step -- done?
        POP     x3
        ldp     x0, x1, [x21]
        add     x0, x0, x3
        str     x0, [x21]
        cmp     x0, x1
        csetm   x2, ge
        csetm   x4, lt
        cmp     x3, #0
        csel    x2, x4, x2, lt
        PUSH    x2
        NEXT

        defcode "UNLOOP", UNLOOP
        add     x21, x21, #16
        NEXT

// ---------------------------------------------------------------- variables, constants

        defvar  "STATE", STATE
        defvar  "HERE", HERE, dict_space
        defvar  "LATEST", LATEST, name_BYE
        defvar  "S0", SZ
        defvar  "BASE", BASE, 10

        defaddr "R0", RZ, return_stack, RSTACK_SIZE
        defaddr "DOCOL", __DOCOL, DOCOL
        defaddr "DODOES", __DODOES, DODOES
        defaddr "DICT0", DICT0, dict_space
        defconst "F_IMMED", __F_IMMED, F_IMMED
        defconst "F_HIDDEN", __F_HIDDEN, F_HIDDEN
        defconst "F_LENMASK", __F_LENMASK, F_LENMASK

// ---------------------------------------------------------------- I/O words

        defcode "KEY", KEY
        bl      _key
        PUSH    x0
        NEXT

        defcode "EMIT", EMIT
        POP     x0
        bl      _emit
        NEXT

        defcode "TELL", TELL            // addr len --
        POP     x25
        POP     x24
1:      cbz     x25, 2f
        ldrb    w0, [x24], #1
        bl      _emit
        sub     x25, x25, #1
        b       1b
2:      NEXT

        defcode "FLUSH", FLUSH
        bl      _flush
        NEXT

        defcode "MS", MS                // n -- (sleep n milliseconds)
        bl      _flush
        POP     x0
        mov     x1, #1000
        udiv    x2, x0, x1              // seconds
        msub    x3, x2, x1, x0          // leftover ms
        mul     x3, x3, x1              // -> microseconds
        stp     x2, x3, [sp, #-16]!     // struct timeval
        mov     x0, #0
        mov     x1, #0
        mov     x2, #0
        mov     x3, #0
        mov     x4, sp
        mov     x16, #SYS_select
        svc     #0x80
        add     sp, sp, #16
        NEXT

        defcode "WORD", WORD
        bl      _word
        PUSH    x0
        PUSH    x1
        NEXT

        defcode "NUMBER", NUMBER        // addr len -- n unparsed
        POP     x1
        POP     x0
        bl      _number
        PUSH    x0
        PUSH    x1
        NEXT

        defcode "CHAR", CHAR
        bl      _word
        ldrb    w0, [x0]
        PUSH    x0
        NEXT

// ---------------------------------------------------------------- dictionary

        defcode "FIND", FIND            // addr len -- header|0
        POP     x1
        POP     x0
        bl      _find
        PUSH    x0
        NEXT

        defcode ">CFA", TCFA
        POP     x0
        bl      _tcfa
        PUSH    x0
        NEXT

        defcode "CREATE", CREATE        // addr len --
        POP     x1
        POP     x0
        LOADADDR x2, var_HERE
        ldr     x3, [x2]
        LOADADDR x4, var_LATEST
        ldr     x5, [x4]
        str     x5, [x3]
        str     x3, [x4]
        and     x6, x1, #F_LENMASK
        strb    w6, [x3, #8]
        add     x7, x3, #9
        mov     x8, #0
1:      cmp     x8, x6
        b.hs    2f
        ldrb    w9, [x0, x8]
        strb    w9, [x7, x8]
        add     x8, x8, #1
        b       1b
2:      add     x7, x7, x6
        add     x7, x7, #7
        and     x7, x7, #~7
        str     x7, [x2]
        NEXT

        defcode ",", COMMA
        POP     x0
        bl      _comma
        NEXT

        defcode "[", LBRAC, F_IMMED
        LOADADDR x9, var_STATE
        str     xzr, [x9]
        NEXT

        defcode "]", RBRAC
        LOADADDR x9, var_STATE
        mov     x10, #1
        str     x10, [x9]
        NEXT

        defcode "IMMEDIATE", IMMEDIATE, F_IMMED
        LOADADDR x9, var_LATEST
        ldr     x9, [x9]
        ldrb    w10, [x9, #8]
        eor     w10, w10, #F_IMMED
        strb    w10, [x9, #8]
        NEXT

        defcode "HIDDEN", HIDDEN        // header --
        POP     x9
        ldrb    w10, [x9, #8]
        eor     w10, w10, #F_HIDDEN
        strb    w10, [x9, #8]
        NEXT

        defword ":", COLON
        .quad   WORD, CREATE, LIT, DOCOL, COMMA, LATEST, FETCH, HIDDEN, RBRAC, EXIT

        defword ";", SEMICOLON, F_IMMED
        .quad   LIT, EXIT, COMMA, LATEST, FETCH, HIDDEN, LBRAC, EXIT

// ---------------------------------------------------------------- control

        defcode "EXIT", EXIT
        POPRSP  x19
        NEXT

        defcode "LIT", LIT
        ldr     x0, [x19], #8
        PUSH    x0
        NEXT

        defcode "'", TICK               // compiled: push the next cell, skip it
        ldr     x0, [x19], #8
        PUSH    x0
        NEXT

        defcode "BRANCH", BRANCH        // offset is relative to the offset cell
        ldr     x0, [x19]
        add     x19, x19, x0
        NEXT

        defcode "0BRANCH", ZBRANCH
        POP     x0
        cbz     x0, code_BRANCH
        add     x19, x19, #8
        NEXT

        defcode "LITSTRING", LITSTRING  // -- addr len
        ldr     x0, [x19], #8
        PUSH    x19
        PUSH    x0
        add     x19, x19, x0
        add     x19, x19, #7
        and     x19, x19, #~7
        NEXT

        defcode "(DOES>)", PDOES        // point the newest word at the rest of this one
        LOADADDR x0, var_LATEST
        ldr     x0, [x0]
        bl      _tcfa
        str     x19, [x0, #8]
        POPRSP  x19
        NEXT

        defcode "EXECUTE", EXECUTE
        POP     x0
        ldr     x1, [x0]
        br      x1

        defcode "INTERPRET", INTERPRET
        bl      _word
        mov     x26, x0
        mov     x27, x1
        bl      _find
        cbz     x0, 3f
        ldrb    w28, [x0, #8]
        bl      _tcfa
        tst     w28, #F_IMMED
        b.ne    1f
        LOADADDR x9, var_STATE
        ldr     x9, [x9]
        cbz     x9, 1f
        bl      _comma                  // compiling: append the xt
        NEXT
1:      ldr     x1, [x0]                // interpreting, or immediate: run it
        br      x1
3:      mov     x0, x26
        mov     x1, x27
        bl      _number
        cbnz    x1, 5f
        mov     x26, x0
        LOADADDR x9, var_STATE
        ldr     x9, [x9]
        cbz     x9, 4f
        LOADADDR x0, LIT                // compiling a literal
        bl      _comma
        mov     x0, x26
        bl      _comma
        NEXT
4:      PUSH    x26
        NEXT
5:      mov     x24, x26                // unknown: print "word ?"
        mov     x25, x27
6:      cbz     x25, 7f
        ldrb    w0, [x24], #1
        bl      _emit
        sub     x25, x25, #1
        b       6b
7:      mov     w0, #32
        bl      _emit
        mov     w0, #63
        bl      _emit
        mov     w0, #10
        bl      _emit
        NEXT

        defword "QUIT", QUIT
        .quad   RZ, RSPSTORE, INTERPRET, BRANCH, -16

        defcode "BYE", BYE
        b       _bye

// ---------------------------------------------------------------- data

        .data
        .balign 8
cold_start:     .quad QUIT
currkey:        .quad 0
bufftop:        .quad 0
outlen:         .quad 0
cur_fd:         .quad -1
src_index:      .quad 1
src_argc:       .quad 0
src_argv:       .quad 0
err_path:       .quad 0
msg_noopen:     .asciz "ember: cannot open "
