# Debugger development and testing

[Debugger guide](index.md) · [Architecture](architecture.md) · [Windows engine](winpe.md)

## Automated checks

```powershell
dotnet test source/Zero.Dbg.Tests/Zero.Dbg.Tests.csproj -c Release
dotnet test source/Zero.Ide.Tests/Zero.Ide.Tests.csproj -c Release
dotnet test source/Zero.Tests/Zero.Tests.csproj -c Release
```

The debugger tests use real compiler output, not simulated CPU state. Native
integration tests run on Windows x64 and are skipped on other hosts. Tests cover
instruction stepping, stepping over calls, repeated breakpoint hits, breakpoint
removal, included-source locations, pause/stop and source-map validation. The
cross-platform map tests run on every supported development host.

The Native AOT smoke host exercises source-generated map serialization, launch,
breakpoint installation, stepping, register reads and exit using a published
native executable:

```powershell
dotnet publish source/Zero.Dbg.Tests/AotSmoke/Zero.Dbg.AotSmoke.csproj -c Release -r win-x64 -o output/debugger-aot
./output/debugger-aot/Zero.Dbg.AotSmoke.exe
```

Keep native process tests bounded with timeouts and stop/dispose each session
before removing its fixture files. The debug loop must stay on its creating OS
thread; replacing that thread with arbitrary asynchronous continuations violates
the Windows API ownership requirements.

## Manual IDE check

Use a saved Windows ZASM console program that calls `ExitProcess`:

```asm
#IMPORT kernel32.dll, ExitProcess
mov eax, 1
call increment
add eax, 3
push 0
call [ExitProcess]
increment: add eax, 2
ret
```

1. F9 on `add eax, 3`, then F5. Verify the initial entry highlight.
2. F10 over `mov`: EAX should become 1.
3. F10 over `call`: execution should stop at `add eax, 3`, with EAX equal to 3.
4. F10 again: EAX should become 6.
5. F5: verify normal exit, no active highlight, and editable source.
6. Repeat with a breakpoint in an included `.zh` file. Verify the correct tab
   and original line open when the breakpoint is reached.
7. Run a loop, use the toolbar Pause button, then Shift+F5. Verify the process
   stops and the toolbar returns to Play.

Check both docked and floating editor windows; they have separate key-binding
routes. Verify that comments/data show unbound breakpoint outlines and do not
acquire software breakpoints.

While paused, move **Debugger — Locals** to the left, into the center document
tabs, and into a floating window. Verify the register tree remains populated,
switch between code and debugger tabs, then step again. Stop and restart the
session: the debugger should hide and restore at its last position. Headless IDE
tests exercise these rendered docking transitions in Light and Dark themes and
verify that editor and debugger controls are never recycled across view types.

## Extending the debugger

Add platform engines behind `IDebugSession`. Keep Avalonia references in the IDE,
not in `Zero.Dbg` or `Zero.Dbg.WinPe`. Preserve the source-generated serializer
and native interop approach when adding map fields or native APIs. Avoid
`System.Linq`, reflection-based serialization and dynamic code generation in the
debugger libraries. AOT compatibility is a build property to verify, not a
guarantee that every future dependency is safe to trim.
