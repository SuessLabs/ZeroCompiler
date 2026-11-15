// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Moq;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class EditorTests
{
  [TestMethod]
  public void DocumentEditsAndUndoUpdateModifiedState()
  {
    var editor = new EditorViewModel();
    editor.Load("mov eax, 1", "sample.zasm");
    editor.Document.Insert(editor.Document.TextLength, "\nret");
    Assert.IsTrue(editor.IsModified);
    Assert.AreEqual("mov eax, 1\nret", editor.Text);
    editor.Document.UndoStack.Undo();
    Assert.IsFalse(editor.IsModified);
    Assert.AreEqual("mov eax, 1", editor.Text);
  }

  [TestMethod]
  public void LoadingAnotherFileReplacesDocumentAndClearsUndoHistory()
  {
    var editor = new EditorViewModel();
    editor.Document.Insert(0, "previous file");
    var previous = editor.Document;
    editor.Load("new file", "new.zasm");
    Assert.AreNotSame(previous, editor.Document);
    Assert.IsFalse(editor.Document.UndoStack.CanUndo);
    Assert.AreEqual("new file", editor.Text);
    previous.Insert(0, "old edit");
    Assert.IsFalse(editor.IsModified);
  }

  [TestMethod]
  public void RevertingTextClearsModifiedState()
  {
    var editor = new EditorViewModel();
    editor.Load("original", "sample.zasm");
    editor.Text = "changed";
    Assert.IsTrue(editor.IsModified);
    editor.Text = "original";
    Assert.IsFalse(editor.IsModified);
  }

  [TestMethod]
  public void NewDocumentResetsNameModifiedStateAndRevision()
  {
    var editor = new EditorViewModel();
    editor.Load("original", "sample.zasm");
    editor.Text = "changed";
    var revision = editor.Revision;
    editor.Load(string.Empty, "Untitled");
    Assert.AreEqual(string.Empty, editor.Text);
    Assert.AreEqual("Untitled", editor.FileName);
    Assert.IsFalse(editor.IsModified);
    Assert.IsGreaterThan(revision, editor.Revision);
  }

  [TestMethod]
  public void SavingSnapshotDoesNotMarkLaterEditsAsSaved()
  {
    var editor = new EditorViewModel { Text = "newer edits" };
    editor.MarkSaved("sample.zasm", "saved snapshot");
    Assert.IsTrue(editor.IsModified);
  }

  [TestMethod]
  public async Task CancelClosePreservesDocumentAsync()
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Cancel);
    var editor = new EditorViewModel { Text = "unsaved" };
    var shell = new MainWindowViewModel(editor, dialogs.Object);
    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.AreEqual("unsaved", editor.Text);
    Assert.IsTrue(editor.IsModified);
    Assert.IsFalse(shell.IsBusy);
  }

  [TestMethod]
  public async Task CancelSavePickerPreventsClosingAsync()
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Save);
    dialogs.Setup(d => d.SaveFileAsync("Untitled")).ReturnsAsync((IStorageFile?)null);
    var editor = new EditorViewModel { Text = "unsaved" };
    var shell = new MainWindowViewModel(editor, dialogs.Object);
    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.IsTrue(editor.IsModified);
  }

  [TestMethod]
  public async Task DiscardAllowsClosingWithoutSavingAsync()
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Discard);
    var shell = new MainWindowViewModel(new EditorViewModel { Text = "unsaved" }, dialogs.Object);
    Assert.IsTrue(await shell.CanCloseAsync());
    dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>()), Times.Never);
  }

  [TestMethod]
  public async Task SaveTruncatesOldContentsAndMarksDocumentSavedAsync()
  {
    var stream = new MemoryStream();
    stream.Write(Encoding.UTF8.GetBytes("previous contents much longer than new contents"));
    stream.Position = 0;
    var file = new Mock<IStorageFile>();
    file.SetupGet(f => f.Name).Returns("sample.zasm");
    file.Setup(f => f.OpenWriteAsync()).ReturnsAsync(stream);
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Save);
    dialogs.Setup(d => d.SaveFileAsync("Untitled")).ReturnsAsync(file.Object);
    var editor = new EditorViewModel { Text = "short" };
    var shell = new MainWindowViewModel(editor, dialogs.Object);
    Assert.IsTrue(await shell.CanCloseAsync());
    Assert.AreEqual("short", Encoding.UTF8.GetString(stream.ToArray()));
    Assert.IsFalse(editor.IsModified);
    Assert.AreEqual("sample.zasm — Zero IDE", shell.Title);
  }

  [TestMethod]
  public async Task FailedSavePreservesModifiedStateAndPreventsClosingAsync()
  {
    var file = new Mock<IStorageFile>();
    file.Setup(f => f.OpenWriteAsync()).ThrowsAsync(new IOException("Disk full"));
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled")).ReturnsAsync(SaveChangesChoice.Save);
    dialogs.Setup(d => d.SaveFileAsync("Untitled")).ReturnsAsync(file.Object);
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    var editor = new EditorViewModel { Text = "unsaved" };
    var shell = new MainWindowViewModel(editor, dialogs.Object);
    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.IsTrue(editor.IsModified);
    Assert.IsFalse(shell.IsBusy);
    dialogs.Verify(d => d.ShowMessageAsync("Unable to close document", "Disk full"), Times.Once);
  }
}
