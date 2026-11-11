# Zero Compiler

![logo](docs/images/zero-logo.png)

The Zero programming language and compiler project (_AKA: Null Compiler_). **Zero** is a simple, fast, and efficient programming language for the x86-64 architecture.

The compiler generates Windows x86 PE32 console executables (including programs
that create Windows GUI controls) and static Linux x86-64 ELF64 executables using
syscalls. Shared-library ELF linking, DLL output, additional architectures, macOS,
and STM32 remain future work. See [Linux x86-64 support](docs/linux64.md).

## About

Zero has its inceptions in 2001 and matured 2006 as a simple, fast, and efficient programming language created by Damian Suess (_Xeno Innovations, inc._). At the time, C/C++ and Visual Basic were the key programmang languages (_besides Java or Pascal_) for creating a compiled business/hobby application. Zero has the heart of assembly language giving it that bare-metal kick.

### Zero Next-Generation

This is the next generation of the Zero Compiler Project, being rewritting from the ground up.

Yes, the heart language and engine are being rewritten for a more modern approach. Offering the capabilities of writing low-level Assembly (`ZASM`) and Zero's C-like style (`ZSRC`). The object-oriented functionality is also coming down the pipeline, to give developers the added boost.

## Supported Architectures

| Supported | Target | Architecture | Executable format | Platform conventions |
|-|-|-|-|-|
| &#9989; | Windows x86   | x86     | PE32  | Windows APIs and calling conventions |
| &#9744; | Windows ARM64 | ARM64   | PE32+ | Windows ARM64 ABI |
| &#9989; | Linux x64     | x86-64  | ELF64 | Static executables and Linux system calls |
| &#9744; | Linux ARM64   | ARM64   | ELF64 | Linux ARM64 ABI |
| &#9744; | macOS ARM64   | ARM64   | Mach-O | macOS ABI and loader |
| &#9744; | STM32         | ARM     | ELF   | Bare-metal startup and board memory map |

**References:**

* [PE Format Reference](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#import-address-table)
  * [File Headers](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#file-headers)
  * [Section Table/Headers](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#section-table-section-headers)
  * [Special Sections](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#special-sections)
  * [Import Address Table](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#import-address-table)

## Project layout

The solution keeps the compiler library, command-line application, and tests in
three projects. The library groups existing implementation files by responsibility:

```text
source/
  Zero/
    Assembly/       Assembler, parsed lines, directives, and import requests
    Core/           Shared utility functions
    Diagnostics/    Error codes and assembler exceptions
    Parsing/        Quote-aware operand and string parsing
    Compilation/    Target selection and instruction encoder contract
    Formats/
      Pe/           PE writer, layout, imports, and binary structures
      Elf/          Static Linux ELF64 writer
    Targets/
      X86/          x86 mnemonic and opcode definitions
      X64/          AMD64 instruction encoder
    Zero.cs         Public compiler entry point
  Zero.Cli/         Arguments, console output, and program execution
  Zero.Tests/       Compiler and CLI tests
```

Public namespaces remain unchanged for compatibility. This is an organizational
step: `Assembler` still coordinates parsing, x86 encoding, and PE output. Future
work can extract shared parsing and target-specific encoding into separate
components. New formats and architectures can be added under `Formats` and
`Targets` when their implementations begin.

## Contributing

* [Building Zero](docs/building.md)
  * Windows, run: `.\build.bat`

## Console assembly sample

From a PowerShell terminal in the repository root, build the compiler, compile
the Windows x86 assembly sample, and run the resulting executable:

```powershell
# Compile Zero Assembly (ZASM)
./build.bat

# From the publish folder:
./zcc.exe run samples/hello-console.zasm
```

Use `zcc run <source> [output.exe]` to compile and execute, or omit `run` to
compile only. The generated executable defaults to the source path with an
`.exe` extension. The run command waits for the program and returns its exit code.
The build publishes both `zcc.exe` and `zero.exe`; either name accepts the same
commands, for example `zero run samples/hello-console.zasm`.

## File Types

The following file extensions are suggested for source code.

| Ext | Description |
|-|-|
| `.zsrc`, `.z`, `.zero` | Zero source code file |
| `.zasm`   | Zero assembly code file |
| `.zinc`   | Include definitions (_alt: `zh`_) |
| `.zproj`  | Project files |
