# ember

A Forth that starts as hand-written AArch64 assembly and grows the rest of
itself in Forth.

- `ember.s` is the kernel: 3.7 KB of machine code, 95 primitive words. No libc
  calls; all I/O is raw `svc #0x80` syscalls.
- `core.fs` grows 72 more words in the language itself: `IF`/`ELSE`/`THEN`,
  every loop, comments, strings, number printing, `CONSTANT`, `VARIABLE`, and
  `SEE`, a decompiler that rebuilds source from compiled threaded code (and
  prints raw ARM64 instruction words for primitives).
- `demos/`: Mandelbrot in fixed point, Conway's Life on a torus with a Gosper
  gun that its own gliders eventually destroy, `self.fs`, where the system
  decompiles its own decompiler, and `fire.fs`, a fire in 24-bit color that
  burns for a while and then goes out.

```
make            # build
make repl       # interactive: type Forth, ctrl-D to quit
make demo
make test
./ember core.fs demos/life.fs
```

Try in the REPL: `SEE SEE`, `SEE DUP`, `WORDS`, `BANNER`,
`: SQ DUP * ; 12 SQ .`

## How it works

Indirect threading. Every word has a header (link, flags+length, name) and a
codeword: the address of machine code to run. Primitives point at their own
assembly; colon definitions point at `DOCOL`, which pushes the instruction
pointer and starts walking a list of other words' codeword addresses. The whole
inner interpreter is three instructions:

```
ldr x0, [x19], #8    // W = *IP++
ldr x1, [x0]         // codeword
br  x1
```

Registers: `x19` IP, `x20` data stack, `x21` return stack. The kernel's own
compiler (`:` and `;`) is written as threaded code in the assembly file, since
there is nothing yet to write it in.

`IF` doesn't exist until `core.fs` line 34 defines it, and comments don't exist
until line 46, so everything above that point uses `\` line comments, which the
assembly tokenizer handles.

Architecture after Richard W.M. Jones's jonesforth (i386, 2009), written fresh
for ARM64 macOS.
