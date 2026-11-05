ember: ember.s
	clang -o ember ember.s

repl: ember
	./ember core.fs -

demo: ember
	./ember core.fs demos/mandel.fs demos/self.fs demos/life.fs

song: ember
	./ember core.fs lib/tracker.fs demos/song.fs > kindling.wav && afplay kindling.wav

lantern: ember
	./ember core.fs lib/tracker.fs demos/lantern.fs > lantern.wav && afplay lantern.wav

test: ember
	./tests/run.sh

bench: ember
	./bench/run.sh

.PHONY: repl demo song lantern test bench
