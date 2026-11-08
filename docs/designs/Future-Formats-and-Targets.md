# Future Executable Formats and Targets/Architecture

**Date:** 2026-11-07 - _Damian Suess_

The next stage of Zero's development will preserve a shared parsing core while separating the components that depend on processor architecture, executable format, and operating system. This will allow the compiler to grow beyond Windows x86 without duplicating its parser or tying every backend to PE output.

For now, `Zero.Cli` will remain separate from the `Zero` compiler library; _though subject to change_. The CLI will handle command-line arguments, file operations, console messages, and exit codes. The library will expose compilation functionality that can also be used by tests, editors, or other applications.

The final command name remains independent of this structure. The CLI's `AssemblyName` setting can produce either `zero.exe` or `zcc.exe`:

```xml
<AssemblyName>zcc</AssemblyName>
```

If both command names become useful, an alias or wrapper can provide the second name without requiring another compiler implementation.

## Separate Architecture, Format, and Platform Decisions

ELF and ARM represent different parts of a compilation target. ELF defines a binary file format; ARM defines a processor architecture. Zero will treat these as separate dimensions, alongside operating-system and ABI requirements.

The following combinations illustrate the intended model:

| Target | Processor architecture | Executable format | Platform conventions |
|---|---|---|---|
| Windows x86 | x86 | PE32 | Windows APIs and x86 calling conventions |
| Windows x64 | x86-64 | PE32+ | Windows x64 ABI |
| Windows ARM64 | ARM64 | PE32+ | Windows ARM64 ABI |
| Linux x64 | x86-64 | ELF64 | Linux x64 ABI |
| Linux ARM64 | ARM64 | ELF64 | Linux ARM64 ABI |

This separation will allow an instruction backend to support multiple executable formats where the platform permits it. Zero will validate supported combinations rather than assume that every combination is meaningful.

## Keeping The Initial Project Structure Manageable

I will retain the existing three projects and establish the new boundaries through folders and namespaces first:

```text
source/
├── Zero/
│   ├── Parsing/
│   ├── Syntax/
│   ├── Diagnostics/
│   ├── Compilation/
│   ├── Symbols/
│   ├── Targets/
│   │   ├── X86/
│   │   └── Arm64/
│   ├── Formats/
│   │   ├── Pe/
│   │   └── Elf/
│   └── Platforms/
│       ├── Windows/
│       └── Linux/
├── Zero.Cli/
└── Zero.Tests/
```

These folders describe responsibilities, including future components; they do not imply that every backend must be implemented immediately.

| Component | Responsibility |
|---|---|
| Parsing | Read tokens, labels, directives, and instruction syntax |
| Syntax | Represent parsed instructions and operands |
| Diagnostics | Report errors and warnings with source locations |
| Compilation | Coordinate stages and compilation options |
| Symbols | Represent sections, symbols, and relocation requests |
| Targets | Validate and encode architecture-specific instructions |
| Formats | Write executable headers, sections, and format-specific metadata |
| Platforms | Define entry-point conventions, external calls, and ABI requirements |
| CLI | Accept user input, invoke compilation, and report results |

If individual components grow substantially, I can extract projects such as `Zero.Targets.Arm64` or `Zero.Formats.Elf` later.

## Establish a Staged Compilation Pipeline

The current assembler combines parsing, x86 encoding, PE layout, and import-address resolution. My first architectural step will be to separate those responsibilities:

```text
Source text
    ↓
Shared parser
    ↓
Parsed instructions, operands, labels, and directives
    ↓
Architecture-specific validation and encoding
    ↓
Sections, symbols, and relocation records
    ↓
Executable layout and address resolution
    ↓
PE or ELF output
```

Relocation records will provide an important boundary. An instruction encoder should be able to request the address of a symbol without knowing its final location. The layout and linking stages will resolve that request. Windows import-table details, including IAT addresses, will remain within the PE and Windows-specific components.

## Preserving Shared Syntax Without Assuming Portable Assembly

The shared parser will handle common syntax. Each architecture backend will validate its own registers, instructions, and operand rules.

Existing `.zasm` programs will remain target-specific where they use processor instructions or platform APIs. For example, a program using `eax` and `WriteFile` cannot become a Linux ARM64 program simply by selecting ELF output.

For the future higher-level Zero language, I plan to introduce a shared intermediate representation that can feed different architecture backends.

## Move Forward Incrementally

The implementation sequence will be:

1. Separate parsing from x86 encoding while preserving current behavior.
2. Separate PE writing and address resolution from instruction encoding.
3. Introduce explicit target options and reject unsupported combinations.
4. Return structured compilation results and diagnostics from the library.
5. Add another format or architecture against these boundaries.

Backend registration will remain explicit to support Native AOT without depending on runtime assembly discovery.

Throughout this work, I will distinguish the platform that runs the compiler from the platform it generates code for. Publishing `zcc` with `-Runtime win-arm64` produces an ARM64 compiler application; generating ARM64 machine code requires a separate ARM64 backend.
