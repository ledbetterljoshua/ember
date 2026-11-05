// ember: a Forth kernel in AArch64 assembly for macOS.
//
// Indirect-threaded. The kernel defines only what cannot be said in Forth;
// everything else (IF, loops, comments, strings, printing numbers, SEE) is
// grown from these primitives by core.fs.
//
// Registers:  x19 = IP (next cell to execute)
//             x22 = top of the data stack
//             x20 = data stack pointer to the rest (full, descending)
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
        .set SYS_sigaction, 46
        .set SYS_mprotect, 74
        .set SYS_select, 93

// Each stack sits between PROT_NONE guard pages, so running off either end
// faults at once instead of overwriting the dictionary or kernel variables.
// Slack above S0 (48 KB, under its own guard page) and R0 (a page) lets an
// underflow reach ?STACK; untouched, it costs no memory.
        .set PAGE, 0x4000
        .zerofill __DATA,__bss,guard_low,PAGE,14
        .zerofill __DATA,__bss,data_stack,2*DSTACK_SIZE,14
        .zerofill __DATA,__bss,guard_mid,PAGE,14
        .zerofill __DATA,__bss,return_stack,RSTACK_SIZE+PAGE,14
        .zerofill __DATA,__bss,guard_high,PAGE,14
// Page numbers of the guards, counting from guard_low, for _fault. The
// data stack's own guard against underflow is the top page of its slack.
        .set G_DTOP, 2*DSTACK_SIZE/PAGE
        .set G_RLOW, G_DTOP + 1
        .set G_HIGH, G_RLOW + 1 + (RSTACK_SIZE+PAGE)/PAGE
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
        str     x22, [x20, #-8]!
        mov     x22, \reg
        .endm
        .macro POP reg
        mov     \reg, x22
        ldr     x22, [x20], #8
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
// No label may start with L: Darwin's assembler keeps those out of the
// symbol table, so LIT is __LIT, as DOCOL's word is __DOCOL.
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
        ldr     x1, [x20], #8
        cmp     x1, x22
        csetm   x22, \cond
        NEXT
        .endm

        .macro defcmp0 name, label, cond
        defcode "\name", \label
        cmp     x22, #0
        csetm   x22, \cond
        NEXT
        .endm

        .macro defbin name, label, op
        defcode "\name", \label
        ldr     x1, [x20], #8
        \op     x22, x1, x22
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
        LOADADDR x0, guard_low
        bl      _guard
        LOADADDR x0, guard_mid
        bl      _guard
        LOADADDR x0, guard_high
        bl      _faults
        LOADADDR x20, data_stack        // empty: x22 is junk that spills to S0
        add     x20, x20, #DSTACK_SIZE
        mov     x22, #0
        sub     x9, x20, #8
        LOADADDR x10, var_SZ
        str     x9, [x10]
        LOADADDR x21, return_stack
        add     x21, x21, #RSTACK_SIZE
        LOADADDR x19, cold_start
        NEXT

_guard:                                 // x0 = page -> PROT_NONE
        mov     x1, #PAGE
        mov     x2, #0
        mov     x16, #SYS_mprotect
        svc     #0x80
        ret

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
3:      LOADADDR x9, cur_fd             // source exhausted: close it and hand
        ldr     x0, [x9]                // back one 0 byte to mark its end; the
        mov     x10, #-1                // next refill opens the next source
        str     x10, [x9]
        cbz     x0, 1f
        mov     x16, #SYS_close
        svc     #0x80
1:      LOADADDR x1, input_buffer
        strb    wzr, [x1]
        LOADADDR x9, currkey
        str     x1, [x9]
        add     x1, x1, #1
        LOADADDR x9, bufftop
        str     x1, [x9]
        ldp     x29, x30, [sp], #16
        b       _key
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
7:      bl      _to_err
        LOADADDR x0, msg_noopen
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
        LOADADDR x0, var_STATUS         // exit status: 1 once any error was reported
        ldr     x0, [x0]
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

// _to_err / _to_out: switch the output stream, flushing what's buffered first
_to_err:
        LOADADDR x9, var_STATUS
        mov     x10, #1
        str     x10, [x9]
        mov     x9, #2
        b       1f
_to_out:
        mov     x9, #1
1:      ENTER
        str     x9, [sp, #-16]!
        bl      _flush
        ldr     x9, [sp], #16
        LOADADDR x10, var_OUT
        str     x9, [x10]
        LEAVE

_flush:
        LOADADDR x9, outlen
        ldr     x2, [x9]
        cbz     x2, 2f
        str     xzr, [x9]
        LOADADDR x1, output_buffer
1:      LOADADDR x0, var_OUT
        ldr     x0, [x0]
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
        cbz     w0, 6f                  // end of a source: an empty word
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
        cbnz    w0, 5f                  // a 0 ended the word: put it back so
        LOADADDR x9, currkey            // the next WORD sees the end too
        ldr     x10, [x9]
        sub     x10, x10, #1
        str     x10, [x9]
5:      LOADADDR x9, var_DELIM          // what ended the word: newline or not
        str     x0, [x9]
        mov     x0, x24
        sub     x1, x25, x24
        LEAVE
4:      bl      _key
        cbz     w0, 6f
        cmp     w0, #10
        b.ne    4b
        b       1b
6:      LOADADDR x9, var_DELIM
        str     xzr, [x9]
        LOADADDR x0, word_buffer
        mov     x1, #0
        LEAVE

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
        mov     x9, #F_LENMASK          // names are significant to 31 chars
        cmp     x1, x9
        csel    x1, x1, x9, ls
        LOADADDR x2, var___LATEST
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
        ldr     x22, [x20], #8
        NEXT

        defcode "SWAP", SWAP
        ldr     x0, [x20]
        str     x22, [x20]
        mov     x22, x0
        NEXT

        defcode "DUP", DUP
        str     x22, [x20, #-8]!
        NEXT

        defcode "OVER", OVER
        ldr     x0, [x20]
        str     x22, [x20, #-8]!
        mov     x22, x0
        NEXT

        defcode "ROT", ROT              // a b c -- b c a
        ldr     x0, [x20]
        ldr     x1, [x20, #8]
        stp     x22, x0, [x20]
        mov     x22, x1
        NEXT

        defcode "-ROT", NROT            // a b c -- c a b
        ldr     x0, [x20]
        ldr     x1, [x20, #8]
        stp     x1, x22, [x20]
        mov     x22, x0
        NEXT

        defcode "2DROP", TWODROP
        ldr     x22, [x20, #8]
        add     x20, x20, #16
        NEXT

        defcode "2DUP", TWODUP
        ldr     x0, [x20]
        stp     x0, x22, [x20, #-16]!
        NEXT

        defcode "2SWAP", TWOSWAP        // a b c d -- c d a b
        ldr     x0, [x20]
        ldr     x1, [x20, #8]
        ldr     x2, [x20, #16]
        stp     x2, x22, [x20]
        str     x0, [x20, #16]
        mov     x22, x1
        NEXT

        defcode "?DUP", QDUP
        cbz     x22, 1f
        str     x22, [x20, #-8]!
1:      NEXT

// ---------------------------------------------------------------- arithmetic

        defcode "1+", INCR
        add     x22, x22, #1
        NEXT

        defcode "1-", DECR
        sub     x22, x22, #1
        NEXT

        defcode "8+", INCR8
        add     x22, x22, #8
        NEXT

        defcode "8-", DECR8
        sub     x22, x22, #8
        NEXT

        defbin  "+", ADD, add
        defbin  "-", SUB, sub
        defbin  "*", MUL, mul
        defbin  "AND", AND, and
        defbin  "OR", OR, orr
        defbin  "XOR", XOR, eor
        defbin  "LSHIFT", __LSHIFT, lsl
        defbin  "RSHIFT", RSHIFT, lsr

        defcode "/MOD", DIVMOD          // a b -- rem quot (truncating)
        cbz     x22, 2f
1:      ldr     x1, [x20]
        sdiv    x2, x1, x22
        msub    x3, x2, x22, x1
        str     x3, [x20]
        mov     x22, x2
        NEXT
2:      LOADADDR x9, var_DIVZERO        // b = 0: run the handler, if set;
        ldr     x0, [x9]                // otherwise sdiv's quiet 0
        cbz     x0, 1b
        ldr     x22, [x20, #8]          // the handler gets the stack without a b
        add     x20, x20, #16
        ldr     x1, [x0]
        br      x1

        defcode "NEGATE", NEGATE
        neg     x22, x22
        NEXT

        defcode "INVERT", INVERT
        mvn     x22, x22
        NEXT

        defcmp  "=", EQU, eq
        defcmp  "<>", NEQU, ne
        defcmp  "<", __LT, lt
        defcmp  ">", GT, gt
        defcmp  "<=", __LE, le
        defcmp  ">=", GE, ge
        defcmp  "U<", ULT, lo
        defcmp0 "0=", ZEQU, eq
        defcmp0 "0<>", ZNEQU, ne
        defcmp0 "0<", ZLT, lt
        defcmp0 "0>", ZGT, gt

// ---------------------------------------------------------------- memory

        defcode "!", STORE
        ldr     x1, [x20], #8
        str     x1, [x22]
        ldr     x22, [x20], #8
        NEXT

        defcode "@", FETCH
        ldr     x22, [x22]
        NEXT

        defcode "+!", ADDSTORE
        ldr     x1, [x20], #8
        ldr     x2, [x22]
        add     x2, x2, x1
        str     x2, [x22]
        ldr     x22, [x20], #8
        NEXT

        defcode "C!", STOREBYTE
        ldr     x1, [x20], #8
        strb    w1, [x22]
        ldr     x22, [x20], #8
        NEXT

        defcode "C@", FETCHBYTE
        ldrb    w22, [x22]
        NEXT

        defcode "CMOVE", CMOVE          // src dst len --
        mov     x2, x22
        ldr     x1, [x20]
        ldr     x0, [x20, #8]
        ldr     x22, [x20, #16]
        add     x20, x20, #24
1:      cbz     x2, 2f
        ldrb    w3, [x0], #1
        strb    w3, [x1], #1
        sub     x2, x2, #1
        b       1b
2:      NEXT

        defcode "FILL", FILL            // addr len byte --
        mov     x2, x22
        ldr     x1, [x20]
        ldr     x0, [x20, #8]
        ldr     x22, [x20, #16]
        add     x20, x20, #24
1:      cbz     x1, 2f
        strb    w2, [x0], #1
        sub     x1, x1, #1
        b       1b
2:      NEXT

// ---------------------------------------------------------------- return stack

        defcode ">R", TOR
        str     x22, [x21, #-8]!
        ldr     x22, [x20], #8
        NEXT

        defcode "R>", FROMR
        str     x22, [x20, #-8]!
        ldr     x22, [x21], #8
        NEXT

        defcode "R@", RFETCH
        str     x22, [x20, #-8]!
        ldr     x22, [x21]
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

// DSP@ spills the top into memory so the whole stack reads as cells from
// the address it returns; DSP! reloads the top from the address it is given.
        defcode "DSP@", DSPFETCH
        str     x22, [x20, #-8]!
        mov     x22, x20
        NEXT

        defcode "DSP!", DSPSTORE
        add     x20, x22, #8
        ldr     x22, [x22]
        NEXT

// Counted loops keep ( limit index ) on the return stack, index on top.
        defcode "I", I
        str     x22, [x20, #-8]!
        ldr     x22, [x21]
        NEXT

        defcode "J", J
        str     x22, [x20, #-8]!
        ldr     x22, [x21, #16]
        NEXT

// (LOOP) and (+LOOP) are followed by a branch offset cell, like BRANCH: they
// jump back by it until the index reaches the limit, then fall through.
// Index and limit are read with two ldr, not one ldp: an ldp spanning the
// index stored on the previous pass can't be forwarded from that store and
// waits for it, which cancelled the gain from branching here.
        defcode "(LOOP)", PLOOP         // --
        ldr     x0, [x21]
        ldr     x1, [x21, #8]
        add     x0, x0, #1
        str     x0, [x21]
        cmp     x0, x1
        b.ge    1f
        ldr     x2, [x19]
        add     x19, x19, x2
        NEXT
1:      add     x19, x19, #8
        NEXT

        defcode "(+LOOP)", PPLOOP       // step --
        POP     x3
        ldr     x0, [x21]
        ldr     x1, [x21, #8]
        add     x0, x0, x3
        str     x0, [x21]
        cmp     x0, x1
        tbnz    x3, #63, 2f             // counting down: done once below limit
        b.ge    1f
        b       3f
2:      b.lt    1f
3:      ldr     x2, [x19]
        add     x19, x19, x2
        NEXT
1:      add     x19, x19, #8
        NEXT

        defcode "UNLOOP", UNLOOP
        add     x21, x21, #16
        NEXT

// ---------------------------------------------------------------- variables, constants

        defvar  "STATE", STATE
        defvar  "HERE", HERE, dict_space
        defvar  "LATEST", __LATEST, name_STACKFAULT
        defvar  "S0", SZ
        defvar  "BASE", BASE, 10
        defvar  "STATUS", STATUS        // process exit status
        defvar  "OUT", OUT, 1           // fd that EMIT and TELL write to
        defvar  "DELIM", DELIM, 10      // the byte that ended the last WORD
        defvar  "DIVZERO", DIVZERO      // xt that /MOD runs when b = 0

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
        LOADADDR x4, var___LATEST
        ldr     x5, [x4]
        str     x5, [x3]
        str     x3, [x4]
        mov     x6, #F_LENMASK          // keep 31 chars; a masked length
        cmp     x1, x6                  // would name a different, shorter word
        csel    x6, x1, x6, ls
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

        defcode "[", __LBRAC, F_IMMED
        LOADADDR x9, var_STATE
        str     xzr, [x9]
        NEXT

        defcode "]", RBRAC
        LOADADDR x9, var_STATE
        mov     x10, #1
        str     x10, [x9]
        NEXT

        defcode "IMMEDIATE", IMMEDIATE, F_IMMED
        LOADADDR x9, var___LATEST
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
        .quad   WORD, CREATE, __LIT, DOCOL, COMMA, __LATEST, FETCH, HIDDEN, RBRAC, EXIT

// ";" clears the hidden bit rather than toggling it, so a stray ";" can't
// hide the last finished word.
        defword ";", SEMICOLON, F_IMMED
        .quad   __LIT, EXIT, COMMA, __LATEST, FETCH, INCR8, DUP, FETCHBYTE
        .quad   __F_HIDDEN, INVERT, AND, SWAP, STOREBYTE, __LBRAC, EXIT

// ---------------------------------------------------------------- control

        defcode "EXIT", EXIT
        POPRSP  x19
        NEXT

        defcode "LIT", __LIT
        str     x22, [x20, #-8]!
        ldr     x22, [x19], #8
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
        mov     x0, x22
        ldr     x22, [x20], #8
        cbz     x0, code_BRANCH
        add     x19, x19, #8
        NEXT

        defcode "LITSTRING", __LITSTRING // -- addr len
        ldr     x0, [x19], #8
        PUSH    x19
        PUSH    x0
        add     x19, x19, x0
        add     x19, x19, #7
        and     x19, x19, #~7
        NEXT

        defcode "(DOES>)", PDOES        // point the newest word at the rest of this one
        LOADADDR x0, var___LATEST
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
        cbnz    x1, 2f
        NEXT                            // end of a source
2:      mov     x26, x0
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
        LOADADDR x0, __LIT              // compiling a literal
        bl      _comma
        mov     x0, x26
        bl      _comma
        NEXT
4:      PUSH    x26
        NEXT
5:      bl      _to_err                 // unknown: "word ?" to stderr
        mov     x24, x26
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
        bl      _to_out
        NEXT

        defword "QUIT", QUIT
        .quad   RZ, RSPSTORE, INTERPRET, BRANCH, -16

        defcode "BYE", BYE
        b       _bye

// ---------------------------------------------------------------- faults
// After every other word, so adding it moved no hot code.

        .text
        .p2align 2
_faults:                                // x0 = guard_high; called once by _main
        mov     x9, sp
        LOADADDR x10, saved_sp
        str     x9, [x10]
        stp     x29, x30, [sp, #-16]!
        bl      _guard
        LOADADDR x0, data_stack         // top page of the data stack's slack
        add     x0, x0, #2*DSTACK_SIZE-PAGE
        bl      _guard
        mov     x0, #10                 // SIGBUS: what a guard page raises
        bl      _catch
        mov     x0, #11                 // SIGSEGV
        bl      _catch
        ldp     x29, x30, [sp], #16
        ret

_catch:                                 // x0 = signal -> handled by _fault
        LOADADDR x1, sigact
        mov     x2, #0
        mov     x16, #SYS_sigaction
        svc     #0x80
        ret

// _fault is installed as the signal trampoline (sa_tramp), so the kernel
// calls it directly: x2 = signal, x3 = siginfo (si_addr at +24). It never
// returns to the kernel; SA_NODEFER keeps the signal unblocked for next
// time. A fault on a guard page resets both stacks and the machine stack and
// runs the xt in STACKFAULT with 1/-1 (data stack over/underflow) or 2/-2
// (return stack). Any other fault, or no xt yet, flushes and exits with
// 128 + signal.
_fault:
        ldr     x9, [x3, #24]
        LOADADDR x10, guard_low
        sub     x9, x9, x10
        lsr     x9, x9, #14             // page number from guard_low
        mov     x12, #1
        cbz     x9, 2f
        mov     x12, #-1
        cmp     x9, #G_DTOP
        b.eq    2f
        mov     x12, #2
        cmp     x9, #G_RLOW
        b.eq    2f
        mov     x12, #-2
        cmp     x9, #G_HIGH
        b.eq    2f
1:      mov     x12, x2
        bl      _flush
        add     x0, x12, #128
        mov     x16, #SYS_exit
        svc     #0x80
2:      LOADADDR x9, var_STACKFAULT
        ldr     x0, [x9]
        cbz     x0, 1b
        LOADADDR x9, saved_sp
        ldr     x9, [x9]
        mov     sp, x9
        LOADADDR x20, data_stack        // a data stack holding just the code
        add     x20, x20, #DSTACK_SIZE
        sub     x20, x20, #8
        mov     x22, x12
        LOADADDR x21, return_stack
        add     x21, x21, #RSTACK_SIZE
        ldr     x1, [x0]
        br      x1

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
        .balign 8
saved_sp:       .quad 0
sigact:         .quad _fault, _fault    // struct __sigaction: handler, tramp,
                .long 0, 0x50           // mask, SA_NODEFER | SA_SIGINFO

// Last, so that adding it moved no other code or data.
        defvar  "STACKFAULT", STACKFAULT // xt run on a guard-page fault
