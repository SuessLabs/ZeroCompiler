// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Input;
using Zero.Ide.Models;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Views;

public partial class SolutionExplorerView : UserControl
{
  public SolutionExplorerView() => InitializeComponent();

  private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
  {
    if (DataContext is SolutionExplorerViewModel model && WorkspaceTree.SelectedItem is WorkspaceNode node)
      model.CmdOpenItem.Execute(node);
  }
}
