---
layout: default
title: ZASM Mnemonic Support
---

# ZASM Mnemonic Support

[Documentation home](../index.md) · [Assembly overview](ZASM-Intro.md) ·
[Language reference](ZASM-Reference.md) · [Examples](ZASM-Examples.md) ·
[Include files](ZASM-Include-Files.md) · [Roadmap](ZASM-Coming-Soon.md)

## Overview

A mnemonic is the name of a CPU instruction, such as `MOV`, `ADD`, or `CALL`.
The category describes what the instruction is used for. Support depends on the
output target and operand form, rather than the architecture running the compiler.
A Windows x64 compiler host can generate Windows x86 programs or Linux x64
programs; it does not imply Windows x64 output support.

| Output target | Format | Current status |
| --- | --- | --- |
| `windows-x86` | Windows PE32, 32-bit x86 | Implemented for the forms below; the default target. |
| `windows-x64` | Windows PE32+, 64-bit x86 | Entire target is unimplemented. No mnemonic currently produces Windows x64 output. |
| `linux-x64` | Static Linux ELF64, 64-bit x86 | Implemented for the forms below. |

The supported tables enumerate the current encoder cases. The unsupported tables
list the named integer/string backlog discussed in the roadmap, including missing
constants already present in the source. They are **not an exhaustive inventory
of the entire x86 ISA**. Additional aliases, system instructions, and floating-point
or vector extensions require further work. An unlisted instruction is not supported
unless it appears in a supported table.

Constants in `Mnemonic.cs`, syntax highlighting, and entries in `OpCodes.cs` do not
establish encoder support. The active encoders do not use the `OpCodes.cs` lookup.
`JZ8` and `JNZ8` are ZASM spellings for explicit short encodings, not additional CPU
instructions. `JE` and `JNE` aliases are currently missing even though `JZ` and `JNZ`
work.

## Windows PE x86/x64

### Supported on Windows PE32 x86

**Every row in this table is unavailable for Windows PE x64 because that target
has not been implemented.** `r32` means EAX, EBX, ECX, EDX, ESI, EDI, EBP, or ESP.
See the [operand reference](ZASM-Reference.md#operands) and
[memory addressing](ZASM-Reference.md#memory-addressing) for syntax and limits.

| Mnemonic | Category | Used for | Supported PE32 x86 operands |
| --- | --- | --- | --- |
| `MOV` | Data movement | Copy a value or load memory. | r32, immediate/address; r32, [memory]. |
| `LEA` | Addresses | Calculate an address without reading its contents. | r32, [memory]. |
| `PUSH` | Stack | Push a value onto the stack. | r32 or immediate/address. |
| `POP` | Stack | Pop a stack value into a register. | r32. |
| `ADD` | Arithmetic | Add the source to the destination. | r32 destination; r32 or immediate/address source. |
| `SUB` | Arithmetic | Subtract the source from the destination. | r32 destination; r32 or immediate/address source. |
| `AND` | Bitwise | Perform a bitwise AND. | r32 destination; r32 or immediate/address source. |
| `OR` | Bitwise | Perform a bitwise OR. | r32 destination; r32 or immediate/address source. |
| `XOR` | Bitwise | Perform a bitwise exclusive OR. | r32 destination; r32 or immediate/address source. |
| `CMP` | Comparison | Set flags from subtraction without storing the result. | r32 destination; r32 or immediate/address source. |
| `TEST` | Comparison | Set flags from AND without storing the result. | r32 destination; r32 or immediate/address source. |
| `JMP` | Control flow | Jump unconditionally. | Label. |
| `JZ` | Conditional branch | Jump if zero/equal. | Label; near rel32. |
| `JNZ` | Conditional branch | Jump if nonzero/not equal. | Label; near rel32. |
| `JZ8` | Conditional branch | Explicit short jump if zero/equal. | Label; range-checked rel8. |
| `JNZ8` | Conditional branch | Explicit short jump if nonzero/not equal. | Label; range-checked rel8. |
| `CALL` | Procedures | Save a return address and call a procedure. | Label or [absolute pointer location], including DLL import slots. |
| `RET` | Procedures | Return to the caller, optionally discarding stack bytes. | No operand or unsigned imm16 cleanup count. |
| `NOP` | Miscellaneous | Perform no operation. | No operands. |
| `INT` | Interrupts | Encode a software interrupt; execution depends on the OS. | imm8. |

### Unsupported on Windows PE x86/x64

`Missing` means there is no working mnemonic encoder case. `Target unavailable`
means Windows x64 compilation is rejected before instruction encoding; a future
PE64 backend may reuse parts of the existing AMD64 encoder.

| Mnemonic | Category | Used for | PE32 x86 | PE32+ x64 |
| --- | --- | --- | --- | --- |
| `ADC` | Carry arithmetic | Add with carry. | Missing | Target unavailable |
| `SBB` | Carry arithmetic | Subtract with borrow. | Missing | Target unavailable |
| `INC` | Arithmetic | Increment by one. | Missing | Target unavailable |
| `DEC` | Arithmetic | Decrement by one. | Missing | Target unavailable |
| `NEG` | Arithmetic | Negate a signed value. | Missing | Target unavailable |
| `MUL` | Arithmetic | Unsigned multiplication. | Missing | Target unavailable |
| `IMUL` | Arithmetic | Signed multiplication. | Missing | Target unavailable |
| `DIV` | Arithmetic | Unsigned division. | Missing | Target unavailable |
| `IDIV` | Arithmetic | Signed division. | Missing | Target unavailable |
| `MOVSX` | Data movement | Copy with sign extension. | Missing | Target unavailable |
| `MOVZX` | Data movement | Copy with zero extension. | Missing | Target unavailable |
| `XCHG` | Data movement | Exchange two values. | Missing | Target unavailable |
| `CBW` | Sign extension | Sign-extend AL into AX. | Missing | Target unavailable |
| `CWDE` | Sign extension | Sign-extend AX into EAX. | Missing | Target unavailable |
| `CWD` | Sign extension | Sign-extend AX into DX:AX. | Missing | Target unavailable |
| `CDQ` | Sign extension | Sign-extend EAX into EDX:EAX. | Missing | Target unavailable |
| `NOT` | Bitwise | Invert every bit. | Missing | Target unavailable |
| `BSWAP` | Bitwise | Reverse the byte order of a register. | Missing | Target unavailable |
| `SHL` | Shifts | Logical left shift. | Missing | Target unavailable |
| `SAL` | Shifts | Arithmetic left shift; alias of SHL. | Missing | Target unavailable |
| `SHR` | Shifts | Logical right shift. | Missing | Target unavailable |
| `SAR` | Shifts | Arithmetic right shift. | Missing | Target unavailable |
| `SHLD` | Shifts | Double-precision left shift. | Missing | Target unavailable |
| `SHRD` | Shifts | Double-precision right shift. | Missing | Target unavailable |
| `ROL` | Rotates | Rotate left. | Missing | Target unavailable |
| `ROR` | Rotates | Rotate right. | Missing | Target unavailable |
| `RCL` | Rotates | Rotate left through carry. | Missing | Target unavailable |
| `RCR` | Rotates | Rotate right through carry. | Missing | Target unavailable |
| `JE` | Conditional branches | Jump on equal/zero. | Missing | Target unavailable |
| `JNE` | Conditional branches | Jump on not equal/nonzero. | Missing | Target unavailable |
| `JG` | Conditional branches | Jump on greater, signed. | Missing | Target unavailable |
| `JGE` | Conditional branches | Jump on greater or equal, signed. | Missing | Target unavailable |
| `JL` | Conditional branches | Jump on less, signed. | Missing | Target unavailable |
| `JLE` | Conditional branches | Jump on less or equal, signed. | Missing | Target unavailable |
| `JA` | Conditional branches | Jump on above, unsigned. | Missing | Target unavailable |
| `JAE` | Conditional branches | Jump on above or equal, unsigned. | Missing | Target unavailable |
| `JB` | Conditional branches | Jump on below, unsigned. | Missing | Target unavailable |
| `JBE` | Conditional branches | Jump on below or equal, unsigned. | Missing | Target unavailable |
| `JO` | Conditional branches | Jump on overflow. | Missing | Target unavailable |
| `JNO` | Conditional branches | Jump on no overflow. | Missing | Target unavailable |
| `JS` | Conditional branches | Jump on negative sign. | Missing | Target unavailable |
| `JNS` | Conditional branches | Jump on nonnegative sign. | Missing | Target unavailable |
| `JP` | Conditional branches | Jump on even parity. | Missing | Target unavailable |
| `JNP` | Conditional branches | Jump on odd parity. | Missing | Target unavailable |
| `JC` | Conditional branches | Jump if carry is set; alias of JB. | Missing | Target unavailable |
| `JNC` | Conditional branches | Jump if carry is clear; alias of JAE. | Missing | Target unavailable |
| `SETE` | Conditional results | Store 1 on equal/zero; otherwise store 0. | Missing | Target unavailable |
| `SETNE` | Conditional results | Store 1 on not equal/nonzero; otherwise store 0. | Missing | Target unavailable |
| `SETG` | Conditional results | Store 1 on greater, signed; otherwise store 0. | Missing | Target unavailable |
| `SETGE` | Conditional results | Store 1 on greater or equal, signed; otherwise store 0. | Missing | Target unavailable |
| `SETL` | Conditional results | Store 1 on less, signed; otherwise store 0. | Missing | Target unavailable |
| `SETLE` | Conditional results | Store 1 on less or equal, signed; otherwise store 0. | Missing | Target unavailable |
| `SETA` | Conditional results | Store 1 on above, unsigned; otherwise store 0. | Missing | Target unavailable |
| `SETAE` | Conditional results | Store 1 on above or equal, unsigned; otherwise store 0. | Missing | Target unavailable |
| `SETB` | Conditional results | Store 1 on below, unsigned; otherwise store 0. | Missing | Target unavailable |
| `SETBE` | Conditional results | Store 1 on below or equal, unsigned; otherwise store 0. | Missing | Target unavailable |
| `SETO` | Conditional results | Store 1 on overflow; otherwise store 0. | Missing | Target unavailable |
| `SETNO` | Conditional results | Store 1 on no overflow; otherwise store 0. | Missing | Target unavailable |
| `SETS` | Conditional results | Store 1 on negative sign; otherwise store 0. | Missing | Target unavailable |
| `SETNS` | Conditional results | Store 1 on nonnegative sign; otherwise store 0. | Missing | Target unavailable |
| `SETP` | Conditional results | Store 1 on even parity; otherwise store 0. | Missing | Target unavailable |
| `SETNP` | Conditional results | Store 1 on odd parity; otherwise store 0. | Missing | Target unavailable |
| `CMOVE` | Conditional moves | Copy on equal/zero. | Missing | Target unavailable |
| `CMOVNE` | Conditional moves | Copy on not equal/nonzero. | Missing | Target unavailable |
| `CMOVG` | Conditional moves | Copy on greater, signed. | Missing | Target unavailable |
| `CMOVGE` | Conditional moves | Copy on greater or equal, signed. | Missing | Target unavailable |
| `CMOVL` | Conditional moves | Copy on less, signed. | Missing | Target unavailable |
| `CMOVLE` | Conditional moves | Copy on less or equal, signed. | Missing | Target unavailable |
| `CMOVA` | Conditional moves | Copy on above, unsigned. | Missing | Target unavailable |
| `CMOVAE` | Conditional moves | Copy on above or equal, unsigned. | Missing | Target unavailable |
| `CMOVB` | Conditional moves | Copy on below, unsigned. | Missing | Target unavailable |
| `CMOVBE` | Conditional moves | Copy on below or equal, unsigned. | Missing | Target unavailable |
| `CMOVO` | Conditional moves | Copy on overflow. | Missing | Target unavailable |
| `CMOVNO` | Conditional moves | Copy on no overflow. | Missing | Target unavailable |
| `CMOVS` | Conditional moves | Copy on negative sign. | Missing | Target unavailable |
| `CMOVNS` | Conditional moves | Copy on nonnegative sign. | Missing | Target unavailable |
| `CMOVP` | Conditional moves | Copy on even parity. | Missing | Target unavailable |
| `CMOVNP` | Conditional moves | Copy on odd parity. | Missing | Target unavailable |
| `LOOP` | Loops | Decrement the counter and jump if nonzero. | Missing | Target unavailable |
| `LOOPE` | Loops | Loop while the counter is nonzero and ZF is set. | Missing | Target unavailable |
| `LOOPZ` | Loops | Alias of LOOPE. | Missing | Target unavailable |
| `LOOPNE` | Loops | Loop while the counter is nonzero and ZF is clear. | Missing | Target unavailable |
| `LOOPNZ` | Loops | Alias of LOOPNE. | Missing | Target unavailable |
| `JECXZ` | Loops | Jump if ECX is zero. | Missing | Target unavailable |
| `BT` | Bit operations | Copy a selected bit to the carry flag. | Missing | Target unavailable |
| `BTS` | Bit operations | Test and set a bit. | Missing | Target unavailable |
| `BTR` | Bit operations | Test and reset a bit. | Missing | Target unavailable |
| `BTC` | Bit operations | Test and complement a bit. | Missing | Target unavailable |
| `BSF` | Bit operations | Find the lowest set bit. | Missing | Target unavailable |
| `BSR` | Bit operations | Find the highest set bit. | Missing | Target unavailable |
| `POPCNT` | Bit operations | Count set bits. | Missing | Target unavailable |
| `ENTER` | Stack frames | Create a stack frame. | Missing | Target unavailable |
| `LEAVE` | Stack frames | Release a stack frame. | Missing | Target unavailable |
| `PUSHF` | Flags | Push flags, with operand-size rules. | Missing | Target unavailable |
| `POPF` | Flags | Restore flags, subject to operand-size and privilege rules. | Missing | Target unavailable |
| `CLC` | Flags | Clear carry. | Missing | Target unavailable |
| `STC` | Flags | Set carry. | Missing | Target unavailable |
| `CMC` | Flags | Complement carry. | Missing | Target unavailable |
| `CLD` | Flags | Clear the string direction flag. | Missing | Target unavailable |
| `STD` | Flags | Set the string direction flag. | Missing | Target unavailable |
| `LAHF` | Flags | Load status flags into AH. | Missing | Target unavailable |
| `SAHF` | Flags | Store AH into status flags. | Missing | Target unavailable |
| `MOVSB` | Strings | Copy a string element. Element width: 8 bits. | Missing | Target unavailable |
| `MOVSW` | Strings | Copy a string element. Element width: 16 bits. | Missing | Target unavailable |
| `MOVSD` | Strings | Copy a string element. Element width: 32 bits. | Missing | Target unavailable |
| `CMPSB` | Strings | Compare string elements. Element width: 8 bits. | Missing | Target unavailable |
| `CMPSW` | Strings | Compare string elements. Element width: 16 bits. | Missing | Target unavailable |
| `CMPSD` | Strings | Compare string elements. Element width: 32 bits. | Missing | Target unavailable |
| `LODSB` | Strings | Load a string element into the accumulator. Element width: 8 bits. | Missing | Target unavailable |
| `LODSW` | Strings | Load a string element into the accumulator. Element width: 16 bits. | Missing | Target unavailable |
| `LODSD` | Strings | Load a string element into the accumulator. Element width: 32 bits. | Missing | Target unavailable |
| `STOSB` | Strings | Store the accumulator into a string element. Element width: 8 bits. | Missing | Target unavailable |
| `STOSW` | Strings | Store the accumulator into a string element. Element width: 16 bits. | Missing | Target unavailable |
| `STOSD` | Strings | Store the accumulator into a string element. Element width: 32 bits. | Missing | Target unavailable |
| `SCASB` | Strings | Compare the accumulator with a string element. Element width: 8 bits. | Missing | Target unavailable |
| `SCASW` | Strings | Compare the accumulator with a string element. Element width: 16 bits. | Missing | Target unavailable |
| `SCASD` | Strings | Compare the accumulator with a string element. Element width: 32 bits. | Missing | Target unavailable |
| `JCXZ` | Loops | Jump if CX is zero. | Missing | Target unavailable |
| `PUSHA` | Stack | Push the 16-bit general-purpose registers. | Missing | Target unavailable |
| `PUSHAD` | Stack | Push the 32-bit general-purpose registers. | Missing | Target unavailable |
| `POPA` | Stack | Pop the 16-bit general-purpose registers. | Missing | Target unavailable |
| `POPAD` | Stack | Pop the 32-bit general-purpose registers. | Missing | Target unavailable |
| `JRCXZ` | Loops | Jump if RCX is zero. | Not a 32-bit form | Target unavailable |
| `MOVSXD` | Sign extension | Sign-extend a doubleword into a 64-bit register. | Not a 32-bit form | Target unavailable |
| `CDQE` | Sign extension | Sign-extend EAX into RAX. | Not a 32-bit form | Target unavailable |
| `CQO` | Sign extension | Sign-extend RAX into RDX:RAX. | Not a 32-bit form | Target unavailable |
| `PUSHFQ` | Flags | Push 64-bit flags. | Not a 32-bit form | Target unavailable |
| `POPFQ` | Flags | Restore 64-bit flags, subject to OS restrictions. | Not a 32-bit form | Target unavailable |
| `MOVSQ` | Strings | Copy 64-bit string elements. | Not a 32-bit form | Target unavailable |
| `CMPSQ` | Strings | Compare 64-bit string elements. | Not a 32-bit form | Target unavailable |
| `LODSQ` | Strings | Load 64-bit string elements. | Not a 32-bit form | Target unavailable |
| `STOSQ` | Strings | Store 64-bit string elements. | Not a 32-bit form | Target unavailable |
| `SCASQ` | Strings | Scan 64-bit string elements. | Not a 32-bit form | Target unavailable |
| `PUSHFD` | Flags | Push 32-bit flags. | Missing | Not a 64-bit form; target unavailable |
| `POPFD` | Flags | Restore 32-bit flags. | Missing | Not a 64-bit form; target unavailable |
| `SYSCALL` | System calls | Enter the OS system-call interface. | Not implemented by this backend | Target unavailable |

`SYSCALL` support currently belongs to Linux ELF64. Ordinary Windows programs use
imported APIs, such as `call [ExitProcess]`; a raw system-call encoding alone would
not provide a Windows calling convention or stable service numbers.

### Missing Windows x86 operand forms

A supported mnemonic does not mean all x86 forms are implemented.

| Area | Implemented today | Still missing |
| --- | --- | --- |
| `MOV` | Register destination with immediate/address or memory source. | Register copies, memory stores, size-qualified immediate stores, and 8/16-bit forms. |
| Arithmetic, bitwise, `CMP`, `TEST` | r32 destination with register or immediate/address source. | Memory source/destination forms and 8/16-bit forms. |
| `CALL` | Label or an absolute pointer location, including import slots. | Register-indirect calls and base/displacement memory-indirect calls. |
| `JMP` | Direct label. | Register-indirect and memory-indirect jumps. |
| `PUSH`, `POP` | Register stack operations; `PUSH` also accepts immediate/address. | Memory operands and additional operand widths. |
| Addresses | Supported absolute/base/displacement loads and `LEA`. | General scaled-index/base-plus-index forms and broader displacement expressions. |

## Linux ELF x64

### Supported on Linux ELF64 x64

Use `#TARGET linux-x64` or `--target linux-x64`. This is static Linux ELF output,
not Windows PE output or dynamic linking. See the [Linux x64 guide](../linux64.md).
`r32` and `r64` include the extended R8D-R15D and R8-R15 registers. Widths must
match. Arithmetic/test immediates for 64-bit destinations are sign-extended imm32.

| Mnemonic | Category | Used for | Supported ELF64 x64 operands |
| --- | --- | --- | --- |
| `MOV` | Data movement | Copy a value or load memory. | Matching r32/r64 register copies; register/immediate; loads and register-to-memory stores. |
| `LEA` | Addresses | Calculate an address without reading its contents. | r32/r64, [memory], including RIP-relative symbols. |
| `PUSH` | Stack | Push a value onto the stack. | r64 or sign-extended imm32. |
| `POP` | Stack | Pop a stack value into a register. | r64. |
| `ADD` | Arithmetic | Add the source to the destination. | r32/r64 destination; matching register or immediate source. |
| `SUB` | Arithmetic | Subtract the source from the destination. | r32/r64 destination; matching register or immediate source. |
| `AND` | Bitwise | Perform a bitwise AND. | r32/r64 destination; matching register or immediate source. |
| `OR` | Bitwise | Perform a bitwise OR. | r32/r64 destination; matching register or immediate source. |
| `XOR` | Bitwise | Perform a bitwise exclusive OR. | r32/r64 destination; matching register or immediate source. |
| `CMP` | Comparison | Set flags from subtraction without storing the result. | r32/r64 destination; matching register or immediate source. |
| `TEST` | Comparison | Set flags from AND without storing the result. | r32/r64 destination; matching register or immediate source. |
| `JMP` | Control flow | Jump unconditionally. | Label, r64, or supported memory-indirect operand. |
| `JZ` | Conditional branch | Jump if zero/equal. | Label; near rel32. |
| `JNZ` | Conditional branch | Jump if nonzero/not equal. | Label; near rel32. |
| `JZ8` | Conditional branch | Explicit short jump if zero/equal. | Label; range-checked rel8. |
| `JNZ8` | Conditional branch | Explicit short jump if nonzero/not equal. | Label; range-checked rel8. |
| `CALL` | Procedures | Save a return address and call a procedure. | Label, r64, or supported memory-indirect operand. |
| `RET` | Procedures | Return to the caller, optionally discarding stack bytes. | No operand or unsigned imm16 cleanup count. |
| `NOP` | Miscellaneous | Perform no operation. | No operands. |
| `INT` | Interrupts | Encode a software interrupt; execution depends on the OS. | imm8; use SYSCALL for native Linux x64 system calls. |
| `SYSCALL` | System calls | Invoke the native Linux x64 system-call interface. | No operands; registers carry the call number and arguments. |

### Unsupported on Linux ELF64 x64

These mnemonics have no working ELF64 encoder cases. This includes all ten
unimplemented instruction constants from the shared catalog: `ADC`, `SBB`, `MUL`,
`NOT`, `MOVSB`, `MOVSW`, `CMPSB`, `CMPSW`, `PUSHF`, and `POPF`.

| Mnemonic | Category | Used for |
| --- | --- | --- |
| `ADC` | Carry arithmetic | Add with carry. |
| `SBB` | Carry arithmetic | Subtract with borrow. |
| `INC` | Arithmetic | Increment by one. |
| `DEC` | Arithmetic | Decrement by one. |
| `NEG` | Arithmetic | Negate a signed value. |
| `MUL` | Arithmetic | Unsigned multiplication. |
| `IMUL` | Arithmetic | Signed multiplication. |
| `DIV` | Arithmetic | Unsigned division. |
| `IDIV` | Arithmetic | Signed division. |
| `MOVSX` | Data movement | Copy with sign extension. |
| `MOVZX` | Data movement | Copy with zero extension. |
| `XCHG` | Data movement | Exchange two values. |
| `CBW` | Sign extension | Sign-extend AL into AX. |
| `CWDE` | Sign extension | Sign-extend AX into EAX. |
| `CWD` | Sign extension | Sign-extend AX into DX:AX. |
| `CDQ` | Sign extension | Sign-extend EAX into EDX:EAX. |
| `NOT` | Bitwise | Invert every bit. |
| `BSWAP` | Bitwise | Reverse the byte order of a register. |
| `SHL` | Shifts | Logical left shift. |
| `SAL` | Shifts | Arithmetic left shift; alias of SHL. |
| `SHR` | Shifts | Logical right shift. |
| `SAR` | Shifts | Arithmetic right shift. |
| `SHLD` | Shifts | Double-precision left shift. |
| `SHRD` | Shifts | Double-precision right shift. |
| `ROL` | Rotates | Rotate left. |
| `ROR` | Rotates | Rotate right. |
| `RCL` | Rotates | Rotate left through carry. |
| `RCR` | Rotates | Rotate right through carry. |
| `JE` | Conditional branches | Jump on equal/zero. |
| `JNE` | Conditional branches | Jump on not equal/nonzero. |
| `JG` | Conditional branches | Jump on greater, signed. |
| `JGE` | Conditional branches | Jump on greater or equal, signed. |
| `JL` | Conditional branches | Jump on less, signed. |
| `JLE` | Conditional branches | Jump on less or equal, signed. |
| `JA` | Conditional branches | Jump on above, unsigned. |
| `JAE` | Conditional branches | Jump on above or equal, unsigned. |
| `JB` | Conditional branches | Jump on below, unsigned. |
| `JBE` | Conditional branches | Jump on below or equal, unsigned. |
| `JO` | Conditional branches | Jump on overflow. |
| `JNO` | Conditional branches | Jump on no overflow. |
| `JS` | Conditional branches | Jump on negative sign. |
| `JNS` | Conditional branches | Jump on nonnegative sign. |
| `JP` | Conditional branches | Jump on even parity. |
| `JNP` | Conditional branches | Jump on odd parity. |
| `JC` | Conditional branches | Jump if carry is set; alias of JB. |
| `JNC` | Conditional branches | Jump if carry is clear; alias of JAE. |
| `SETE` | Conditional results | Store 1 on equal/zero; otherwise store 0. |
| `SETNE` | Conditional results | Store 1 on not equal/nonzero; otherwise store 0. |
| `SETG` | Conditional results | Store 1 on greater, signed; otherwise store 0. |
| `SETGE` | Conditional results | Store 1 on greater or equal, signed; otherwise store 0. |
| `SETL` | Conditional results | Store 1 on less, signed; otherwise store 0. |
| `SETLE` | Conditional results | Store 1 on less or equal, signed; otherwise store 0. |
| `SETA` | Conditional results | Store 1 on above, unsigned; otherwise store 0. |
| `SETAE` | Conditional results | Store 1 on above or equal, unsigned; otherwise store 0. |
| `SETB` | Conditional results | Store 1 on below, unsigned; otherwise store 0. |
| `SETBE` | Conditional results | Store 1 on below or equal, unsigned; otherwise store 0. |
| `SETO` | Conditional results | Store 1 on overflow; otherwise store 0. |
| `SETNO` | Conditional results | Store 1 on no overflow; otherwise store 0. |
| `SETS` | Conditional results | Store 1 on negative sign; otherwise store 0. |
| `SETNS` | Conditional results | Store 1 on nonnegative sign; otherwise store 0. |
| `SETP` | Conditional results | Store 1 on even parity; otherwise store 0. |
| `SETNP` | Conditional results | Store 1 on odd parity; otherwise store 0. |
| `CMOVE` | Conditional moves | Copy on equal/zero. |
| `CMOVNE` | Conditional moves | Copy on not equal/nonzero. |
| `CMOVG` | Conditional moves | Copy on greater, signed. |
| `CMOVGE` | Conditional moves | Copy on greater or equal, signed. |
| `CMOVL` | Conditional moves | Copy on less, signed. |
| `CMOVLE` | Conditional moves | Copy on less or equal, signed. |
| `CMOVA` | Conditional moves | Copy on above, unsigned. |
| `CMOVAE` | Conditional moves | Copy on above or equal, unsigned. |
| `CMOVB` | Conditional moves | Copy on below, unsigned. |
| `CMOVBE` | Conditional moves | Copy on below or equal, unsigned. |
| `CMOVO` | Conditional moves | Copy on overflow. |
| `CMOVNO` | Conditional moves | Copy on no overflow. |
| `CMOVS` | Conditional moves | Copy on negative sign. |
| `CMOVNS` | Conditional moves | Copy on nonnegative sign. |
| `CMOVP` | Conditional moves | Copy on even parity. |
| `CMOVNP` | Conditional moves | Copy on odd parity. |
| `LOOP` | Loops | Decrement the counter and jump if nonzero. |
| `LOOPE` | Loops | Loop while the counter is nonzero and ZF is set. |
| `LOOPZ` | Loops | Alias of LOOPE. |
| `LOOPNE` | Loops | Loop while the counter is nonzero and ZF is clear. |
| `LOOPNZ` | Loops | Alias of LOOPNE. |
| `JECXZ` | Loops | Jump if ECX is zero. |
| `BT` | Bit operations | Copy a selected bit to the carry flag. |
| `BTS` | Bit operations | Test and set a bit. |
| `BTR` | Bit operations | Test and reset a bit. |
| `BTC` | Bit operations | Test and complement a bit. |
| `BSF` | Bit operations | Find the lowest set bit. |
| `BSR` | Bit operations | Find the highest set bit. |
| `POPCNT` | Bit operations | Count set bits. |
| `ENTER` | Stack frames | Create a stack frame. |
| `LEAVE` | Stack frames | Release a stack frame. |
| `PUSHF` | Flags | Push flags, with operand-size rules. |
| `POPF` | Flags | Restore flags, subject to operand-size and privilege rules. |
| `CLC` | Flags | Clear carry. |
| `STC` | Flags | Set carry. |
| `CMC` | Flags | Complement carry. |
| `CLD` | Flags | Clear the string direction flag. |
| `STD` | Flags | Set the string direction flag. |
| `LAHF` | Flags | Load status flags into AH. |
| `SAHF` | Flags | Store AH into status flags. |
| `MOVSB` | Strings | Copy a string element. Element width: 8 bits. |
| `MOVSW` | Strings | Copy a string element. Element width: 16 bits. |
| `MOVSD` | Strings | Copy a string element. Element width: 32 bits. |
| `CMPSB` | Strings | Compare string elements. Element width: 8 bits. |
| `CMPSW` | Strings | Compare string elements. Element width: 16 bits. |
| `CMPSD` | Strings | Compare string elements. Element width: 32 bits. |
| `LODSB` | Strings | Load a string element into the accumulator. Element width: 8 bits. |
| `LODSW` | Strings | Load a string element into the accumulator. Element width: 16 bits. |
| `LODSD` | Strings | Load a string element into the accumulator. Element width: 32 bits. |
| `STOSB` | Strings | Store the accumulator into a string element. Element width: 8 bits. |
| `STOSW` | Strings | Store the accumulator into a string element. Element width: 16 bits. |
| `STOSD` | Strings | Store the accumulator into a string element. Element width: 32 bits. |
| `SCASB` | Strings | Compare the accumulator with a string element. Element width: 8 bits. |
| `SCASW` | Strings | Compare the accumulator with a string element. Element width: 16 bits. |
| `SCASD` | Strings | Compare the accumulator with a string element. Element width: 32 bits. |
| `JRCXZ` | Loops | Jump if RCX is zero. |
| `MOVSXD` | Sign extension | Sign-extend a doubleword into a 64-bit register. |
| `CDQE` | Sign extension | Sign-extend EAX into RAX. |
| `CQO` | Sign extension | Sign-extend RAX into RDX:RAX. |
| `PUSHFQ` | Flags | Push 64-bit flags. |
| `POPFQ` | Flags | Restore 64-bit flags, subject to OS restrictions. |
| `MOVSQ` | Strings | Copy 64-bit string elements. |
| `CMPSQ` | Strings | Compare 64-bit string elements. |
| `LODSQ` | Strings | Load 64-bit string elements. |
| `STOSQ` | Strings | Store 64-bit string elements. |
| `SCASQ` | Strings | Scan 64-bit string elements. |

Legacy `PUSHA`/`PUSHAD`, `POPA`/`POPAD`, `PUSHFD`/`POPFD`, and `JCXZ` forms are not
long-mode instruction forms to port into this target. Use individually supported
64-bit register operations where appropriate; flag-stack support remains future
work. Other condition aliases also remain unimplemented.

### Missing ELF x64 operand forms

| Area | Implemented today | Still missing |
| --- | --- | --- |
| `MOV` | Matching-width register copies, immediate loads, memory loads and register stores. | Size-qualified immediate stores, 8/16-bit forms, and SIMD moves. |
| Arithmetic, bitwise, `CMP`, `TEST` | r32/r64 destination with matching register or immediate source. | Memory operands and 8/16-bit forms. |
| Stack | `PUSH r64`, `POP r64`, `PUSH` sign-extended imm32. | Memory operands and broader stack instruction coverage. |
| Addresses | Base plus signed numeric displacement, RIP-relative symbols, RSP/R12 base encoding. | Scaled-index/base-plus-index forms and general expressions. |

Data remains inline in a read-only executable section. Memory-store instruction
support does not make inline data writable; use suitable writable storage such as
the process stack. Writable sections/BSS are separate roadmap items.

## Data Directives and Prefixes

Data directives emit bytes; they are not CPU instructions.

| Directive | Category | Used for | PE32 x86 | PE32+ x64 | ELF64 x64 |
| --- | --- | --- | --- | --- | --- |
| `DB` | Data | Bytes or UTF-8 strings. | Supported | Target unavailable | Supported |
| `DW` | Data | 16-bit integer values. | Supported | Target unavailable | Supported |
| `DD` | Data | 32-bit integer values. | Supported | Target unavailable | Supported |
| `DQ` | Data | 64-bit integer values. | Missing | Target unavailable | Supported, numeric values |

Instruction prefixes also require parsing, validation, and encoding support.

| Prefix | Category | Used for | PE32 x86 | PE32+ x64 | ELF64 x64 |
| --- | --- | --- | --- | --- | --- |
| `REP` | String repetition | Repeat an applicable string instruction using the counter. | Missing | Target unavailable | Missing |
| `REPE` / `REPZ` | String repetition | Repeat applicable compare/scan operations while equal/zero. | Missing | Target unavailable | Missing |
| `REPNE` / `REPNZ` | String repetition | Repeat applicable compare/scan operations while unequal/nonzero. | Missing | Target unavailable | Missing |

## Floating-Point, SIMD, and Other Extensions

No floating-point or vector instruction family currently has working encoder
support in either output backend. The examples below identify broader work rather
than promise a complete implementation of each extension.

| Family | Example unsupported mnemonics | Category / use |
| --- | --- | --- |
| x87 | `FLD`, `FST`, `FSTP`, `FADD`, `FSUB`, `FMUL`, `FDIV`, `FILD`, `FISTP` | Stack-based floating-point arithmetic and conversions. |
| MMX | `MOVQ`, `PADDW`, `PADDD`, `PAND`, `POR`, `PXOR` | Packed integer operations. |
| SSE | `MOVAPS`, `MOVUPS`, `ADDPS`, `ADDPD`, `MULPS`, `DIVPS` | Scalar/packed floating-point and vector operations. |
| AVX / AVX-512 | `VMOVAPS`, `VADDPS`, `VMULPS`, `VDIVPS` | Wider vector operations and additional operand/prefix encodings. |
| Additional CPU features | `CPUID`, `RDTSC`, `CMPXCHG`, `AESENC` | Feature queries, timestamps, atomic operations, and cryptography. |

CPU availability, execution mode, and OS permissions must be considered when
choosing future instruction forms; a lookup-table entry alone is insufficient.

## Related Assembly Documentation

- [Assembly overview](ZASM-Intro.md): selected Windows x86 encodings and basic concepts.
- [Full ZASM reference](ZASM-Reference.md): syntax, operands, registers, labels, imports, and calling conventions.
- [Linux x64 and ELF64](../linux64.md): AMD64 operands, syscall examples, and executable layout.
- [Include files](ZASM-Include-Files.md): shared headers, search paths, and source loading.
- [Examples](ZASM-Examples.md) and [string concatenation tutorial](../tutorial-zasm-concat.md): working assembly programs.
- [Feature roadmap](ZASM-Coming-Soon.md): implemented capabilities and proposed priorities.
- [vNext sandbox](ZASM-vNext-Sandbox.md): experimental syntax proposals, separate from current support.

## Maintaining This Inventory

Check [X86Encoder.cs](https://github.com/SuessLabs/ZeroCompiler/blob/master/source/Zero/Targets/X86/X86Encoder.cs),
[X64Encoder.cs](https://github.com/SuessLabs/ZeroCompiler/blob/master/source/Zero/Targets/X64/X64Encoder.cs),
[AssemblyData.cs](https://github.com/SuessLabs/ZeroCompiler/blob/master/source/Zero/Assembly/AssemblyData.cs), and
[target selection](https://github.com/SuessLabs/ZeroCompiler/blob/master/source/Zero/Assembly/Assembler.cs) when updating support.
Add encoding tests and, where appropriate, runtime tests before marking a form
supported. Update this page, the operand reference, the roadmap, and working
examples together.
