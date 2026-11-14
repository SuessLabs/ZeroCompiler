// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Prism.Commands;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
  private readonly IEditorDialogService _dialogs;
  private readonly ISettingsService? _settings;
  private IStorageFile? _file;
  private bool _isBusy;
  private string _status = "Ready";

  public MainWindowViewModel(EditorViewModel editor, IEditorDialogService dialogs, ISettingsService? settings = null)
  {
    Editor = editor;
    _dialogs = dialogs;
    _settings = settings;
    if (settings is not null)
      settings.SettingsChanged += (_, _) => NotifyThemeChanged();

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
    CmdBuild = new DelegateCommand(() => { }, () => false);
    CmdRebuild = new DelegateCommand(() => { }, () => false);
    CmdClean = new DelegateCommand(() => { }, () => false);
  }

  public DelegateCommand CmdAbout { get; }
  public DelegateCommand CmdBuild { get; }
  public DelegateCommand CmdClean { get; }
  public DelegateCommand CmdExit { get; }
  public DelegateCommand CmdNew { get; }
  public DelegateCommand CmdOpen { get; }
  public DelegateCommand CmdOptions { get; }
  public DelegateCommand CmdRebuild { get; }
  public DelegateCommand CmdSave { get; }
  public DelegateCommand CmdSaveAs { get; }
  public DelegateCommand CmdThemeSystem { get; }
  public DelegateCommand CmdThemeLight { get; }
  public DelegateCommand CmdThemeDark { get; }
  public DelegateCommand CmdThemeCoolSlate { get; }
  public bool IsSystemTheme => (_settings?.Current.Theme ?? IdeTheme.System) == IdeTheme.System;
  public bool IsLightTheme => _settings?.Current.Theme == IdeTheme.Light;
  public bool IsDarkTheme => _settings?.Current.Theme == IdeTheme.Dark;
  public bool IsCoolSlateTheme => _settings?.Current.Theme == IdeTheme.CoolSlate;
  public EditorViewModel Editor { get; }

  public bool IsBusy
  {
    get => _isBusy;
    private set => SetProperty(ref _isBusy, value);
  }

  public string Status
  {
    get => _status;
    private set => SetProperty(ref _status, value);
  }

  public string Title => $"{Editor.FileName}{(Editor.IsModified ? " *" : string.Empty)} — Zero IDE";

  private Task SelectThemeAsync(IdeTheme theme)
  {
    try
    {
      if (_settings is null) throw new InvalidOperationException("Settings are unavailable.");
      _settings.Save(_settings.Current with { Theme = theme });
      return Task.CompletedTask;
    }
    finally
    {
      NotifyThemeChanged();
    }
  }

  private void NotifyThemeChanged()
  {
    RaisePropertyChanged(nameof(IsSystemTheme));
    RaisePropertyChanged(nameof(IsLightTheme));
    RaisePropertyChanged(nameof(IsDarkTheme));
    RaisePropertyChanged(nameof(IsCoolSlateTheme));
  }

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

  private DelegateCommand CreateCommand(Func<Task> action) =>
      new DelegateCommand(async () =>
    {
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
    }, () => !IsBusy).ObservesProperty(() => IsBusy);

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

  private async Task<bool> SaveAsync(bool saveAs)
  {
    var file = saveAs || _file is null ? await _dialogs.SaveFileAsync(Editor.FileName) : _file;

    if (file is null)
      return false;

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
}
