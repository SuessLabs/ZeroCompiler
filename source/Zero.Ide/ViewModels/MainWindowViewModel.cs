// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Prism.Commands;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
  private readonly ICompilerService? _compilerService;
  private readonly IEditorDialogService _dialogs;
  private readonly IProgramRunner? _programRunner;
  private readonly ISettingsService? _settings;
  private readonly IUpdateService? _updates;

  private string _buildConfiguration = BuildConstants.DebugConfiguration;
  private string _buildOutput = string.Empty;
  private EditorBuildState _buildState;
  private bool _closingForUpdate;
  private IStorageFile? _file;
  private bool _isBusy;
  private string _status = "Ready";

  public MainWindowViewModel(EditorViewModel editor, IEditorDialogService dialogs, ISettingsService? settings = null,
    ICompilerService? compilerService = null, IProgramRunner? programRunner = null, IUpdateService? updates = null)
  {
    Editor = editor;
    _dialogs = dialogs;
    _settings = settings;
    _compilerService = compilerService;
    _programRunner = programRunner;
    _updates = updates;
    settings?.SettingsChanged += (_, _) => NotifyThemeChanged();

    Editor.PropertyChanged += (_, _) => RaisePropertyChanged(nameof(Title));

    CmdNew = CreateCommand(async () =>
    {
      if (!await ConfirmSaveAsync())
        return;

      _file = null;
      Editor.Load(string.Empty, "Untitled");
      Status = "New document";
    });

    CmdOpen = CreateCommand(OpenAsync);
    CmdSave = CreateCommand(async () => { await SaveAsync(false); });
    CmdSaveAs = CreateCommand(async () => { await SaveAsync(true); });
    CmdExit = new DelegateCommand(() => _dialogs.CloseWindow(), () => !IsBusy).ObservesProperty(() => IsBusy);
    CmdAbout = CreateCommand(_dialogs.ShowAboutAsync);
    CmdCheckForUpdates = CreateCommand(CheckForUpdatesAsync, () => _updates is not null);
    CmdOptions = CreateCommand(_dialogs.ShowOptionsAsync);
    CmdThemeSystem = CreateCommand(() => SelectThemeAsync(IdeTheme.System));
    CmdThemeLight = CreateCommand(() => SelectThemeAsync(IdeTheme.Light));
    CmdThemeDark = CreateCommand(() => SelectThemeAsync(IdeTheme.Dark));
    CmdThemeCoolSlate = CreateCommand(() => SelectThemeAsync(IdeTheme.CoolSlate));
    CmdBuild = CreateCommand(() => BuildAsync(false), () => _compilerService is not null);
    CmdBuildAndRun = CreateCommand(() => BuildAsync(false, true), () => _compilerService is not null && _programRunner is not null);
    CmdRebuild = CreateCommand(() => BuildAsync(true), () => _compilerService is not null);
    CmdClean = CreateCommand(CleanAsync, () => _compilerService is not null);
    CmdBuildOutput = CreateCommand(() => _dialogs.ShowMessageAsync("Build output", BuildOutput),
      () => !string.IsNullOrEmpty(BuildOutput)).ObservesProperty(() => BuildOutput);
  }

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
  public DelegateCommand CmdExit { get; }
  public DelegateCommand CmdNew { get; }
  public DelegateCommand CmdOpen { get; }
  public DelegateCommand CmdOptions { get; }
  public DelegateCommand CmdRebuild { get; }
  public DelegateCommand CmdSave { get; }
  public DelegateCommand CmdSaveAs { get; }
  public DelegateCommand CmdThemeCoolSlate { get; }
  public DelegateCommand CmdThemeDark { get; }
  public DelegateCommand CmdThemeLight { get; }
  public DelegateCommand CmdThemeSystem { get; }
  public EditorViewModel Editor { get; }
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

  public string Status
  {
    get => _status;
    private set => SetProperty(ref _status, value);
  }

  public string Title => $"{Editor.FileName}{(Editor.IsModified ? " *" : string.Empty)} — Zero IDE";

  public async Task<bool> CanCloseAsync()
  {
    if (_closingForUpdate) return true;
    if (IsBusy)
      return false;

    IsBusy = true;

    try
    {
      return await ConfirmSaveAsync();
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

  private async Task BuildAsync(bool cleanFirst, bool runAfterBuild = false)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(Editor.Settings.CompilerPath))
        throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");

      if ((_file is null || Editor.IsModified) && !await SaveAsync(false))
        return;

      var sourcePath = _file?.TryGetLocalPath() ?? throw new InvalidOperationException($"Building requires a local {SourceFileExtensions.Assembly} file.");

      if (!string.Equals(Path.GetExtension(sourcePath), SourceFileExtensions.Assembly, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Only {SourceFileExtensions.Assembly} source files can be built.");

      BuildState = EditorBuildState.Building;
      Status = $"Building {Editor.FileName} ({BuildConfiguration})";
      BuildOutput = Status;

      var result = await _compilerService!.BuildAsync(Editor.Settings.CompilerPath, sourcePath, BuildConfiguration, cleanFirst);

      BuildOutput = result.Output + Environment.NewLine + $"Compiler exit code: {result.ExitCode}";

      if (!result.Succeeded)
      {
        BuildState = EditorBuildState.Failed;
        Status = "Build failed";
        await _dialogs.ShowMessageAsync("Build failure", BuildOutput);
        return;
      }

      BuildState = EditorBuildState.Idle;
      Status = $"Build succeeded: {result.OutputPath}";
      if (runAfterBuild)
      {
        try
        {
          _programRunner!.Run(result.OutputPath, Path.GetDirectoryName(sourcePath)!);
          Status = $"Started {result.OutputPath}";
          BuildOutput += Environment.NewLine + Status;
        }
        catch (Exception ex)
        {
          BuildState = EditorBuildState.RunFailed;
          Status = "Run failed";
          BuildOutput += Environment.NewLine + ex.Message;
          await _dialogs.ShowMessageAsync("Run failure", ex.Message);
        }
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
      var sourcePath = _file?.TryGetLocalPath()
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
    if (!Editor.IsModified)
      return true;

    return await _dialogs.ConfirmSaveAsync(Editor.FileName) switch
    {
      SaveChangesChoice.Save => await SaveAsync(false),
      SaveChangesChoice.Discard => true,
      _ => false,
    };
  }

  private DelegateCommand CreateCommand(Func<Task> action, Func<bool>? canExecute = null) =>
      new DelegateCommand(async () =>
    {
      if (IsBusy || canExecute?.Invoke() == false)
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
    }, () => !IsBusy && canExecute?.Invoke() != false).ObservesProperty(() => IsBusy);

  private void NotifyThemeChanged()
  {
    RaisePropertyChanged(nameof(IsSystemTheme));
    RaisePropertyChanged(nameof(IsLightTheme));
    RaisePropertyChanged(nameof(IsDarkTheme));
    RaisePropertyChanged(nameof(IsCoolSlateTheme));
  }

  private async Task OpenAsync()
  {
    if (!await ConfirmSaveAsync())
      return;

    var file = await _dialogs.OpenFileAsync();

    if (file is null)
      return;

    await using var stream = await file.OpenReadAsync();
    using var reader = new StreamReader(stream);
    var text = await reader.ReadToEndAsync();

    _file = file;
    Editor.Load(text, file.Name);
    Status = $"Opened {file.Name}";
  }

  private async Task ReportBuildFailureAsync(Exception exception)
  {
    BuildState = EditorBuildState.Failed;
    Status = "Build failed";
    BuildOutput = exception.Message;

    await _dialogs.ShowMessageAsync("Build failure", BuildOutput);
  }

  private async Task<bool> SaveAsync(bool saveAs)
  {
    var file = saveAs || _file is null ? await _dialogs.SaveFileAsync(Editor.FileName) : _file;

    if (file is null)
      return false;

    if (Editor.Settings.TrimTrailingWhitespaceOnSave)
      Editor.TrimTrailingWhitespace();

    var text = Editor.Text;

    await using (var stream = await file.OpenWriteAsync())
    {
      stream.SetLength(0);
      await using var writer = new StreamWriter(stream);
      await writer.WriteAsync(text);
    }

    _file = file;
    Editor.MarkSaved(file.Name, text);
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
}
