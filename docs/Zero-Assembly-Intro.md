# Zero Assembly Overview

[Documentation home](index.md) · [Full language reference](zasm-reference.md) ·
[Basic examples](zasm-examples.md)

ZASM currently produces Windows x86 PE32 executables. This page summarizes common
encodings; the [instruction reference](zasm-reference.md#instruction-reference)
lists every implemented mnemonic and its operand forms.

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
See [registers](zasm-reference.md#registers) for their roles.

## Data and Other Directives

| Directive | Supported data | Example |
| --- | --- | --- |
| `DB` | Integer bytes and UTF-8 strings. | `message db 'Hello', 0` |
| `DW` | 16-bit integer values, little-endian. | `count: dw 42` |
| `DD` | 32-bit integer values, little-endian. | `value: dd 42` |
| `#IMPORT` | DLL name followed by exported function names. | `#IMPORT msvcrt.dll, puts, exit` |
| `using` | Double-quoted header path. | `using "console.zh"` |

Use `;` for comments and `label:` to name a location. `DB` also allows a named
byte declaration without a colon. Strings need an explicit terminating zero when
passed to APIs expecting zero-terminated text. `DW` and `DD` do not currently
parse string, floating-point, or symbol initializers.

Data is emitted into the read-only code section. The first emitted byte is the
entry point; use a jump to skip inline data at the beginning of a program.
See [directives](zasm-reference.md#assembler-directives),
[labels](zasm-reference.md#labels), and [headers](Zero-Assembly-Include-Files.md).

## Coming Soon

**Coming Soon:** `DQ`, more instruction widths, writable sections, constants,
macros, and additional targets. These are not currently supported syntax.
See the [documentation roadmap](index.md#coming-soon).
