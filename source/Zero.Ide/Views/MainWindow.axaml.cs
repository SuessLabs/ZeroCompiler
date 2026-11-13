// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views
{
  public partial class MainWindow : Window
  {
    private bool _allowClose;
    private bool _confirmingClose;

    public MainWindow()
    {
      InitializeComponent();
      Closing += OnClosingAsync;
#if DEBUG
      this.AttachDevTools();
#endif
    }

    // Text selection, clipboard and undo history belong to the editor control.
    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
      if (DataContext is MainWindowViewModel { IsBusy: true }) return;
      var action = (sender as Control)?.Tag as string;
      switch (action)
      {
        case "Undo": EditorPane.Editor.Undo(); break;
        case "Redo": EditorPane.Editor.Redo(); break;
        case "Cut": EditorPane.Editor.Cut(); break;
        case "Copy": EditorPane.Editor.Copy(); break;
        case "Paste": EditorPane.Editor.Paste(); break;
      }

      EditorPane.Editor.Focus();
    }

    private async void OnClosingAsync(object? sender, WindowClosingEventArgs e)
    {
      if (_allowClose || DataContext is not MainWindowViewModel viewModel) return;
      e.Cancel = true;
      if (_confirmingClose) return;
      _confirmingClose = true;
      try
      {
        if (await viewModel.CanCloseAsync())
        {
          _allowClose = true;
          Dispatcher.UIThread.Post(Close);
        }
      }
      finally
      {
        _confirmingClose = false;
      }
    }
  }
}
