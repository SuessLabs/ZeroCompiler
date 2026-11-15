# ZASM debugger

[Documentation home](../index.md) · [Architecture](architecture.md) ·
[Windows PE engine](winpe.md) · [Development and testing](testing.md)

Zero IDE can debug Windows x86 ZASM executables on a Windows x64 computer.
`Zero.Dbg` contains the platform-neutral session contract and source-map format.
`Zero.Dbg.WinPe` implements native Windows PE32/WOW64 process debugging. The name
identifies both its host API and executable format; it does not imply ELF support
on Windows or support for every Windows CPU architecture.

## Using the debugger

1. Build/update the Zero CLI and select it in **Tools > Options > Compiler**.
   Debugging requires a compiler that accepts `--debug`.
2. Open and save a `.zasm` program. Save modified headers and other open source
   tabs as well. The program must begin with an instruction; use a jump to skip
   any initial inline data.
3. Click the gutter to the left of the line numbers, or press **F9**, to toggle a
   source breakpoint. An outlined circle is pending/unbound; a filled circle
   identifies a line that maps to an instruction in the last debug build.
4. Press **F5** or click **Play** to build in Debug configuration and launch.
   The debugger initially stops at program entry. The yellow arrow/background
   marks the instruction that will execute next.
5. Press **F5** again to resume to a breakpoint or program exit. While running,
   the toolbar's Play button becomes **Pause**. Click it to interrupt execution.
6. Press **F10** while paused to execute one instruction, stepping over a `CALL`
   until control returns to its caller. **Shift+F5** stops the debuggee.

The bottom **Debugger — Registers** tool shows EAX, EBX, ECX, EDX, ESI, EDI, EBP,
ESP, EIP and EFLAGS as hexadecimal values, plus the active thread, source location,
stop reason and exit code. It can be resized and floated using the docking UI.
Registers are cleared while running so old values cannot be mistaken for current
process state. **Build > Run Without Debugging** retains ordinary execution.

Source editors are read-only during a session. Breakpoints can still be toggled.
New breakpoints set while running request a pause before modifying executable
memory. Breakpoints on comments, standalone labels, include directives and data
declarations remain unbound; they never patch data bytes.

## Current boundaries

- Windows x64 host, Windows x86 PE32 target. Other hosts can build the libraries
  and inspect maps, but native debugging of Linux/ELF, ARM and Windows x64 targets
  is not implemented.
- Start a new process; attaching to an existing process is not implemented.
- F10 is instruction stepping. A call is stepped over with a temporary return
  breakpoint. A user breakpoint inside that call still takes precedence.
- Pause may stop inside Windows runtime code. Registers remain available even
  when there is no matching ZASM source location; F5 resumes normally.
- The engine tracks threads, but the IDE currently exposes one stopped thread,
  with no thread selector, call-stack unwinder, memory editor, conditional
  breakpoints, watch expressions, Step Out, or edit-and-continue.
- Debug maps are custom `.zdbg.json` sidecars, not PDB/DWARF symbols. Rebuild after
  changing sources or moving a build to another source directory.
- Stopping or closing the IDE ends the process launched by this debug session.
  Programs launched normally are unaffected.
