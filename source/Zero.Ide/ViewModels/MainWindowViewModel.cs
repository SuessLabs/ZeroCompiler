// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Dock.Model.Controls;
using Prism.Commands;
using Zero.Dbg;
using Zero.Ide.Docking;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
  private readonly ICompilerService? _compilerService;
  private readonly IEditorDialogService _dialogs;
  private readonly EditorViewModel _emptyEditor = new();
  private readonly EditorExecutionCoordinator? _executionCoordinator;
  private readonly IProgramRunner? _programRunner;
  private readonly ISettingsService? _settings;
  private readonly IUpdateService? _updates;

  private EditorDocument? _activeDocument;
  private string _buildConfiguration = BuildConstants.DebugConfiguration;
  private string _buildOutput = string.Empty;
  private EditorBuildState _buildState;
  private bool _closingForUpdate;
  private bool _isBusy;
  private string _status = "Ready";
  private int _untitledCount = 1;

  public MainWindowViewModel(EditorViewModel editor, IEditorDialogService dialogs, ISettingsService? settings = null,
    ICompilerService? compilerService = null, IProgramRunner? programRunner = null, IUpdateService? updates = null,
    IDebugSession? debugSession = null, EditorExecutionCoordinator? executionCoordinator = null)
  {
    _dialogs = dialogs;
    _settings = settings;
    _compilerService = compilerService;
    _executionCoordinator = executionCoordinator ?? (compilerService is null ? null : new EditorExecutionCoordinator(compilerService, programRunner));
    _programRunner = programRunner;
    _updates = updates;
    settings?.SettingsChanged += (_, _) => NotifyThemeChanged();

    CmdNew = CreateCommand(() =>
    {
      var model = new EditorViewModel(_settings);
      model.Load(string.Empty, $"Untitled{++_untitledCount}{SourceFileExtensions.Assembly}");
      AddDocument(new EditorDocument(model));
      Status = "New document";
      return Task.CompletedTask;
    });

    CmdOpen = CreateCommand(OpenAsync);
    CmdSave = CreateCommand(async () => { if (ActiveDocument is { } document) await SaveAsync(document, false); }, () => HasActiveDocument).ObservesProperty(() => HasActiveDocument);
    CmdSaveAs = CreateCommand(async () => { if (ActiveDocument is { } document) await SaveAsync(document, true); }, () => HasActiveDocument).ObservesProperty(() => HasActiveDocument);
    CmdSaveAll = CreateCommand(SaveAllAsync);
    CmdCloseDocument = CreateCommand(async () =>
    {
      if (ActiveDocument is { } document && await ConfirmSaveAsync(document))
        RemoveDocument(document);
    }, () => HasActiveDocument).ObservesProperty(() => HasActiveDocument);
    CmdOpenFolder = CreateCommand(OpenFolderAsync);
    CmdShowSolutionExplorer = CreateCommand(ShowSolutionExplorerAsync);
    CmdExit = new DelegateCommand(() => _dialogs.CloseWindow(), () => !IsBusy).ObservesProperty(() => IsBusy);
    CmdAbout = CreateCommand(_dialogs.ShowAboutAsync);
    CmdCheckForUpdates = CreateCommand(CheckForUpdatesAsync, () => _updates is not null);
    CmdOptions = CreateCommand(_dialogs.ShowOptionsAsync);
    CmdThemeSystem = CreateCommand(() => SelectThemeAsync(IdeTheme.System));
    CmdThemeLight = CreateCommand(() => SelectThemeAsync(IdeTheme.Light));
    CmdThemeDark = CreateCommand(() => SelectThemeAsync(IdeTheme.Dark));
    CmdThemeCoolSlate = CreateCommand(() => SelectThemeAsync(IdeTheme.CoolSlate));
    CmdBuild = CreateCommand(() => BuildAsync(false), () => HasActiveDocument && _compilerService is not null).ObservesProperty(() => HasActiveDocument);
    CmdBuildAndRun = CreateCommand(() => BuildAsync(false, true), () => HasActiveDocument && _compilerService is not null && _programRunner is not null).ObservesProperty(() => HasActiveDocument);
    CmdRebuild = CreateCommand(() => BuildAsync(true), () => HasActiveDocument && _compilerService is not null).ObservesProperty(() => HasActiveDocument);
    CmdClean = CreateCommand(CleanAsync, () => HasActiveDocument && _compilerService is not null).ObservesProperty(() => HasActiveDocument);
    CmdBuildOutput = CreateCommand(() => _dialogs.ShowMessageAsync("Build output", BuildOutput),
      () => !string.IsNullOrEmpty(BuildOutput)).ObservesProperty(() => BuildOutput);
    InitializeDebugger(debugSession);

    SolutionExplorer = new SolutionExplorerViewModel(CmdOpenFolder, OpenWorkspaceFileAsync);

    // Initial active document
    var initial = new EditorDocument(editor);
    initial.PropertyChanged += OnDocumentChanged;
    editor.PropertyChanged += OnEditorDebugPropertyChanged;
    Documents.Add(initial);
    _activeDocument = initial;

    // Docking
    DockFactory = new EditorDockFactory(this);
    Layout = DockFactory.CreateLayout();
    DockFactory.InitLayout(Layout);
    DockFactory.SetDebuggerVisible(IsDebugging);
  }

  public EditorDocument? ActiveDocument => _activeDocument;

  public string BuildConfiguration
  {
    get => _buildConfiguration;
    set
    {
      if (!BuildConstants.IsSupportedConfiguration(value))
        throw new ArgumentException("Choose Debug or Release.");

      SetProperty(ref _buildConfiguration, value);
    }
  }

  public IReadOnlyList<string> BuildConfigurations => BuildConstants.Configurations;

  public string BuildOutput { get => _buildOutput; private set => SetProperty(ref _buildOutput, value); }

  public EditorBuildState BuildState
  {
    get => _buildState;
    private set
    {
      if (!SetProperty(ref _buildState, value)) return;
      RaisePropertyChanged(nameof(BuildStatus));
      RaisePropertyChanged(nameof(IsBuilding));
      RaisePropertyChanged(nameof(HasBuildFailed));
      RaisePropertyChanged(nameof(HasRunFailed));
    }
  }

  public string BuildStatus => BuildState switch
  {
    EditorBuildState.Building => "Building...",
    EditorBuildState.Failed => "Build failure",
    EditorBuildState.RunFailed => "Run failure",
    _ => "Idle",
  };

  public DelegateCommand CmdAbout { get; }
  public DelegateCommand CmdBuild { get; }
  public DelegateCommand CmdBuildAndRun { get; }
  public DelegateCommand CmdBuildOutput { get; }
  public DelegateCommand CmdCheckForUpdates { get; }
  public DelegateCommand CmdClean { get; }
  public DelegateCommand CmdCloseDocument { get; }
  public DelegateCommand CmdExit { get; }
  public DelegateCommand CmdNew { get; }
  public DelegateCommand CmdOpen { get; }
  public DelegateCommand CmdOpenFolder { get; }
  public DelegateCommand CmdOptions { get; }
  public DelegateCommand CmdRebuild { get; }
  public DelegateCommand CmdSave { get; }
  public DelegateCommand CmdSaveAll { get; }
  public DelegateCommand CmdSaveAs { get; }
  public DelegateCommand CmdShowSolutionExplorer { get; }
  public DelegateCommand CmdThemeCoolSlate { get; }
  public DelegateCommand CmdThemeDark { get; }
  public DelegateCommand CmdThemeLight { get; }
  public DelegateCommand CmdThemeSystem { get; }
  public EditorDockFactory DockFactory { get; }
  public ObservableCollection<EditorDocument> Documents { get; } = [];
  public EditorViewModel Editor => ActiveDocument?.Editor ?? _emptyEditor;
  public bool HasActiveDocument => ActiveDocument is not null;
  public bool HasBuildFailed => BuildState == EditorBuildState.Failed;
  public bool HasRunFailed => BuildState == EditorBuildState.RunFailed;
  public bool IsBuilding => BuildState == EditorBuildState.Building;

  public bool IsBusy
  {
    get => _isBusy;
    private set => SetProperty(ref _isBusy, value);
  }

  public bool IsCoolSlateTheme => _settings?.Current.Theme == IdeTheme.CoolSlate;
  public bool IsDarkTheme => _settings?.Current.Theme == IdeTheme.Dark;
  public bool IsLightTheme => _settings?.Current.Theme == IdeTheme.Light;
  public bool IsSystemTheme => (_settings?.Current.Theme ?? IdeTheme.System) == IdeTheme.System;
  public IRootDock Layout { get; }
  public SolutionExplorerViewModel SolutionExplorer { get; }

  public string Status
  {
    get => _status;
    private set => SetProperty(ref _status, value);
  }

  public string Title => ActiveDocument is { } document ? $"{document.DisplayTitle} — Zero IDE" : "Zero IDE";

  public void ActivateDocument(EditorDocument document)
  {
    if (!Documents.Contains(document) ||
        ReferenceEquals(_activeDocument, document))
      return;

    _activeDocument = document;
    NotifyActiveDocumentChanged();
  }

  public async Task<bool> CanCloseAsync()
  {
    if (_closingForUpdate)
    {
      DockFactory.ApproveApplicationClose();
      return true;
    }

    if (IsBusy)
      return false;

    IsBusy = true;

    try
    {
      if (_debugSession is not null) await _debugSession.StopAsync();
      var canClose = await ConfirmSaveAsync();
      if (canClose)
        DockFactory.ApproveApplicationClose();

      return canClose;
    }
    catch (Exception ex)
    {
      await _dialogs.ShowMessageAsync("Unable to close document", ex.Message);
      return false;
    }
    finally
    {
      IsBusy = false;
    }
  }

  public Task<bool> CloseDocumentAsync(EditorDocument document) => CloseDocumentsAsync([document]);

  public async Task<bool> CloseDocumentsAsync(IReadOnlyList<EditorDocument> documents)
  {
    if (IsBusy || IsDebugging)
      return false;

    IsBusy = true;

    try
    {
      // Confirm the whole group first, so Cancel keeps a floating group intact.
      foreach (var document in documents)
      {
        if (Documents.Contains(document) && !await ConfirmSaveAsync(document))
          return false;
      }

      foreach (var document in documents)
      {
        if (Documents.Contains(document))
          RemoveDocument(document);
      }

      return true;
    }
    catch (Exception ex)
    {
      await _dialogs.ShowMessageAsync("Unable to close document", ex.Message);
      return false;
    }
    finally
    {
      IsBusy = false;
    }
  }

  public async Task OpenWorkspaceFileAsync(string path)
  {
    if (IsBusy || IsDebugging)
      return;

    IsBusy = true;

    try
    {
      var extension = Path.GetExtension(path);

      if (!string.Equals(extension, SourceFileExtensions.Assembly, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(extension, SourceFileExtensions.Source, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(extension, SourceFileExtensions.Header, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(extension, SourceFileExtensions.Zero, StringComparison.OrdinalIgnoreCase))
        return;

      var file = await _dialogs.GetLocalFileAsync(path)
        ?? throw new FileNotFoundException("The workspace file is unavailable.", path);

      await OpenDocumentAsync(file);
    }
    catch (Exception ex)
    {
      await _dialogs.ShowMessageAsync("Unable to open workspace file", ex.Message);
    }
    finally
    {
      IsBusy = false;
    }
  }

  private void AddDocument(EditorDocument document)
  {
    document.PropertyChanged += OnDocumentChanged;
    document.Editor.PropertyChanged += OnEditorDebugPropertyChanged;
    Documents.Add(document);
    DockFactory.AddDocument(document);
  }

  private async Task BuildAsync(bool cleanFirst, bool runAfterBuild = false, bool debugAfterBuild = false)
  {
    try
    {
      var document = ActiveDocument;
      if (document is null)
        return;

      var editor = document.Editor;
      var configuration = debugAfterBuild ? BuildConstants.DebugConfiguration : BuildConfiguration;
      var mode = debugAfterBuild ? EditorExecutionMode.Debug : runAfterBuild ? EditorExecutionMode.Run : EditorExecutionMode.Build;

      var request = new EditorExecutionRequest(
        editor.Settings.CompilerPath,
        configuration,
        cleanFirst,
        mode,
        SaveSourceAsync: async () =>
        {
          if ((document.File is null || editor.IsModified) && !await SaveAsync(document, false))
            return null;

          return document.File?.TryGetLocalPath()
            ?? throw new InvalidOperationException($"Building requires a local {SourceFileExtensions.Assembly} file.");
        }, StartDebuggerAsync: LaunchDebuggerAsync);

      var outcome = await _executionCoordinator!.ExecuteAsync(request, update =>
      {
        if (update.Stage == EditorExecutionStage.Build)
        {
          BuildState = EditorBuildState.Building;
          BuildOutput = Status = $"Building {editor.FileName} ({configuration})";
        }
        else if (update.Stage == EditorExecutionStage.Built)
        {
          BuildState = EditorBuildState.Idle;
          BuildOutput = update.Compilation!.Output + Environment.NewLine + $"Compiler exit code: {update.Compilation.ExitCode}";
          Status = $"Build succeeded: {update.Compilation.OutputPath}";
        }
        else if (update.Stage == EditorExecutionStage.Completed && mode == EditorExecutionMode.Run)
        {
          Status = $"Started {update.Compilation!.OutputPath}";
          BuildOutput += Environment.NewLine + Status;
        }
      });

      if (outcome.Stage == EditorExecutionStage.Failed)
      {
        if (outcome.Error is { } error)
        {
          await ReportBuildFailureAsync(error);
        }
        else
        {
          BuildState = EditorBuildState.Failed;
          Status = "Build failed";
          BuildOutput = outcome.Compilation!.Output + Environment.NewLine + $"Compiler exit code: {outcome.Compilation.ExitCode}";
          await _dialogs.ShowMessageAsync("Build failure", BuildOutput);
        }
      }
      else if (outcome.Stage == EditorExecutionStage.RunFailed)
      {
        BuildState = EditorBuildState.RunFailed;
        Status = "Run failed";
        BuildOutput += Environment.NewLine + outcome.Error!.Message;
        await _dialogs.ShowMessageAsync("Run failure", outcome.Error.Message);
      }
    }
    catch (Exception ex)
    {
      await ReportBuildFailureAsync(ex);
    }
  }

  private async Task CheckForUpdatesAsync()
  {
    if (!_updates!.IsSupported)
    {
      await _dialogs.ShowMessageAsync("Check for updates", "Installing updates from the IDE currently requires Windows x64. Use your platform's installer or package manager to update the IDE and compiler.");
      return;
    }

    try
    {
      Status = "Checking for updates...";
      var update = await _updates.CheckAsync();

      if (update is null)
      {
        Status = "Zero IDE is up to date";
        await _dialogs.ShowMessageAsync("Check for updates", "You are running the latest available Zero IDE installer version.");
        return;
      }

      if (!await _dialogs.ConfirmUpdateAsync(_updates.CurrentVersion, update.Version))
      {
        Status = "Update canceled";
        return;
      }

      Status = "Downloading and verifying update...";
      var download = await _updates.DownloadAsync(update);
      if (!await ConfirmSaveAsync())
      {
        Status = "Update canceled";
        return;
      }

      _updates.LaunchInstaller(download);
      // The installer waits for our PID before touching IDE or compiler files.
      _closingForUpdate = true;
      Status = "Starting update installer...";
      _dialogs.CloseWindow();
    }
    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
    {
      _closingForUpdate = false;
      Status = "Update canceled"; // UAC was declined; leave the document open.
    }
    catch (Exception ex)
    {
      _closingForUpdate = false;
      Status = "Update failed";
      await _dialogs.ShowMessageAsync("Unable to update Zero IDE", ex.Message);
    }
  }

  private async Task CleanAsync()
  {
    try
    {
      var sourcePath = ActiveDocument?.File?.TryGetLocalPath()
        ?? throw new InvalidOperationException($"Open or save a local {SourceFileExtensions.Assembly} file before cleaning.");

      _compilerService!.Clean(sourcePath, BuildConfiguration);
      BuildState = EditorBuildState.Idle;
      BuildOutput = Status = $"Cleaned {Editor.FileName} ({BuildConfiguration})";
    }
    catch (Exception ex)
    {
      await ReportBuildFailureAsync(ex);
    }
  }

  private async Task<bool> ConfirmSaveAsync()
  {
    foreach (var document in new List<EditorDocument>(Documents))
    {
      if (!await ConfirmSaveAsync(document))
        return false;
    }

    return true;
  }

  private async Task<bool> ConfirmSaveAsync(EditorDocument document)
  {
    if (!document.Editor.IsModified)
      return true;

    DockFactory.Activate(document);
    return await _dialogs.ConfirmSaveAsync(document.Editor.FileName) switch
    {
      SaveChangesChoice.Save => await SaveAsync(document, false),
      SaveChangesChoice.Discard => true,
      _ => false,
    };
  }

  private DelegateCommand CreateCommand(Func<Task> action, Func<bool>? canExecute = null) =>
      new DelegateCommand(async () =>
    {
      if (IsBusy || IsDebugging || canExecute?.Invoke() == false)
        return;

      IsBusy = true;

      try
      {
        await action();
      }
      catch (Exception ex)
      {
        Status = "Operation failed";
        await _dialogs.ShowMessageAsync("Operation failed", ex.Message);
      }
      finally
      {
        IsBusy = false;
      }
    }, () => !IsBusy && !IsDebugging && canExecute?.Invoke() != false)
      .ObservesProperty(() => IsBusy).ObservesProperty(() => IsDebugging);

  private void NotifyActiveDocumentChanged()
  {
    RaisePropertyChanged(nameof(ActiveDocument));
    RaisePropertyChanged(nameof(Editor));
    RaisePropertyChanged(nameof(HasActiveDocument));
    RaisePropertyChanged(nameof(Title));
  }

  private void NotifyThemeChanged()
  {
    RaisePropertyChanged(nameof(IsSystemTheme));
    RaisePropertyChanged(nameof(IsLightTheme));
    RaisePropertyChanged(nameof(IsDarkTheme));
    RaisePropertyChanged(nameof(IsCoolSlateTheme));
  }

  private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(EditorDocument.Title) or nameof(EditorDocument.IsModified))
    {
      RaisePropertyChanged(nameof(Title));
      if (sender is EditorDocument document)
        DockFactory.UpdateWindowTitle(document);
    }
  }

  private async Task OpenAsync()
  {
    var files = await _dialogs.OpenFilesAsync();
    foreach (var file in files) await OpenDocumentAsync(file);
  }

  private async Task OpenDocumentAsync(IStorageFile file)
  {
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    foreach (var document in Documents)
    {
      if (document.File is { } open && string.Equals(open.Path.AbsoluteUri, file.Path.AbsoluteUri, comparison))
      {
        DockFactory.Activate(document);
        return;
      }
    }

    await using var stream = await file.OpenReadAsync();
    using var reader = new StreamReader(stream);
    var text = await reader.ReadToEndAsync();

    var editor = new EditorViewModel(_settings);
    editor.Load(text, file.Name);
    AddDocument(new EditorDocument(editor) { File = file });
    Status = $"Opened {file.Name}";
  }

  private async Task OpenFolderAsync()
  {
    var path = await _dialogs.OpenFolderAsync();
    if (path is null)
      return;

    SolutionExplorer.LoadFolder(path);
    DockFactory.ShowExplorer();
  }

  private void RemoveDocument(EditorDocument document)
  {
    document.PropertyChanged -= OnDocumentChanged;
    document.Editor.PropertyChanged -= OnEditorDebugPropertyChanged;
    DockFactory.RemoveDocument(document);
    Documents.Remove(document);
    if (ReferenceEquals(_activeDocument, document))
    {
      _activeDocument = Documents.Count > 0 ? Documents[Documents.Count - 1] : null;
      if (_activeDocument is not null)
        DockFactory.Activate(_activeDocument);
    }

    document.Dispose();
    NotifyActiveDocumentChanged();
  }

  private async Task ReportBuildFailureAsync(Exception exception)
  {
    BuildState = EditorBuildState.Failed;
    Status = "Build failed";
    BuildOutput = exception.Message;

    await _dialogs.ShowMessageAsync("Build failure", BuildOutput);
  }

  private async Task SaveAllAsync()
  {
    var active = ActiveDocument;
    try
    {
      foreach (var document in new List<EditorDocument>(Documents))
      {
        if (!document.Editor.IsModified)
          continue;

        if (document.File is null)
          DockFactory.Activate(document);

        if (!await SaveAsync(document, false))
          return;
      }
    }
    finally
    {
      if (active is not null && Documents.Contains(active))
        DockFactory.Activate(active);
    }
  }

  private async Task<bool> SaveAsync(EditorDocument document, bool saveAs)
  {
    var editor = document.Editor;
    var file = saveAs || document.File is null ? await _dialogs.SaveFileAsync(editor.FileName) : document.File;

    if (file is null)
      return false;

    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    foreach (var other in Documents)
    {
      if (!ReferenceEquals(other, document) && other.File is { } open &&
          string.Equals(open.Path.AbsoluteUri, file.Path.AbsoluteUri, comparison))
        throw new InvalidOperationException("That file is already open in another tab. Save that tab or choose a different file name.");
    }

    if (editor.Settings.TrimTrailingWhitespaceOnSave)
      editor.TrimTrailingWhitespace();

    var text = editor.Text;

    await using (var stream = await file.OpenWriteAsync())
    {
      stream.SetLength(0);
      await using var writer = new StreamWriter(stream);
      await writer.WriteAsync(text);
    }

    document.File = file;
    editor.MarkSaved(file.Name, text);
    Status = $"Saved {file.Name}";

    return true;
  }

  private Task SelectThemeAsync(IdeTheme theme)
  {
    try
    {
      if (_settings is null)
        throw new InvalidOperationException("Settings are unavailable.");

      _settings.Save(_settings.Current with { Theme = theme });
      return Task.CompletedTask;
    }
    finally
    {
      NotifyThemeChanged();
    }
  }

  private Task ShowSolutionExplorerAsync()
  {
    DockFactory.ShowExplorer();
    return Task.CompletedTask;
  }
}
