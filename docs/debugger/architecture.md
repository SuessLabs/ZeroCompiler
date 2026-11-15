# Debugger architecture and source maps

[Debugger guide](index.md) · [Windows engine](winpe.md)

## Components

| Component | Responsibility |
| --- | --- |
| `Zero` compiler | Emit executable bytes and instruction-to-source metadata. |
| `Zero.Dbg` | `IDebugSession`, immutable event snapshots, breakpoints, registers and versioned JSON maps. |
| `Zero.Dbg.WinPe` | Launch/control PE32 processes through the Windows debug APIs. |
| `Zero.Ide.Services.IdeDebugSession` | Marshal native-thread snapshots onto Avalonia's dispatcher. |
| `Zero.Ide` views and view models | Editor markers, execution highlight, commands and register tool. |

The shared and Windows libraries have no Avalonia dependency, no `System.Linq`
usage, and declare `IsAotCompatible`. Native interop uses source-generated
`LibraryImport` methods. JSON uses an explicit `JsonSerializerContext`; neither
serialization nor native calls require runtime code generation.

## Build pipeline

`SourceLoader` expands includes into `SourceLine` records carrying original file
paths and line numbers. Comment removal preserves line counts. The assembler
computes instruction sizes and section-relative offsets, then the executable
writer supplies the preferred image base and code virtual address.

For a debug build, the assembler records:

```text
instruction RVA = code virtual address - preferred image base + instruction offset
runtime address = actual process image base + instruction RVA
```

The final map translates expanded line numbers back to the original file and
line, including instructions in `.zh` files. Only executable instructions receive
entries; `DB`, `DW`, `DD` and `DQ` emit bytes but are excluded from breakpoints.
Normal builds omit metadata collection and serialization.

The CLI option `--debug` writes `<output>.zdbg.json`. The IDE requests this option
for Debug builds, stages the executable and sidecar together, and moves both to
their final `.zeroide/Debug/...` names. Release builds remove stale sidecars.
Starting the debugger always builds Debug, independently of the toolbar's normal
build configuration selection.

## Version 1 sidecar

The JSON object contains:

| Field | Meaning |
| --- | --- |
| `Version` | Format version, currently `1`. |
| `Target` | Canonical target name, such as `windows-x86`. |
| `ExecutableHash` | SHA-256 of the executable's exact bytes. |
| `PreferredImageBase` | Image base chosen by the executable writer. |
| `EntryRva` | Module-relative executable entry point. |
| `Sources` | Absolute source paths and SHA-256 hashes, including include-only files. |
| `Instructions` | Ordered file path, original line, RVA, size and `IsCall` flag. |

Before launching, the debugger verifies the map version, executable hash, source
hashes, instruction ranges, PE architecture and executable entry point. Mismatched
or missing files require a rebuild. This avoids silently assigning breakpoints
to an older binary or a different version of a header.

Source lookup for breakpoint binding matches an instruction's exact original
file and line. Address lookup uses binary search over ordered instruction RVAs.
Paths follow host case-sensitivity rules. Unbound breakpoints are retained in the
editor, allowing a later build to resolve them.

## Session contract

`LaunchAsync` completes at the initial entry stop, or faults if startup fails.
`Continue`, `Pause`, `StepOver` and `SetBreakpoints` enqueue requests. `StopAsync`
completes once the debug loop has released the launched process and native
handles. A session can launch again after completion. `DisposeAsync` stops it.

`Changed` carries an immutable `DebugSnapshot` describing Starting, Running,
Paused, Exited or Faulted state. A paused snapshot contains a thread ID,
instruction pointer, optional source instruction, and register values. The
native engine raises events on its dedicated debug thread. Consumers must marshal
them to their UI thread; `IdeDebugSession` does this for Avalonia.

The interface leaves room for an ELF backend or a DAP adapter later. Neither is
required by the current direct IDE-to-debugger integration.
