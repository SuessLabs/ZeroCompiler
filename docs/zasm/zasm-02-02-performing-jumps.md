---
layout: default
title: "Performing Jumps"
---

# Performing Jumps

Section 2 — Basics · Chapter 2.2

[Previous](zasm-02-01-moving-numbers.md) · [Contents](index.md) · [Next](zasm-02-03-storing-data-and-printing.md)

A jump changes which instruction executes next. `JMP` always branches. `JZ`
branches when the zero flag is set; `JNZ` branches when it is clear.

```asm
mov ecx, 3
again:
  sub ecx, 1
  cmp ecx, 0
  jnz again
```

The loop stops after three subtractions. `CMP` sets flags as if subtracting its
second operand from the first, without changing either operand. Equality sets
the zero flag. `SUB` also sets flags, but the explicit comparison makes the
condition easy to see.

Keep a branch near the instruction establishing its condition: intervening
arithmetic can replace those flags. Labels and forward references are resolved
by the compiler. Use unique labels for distinct locations.

Use `JZ` and `JNZ` normally. `JZ8` and `JNZ8` require a short displacement in
range. `JE`, `JNE`, and greater/less-than branches are not implemented.

> **Exercise:** Start the loop at 5. Step through and watch ECX and EIP.

---

[Previous](zasm-02-01-moving-numbers.md) · [Contents](index.md) · [Next](zasm-02-03-storing-data-and-printing.md)
