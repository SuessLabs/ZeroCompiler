# Zero Compiler

The Zero compiler project (_AKA: Null Compiler_) is a custom programming language that aims to be simple, fast, and efficient.

The compiler supports PE (portable exectuable) file formats for GUI, CLI, and library binaries.

## About

The Zero programming language has its inceptions in 2001 and is aimed to be a simple, fast, and efficient programming language created by Damian Suess. At the time of writing, C/C++ and Visual Basic were the key programmang languages (besides, Java) for creating a compiled application. Marriaged with the C-like style, the simplicity of VB, and the heart of assembly, the Zero compiler language was concieved.

This is the next generation of the Zero Compiler Project.

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
    Formats/
      Pe/           PE writer, layout, imports, and binary structures
    Targets/
      X86/          x86 mnemonic and opcode definitions
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
| `.z`, `.zero` | Zero source code file (_`.zsrc`_) |
| `.zasm` | Zero assembly code file |
| `.zsrc` | Zero source code file |
| `.zinc` | Include definitions |
