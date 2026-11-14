// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Zero.Ide.Services;

namespace Zero.Ide.Adapters;

// Owns subscriptions only while the editor view is attached to the visual tree.
internal sealed class AvaloniaEditActions : IEditorActions, IDisposable
{
  private readonly TextEditor _editor;
  private UndoStack? _undoStack;

  public AvaloniaEditActions(TextEditor editor)
  {
    _editor = editor;
    _editor.DocumentChanged += OnDocumentChanged;
    _editor.TextArea.SelectionChanged += OnSelectionChanged;
    _editor.PropertyChanged += OnEditorPropertyChanged;

    ObserveUndoStack();
  }

  public event EventHandler? StateChanged;

  public bool CanCopy => _editor.IsEffectivelyEnabled && _editor.CanCopy;
  public bool CanCut => _editor.IsEffectivelyEnabled && !_editor.IsReadOnly && _editor.CanCut;
  public bool CanPaste => _editor.IsEffectivelyEnabled && !_editor.IsReadOnly;
  public bool CanRedo => _editor.IsEffectivelyEnabled && !_editor.IsReadOnly && _editor.CanRedo;
  public bool CanUndo => _editor.IsEffectivelyEnabled && !_editor.IsReadOnly && _editor.CanUndo;

  public void Copy() => _editor.Copy();

  public void Cut() => _editor.Cut();

  public void Dispose()
  {
    _editor.DocumentChanged -= OnDocumentChanged;
    _editor.TextArea.SelectionChanged -= OnSelectionChanged;
    _editor.PropertyChanged -= OnEditorPropertyChanged;

    _undoStack?.PropertyChanged -= OnUndoStackChanged;

    _undoStack = null;
  }

  public void Paste() => _editor.Paste();

  public void Redo() => _editor.Redo();

  public void Undo() => _editor.Undo();

  private void ObserveUndoStack()
  {
    _undoStack?.PropertyChanged -= OnUndoStackChanged;
    _undoStack = _editor.Document?.UndoStack;
    _undoStack?.PropertyChanged += OnUndoStackChanged;

    StateChanged?.Invoke(this, EventArgs.Empty);
  }

  private void OnDocumentChanged(object? sender, EventArgs e) =>
    ObserveUndoStack();

  private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
  {
    if (e.Property == InputElement.IsEffectivelyEnabledProperty || e.Property == TextEditor.IsReadOnlyProperty)
      StateChanged?.Invoke(this, EventArgs.Empty);
  }

  private void OnSelectionChanged(object? sender, EventArgs e) =>
      StateChanged?.Invoke(this, EventArgs.Empty);

  private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e) =>
    StateChanged?.Invoke(this, EventArgs.Empty);
}
