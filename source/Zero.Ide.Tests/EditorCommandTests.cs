// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Moq;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class EditorCommandTests
{
  [TestMethod]
  public void SearchCommandRequiresQueryAndAttachedEnabledEditor()
  {
    var editor = new EditorViewModel();
    editor.SearchText = "#TARGET";
    Assert.IsFalse(editor.CmdFindNext.CanExecute());
    var actions = new Mock<IEditorActions>();
    actions.SetupGet(a => a.CanSearch).Returns(true);
    editor.AttachEditorActions(actions.Object);
    Assert.IsTrue(editor.CmdFindNext.CanExecute());
    editor.CmdFindNext.Execute();
    actions.Verify(a => a.FindNext("#TARGET"), Times.Once);
    var notifications = 0;
    editor.CmdFindNext.CanExecuteChanged += (_, _) => notifications++;
    editor.SearchText = string.Empty;
    Assert.AreEqual(1, notifications);
    Assert.IsFalse(editor.CmdFindNext.CanExecute());
    editor.SearchText = "mov";
    actions.SetupGet(a => a.CanSearch).Returns(false);
    actions.Raise(a => a.StateChanged += null, EventArgs.Empty);
    Assert.IsFalse(editor.CmdFindNext.CanExecute());
    editor.CmdFindNext.Execute();
    actions.Verify(a => a.FindNext("mov"), Times.Never);
  }

  [TestMethod]
  public void ToolbarConfigurationAndBuildStatusRepresentBuildState()
  {
    var shell = new MainWindowViewModel(new EditorViewModel(), Mock.Of<IEditorDialogService>());
    Assert.AreEqual("Debug", shell.BuildConfiguration);
    CollectionAssert.AreEqual(new[] { "Debug", "Release" }, new System.Collections.Generic.List<string>(shell.BuildConfigurations));
    shell.BuildConfiguration = "Release";
    Assert.AreEqual("Release", shell.BuildConfiguration);
    Assert.AreEqual("Zero IDE", shell.BuildStatus);
    var notifications = 0;
    shell.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(shell.BuildStatus)) notifications++; };
    shell.IsBuilding = true;
    Assert.AreEqual("Building...", shell.BuildStatus);
    shell.IsBuilding = false;
    Assert.AreEqual("Zero IDE", shell.BuildStatus);
    Assert.AreEqual(2, notifications);
    Assert.IsFalse(shell.CmdRebuild.CanExecute());
  }

  [TestMethod]
  public void CommandsAreDisabledWithoutAnAttachedEditor()
  {
    var editor = new EditorViewModel();
    foreach (var command in new[] { editor.CmdUndo, editor.CmdRedo, editor.CmdCut, editor.CmdCopy, editor.CmdPaste })
    {
      Assert.IsFalse(command.CanExecute());
      command.Execute(); // Programmatic calls must also be safe while detached.
    }
  }

  [TestMethod]
  public void CommandsTrackAvailabilityAndDelegateToTheAttachedEditor()
  {
    var actions = new Mock<IEditorActions>();
    var editor = new EditorViewModel();
    editor.AttachEditorActions(actions.Object);
    var commands = new[] { editor.CmdUndo, editor.CmdRedo, editor.CmdCut, editor.CmdCopy, editor.CmdPaste };
    var notifications = new int[commands.Length];
    for (var index = 0; index < commands.Length; index++)
    {
      var commandIndex = index;
      commands[index].CanExecuteChanged += (_, _) => notifications[commandIndex]++;
      Assert.IsFalse(commands[index].CanExecute());
      commands[index].Execute();
    }

    actions.Verify(a => a.Undo(), Times.Never);
    actions.Verify(a => a.Redo(), Times.Never);
    actions.Verify(a => a.Cut(), Times.Never);
    actions.Verify(a => a.Copy(), Times.Never);
    actions.Verify(a => a.Paste(), Times.Never);

    actions.SetupGet(a => a.CanUndo).Returns(true);
    actions.SetupGet(a => a.CanRedo).Returns(true);
    actions.SetupGet(a => a.CanCut).Returns(true);
    actions.SetupGet(a => a.CanCopy).Returns(true);
    actions.SetupGet(a => a.CanPaste).Returns(true);
    actions.Raise(a => a.StateChanged += null, EventArgs.Empty);
    for (var index = 0; index < commands.Length; index++)
    {
      Assert.AreEqual(1, notifications[index]);
      Assert.IsTrue(commands[index].CanExecute());
      commands[index].Execute();
    }

    actions.Verify(a => a.Undo(), Times.Once);
    actions.Verify(a => a.Redo(), Times.Once);
    actions.Verify(a => a.Cut(), Times.Once);
    actions.Verify(a => a.Copy(), Times.Once);
    actions.Verify(a => a.Paste(), Times.Once);

    // Selection and read-only changes can disable operations independently.
    actions.SetupGet(a => a.CanCut).Returns(false);
    actions.SetupGet(a => a.CanPaste).Returns(false);
    actions.Raise(a => a.StateChanged += null, EventArgs.Empty);
    Assert.IsFalse(editor.CmdCut.CanExecute());
    Assert.IsFalse(editor.CmdPaste.CanExecute());
    Assert.IsTrue(editor.CmdCopy.CanExecute());
  }

  [TestMethod]
  public void ReplacingOrDetachingEditorRemovesOldSubscriptions()
  {
    var first = new Mock<IEditorActions>();
    var second = new Mock<IEditorActions>();
    first.SetupGet(a => a.CanCopy).Returns(true);
    second.SetupGet(a => a.CanCopy).Returns(true);

    var editor = new EditorViewModel();
    editor.AttachEditorActions(first.Object);
    editor.AttachEditorActions(second.Object);
    editor.AttachEditorActions(second.Object);

    var notifications = 0;
    editor.CmdCopy.CanExecuteChanged += (_, _) => notifications++;
    first.Raise(a => a.StateChanged += null, EventArgs.Empty);
    Assert.AreEqual(0, notifications);

    editor.DetachEditorActions(first.Object);
    Assert.IsTrue(editor.CmdCopy.CanExecute());

    second.Raise(a => a.StateChanged += null, EventArgs.Empty);
    Assert.AreEqual(1, notifications);

    editor.CmdCopy.Execute();
    first.Verify(a => a.Copy(), Times.Never);
    second.Verify(a => a.Copy(), Times.Once);

    editor.DetachEditorActions(second.Object);
    Assert.AreEqual(2, notifications);

    second.Raise(a => a.StateChanged += null, EventArgs.Empty);
    Assert.AreEqual(2, notifications);
    Assert.IsFalse(editor.CmdCopy.CanExecute());

    editor.CmdCopy.Execute();
    second.Verify(a => a.Copy(), Times.Once);
  }
}
