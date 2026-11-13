// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Prism.Dialogs;
using Zero.Ide.Views;

namespace Zero.Ide.Services;

public class EditorDialogService : IEditorDialogService
{
  private readonly IDialogService _dialogService;

  public EditorDialogService(IDialogService dialogService)
  {
    _dialogService = dialogService;
  }

  private static Window Owner =>
    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
    ?? throw new InvalidOperationException("The editor window is not available.");

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

  public async Task<IStorageFile?> OpenFileAsync()
  {
    var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Open source file",
      AllowMultiple = false,
      FileTypeFilter = [new FilePickerFileType("Zero source") { Patterns = ["*.zasm", "*.zh", "*.zero"] }, FilePickerFileTypes.All],
    });
    return files.Count > 0 ? files[0] : null;
  }

  public Task<IStorageFile?> SaveFileAsync(string suggestedName) =>
    Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = "Save source file",
      SuggestedFileName = suggestedName == "Untitled" ? "Untitled.zasm" : suggestedName,
      ShowOverwritePrompt = true,
      FileTypeChoices = [new FilePickerFileType("Zero assembly") { Patterns = ["*.zasm"] }, FilePickerFileTypes.All],
    });

  public async Task ShowMessageAsync(string title, string message) =>
    await ShowDialogAsync(title, message, false);

  public async Task ShowAboutAsync() =>
    await _dialogService.ShowDialogAsync(nameof(AboutDialogView), new DialogParameters
    {
      { KnownDialogParameters.WindowName, nameof(AboutDialogWindow) },
      { KnownDialogParameters.ShowNonModal, false },
    });

  public async Task ShowOptionsAsync() =>
    await _dialogService.ShowDialogAsync(nameof(OptionsDialogView), new DialogParameters
    {
      { KnownDialogParameters.WindowName, nameof(OptionsDialogWindow) },
      { KnownDialogParameters.ShowNonModal, false },
    });

  private Task<IDialogResult> ShowDialogAsync(string title, string message, bool confirmSave) =>
    _dialogService.ShowDialogAsync(nameof(EditorDialogView), new DialogParameters
    {
      { "title", title },
      { "message", message },
      { "confirmSave", confirmSave },
      { KnownDialogParameters.WindowName, nameof(EditorDialogWindow) },
    });
}
