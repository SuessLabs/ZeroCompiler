---
layout: default
title: "Working with Strings"
---

# Working with Strings

Section 3 — Level-Up Learning · Chapter 3.3

[Previous](zasm-03-02-creating-structs.md) · [Contents](index.md) · [Next](zasm-03-04-organizing-your-system.md)

`DB` can encode text with an explicit zero terminator:

```asm
greeting db 'Hello', 0
newline db 13, 10, 0
```

These are data fragments; place them behind a jump. A C-style string ends at
its first zero byte. Its length excludes that terminator; capacity includes it.

To concatenate text, use writable destination storage. The complete
[string concatenation sample](../../samples/Windows/win-cli-05-string-concat.zasm)
reserves 64 stack bytes and calls `strcpy` and `strcat` to build
`Hello Zero Assembly!`. It needs 21 bytes including the ending zero. These
functions do not know the destination capacity: prove the bound before calling
them. Do not copy unbounded user input with this pattern.

ZASM literals emit UTF-8 bytes. Windows `A` APIs interpret bytes using an ANSI
code page; `W` APIs require UTF-16. ASCII is a common starting point; arbitrary
UTF-8 is not automatically compatible with either API family. Choose an encoding
and convert deliberately for a larger editor.

> **Exercise:** Count bytes for two ASCII strings, a space, and an ending zero.
> Compare the [ASCII appendix](zasm-07-02-ascii-character-set.md).

---

[Previous](zasm-03-02-creating-structs.md) · [Contents](index.md) · [Next](zasm-03-04-organizing-your-system.md)
