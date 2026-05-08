# Zero - Assembly Compiler

The Zero compiler project assembly language support.

## Instructions

* `NOP` → `90`
* `RET` → `C3`
* `INT imm8` → `CD ib` (e.g., INT 3)
* `MOV r32, imm32` → `B8+r ib, id`
* `PUSH r32` → `50+r`
* `POP r32` → `58+r`
* `PUSH imm32` → `68 id`
* `ADD r32, imm32` → `81 /0 r, id`
* `SUB r32, imm32` → `81 /5 r, id`
* `CMP r32, imm32` → `81 /7 r, id`
* `JMP label` → `E9 rel32`
* `CALL label` → `E8 rel32`
* `JZ label` → `0F 84 rel32`
* `JNZ label` → `0F 85 rel32`

## Registers

EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI

## Directives

* `db <byte>[, ...]` (supports decimal or 0x..)
* `dw <word>[, ...]`
* `dd <dword>[, ...]`

## Other

* ; comment support
* label: creates a label at current offset
