// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Prism.Commands;
using Zero.Ide.Highlighting;
using Zero.Ide.Models;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views;

public partial class EditorView : UserControl
{
  private UndoStack? _undoStack;
  private EditorViewModel? _viewModel;

  public EditorView()
  {
    CmdUndo = new DelegateCommand(() => Editor.Undo(), () => IsEnabled && Editor.CanUndo);
    CmdRedo = new DelegateCommand(() => Editor.Redo(), () => IsEnabled && Editor.CanRedo);
    CmdCut = new DelegateCommand(() => Editor.Cut(), () => IsEnabled && Editor.CanCut);
    CmdCopy = new DelegateCommand(() => Editor.Copy(), () => IsEnabled && Editor.CanCopy);
    CmdPaste = new DelegateCommand(() => Editor.Paste(), () => IsEnabled && !Editor.IsReadOnly);

    InitializeComponent();
    Editor.Options.CutCopyWholeLine = false;

    Editor.DocumentChanged += (_, _) => ObserveUndoStack();
    Editor.TextArea.SelectionChanged += (_, _) => UpdateCommands();
    ActualThemeVariantChanged += (_, _) => UpdateHighlighting();
    DataContextChanged += (_, _) => ConnectViewModel();
    PropertyChanged += (_, change) =>
    {
      if (change.Property == IsEnabledProperty) UpdateCommands();
    };

    UpdateHighlighting();
    ObserveUndoStack();
    ConnectViewModel();
  }

  public DelegateCommand CmdCopy { get; }
  public DelegateCommand CmdCut { get; }
  public DelegateCommand CmdPaste { get; }
  public DelegateCommand CmdRedo { get; }
  public DelegateCommand CmdUndo { get; }
  public TextEditor Editor => SourceEditor;

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ConnectViewModel();
    ObserveUndoStack();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    if (_viewModel is not null) _viewModel.PropertyChanged -= OnDocumentChanged;
    if (_undoStack is not null) _undoStack.PropertyChanged -= OnUndoStackChanged;
    _viewModel = null;
    _undoStack = null;
    base.OnDetachedFromVisualTree(e);
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
    if (_undoStack is not null) _undoStack.PropertyChanged -= OnUndoStackChanged;
    _undoStack = Editor.Document?.UndoStack;
    if (_undoStack is not null) _undoStack.PropertyChanged += OnUndoStackChanged;
    UpdateCommands();
  }

  private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(EditorViewModel.Settings)) ApplySettings();
    if (e.PropertyName != nameof(EditorViewModel.Revision)) return;
    Editor.CaretOffset = 0;
    Editor.ScrollTo(1, 1, AvaloniaEdit.Rendering.VisualYPosition.LineTop, 0, 0);
    Editor.TextArea.Focus();
    UpdateCommands();
  }

  private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e) => UpdateCommands();

  private void UpdateCommands()
  {
    CmdUndo.RaiseCanExecuteChanged();
    CmdRedo.RaiseCanExecuteChanged();
    CmdCut.RaiseCanExecuteChanged();
    CmdCopy.RaiseCanExecuteChanged();
    CmdPaste.RaiseCanExecuteChanged();
  }

  private void UpdateHighlighting() =>
    Editor.SyntaxHighlighting = ZasmHighlighting.Create(ActualThemeVariant == ThemeVariant.Dark);

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
}
