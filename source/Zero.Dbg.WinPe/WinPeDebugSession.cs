// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Zero.Dbg.WinPe;

/// <summary>Native Windows PE32 debugger. All debug events and context mutations belong to one dedicated thread.</summary>
public sealed class WinPeDebugSession : IDebugSession
{
  private readonly Dictionary<uint, Breakpoint> _breakpoints = [];

  private readonly byte[] _byte = new byte[1];

  private readonly AutoResetEvent _commandReady = new(false);

  private readonly ConcurrentQueue<Command> _commands = new();

  private readonly object _lifecycle = new();

  private readonly List<IntPtr> _suspended = [];

  private readonly Dictionary<uint, IntPtr> _threads = [];

  private bool _entryReached, _pauseRequested, _stopping, _pauseAfterStep, _disposed;

  private TaskCompletionSource _finished = Completed();

  private uint _imageBase, _mainThread;

  private DebugLaunchOptions _options = null!;

  private IntPtr _process;

  private Breakpoint? _rearm;

  private DebugSnapshot _snapshot = new(DebugState.Idle, "Ready");

  private TaskCompletionSource _started = NewCompletion();

  private uint _stepThread;

  public event EventHandler<DebugSnapshot>? Changed;

  private enum CommandKind
  { Continue, Pause, StepOver, Breakpoints, Stop }

  private sealed record Command(CommandKind Kind, IReadOnlyList<SourceBreakpoint>? Breakpoints = null);

  public static bool IsSupported => OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

  public DebugSnapshot Snapshot => Volatile.Read(ref _snapshot);

  public void Continue() => Enqueue(new(CommandKind.Continue));

  public async ValueTask DisposeAsync()
  {
    await StopAsync().ConfigureAwait(false);
    lock (_lifecycle)
    {
      if (_disposed) return;
      _disposed = true;
      _commandReady.Dispose();
    }
  }

  public Task LaunchAsync(DebugLaunchOptions options)
  {
    lock (_lifecycle)
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      if (!_finished.Task.IsCompleted) throw new InvalidOperationException("A debug session is already active.");
      if (!IsSupported) throw new PlatformNotSupportedException("PE debugging requires a Windows x64 host.");
      if (options.Map.Target != "windows-x86") throw new NotSupportedException("This debugger supports Windows x86 PE32 programs.");
      options.Map.Validate(options.ExecutablePath);
      if (options.Map.FindInstruction(options.Map.EntryRva) is null)
        throw new InvalidDataException("Debugging requires an instruction at the executable entry point. Place a jump before inline data.");
      using (var reader = new PEReader(File.OpenRead(options.ExecutablePath)))
      {
        if (reader.PEHeaders.CoffHeader.Machine != Machine.I386 || reader.PEHeaders.PEHeader?.Magic != PEMagic.PE32)
          throw new InvalidDataException("Expected a Windows x86 PE32 executable.");
        if (reader.PEHeaders.PEHeader.AddressOfEntryPoint != options.Map.EntryRva)
          throw new InvalidDataException("The debugger map entry point does not match the executable.");
      }

      _options = options;
      _entryReached = _pauseRequested = _stopping = _pauseAfterStep = false;
      _rearm = null;
      _stepThread = 0;
      _threads.Clear();
      _breakpoints.Clear();
      _suspended.Clear();
      while (_commands.TryDequeue(out _)) { }

      _started = NewCompletion();
      _finished = NewCompletion();
      Publish(new(DebugState.Starting, "Starting debugger"));
      new Thread(DebugLoop) { IsBackground = true, Name = "Zero.Dbg.WinPe" }.Start();
      return _started.Task;
    }
  }

  public void Pause() => Enqueue(new(CommandKind.Pause));

  public void SetBreakpoints(IReadOnlyList<SourceBreakpoint> breakpoints) =>
    Enqueue(new(CommandKind.Breakpoints, new List<SourceBreakpoint>(breakpoints)));

  public void StepOver() => Enqueue(new(CommandKind.StepOver));

  public Task StopAsync()
  {
    lock (_lifecycle)
    {
      if (!_finished.Task.IsCompleted) Enqueue(new(CommandKind.Stop));
      return _finished.Task;
    }
  }

  private static void Check(bool success)
  {
    if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError());
  }

  private static TaskCompletionSource Completed()
  {
    var result = NewCompletion();
    result.SetResult();
    return result;
  }

  private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

  private Breakpoint AddBreakpoint(uint address)
  {
    if (_breakpoints.TryGetValue(address, out var existing)) return existing;
    Check(Native.ReadProcessMemory(_process, (IntPtr)(long)address, _byte, 1, out var read));
    if (read != 1) throw new IOException("Could not read breakpoint instruction.");
    var breakpoint = new Breakpoint(address, _byte[0]);
    _breakpoints.Add(address, breakpoint);
    return breakpoint;
  }

  private void ApplyBreakpoints(IReadOnlyList<SourceBreakpoint> requested)
  {
    _options = _options with { Breakpoints = requested };
    foreach (var breakpoint in _breakpoints.Values) breakpoint.User = false;
    foreach (var source in requested)
    {
      var instruction = _options.Map.FindInstruction(source.FilePath, source.Line);
      if (instruction is null) continue; // Comments, labels, directives and data never bind.
      AddBreakpoint(checked(_imageBase + instruction.Rva)).User = true;
    }

    foreach (var breakpoint in _breakpoints.Values)
    {
      if (ReferenceEquals(breakpoint, _rearm)) continue;
      if (breakpoint.User || breakpoint.Temporary) Install(breakpoint);
      else Restore(breakpoint);
    }
  }

  private void BeginStep(uint threadId, bool pauseAfter)
  {
    var context = GetContext(threadId);
    context.EFlags |= Native.TrapFlag;
    SetContext(threadId, ref context);
    _stepThread = threadId;
    _pauseAfterStep = pauseAfter;
    foreach (var thread in _threads)
      if (thread.Key != threadId) Suspend(thread.Value);
  }

  private void DebugLoop()
  {
    var startup = new Native.StartupInfo { Size = (uint)Marshal.SizeOf<Native.StartupInfo>() };
    Native.ProcessInformation process = default;
    Native.DebugEvent? currentEvent = null;
    try
    {
      Check(Native.CreateProcess(Path.GetFullPath(_options.ExecutablePath), IntPtr.Zero, IntPtr.Zero,
        IntPtr.Zero, false, 0x00000002, IntPtr.Zero, _options.WorkingDirectory, ref startup, out process));
      _process = process.Process;
      _mainThread = process.ThreadId;
      // CreateProcess's handles are owned here; event thread handles are managed by the OS.
      while (true)
      {
        DrainRunningCommands();
        if (!Native.WaitForDebugEventEx(out var debugEvent, 50))
        {
          int error = Marshal.GetLastPInvokeError();
          if (error == 121) continue; // ERROR_SEM_TIMEOUT
          throw new Win32Exception(error);
        }

        currentEvent = debugEvent;
        uint status = Native.Continue;
        switch (debugEvent.Code)
        {
          case 3: // CREATE_PROCESS_DEBUG_EVENT
            _imageBase = checked((uint)debugEvent.ImageBase.ToInt64());
            _threads.Add(debugEvent.ThreadId, debugEvent.InitialThread);
            if (debugEvent.File != IntPtr.Zero) Native.CloseHandle(debugEvent.File);
            ApplyBreakpoints(_options.Breakpoints);
            var entry = AddBreakpoint(checked(_imageBase + _options.Map.EntryRva));
            entry.Temporary = true;
            Install(entry);
            break;

          case 2: // CREATE_THREAD_DEBUG_EVENT: hThread occupies the first union field.
            _threads.Add(debugEvent.ThreadId, debugEvent.File);
            if (_rearm is not null || _stepThread != 0)
              Suspend(debugEvent.File);
            break;

          case 4: // EXIT_THREAD_DEBUG_EVENT
            _threads.Remove(debugEvent.ThreadId);
            break;

          case 6: // LOAD_DLL_DEBUG_EVENT
            if (debugEvent.File != IntPtr.Zero) Native.CloseHandle(debugEvent.File);
            break;

          case 1:
            status = HandleException(debugEvent);
            break;

          case 5: // EXIT_PROCESS_DEBUG_EVENT
            Check(Native.ContinueDebugEvent(debugEvent.ProcessId, debugEvent.ThreadId, Native.Continue));
            currentEvent = null;
            if (Native.WaitForSingleObject(_process, 5000) != 0)
              throw new TimeoutException("The debuggee did not finish terminating.");
            Publish(new(DebugState.Exited, $"Process exited with code {debugEvent.ExitCode}.", ExitCode: debugEvent.ExitCode));
            _started.TrySetException(new InvalidOperationException("The program exited before reaching its entry point."));
            return;
        }

        Check(Native.ContinueDebugEvent(debugEvent.ProcessId, debugEvent.ThreadId, status));
        currentEvent = null;
      }
    }
    catch (Exception ex)
    {
      if (_process != IntPtr.Zero) Native.TerminateProcess(_process, 1);
      if (currentEvent is { } outstanding)
        Native.ContinueDebugEvent(outstanding.ProcessId, outstanding.ThreadId, Native.Continue);
      // Drain termination events before leaving the thread that owns the debug port.
      // Closing our handles alone does not release a pending debug event.
      if (_process != IntPtr.Zero)
      {
        for (int i = 0; i < 40; i++)
        {
          if (!Native.WaitForDebugEventEx(out var pending, 50)) continue;
          if (pending.Code is 3 or 6 && pending.File != IntPtr.Zero) Native.CloseHandle(pending.File);
          Native.ContinueDebugEvent(pending.ProcessId, pending.ThreadId, Native.Continue);
          if (pending.Code == 5) break;
        }

        Native.WaitForSingleObject(_process, 5000);
      }

      Publish(new(DebugState.Faulted, ex.Message));
      _started.TrySetException(ex);
    }
    finally
    {
      _threads.Clear();
      _suspended.Clear();
      if (process.Thread != IntPtr.Zero) Native.CloseHandle(process.Thread);
      if (process.Process != IntPtr.Zero) Native.CloseHandle(process.Process);
      _process = IntPtr.Zero;
      _finished.TrySetResult();
    }
  }

  private void DrainRunningCommands()
  {
    while (_commands.TryDequeue(out var command))
    {
      if (command.Kind == CommandKind.Stop && !_stopping)
      {
        _stopping = true;
        Check(Native.TerminateProcess(_process, 0));
      }
      else if (command.Kind == CommandKind.Pause && !_pauseRequested && !_stopping)
      {
        Check(Native.DebugBreakProcess(_process));
        _pauseRequested = true;
      }
      else if (command.Kind == CommandKind.Breakpoints)
      {
        // Modify code only while all debuggee threads are stopped by a debug event.
        _options = _options with { Breakpoints = command.Breakpoints! };
        if (!_pauseRequested && !_stopping)
        {
          Check(Native.DebugBreakProcess(_process));
          _pauseRequested = true;
        }
      }
    }
  }

  private void Enqueue(Command command)
  {
    lock (_lifecycle)
    {
      if (_disposed || _finished.Task.IsCompleted) return;
      _commands.Enqueue(command);
      _commandReady.Set();
    }
  }

  private Native.Context GetContext(uint threadId)
  {
    var context = new Native.Context { Flags = 0x00010007 }; // WOW64_CONTEXT_FULL
    Check(Native.Wow64GetThreadContext(_threads[threadId], ref context));
    return context;
  }

  private uint HandleException(Native.DebugEvent debugEvent)
  {
    if (_stopping) return Native.NotHandled;
    uint code = debugEvent.ExceptionCode;
    uint address = unchecked((uint)debugEvent.ExceptionAddress.ToInt64());
    if (code is Native.SingleStep or Native.WowSingleStep && debugEvent.ThreadId == _stepThread)
    {
      var context = GetContext(debugEvent.ThreadId);
      context.EFlags &= ~Native.TrapFlag;
      SetContext(debugEvent.ThreadId, ref context);
      if (_rearm is { } rearm)
      {
        if (rearm.User || rearm.Temporary) Install(rearm);
        _rearm = null;
      }

      _stepThread = 0;
      ResumeOthers();
      if (_pauseAfterStep)
      {
        _pauseAfterStep = false;
        // A trap can stop immediately before an installed INT3. Present that
        // instruction once, rather than reporting it again when F5 executes INT3.
        if (_breakpoints.TryGetValue(context.Eip, out var next) && next.Installed)
        {
          Restore(next);
          _rearm = next;
          if (next.Temporary && (next.TemporaryThread == 0 ||
              (next.TemporaryThread == debugEvent.ThreadId && context.Esp >= next.StackPointer)))
            next.Temporary = false;
        }

        return WaitWhilePaused(debugEvent.ThreadId, "Step complete", Native.Continue);
      }

      return Native.Continue;
    }

    if (code is Native.Breakpoint or Native.WowBreakpoint)
    {
      if (_breakpoints.TryGetValue(address, out var breakpoint) && breakpoint.Installed)
      {
        var context = GetContext(debugEvent.ThreadId);
        Restore(breakpoint);
        context.Eip = address; // INT3 reports EIP immediately after its one-byte opcode.
        SetContext(debugEvent.ThreadId, ref context);
        _rearm = breakpoint;
        bool temporaryHit = breakpoint.Temporary &&
          (breakpoint.TemporaryThread == 0 || (breakpoint.TemporaryThread == debugEvent.ThreadId && context.Esp >= breakpoint.StackPointer));
        if (temporaryHit) breakpoint.Temporary = false;
        if (!breakpoint.User && !temporaryHit)
        {
          BeginStep(debugEvent.ThreadId, false);
          return Native.Continue;
        }

        bool first = !_entryReached;
        _entryReached = true;
        return WaitWhilePaused(debugEvent.ThreadId, first ? "Program entry" : "Breakpoint", Native.Continue);
      }

      if (_pauseRequested)
      {
        _pauseRequested = false;
        uint thread = _threads.ContainsKey(_mainThread) ? _mainThread : debugEvent.ThreadId;
        return WaitWhilePaused(thread, "Paused", Native.Continue);
      }
      // Windows and WOW64 report loader breakpoints before user entry.
      if (!_entryReached) return Native.Continue;
    }

    // First-chance exceptions belong to the program. Stop only on an unhandled exception.
    if (debugEvent.FirstChance != 0) return Native.NotHandled;
    return WaitWhilePaused(debugEvent.ThreadId, $"Unhandled exception 0x{code:X8} at 0x{address:X8}", Native.NotHandled);
  }

  private void Install(Breakpoint breakpoint)
  {
    if (breakpoint.Installed) return;
    WriteByte(breakpoint.Address, 0xCC);
    breakpoint.Installed = true;
  }

  private void Publish(DebugSnapshot snapshot)
  {
    Volatile.Write(ref _snapshot, snapshot);
    Changed?.Invoke(this, snapshot);
  }

  private void PublishPaused(uint threadId, string reason)
  {
    ApplyBreakpoints(_options.Breakpoints);
    var c = GetContext(threadId);
    var location = c.Eip >= _imageBase ? _options.Map.FindInstruction(c.Eip - _imageBase) : null;
    RegisterValue[] registers =
    [
      new("EAX", c.Eax), new("EBX", c.Ebx), new("ECX", c.Ecx), new("EDX", c.Edx),
      new("ESI", c.Esi), new("EDI", c.Edi), new("EBP", c.Ebp), new("ESP", c.Esp),
      new("EIP", c.Eip), new("EFLAGS", c.EFlags),
    ];
    Publish(new(DebugState.Paused, reason, threadId, c.Eip, location, registers));
  }

  private void Restore(Breakpoint breakpoint)
  {
    if (!breakpoint.Installed) return;
    WriteByte(breakpoint.Address, breakpoint.Original);
    breakpoint.Installed = false;
  }

  private void ResumeOthers()
  {
    foreach (var thread in _suspended) Native.ResumeThread(thread);
    _suspended.Clear();
  }

  private void SetContext(uint threadId, ref Native.Context context) =>
    Check(Native.Wow64SetThreadContext(_threads[threadId], ref context));

  private void Suspend(IntPtr thread)
  {
    if (Native.SuspendThread(thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastPInvokeError());
    _suspended.Add(thread);
  }

  private uint WaitWhilePaused(uint threadId, string reason, uint continueStatus)
  {
    PublishPaused(threadId, reason);
    _started.TrySetResult();
    while (true)
    {
      if (!_commands.TryDequeue(out var command)) { _commandReady.WaitOne(); continue; }

      if (command.Kind == CommandKind.Breakpoints) { ApplyBreakpoints(command.Breakpoints!); continue; }

      if (command.Kind == CommandKind.Pause) continue;
      if (command.Kind == CommandKind.Stop)
      {
        _stopping = true;
        Check(Native.TerminateProcess(_process, 0));
        return Native.Continue;
      }
      // Never single-step an exception that must be delivered to the debuggee.
      if (continueStatus == Native.NotHandled && command.Kind == CommandKind.StepOver) continue;
      if (command.Kind == CommandKind.StepOver)
      {
        var context = GetContext(threadId);
        var instruction = context.Eip >= _imageBase ? _options.Map.FindInstruction(context.Eip - _imageBase) : null;
        if (instruction?.IsCall == true)
        {
          var next = AddBreakpoint(checked(context.Eip + (uint)instruction.Size));
          next.Temporary = true;
          next.TemporaryThread = threadId;
          next.StackPointer = context.Esp;
          Install(next);
          if (_rearm is not null) BeginStep(threadId, false);
        }
        else BeginStep(threadId, true);
      }
      else if (_rearm is not null) BeginStep(threadId, false);
      Publish(new(DebugState.Running, "Running"));
      return continueStatus;
    }
  }

  private void WriteByte(uint address, byte value)
  {
    _byte[0] = value;
    Check(Native.WriteProcessMemory(_process, (IntPtr)(long)address, _byte, 1, out var written));
    if (written != 1) throw new IOException("Could not write breakpoint instruction.");
    Check(Native.FlushInstructionCache(_process, (IntPtr)(long)address, 1));
  }

  private sealed class Breakpoint(uint address, byte original)
  {
    internal uint Address = address;
    internal byte Original = original;
    internal uint TemporaryThread, StackPointer;
    internal bool User, Temporary, Installed;
  }
}
