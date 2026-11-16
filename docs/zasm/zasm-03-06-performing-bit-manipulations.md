---
layout: default
title: "Performing Bit Manipulations"
---

# Performing Bit Manipulations

Section 3 — Level-Up Learning · Chapter 3.6

[Previous](zasm-03-05-arithmetic-operations.md) · [Contents](index.md) · [Next](zasm-03-07-creating-functions.md)

A mask identifies bit positions in a value. For a 32-bit flags value in EAX:

```asm
or eax, 0x04          ; Set bit 2, counting from bit 0.
and eax, 0xFFFFFFFB   ; Clear bit 2; keep the other bits.
xor eax, 0x04         ; Toggle bit 2.
test eax, 0x04        ; Set ZF according to whether bit 2 is clear.
```

Setting an already-set bit leaves it set. Clearing an already-clear bit leaves
it clear. Toggling twice restores the original value. Testing changes flags
without changing the value.

Match masks to register width. The clear mask above is the 32-bit complement
of `0x04`. Linux 64-bit destinations have different immediate encoding
constraints; consult that target's operand rules before porting masks.

Shifts, rotates, `NOT`, and `BT`/`BTS`/`BTR` are not implemented. These concepts
can be added when the encoders support them. For now use AND/OR/XOR/TEST.

**Exercise:** From zero, set bits 0 and 3, clear bit 0, and predict EAX before
inspecting it in the debugger.

---

[Previous](zasm-03-05-arithmetic-operations.md) · [Contents](index.md) · [Next](zasm-03-07-creating-functions.md)
