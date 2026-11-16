---
layout: default
title: "What Is ELF?"
---

# What Is ELF?

Section 6 — Architecture · Chapter 6.4

[Previous](zasm-06-03-what-is-windows-pe.md) · [Contents](index.md) · [Next](zasm-06-05-what-is-arm.md)

Executable and Linkable Format (ELF) is used by Linux and other systems for
executables and other binary artifacts. Its headers describe how a loader maps
a program and begins execution.

Zero produces **static Linux x64 ELF64 executables**, using Linux system calls
rather than a C runtime or shared-library linker. Select the target with
`#TARGET linux-x64` or `--target linux-x64`; both must agree if present.

Cross-compilation means host and target can differ. A Windows compiler can
build Linux output, but you run it on x64 Linux or compatible WSL. On Linux,
ensure execute permission before launching `./program`.

ELF alone does not specify an architecture or runtime interface. An ARM ELF
program would need an ARM encoder and platform conventions. Linux shared-library
linking and ARM backends remain future work.

See [Linux x64 and ELF64](../linux64.md) for implemented layout and syscall examples.

> **Checkpoint:** Explain why renaming `app.exe` to `app` cannot convert a Windows
> program into a Linux program.

---

[Previous](zasm-06-03-what-is-windows-pe.md) · [Contents](index.md) · [Next](zasm-06-05-what-is-arm.md)
