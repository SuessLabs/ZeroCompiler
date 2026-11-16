---
layout: default
title: "What Is ARM?"
---

# What Is ARM?

Section 6 — Architecture · Chapter 6.5

[Previous](zasm-06-04-what-is-elf.md) · [Contents](index.md) · [Next](zasm-07-01-mnemonic-lookup.md)

ARM is a family of processor architectures with its own instruction sets and
register conventions. AArch64 is the 64-bit execution architecture commonly
associated with ARM64. Its instructions are not x86 instructions with new names.

**Zero has no ARM output backend today.** An ARM64 host package, where available,
describes where the compiler or IDE runs; it does not establish ARM output.
Do not pass invented ARM target names or substitute ARM registers in this book.

A port needs encoding, relocations, registers and calling conventions, binary
packaging, and target-specific tests. Bare-metal boards also require startup
and memory-layout decisions beyond those of an OS application.

This chapter reserves a place for ARM while the working paths remain Windows
x86 and Linux x64. See [future formats and targets](../designs/Future-Formats-and-Targets.md).

> **Exercise:** Separate the algorithm in a hello program from its CPU-dependent
> and OS-dependent operations. Those boundaries define much of the porting work.

---

[Previous](zasm-06-04-what-is-elf.md) · [Contents](index.md) · [Next](zasm-07-01-mnemonic-lookup.md)
