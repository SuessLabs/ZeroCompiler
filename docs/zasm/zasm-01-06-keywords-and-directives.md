---
layout: default
title: "Zero Assembly Keywords and Directives"
---

# Zero Assembly Keywords and Directives

Section 1 — Intro · Chapter 1.6

[Previous](zasm-01-05-hello-zero-assembly.md) · [Contents](index.md) · [Next](zasm-02-01-moving-numbers.md)

Some words in a ZASM file tell the compiler how to build the program. These
keywords and directives serve a different role from CPU instructions such as
`MOV` or `CALL`: the compiler processes them while building the executable.

## Select the Target with #TARGET

`#TARGET` selects the platform and architecture for the generated program.
Write it as a standalone statement, conventionally near the top of the source.
The currently implemented choices also determine the executable format:

| Directive | Platform | CPU architecture | Output |
| --- | --- | --- | --- |
| `#TARGET windows-x86` | Windows | 32-bit x86 | PE32 executable (`.exe`) |
| `#TARGET linux-x64` | Linux | 64-bit x86-64 | Static ELF64 executable |

If you omit the directive and the command-line target, Zero defaults to
`windows-x86`. The target describes the program you are building, independently
of the computer running the compiler.

This complete Windows example makes the default choice explicit:

```asm
#TARGET windows-x86
#IMPORT kernel32.dll, ExitProcess

push 0
call [ExitProcess]
```

The corresponding minimal Linux x64 program uses the Linux system-call interface:

```asm
#TARGET linux-x64

mov eax, 60        ; Linux x64 exit system-call number.
xor edi, edi      ; Exit status 0.
syscall
```

Changing the target directive does not translate Windows API calls into Linux
system calls. The instructions, registers, and external interfaces must match
the chosen target.

You can also choose a target on the command line:

```text
zero main.zasm --target linux-x64
```

Use your compiler's full path if needed. If the source contains `#TARGET`, the
command-line choice must agree with it. Conflicting target declarations produce
an error; a command-line option does not override a different source target.
Cross-compiling is supported, but running the result requires a compatible host.

## Other Keywords and Directives You Will Meet

| Syntax | Purpose | Example |
| --- | --- | --- |
| `using` | Include shared source, commonly import declarations | `using "windows/console.zh"` |
| `#IMPORT` | Declare one or more exports from a Windows DLL | `#IMPORT kernel32.dll, Sleep, ExitProcess` |
| `DB` | Emit bytes or UTF-8 string bytes | `message db 'Hello', 0` |
| `DW` | Emit 16-bit integer values | `count: dw 10` |
| `DD` | Emit 32-bit integer values | `value: dd 42` |
| `DQ` | Emit numeric 64-bit values on Linux x64 | `value: dq 42` |

The data declarations in this table are fragments. Keep inline data behind a
jump so execution starts with an instruction. Data directives emit bytes;
`#TARGET` and `#IMPORT` configure compilation rather than emit CPU instructions.
`using` loads source that may itself contain declarations, instructions, or data.

## Future Output Configuration

The planned direction is to expand keywords and directives so source can
describe more aspects of the output. Keep these choices distinct:

| Configuration | What it describes | Current support and future direction |
| --- | --- | --- |
| Platform | Which operating system or environment runs the program | Windows and Linux today; additional environments are future work |
| Binary format | How code and loader metadata are packaged | PE32 and ELF64 through the implemented targets |
| CPU architecture and bitness | Which instructions, registers, and pointer widths apply | x86 (32-bit) and x64 (64-bit) today; ARM/ARM64 output is future work |
| Output kind | Whether the result is an application or shared library | Executables today; DLL output and Linux shared-library linking are future work |

PE and ELF are binary formats; ARM is a processor architecture. An EXE and a
DLL can both use PE, but they have different loading and entry-point contracts.
Those dimensions must form a supported combination to produce a working binary.

**Future syntax is not defined here.** There are no documented standalone
directives for independently selecting PE/ELF, ARM, 32/64-bit mode, or EXE/DLL
output today. Use the two working `#TARGET` choices above. Importing a DLL with
`#IMPORT` calls an existing library; it does not make your output a DLL.

See the [target design](../designs/Future-Formats-and-Targets.md),
[feature roadmap](ZASM-Coming-Soon.md), and
[compiler switches](zasm-05-02-compiler-switches.md) for related details.

> **Checkpoint:** Identify the platform, architecture, bitness, binary format,
> and output kind selected by `#TARGET windows-x86`. Explain why changing a
> filename extension to `.dll` does not change the compiled output kind.

---

[Previous](zasm-01-05-hello-zero-assembly.md) · [Contents](index.md) · [Next](zasm-02-01-moving-numbers.md)
