---
layout: default
title: Zero Assembly Overview
---

# Zero Assembly Overview

[Documentation home](../index.md) · [Full language reference](ZASM-Reference.md) ·
[Basic examples](ZASM-Examples.md) · [Mnemonic support](ZASM-Mnemonics.md)

ZASM produces Windows x86 PE32 and static Linux x64 ELF64 executables. This page
summarizes selected Windows x86 encodings; the
[instruction reference](ZASM-Reference.md#instruction-reference) describes their
operand forms. See [mnemonic support](ZASM-Mnemonics.md) for both output backends;
Windows PE x64 output is not implemented.

## Selected x86 Encodings

`r32` means a supported 32-bit register; `imm32` is a 32-bit immediate value.
`rel32` is a displacement relative to the end of the instruction.

| Instruction | Encoding | Notes |
| --- | --- | --- |
| `NOP`             | `90`    | No operation. |
| `RET`             | `C3`    | Return to a caller. |
| `RET imm16`       | `C2 iw` | Return and discard argument bytes. |
| `INT imm8`        | `CD ib` | Software interrupt, subject to OS rules. |
| `MOV r32, imm32`  | `B8+r id` | Also accepts an address symbol. |
| `MOV r32, [mem]`  | `8B /r` plus addressing bytes | Read a 32-bit value. |
| `LEA r32, [mem]`  | `8D /r` plus addressing bytes | Calculate an address. |
| `PUSH r32`        | `50+r`  | Push a 32-bit register. |
| `POP r32`         | `58+r`  | Pop into a 32-bit register. |
| `PUSH imm32`      | `68 id` | Also accepts an address symbol. |
| `ADD r32, imm32`  | `81 /0 id` | Register-to-register ADD also works. |
| `SUB r32, imm32`  | `81 /5 id` | Register-to-register SUB also works. |
| `CMP r32, imm32`  | `81 /7 id` | Set flags without storing the subtraction result. |
| `JMP label`       | `E9 rel32` | Unconditional branch. |
| `CALL label`      | `E8 rel32` | Direct procedure call. |
| `CALL [address]`  | `FF 15` plus absolute address | Includes calls through import-table slots. |
| `JZ label`        | `0F 84 rel32` | Branch when zero/equal. |
| `JNZ label`       | `0F 85 rel32` | Branch when nonzero/unequal. |
| `JZ8 label`       | `74 rel8` | Short branch, -128 to +127 bytes. |
| `JNZ8 label`      | `75 rel8` | Short branch, -128 to +127 bytes. |

This is a selected encoding table; `AND`, `OR`, `XOR`, and `TEST` are also
implemented. The current assembler uses **EAX, EBX, ECX, EDX, ESI, EDI, EBP, ESP**.
See [registers](ZASM-Reference.md#registers) for their roles.

## Data and Other Directives

| Directive | Supported data | Example |
| --- | --- | --- |
| `DB` | Integer bytes and UTF-8 strings. | `message db 'Hello', 0` |
| `DW` | 16-bit integer values, little-endian. | `count: dw 42` |
| `DD` | 32-bit integer values, little-endian. | `value: dd 42` |
| `#IMPORT` | DLL name followed by exported function names. | `#IMPORT msvcrt.dll, puts, exit` |
| `using` | Double-quoted header path. | `using "windows/console.zh"` |

Use `;` or `//` for line comments, `/* ... */` for block comments, and `label:`
to name a location. Block comments can span lines. `DB` also allows a named
byte declaration without a colon. Strings need an explicit terminating zero when
passed to APIs expecting zero-terminated text. `DW` and `DD` do not currently
parse string, floating-point, or symbol initializers.

Data is emitted into the read-only code section. The first emitted byte is the
entry point; use a jump to skip inline data at the beginning of a program.
See [directives](ZASM-Reference.md#assembler-directives),
[labels](ZASM-Reference.md#labels), and [headers](ZASM-Include-Files.md).

## Coming Soon

**Coming Soon for Windows x86:** `DQ`, more instruction widths, writable sections,
constants, macros, and additional targets. Linux x64 already supports numeric
`DQ` and essential 64-bit instruction forms.
See the [documentation roadmap](../index.md#coming-soon).
