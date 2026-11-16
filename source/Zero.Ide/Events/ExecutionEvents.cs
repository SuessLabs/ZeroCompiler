// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Zero.Ide.Services;

namespace Zero.Ide.Events;

/// <summary>Signals entry into a workflow state or its Built progress notification; ExecutionId correlates one request.</summary>
/// <param name="ExecutionId">The identifier shared by all execution and build notifications for this workflow request.</param>
/// <param name="Mode">Whether the request builds only, builds and runs, or builds and starts debugging.</param>
/// <param name="Configuration">The effective build configuration; debug requests always use Debug.</param>
/// <param name="Stage">The workflow stage being entered, including Built progress and terminal outcomes.</param>
/// <param name="SourcePath">The source path returned by saving, or null before a source path is available.</param>
/// <param name="ErrorMessage">The captured exception message, or null when no exception was recorded; inspect build results for compiler diagnostics.</param>
public sealed record ExecutionStateChangedEvent(
  Guid ExecutionId,
  BuildExecutionMode Mode,
  string Configuration,
  BuildExecutionStage Stage,
  string? SourcePath,
  string? ErrorMessage) : IdeEvent;

/// <summary>Signals that validated source is about to be compiled, including clean-and-build requests.</summary>
/// <param name="ExecutionId">The identifier correlating this compiler attempt with its workflow and completion notification.</param>
/// <param name="SourcePath">The fully qualified path of the saved and validated source file being compiled.</param>
/// <param name="Configuration">The effective Debug or Release configuration passed to the compiler service.</param>
/// <param name="Mode">The workflow's requested action after compilation: build only, run, or debug.</param>
/// <param name="CleanFirst">Whether existing build output should be cleaned before compilation.</param>
public sealed record BuildStartedEvent(
  Guid ExecutionId,
  string SourcePath,
  string Configuration,
  BuildExecutionMode Mode,
  bool CleanFirst) : IdeEvent;

/// <summary>Signals completion of a compiler attempt before any launch; validation failures and canceled saves do not emit this event.</summary>
/// <param name="ExecutionId">The identifier shared with this attempt's start notification and execution-state notifications.</param>
/// <param name="SourcePath">The fully qualified source path supplied to the compiler service.</param>
/// <param name="Configuration">The effective Debug or Release configuration used for this compiler attempt.</param>
/// <param name="Mode">The workflow's requested action after compilation: build only, run, or debug.</param>
/// <param name="CleanFirst">Whether this attempt requested cleaning existing output before compilation.</param>
/// <param name="Compilation">The compiler result containing its exit code, diagnostics, and output path, or null if compilation threw an exception.</param>
/// <param name="ErrorMessage">The compilation exception message, or null when the compiler returned a result, including a nonzero exit code.</param>
public sealed record BuildCompletedEvent(
  Guid ExecutionId,
  string SourcePath,
  string Configuration,
  BuildExecutionMode Mode,
  bool CleanFirst,
  CompilerResult? Compilation,
  string? ErrorMessage) : IdeEvent
{
  /// <summary>Gets whether the compiler completed successfully without an exception, independently of any later launch outcome.</summary>
  public bool Succeeded => Compilation?.Succeeded == true && ErrorMessage is null;
}
