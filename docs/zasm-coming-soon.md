---
layout: default
title: ZASM Feature Roadmap
---

# ZASM Feature Roadmap

[Documentation home](index.md) · [Language reference](zasm-reference.md) ·
[Basic examples](zasm-examples.md)

This page tracks what Zero Assembly can do today and the features to consider
next. **Implemented** describes working capabilities. **What's Next** lists
proposed priorities for a more complete assembler. **Nice to Haves** collects
optional improvements. Unchecked items are Coming Soon placeholders, not release
commitments; their syntax and implementation order still need to be decided.

## Implemented

Support applies to the operand forms in the [language reference](zasm-reference.md),
not every form offered by other x86 assemblers. A mnemonic or register constant
in the compiler source does not by itself mean its instruction encoding works.

### Instructions and Registers

- [x] Windows x86 (32-bit) instruction encoding and PE32 executable output.
- [x] General-purpose registers: `EAX`, `EBX`, `ECX`, `EDX`, `ESI`, `EDI`, `EBP`, `ESP`.
- [x] `MOV` from an immediate/address or 32-bit memory into a register.
- [x] `LEA` for address calculation.
- [x] `PUSH` of a register or immediate/address; `POP` into a register.
- [x] `ADD`, `SUB`, `AND`, `OR`, `XOR`, `CMP`, and `TEST` with register or immediate/address sources.
- [x] `JMP`, `JZ`, and `JNZ` to labels; explicit short forms `JZ8` and `JNZ8` with range checks.
- [x] Direct `CALL` to a label and indirect `CALL` through an absolute pointer location, including DLL import slots.
- [x] `RET`, optional `RET imm16` argument cleanup, `NOP`, and `INT imm8`.
- [x] Memory addressing for loads and `LEA`: absolute addresses/symbols, register bases, and base-plus-displacement forms.

### Source, Data, and Symbols

- [x] Intel-style operand order, comma-separated operands, and case-insensitive mnemonics/registers/labels.
- [x] Semicolon comments, including trailing comments outside quoted strings.
- [x] Decimal integers and hexadecimal integers using `0x` or an `h` suffix.
- [x] `DB` byte values and UTF-8 strings; `DW` and `DD` integer values.
- [x] Named byte declarations with or without a colon, such as `message db 'Hello', 0`.
- [x] Labels, forward references, duplicate-name checks, and label/import collision checks.
- [x] Quoted strings with doubled-quote escaping and literal backslashes.

Data is currently inline in the read-only code section. Named declarations
identify addresses; they do not provide writable high-level variables. The first
emitted byte is the program entry point.

### Headers, Imports, and Compiler Tools

- [x] `using "file.zh"` with recursive loading and one inclusion per normalized path per compilation.
- [x] Include search order: working directory, ordered `-I`/`--include` paths, then `include` beside the compiler.
- [x] Include-cycle diagnostics, searched-path errors, and original filename/line reporting for included source.
- [x] Shipped `console.zh` and `windows.zh` headers.
- [x] `#IMPORT dll, function[, ...]` declarations and PE import table generation.
- [x] Plain imported function names such as `call [puts]`, with backward-compatible and DLL-qualified aliases.
- [x] `zero.exe` and `zcc.exe`, compile-only mode, and compile-and-execute `run` mode.
- [x] Native AOT publishing with headers and samples beside the compiler.
- [x] Build-time sample compilation, sample discovery in unit tests, and selected Windows runtime checks.

See the [include guide](Zero-Assembly-Include-Files.md),
[build guide](building.md), and [sample guide](https://github.com/SuessLabs/ZeroCompiler/blob/master/samples/readme.md).

## What's Next

These proposed priorities address common gaps when writing ordinary assembly
programs. Each item needs a defined syntax, validation, tests, and documentation
before it moves to Implemented.

### Complete Everyday Instruction Forms

- [ ] **Register copies and memory stores:** register-to-register `MOV` and register/immediate-to-memory forms.
- [ ] **Arithmetic on memory:** supported memory source/destination combinations with explicit operand-size rules.
- [ ] **Multiplication and division:** `MUL`, `IMUL`, `DIV`, `IDIV`, and the sign-extension instructions their forms require.
- [ ] **Additional integer operations:** `INC`, `DEC`, `NEG`, `NOT`, `ADC`, and `SBB`.
- [ ] **Shifts and rotates:** logical/arithmetic shifts and rotations, with validated count operands.
- [ ] **More conditions:** signed and unsigned conditional branches, plus familiar aliases such as `JE` and `JNE`.
- [ ] **Indirect control flow:** calls and jumps through registers and more memory-address forms.

### Operand Sizes, Addresses, and Expressions

- [ ] **8-bit and 16-bit forms:** real encodings for subregisters, prefixes, and width checks.
- [ ] **Size-qualified memory:** an unambiguous way to state byte, word, or double-word operations when no register supplies the width.
- [ ] **Full x86 addressing:** negative displacements, base-plus-index, and scaled-index forms.
- [ ] **Consistent signed immediates:** accept negative decimal operands where the instruction permits them.
- [ ] **Constant expressions:** arithmetic, bitwise expressions, symbol-plus-offset values, and clear precedence rules.
- [ ] **Range validation:** consistent errors for values that do not fit an operand or data width.

### Writable Storage and Program Layout

- [ ] **Sections:** separate executable code, read-only data, writable data, and uninitialized storage.
- [ ] **Storage directives:** reserve byte/word/double-word buffers and support repeated data values.
- [ ] **Alignment:** align data and code with explicit padding rules.
- [ ] **Wider data:** `DQ` and clearly specified wider integer/string representations.
- [ ] **Symbol initializers:** allow suitable data directives to contain addresses and expressions.
- [ ] **Entry-point selection:** explicitly select a label instead of always starting at the first emitted byte.
- [ ] **Subsystem selection:** choose Windows console or GUI output independently of which APIs the program uses.

### Reuse and Larger Programs

- [ ] **Named constants:** equates or definitions shared by instructions and headers.
- [ ] **Conditional assembly:** include source based on definitions or target settings.
- [ ] **Macros:** reusable instruction sequences with parameter and error-location rules.
- [ ] **Structured data:** layouts and named field offsets for Windows API structures and application data.
- [ ] **Scoped symbols:** local labels and a naming strategy for shared headers and procedures.
- [ ] **Assembly objects and linking:** define an object format, relocations, external symbols, and multi-file linking.
- [ ] **Exports and libraries:** define DLL exports and supported library output modes.

### Diagnostics and Reference Coverage

- [ ] **Operand validation:** reject unsupported register widths/forms consistently rather than accepting names that encode incorrectly.
- [ ] **Helpful errors:** show the failing source text, expected operands, and useful suggestions.
- [ ] **Feature examples:** add a sample and appropriate encoding/runtime tests for each newly supported feature.
- [ ] **Complete reference:** document instruction effects, flags, widths, address forms, and calling-convention examples as support grows.

## Nice to Haves

These ideas can follow the core assembler work or be developed independently.

### Convenience and Productivity

- [ ] **Automatic branch sizing:** select short or near branches when possible.
- [ ] **More literal formats:** binary numbers and readable digit separators.
- [ ] **Procedure helpers:** optional syntax for stack frames, parameters, and register preservation.
- [ ] **Calling-convention helpers:** optional argument/call helpers with explicit ABI rules.
- [ ] **String instructions:** `MOVSB`, `MOVSW`, `CMPSB`, `CMPSW`, and useful repeat prefixes.
- [ ] **Flag-stack operations:** `PUSHF`/`POPF` forms with documented operand sizes and OS restrictions.
- [ ] **Floating-point and SIMD:** selected x87/SSE instructions and matching data/operand support.
- [ ] **More standard headers:** broader Win32 imports, named constants, structure layouts, and reusable helper routines.

### Tooling and Learning

- [ ] **Listings and maps:** show emitted bytes, addresses, source lines, and symbol locations.
- [ ] **Debug information:** debugger-friendly symbols and source mappings in generated programs.
- [ ] **Editor support:** syntax highlighting, completion, navigation, and inline diagnostics.
- [ ] **Project configuration:** define `.zprj` settings for inputs, paths, output, and target selection.
- [ ] **More tutorials:** procedures, loops, writable buffers, structures, callbacks, and Windows message handling.
- [ ] **Compatibility guide:** explain differences from MASM, NASM, and FASM without promising source compatibility.

### More Targets and Executable Formats

- [ ] **Windows x64:** 64-bit instructions, PE32+, and the Windows x64 ABI.
- [ ] **ARM/ARM64:** dedicated instruction encoders and platform calling conventions.
- [ ] **ELF:** a separate executable-format writer, target combinations, imports/linking, and OS startup conventions.
- [ ] **Additional platforms:** define supported architecture, format, and ABI combinations before exposing target options.

Changing the compiler's own host runtime does not change its generated target.
See [Future formats and architectures](designs/Future-Formats-and-Targets.md)
for the shared-parser design direction.

## Updating This Roadmap

When a feature is ready, move it into Implemented, describe its supported forms
in the [reference](zasm-reference.md), and link a working example. A feature can
be partially implemented: state which forms work and leave the remaining forms
unchecked. Proposed names such as constants, section directives, and procedure
helpers need a syntax decision; this page does not establish their spelling.

Higher-level Zero (`.zsrc`) has its own language-design work and is outside this
assembly checklist. The [homepage's Coming Soon section](index.md#coming-soon)
also tracks broader language and documentation topics.
