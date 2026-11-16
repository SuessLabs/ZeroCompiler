---
layout: default
title: "Basic Variables"
---

# Basic Variables

Section 2 — Basics · Chapter 2.5

[Previous](zasm-02-04-boolean-algebra.md) · [Contents](index.md) · [Next](zasm-03-01-importing-and-calling-external-library.md)

A variable is storage with an agreed meaning and lifetime. Registers provide
temporary storage. Named data declarations provide initial bytes, but current
inline data is read-only.

```asm
push 42
lea esi, [esp]     ; Address of a writable four-byte slot.
mov eax, [esi]     ; Read 42.
add esp, 4         ; Release it; ESI must no longer be used for that slot.
```

`LEA` calculates an address without reading memory. For an API output buffer,
reserve sufficient bytes with `sub esp, capacity` and pass its address. Release
the reservation only when all users of that storage have finished.

Windows x86 memory-store instructions are not implemented: do not write
`mov [value], eax`. Stack pushes and imported functions can populate writable
storage. Linux x64 supports register-to-memory stores, but its inline data is
still read-only. An instruction cannot override memory permissions.

Calls can overwrite temporary registers. Follow the calling convention and
preserve values needed afterward.

> **Exercise:** Explain why a pointer to
> released stack storage cannot serve as a lasting variable.

---

[Previous](zasm-02-04-boolean-algebra.md) · [Contents](index.md) · [Next](zasm-03-01-importing-and-calling-external-library.md)
