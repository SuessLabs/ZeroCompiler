---
layout: default
title: "Storing Data and Printing"
---

# Storing Data and Printing

Section 2 — Basics · Chapter 2.3

[Previous](zasm-02-02-performing-jumps.md) · [Contents](index.md) · [Next](zasm-02-04-boolean-algebra.md)

`DB` emits bytes and UTF-8 strings, `DW` emits 16-bit integers, and `DD` emits
32-bit integers.

```asm
  jmp start
format db 'Value: %d', 0
value: dd 42
start:
  mov eax, [value]
  push eax
  push format
  call [printf]
  add esp, 8
```

This fragment requires `using "windows/console.zh"` before the jump and an
explicit process exit afterward. `printf` receives arguments right to left:
the value first, then the format address. Two 32-bit arguments occupy eight
bytes, which the caller removes. For a Windows line ending, append bytes
`13, 10` before the format's ending zero.

`value` means an address; `[value]` reads its contents. Do not confuse pointers
and numbers when passing arguments.

Inline data is in the read-only code section. `value: dd 42` does not make a
writable variable. Use stack or allocated storage for values that change.

> **Exercise:** Change the initial value and predict the output. Compare the
> [complete output sample](../../samples/Windows/win-cli-02-console-printf-db-vars.zasm).

---

[Previous](zasm-02-02-performing-jumps.md) · [Contents](index.md) · [Next](zasm-02-04-boolean-algebra.md)
