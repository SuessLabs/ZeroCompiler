// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Prism.Commands;
using Zero.Ide.Highlighting;
using Zero.Ide.Models;
using Zero.Ide.Themes;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views;

public partial class EditorView : UserControl
{
  private UndoStack? _undoStack;
  private EditorViewModel? _viewModel;

  public EditorView()
  {
    InitializeComponent();
    Editor.Options.CutCopyWholeLine = false;

    Editor.DocumentChanged += (_, _) => ObserveUndoStack();
    Editor.TextArea.SelectionChanged += (_, _) => UpdateCommands();
    ActualThemeVariantChanged += (_, _) => UpdateHighlighting();
    DataContextChanged += (_, _) => ConnectViewModel();

    PropertyChanged += (_, change) =>
    {
      if (change.Property == IsEnabledProperty)
        UpdateCommands();
    };

    UpdateHighlighting();
    ObserveUndoStack();
    ConnectViewModel();
  }

  public DelegateCommand CmdCopy => new(() => Editor.Copy(), () => IsEnabled && Editor.CanCopy);
  public DelegateCommand CmdCut => new(() => Editor.Cut(), () => IsEnabled && Editor.CanCut);
  public DelegateCommand CmdPaste => new(() => Editor.Paste(), () => IsEnabled && !Editor.IsReadOnly);
  public DelegateCommand CmdRedo => new(() => Editor.Redo(), () => IsEnabled && Editor.CanRedo);
  public DelegateCommand CmdUndo => new(() => Editor.Undo(), () => IsEnabled && Editor.CanUndo);
  public TextEditor Editor => SourceEditor;

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ConnectViewModel();
    ObserveUndoStack();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    _viewModel?.PropertyChanged -= OnDocumentChanged;
    _undoStack?.PropertyChanged -= OnUndoStackChanged;

    _viewModel = null;
    _undoStack = null;

    base.OnDetachedFromVisualTree(e);
  }

  private void ApplySettings()
  {
    var settings = _viewModel?.Settings ?? IdeSettings.Default;
    Editor.FontFamily = new FontFamily(settings.FontFamily);
    Editor.FontSize = settings.FontSize;
    Editor.ShowLineNumbers = settings.ShowLineNumbers;
    Editor.WordWrap = settings.WordWrap;
    Editor.Options.IndentationSize = settings.IndentationSize;
    Editor.Options.ConvertTabsToSpaces = settings.ConvertTabsToSpaces;
  }

  private void ConnectViewModel()
  {
    _viewModel?.PropertyChanged -= OnDocumentChanged;
    _viewModel = DataContext as EditorViewModel;
    _viewModel?.PropertyChanged += OnDocumentChanged;
    ApplySettings();
  }

  private void ObserveUndoStack()
  {
    _undoStack?.PropertyChanged -= OnUndoStackChanged;
    _undoStack = Editor.Document?.UndoStack;
    _undoStack?.PropertyChanged += OnUndoStackChanged;
    UpdateCommands();
  }

  private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(EditorViewModel.Settings))
      ApplySettings();

    if (e.PropertyName != nameof(EditorViewModel.Revision))
      return;

    Editor.CaretOffset = 0;
    Editor.ScrollTo(1, 1, AvaloniaEdit.Rendering.VisualYPosition.LineTop, 0, 0);
    Editor.TextArea.Focus();
    UpdateCommands();
  }

  private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e) =>
    UpdateCommands();

  private void UpdateCommands()
  {
    CmdUndo.RaiseCanExecuteChanged();
    CmdRedo.RaiseCanExecuteChanged();
    CmdCut.RaiseCanExecuteChanged();
    CmdCopy.RaiseCanExecuteChanged();
    CmdPaste.RaiseCanExecuteChanged();
  }

  private void UpdateHighlighting() =>
    Editor.SyntaxHighlighting = ZasmHighlighting.Create(IdeThemes.IsDark(ActualThemeVariant));
}
