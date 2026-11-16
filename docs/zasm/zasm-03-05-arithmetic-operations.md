---
layout: default
title: "Arithmetic Operations"
---

# Arithmetic Operations

Section 3 — Level-Up Learning · Chapter 3.5

[Previous](zasm-03-04-organizing-your-system.md) · [Contents](index.md) · [Next](zasm-03-06-performing-bit-manipulations.md)

The implemented integer arithmetic begins with `ADD` and `SUB`:

```asm
mov eax, 20
add eax, 22       ; 42.
sub eax, 2        ; 40.
mov ebx, 10
add eax, ebx      ; 50.
```

Arithmetic changes its destination and flags. A 32-bit result retains the low
32 bits: adding 1 to `0xFFFFFFFF` yields zero. Decide whether wrapping is valid
for your program and choose checks appropriate to that decision.

For a small repeated-addition exercise, multiply 7 by 3:

```asm
mov eax, 0
mov ecx, 3
multiply_again:
  add eax, 7
  sub ecx, 1
  jnz multiply_again
```

This assumes a positive, nonzero count. Starting at zero would wrap on the first
subtraction and create a very long loop. General algorithms must handle zero
and other boundaries explicitly.

`MUL`, `IMUL`, `DIV`, `IDIV`, `INC`, and `DEC` are not implemented. Check the
[mnemonic inventory](ZASM-Mnemonics.md) before adapting another assembler's code.

> **Exercise:** Print the result using the earlier `printf` pattern.

---

[Previous](zasm-03-04-organizing-your-system.md) · [Contents](index.md) · [Next](zasm-03-06-performing-bit-manipulations.md)
