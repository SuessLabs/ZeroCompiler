// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia.Platform.Storage;

namespace Zero.Ide.Services;

public enum SaveChangesChoice
{
  Cancel,
  Save,
  Discard,
}

public interface IEditorDialogService
{
  Task<string?> BrowseCompilerAsync();

  void CloseWindow();

  Task<SaveChangesChoice> ConfirmSaveAsync(string fileName);

  Task<bool> ConfirmUpdateAsync(string currentVersion, string latestVersion);

  Task<IStorageFile?> GetLocalFileAsync(string path);

  Task<IStorageFile?> OpenFileAsync();

  Task<IReadOnlyList<IStorageFile>> OpenFilesAsync();

  Task<string?> OpenFolderAsync();

  Task<IStorageFile?> SaveFileAsync(string suggestedName);

  Task ShowAboutAsync();

  Task ShowMessageAsync(string title, string message);

  Task ShowOptionsAsync();
}
