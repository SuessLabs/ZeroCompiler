// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
      _ = _owner.CloseDocumentsAsync(DocumentsInRange(visible, 0, visible.Count));
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

  public void CloseFloatingWindows()
  {
    if (!_applicationClosing)
      return;

    if (_owner.Layout.Windows is { } windows)
      foreach (var window in new List<IDockWindow>(windows)) window.Exit();
  }

  public override void CloseLeftDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync(DocumentsInRange(visible, 0, visible.IndexOf(dockable)));
  }

  public override void CloseOtherDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync(DocumentsInRange(visible, 0, visible.Count, dockable));
  }

  public override void CloseRightDockables(IDockable dockable)
  {
    if (dockable.Owner is IDock dock && dock.VisibleDockables is { } visible)
      _ = _owner.CloseDocumentsAsync(DocumentsInRange(visible, visible.IndexOf(dockable) + 1, visible.Count));
  }

  public override IRootDock CreateLayout()
  {
    var documents = new List<IDockable>(_owner.Documents.Count);
    foreach (var document in _owner.Documents) documents.Add(document);
    _documents = new DocumentDock
    {
      Id = "Documents",
      IsCollapsable = false,
      CanCloseLastDockable = true,
      CanCreateDocument = false,
      ActiveDockable = _owner.ActiveDocument,
      VisibleDockables = CreateList<IDockable>(documents.ToArray()),
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
      Alignment = Alignment.Right,
      Proportion = 0.34,
      ActiveDockable = _owner.Debugger,
      VisibleDockables = CreateList<IDockable>(_owner.Debugger),
    };

    _main.VisibleDockables.Add(new ProportionalDockSplitter());
    _main.VisibleDockables.Add(debuggerDock);
    root.VisibleDockables = CreateList<IDockable>(_main);
    root.DefaultDockable = _main;
    root.ActiveDockable = _main;
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

    var documents = new List<EditorDocument>();
    foreach (var item in Descendants(window.Layout))
      if (item is EditorDocument document)
        documents.Add(document);

    if (documents.Count == 0)
      return base.OnWindowClosing(window);

    if (!_owner.IsBusy && _closingWindows.Add(window))
      _ = CloseWindowAsync(window, documents.ToArray());

    return false;
  }

  public void RemoveDocument(EditorDocument document) => base.CloseDockable(document);

  public void SetDebuggerVisible(bool visible)
  {
    var debugger = _owner.Debugger;
    var root = FindRoot(debugger, _ => true);
    var hidden = root?.HiddenDockables?.Contains(debugger) == true;

    if (visible && hidden)
    {
      RestoreDockable(debugger);
      if (debugger.Owner is IDock dock)
        dock.ActiveDockable = debugger;

      if (root?.Window?.Host is HostWindow host)
        host.Show();
    }
    else if (!visible && !hidden)
    {
      var owner = debugger.Owner as IDock;
      HideDockable(debugger);

      if (owner is not null && ReferenceEquals(owner.ActiveDockable, debugger))
      {
        owner.ActiveDockable = FirstContent(owner.VisibleDockables);
        if (owner.ActiveDockable is { } active)
          SetActiveDockable(active);
      }

      if (root is not null && ReferenceEquals(root.FocusedDockable, debugger) && owner is not null)
        SetFocusedDockable(owner, owner.ActiveDockable);

      // Keep the layout and floating bounds so the next session restores the user's placement.
      if (root?.Window?.Host is HostWindow host &&
          !HasHostContent(root))
      {
        host.Hide();
      }
    }
  }

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

  private static List<EditorDocument> DocumentsInRange(IList<IDockable> items, int start, int end, IDockable? exclude = null)
  {
    var documents = new List<EditorDocument>();
    for (int i = start; i < end; i++)
      if (items[i] is EditorDocument document && !ReferenceEquals(document, exclude))
        documents.Add(document);

    return documents;
  }

  private static IDockable? FirstContent(IList<IDockable>? items)
  {
    if (items is not null)
      foreach (var item in items)
        if (item is not ISplitter)
          return item;

    return null;
  }

  private static bool HasHostContent(IDockable root)
  {
    foreach (var item in Descendants(root))
      if (item is ITool or IDocument)
        return true;

    return false;
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
