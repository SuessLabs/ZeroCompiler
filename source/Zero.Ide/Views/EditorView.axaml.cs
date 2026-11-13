// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Avalonia.Controls;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views;

public partial class EditorView : UserControl
{
  private EditorViewModel? _viewModel;

  public EditorView()
  {
    InitializeComponent();
    DataContextChanged += (_, _) => ConnectViewModel();
    ConnectViewModel();
  }

  public TextBox Editor => SourceEditor;

  private void ConnectViewModel()
  {
    _viewModel?.PropertyChanged -= OnDocumentChanged;
    _viewModel = DataContext as EditorViewModel;
    _viewModel?.PropertyChanged += OnDocumentChanged;
  }

  private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(EditorViewModel.Revision)) return;
    Editor.IsUndoEnabled = false;
    Editor.IsUndoEnabled = true;
    Editor.CaretIndex = 0;
    Editor.Focus();
  }
}
