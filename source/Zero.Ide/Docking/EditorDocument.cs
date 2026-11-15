// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Dock.Model.Mvvm.Controls;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Docking;

public sealed class EditorDocument : Document, IDisposable
{
  public const string ModifiedSuffix = "*";

  public EditorDocument(EditorViewModel editor)
  {
    Id = Guid.NewGuid().ToString("N");
    Editor = editor;
    CanClose = true;
    CanFloat = true;
    CanPin = false;

    Editor.PropertyChanged += OnEditorChanged;

    UpdateTitle();
  }

  public EditorViewModel Editor { get; }

  public string DisplayTitle => Title + (IsModified ? ModifiedSuffix : string.Empty);

  public IStorageFile? File { get; set; }

  public void Dispose()
  {
    Editor.PropertyChanged -= OnEditorChanged;
    Editor.Dispose();
  }

  private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(EditorViewModel.FileName) or nameof(EditorViewModel.IsModified))
      UpdateTitle();
  }

  private void UpdateTitle()
  {
    IsModified = Editor.IsModified;

    // Dock renders its own modified suffix beside Title; keep the base name unadorned.
    Title = Editor.FileName;
    OnPropertyChanged(nameof(DisplayTitle));
  }
}
