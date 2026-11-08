---
layout: default
title: Zero Compiler
---

# Zero Compiler

A compiler for the Zero programming language and Zero Assembly.

Zero Assembly (**ZASM**) lets you write assembly instructions, define named data,
and call functions from Windows libraries. Start with a console program, then
explore the included Windows controls example.

[Download Zero](https://github.com/SuessLabs/ZeroCompiler/releases/latest) ·
[Getting started](#getting-started-with-zasm) ·
[View the source](https://github.com/SuessLabs/ZeroCompiler)

## Download and Install

1. Open the [latest release](https://github.com/SuessLabs/ZeroCompiler/releases/latest).
2. Under **Assets**, download the Windows x64 ZIP named
   `zero-v<version>-win-x64.zip`.
3. Extract the ZIP into a folder where you can save files.
4. Open PowerShell in that folder and check the compiler:

   ```powershell
   .\zero.exe --help
   ```

The release contains `zero.exe`, `zcc.exe`, and a `samples` folder. Both executable
names run the same compiler. There is no installer, and the released native
compiler does not require an installed .NET runtime.

The Windows x64 release runs on Windows x64. The current ZASM backend generates
32-bit Windows executables.

## Getting Started with ZASM

Create a plain-text file named `hello-zero.zasm` in the same folder as `zero.exe`.
Make sure your editor saves it with the `.zasm` extension rather than `.txt`.
Copy this program into the file:

```asm
; Print Hello Zero to the console.
#IMPORT msvcrt.dll, puts, exit

  jmp start               ; Skip the text stored below.

message db 'Hello Zero', 0 ; The zero marks the end of the string.

start:
  push message            ; Pass the text's address to puts.
  call [puts]             ; Print the text and add a newline.
  add esp, 4              ; Remove the argument from the stack.

  push 0                  ; Exit code 0 means success.
  call [exit]
```

Save the file, then compile and run it:

```powershell
.\zero.exe run hello-zero.zasm
```

The program prints:

```text
Hello Zero
```

The compiler also creates `hello-zero.exe` beside the source file. You can run
that executable directly afterward:

```powershell
.\hello-zero.exe
```

### How the example works

| Code | Purpose |
| --- | --- |
| `; comment` | Explains the code; the assembler ignores it. |
| `#IMPORT` | Declares functions to use from a Windows DLL. |
| `jmp start` | Jumps past the stored text to the instructions. |
| `message db 'Hello Zero', 0` | Names and stores the text, followed by an ending zero. |
| `start:` | Marks a location in the program. |
| `push message` | Places the text's address on the stack as a function argument. |
| `call [puts]` | Calls the imported printing function. The brackets read its stored address. |
| `add esp, 4` | Removes the one 32-bit argument after the call. |
| `push 0` and `call [exit]` | Finish the program successfully and flush console output. |

## Compile Without Running

Omit `run` to build an executable without launching it:

```powershell
.\zero.exe hello-zero.zasm
```

You can also choose the output filename:

```powershell
.\zero.exe hello-zero.zasm greeting.exe
```

## Explore the Samples

From the extracted release folder, try:

```powershell
# Named strings and console output
.\zero.exe run samples\hello-db-vars.zasm

# Join two strings with a space
.\zero.exe run samples\hello-concat.zasm

# A Windows window with a textbox, label, and button
.\zero.exe run samples\hello-win86-controls.zasm
```

See the [sample guide](https://github.com/SuessLabs/ZeroCompiler/blob/master/samples/readme.md)
for more examples, or read the
[string concatenation tutorial](https://github.com/SuessLabs/ZeroCompiler/blob/master/docs/tutorial-zasm-concat.md).

## Build from Source

Clone the repository and run `build.bat` from its root folder. This builds the
compiler, validates every ZASM sample, and publishes both executable names with
the samples into `publish`.

See the [build instructions](https://github.com/SuessLabs/ZeroCompiler/blob/master/docs/building.md)
for the required .NET SDK and C++ build tools.
