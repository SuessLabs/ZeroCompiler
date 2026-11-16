---
layout: default
title: "The x64 Instruction Set"
---

# The x64 Instruction Set

Section 6 — Architecture · Chapter 6.2

[Previous](zasm-06-01-x86-instruction-set.md) · [Contents](index.md) · [Next](zasm-06-03-what-is-windows-pe.md)

x64, also called x86-64 or AMD64, extends x86 with 64-bit registers and additional
general-purpose registers. A 64-bit pointer and ABI differ from x86: porting
requires more than renaming EAX to RAX.

Zero currently uses x64 for **Linux ELF64**. Windows x64 output is future work.
This complete Linux-only example exits successfully:

```asm
#TARGET linux-x64
mov eax, 60
xor edi, edi
syscall
```

60 is the Linux x64 exit system-call number; RDI carries the status. Writing
EAX or EDI clears the upper half of its 64-bit register. Linux system calls
use registers for arguments; `SYSCALL` clobbers RCX and R11.

The Linux backend supports matching-width register moves, loads/stores, and
additional indirect-call forms. This does not make the entire AMD64 set
available. Inline data remains read-only even when store instructions encode.

See [Linux x64 support](../linux64.md) for examples and operand restrictions.

> **Exercise:** Compare this exit with Windows x86 `push 0` / `call [ExitProcess]`.

---

[Previous](zasm-06-01-x86-instruction-set.md) · [Contents](index.md) · [Next](zasm-06-03-what-is-windows-pe.md)
