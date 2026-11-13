// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Zero.Ide.Services;

public class EditorDialogService : IEditorDialogService
{
  private static Window Owner =>
    (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
    ?? throw new InvalidOperationException("The editor window is not available.");

  public void CloseWindow() => Owner.Close();

  public Task<SaveChangesChoice> ConfirmSaveAsync(string fileName) =>
    ShowDialogAsync("Unsaved changes", $"Save changes to {fileName}?",
      [("Save", SaveChangesChoice.Save), ("Don't Save", SaveChangesChoice.Discard), ("Cancel", SaveChangesChoice.Cancel)]);

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
    await ShowDialogAsync(title, message, [("OK", SaveChangesChoice.Cancel)]);

  private static Task<SaveChangesChoice> ShowDialogAsync(
    string title, string message, (string Label, SaveChangesChoice Result)[] choices)
  {
    var dialog = new Window
    {
      Title = title,
      Width = 440,
      SizeToContent = SizeToContent.Height,
      CanResize = false,
      ShowInTaskbar = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };

    var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

    foreach (var (label, result) in choices)
    {
      var button = new Button { Content = label, MinWidth = 80, IsCancel = label is "Cancel" or "OK", IsDefault = label is "Save" or "OK" };
      button.Click += (_, _) => dialog.Close(result);
      buttons.Children.Add(button);
    }

    dialog.Content = new StackPanel
    {
      Margin = new Thickness(24),
      Spacing = 24,
      Children =
      {
        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        buttons,
      },
    };

    return dialog.ShowDialog<SaveChangesChoice>(Owner);
  }
}
