# Proof of Concept

> **Coming Soon: higher-level Zero.** This page explores proposed `.zsrc`, `.zero`
> syntax; it is not a runnable example for the current compiler.
> See the [documentation home](../index.md) and [working ZASM examples](../zasm-examples.md).

In the next generation of Zero, we are re-aligning with the original design specifications and proof-of-concept.

## Zero Source (ZSRC)

The proof-of-concept of the early passes of the Zero language. In its early form, it has a resemblance to C and C#, where it aims to not use pointers

Provides the ability to define the platform and target architecture

```csharp
// Zero v0.2.0-pre
// Demonstrates high-level syntax

// Compiler options
option Platform(WinPE); // Specify the target platform
option Target(x64);     // Target or Architecture ("Arch")

// Library header inclusion
using "Windows.zh";     // '.zh' denotes, Zero Header.

// Main entry point
int main()
{
  uint8 a = 1;
  word  b = 2;  // Word and uint8 are linked types (same thing)
  word  c = a + b;

  if (a > b)
    Console.Print()

  // Concatnation of strings and numeric values
  string msg = "Addition (a + b): " + c + "\n";
  Console.Print(msg);

  return 0;
}
```

## Zero Assembly (ZASM)

### Header Directives

```sh
// OS Target
#target linux-64            # Linux-x64 (ELF-x64)

// Import library and function
#IMPORT library.dll, getaddrinfo
#IMPORT libc.so.6, function1, function2

// Set target type, platform, file output type
#format PE64 console        # Win-x64 EXE (PE-x64)
#format PE64 console DLL    # Win-x64 DLL (PE-x64)
```

### Raw

Ideal for copy/pasting in from other assemblers.

```csharp
#format PE64 console

using "windows/console.zh";
using "win32.zh";

;// #IMPORT CustomLibrary.dll, AddNumbers
;// import("CustomLibrary.dll", dword, "AddNumbers", dword a, dword b);
;// import "CustomLibrary.dll": dword AddNumbers(dword a, dword b);
import "CustomLibrary.dll", dword AddNumbers(dword a, dword b);

section .data (readable, writable)
  greeting db 'Hi!', 0x0A   ; 'H', 'i', '!', and a newline character (4 bytes)

section .text
  global _start

_start:
  mov rax, 1                ; sys_write system call
  mov rdi, 1                ; file descriptor 1 (stdout)
  mov rsi, greeting         ; pass the pointer to our DB string
  mov rdx, 4                ; the explicit length of our string
  syscall                   ; execute

  mov rax, 60               ; sys_exit system call
  xor rdi, rdi              ; return code 0
  syscall
```

### Zero's C-Like Assembly

Zero's C-Like assemblly syntax is intended to catch line errors and accidental section misplacements by use of curley braces `{ }` and semicolons `;`.

```csharp
using "windows/console.zh"

section .data
{
  greeting db 'Hi!', 0x0A;  // 'H', 'i', '!', and a newline character (4 bytes)
}

section .text (readable, executable)
{
  global _start
}

_start:
{
  mov rax, 1;               // sys_write system call
  mov rdi, 1;               // file descriptor 1 (stdout)
  mov rsi, greeting;        // pass the pointer to our DB string
  mov rdx, 4;               // the explicit length of our string
  syscall;                  // execute

  mov rax, 60;              // sys_exit system call
  xor rdi, rdi;             // return code 0
  syscall;
}
```
