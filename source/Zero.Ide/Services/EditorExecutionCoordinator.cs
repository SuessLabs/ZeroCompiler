// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lite.StateMachine;
using Zero.Ide.Events;
using Zero.Ide.Models;

namespace Zero.Ide.Services;

/// <summary>Compiler build execution modes.</summary>
public enum BuildExecutionMode
{
  Build,
  Run,
  Debug
}

/// <summary>Build execution state machine states.</summary>
public enum BuildExecutionStage
{
  ValidateCompiler,
  Save,
  ValidateSource,
  Build,
  Built,
  Launch,
  Completed,
  Canceled,
  Failed,
  RunFailed,
}

/// <summary>Describes one save, build, and optional launch workflow.</summary>
/// <param name="CompilerPath">The configured Zero CLI executable path, validated by the compiler service before compilation.</param>
/// <param name="Configuration">The requested Debug or Release configuration; debug mode overrides this with Debug.</param>
/// <param name="CleanFirst">Whether the compiler service should remove existing output before building.</param>
/// <param name="Mode">Whether to build only, build and run, or build and start debugging.</param>
/// <param name="SaveSourceAsync">The callback that saves the active document and returns its absolute path, or null when saving is canceled.</param>
/// <param name="StartDebuggerAsync">The callback receiving the compiled executable path and source path to start debugging; required only in debug mode.</param>
public sealed record EditorExecutionRequest(
  string CompilerPath,
  string Configuration,
  bool CleanFirst,
  BuildExecutionMode Mode,
  Func<Task<string?>> SaveSourceAsync,
  Func<string, string, Task>? StartDebuggerAsync = null);

/// <summary>Reports workflow progress or its terminal outcome together with available compilation diagnostics.</summary>
/// <param name="Stage">The current workflow stage or terminal outcome.</param>
/// <param name="Compilation">The completed compiler result, or null before a result is available.</param>
/// <param name="Error">The exception captured during workflow execution, or null when no exception occurred.</param>
public sealed record EditorExecutionUpdate(BuildExecutionStage Stage, CompilerResult? Compilation = null, Exception? Error = null);

/// <summary>Coordinates save, validation, compilation, and optional launch through explicit state transitions.</summary>
/// <param name="compiler">The service that compiles saved source files and optionally cleans existing output.</param>
/// <param name="runner">The service used to launch output without debugging; required only for run mode.</param>
/// <param name="events">The optional service that publishes observational execution and compiler notifications.</param>
/// <remarks>
///   Considering renaming to BuildExecutionService and calling the state machine, a build execution coordinator.
/// </remarks>
public sealed class EditorExecutionCoordinator(
  ICompilerService compiler,
  IProgramRunner? runner = null,
  IIdeEventService? events = null)
{
  private int _executing;

  /// <summary>Exports the registered workflow for the requested mode as Graphviz DOT without executing any states.</summary>
  /// <param name="mode">The build, run, or debug workflow whose transitions should be exported.</param>
  /// <returns>The Graphviz DOT representation of the selected workflow.</returns>
  public static string ExportStateDiagram(BuildExecutionMode mode) =>
    CreateMachine(mode).ExportUml(
      initialStateIds: [BuildExecutionStage.ValidateCompiler],
      includeLegend: false,
      transitionColors: new Dictionary<Result, string>
      {
        [Result.Success] = "#2563EB",
        [Result.Error] = "#B91C1C",
        [Result.Failure] = "#B45309",
      },
      graphName: $"Zero IDE — {mode}",
      rankLeftToRight: false,
      nodeLabelSelector: stage => stage switch
      {
        BuildExecutionStage.ValidateCompiler => $"1. {nameof(ValidateCompilerState)}",
        BuildExecutionStage.Save => $"2. {nameof(SaveSourceState)}",
        BuildExecutionStage.ValidateSource => $"3. {nameof(ValidateSourceState)}",
        BuildExecutionStage.Build => $"4. {nameof(CompileState)}",
        BuildExecutionStage.Launch => $"5. {nameof(LaunchState)}",
        BuildExecutionStage.Completed => $"6. {nameof(CompletedState)}",
        BuildExecutionStage.Canceled => $"2a. {nameof(CanceledState)}",
        BuildExecutionStage.Failed => nameof(FailedState),
        BuildExecutionStage.RunFailed => $"5a. {nameof(RunFailedState)}",
        _ => stage.ToString(),
      });

  /// <summary>Executes one workflow at a time, reporting progress on the caller's captured synchronization context.</summary>
  /// <param name="request">The configuration and callbacks for saving, compiling, and optionally launching the active document.</param>
  /// <param name="report">The progress callback invoked as stages are entered; terminal updates include available results and errors.</param>
  /// <returns>The terminal workflow update, distinguishing completion, cancellation, build failure, and run failure.</returns>
  public async Task<EditorExecutionUpdate> ExecuteAsync(EditorExecutionRequest request, Action<EditorExecutionUpdate> report)
  {
    if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0)
      throw new InvalidOperationException("An editor execution is already in progress.");

    try
    {
      var run = new ExecutionRun(request, compiler, runner, report, SynchronizationContext.Current, events);

      // Build state instances for each execution stage.
      var states = new Dictionary<Type, IState<BuildExecutionStage>>
      {
        [typeof(ValidateCompilerState)] = new ValidateCompilerState(run),
        [typeof(SaveSourceState)] = new SaveSourceState(run),
        [typeof(ValidateSourceState)] = new ValidateSourceState(run),
        [typeof(CompileState)] = new CompileState(run),
        [typeof(LaunchState)] = new LaunchState(run),
        [typeof(CompletedState)] = new CompletedState(run),
        [typeof(CanceledState)] = new CanceledState(run),
        [typeof(FailedState)] = new FailedState(run),
        [typeof(RunFailedState)] = new RunFailedState(run),
      };

      var machine = CreateMachine(request.Mode, type => states[type]);

      await machine.RunAsync(BuildExecutionStage.ValidateCompiler);
      return run.Update;
    }
    finally
    {
      Volatile.Write(ref _executing, 0);
    }
  }

  /// <summary>Shares one transition definition between execution and diagram export, omitting states unused by the selected mode.</summary>
  /// <param name="mode">The workflow mode that determines launch states and failure transitions.</param>
  /// <param name="factory">The optional state-instance resolver used during execution; omitted when exporting transitions only.</param>
  /// <returns>A state machine configured with the selected workflow's transitions.</returns>
  private static StateMachine<BuildExecutionStage> CreateMachine(BuildExecutionMode mode, Func<Type, object?>? factory = null)
  {
    if (mode is not (BuildExecutionMode.Build or BuildExecutionMode.Run or BuildExecutionMode.Debug))
      throw new ArgumentOutOfRangeException(nameof(mode));

    var machine = new StateMachine<BuildExecutionStage>(containerFactory: factory)
      .RegisterState<ValidateCompilerState>(BuildExecutionStage.ValidateCompiler, BuildExecutionStage.Save, BuildExecutionStage.Failed)
      .RegisterState<SaveSourceState>(BuildExecutionStage.Save, BuildExecutionStage.ValidateSource, BuildExecutionStage.Failed, BuildExecutionStage.Canceled, parentStateId: null)
      .RegisterState<ValidateSourceState>(BuildExecutionStage.ValidateSource, BuildExecutionStage.Build, BuildExecutionStage.Failed)
      .RegisterState<CompileState>(BuildExecutionStage.Build,
        mode == BuildExecutionMode.Build ? BuildExecutionStage.Completed : BuildExecutionStage.Launch, BuildExecutionStage.Failed)
      .RegisterState<CompletedState>(BuildExecutionStage.Completed)
      .RegisterState<CanceledState>(BuildExecutionStage.Canceled)
      .RegisterState<FailedState>(BuildExecutionStage.Failed);

    if (mode != BuildExecutionMode.Build)
    {
      machine.RegisterState<LaunchState>(
        stateId: BuildExecutionStage.Launch,
        onSuccess: BuildExecutionStage.Completed,
        onError: (mode == BuildExecutionMode.Run) ? BuildExecutionStage.RunFailed : BuildExecutionStage.Failed);
    }

    if (mode == BuildExecutionMode.Run)
      machine.RegisterState<RunFailedState>(BuildExecutionStage.RunFailed);

    return machine;
  }

  /// <summary>2a. Alternate terminal path: reports cancellation when the save step returns no source path, without compiling or launching.</summary>
  /// <param name="run">The current workflow's saved source, results, callbacks, and notification context.</param>
  private sealed class CanceledState(ExecutionRun run) : ExecutionState(run);

  /// <summary>4. Invokes the compiler with the selected configuration and clean option, retaining diagnostics and checking success before launch.</summary>
  /// <param name="run">The current workflow's compiler request, results, and notification context.</param>
  private sealed class CompileState(ExecutionRun run) : ExecutionState(run);

  /// <summary>6. Reports successful completion after launch, or directly after step 4 for a build-only request.</summary>
  /// <param name="run">The current workflow's completed results and notification context.</param>
  private sealed class CompletedState(ExecutionRun run) : ExecutionState(run);

  /// <summary>Holds one workflow's mutable results and executes stage actions on its captured synchronization context.</summary>
  /// <param name="request">The workflow's configuration and save/debug-launch callbacks.</param>
  /// <param name="compiler">The compiler service invoked by the build stage.</param>
  /// <param name="runner">The optional service that launches output in run mode.</param>
  /// <param name="report">The callback receiving stage progress and terminal results.</param>
  /// <param name="uiContext">The caller's captured synchronization context, or null to execute without context marshaling.</param>
  /// <param name="events">The optional event publisher used to notify subscribers about this execution.</param>
  private sealed class ExecutionRun(
    EditorExecutionRequest request,
    ICompilerService compiler,
    IProgramRunner? runner,
    Action<EditorExecutionUpdate> report,
    SynchronizationContext? uiContext,
    IIdeEventService? events)
  {
    private readonly string _configuration = request.Mode == BuildExecutionMode.Debug ? BuildConstants.DebugConfiguration : request.Configuration;
    private readonly Guid _executionId = Guid.NewGuid();
    private CompilerResult? _compilation;
    private string? _sourcePath;

    public Exception? Error { get; set; }

    public EditorExecutionUpdate Update { get; private set; } = new(BuildExecutionStage.ValidateCompiler);

    /// <summary>Invokes a stage action on the captured synchronization context when marshaling is required.</summary>
    /// <param name="action">The asynchronous stage operation to invoke.</param>
    /// <returns>A task carrying the stage transition result or the action's exception.</returns>
    public Task<Result> InvokeAsync(Func<Task<Result>> action)
    {
      // Lite.StateMachine uses ConfigureAwait(false); UI operations must retain the caller's context.
      if (uiContext is null || ReferenceEquals(SynchronizationContext.Current, uiContext))
        return action();

      var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

      uiContext.Post(async _ =>
      {
        try { completed.SetResult(await action()); }
        catch (Exception ex) { completed.SetException(ex); }
      }, null);
      return completed.Task;
    }

    /// <summary>Reports the entered stage, performs its workflow operation, and selects the transition result.</summary>
    /// <param name="stage">The workflow stage whose operation should execute.</param>
    /// <returns>The transition result indicating success, compiler failure, or save cancellation.</returns>
    public async Task<Result> StepAsync(BuildExecutionStage stage)
    {
      // Sent notification of a build execution-state change to the IDE and any subscribers.
      EventNotify(stage);

      switch (stage)
      {
        case BuildExecutionStage.ValidateCompiler:
          if (string.IsNullOrWhiteSpace(request.CompilerPath))
            throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");

          if (!BuildConstants.IsSupportedConfiguration(_configuration))
            throw new ArgumentException("Choose Debug or Release.");

          break;

        case BuildExecutionStage.Save:
          _sourcePath = await request.SaveSourceAsync();
          if (_sourcePath is null)
            return Result.Failure;

          break;

        case BuildExecutionStage.ValidateSource:
          if (!Path.IsPathFullyQualified(_sourcePath!))
            throw new InvalidOperationException($"Building requires a local {SourceFileExtensions.Assembly} file.");

          if (!string.Equals(Path.GetExtension(_sourcePath), SourceFileExtensions.Assembly, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only {SourceFileExtensions.Assembly} source files can be built.");

          break;

        case BuildExecutionStage.Build:
          // Notify of Build Started
          events?.Publish(new BuildStartedEvent(_executionId, _sourcePath!, _configuration, request.Mode, request.CleanFirst));

          try
          {
            _compilation = await compiler.BuildAsync(request.CompilerPath, _sourcePath!, _configuration, request.CleanFirst);
          }
          catch (Exception ex)
          {
            // Notify of a build failure
            events?.Publish(new BuildCompletedEvent(_executionId, _sourcePath!, _configuration, request.Mode, request.CleanFirst, null, ex.Message));
            throw;
          }

          events?.Publish(new BuildCompletedEvent(_executionId, _sourcePath!, _configuration, request.Mode, request.CleanFirst, _compilation, null));
          if (!_compilation.Succeeded)
            return Result.Error;

          EventNotify(BuildExecutionStage.Built);
          break;

        case BuildExecutionStage.Launch:
          if (request.Mode == BuildExecutionMode.Debug)
            await (request.StartDebuggerAsync ?? throw new InvalidOperationException("The debugger is unavailable."))(_compilation!.OutputPath, _sourcePath!);
          else
            (runner ?? throw new InvalidOperationException("The program runner is unavailable.")).Run(_compilation!.OutputPath, Path.GetDirectoryName(_sourcePath)!);
          break;
      }

      return Result.Success;
    }

    /// <summary>Publish event of updates to workflow progress, publishing a correlated execution-state notification.</summary>
    /// <param name="stage">The entered stage or progress notification to report to the UI and subscribers.</param>
    private void EventNotify(BuildExecutionStage stage)
    {
      Update = new EditorExecutionUpdate(stage, _compilation, Error);
      report(Update);

      // Inform system of a build execution-state change
      events?.Publish(new ExecutionStateChangedEvent(_executionId, request.Mode, _configuration, stage, _sourcePath, Error?.Message));
    }
  }

  /// <summary>Shared base for steps 1–6 and alternate exits; runs state actions on the caller's UI context and routes exceptions to error transitions.</summary>
  /// <param name="run">The current workflow's stage executor, results, and captured synchronization context.</param>
  private abstract class ExecutionState(ExecutionRun run) : IState<BuildExecutionStage>
  {
    /// <summary>Executes the entered stage and selects its next transition, capturing exceptions as workflow errors.</summary>
    /// <param name="context">The state-machine context identifying the entered stage and accepting the next transition result.</param>
    /// <returns>A task that completes when the stage action and transition selection finish.</returns>
    public async Task OnEnter(Context<BuildExecutionStage> context)
    {
      try
      {
        context.NextState(await run.InvokeAsync(() => run.StepAsync(context.CurrentStateId)));
      }
      catch (Exception ex)
      {
        run.Error = ex;
        context.NextState(Result.Error);
      }
    }

    /// <summary>Completes the pre-entry hook without additional work.</summary>
    /// <param name="context">The state-machine context for the stage about to be entered.</param>
    /// <returns>An already completed task.</returns>
    public Task OnEntering(Context<BuildExecutionStage> context) => Task.CompletedTask;

    /// <summary>Completes the exit hook without additional work.</summary>
    /// <param name="context">The state-machine context for the stage being exited.</param>
    /// <returns>An already completed task.</returns>
    public Task OnExit(Context<BuildExecutionStage> context) => Task.CompletedTask;
  }

  /// <summary>1–5, alternate terminal path. Reports validation, save, compilation, or debugger-startup failure instead of continuing to subsequent steps.</summary>
  /// <param name="run">The current workflow's error, compilation results, and notification context.</param>
  private sealed class FailedState(ExecutionRun run) : ExecutionState(run);

  /// <summary>5. After successful compilation, launches the executable normally or starts a debug session; skipped for build-only requests.</summary>
  /// <param name="run">The current workflow's compiled output, launch services, and mode.</param>
  private sealed class LaunchState(ExecutionRun run) : ExecutionState(run);

  /// <summary>5a. Alternate terminal path: reports a normal program-launch failure while preserving successful compiler output.</summary>
  /// <param name="run">The current workflow's launch error, successful compilation result, and notification context.</param>
  private sealed class RunFailedState(ExecutionRun run) : ExecutionState(run);

  /// <summary>2. Runs the source-saving callback and captures its resulting path; cancellation branches to step 2a.</summary>
  /// <param name="run">The current workflow's save callback, source path, and notification context.</param>
  private sealed class SaveSourceState(ExecutionRun run) : ExecutionState(run);

  /// <summary>1. Checks that a compiler is configured and that the effective Debug or Release configuration is supported before requesting a save.</summary>
  /// <param name="run">The current workflow's compiler settings, effective configuration, and notification context.</param>
  private sealed class ValidateCompilerState(ExecutionRun run) : ExecutionState(run);

  /// <summary>3. Verifies that the saved source path is fully qualified and has the supported ZASM extension before compilation.</summary>
  /// <param name="run">The current workflow's saved source path and notification context.</param>
  private sealed class ValidateSourceState(ExecutionRun run) : ExecutionState(run);
}
