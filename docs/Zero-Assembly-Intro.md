# Zero - Assembly Compiler

The Zero compiler project assembly language support.

## x86 Support

### Instructions

| Instruction | Op Code | Notes |
|-|-|-|
| `NOP`             | `90` |
| `RET`             | `C3` |
| `INT imm8`        | `CD ib` (e.g., INT 3) |
| `MOV r32, imm32`  | `B8+r ib, id` |
| `PUSH r32`        | `50+r` |
| `POP r32`         | `58+r` |
| `PUSH imm32`      | `68 id` |
| `ADD r32, imm32`  | `81 /0 r, id` |
| `SUB r32, imm32`  | `81 /5 r, id` |
| `CMP r32, imm32`  | `81 /7 r, id` |
| `JMP label`       | `E9 rel32` |
| `CALL label`      | `E8 rel32` |
| `JZ label`        | `0F 84 rel32` |
| `JNZ label`       | `0F 85 rel32` |

### Registers

EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI

### Data Directives

Data Directives are used to allocate data of varying widths.

| Directive | Full Name | Size in Bytes | Use Case | Sample |
|-|-|-|-|-|
| `db` | Define Byte        | 1 byte (8-bit) | Characters, strings, etc. | `db <byte>[, ...]` (supports decimal or 0x..)
| `dw` | Define Word        | 2 | 16-bit integers, memory offsets, Unicode/UTF-16 characters | `dw <word>[, ...]` |
| `dd` | Define Double Word | 4 | 32-bit integers, single-precision floats, memory addresses | `dd <dword>[, ...]` |
| `dq` | Define Quad Word   | 8 | 64-bit pointers, 64-bit integers, double-precision floats | `` |

## Other

* `;` - Comment support
* `label:` - Creates a label at current offset. Replace "label" with `Aa-Zz` non-whitespace.
