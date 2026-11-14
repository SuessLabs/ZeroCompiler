// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using Zero.Ide.Adapters;
using Zero.Ide.Highlighting;
using Zero.Ide.Models;
using Zero.Ide.Themes;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views;

public partial class EditorView : UserControl
{
  private AvaloniaEditActions? _editorActions;
  private EditorViewModel? _viewModel;

  public EditorView()
  {
    InitializeComponent();
    Editor.Options.CutCopyWholeLine = false;

    ActualThemeVariantChanged += (_, _) => UpdateHighlighting();
    DataContextChanged += (_, _) => ConnectViewModel();

    UpdateHighlighting();
    ConnectViewModel();
  }

  public TextEditor Editor => SourceEditor;

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    _editorActions = new AvaloniaEditActions(Editor);
    ConnectViewModel();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    _viewModel?.PropertyChanged -= OnDocumentChanged;
    if (_editorActions is not null)
    {
      _viewModel?.DetachEditorActions(_editorActions);
      _editorActions.Dispose();
    }

    _viewModel = null;
    _editorActions = null;

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
    if (_editorActions is not null) _viewModel?.DetachEditorActions(_editorActions);
    _viewModel = DataContext as EditorViewModel;
    _viewModel?.PropertyChanged += OnDocumentChanged;
    if (_editorActions is not null) _viewModel?.AttachEditorActions(_editorActions);
    ApplySettings();
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
  }

  private void UpdateHighlighting() =>
    Editor.SyntaxHighlighting = ZasmHighlighting.Create(IdeThemes.IsDark(ActualThemeVariant));
}
