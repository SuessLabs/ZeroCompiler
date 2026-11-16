---
layout: default
title: "Creating Structs"
---

# Creating Structs

Section 3 — Level-Up Learning · Chapter 3.2

[Previous](zasm-03-01-importing-and-calling-external-library.md) · [Contents](index.md) · [Next](zasm-03-03-working-with-strings.md)

A structure is a layout agreement: fields occupy known offsets in contiguous
storage. ZASM does not yet provide `struct` declarations, typed fields, or
automatic layout. Use explicit storage and offsets.

## A Point in C/C++

A `Point` groups two coordinates, `x` and `y`, into one value. On the Windows
x86 target used in this book, each C/C++ `int` occupies four bytes.

```cpp
struct Point
{
  int x;
  int y;
};

struct Point point = { 10, 20 };
```

The compiler calculates the field offsets and storage size. Access the
coordinates with `point.x` and `point.y`.

## The Same Point in C#

C# `int` is a signed 32-bit integer. Sequential layout keeps these fields in
declaration order for the corresponding native layout.

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
struct Point
{
  public int x;
  public int y;
}
```

Inside a method, create the same value:

```csharp
Point point = new Point { x = 10, y = 20 };
```

## The Same Point in Zero Assembly

For these two 32-bit fields, the layout is eight bytes in total:

| Offset | Size | Meaning |
| --- | --- | --- |
| 0 | 4 bytes | `x` = 10 |
| 4 | 4 bytes | `y` = 20 |

ZASM uses explicit storage rather than a `struct` declaration. This Windows
x86 fragment constructs the same Point on the writable process stack:

```asm
push 20            ; point.y: push the last field first.
push 10            ; point.x: placed at the lowest address.
lea edx, [esp]     ; EDX = address of the Point.
mov ecx, [edx]     ; ECX = point.x (10), at offset 0.
mov eax, [edx + 4] ; EAX = point.y (20), at offset 4.
add esp, 8         ; Release the Point's eight bytes of stack storage.
```

The stack grows toward lower addresses, so pushing fields in reverse creates
the layout shown. `[edx]` corresponds to `point.x`; `[edx + 4]` corresponds to
`point.y`. EDX no longer points to live Point storage after `add esp, 8`.

Unlike the C/C++ and C# examples, ZASM does not associate a type or field names
with this address. You must keep offsets, sizes, and storage lifetime consistent.
Direct memory-store instructions are not implemented for Windows x86, so this
example initializes the writable fields with `PUSH`.

OS structures require ABI-defined offsets and alignment. Adding pointers or
changing targets can change a structure's layout; derive it for the target
rather than assuming every field occupies four bytes.

The [Windows controls sample](../../samples/Windows/win-ui-02-controls.zasm)
builds `WNDCLASSA` this way. Study its 40-byte layout and callback pointer before
the Notepad project.

> **Exercise:** Extend Point with a third 32-bit coordinate, `z`. Derive its
> offset, the total size, and the order of pushes needed to construct it.

---

[Previous](zasm-03-01-importing-and-calling-external-library.md) · [Contents](index.md) · [Next](zasm-03-03-working-with-strings.md)
