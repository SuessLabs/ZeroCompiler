---
layout: default
title: "Creating Functions"
---

# Creating Functions

Section 3 — Level-Up Learning · Chapter 3.7

[Previous](zasm-03-06-performing-bit-manipulations.md) · [Contents](index.md) · [Next](zasm-04-01-notepad-project-setup.md)

A local function is a label reached with `CALL` and left with `RET`. Agree on
inputs, outputs, and preserved registers before writing its body.

```asm
#IMPORT kernel32.dll, ExitProcess

  mov eax, 40
  call add_two
  push eax
  call [ExitProcess]

; Input: EAX. Output: EAX + 2. Changes EAX and arithmetic flags.
add_two:
  add eax, 2
  ret
```

This complete Windows x86 program exits with code 42. `CALL` pushes the return
address; `RET` pops it and resumes the caller. Process termination prevents
execution from falling through into the function a second time.

At entry to an x86 stack-argument function, `[esp]` is the return address and
`[esp + 4]` is the first argument. Saving registers changes those offsets.
The controls sample uses `push ebp` and `lea ebp, [esp]` for stable offsets.

A plain `ret` leaves arguments for the caller. `ret 16` also discards sixteen
argument bytes, as required by the four-argument stdcall window callback. Match
the caller's and callee's cleanup conventions.

**Exercise:** Make `add_two` preserve EBX if you change it to use that register.
Balance pushes and pops on every return path.

---

[Previous](zasm-03-06-performing-bit-manipulations.md) · [Contents](index.md) · [Next](zasm-04-01-notepad-project-setup.md)
