// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Prism.Commands;
using Zero.Dbg;
using Zero.Dbg.WinPe;
using Zero.Ide.Events;

namespace Zero.Ide.ViewModels;

public partial class MainWindowViewModel
{
  private DebugMap? _debugMap;
  private IDebugSession? _debugSession;
  private DebugSnapshot _debugSnapshot = new(DebugState.Idle, "Ready");
  public DelegateCommand CmdDebugContinue { get; private set; } = null!;
  public DelegateCommand CmdDebugPlayPause { get; private set; } = null!;
  public DelegateCommand CmdDebugStepOver { get; private set; } = null!;
  public DelegateCommand CmdDebugStop { get; private set; } = null!;
  public DelegateCommand CmdToggleBreakpoint { get; private set; } = null!;
  public DebuggerViewModel Debugger { get; } = new();

  public Geometry DebugPlayIcon => Geometry.Parse(IsDebugRunning
    ? "M6,3 L10,3 10,21 6,21 Z M14,3 L18,3 18,21 14,21 Z"
    : "M7,3 L21,12 7,21 Z");

  public string DebugPlayLabel => IsDebugRunning ? "Pause debugging" : IsDebugging ? "Continue debugging (F5)" : "Start debugging (F5)";
  public bool IsDebugging => _debugSnapshot.State is DebugState.Starting or DebugState.Running or DebugState.Paused;
  public bool IsDebugRunning => _debugSnapshot.State == DebugState.Running;

  private static bool SameSource(string left, string right) =>
    string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

  private void BindExecutableLines(EditorViewModel editor, string? path)
  {
    var lines = new HashSet<int>();
    if (_debugMap is not null && path is not null)
    {
      foreach (var instruction in _debugMap.Instructions)
        if (SameSource(instruction.FilePath, path)) lines.Add(instruction.Line);
    }

    editor.SetExecutableLines(lines);
  }

  private List<SourceBreakpoint> CollectBreakpoints()
  {
    var result = new List<SourceBreakpoint>();
    foreach (var document in Documents)
    {
      var path = document.File?.TryGetLocalPath();
      if (path is null)
        continue;

      foreach (var anchor in document.Editor.Breakpoints)

        if (!anchor.IsDeleted)
          result.Add(new SourceBreakpoint(path, anchor.Line));
    }

    return result;
  }

  private DelegateCommand DebugCommand(Func<Task> action, Func<bool> canExecute) => new DelegateCommand(
    async () =>
    {
      if (IsBusy || !canExecute())
        return;

      IsBusy = true;
      try
      {
        await action();
      }
      catch (Exception ex)
      {
        Status = $"Debugger: {ex.Message}";
        await _dialogs.ShowMessageAsync("Debugger", ex.Message);
      }
      finally
      {
        IsBusy = false;
      }
    },
    () => !IsBusy && canExecute())
    .ObservesProperty(() => IsBusy)
    .ObservesProperty(() => IsDebugging);

  private async Task DebugContinueAsync()
  {
    if (_debugSnapshot.State == DebugState.Paused)
      _debugSession!.Continue();
    else
    {
      if (!WinPeDebugSession.IsSupported)
        throw new PlatformNotSupportedException("Debugging currently supports Windows x86 programs on a Windows x64 host.");

      await BuildAsync(false, debugAfterBuild: true);
    }
  }

  private Docking.EditorDocument? FindSourceDocument(string path)
  {
    foreach (var document in Documents)
      if (document.File?.TryGetLocalPath() is { } open && SameSource(open, path))
        return document;

    return null;
  }

  private void InitializeDebugger(IDebugSession? session)
  {
    _debugSession = session;

    session?.Changed += OnDebuggerChangedAsync;

    CmdDebugContinue = DebugCommand(DebugContinueAsync,
      () => _debugSession is not null &&
            HasActiveDocument &&
            !IsDebugRunning &&
            _debugSnapshot.State != DebugState.Starting);

    CmdDebugPlayPause = DebugCommand(
      async () =>
      {
        if (IsDebugRunning)
          _debugSession!.Pause();
        else
          await DebugContinueAsync();
      },
      () => _debugSession is not null &&
            HasActiveDocument &&
            _debugSnapshot.State != DebugState.Starting);

    CmdDebugStepOver = DebugCommand(
      () =>
      {
        _debugSession!.StepOver();
        return Task.CompletedTask;
      },
      () => _debugSnapshot.State == DebugState.Paused
        && !_debugSnapshot.Reason.StartsWith("Unhandled exception", StringComparison.Ordinal));

    CmdDebugStop = DebugCommand(
      async () => await _debugSession!.StopAsync(),
      () => IsDebugging);

    CmdToggleBreakpoint = new DelegateCommand(
      () => Editor.ToggleBreakpoint(Editor.CaretLine),
      () => HasActiveDocument)
      .ObservesProperty(() => HasActiveDocument);
  }

  private async Task LaunchDebuggerAsync(string executable, string sourcePath)
  {
    var mapPath = executable + DebugMap.Extension;
    if (!File.Exists(mapPath))
      throw new InvalidOperationException("The compiler did not produce debug information. Rebuild/update the Zero CLI selected in Tools > Options > Compiler; it must support --debug.");

    _debugMap = DebugMap.Load(mapPath);

    if (_debugMap.Target != "windows-x86")
      throw new NotSupportedException("The Windows PE debugger supports windows-x86. Linux debugging is not implemented yet.");

    foreach (var document in Documents)
    {
      if (document.Editor.IsModified)
        throw new InvalidOperationException($"Save {document.Editor.FileName} before debugging so source lines match the executable.");

      BindExecutableLines(document.Editor, document.File?.TryGetLocalPath());
    }

    foreach (var document in Documents)
      document.Editor.IsDebugReadOnly = true;

    try
    {
      await _debugSession!.LaunchAsync(
        new DebugLaunchOptions(
          executable,
          Path.GetDirectoryName(sourcePath)!,
          _debugMap,
          CollectBreakpoints()));
    }
    catch
    {
      foreach (var document in Documents)
        document.Editor.IsDebugReadOnly = false;

      throw;
    }
  }

  private async void OnDebuggerChangedAsync(object? sender, DebugSnapshot snapshot)
  {
    _debugSnapshot = snapshot;
    Debugger.Update(snapshot);
    DockFactory.SetDebuggerVisible(IsDebugging);
    Status = $"Debugger: {snapshot.Reason}";

    RaisePropertyChanged(nameof(IsDebugging));
    RaisePropertyChanged(nameof(IsDebugRunning));
    RaisePropertyChanged(nameof(DebugPlayLabel));
    RaisePropertyChanged(nameof(DebugPlayIcon));

    CmdDebugContinue.RaiseCanExecuteChanged();
    CmdDebugPlayPause.RaiseCanExecuteChanged();
    CmdDebugStepOver.RaiseCanExecuteChanged();
    CmdDebugStop.RaiseCanExecuteChanged();

    foreach (var document in Documents)
    {
      document.Editor.IsDebugReadOnly = IsDebugging;
      document.Editor.ExecutionLine = 0;
    }

    if (snapshot.State == DebugState.Paused)
      _events?.Publish(new DebugStoppedEvent(snapshot.ThreadId, snapshot.InstructionPointer, snapshot.Reason,
        snapshot.Location?.FilePath, snapshot.Location?.Line, new List<RegisterValue>(snapshot.Registers ?? []).AsReadOnly()));
    else if (snapshot.State is DebugState.Exited or DebugState.Faulted)
      _events?.Publish(new DebugSessionEndedEvent(snapshot.State, snapshot.Reason, snapshot.ExitCode));

    if (snapshot.State != DebugState.Paused || snapshot.Location is not { } location)
      return;

    try
    {
      var sourceDocument = FindSourceDocument(location.FilePath);
      if (sourceDocument is null)
      {
        var file = await _dialogs.GetLocalFileAsync(location.FilePath);
        if (file is null || !ReferenceEquals(_debugSnapshot, snapshot))
          return;

        await OpenDocumentAsync(file);
        sourceDocument = FindSourceDocument(location.FilePath);
      }

      if (sourceDocument is null || !ReferenceEquals(_debugSnapshot, snapshot))
        return;

      BindExecutableLines(sourceDocument.Editor, location.FilePath);
      sourceDocument.Editor.IsDebugReadOnly = true;
      DockFactory.Activate(sourceDocument);
      sourceDocument.Editor.ExecutionLine = location.Line;
    }
    catch (Exception ex)
    {
      Debugger.Update(snapshot with { Reason = $"{snapshot.Reason}. Cannot display source: {ex.Message}" });
    }
  }

  private void OnEditorDebugPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(EditorViewModel.BreakpointRevision) && IsDebugging)
      _debugSession?.SetBreakpoints(CollectBreakpoints());
  }
}
