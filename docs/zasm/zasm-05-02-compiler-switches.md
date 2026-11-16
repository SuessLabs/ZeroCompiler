---
layout: default
title: "Compiler Switches"
---

# Compiler Switches

Section 5 — Compiler and Debugging · Chapter 5.2

[Previous](zasm-05-01-zero-cli-compiler.md) · [Contents](index.md) · [Next](zasm-05-03-zero-debugger.md)

Substitute your compiler executable's path for `zero` below.

| Argument | Purpose | Example |
| --- | --- | --- |
| `-h`, `--help` | Display usage | `zero --help` |
| `run` | Compile and execute; placed first | `zero run hello.zasm` |
| `-I <directory>` | Add a search path; repeatable | `zero main.zasm -I headers` |
| `--include <directory>` | Alternative to `-I` | `zero main.zasm --include headers` |
| `--target <target>` | `windows-x86` or `linux-x64` | `zero main.zasm --target linux-x64` |
| `--debug` | Emit a `.zdbg.json` source map | `zero main.zasm --debug` |
| Second positional path | Choose output filename | `zero main.zasm app.exe` |

Windows x86 is the default. Source can select `#TARGET linux-x64`; an explicit
command-line target must agree. Quote paths containing spaces.

```powershell
.\zero.exe main.zasm app.exe --debug -I "shared headers" -I headers
```

`run` is a command, not a switch. The output path is positional: there is no
`-o` option. Unknown switches and missing values are errors. The CLI has no
optimization-level or Debug/Release selector; IDE configurations separate
output folders, and debugging requests source maps.

See [command-line errors](ZASM-Reference.md#command-line-and-errors) for codes and
[Program.cs](../../source/Zero.Cli/Program.cs) when maintaining this table.

---

[Previous](zasm-05-01-zero-cli-compiler.md) · [Contents](index.md) · [Next](zasm-05-03-zero-debugger.md)
