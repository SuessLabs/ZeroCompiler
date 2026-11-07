# Zero Compiler

The Zero compiler project (_AKA: Null Compiler_) is a custom programming language that aims to be simple, fast, and efficient.

The compiler supports PE (portable exectuable) file formats for GUI, CLI, and library binaries.

## About

The Zero programming language has its inceptions in 2001 and is aimed to be a simple, fast, and efficient programming language created by Damian Suess. At the time of writing, C/C++ and Visual Basic were the key programmang languages (besides, Java) for creating a compiled application. Marriaged with the C-like style, the simplicity of VB, and the heart of assembly, the Zero compiler language was concieved.

This is the next generation of the Zero Compiler Project.

## Console assembly sample

From a PowerShell terminal in the repository root, build the compiler, compile
the Windows x86 assembly sample, and run the resulting executable:

```powershell
.\build.bat
.\output\Zero.Cli\Debug\zcc.exe docs\samples\hello-console.zasm output\hello-console.exe
.\output\hello-console.exe
```

The program prints `Hello from Zero!` followed by a newline and exits with code 0
(or code 1 if writing fails). It uses `#IMPORT` to call the Windows functions
`GetStdHandle`, `WriteFile`, and `ExitProcess`. The message is stored as numeric
ASCII bytes because `DB` currently accepts numeric operands. Run it from an
existing terminal to keep the output visible.

## File Types

The following file extensions are suggested for source code.

| Ext | Description |
|-|-|
| `.z`, `.zero` | Zero source code file (_`.zsrc`_) |
| `.zasm` | Zero assembly code file |
| `.zsrc` | Zero source code file |
| `.zinc` | Include definitions |
