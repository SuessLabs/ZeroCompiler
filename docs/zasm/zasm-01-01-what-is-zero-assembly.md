---
layout: default
title: "What Is Zero Assembly?"
---

# What Is Zero Assembly?

Section 1 — Intro · Chapter 1.1

[Library](../index.md) · [Contents](index.md) · [Next](zasm-01-02-why-learn-assembly-today.md)

Zero Assembly (ZASM) is the assembly language accepted by the Zero compiler.
You describe instructions, data, and operating-system calls in a text file;
Zero turns them into a native executable.

```asm
mov eax, 42
add eax, 8
```

This fragment puts 42 in EAX, then adds 8. A register is a small storage location
in the processor. The destination comes first: `add eax, 8` changes EAX.

ZASM currently targets Windows x86 PE32 and static Linux x64 ELF64. Most lessons
use **Windows x86**, including the GUI project. Linux examples are labeled.
A Windows x64 compiler executable does not mean it produces Windows x64 programs.

Source files use `.zasm`; shared headers use `.zh`. The higher-level Zero
language uses `.zsrc` and remains under redesign. Future features are labeled
where they arise.

> **Checkpoint:** Explain the difference between source text, a compiler, and an
> executable. The [language reference](ZASM-Reference.md) supplies exact operand rules.

---

[Library](../index.md) · [Contents](index.md) · [Next](zasm-01-02-why-learn-assembly-today.md)
