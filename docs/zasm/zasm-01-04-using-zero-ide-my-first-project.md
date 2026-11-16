---
layout: default
title: "Using Zero.IDE — My First Project"
---

# Using Zero.IDE — My First Project

Section 1 — Intro · Chapter 1.4

[Previous](zasm-01-03-installing-zero-ide.md) · [Contents](index.md) · [Next](zasm-01-05-hello-zero-assembly.md)

A project in these lessons is a folder containing a main `.zasm` file and optional
headers. A dedicated Zero project-file format is future work.

1. Create a writable folder named `HelloZero`.
2. Choose **File > Open Folder...** in Zero.IDE and select it.
3. Choose **File > New**, then **File > Save As...** to save `hello.zasm` there.
4. Enter the complete program from the next chapter and save it.
5. Choose **Build > Build** (`Ctrl+B`). Use **Build > Build Output...** to
   inspect diagnostics if compilation fails.
6. Choose **Build > Run Without Debugging** to launch normally. For console
   output that should stay visible, run the CLI from an existing terminal.
7. Press **F9** on an instruction to set a breakpoint, then **F5** to debug.
   The debugger initially pauses at entry. **F10** steps over an instruction
   or call; **Shift+F5** stops the session.

The IDE writes output under the source folder at
`.zeroide/<Debug|Release>/<source-file>/`. Use **Build > Build Output...** to find
the artifact. CLI default output locations are described later.

If a header is missing, check the compiler location and installed `include`
layout. Save modified headers before debugging. Source editors are read-only
during a debug session.

**Checkpoint:** Locate your source file, compiler executable, and generated
executable. Each has a different role.

---

[Previous](zasm-01-03-installing-zero-ide.md) · [Contents](index.md) · [Next](zasm-01-05-hello-zero-assembly.md)
