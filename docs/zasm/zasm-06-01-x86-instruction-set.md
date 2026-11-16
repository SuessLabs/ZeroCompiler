---
layout: default
title: "The x86 Instruction Set"
---

# The x86 Instruction Set

Section 6 — Architecture · Chapter 6.1

[Previous](zasm-05-03-zero-debugger.md) · [Contents](index.md) · [Next](zasm-06-02-x64-instruction-set.md)

An instruction set defines operations and machine encodings a processor can
execute. Assembly mnemonics are readable names for operations. An assembler must
implement each operand form before you can use it.

Zero's Windows backend emits 32-bit x86 and PE32 executables. Its general-purpose
registers are EAX, EBX, ECX, EDX, ESI, EDI, EBP, and ESP. Execution also involves
EIP and status flags such as the zero flag.

```asm
mov eax, 5
cmp eax, 5
jz equal
```

This fragment loads, compares, and branches. Supply the `equal` label and
termination path in a complete program. Understanding its data and control flow
matters more initially than memorizing bytes.

ZASM implements a subset of x86. CPU support does not establish compiler support;
a supported mnemonic may still accept only some operands. Use
[Appendix I](zasm-07-01-mnemonic-lookup.md) with the
[operand reference](ZASM-Reference.md#instruction-reference).

Architecture, executable format, and OS calling convention are separate choices.
A Windows import call is not a Linux system call.

---

[Previous](zasm-05-03-zero-debugger.md) · [Contents](index.md) · [Next](zasm-06-02-x64-instruction-set.md)
