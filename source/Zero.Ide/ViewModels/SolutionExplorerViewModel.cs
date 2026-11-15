// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Dock.Model.Mvvm.Controls;
using Prism.Commands;
using Zero.Ide.Models;

namespace Zero.Ide.ViewModels;

public sealed class SolutionExplorerViewModel : Tool
{
  private readonly Func<string, Task> _openFile;
  private string? _workspacePath;

  public SolutionExplorerViewModel(DelegateCommand openFolder, Func<string, Task> openFile)
  {
    Id = "SolutionExplorer";
    Title = "Solution Explorer";

    CanClose = true;
    CanFloat = true;
    CanPin = true;

    _openFile = openFile;

    CmdOpenFolder = openFolder;
  }

  public DelegateCommand CmdOpenFolder { get; }

  public AsyncDelegateCommand<WorkspaceNode> CmdOpenItem => new(async node =>
  {
    if (node is { IsDirectory: false, IsPlaceholder: false })
      await _openFile(node.Path);
  });

  public DelegateCommand CmdRefresh => new(() =>
  {
    if (_workspacePath is not null)
      LoadFolder(_workspacePath);
  });

  public ObservableCollection<WorkspaceNode> Roots { get; } = [];

  public void LoadFolder(string path)
  {
    _workspacePath = path;
    Roots.Clear();
    Roots.Add(new WorkspaceNode(path, true) { IsExpanded = true });
  }
}
