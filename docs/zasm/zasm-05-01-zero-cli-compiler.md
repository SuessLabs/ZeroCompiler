---
layout: default
title: "Zero Compiler (Zero.CLI)"
---

# Zero Compiler (Zero.CLI)

Section 5 — Compiler and Debugging · Chapter 5.1

[Previous](zasm-04-05-notepad-testing-and-next-steps.md) · [Contents](index.md) · [Next](zasm-05-02-compiler-switches.md)

The `Zero.Cli` project produces `zero` (`zero.exe` on Windows). It accepts a
main `.zasm` source and an optional output path:

```text
zero <source> [output] [options]
zero run <source> [output] [options]
```

Use the executable's full path if it is not on PATH. From a portable release
folder containing `zero.exe` and `hello.zasm`:

```powershell
.\zero.exe hello.zasm
.\zero.exe hello.zasm greeting.exe
.\zero.exe run hello.zasm
```

Without an output path, Windows output uses `.exe` beside the source; Linux
output has no extension. `run` compiles, launches on a compatible host, waits,
and returns the program's exit code. Compilation failure prevents an older
executable from launching. Run Linux output on Linux or compatible WSL.

The loader resolves includes, then the backend emits instructions and packages
the executable. Diagnostics identify source filenames and lines, including
headers. Begin troubleshooting at the first error.

This chapter covers the assembly workflow. The future standalone
[Zero and Zero.CLI guide](../zero/index.md) has its own reserved book structure.

---

[Previous](zasm-04-05-notepad-testing-and-next-steps.md) · [Contents](index.md) · [Next](zasm-05-02-compiler-switches.md)
