# Zero Assembly (ZASM) - Using Included Files

Use `using` to share imports, data, and procedures between ZASM files.
The compiler expands each header at the directive's position before assembly.

Syntax:

```txt
using "{filename.zh}"
```

### Sample

**include/console.zh**

```asm
#IMPORT msvcrt.dll, puts, exit
```

**main.zasm:**

```asm
using "console.zh"
; using "subfolder/myfile.zh"

  jmp start

message db 'Hello Zero!', 0

start:
  push message
  call [puts]
  add esp, 4

  push 0
  call [exit]
```

## Overview

Like C/C++ `#include`, `using` loads source text into the current program.
Each resolved file is included once per compilation, so shared dependencies
do not produce duplicate declarations. Circular includes are errors: the compiler
reports the chain, such as `a.zh -> b.zh -> a.zh`.

This is currently implemented for ZASM. Headers share the program's labels and
imports; they do not introduce a namespace. Keep declarations before code that
uses them for readability. Forward label references also work.

## The Order of Search

1. The terminal's current working directory.
2. Directories supplied with `-I` or `--include`, in command-line order.
3. The `include` directory beside `zero.exe` or `zcc.exe`.

For `using "subfolder/myfile.zh"`, the compiler checks that entire relative path
under each directory. Nested headers follow the same order; their own containing
directory is not added to the search. Absolute filenames are checked directly.

## Command Line Option

```pwsh
zero.exe run main.zasm -I "headers" -I "C:\Libraries\Zero"

# Managed builds from the repository need the header path explicitly:
dotnet output/Zero.Cli/Release/zcc.dll samples/hello-using.zasm -I include
```

## Rules

* Process nested using directives recursively.
* Include each resolved file once per compilation.
* Detect circular includes and report the chain, such as a.zh → b.zh → a.zh.
* Keep your stated search order for nested includes too; don’t silently add another search location.
* Report missing files with the requesting filename, line number, and paths searched.
* Normalize paths before comparing them so `file.zh` and `./file.zh` refer to the same file.
* Recognize using as a complete directive, respecting quoted filenames and trailing comments.

Use double quotes around filenames. `using` is case-insensitive. A semicolon
starts a trailing comment outside quotes. Extra text after the filename is an
error. Errors inside headers report the header's filename and original line number.

## Shipped headers

| Header | Purpose | Imported functions |
| --- | --- | --- |
| `console.zh` | Console text and program exit | `puts`, `printf`, `exit` |
| `windows.zh` | Basic Windows APIs | `ExitProcess`, `GetModuleHandleA`, `MessageBoxA` |

The headers target the compiler's current Windows x86 output. `puts` and `printf`
use cdecl, so remove their arguments from the stack after calls. Windows API
functions remove their own arguments.

Running `build.ps1` publishes this layout, also included in release ZIPs:

```text
publish/
  zero.exe
  zcc.exe
  include/
    console.zh
    windows.zh
    readme.md
  samples/
    hello-using.zasm
    ...
```

From any working directory, run the published sample with:

```powershell
C:\path\to\publish\zero.exe run C:\path\to\publish\samples\hello-using.zasm
```

It prints `Hello Zero!`. Keep the `include` folder beside the compiler when
moving or installing it. Headers containing executable code or inline data are
inserted exactly where `using` appears; use a jump to skip declarations when needed.

## Compiler library

The file-based `Zero.Compile` API performs the same expansion as the CLI.
Pass `Zero.Compilation.CompilerOptions` as its optional fourth argument to set
`IncludeDirectories`, `WorkingDirectory`, or `CompilerDirectory`. For direct
assembler use, load files with `Zero.Parsing.SourceLoader.Load` and pass its
`SourceLine` list to `Assembler.Assemble` to retain source locations.
The existing `Assemble(string[])` and `CompileAsm(string[])` APIs still accept
assembly lines directly; use the file loader when those lines contain `using`.
