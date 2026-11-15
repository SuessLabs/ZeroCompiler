# Windows PE32 engine

[Debugger guide](index.md) · [Architecture](architecture.md) · [Tests](testing.md)

## Native ownership and event loop

`WinPeDebugSession` owns a dedicated managed thread. That same OS thread calls
`CreateProcessW` with `DEBUG_ONLY_THIS_PROCESS`, receives events through
`WaitForDebugEventEx`, and releases them using `ContinueDebugEvent`. It never
blocks Avalonia's dispatcher. Debug requests arrive through a concurrent queue
and an event; the running event loop polls with a bounded wait.

`CREATE_PROCESS_DEBUG_EVENT` supplies the actual module base, allowing source-map
RVAs to be resolved into process addresses. Process/thread and DLL events are
tracked. File handles supplied by create-process and DLL-load events are closed
by the engine. Windows owns the lifetime of event-supplied process/thread handles;
the separately returned `CreateProcessW` handles are closed by the engine.

The interop structures explicitly use the Windows x64 ABI. x86 register state is
read and written through `Wow64GetThreadContext`/`Wow64SetThreadContext` with
`WOW64_CONTEXT_FULL`. Host architecture is checked before native interop begins.

## Software breakpoints

Each breakpoint address retains its original byte, installation state, user
ownership and optional temporary ownership. Multiple purposes at the same
address share one record, so removing a temporary breakpoint cannot remove a
user breakpoint.

1. Bind source lines to instruction starts in the validated map.
2. Write the one-byte x86 `INT3` opcode (`0xCC`) at each selected address and
   flush the process's instruction cache.
3. On an owned breakpoint exception, restore the original byte and move EIP back
   to the original instruction address. All threads remain stopped while the
   corresponding debug event is held.
4. On resume, suspend the other known threads and set the stopped thread's trap
   flag. Execute the restored instruction once.
5. On its single-step event, reinsert the breakpoint if still requested, clear
   the trap flag, and release the other suspended threads. Continue or publish a
   paused snapshot according to the requested operation.

This rearm cycle prevents immediately hitting the same breakpoint again and
prevents other tracked threads running through its temporarily restored byte.
The initial entry stop uses temporary ownership on the entry instruction.

## F10 and calls

For an ordinary instruction, F10 uses the processor's single-step mechanism.
For `CALL`, it installs a temporary breakpoint at the instruction immediately
after the call and continues. The temporary breakpoint is associated with the
originating thread and stack depth, preventing a deeper recursive invocation or
another thread from completing the step prematurely. A user breakpoint reached
inside the callee still stops execution.

Calls into DLLs use the same return-address technique and do not require symbols
for the DLL. Calls that never return can be interrupted with Pause or Stop.

## Pause, exceptions and termination

Pause requests call `DebugBreakProcess`. On the resulting breakpoint event the
engine reports the main program thread's WOW64 context when that thread is still
available, rather than presenting the injected break-in thread as ZASM code.
That context can be in Windows code, so a source location is optional.

Loader breakpoint notifications before program entry are consumed. Other
first-chance exceptions are passed to the program. An unhandled exception stops
the session with its code and address; F5 passes it onward, while Stop terminates
the program. F10 is disabled for an unhandled exception stop.

Stop calls `TerminateProcess` only on the process created by this session, releases
pending debug events and waits for process termination. Process exit publishes
the exit code. Startup failures terminate the launched process and drain pending
events before releasing handles. Child processes are not debugged.

## Windows API references

- [Debugger main loop](https://learn.microsoft.com/en-us/windows/win32/debug/writing-the-debugger-s-main-loop)
- [WaitForDebugEvent ownership and handles](https://learn.microsoft.com/en-us/windows/win32/api/debugapi/nf-debugapi-waitfordebugevent)
- [WOW64_CONTEXT](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-wow64_context)
- [Debugger exception handling](https://learn.microsoft.com/en-us/windows/win32/debug/debugger-exception-handling)
