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
  private readonly ISettingsService? _settings;
  private string _buildConfiguration = "Debug";
  private string _buildOutput = string.Empty;
  private EditorBuildState _buildState;
  private IStorageFile? _file;
  private bool _isBusy;
  private string _status = "Ready";

  public MainWindowViewModel(EditorViewModel editor, IEditorDialogService dialogs, ISettingsService? settings = null,
    ICompilerService? compilerService = null)
  {
    Editor = editor;
    _dialogs = dialogs;
    _settings = settings;
    _compilerService = compilerService;
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
    CmdOptions = CreateCommand(_dialogs.ShowOptionsAsync);
    CmdThemeSystem = CreateCommand(() => SelectThemeAsync(IdeTheme.System));
    CmdThemeLight = CreateCommand(() => SelectThemeAsync(IdeTheme.Light));
    CmdThemeDark = CreateCommand(() => SelectThemeAsync(IdeTheme.Dark));
    CmdThemeCoolSlate = CreateCommand(() => SelectThemeAsync(IdeTheme.CoolSlate));
    CmdBuild = CreateCommand(() => BuildAsync(false), () => _compilerService is not null);
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
      if (value is not ("Debug" or "Release"))
        throw new ArgumentException("Choose Debug or Release.");

      SetProperty(ref _buildConfiguration, value);
    }
  }

  public IReadOnlyList<string> BuildConfigurations { get; } = Array.AsReadOnly<string>(["Debug", "Release"]);

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
    }
  }

  public string BuildStatus => BuildState switch
  {
    EditorBuildState.Building => "Building...",
    EditorBuildState.Failed => "Build failure",
    _ => "Idle",
  };

  public DelegateCommand CmdAbout { get; }
  public DelegateCommand CmdBuild { get; }
  public DelegateCommand CmdBuildOutput { get; }
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

  private async Task BuildAsync(bool cleanFirst)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(Editor.Settings.CompilerPath))
        throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");

      if ((_file is null || Editor.IsModified) && !await SaveAsync(false))
        return;

      var sourcePath = _file?.TryGetLocalPath() ?? throw new InvalidOperationException("Building requires a local .zasm file.");

      if (!string.Equals(Path.GetExtension(sourcePath), ".zasm", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Only .zasm source files can be built.");

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
    }
    catch (Exception ex)
    {
      await ReportBuildFailureAsync(ex);
    }
  }

  private async Task CleanAsync()
  {
    try
    {
      var sourcePath = _file?.TryGetLocalPath()
        ?? throw new InvalidOperationException("Open or save a local .zasm file before cleaning.");

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
