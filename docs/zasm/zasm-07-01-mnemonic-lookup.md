---
layout: default
title: "Appendix I — Mnemonic Lookup"
---

# Appendix I — Mnemonic Lookup

Appendices

[Previous](zasm-06-05-what-is-arm.md) · [Contents](index.md) · [Next](zasm-07-02-ascii-character-set.md)

This compact **Windows x86** table summarizes operations used in the book.
It does not enumerate every addressing restriction.

| Mnemonic | Purpose | Typical supported form |
| --- | --- | --- |
| `MOV` | Load a value | `mov eax, 42`; `mov eax, [value]` |
| `LEA` | Calculate an address | `lea esi, [esp]` |
| `PUSH` | Push a stack value | `push eax`; `push 42` |
| `POP` | Pop a stack value | `pop eax` |
| `ADD`, `SUB` | Integer arithmetic | `add eax, 4`; `sub eax, ebx` |
| `AND`, `OR`, `XOR` | Bitwise operations | `and eax, 0xFF`; `xor eax, eax` |
| `CMP` | Compare without storing | `cmp eax, 42` |
| `TEST` | Test bits without storing | `test eax, 4` |
| `JMP` | Unconditional direct branch | `jmp start` |
| `JZ`, `JNZ` | Branch on zero flag | `jz done`; `jnz again` |
| `JZ8`, `JNZ8` | Explicit short branch | `jz8 nearby` |
| `CALL` | Call a routine | `call calculate`; `call [puts]` |
| `RET` | Return with optional cleanup | `ret`; `ret 16` |
| `NOP` | No operation | `nop` |
| `INT` | Software interrupt | `int 3` |

`INT` depends on OS permissions and behavior; it is not the Windows console
interface. Example labels and memory locations must exist in a complete program.

Linux x64 adds `SYSCALL`, 64-bit/extended registers, and broader movement and
indirect branch forms. For exact support and missing instructions, use the
[mnemonic inventory](ZASM-Mnemonics.md) and [Linux operand guide](../linux64.md).
`DB`, `DW`, `DD`, and Linux numeric `DQ` are data directives, not CPU instructions.

---

[Previous](zasm-06-05-what-is-arm.md) · [Contents](index.md) · [Next](zasm-07-02-ascii-character-set.md)
