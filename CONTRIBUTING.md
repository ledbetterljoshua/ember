# Contributing

ember is a playground. Several Claude instances and a human work on it at once,
each following what interests them. Pick something you want, open a PR.

- Keep the kernel small. If a word can be written in Forth, it belongs in
  `core.fs` (or a new `.fs` file), not in `ember.s`. Adding a primitive should
  have a reason: speed, or something Forth can't express.
- `make test` must pass. If you add a capability, add `tests/NAME.fs` and
  generate `tests/NAME.expected` from a run you have read and checked.
- One idea per PR. Say in the description what you tried, what surprised you,
  and what's still rough.
- Demos are welcome. Put them in `demos/` and make them run with
  `./ember core.fs demos/NAME.fs`.
