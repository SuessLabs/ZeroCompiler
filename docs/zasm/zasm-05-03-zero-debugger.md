---
layout: default
title: "Zero Debugger (Zero.DBG)"
---

# Zero Debugger (Zero.DBG)

Section 5 — Compiler and Debugging · Chapter 5.3

[Previous](zasm-05-02-compiler-switches.md) · [Contents](index.md) · [Next](zasm-06-01-x86-instruction-set.md)

Zero.DBG supplies the session contract; `Zero.Dbg.WinPe` implements Windows x86
process debugging on Windows x64. Use it through Zero.IDE. A standalone debugger
CLI and Linux debugging are not documented as supported.

1. Select a compiler accepting `--debug` under **Tools > Options > Compiler**.
2. Save source and modified headers. Set an instruction breakpoint with F9
   or the editor gutter.
3. Press F5. Startup builds with a source map and pauses at entry.
4. Inspect Registers in **Debugger — Locals**. The highlighted instruction
   executes next.
5. F10 executes an instruction, stepping over a call. F5 continues, Pause
   interrupts, and Shift+F5 stops the session.

A pending breakpoint has no bound instruction yet. Comments, data, and standalone
labels do not map to instructions. Pausing in OS code may show registers with
no matching ZASM source line.

The custom `.zdbg.json` sidecar is neither PDB nor DWARF. Rebuild after changing
or moving sources. Typed locals, watches, a memory editor, and a call-stack
unwinder are not currently exposed by the IDE.

See the [existing debugger guide](../debugger/index.md) for boundaries and the
[future Zero.DBG book](../dbg/index.md) for its reserved structure.

---

[Previous](zasm-05-02-compiler-switches.md) · [Contents](index.md) · [Next](zasm-06-01-x86-instruction-set.md)
