# ember

A Forth that starts as hand-written AArch64 assembly and grows the rest of
itself in Forth.

- `ember.s` is the kernel: 3.7 KB of machine code, 95 primitive words. No libc
  calls; all I/O is raw `svc #0x80` syscalls.
- `core.fs` grows 72 more words in the language itself: `IF`/`ELSE`/`THEN`,
  every loop, comments, strings, number printing, `CONSTANT`, `VARIABLE`, and
  `SEE`, a decompiler that rebuilds source from compiled threaded code (and
  prints raw ARM64 instruction words for primitives).
- `lib/disasm.fs`, loaded after `core.fs`, is an AArch64 disassembler in
  Forth. With it, `SEE DUP` prints assembly, with branch targets and
  `adrp`/`add` pairs named from the binary's own symbol table, which it reads
  out of the Mach-O image in memory. `lib/disasm-check.sh` diffs it against
  `objdump -d`, instruction by instruction.
- `lib/see.fs` makes `SEE` rebuild `IF`/`ELSE`/`THEN`, `BEGIN`/`UNTIL`/`AGAIN`,
  `BEGIN`/`WHILE`/`REPEAT`, `DO`/`LOOP` and `."` from the branch offsets, so
  `SEE (` prints `: ( IMMEDIATE 1 BEGIN KEY DUP '(' = IF DROP 1+ ELSE ...`.
  `lib/see-check.sh` recompiles every printout and compares it cell by cell.
- `demos/`: Mandelbrot in fixed point, Conway's Life on a torus with a Gosper
  gun that its own gliders eventually destroy, `self.fs`, where the system
  decompiles its own decompiler; two songs, `song.fs` (a chiptune) and
  `lantern.fs` (a music-box waltz), each a tracker score that ember renders to
  a stereo WAV on stdout; and `fire.fs`, a fire in 24-bit color that burns for
  a while and then goes out.
- `lib/tracker.fs`: the studio the songs share: sine table, tuning, score
  readers, envelopes, mixing, echo and a WAV renderer. Load it before a song:
  `./ember core.fs lib/tracker.fs demos/lantern.fs > lantern.wav`.

```
make            # build
make repl       # interactive: type Forth, ctrl-D to quit
make demo
make test
make song       # renders demos/song.fs to kindling.wav and plays it
make bench      # CPU cycles per benchmark (bench/run.sh ../other . compares)
make lantern    # renders demos/lantern.fs to lantern.wav and plays it
./ember core.fs demos/life.fs
./ember core.fs lib/disasm.fs -    # REPL where SEE DUP reads as assembly
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
