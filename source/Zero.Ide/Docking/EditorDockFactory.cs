// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Zero.Ide.Input;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Docking;

public sealed class EditorDockFactory : Factory
{
  private readonly HashSet<IDockWindow> _approvedWindows = [];
  private readonly HashSet<IDockWindow> _closingWindows = [];
  private readonly MainWindowViewModel _owner;

  private bool _applicationClosing;
  private DocumentDock _documents = null!;
  private ToolDock _explorerDock = null!;
  private ProportionalDock _main = null!;

  public EditorDockFactory(MainWindowViewModel owner)
  {
    _owner = owner;
    HideDocumentsOnClose = false;
    HideToolsOnClose = true;
    DefaultHostWindowLocator = CreateHostWindow;
  }

  public void Activate(EditorDocument document)
  {
    SetActiveDockable(document);
    if (document.Owner is IDock dock)
      SetFocusedDockable(dock, document);

    _owner.ActivateDocument(document);
  }

  public void AddDocument(EditorDocument document)
  {
    AddDockable(_documents, document);
    Activate(document);
  }

  public void ApproveApplicationClose() => _applicationClosing = true;

  public override void CloseAllDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync([.. visible.OfType<EditorDocument>()]);
  }

  public override void CloseDockable(IDockable dockable)
  {
    if (dockable is EditorDocument document)
    {
      _ = _owner.CloseDocumentAsync(document);
      return;
    }

    if (!_owner.IsBusy)
      base.CloseDockable(dockable);
  }

  public override void CloseLeftDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync([.. visible
        .Take(visible.IndexOf(dockable))
        .OfType<EditorDocument>()]);
  }

  public override void CloseOtherDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync([.. visible
        .OfType<EditorDocument>()
        .Where(document => !ReferenceEquals(document, dockable))]);
  }

  public override void CloseRightDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync([.. visible
        .Skip(visible.IndexOf(dockable) + 1)
        .OfType<EditorDocument>()]);
  }

  public override IRootDock CreateLayout()
  {
    _documents = new DocumentDock
    {
      Id = "Documents",
      IsCollapsable = false,
      CanCloseLastDockable = true,
      CanCreateDocument = false,
      ActiveDockable = _owner.ActiveDocument,
      VisibleDockables = CreateList<IDockable>(_owner.Documents.Cast<IDockable>().ToArray()),
    };

    _explorerDock = CreateExplorerDock();

    _main = new ProportionalDock
    {
      Id = "Workspace",
      IsCollapsable = false,
      Orientation = Orientation.Horizontal,
      VisibleDockables = CreateList<IDockable>(_explorerDock, new ProportionalDockSplitter(), _documents),
    };

    var root = CreateRootDock();
    root.Id = "Root";
    root.IsCollapsable = false;

    var debuggerDock = new ToolDock
    {
      Id = "DebuggerDock",
      Alignment = Alignment.Bottom,
      Proportion = 0.24,
      ActiveDockable = _owner.Debugger,
      VisibleDockables = CreateList<IDockable>(_owner.Debugger),
    };

    var vertical = new ProportionalDock
    {
      Id = "WorkspaceAndDebugger",
      IsCollapsable = false,
      Orientation = Orientation.Vertical,
      VisibleDockables = CreateList<IDockable>(_main, new ProportionalDockSplitter(), debuggerDock),
    };

    root.VisibleDockables = CreateList<IDockable>(vertical);
    root.DefaultDockable = vertical;
    root.ActiveDockable = vertical;
    return root;
  }

  public override void OnActiveDockableChanged(IDockable? dockable)
  {
    base.OnActiveDockableChanged(dockable);
    if (dockable is EditorDocument document)
    {
      _owner.ActivateDocument(document);
      UpdateWindowTitle(document);
    }
  }

  public override void OnFocusedDockableChanged(IDockable? dockable)
  {
    base.OnFocusedDockableChanged(dockable);
    if (dockable is EditorDocument document)
    {
      _owner.ActivateDocument(document);
      UpdateWindowTitle(document);
    }
  }

  public override bool OnWindowClosing(IDockWindow? window)
  {
    if (_applicationClosing || window is null || _approvedWindows.Remove(window))
      return base.OnWindowClosing(window);

    var documents = Descendants(window.Layout).OfType<EditorDocument>().ToArray();

    if (documents.Length == 0)
      return base.OnWindowClosing(window);

    if (!_owner.IsBusy && _closingWindows.Add(window))
      _ = CloseWindowAsync(window, documents);

    return false;
  }

  public void RemoveDocument(EditorDocument document) => base.CloseDockable(document);

  public void ShowExplorer()
  {
    // Always restore the tool on the left, even after it was floated or moved.
    if (_owner.SolutionExplorer.Owner is IRootDock root && root.HiddenDockables?.Contains(_owner.SolutionExplorer) == true)
      RestoreDockable(_owner.SolutionExplorer);

    RemoveDockable(_owner.SolutionExplorer, true);

    _explorerDock = CreateExplorerDock();
    _main.VisibleDockables ??= CreateList<IDockable>();

    InsertDockable(_main, _explorerDock, 0);
    InsertDockable(_main, new ProportionalDockSplitter(), 1);
    SetActiveDockable(_owner.SolutionExplorer);
  }

  public void UpdateWindowTitle(EditorDocument document)
  {
    if (FindRoot(document, _ => true)?.Window is { } window)
      window.Title = $"{document.DisplayTitle} — Zero IDE";
  }

  private static IEnumerable<IDockable> Descendants(IDockable? dockable)
  {
    if (dockable is null)
      yield break;

    yield return dockable;

    if (dockable is not IDock dock || dock.VisibleDockables is null)
      yield break;

    foreach (var child in dock.VisibleDockables)
      foreach (var descendant in Descendants(child))
        yield return descendant;
  }

  private async Task CloseWindowAsync(IDockWindow window, EditorDocument[] documents)
  {
    try
    {
      if (!await _owner.CloseDocumentsAsync(documents))
        return;

      _approvedWindows.Add(window);
      base.CloseWindow(window);
    }
    finally
    {
      _closingWindows.Remove(window);
      _approvedWindows.Remove(window);
    }
  }

  private ToolDock CreateExplorerDock() => new()
  {
    Id = "ExplorerDock",
    Alignment = Alignment.Left,
    Proportion = 0.23,
    ActiveDockable = _owner.SolutionExplorer,
    VisibleDockables = CreateList<IDockable>(_owner.SolutionExplorer),
  };

  private HostWindow CreateHostWindow()
  {
    var window = new HostWindow();

    // Native floating windows have a separate input route from MainWindow.
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.NewFile, Command = _owner.CmdNew });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.OpenFile, Command = _owner.CmdOpen });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.Save, Command = _owner.CmdSave });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.SaveAs, Command = _owner.CmdSaveAs });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.SaveAll, Command = _owner.CmdSaveAll });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.CloseDocument, Command = _owner.CmdCloseDocument });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.BuildAndRun, Command = _owner.CmdDebugContinue });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.StepOver, Command = _owner.CmdDebugStepOver });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.ToggleBreakpoint, Command = _owner.CmdToggleBreakpoint });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.StopDebugging, Command = _owner.CmdDebugStop });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.Build, Command = _owner.CmdBuild });
    window.KeyBindings.Add(new KeyBinding { Gesture = EditorKeyGestures.Rebuild, Command = _owner.CmdRebuild });

    return window;
  }
}
