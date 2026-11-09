---
layout: default
title: Zero Compiler
---

# Zero Compiler

Zero is a compiler project for the Zero programming language and **Zero Assembly
(ZASM)**. ZASM is the working assembly language today: write x86 instructions,
define data, share headers, and call Windows DLL functions to build an executable.
The higher-level Zero language is under design; see [Coming Soon](#coming-soon).

[Download Zero](https://github.com/SuessLabs/ZeroCompiler/releases/latest) ·
[Getting started](#getting-started-with-zasm) ·
[Language reference](zasm-reference.md) ·
[Examples](zasm-examples.md) ·
[Source code](https://github.com/SuessLabs/ZeroCompiler)

## What Zero Supports Today

| Feature | Current support |
| --- | --- |
| Assembly input | `.zasm` source files using Intel-style operand order. |
| Executable output | Windows x86 (32-bit) PE executables with the console subsystem. |
| Compiler commands | `zero.exe` and `zcc.exe` provide the same commands. |
| Compile and run | `run` builds a source file, executes it, and returns its exit code. |
| Native distribution | Self-contained Native AOT compiler; no .NET runtime needed to run it. |
| Instructions | Data movement, integer addition/subtraction, bitwise operations, comparisons, branches, calls, and returns. |
| Data | `DB`, `DW`, and `DD`; named UTF-8 byte strings with `DB`. |
| Symbols | Labels, forward references, and named data addresses. |
| Windows libraries | `#IMPORT` declares DLL functions; call them with `call [FunctionName]`. |
| Shared headers | `using "file.zh"`, nested includes, include-once behavior, and repeatable `-I` paths. |
| Diagnostics | Source filename and line number, including errors inside headers. |
| Samples | Console output, arithmetic, string concatenation, MessageBox, and Windows controls. |

The compiler's host architecture and its output architecture are separate. The
Windows x64 release runs on Windows x64 and generates **Windows x86** programs.
Windows APIs can create windows and controls, but the current output still uses
the console subsystem. ELF, ARM code generation, and native x64 output are future work.

## Download and Install

1. Open the [latest release](https://github.com/SuessLabs/ZeroCompiler/releases/latest).
2. Under **Assets**, download `zero-v<version>-win-x64.zip`.
3. Extract the ZIP into a folder where you can save files.
4. Open PowerShell in that folder and run:

   ```powershell
   .\zero.exe --help
   ```

Keep this layout when moving or installing the compiler:

```text
zero.exe
zcc.exe
include/
  console.zh
  windows.zh
samples/
  hello-using.zasm
  ...
```

Both executable names run the same compiler. There is no installer, and the native
release does not require .NET to be installed. Keep `include` beside the executables
so shared headers can be found from any working directory.

## Getting Started with ZASM

Save this as `hello-zero.zasm` in the extracted release folder. Make sure the
extension is `.zasm`, rather than `.txt`.

```asm
; Print a greeting using the shipped console header.
using "console.zh"

  jmp start               ; Skip the bytes stored below.

message db 'Hello Zero!', 0

start:
  push message            ; Pass the string's address.
  call [puts]             ; Print the string and a newline.
  add esp, 4              ; Remove the one 32-bit argument.

  push 0                  ; Return exit code 0.
  call [exit]
```

Compile and run it:

```powershell
.\zero.exe run hello-zero.zasm
```

Output:

```text
Hello Zero!
```

The compiler creates `hello-zero.exe` beside the source file. Run it again with
`.\hello-zero.exe`, or omit `run` to compile without executing.

### Read the Program

| Term | Meaning | Example |
| --- | --- | --- |
| Statement | A source line that declares something or requests an operation. | `using "console.zh"` |
| Mnemonic  | The short name of a CPU instruction. | `push`, `call`, `add` |
| Operand   | A value, register, address, or label used by an instruction. | `message` in `push message` |
| Register  | A small storage location inside the CPU. | `esp` is the stack pointer. |
| Assembler directive | Tells the compiler to define data or imports rather than execute an instruction. | `db`, `#IMPORT` |
| Label     | A name for a position in the program. | `start:` |
| Comment   | Explanatory text ignored by the compiler. | `; Print a greeting` |

`using` inserts the header's source. `console.zh` declares the DLL imports for
`puts`, `printf`, and `exit`. `jmp start` skips the inline string data. `puts`
prints a zero-terminated string and adds a newline. Its cdecl calling convention
requires the caller to remove the argument with `add esp, 4`.

## Command-Line Guide

```powershell
# Compile; output defaults to hello-zero.exe.
.\zero.exe hello-zero.zasm

# Choose an output file.
.\zero.exe hello-zero.zasm greeting.exe

# Compile and execute; wait for the program's exit code.
.\zero.exe run hello-zero.zasm

# Add header search directories in order.
.\zero.exe run hello-zero.zasm -I "headers" -I "C:\Libraries\Zero"

# zcc is another name for the same compiler.
.\zcc.exe run hello-zero.zasm
```

Use `-h` or `--help` for help. `--include` is an alternative to `-I`.
A compilation failure prevents `run` from executing an older output file.
See the [reference for command options and error codes](zasm-reference.md#command-line-and-errors).

## ZASM Language at a Glance

Use one instruction or directive per line. Operands are separated by commas;
when an instruction has a destination, it comes first. Mnemonics, register names,
and labels are case-insensitive. Preserve the spelling of DLL export names.
Semicolons start comments outside quoted strings; they are not required terminators.

### Mnemonics

| Group | Supported mnemonics | Example |
| --- | --- | --- |
| Data and addresses | `MOV`, `LEA` | `mov eax, 42` |
| Stack | `PUSH`, `POP` | `push eax` |
| Arithmetic | `ADD`, `SUB` | `add eax, 5` |
| Bitwise | `AND`, `OR`, `XOR` | `xor eax, eax` |
| Comparisons | `CMP`, `TEST` | `cmp eax, 42` |
| Branches | `JMP`, `JZ`, `JNZ`, `JZ8`, `JNZ8` | `jz done` |
| Procedures | `CALL`, `RET` | `call calculate` |
| Other | `NOP`, `INT` | `nop` |

Only the operand forms listed in the [instruction reference](zasm-reference.md#instruction-reference)
are supported. For example, `mov eax, 42` and `mov eax, [value]` work;
register-to-register `MOV` and memory stores are Coming Soon.

### Operands and Registers

| Operand | Example | Meaning |
| --- | --- | --- |
| Decimal integer | `42` | A constant value. |
| Hexadecimal integer | `0x2A` or `2Ah` | The same value written in base 16. |
| Register | `eax` | A CPU storage location. |
| Label or symbol | `message` | An address, or a branch target where required. |
| Memory | `[value]`, `[eax]`, `[esp + 4]` | Read memory or calculate an address, depending on the instruction. |
| Quoted string | `'Hello'` | Text accepted by `DB`, rather than an instruction operand. |

The current instruction forms use the 32-bit registers **EAX, EBX, ECX, EDX,
ESI, EDI, EBP, and ESP**. Other widths and SIMD registers are future work.
See [register roles and memory addressing](zasm-reference.md#registers).

### Directives, Labels, and Statements

| Syntax | Purpose | Example |
| --- | --- | --- |
| `DB` | Emit bytes or UTF-8 string bytes. | `message db 'Hello', 0` |
| `DW` | Emit 16-bit integer values. | `count: dw 42` |
| `DD` | Emit 32-bit integer values. | `value: dd 42` |
| `label:` | Name the current location. | `start: mov eax, 42` |
| `using "file.zh"` | Include another source file. | `using "console.zh"` |
| `#IMPORT dll, function` | Declare one or more imported functions. | `#IMPORT kernel32.dll, ExitProcess` |
| `; comment` | Add a comment or trailing explanation. | `ret ; Return to caller` |

Use a colon for labels before `DW` and `DD`; only `DB` also supports the
colon-free form shown above. Data currently lives alongside instructions in the
read-only code section. Start with a jump when inline data appears before your code.
The first emitted byte is the entry point; the name `start` has no special meaning.

Include search order is the current working directory, then each `-I` directory,
then `include` beside the compiler. Nested headers use the same search order.
See [Using included files](Zero-Assembly-Include-Files.md) for nested headers,
cycle detection, source locations, and library integration.

Read the [full ZASM reference](zasm-reference.md) for strings, labels, instruction
forms, imports, calling conventions, and current limitations.

## Examples and Tutorials

Start with the [complete basic examples](zasm-examples.md): console text, integer
addition with formatted output, and a Windows MessageBox. Each includes a command
and expected behavior.

From the release folder, also try:

```powershell
.\zero.exe run samples\hello-using.zasm
.\zero.exe run samples\hello-db-vars.zasm
.\zero.exe run samples\v002-integer-math.zasm
.\zero.exe run samples\hello-concat.zasm
.\zero.exe run samples\hello-win86-controls.zasm
```

- [String concatenation tutorial](tutorial-zasm-concat.md): a short walkthrough with a mnemonic table.
- [Sample guide](https://github.com/SuessLabs/ZeroCompiler/blob/master/samples/readme.md): console, arithmetic, strings, MessageBox, and controls.
- [Header guide](https://github.com/SuessLabs/ZeroCompiler/blob/master/include/readme.md): the shipped `console.zh` and `windows.zh` headers.
- [Assembly overview and opcodes](Zero-Assembly-Intro.md): a compact companion to the language reference.

## Build and Contribute

Clone the repository and run `.\build.bat` from its root. This builds the compiler,
validates every ZASM sample, and publishes `zero.exe`, `zcc.exe`, `include`, and
`samples` into `publish`.

- [Building Zero](building.md): prerequisites, AOT publishing, sample validation, and version settings.
- [Automatic versioning and releases](builds-AutoVersioning.md): GitHub Actions version increments, tags, and releases.
- [Project background and original language ideas](readme.md): history and proposed higher-level syntax.
- [Future formats and architectures](designs/Future-Formats-and-Targets.md): shared parsing with separate target backends.
- [ZSRC console proof of concept](designs/ZSRC-v0.3-POC-Console.md): a future high-level language example.
- [Assembly syntax sandbox](Zero-Assembly-PoC-Sandbox.md): experimental proposals, including directives that are not implemented.

The reference describes the current compiler. Historical examples and design
proposals are labeled separately so they can evolve without implying support today.

## Coming Soon

See the [ZASM feature roadmap](zasm-coming-soon.md) for implemented assembly
features, proposed key features, and nice-to-have improvements.

These are documentation placeholders and development ideas, with no promised release date.
They are not current compiler capabilities.

| Topic | Placeholder |
| --- | --- |
| Higher-level Zero / `.zsrc` | **Coming Soon:** specify grammar, types, expressions, functions, scopes, and a first working program. |
| More x86 instructions | **Coming Soon:** multiplication/division, additional conditions, and more data movement forms. |
| Additional register widths | **Coming Soon:** document validated 8-bit, 16-bit, 64-bit, and SIMD instructions when implemented. |
| Writable data and sections | **Coming Soon:** define data sections, buffers, alignment, and memory stores. |
| Macros and constants | **Coming Soon:** define reusable constants, expressions, macros, and conditional assembly. |
| Projects and libraries | **Coming Soon:** document `.zprj`, multi-file linking, objects, and exports. |
| More targets | **Coming Soon:** Windows x64, ARM/ARM64, ELF, and platform-specific calling conventions. |
| GUI output selection | **Coming Soon:** select a GUI subsystem without an accompanying console window. |
| Debugging | **Coming Soon:** debugger setup, symbols, source stepping, and troubleshooting walkthroughs. |
| More tutorials | **Coming Soon:** loops, reusable procedures, stack frames, and a step-by-step controls application. |

Contributors can replace these placeholders with tested syntax and examples as
features become available. See the [reference's limitations](zasm-reference.md#current-limitations)
before adapting assembly from another assembler.
