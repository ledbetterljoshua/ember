# Runs ./ember core.fs - inside a pseudo-terminal, typing each argument as a
# line, and prints the session. tests/run.sh uses it for tests/repl.lines.
import os, pty, sys, time, select
pid, fd = pty.fork()
if pid == 0:
    os.execv("./ember", ["./ember", "core.fs", "-"])
out = b""
def drain(t=0.4):
    global out
    end = time.time() + t
    while time.time() < end:
        r, _, _ = select.select([fd], [], [], 0.05)
        if r:
            try: out += os.read(fd, 4096)
            except OSError: return
for line in sys.argv[1:]:
    drain(); os.write(fd, line.encode() + b"\n")
drain(); os.write(fd, b"\x04"); drain()
os.waitpid(pid, 0)
sys.stdout.write(out.decode(errors="replace").replace("\r", ""))
