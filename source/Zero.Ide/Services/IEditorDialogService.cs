// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
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
  void CloseWindow();

  Task<SaveChangesChoice> ConfirmSaveAsync(string fileName);

  Task<IStorageFile?> OpenFileAsync();

  Task<IStorageFile?> SaveFileAsync(string suggestedName);

  Task ShowAboutAsync();

  Task ShowMessageAsync(string title, string message);
}
