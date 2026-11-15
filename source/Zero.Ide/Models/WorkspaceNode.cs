// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Models;

public sealed class WorkspaceNode : ViewModelBase
{
  private string _errorMessage = string.Empty;
  private bool _isExpanded;
  private bool _loaded;
  private bool _loading;

  public WorkspaceNode(string path, bool isDirectory, bool canExpand = true)
  {
    Path = path;
    Name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));

    if (Name.Length == 0)
      Name = path;

    IsDirectory = isDirectory;
    CanExpand = isDirectory && canExpand;

    if (CanExpand)
      Children.Add(new WorkspaceNode());
  }

  private WorkspaceNode()
  {
    Path = string.Empty;
    Name = "Loading...";
    IsPlaceholder = true;
  }

  public bool CanExpand { get; }
  public ObservableCollection<WorkspaceNode> Children { get; } = [];
  public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
  public bool IsDirectory { get; }

  public bool IsExpanded
  {
    get => _isExpanded;
    set
    {
      if (SetProperty(ref _isExpanded, value) && value)
        _ = LoadChildrenAsync();
    }
  }

  public bool IsPlaceholder { get; }

  public string Name { get; }

  public string Path { get; }

  public async Task LoadChildrenAsync()
  {
    if (!CanExpand || _loaded || _loading)
      return;

    _loading = true;

    try
    {
      // Enumerate only the expanded directory, off the UI thread. Never recurse into links.
      var entries = await Task.Run(() =>
      {
        var items = new List<(string FullName, bool IsDirectory, bool CanExpand, int OriginalIndex)>();
        foreach (var entry in new DirectoryInfo(Path).EnumerateFileSystemInfos())
          items.Add((entry.FullName,
            (entry.Attributes & FileAttributes.Directory) != 0,
            (entry.Attributes & FileAttributes.ReparsePoint) == 0,
            items.Count));

        items.Sort(static (left, right) =>
        {
          int comparison = right.IsDirectory.CompareTo(left.IsDirectory);
          if (comparison == 0)
            comparison = StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName);

          return comparison != 0 ? comparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
        });
        return items;
      });

      Children.Clear();

      foreach (var entry in entries)
        Children.Add(new WorkspaceNode(entry.FullName, entry.IsDirectory, entry.CanExpand));

      _loaded = true;
      ErrorMessage = string.Empty;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      Children.Clear();
      ErrorMessage = ex.Message;
    }
    finally
    {
      _loading = false;
    }
  }
}
