// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.Core;
using Moq;
using Zero.Dbg;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class DebuggerTests
{
  [TestMethod]
  public void BreakpointsFollowEditsAndToggleAtTheSameLine()
  {
    var editor = new EditorViewModel();
    editor.Load("nop\nmov eax, 42\nret", "sample.zasm");
    editor.ToggleBreakpoint(2);

    Assert.IsTrue(editor.HasBreakpoint(2));

    editor.SetExecutableLines(new HashSet<int> { 2 });
    Assert.IsTrue(editor.IsBreakpointBound(2));

    editor.Document.Insert(0, "; added line\n");
    Assert.IsTrue(editor.HasBreakpoint(3));
    Assert.IsFalse(editor.HasBreakpoint(2));
    Assert.IsFalse(editor.IsBreakpointBound(3));

    editor.ToggleBreakpoint(3);
    Assert.IsEmpty(editor.Breakpoints);
  }

  [TestMethod]
  public void DebuggerCommandsAndRegistersFollowSessionState()
  {
    var session = new Mock<IDebugSession>();
    session.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);
    var dialogs = new Mock<IEditorDialogService>();
    var editor = new EditorViewModel();
    var shell = new MainWindowViewModel(editor, dialogs.Object, debugSession: session.Object);

    Assert.IsFalse(shell.CmdDebugStepOver.CanExecute());
    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Paused, "Breakpoint", Registers: [new("EAX", 42)]));

    Assert.IsTrue(shell.IsDebugging);
    Assert.IsTrue(editor.IsDebugReadOnly);
    Assert.IsFalse(shell.CmdBuild.CanExecute());
    Assert.IsTrue(shell.CmdDebugStepOver.CanExecute());
    Assert.AreEqual("0x0000002A", shell.Debugger.Registers[0].Hex);

    shell.CmdDebugStepOver.Execute();
    session.Verify(s => s.StepOver(), Times.Once);

    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Running, "Running"));

    Assert.IsTrue(shell.IsDebugRunning);
    Assert.AreEqual("Pause debugging", shell.DebugPlayLabel);
    Assert.IsEmpty(shell.Debugger.Registers);
    Assert.IsFalse(shell.CmdDebugStepOver.CanExecute());
    Assert.IsFalse(shell.CmdDebugContinue.CanExecute());

    shell.CmdDebugPlayPause.Execute();
    session.Verify(s => s.Pause(), Times.Once);

    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Paused, "Paused"));
    shell.CmdDebugContinue.Execute();
    session.Verify(s => s.Continue(), Times.Once);
    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Exited, "Exited", ExitCode: 0));

    Assert.IsFalse(shell.IsDebugging);
    Assert.IsFalse(editor.IsDebugReadOnly);
    Assert.IsFalse(shell.CmdDebugStop.CanExecute());
  }

  [TestMethod]
  public void DebuggerDockIsHiddenOutsideDebuggingAndRetainsMovedPlacement()
  {
    var session = new Mock<IDebugSession>();
    var shell = new MainWindowViewModel(new EditorViewModel(), new Mock<IEditorDialogService>().Object, debugSession: session.Object);

    Assert.Contains(shell.Debugger, shell.Layout.HiddenDockables);

    var originalDock = (IToolDock)shell.Debugger.OriginalOwner!;
    Assert.AreEqual(Alignment.Right, originalDock.Alignment);

    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Starting, "Starting"));

    Assert.AreSame(originalDock, shell.Debugger.Owner);
    Assert.DoesNotContain(shell.Debugger, shell.Layout.HiddenDockables);

    var explorerDock = (IDock)shell.SolutionExplorer.Owner!;
    shell.DockFactory.MoveDockable(originalDock, explorerDock, shell.Debugger, shell.SolutionExplorer);

    foreach (var state in new[] { DebugState.Paused, DebugState.Running, DebugState.Exited, DebugState.Starting, DebugState.Faulted, DebugState.Idle })
    {
      session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(state, state.ToString()));

      if (shell.IsDebugging)
      {
        Assert.AreSame(explorerDock, shell.Debugger.Owner);
        Assert.IsNotNull(explorerDock.VisibleDockables);
        Assert.Contains(shell.Debugger, explorerDock.VisibleDockables);
      }
      else
      {
        Assert.Contains(shell.Debugger, shell.Layout.HiddenDockables);
        Assert.IsNotNull(explorerDock.VisibleDockables);
        Assert.DoesNotContain(shell.Debugger, explorerDock.VisibleDockables);
        Assert.AreSame(explorerDock, shell.Debugger.OriginalOwner);
      }
    }

    Assert.ContainsSingle(dockable => ReferenceEquals(dockable, shell.Debugger), shell.Layout.HiddenDockables);
  }

  [TestMethod]
  public void DebuggerValueTreeRetainsNodesAndSupportsNestedScopes()
  {
    var model = new DebuggerViewModel();
    model.Update(new DebugSnapshot(DebugState.Paused, "Step", Registers: [new("EAX", 42), new("EBX", 7)]));
    var scope = model.Values.Single();
    var eax = scope.Children[0];

    Assert.AreEqual("Registers", scope.Name);
    Assert.AreEqual("EAX", eax.Name);
    Assert.AreEqual("0x0000002A", eax.Value);
    Assert.AreEqual("uint32", eax.Type);

    scope.IsExpanded = false;
    model.Update(new DebugSnapshot(DebugState.Paused, "Step", Registers: [new("EAX", 43)]));

    Assert.AreSame(eax, scope.Children.Single());
    Assert.AreEqual("0x0000002B", eax.Value);
    Assert.IsFalse(scope.IsExpanded);

    var locals = new Models.DebugValueNode("Locals");
    locals.Children.Add(new Models.DebugValueNode("message", "hello", "string"));
    model.Values.Add(locals);
    model.Update(new DebugSnapshot(DebugState.Running, "Running"));

    Assert.IsEmpty(scope.Children);
    Assert.AreSame(locals, model.Values[1]);
  }

  [TestMethod]
  public void DeletingBreakpointLineRemovesItsAnchor()
  {
    var editor = new EditorViewModel();
    editor.Load("nop\nmov eax, 42\nret", "sample.zasm");
    editor.ToggleBreakpoint(2);

    var line = editor.Document.GetLineByNumber(2);
    editor.Document.Remove(line.Offset, line.TotalLength);

    Assert.IsEmpty(editor.Breakpoints);
  }
}
