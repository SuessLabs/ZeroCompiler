---
layout: default
title: "What Is Windows PE?"
---

# What Is Windows PE?

Section 6 — Architecture · Chapter 6.3

[Previous](zasm-06-02-x64-instruction-set.md) · [Contents](index.md) · [Next](zasm-06-04-what-is-elf.md)

Portable Executable (PE) is a Windows executable-file format. It packages code,
data, entry-point information, and loader metadata. The Windows loader maps
it into memory and resolves imported DLL functions.

Zero currently emits **PE32 for x86**. The import address table supplies slots
for calls such as `call [MessageBoxA]`. Source `#IMPORT` declarations identify
which exports to describe there.

Sections describe image regions and permissions. Inline ZASM data currently
shares a read-only executable section with instructions. A label on `DD` names
bytes; it does not request writable storage.

Output uses the console subsystem today. Windows APIs can create graphical
windows, but selecting a GUI subsystem without an accompanying console is
future work. Windows x64 PE32+ and DLL output are also future targets.

See the [Windows debugger engine](../debugger/winpe.md) and
[future target design](../designs/Future-Formats-and-Targets.md).
**Checkpoint:** Distinguish executable format, CPU architecture, and subsystem.

---

[Previous](zasm-06-02-x64-instruction-set.md) · [Contents](index.md) · [Next](zasm-06-04-what-is-elf.md)
