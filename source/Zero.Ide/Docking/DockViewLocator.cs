// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Zero.Ide.ViewModels;
using Zero.Ide.Views;

namespace Zero.Ide.Docking;

/// <summary>Recycles dock content only when the existing view matches the requested view model.</summary>
public sealed class DockViewLocator : IRecyclingDataTemplate
{
  public bool Match(object? data) => data is EditorDocument or DebuggerViewModel or SolutionExplorerViewModel;

  public Control? Build(object? data) => Build(data, null);

  public Control? Build(object? data, Control? existing)
  {
    // Dock can supply the previous tab's view, even when it belongs to a different template.
    Control? view = data switch
    {
      EditorDocument => existing as EditorView ?? new EditorView(),
      DebuggerViewModel => existing as DebuggerView ?? new DebuggerView(),
      SolutionExplorerViewModel => existing as SolutionExplorerView ?? new SolutionExplorerView(),
      _ => null,
    };

    view?.DataContext = data is EditorDocument document ? document.Editor : data;

    return view;
  }
}
