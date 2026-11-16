---
layout: default
title: "Installing Zero.IDE"
---

# Installing Zero.IDE

Section 1 — Intro · Chapter 1.3

[Previous](zasm-01-02-why-learn-assembly-today.md) · [Contents](index.md) · [Next](zasm-01-04-using-zero-ide-my-first-project.md)

Use a Windows x64 computer for the main learning path and native debugger.
The IDE and compiler have independent versions; retain a compatible pair.

1. Open the release downloads from the
   [documentation home](../index.md#download-and-install).
2. If the release provides a Windows x64 **Zero.IDE setup** asset, download and
   run it. IDE setup includes the CLI installer and uses a shared install root.
3. Launch `Zero.Ide.exe` from the installed `ide` directory or its launcher.
4. Open **Tools > Options > Compiler**. The IDE discovers the sibling
   `bin/zero.exe` for new or blank settings; select it explicitly if necessary.
5. Save a new `.zasm` file in a writable folder outside Program Files.

```text
Zero/
  bin/zero.exe
  ide/Zero.Ide.exe
  include/windows/
  samples/
  docs/
```

A portable CLI ZIP is useful for terminal lessons. Keep its `include` directory
beside `zero.exe`. If a release has no IDE installer, build the IDE using the
[build instructions](../building.md); do not expect the compiler ZIP to contain it.

See the [installer guide](../../installer/README.md) for packaging and other hosts.

> **Checkpoint:** Confirm the selected compiler displays `--help` in a terminal.

---

[Previous](zasm-01-02-why-learn-assembly-today.md) · [Contents](index.md) · [Next](zasm-01-04-using-zero-ide-my-first-project.md)
