---
layout: default
title: "Moving Numbers"
---

# Moving Numbers

Section 2 — Basics · Chapter 2.1

[Previous](zasm-01-06-keywords-and-directives.md) · [Contents](index.md) · [Next](zasm-02-02-performing-jumps.md)

A number written in an instruction is an immediate value. `42`, `0x2A`, and
`2Ah` are spellings of the same integer.

```asm
mov eax, 42
mov ebx, 0x2A
push eax
pop ecx
```

EAX, EBX, and ECX now hold 42. Windows x86 supports immediate loads and memory
loads with `MOV`, but not register-to-register `MOV`. The balanced `push`/`pop`
pair copies through the stack. It temporarily changes ESP; do not insert
unrelated stack operations between the pair.

A register holds bits. Whether those bits represent a signed integer, an
unsigned integer, or an address depends on the operation using them. On this
target the general-purpose registers are 32 bits wide.

> **Exercise:** Load 100 into EAX, copy it into EDX using the stack, and inspect
> both registers. Linux x64 has broader `MOV` forms; see [Linux x64](../linux64.md).

---

[Previous](zasm-01-06-keywords-and-directives.md) · [Contents](index.md) · [Next](zasm-02-02-performing-jumps.md)
