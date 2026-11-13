---
layout: default
title: Linux x86-64 and ELF64
---

# Linux x86-64 and ELF64

[Documentation home](index.md) · [Linux header drafts](linux-headers.md) ·
[ZASM reference](zasm-reference.md)

Zero can generate static Linux x86-64 ELF64 executables from Windows or Linux.
This first backend uses Linux syscalls directly: no libc, dynamic interpreter,
external linker, or shared-library imports are needed. Windows x86 PE32 remains
the default target for existing sources.

## Compile the Sample

From the published folder on Windows:

```powershell
.\zero.exe samples\hello-linux64-cli.zasm hello-linux64-cli
```

The sample declares `#TARGET linux-x64`. For other sources, you can select the
target with `--target linux-x64`. If both are supplied, they must agree.
The default Linux output removes the source extension instead of adding `.exe`.

Copy the generated file into Linux or WSL, then run:

```sh
chmod +x hello-linux64-cli
./hello-linux64-cli
```

Expected output and exit status:

```text
Hello Zero from Linux x86-64!
```

The exit status is 0 on success and 1 if the write does not return the expected
byte count. A compiler running on Linux sets executable permissions automatically.
On Linux x86-64, `zero run hello-linux64-cli.zasm` can compile and execute directly.
On Windows, `run` compiles ELF output but reports that Linux/WSL is needed to execute it.

To run the managed compiler on Linux, build with the .NET 10 SDK and use:

```sh
dotnet build source/Zero.slnx --configuration Release
dotnet output/Zero.Cli/Release/zcc.dll run samples/hello-linux64-cli.zasm
```

The Windows `build.ps1` still publishes a Windows-hosted Native AOT compiler.
Its `-Runtime` option selects the compiler host; `--target`/`#TARGET` selects the
generated application's target. Cross-publishing Linux Native AOT from Windows
is a separate toolchain concern.

## Target Selection

```asm
#TARGET linux-x64
```

Supported targets are `windows-x86` (PE32) and `linux-x64` (ELF64).
`#TARGET` is a standalone, case-insensitive statement and may have a semicolon
comment. Conflicting declarations or CLI selections are errors. The include loader
runs before target selection, so target declarations in headers are also checked.

Platform, CPU architecture, and executable format are separate fields in the
compiler's target model. macOS, STM32, and Linux ARM64 are distinct future targets;
selecting one currently reports an unsupported-target error. ELF output does not
imply Linux behavior for STM32, nor does it imply macOS application support.

## Essential AMD64 Instructions

| Area | Implemented forms |
| --- | --- |
| Registers | `RAX`–`RDI`, `R8`–`R15`, `EAX`–`EDI`, `R8D`–`R15D`; the traditional names include stack/base pointers. |
| `MOV` | Register copies with matching widths; r32/imm32, r64/imm64; 32/64-bit loads and register-to-memory stores. |
| `LEA` | r32/r64 destination and supported memory address. |
| Stack | `PUSH r64`, `POP r64`, and `PUSH` sign-extended imm32. |
| Arithmetic/flags | `ADD`, `SUB`, `AND`, `OR`, `XOR`, `CMP`, `TEST`: r32/r64 destination with matching register or immediate source. |
| Branches | Direct `JMP`, `CALL`, `JZ`, `JNZ`; range-checked `JZ8`/`JNZ8`; indirect `CALL`/`JMP` through r64 or supported memory. |
| Other | `RET`, `RET imm16`, `NOP`, `INT imm8`, `SYSCALL`. |
| Data | `DB` (including UTF-8 strings), `DW`, `DD`, and numeric `DQ`. |

Arithmetic/test operations on r64 use sign-extended imm32, not arbitrary imm64.
Register widths must match. Unsupported widths, oversized immediates, unknown
instructions, and invalid address forms produce assembly diagnostics.
`INT` is encoded but is not a substitute for the Linux x86-64 syscall interface.

## Memory and Addresses

```asm
lea rsi, [message]       ; RIP-relative address of a label.
mov rax, [rsp + 8]      ; Read an eight-byte stack value.
mov [rsp + 8], rax      ; Write an eight-byte stack value.
mov eax, [r12 - 4]      ; Read a four-byte value.
```

Bare `[label]` references are RIP-relative on this target. `[rip + label]` also
targets that label; `[rip + 8]` uses a literal displacement from the next instruction.
Base addressing requires a 64-bit register, with an optional signed 32-bit numeric
displacement. RSP and R12 bases receive the required SIB encoding. Scaled indexes,
multiple base/index registers, size-qualified immediate stores, and general
expressions are not implemented.

Data declarations remain inline in the read-only executable `.text` section.
The sample jumps over its message before executing code. The entry point is the
first emitted byte, not a special label name. Use the writable process stack for
temporary storage; memory-store support does not make inline data writable.

## Linux Syscalls

The sample uses syscall 1 (`write`) and syscall 60 (`exit`). For native x86-64,
place the number in RAX, arguments in RDI, RSI, RDX, R10, R8, R9, and read the result
from RAX. `SYSCALL` clobbers RCX and R11. Writing EAX/EDI/EDX also zeroes the upper
half of their corresponding 64-bit registers, as defined by AMD64.

The message contains 30 bytes including its newline; no terminating zero is needed
for `write`, which takes an explicit count. Raw syscall errors are returned as
negative error numbers, rather than libc's `-1` plus `errno` convention.
See [Linux syscall conventions](https://man7.org/linux/man-pages/man2/syscall.2.html)
and the [x86-64 syscall table](https://github.com/torvalds/linux/blob/master/arch/x86/entry/syscalls/syscall_64.tbl).
The `include/linux/syscalls/x64.zh` file remains a comment-only reference;
it does not define symbolic constants or macros.

## Executable Layout and Compiler Structure

The writer creates a little-endian AMD64 `ET_EXEC` ELF64 image with one read/execute
load segment, a non-executable stack declaration, `.text`, and `.shstrtab`.
The fixed load address is `0x400000`; emitted text starts at `0x401000`.
It is not PIE and does not supply writable sections, BSS, or relocation records.
See the [ELF header](https://gabi.xinuos.com/elf/02-eheader.html) and
[loading specification](https://gabi.xinuos.com/elf/07-pheader.html).

The shared `AssemblyParser` collects instructions, labels, and imports. Separate
`X86Encoder` and `X64Encoder` implementations encode instructions. The assembler
coordinates a PE or ELF executable writer using 64-bit layout addresses.
Future ARM encoders can implement the same encoding interface; macOS and STM32
need their own platform conventions and supported format combinations.

## Testing and Coming Soon

Encoding and executable-header tests run on any supported .NET host. All samples
compile during `build.ps1`; Linux samples declare their target and produce
extensionless files in the validation output. Native runtime tests execute the
greeting, exit codes, branches, register calls, and stack loads/stores on Linux
x86-64. Other hosts report those tests as skipped. The Linux CI job runs them on
Ubuntu and inspects the sample with `readelf`.

**Coming Soon:** shared-library imports/libc linking, PIE and relocations, writable
sections/BSS, broader instruction coverage, debug information, and additional
platform/architecture backends. The `linux/libc/*.zh` files are still drafts:
`#IMPORT` is explicitly rejected by this static ELF64 backend.
