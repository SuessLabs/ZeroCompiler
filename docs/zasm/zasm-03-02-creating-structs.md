---
layout: default
title: "Creating Structs"
---

# Creating Structs

Section 3 — Level-Up Learning · Chapter 3.2

[Previous](zasm-03-01-calling-external-library-functions.md) · [Contents](index.md) · [Next](zasm-03-03-working-with-strings.md)

A structure is a layout agreement: fields occupy known offsets in contiguous
storage. ZASM does not yet provide `struct` declarations, typed fields, or
automatic layout. Use explicit storage and offsets.

For an x86 record containing a 32-bit count and a 32-bit pointer:

| Offset | Size | Meaning |
| --- | --- | --- |
| 0 | 4 bytes | Count |
| 4 | 4 bytes | Pointer |

```asm
; Assume EAX contains a valid pointer.
push eax          ; Last field first.
push 3            ; Count at the lowest address.
lea edx, [esp]    ; Address of the record.
mov ecx, [edx]    ; Count = 3.
mov eax, [edx + 4]
add esp, 8
```

The stack grows toward lower addresses, so pushing fields in reverse creates
the layout shown. OS structures require ABI-defined offsets and alignment;
changing pointer width can change their layout.

The [Windows controls sample](../../samples/Windows/win-ui-02-controls.zasm)
builds `WNDCLASSA` this way. Study its 40-byte layout and callback pointer before
the Notepad project. **Exercise:** Add a third 32-bit field on paper and derive
its offset and the total size.

---

[Previous](zasm-03-01-calling-external-library-functions.md) · [Contents](index.md) · [Next](zasm-03-03-working-with-strings.md)
