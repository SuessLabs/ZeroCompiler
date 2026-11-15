// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Moq;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class CodeCleanupTests
{
  [TestMethod]
  [DataRow("  mov eax, 1 \t\r\n\tret\t\n \t\rfinal  ", "  mov eax, 1\r\n\tret\n\rfinal")]
  [DataRow(" \t\r\n  \n", "\r\n\n")]
  [DataRow("", "")]
  public void CleanupPreservesIndentationAndLineEndingsAndIsOneUndoStep(string input, string expected)
  {
    var editor = new EditorViewModel();
    editor.Load(input, "sample.zasm");
    var document = editor.Document;
    editor.TrimTrailingWhitespace();
    Assert.AreSame(document, editor.Document);
    Assert.AreEqual(expected, editor.Text);

    if (input != expected)
    {
      document.UndoStack.Undo();
      Assert.AreEqual(input, editor.Text);
      Assert.IsFalse(document.UndoStack.CanUndo);

      document.UndoStack.Redo();
      Assert.AreEqual(expected, editor.Text);
    }
    else
    {
      Assert.IsFalse(document.UndoStack.CanUndo);
    }
  }

  [TestMethod]
  [DataRow(true, false)]
  [DataRow(true, true)]
  [DataRow(false, false)]
  public async Task SaveCommandsRespectCleanupPreferenceAsync(bool enabled, bool saveAs)
  {
    const string original = "  mov eax, 1  \r\n\tret\t";
    var expected = enabled ? "  mov eax, 1\r\n\tret" : original;
    var editor = CreateEditor(enabled);
    editor.Load(original, "sample.zasm");

    var stream = new MemoryStream();
    var file = new Mock<IStorageFile>();

    file.SetupGet(f => f.Name).Returns("sample.zasm");
    file.Setup(f => f.OpenWriteAsync()).ReturnsAsync(stream);

    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.SaveFileAsync("sample.zasm")).ReturnsAsync(file.Object);

    var shell = new MainWindowViewModel(editor, dialogs.Object);
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    shell.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy)
        completed.TrySetResult();
    };

    (saveAs ? shell.CmdSaveAs : shell.CmdSave).Execute();
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(expected, Encoding.UTF8.GetString(stream.ToArray()));
    Assert.AreEqual(expected, editor.Text);
    Assert.IsFalse(editor.IsModified);

    if (enabled)
    {
      editor.Document.UndoStack.Undo();
      Assert.AreEqual(original, editor.Text);
      Assert.IsTrue(editor.IsModified);
    }
  }

  [TestMethod]
  public async Task CancelledSaveDoesNotCleanDocumentAsync()
  {
    var editor = CreateEditor(true);
    editor.Text = "ret \t";

    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Save);
    dialogs.Setup(d => d.SaveFileAsync("Untitled")).ReturnsAsync((IStorageFile?)null);

    var shell = new MainWindowViewModel(editor, dialogs.Object);

    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.AreEqual("ret \t", editor.Text);
  }

  [TestMethod]
  public async Task FailedSaveKeepsCleanedDocumentDirtyAndUndoableAsync()
  {
    var editor = CreateEditor(true);
    editor.Text = "ret \t";

    var file = new Mock<IStorageFile>();
    file.Setup(f => f.OpenWriteAsync()).ThrowsAsync(new IOException("Disk full"));

    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Save);
    dialogs.Setup(d => d.SaveFileAsync("Untitled")).ReturnsAsync(file.Object);
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

    var shell = new MainWindowViewModel(editor, dialogs.Object);

    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.AreEqual("ret", editor.Text);
    Assert.IsTrue(editor.IsModified);

    editor.Document.UndoStack.Undo();
    Assert.AreEqual("ret \t", editor.Text);
  }

  private static EditorViewModel CreateEditor(bool cleanupEnabled)
  {
    var settings = new Mock<ISettingsService>();
    settings.SetupGet(s => s.Current)
            .Returns(IdeSettings.Default with { TrimTrailingWhitespaceOnSave = cleanupEnabled });

    return new EditorViewModel(settings.Object);
  }
}
