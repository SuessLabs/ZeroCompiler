// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zero.Dbg;

public sealed record SourceBreakpoint(string FilePath, int Line);

public sealed record RegisterValue(string Name, uint Value)
{
  public string Hex => $"0x{Value:X8}";
}

/// <summary>An immutable snapshot. Native implementations raise Changed on their debug thread.</summary>
public sealed record DebugSnapshot(DebugState State, string Reason,
  uint ThreadId = 0, uint InstructionPointer = 0, DebugInstruction? Location = null,
  IReadOnlyList<RegisterValue>? Registers = null, uint? ExitCode = null);

public sealed record DebugLaunchOptions(string ExecutablePath, string WorkingDirectory,
  DebugMap Map, IReadOnlyList<SourceBreakpoint> Breakpoints);

public interface IDebugSession : IAsyncDisposable
{
  event EventHandler<DebugSnapshot>? Changed;

  DebugSnapshot Snapshot { get; }

  void Continue();

  Task LaunchAsync(DebugLaunchOptions options);

  void Pause();

  void SetBreakpoints(IReadOnlyList<SourceBreakpoint> breakpoints);

  void StepOver();

  Task StopAsync();
}
