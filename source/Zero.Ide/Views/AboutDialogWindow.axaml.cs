// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia.Controls;
using Prism.Dialogs;

namespace Zero.Ide.Views;

public partial class AboutDialogWindow : Window, IDialogWindow
{
  public AboutDialogWindow()
  {
    InitializeComponent();
  }

  public IDialogResult Result { get; set; } = new DialogResult(ButtonResult.Cancel);
}
