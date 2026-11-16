// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Zero.Dbg;

namespace Zero.Ide.Events;

/// <summary>Signals a debugger pause at entry, a breakpoint, a step, or an exception so subscribers can inspect values.</summary>
/// <param name="ThreadId">The native identifier of the thread that paused.</param>
/// <param name="InstructionPointer">The paused thread's instruction address in the debugged process.</param>
/// <param name="Reason">The debugger's description of why execution paused.</param>
/// <param name="SourcePath">The mapped source file path, or null when no source location is available.</param>
/// <param name="SourceLine">The one-based mapped source line, or null when no source location is available.</param>
/// <param name="Registers">A read-only copy of the register names and values captured at the pause; empty when unavailable.</param>
public sealed record DebugStoppedEvent(
  uint ThreadId,
  uint InstructionPointer,
  string Reason,
  string? SourcePath,
  int? SourceLine,
  IReadOnlyList<RegisterValue> Registers) : IdeEvent;

/// <summary>Signals that a debug session exited or faulted, rather than merely pausing for inspection.</summary>
/// <param name="State">The terminal debugger state, either Exited or Faulted.</param>
/// <param name="Reason">The debugger's description of the process exit or session failure.</param>
/// <param name="ExitCode">The debugged process's exit code, or null when no exit code is available.</param>
public sealed record DebugSessionEndedEvent(DebugState State, string Reason, uint? ExitCode) : IdeEvent;
