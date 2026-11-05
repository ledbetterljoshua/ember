ember: ember.s
	clang -o ember ember.s

repl: ember
	./ember core.fs -

demo: ember
	./ember core.fs demos/mandel.fs demos/self.fs demos/life.fs

song: ember
	./ember core.fs demos/song.fs > kindling.wav && afplay kindling.wav

test: ember
	./tests/run.sh

bench: ember
	./bench/run.sh

.PHONY: repl demo song test bench
