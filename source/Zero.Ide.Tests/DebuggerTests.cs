// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
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
