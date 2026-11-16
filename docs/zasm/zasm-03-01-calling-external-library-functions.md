---
layout: default
title: "Calling External Library Functions"
---

# Calling External Library Functions

Section 3 — Level-Up Learning · Chapter 3.1

[Previous](zasm-02-05-basic-variables.md) · [Contents](index.md) · [Next](zasm-03-02-creating-structs.md)

On Windows x86, `#IMPORT` declares DLL exports. Shipped `.zh` headers group
these declarations, so programs usually include a header instead.

```asm
#IMPORT kernel32.dll, ExitProcess
push 0
call [ExitProcess]
```

This is a complete minimal program. Brackets mean an indirect call through the
import slot populated by the Windows loader. Preserve export spelling.

| Convention | Argument order | Argument cleanup | Examples |
| --- | --- | --- | --- |
| cdecl | Push right to left | Caller | `puts`, `printf`, `strcpy` |
| stdcall | Push right to left | Callee | `MessageBoxA`, `WriteFile` |

Do not remove a stdcall function's arguments a second time with `add esp, ...`.
Independently reserved local buffers still need their own cleanup. EAX commonly
carries the result; EAX, ECX, and EDX are volatile in these conventions. Preserve
EBX, ESI, EDI, and EBP in procedures that change them.

An import declaration does not validate argument types, capacity, or callbacks.
Read the API contract before adding a call. Linux's current backend uses system
calls rather than imported Windows DLLs.

**Exercise:** Find caller cleanup in the hello program and callee cleanup in the
[MessageBox sample](../../samples/Windows/win-ui-01-msgbox.zasm).

---

[Previous](zasm-02-05-basic-variables.md) · [Contents](index.md) · [Next](zasm-03-02-creating-structs.md)
