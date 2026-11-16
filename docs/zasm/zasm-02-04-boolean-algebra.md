---
layout: default
title: "Boolean Algebra"
---

# Boolean Algebra

Section 2 — Basics · Chapter 2.4

[Previous](zasm-02-03-storing-data-and-printing.md) · [Contents](index.md) · [Next](zasm-02-05-basic-variables.md)

Boolean operations act on individual bits:

| A | B | A AND B | A OR B | A XOR B |
| --- | --- | --- | --- | --- |
| 0 | 0 | 0 | 0 | 0 |
| 0 | 1 | 0 | 1 | 1 |
| 1 | 0 | 0 | 1 | 1 |
| 1 | 1 | 1 | 1 | 0 |

```asm
mov eax, 0x0A     ; 1010 in binary.
and eax, 0x0C     ; EAX = 8.
or eax, 1        ; EAX = 9.
xor eax, 3       ; EAX = 10.
test eax, 2      ; Test without modifying EAX.
```

`TEST` sets flags from AND but leaves its operands unchanged. `AND`, `OR`, and
`XOR` store the result and update flags. After `test eax, 2`, `JZ` would branch
if that bit were clear. Supply the branch target in your complete program.

Use hexadecimal masks: binary literal syntax is not currently supported.

**Exercise:** Work out `0x0F AND 0x05` on paper, then verify it in EAX.

---

[Previous](zasm-02-03-storing-data-and-printing.md) · [Contents](index.md) · [Next](zasm-02-05-basic-variables.md)
