// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lite.StateMachine;
using Zero.Ide.Models;

namespace Zero.Ide.Services;

public enum EditorExecutionMode
{ Build, Run, Debug }

public enum EditorExecutionStage
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

public sealed record EditorExecutionRequest(
  string CompilerPath, string Configuration, bool CleanFirst, EditorExecutionMode Mode,
  Func<Task<string?>> SaveSourceAsync, Func<string, string, Task>? StartDebuggerAsync = null);

public sealed record EditorExecutionUpdate(EditorExecutionStage Stage, CompilerResult? Compilation = null, Exception? Error = null);

/// <summary>Coordinates save, validation, compilation, and optional launch through explicit state transitions.</summary>
public sealed class EditorExecutionCoordinator(ICompilerService compiler, IProgramRunner? runner = null)
{
  private int _executing;

  /// <summary>Exports the registered workflow for the requested mode as Graphviz DOT without executing any states.</summary>
  public static string ExportStateDiagram(EditorExecutionMode mode) => CreateMachine(mode).ExportUml(
    initialStateIds: [EditorExecutionStage.ValidateCompiler],
    includeLegend: false,
    transitionColors: new Dictionary<Result, string>
    {
      [Result.Success] = "#2563EB", [Result.Error] = "#B91C1C", [Result.Failure] = "#B45309",
    },
    graphName: $"Zero IDE — {mode}",
    rankLeftToRight: false,
    nodeLabelSelector: stage => stage switch
    {
      EditorExecutionStage.ValidateCompiler => $"1. {nameof(ValidateCompilerState)}",
      EditorExecutionStage.Save => $"2. {nameof(SaveSourceState)}",
      EditorExecutionStage.ValidateSource => $"3. {nameof(ValidateSourceState)}",
      EditorExecutionStage.Build => $"4. {nameof(CompileState)}",
      EditorExecutionStage.Launch => $"5. {nameof(LaunchState)}",
      EditorExecutionStage.Completed => $"6. {nameof(CompletedState)}",
      EditorExecutionStage.Canceled => $"2a. {nameof(CanceledState)}",
      EditorExecutionStage.Failed => nameof(FailedState),
      EditorExecutionStage.RunFailed => $"5a. {nameof(RunFailedState)}",
      _ => stage.ToString(),
    });

  /// <summary>Shares one transition definition between execution and diagram export, omitting states unused by the selected mode.</summary>
  private static StateMachine<EditorExecutionStage> CreateMachine(EditorExecutionMode mode, Func<Type, object?>? factory = null)
  {
    if (mode is not (EditorExecutionMode.Build or EditorExecutionMode.Run or EditorExecutionMode.Debug))
      throw new ArgumentOutOfRangeException(nameof(mode));

    var machine = new StateMachine<EditorExecutionStage>(containerFactory: factory)
      .RegisterState<ValidateCompilerState>(EditorExecutionStage.ValidateCompiler, EditorExecutionStage.Save, EditorExecutionStage.Failed)
      .RegisterState<SaveSourceState>(EditorExecutionStage.Save, EditorExecutionStage.ValidateSource, EditorExecutionStage.Failed, EditorExecutionStage.Canceled, parentStateId: null)
      .RegisterState<ValidateSourceState>(EditorExecutionStage.ValidateSource, EditorExecutionStage.Build, EditorExecutionStage.Failed)
      .RegisterState<CompileState>(EditorExecutionStage.Build,
        mode == EditorExecutionMode.Build ? EditorExecutionStage.Completed : EditorExecutionStage.Launch, EditorExecutionStage.Failed)
      .RegisterState<CompletedState>(EditorExecutionStage.Completed)
      .RegisterState<CanceledState>(EditorExecutionStage.Canceled)
      .RegisterState<FailedState>(EditorExecutionStage.Failed);

    if (mode != EditorExecutionMode.Build)
      machine.RegisterState<LaunchState>(EditorExecutionStage.Launch, EditorExecutionStage.Completed,
        mode == EditorExecutionMode.Run ? EditorExecutionStage.RunFailed : EditorExecutionStage.Failed);

    if (mode == EditorExecutionMode.Run)
      machine.RegisterState<RunFailedState>(EditorExecutionStage.RunFailed);

    return machine;
  }

  public async Task<EditorExecutionUpdate> ExecuteAsync(EditorExecutionRequest request, Action<EditorExecutionUpdate> report)
  {
    if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0)
      throw new InvalidOperationException("An editor execution is already in progress.");

    try
    {
      var run = new ExecutionRun(request, compiler, runner, report, SynchronizationContext.Current);

      // Build state instances for each execution stage.
      var states = new Dictionary<Type, IState<EditorExecutionStage>>
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

      await machine.RunAsync(EditorExecutionStage.ValidateCompiler);
      return run.Update;
    }
    finally
    {
      Volatile.Write(ref _executing, 0);
    }
  }

  /// <summary>2a. Alternate terminal path: reports cancellation when the save step returns no source path, without compiling or launching.</summary>
  private sealed class CanceledState(ExecutionRun run) : ExecutionState(run);

  /// <summary>4. Invokes the compiler with the selected configuration and clean option, retaining diagnostics and checking success before launch.</summary>
  private sealed class CompileState(ExecutionRun run) : ExecutionState(run);

  /// <summary>6. Reports successful completion after launch, or directly after step 4 for a build-only request.</summary>
  private sealed class CompletedState(ExecutionRun run) : ExecutionState(run);

  private sealed class ExecutionRun(EditorExecutionRequest request, ICompilerService compiler, IProgramRunner? runner,
    Action<EditorExecutionUpdate> report, SynchronizationContext? uiContext)
  {
    private readonly string _configuration = request.Mode == EditorExecutionMode.Debug ? BuildConstants.DebugConfiguration : request.Configuration;
    private CompilerResult? _compilation;
    private string? _sourcePath;

    public Exception? Error { get; set; }
    public EditorExecutionUpdate Update { get; private set; } = new(EditorExecutionStage.ValidateCompiler);

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

    public async Task<Result> StepAsync(EditorExecutionStage stage)
    {
      Notify(stage);
      switch (stage)
      {
        case EditorExecutionStage.ValidateCompiler:
          if (string.IsNullOrWhiteSpace(request.CompilerPath))
            throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");

          if (!BuildConstants.IsSupportedConfiguration(_configuration))
            throw new ArgumentException("Choose Debug or Release.");

          break;

        case EditorExecutionStage.Save:
          _sourcePath = await request.SaveSourceAsync();
          if (_sourcePath is null) return Result.Failure;
          break;

        case EditorExecutionStage.ValidateSource:
          if (!Path.IsPathFullyQualified(_sourcePath!))
            throw new InvalidOperationException($"Building requires a local {SourceFileExtensions.Assembly} file.");

          if (!string.Equals(Path.GetExtension(_sourcePath), SourceFileExtensions.Assembly, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only {SourceFileExtensions.Assembly} source files can be built.");

          break;

        case EditorExecutionStage.Build:
          _compilation = await compiler.BuildAsync(request.CompilerPath, _sourcePath!, _configuration, request.CleanFirst);
          if (!_compilation.Succeeded)
            return Result.Error;

          Notify(EditorExecutionStage.Built);
          break;

        case EditorExecutionStage.Launch:
          if (request.Mode == EditorExecutionMode.Debug)
            await (request.StartDebuggerAsync ?? throw new InvalidOperationException("The debugger is unavailable."))(_compilation!.OutputPath, _sourcePath!);
          else
            (runner ?? throw new InvalidOperationException("The program runner is unavailable.")).Run(_compilation!.OutputPath, Path.GetDirectoryName(_sourcePath)!);
          break;
      }

      return Result.Success;
    }

    private void Notify(EditorExecutionStage stage)
    {
      Update = new EditorExecutionUpdate(stage, _compilation, Error);
      report(Update);
    }
  }

  /// <summary>Shared base for steps 1–6 and alternate exits; runs state actions on the caller's UI context and routes exceptions to error transitions.</summary>
  private abstract class ExecutionState(ExecutionRun run) : IState<EditorExecutionStage>
  {
    public async Task OnEnter(Context<EditorExecutionStage> context)
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

    public Task OnEntering(Context<EditorExecutionStage> context) => Task.CompletedTask;

    public Task OnExit(Context<EditorExecutionStage> context) => Task.CompletedTask;
  }

  /// <summary>1–5, alternate terminal path. Reports validation, save, compilation, or debugger-startup failure instead of continuing to subsequent steps.</summary>
  private sealed class FailedState(ExecutionRun run) : ExecutionState(run);

  /// <summary>5. After successful compilation, launches the executable normally or starts a debug session; skipped for build-only requests.</summary>
  private sealed class LaunchState(ExecutionRun run) : ExecutionState(run);

  /// <summary>5a. Alternate terminal path: reports a normal program-launch failure while preserving successful compiler output.</summary>
  private sealed class RunFailedState(ExecutionRun run) : ExecutionState(run);

  /// <summary>2. Runs the source-saving callback and captures its resulting path; cancellation branches to step 2a.</summary>
  private sealed class SaveSourceState(ExecutionRun run) : ExecutionState(run);

  /// <summary>1. Checks that a compiler is configured and that the effective Debug or Release configuration is supported before requesting a save.</summary>
  private sealed class ValidateCompilerState(ExecutionRun run) : ExecutionState(run);

  /// <summary>3. Verifies that the saved source path is fully qualified and has the supported ZASM extension before compilation.</summary>
  private sealed class ValidateSourceState(ExecutionRun run) : ExecutionState(run);
}
