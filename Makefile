ember: ember.s
	clang -o ember ember.s

repl: ember
	./ember core.fs -

demo: ember
	./ember core.fs demos/mandel.fs demos/self.fs demos/life.fs

song: ember
	./ember core.fs lib/tracker.fs demos/song.fs > kindling.wav && afplay kindling.wav

selfhost: ember
	mkdir -p out && ./ember core.fs lib/tracker.fs lib/synth.fs demos/selfhost.fs > out/self-hosting.wav && afplay out/self-hosting.wav

lantern: ember
	./ember core.fs lib/tracker.fs demos/lantern.fs > lantern.wav && afplay lantern.wav

test: ember
	./tests/run.sh

bench: ember
	./bench/run.sh

.PHONY: repl demo song lantern selfhost test bench
