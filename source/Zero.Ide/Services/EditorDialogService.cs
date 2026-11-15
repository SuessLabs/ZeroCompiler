// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Prism.Dialogs;
using Zero.Ide.Models;
using Zero.Ide.Views;

namespace Zero.Ide.Services;

public class EditorDialogService : IEditorDialogService
{
  public const string ConfirmSaveKey = "confirmSave";
  public const string ConfirmUpdateKey = "confirmUpdate";
  public const string MessageKey = "message";
  public const string TitleKey = "title";

  private readonly IDialogService _dialogService;

  public EditorDialogService(IDialogService dialogService)
  {
    _dialogService = dialogService;
  }

  private static Window Owner =>
    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
    ?? throw new InvalidOperationException("The editor window is not available.");

  public async Task<string?> BrowseCompilerAsync()
  {
    var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
    Window? activeWindow = null;
    if (desktop is not null)
      foreach (var window in desktop.Windows)
        if (window.IsActive) activeWindow = window;

    var owner = activeWindow ?? Owner;

    var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Select Zero compiler executable",
      AllowMultiple = false,
      FileTypeFilter = OperatingSystem.IsWindows()
        ? [new FilePickerFileType("Executables") { Patterns = ["*" + BuildConstants.WindowsExecutableExtension] }, FilePickerFileTypes.All]
        : [FilePickerFileTypes.All],
    });

    if (files.Count == 0)
      return null;

    return files[0].TryGetLocalPath()
      ?? throw new InvalidOperationException("Select a local compiler executable.");
  }

  public void CloseWindow() => Owner.Close();

  public async Task<SaveChangesChoice> ConfirmSaveAsync(string fileName)
  {
    var result = await ShowDialogAsync("Unsaved changes", $"Save changes to {fileName}?", true);
    return result.Result switch
    {
      ButtonResult.Yes => SaveChangesChoice.Save,
      ButtonResult.No => SaveChangesChoice.Discard,
      _ => SaveChangesChoice.Cancel,
    };
  }

  public async Task<bool> ConfirmUpdateAsync(string currentVersion, string latestVersion)
  {
    var result = await _dialogService.ShowDialogAsync(nameof(EditorDialogView), new DialogParameters
    {
      { TitleKey, "Update Zero IDE" },
      { MessageKey, $"Zero IDE {latestVersion} is available (current: {currentVersion}). Download and start the installer to update the IDE and bundled compiler? You will be prompted to save your work, then the IDE will close. Windows will ask for administrator permission." },
      { ConfirmUpdateKey, true },
      { KnownDialogParameters.WindowName, nameof(EditorDialogWindow) },
    });

    return result.Result == ButtonResult.Yes;
  }

  public async Task<IStorageFile?> GetLocalFileAsync(string path) =>
    await Owner.StorageProvider.TryGetFileFromPathAsync(path);

  public async Task<IStorageFile?> OpenFileAsync()
  {
    var files = await OpenFilesAsync();
    return files.Count > 0 ? files[0] : null;
  }

  public async Task<IReadOnlyList<IStorageFile>> OpenFilesAsync() =>
    await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Open source file",
      AllowMultiple = true,
      FileTypeFilter = [new FilePickerFileType("Zero source")
      {
        Patterns =
        [
          SourceFileExtensions.AssemblyPattern,
          SourceFileExtensions.SourcePattern,
          SourceFileExtensions.HeaderPattern,
          SourceFileExtensions.ZeroPattern,
        ],
      }, FilePickerFileTypes.All],
    });

  public async Task<string?> OpenFolderAsync()
  {
    var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
      Title = "Open workspace folder",
      AllowMultiple = false,
    });

    if (folders.Count == 0)
      return null;

    return folders[0].TryGetLocalPath()
      ?? throw new InvalidOperationException("Select a local workspace folder.");
  }

  public Task<IStorageFile?> SaveFileAsync(string suggestedName) =>
    Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = "Save source file",
      SuggestedFileName = suggestedName == "Untitled" ? "Untitled" + SourceFileExtensions.Assembly : suggestedName,
      ShowOverwritePrompt = true,
      FileTypeChoices =
      [
        new FilePickerFileType("Zero assembly")
        {
          Patterns = [SourceFileExtensions.AssemblyPattern]
        },
        new FilePickerFileType("Zero source") { Patterns = [SourceFileExtensions.SourcePattern] },
        FilePickerFileTypes.All,
      ],
    });

  public async Task ShowAboutAsync() =>
    await _dialogService.ShowDialogAsync(nameof(AboutDialogView), new DialogParameters
    {
      { KnownDialogParameters.WindowName, nameof(AboutDialogWindow) },
      { KnownDialogParameters.ShowNonModal, false },
    });

  public async Task ShowMessageAsync(string title, string message) =>
      await ShowDialogAsync(title, message, false);

  public async Task ShowOptionsAsync() =>
    await _dialogService.ShowDialogAsync(nameof(OptionsDialogView), new DialogParameters
    {
      { KnownDialogParameters.WindowName, nameof(OptionsDialogWindow) },
      { KnownDialogParameters.ShowNonModal, false },
    });

  private Task<IDialogResult> ShowDialogAsync(string title, string message, bool confirmSave) =>
    _dialogService.ShowDialogAsync(nameof(EditorDialogView), new DialogParameters
    {
      { TitleKey, title },
      { MessageKey, message },
      { ConfirmSaveKey, confirmSave },
      { KnownDialogParameters.WindowName, nameof(EditorDialogWindow) },
    });
}
