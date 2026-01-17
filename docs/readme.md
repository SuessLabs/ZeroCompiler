# Zero Compiler

Welcome to the Zero Compiler

Copyright 2026 Xeno Innovations, Inc.

## About

The Zero programming language has its inceptions in 2003 and is aimed to be a simple, fast, and efficient programming language created by Damian Suess. At the time of writing, C/C++, Visual Basic, and Java were the key programmang languages for creating a compiled application. Marriaged with the C-like style, the simplicity of VB, and the heart of assembly, the Zero compiler language was concieved.

This repository is a rewrite of the engine based on the original designs.

## Features

Future iterations will be self-hosted instead of written in C# and is designed to be cross-platform.

## Samples

### Console Application

Note, the defining of the type of application and there are no private/public protections

```txt
// Type of application to build
#zero PE CLI;   // 32-bit Windows Command Line Interface

include "Windows.zh";
include "Windows-Cli.zh";

// Specify the section name space for the file
namespace MyApp;

int Main()
{
  Console.WriteLine("Hello");
}
```

### Windows Application

```txt
// Type of application to build
#zero PE GUI;
#zero entry main

// Note: We're going to manually import
// include "Windows/kernel32.zh";
// include "Windows/user32.zh";

// Custom compiler directive
#define user32 = "USER32.DLL;

// Import Libraries References and assign alias
#importlib "KERNEL32.DLL", ExitProcess as ExitProcess;
#importlib user32, "MessageBeep", "MessageBeep";
#importlib user32, "MessageBoxA", "MessageBox";

namespace MyApp;

int Main()
{
  Console.WriteLine("Hello");
}
```

### Library Application

```txt
// Type of application to build
#zero PE;   // 32-bit Windows Library

include "Windows.zh";

namespace MyLib;

// Defines functions to export.
// When defined more than once or not implemented, it will be ignored.
export Add;
export Add, Subtract, NotImplemented;

int Add(dword a, dword b)
{
  return a + b;
}

int Subtract(dword a, dword b)
{
  return a - b;
}
```

## Syntax

### Define type of application

```txt
#zero PE CLI;   // 32-bit Windows Command Line Interface
#zero PE64 CLI; // 64-bit Windows Command Line Interface
#zero PE GUI;   // 32-bit Windows GUI
```

### Data Types

| Type | Alt | Description |
| - | - | - |
| bit       | bool  | Single bit, false/true, 0-1 |
| nib       |       | 4 bits, (0 - 15) |
| byte      |       | 1 byte, 8 bits, (0 - 255) |
| word      | short | 2 bytes, (0 - 65,535) |
| dword     | int   | Double Word, 32-bit, 4 bytes, (0 - 4,294,967,295) |
| qword     | int64 | Quad Word, 64-bit, 8 bytes, (0 - 18,446,744,073,709,551,615) |
| dqword    |       | Double Quad Word, 16 bytes |
| dqqword   |       | Quad Quad Word, 32 bytes |

### Structures

```txt
struct Point
{
    dword X;
    dword Y;
}
```

## References
