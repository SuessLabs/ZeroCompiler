---
layout: default
title: ZASM Language Reference
---

# ZASM Language Reference

[Documentation home](index.md) · [Complete examples](zasm-examples.md) ·
[Include files](Zero-Assembly-Include-Files.md)

This reference covers the current Windows x86 assembler. An instruction name
appearing in a source-code constant or an older design document does not establish
support; the operand forms below describe the implemented encodings.

## Source Files and Statements

- `.zasm` is the main assembly source file. `.zh` is the convention for shared headers.
- Write one instruction, data directive, label, or include statement per line.
- Indentation uses spaces or tabs. Separate operands with commas.
- Use Intel-style destination-first order: `add eax, 5` adds 5 to EAX.
- Mnemonics, register names, labels, and the `using` keyword are case-insensitive.
- Keep DLL export names in their documented spelling, such as `MessageBoxA`.
- Start comments with `;`. `//` and block comments are not implemented for ZASM.
- A semicolon outside quotes comments out the remainder of the line; it does not separate instructions.

```asm
  mov eax, 7       ; EAX now contains 7.
  add eax, 5       ; EAX now contains 12.
```

The entry point is the first emitted byte, even if it belongs to a data directive.
Neither `start:` nor `main:` automatically selects an entry point. Place code
first or use `jmp start` before inline data. Import/include declarations themselves
emit no CPU instructions, though included files can contain instructions or data.

## Operands

| Notation | Meaning | Examples |
| --- | --- | --- |
| `r32` | One of the eight supported 32-bit registers. | `eax`, `esp` |
| `imm32` | An integer value encoded in 32 bits. | `42`, `0x2A`, `2Ah` |
| `address` | A local data/code label or an imported address symbol. | `message` |
| `mem32` | A memory location containing a 32-bit value. | `[value]`, `[eax]`, `[ebp + 8]` |
| `label` | A named location for a direct branch or procedure call. | `done`, `calculate` |
| `imm8` | A numeric byte operand. | `3` in `int 3` |
| `imm16` | A numeric unsigned 16-bit operand for `RET`. | `ret 16` |

Decimal and hexadecimal integer literals are supported. Hexadecimal can use a
`0x` prefix or an `h` suffix. Binary literals, floating-point literals, and general
expressions such as `2 * 4` or `message + 1` are Coming Soon.
Signed decimal immediates are not consistently accepted across instructions;
use a 32-bit hexadecimal bit pattern, such as `0xFFFFFFFF`, when needed.
Data directives parse integer operands directly and can accept signed decimal values.

Square brackets mean memory access when used with `MOV` or indirect `CALL`.
With `LEA`, brackets describe an address to calculate without reading its contents:

```asm
value: dd 42
; Instruction examples; place data behind a jump in a complete program.
mov eax, value       ; Address of value.
mov eax, [value]     ; The 32-bit value 42 stored at that address.
lea edx, [value]     ; Address of value, without loading the stored number.
```

### Memory Addressing

`MOV r32, [mem]` and `LEA r32, [mem]` accept these forms:

| Form | Example | Meaning |
| --- | --- | --- |
| Absolute symbol | `[value]` | The address associated with the symbol. |
| Absolute number | `[0x401000]` | An explicit address. |
| Register base | `[eax]` | The address currently stored in EAX. |
| Base plus displacement | `[esp + 4]` | ESP plus the numeric displacement. |

The displacement can also be a resolvable address symbol. Scaled indexes,
multiple register bases, and subtraction syntax such as `[ebp - 4]` are not
implemented. Examples using `[esp + 4]` assume the stack contains valid data there.
Indirect `CALL` has a narrower form: use an absolute address or import/data symbol,
such as `call [puts]`. Register-indirect calls and `[reg + offset]` calls are not supported.

## Registers

The current backend emits 32-bit instructions using these general-purpose registers.
The roles are conventions; instructions determine how a register is used.

| Register | Common role |
| --- | --- |
| `EAX` | Arithmetic results and integer function return values. |
| `EBX` | General storage; normally preserved by a called procedure. |
| `ECX` | Counters and temporary values. |
| `EDX` | Temporary values and additional result data. |
| `ESI` | Source pointers; normally preserved by a called procedure. |
| `EDI` | Destination pointers; normally preserved by a called procedure. |
| `EBP` | Stack-frame base; normally preserved by a called procedure. |
| `ESP` | Stack pointer; changes with stack operations and calls. |

Use these 32-bit names. Register constants such as `AX`, `AL`, `RAX`, and `XMM0`
exist in the project, but their corresponding instruction widths are not implemented.
Their presence is not a guarantee that they encode correctly.

## Instruction Reference

`imm32/address` means a number or an address symbol. Arithmetic and bitwise
instructions below operate on 32-bit registers; memory operands are limited to
the explicitly listed forms.

| Mnemonic | Supported forms | Effect |
| --- | --- | --- |
| `MOV` | `MOV r32, imm32/address`; `MOV r32, [mem]` | Load a value or read 32-bit memory into a register. |
| `LEA` | `LEA r32, [mem]` | Calculate an address without reading memory. |
| `PUSH` | `PUSH r32`; `PUSH imm32/address` | Place a 32-bit value on the stack; decrease ESP by 4. |
| `POP` | `POP r32` | Read a 32-bit stack value into a register; increase ESP by 4. |
| `ADD` | `ADD r32, r32`; `ADD r32, imm32/address` | Add the second operand to the first. |
| `SUB` | `SUB r32, r32`; `SUB r32, imm32/address` | Subtract the second operand from the first. |
| `CMP` | `CMP r32, r32`; `CMP r32, imm32/address` | Set CPU flags as if subtracting, without changing the operands. |
| `AND` | `AND r32, r32`; `AND r32, imm32/address` | Keep bits set in both operands. |
| `OR` | `OR r32, r32`; `OR r32, imm32/address` | Set bits present in either operand. |
| `XOR` | `XOR r32, r32`; `XOR r32, imm32/address` | Set bits that differ between the operands. |
| `TEST` | `TEST r32, r32`; `TEST r32, imm32/address` | Set flags from bitwise AND without changing the operands. |
| `JMP` | `JMP label` | Jump unconditionally to a label. |
| `JZ` | `JZ label` | Jump when the zero flag is set. After CMP, this means equal. |
| `JNZ` | `JNZ label` | Jump when the zero flag is clear. After CMP, this means unequal. |
| `JZ8` | `JZ8 label` | Short form of JZ; signed 8-bit relative distance. |
| `JNZ8` | `JNZ8 label` | Short form of JNZ; signed 8-bit relative distance. |
| `CALL` | `CALL label`; `CALL [absolute-address-symbol-or-number]` | Save a return address and call local code or an indirect target. |
| `RET` | `RET`; `RET imm16` | Return to the caller; optionally discard argument bytes after popping the return address. |
| `NOP` | `NOP` | Perform no operation. |
| `INT` | `INT imm8` | Issue a software interrupt. Permission and behavior depend on the OS. |

Short jumps must be within -128 through +127 bytes of the end of the instruction.
Use `JZ` or `JNZ` when the short form is out of range. `JE` and `JNE` aliases are
not currently implemented. `RET` accepts a cleanup count from 0 to 65535 bytes;
use it only when returning to a caller that expects that calling convention.

`INT` is not a portable console-output or system-call interface for a Windows
application. Use imported Windows/C runtime functions for the examples here.

### Compare and Branch

```asm
  mov eax, 7
  add eax, 5
  cmp eax, 12
  jz equal
  mov eax, 1
  jmp done
equal:
  mov eax, 0
done:
  nop
```

This is a fragment: add program termination in a complete application. ZASM has
no high-level `if` statement or loop statement yet; use labels and branches.
See [the arithmetic example](zasm-examples.md#add-two-integers) for a complete program.

## Assembler Directives

A directive controls assembly rather than describing a CPU instruction.
Data directives emit bytes; imports describe DLL dependencies. `using` loads
additional source before assembly.

| Directive | Operands | Output | Example |
| --- | --- | --- | --- |
| `DB` | One or more integers or quoted strings. | One byte per integer; UTF-8 bytes for strings. | `message db 'Hello', 0` |
| `DW` | Integer values separated by commas. | Two bytes per value, little-endian. | `counts: dw 10, 20` |
| `DD` | Integer values separated by commas. | Four bytes per value, little-endian. | `total: dd 42` |
| `#IMPORT` | DLL name, then one or more exported function names. | PE import table entries. | `#IMPORT msvcrt.dll, puts, exit` |
| `using` | One double-quoted filename. | Source expanded at the directive position. | `using "console.zh"` |

`DW` and `DD` accept integer literals, not quoted strings, symbol initializers,
or floating-point values. `DB` integer operands emit their low 8 bits; `DW`
emits the low 16 bits. Values do not imply range-checked variable types.
`DQ`, repetition directives such as `TIMES`/`DUP`, alignment, constants, and
separate data/code sections are Coming Soon.

### Strings and Named Data

```asm
message db 'Hello, World!', 0x0A, 0
quote: db 'It''s Zero', 0
path: db "C:\Zero\samples", 0
```

Both single-quoted and double-quoted strings emit UTF-8. Double the matching quote
to put it inside a string. Backslashes are literal: use numeric `10` or `0x0A`
for a newline rather than `\n`. A trailing zero is not added automatically.
Semicolons, commas, and colons inside quotes are ordinary text.
Whether a library displays UTF-8 correctly depends on that API and the console configuration.

`message db ...` and `message: db ...` both work. Use the colon form for `DW`
and `DD`. Names describe addresses, not writable high-level variables. Current
data declarations are stored in the read-only `.text` section. For writable
scratch space, existing samples reserve stack space and pass its address to a DLL.

## Labels

A label names the current position and can appear before an instruction on the
same line. Forward references are supported.

```asm
  jmp start
message db 'Hello', 0
start: push message
```

Label rules:

- Start with a letter, underscore, or period; continue with letters, digits, underscores, or periods.
- Use unique names throughout the expanded source, including headers.
- Names are case-insensitive. `start` and `START` identify the same symbol.
- A leading period does not create a local scope; `.loop` is still a global label.
- Register names cannot be labels. Labels must not collide with imported symbols.
- Use bare labels for direct branches/calls; use address symbols for data operands.

Procedures are labels followed by instructions and usually `RET`; no `PROC` or
`ENDP` directive is required or implemented. The top-level program should call
`exit` or `ExitProcess` rather than depend on `RET` to terminate it.

## Imports and Calling Conventions

```asm
#import kernel32.dll, ExitProcess
#IMPORT msvcrt.dll, puts, exit
```

`#import` and `#IMPORT` are the same directive. DLL names without an extension
get `.dll` appended. Declare the actual exported function spelling; for example,
use `MessageBoxA` for the ANSI API. Import declarations do not describe parameter
types or perform argument checking. Push arguments in reverse order yourself.

| Call | Meaning |
| --- | --- |
| `call helper` | Direct call to your own label. |
| `call [puts]` | Read the imported function pointer from the Import Address Table and call it. |
| `call [IAT_puts]` | Backward-compatible import alias. |
| `call [__imp_puts]` | Another backward-compatible import alias. |
| `call [IAT_kernel32_dll_ExitProcess]` | DLL-qualified alias; use this form to disambiguate duplicate function names. |

The `IAT_` prefix is optional for unambiguous function names. Plain aliases are
unavailable when multiple imported DLLs export the same requested name.

For x86 C runtime functions such as `puts` and `printf`, the caller removes the
arguments after the call. Each argument in these examples occupies 4 bytes:

```asm
  push message
  call [puts]
  add esp, 4
```

For x86 Win32 APIs such as `MessageBoxA`, the callee removes its arguments:

```asm
  push 0           ; uType
  push caption     ; lpCaption
  push message     ; lpText
  push 0           ; hWnd
  call [MessageBoxA]
```

These snippets require the referenced labels and imports. See the
[complete examples](zasm-examples.md). Library calls can overwrite EAX, ECX, and
EDX. Preserve any values you need across calls, and preserve EBX, ESI, EDI, and EBP
when implementing a procedure or callback whose ABI requires it.

## Shared Headers

```asm
using "console.zh"
using "subfolder/helpers.zh" ; A trailing comment is allowed.
```

Search order is:

1. The process's current working directory.
2. Directories supplied by repeated `-I` or `--include`, in order.
3. `include` beside the compiler executable.

The entire relative filename is appended to each directory. Nested includes use
the same roots, not the directory containing the current header. Absolute paths
are checked directly. Resolved, normalized paths are included once per compilation;
active cycles report an error with the chain. Missing-file errors list the
requesting source location and searched paths.

| Shipped header | Imports |
| --- | --- |
| `console.zh` | `puts`, `printf`, `exit` from `msvcrt.dll`. |
| `windows.zh` | `ExitProcess`, `GetModuleHandleA` from `kernel32.dll`; `MessageBoxA` from `user32.dll`. |

DLL-specific headers also cover Kernel32, User32, GDI32, ComCtl32, ComDlg32,
Shell32, Winsock, registry/security, COM, Automation, path helpers, multimedia,
HTTP, and file versions. See [Windows API headers](windows-headers.md) for the catalog,
architecture naming, string encodings, and initialization requirements.

The repository keeps these files in root `include`; publishing copies them to
`publish/include`. Read the [include guide](Zero-Assembly-Include-Files.md) for more details.

## Command Line and Errors

```text
zero <source> [output.exe] [-I <directory>]...
zero run <source> [output.exe] [-I <directory>]...
```

`zcc` accepts the same arguments. Options can appear before or after the positional
filenames, after `run` if present. `-I` and `--include` each take a separate directory
argument. `-h` and `--help` display help. The output defaults to the source path with
an `.exe` extension. `run` inherits the terminal and working directory, waits for
completion, and returns the generated program's exit code.

| CLI code | Meaning |
| --- | --- |
| `0` | Compilation succeeded, or help was displayed. With `run`, the program may also return 0. |
| `1` | Invalid command-line arguments. |
| `2` | Source file not found. |
| `3` | Assembly or include error. |
| `4` | An I/O, launch, or other handled operational failure. |

After successful compilation, `run` returns the application's code, which can
overlap these compiler error values. The preceding diagnostics distinguish the
cases. No application is launched after a compilation failure.

Source errors include a filename and line number, even when the error originates
in an included header. Check for an unsupported mnemonic/form, a misspelled symbol,
an invalid include path, or a short jump out of range.

## Current Limitations

The following require future implementation:

- Register-to-register `MOV`, memory stores, arithmetic on memory operands, and register-indirect calls.
- `MUL`, division, `ADC`, `SBB`, `NOT`, string instructions, flag-stack instructions, and additional branch conditions.
- Real 8-bit, 16-bit, 64-bit, or SIMD instruction forms.
- Macros, constants/equates, conditional assembly, general expressions, and typed structures.
- Writable data sections, alignment/reservation directives, and entry-point/subsystem selection.
- `#zero`, `#entry`, `#define`, `#importlib`, `PROC`, `ENDP`, `END`, and MASM-style section/model directives.
- Object files, linker inputs, exports, and `.zprj` project files.
- Higher-level Zero compilation, ELF output, and ARM/ARM64 or native x64 code generation.

**Coming Soon:** expand this reference as each feature gains working syntax and
examples. The [ZASM feature roadmap](zasm-coming-soon.md) tracks implemented
features and outstanding work. The [documentation roadmap](index.md#coming-soon)
collects broader topics to fill in.
