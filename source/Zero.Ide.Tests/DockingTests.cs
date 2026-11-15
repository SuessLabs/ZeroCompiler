// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Dock.Model.Controls;
using Dock.Model.Core;
using Moq;
using Prism.Commands;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class DockingTests
{
  [TestMethod]
  public async Task ApplicationCloseApprovalDoesNotRepromptFloatingWindowsAsync()
  {
    var dialogs = CreateDialogs();
    dialogs.Setup(d => d.ConfirmSaveAsync(It.IsAny<string>())).ReturnsAsync(SaveChangesChoice.Discard);

    var shell = CreateShell(dialogs);
    shell.Editor.Text = "unsaved";
    Assert.IsTrue(await shell.CanCloseAsync());

    var window = new Mock<IDockWindow>();
    window.SetupGet(item => item.Layout).Returns(shell.Layout);
    window.Setup(item => item.OnClose()).Returns(true);
    Assert.IsTrue(shell.DockFactory.OnWindowClosing(window.Object));

    dialogs.Verify(d => d.ConfirmSaveAsync("Untitled"), Times.Once);
  }

  [TestMethod]
  public async Task BuildUsesCapturedActiveTabEvenIfSelectionChangesWhileSavingAsync()
  {
    var first = new TestFile("first.zasm", "first");
    var second = new TestFile("second.zasm", "second");

    var dialogs = CreateDialogs();
    dialogs.Setup(d => d.OpenFilesAsync()).ReturnsAsync([first.File.Object, second.File.Object]);

    var settings = new Mock<ISettingsService>();
    settings.SetupGet(s => s.Current).Returns(IdeSettings.Default with { CompilerPath = "compiler" });

    var compiler = new Mock<ICompilerService>();
    compiler
      .Setup(c => c.BuildAsync("compiler", first.File.Object.Path.LocalPath, "Debug", false))
      .ReturnsAsync(new CompilerResult(0, "Compiled first", "first.exe"));

    var shell = new MainWindowViewModel(new EditorViewModel(settings.Object), dialogs.Object, settings.Object, compiler.Object);
    await RunAsync(shell, shell.CmdOpen);

    var firstTab = shell.Documents.Single(document => document.File == first.File.Object);
    var secondTab = shell.Documents.Single(document => document.File == second.File.Object);

    shell.DockFactory.Activate(firstTab);
    firstTab.Editor.Text = "unsaved first";

    first.File
      .Setup(f => f.OpenWriteAsync())
      .Callback(() => shell.DockFactory.Activate(secondTab))
      .ReturnsAsync(() => first.NewWriteStream());

    await RunAsync(shell, shell.CmdBuild);
    Assert.AreSame(secondTab, shell.ActiveDocument);

    compiler.Verify(c => c.BuildAsync("compiler", first.File.Object.Path.LocalPath, "Debug", false), Times.Once);
    Assert.AreEqual("unsaved first", first.WrittenText);
    Assert.AreEqual("second", secondTab.Editor.Text);
    Assert.IsFalse(firstTab.Editor.IsModified);
  }

  [TestMethod]
  public async Task CancelledSaveOrWriteFailureKeepsTabOpenAsync()
  {
    var dialogs = CreateDialogs();

    dialogs
      .Setup(d => d.ConfirmSaveAsync(It.IsAny<string>()))
      .ReturnsAsync(SaveChangesChoice.Save);

    dialogs
      .Setup(d => d.SaveFileAsync(It.IsAny<string>()))
      .ReturnsAsync((IStorageFile?)null);

    var shell = CreateShell(dialogs);
    shell.Editor.Text = "dirty";

    var document = shell.ActiveDocument!;
    Assert.IsFalse(await shell.CloseDocumentAsync(document));

    var file = new TestFile("failed.zasm");
    file.File
      .Setup(f => f.OpenWriteAsync())
      .ThrowsAsync(new IOException("Disk full"));

    dialogs
      .Setup(d => d.SaveFileAsync(It.IsAny<string>()))
      .ReturnsAsync(file.File.Object);

    Assert.IsFalse(await shell.CloseDocumentAsync(document));
    Assert.Contains(document, shell.Documents);
    Assert.IsTrue(document.Editor.IsModified);
    Assert.IsFalse(shell.IsBusy);
  }

  [TestMethod]
  public async Task ClosedEditorsUnsubscribeFromSharedSettingsAsync()
  {
    var settings = new Mock<ISettingsService>();

    settings.SetupGet(service => service.Current)
            .Returns(IdeSettings.Default);

    var editor = new EditorViewModel(settings.Object);
    var shell = new MainWindowViewModel(editor, CreateDialogs().Object, settings.Object);
    var notifications = 0;

    editor.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(editor.Settings))
        notifications++;
    };

    Assert.IsTrue(await shell.CloseDocumentAsync(shell.ActiveDocument!));

    settings.Raise(service => service.SettingsChanged += null, EventArgs.Empty);
    Assert.AreEqual(0, notifications);
  }

  [TestMethod]
  [DataRow(SaveChangesChoice.Cancel, false)]
  [DataRow(SaveChangesChoice.Discard, true)]
  [DataRow(SaveChangesChoice.Save, true)]
  public async Task ClosingDirtyTabHonorsAllPromptChoicesAsync(SaveChangesChoice choice, bool expected)
  {
    var file = new TestFile("saved.zasm");
    var dialogs = CreateDialogs();

    dialogs.Setup(d => d.ConfirmSaveAsync("Untitled"))
           .ReturnsAsync(choice);

    dialogs.Setup(d => d.SaveFileAsync("Untitled"))
           .ReturnsAsync(file.File.Object);

    var shell = CreateShell(dialogs);
    shell.Editor.Text = "dirty text";
    var document = shell.ActiveDocument!;

    Assert.AreEqual(expected, await shell.CloseDocumentAsync(document));
    Assert.AreEqual(!expected, shell.Documents.Contains(document));

    if (choice == SaveChangesChoice.Save)
      Assert.AreEqual("dirty text", file.WrittenText);

    if (expected)
    {
      Assert.IsFalse(shell.HasActiveDocument);
      Assert.IsFalse(shell.CmdSave.CanExecute());
      Assert.AreEqual("Zero IDE", shell.Title);

      await RunAsync(shell, shell.CmdNew);
      Assert.IsTrue(shell.HasActiveDocument);
    }
  }

  [TestMethod]
  public async Task DockCloseAndExplorerRestoreUseSharedLifecycleAsync()
  {
    var dialogs = CreateDialogs();
    var shell = CreateShell(dialogs);
    var first = shell.ActiveDocument!;

    await RunAsync(shell, shell.CmdNew);

    shell.DockFactory.CloseDockable(first);
    Assert.HasCount(1, shell.Documents);

    var remaining = shell.ActiveDocument;

    for (var index = 0; index < 3; index++)
    {
      shell.DockFactory.CloseDockable(shell.SolutionExplorer);
      await RunAsync(shell, shell.CmdShowSolutionExplorer);

      Assert.AreSame(remaining, shell.ActiveDocument);
      Assert.ContainsSingle(dockable => ReferenceEquals(dockable, shell.SolutionExplorer), Descendants(shell.Layout));
      Assert.AreNotEqual(true, shell.Layout.HiddenDockables?.Contains(shell.SolutionExplorer));
      Assert.AreEqual(Alignment.Left, ((IToolDock)shell.SolutionExplorer.Owner!).Alignment);
    }
  }

  [TestMethod]
  public async Task GroupCloseCancellationAndApplicationExitCheckEveryDirtyDocumentAsync()
  {
    var dialogs = CreateDialogs();
    var shell = CreateShell(dialogs);

    shell.Editor.Text = "first";
    await RunAsync(shell, shell.CmdNew);
    shell.Editor.Text = "second";

    var documents = shell.Documents.ToArray();
    dialogs.Setup(d => d.ConfirmSaveAsync(documents[0].Editor.FileName))
           .ReturnsAsync(SaveChangesChoice.Discard);

    dialogs.Setup(d => d.ConfirmSaveAsync(documents[1].Editor.FileName))
           .ReturnsAsync(SaveChangesChoice.Cancel);

    Assert.IsFalse(await shell.CloseDocumentsAsync(documents));
    Assert.HasCount(2, shell.Documents);
    Assert.IsFalse(await shell.CanCloseAsync());
    Assert.HasCount(2, shell.Documents);

    dialogs.Verify(d => d.ConfirmSaveAsync(documents[0].Editor.FileName), Times.Exactly(2));
    dialogs.Verify(d => d.ConfirmSaveAsync(documents[1].Editor.FileName), Times.Exactly(2));
  }

  [TestMethod]
  public async Task NewDocumentKeepsDirtyExistingTabAndSaveAllSavesEachBufferAsync()
  {
    var dialogs = CreateDialogs();
    var first = new TestFile("first.zasm");
    var second = new TestFile("second.zasm");

    dialogs.SetupSequence(d => d.SaveFileAsync(It.IsAny<string>()))
           .ReturnsAsync(first.File.Object).ReturnsAsync(second.File.Object);

    var shell = CreateShell(dialogs);
    shell.Editor.Text = "first buffer";
    var initial = shell.ActiveDocument!;

    await RunAsync(shell, shell.CmdNew);

    Assert.HasCount(2, shell.Documents);
    Assert.IsTrue(initial.Editor.IsModified);

    shell.Editor.Text = "second buffer";
    var active = shell.ActiveDocument;
    await RunAsync(shell, shell.CmdSaveAll);

    Assert.AreSame(active, shell.ActiveDocument);
    Assert.AreEqual("first buffer", first.WrittenText);
    Assert.AreEqual("second buffer", second.WrittenText);
    Assert.IsTrue(shell.Documents.All(document => !document.Editor.IsModified));

    dialogs.Verify(d => d.ConfirmSaveAsync(It.IsAny<string>()), Times.Never);
  }

  [TestMethod]
  public async Task OpeningMultipleFilesCreatesIndependentTabsAndDeduplicatesPathsAsync()
  {
    var first = new TestFile("first.zasm", "mov eax, 1");
    var second = new TestFile("second.ZSRC", "source text");
    var dialogs = CreateDialogs();

    dialogs.Setup(d => d.OpenFilesAsync())
           .ReturnsAsync([first.File.Object, second.File.Object]);

    var shell = CreateShell(dialogs);
    await RunAsync(shell, shell.CmdOpen);

    Assert.HasCount(3, shell.Documents);

    var firstTab = shell.Documents.Single(document => document.File == first.File.Object);
    var secondTab = shell.Documents.Single(document => document.File == second.File.Object);

    Assert.AreNotSame(firstTab.Editor.Document, secondTab.Editor.Document);
    Assert.AreSame(secondTab, shell.ActiveDocument);

    shell.DockFactory.Activate(firstTab);
    firstTab.Editor.Document.Insert(0, "changed ");

    Assert.AreEqual("first.zasm*", firstTab.DisplayTitle);
    Assert.AreEqual("first.zasm", firstTab.Title); // Dock adds its own visual '*' for IsModified.
    Assert.IsTrue(firstTab.IsModified);
    Assert.AreEqual("second.ZSRC", secondTab.Title);

    shell.DockFactory.Activate(secondTab);
    Assert.AreEqual("source text", shell.Editor.Text);

    await RunAsync(shell, shell.CmdOpen);
    Assert.HasCount(3, shell.Documents);
    Assert.AreSame(secondTab, shell.ActiveDocument);

    firstTab.Editor.Document.UndoStack.Undo();
    Assert.AreEqual("first.zasm", firstTab.Title);
    Assert.AreEqual("mov eax, 1", firstTab.Editor.Text);
  }

  [TestMethod]
  public async Task SaveAllCancellationKeepsRemainingDocumentsDirtyAsync()
  {
    var dialogs = CreateDialogs();
    var file = new TestFile("first.zasm");

    dialogs.SetupSequence(d => d.SaveFileAsync(It.IsAny<string>()))
           .ReturnsAsync(file.File.Object)
           .ReturnsAsync((IStorageFile?)null);

    var shell = CreateShell(dialogs);

    shell.Editor.Text = "first";
    await RunAsync(shell, shell.CmdNew);

    shell.Editor.Text = "second";
    await RunAsync(shell, shell.CmdNew);

    shell.Editor.Text = "third";
    var documents = shell.Documents.ToArray();
    await RunAsync(shell, shell.CmdSaveAll);

    Assert.IsFalse(documents[0].Editor.IsModified);
    Assert.IsTrue(documents[1].Editor.IsModified);
    Assert.IsTrue(documents[2].Editor.IsModified);
    Assert.AreSame(documents[2], shell.ActiveDocument);

    dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>()), Times.Exactly(2));
  }

  [TestMethod]
  public async Task SaveAllSkipsUnchangedTabsIncludingInitialPlaceholderAsync()
  {
    var dialogs = CreateDialogs();
    var shell = CreateShell(dialogs);
    await RunAsync(shell, shell.CmdSaveAll);
    dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>()), Times.Never);
  }

  [TestMethod]
  public async Task SaveAsCannotOverwriteAnotherOpenTabAsync()
  {
    var file = new TestFile("first.zasm", "first on disk");
    var dialogs = CreateDialogs();

    dialogs.Setup(d => d.OpenFilesAsync())
           .ReturnsAsync([file.File.Object]);

    dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>()))
           .ReturnsAsync(file.File.Object);

    var shell = CreateShell(dialogs);
    await RunAsync(shell, shell.CmdOpen);
    await RunAsync(shell, shell.CmdNew);

    shell.Editor.Text = "different content";
    await RunAsync(shell, shell.CmdSaveAs);

    Assert.IsEmpty(file.Writes);
    Assert.IsTrue(shell.Editor.IsModified);
    dialogs.Verify(d => d.ShowMessageAsync("Operation failed", It.Is<string>(message => message.Contains("already open"))), Times.Once);
  }

  [TestMethod]
  public async Task SaveWritesOnlyActiveDocumentAsync()
  {
    var first = new TestFile("first.zasm", "original first");
    var second = new TestFile("second.zasm", "original second");
    var dialogs = CreateDialogs();

    dialogs.Setup(d => d.OpenFilesAsync())
           .ReturnsAsync([first.File.Object, second.File.Object]);

    var shell = CreateShell(dialogs);

    await RunAsync(shell, shell.CmdOpen);

    var firstTab = shell.Documents.Single(document => document.File == first.File.Object);
    var secondTab = shell.Documents.Single(document => document.File == second.File.Object);

    firstTab.Editor.Text = "edited first";
    secondTab.Editor.Text = "edited second";
    shell.DockFactory.Activate(firstTab);
    await RunAsync(shell, shell.CmdSave);

    Assert.AreEqual("edited first", first.WrittenText);
    Assert.IsEmpty(second.Writes);
    Assert.IsFalse(firstTab.Editor.IsModified);
    Assert.IsTrue(secondTab.Editor.IsModified);
  }

  [TestMethod]
  public async Task WorkspaceEnumeratesFoldersLazilyAndHandlesMissingDirectoriesAsync()
  {
    var root = Path.Combine(Path.GetTempPath(), "Zero.Ide.Workspace.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "nested"));

    File.WriteAllText(Path.Combine(root, "main.ZASM"), "ret");
    File.WriteAllText(Path.Combine(root, "next.zsrc"), "source");
    File.WriteAllText(Path.Combine(root, "nested", "child.zasm"), "ret");

    try
    {
      var node = new WorkspaceNode(root, true);
      Assert.IsTrue(node.Children.Single().IsPlaceholder);

      await node.LoadChildrenAsync();
      Assert.HasCount(3, node.Children);
      Assert.IsTrue(node.Children[0].IsDirectory);

      var nested = node.Children.Single(child => child.Name == "nested");
      Assert.IsTrue(nested.Children.Single().IsPlaceholder);

      await nested.LoadChildrenAsync();
      Assert.AreEqual("child.zasm", nested.Children.Single().Name);

      var missing = new WorkspaceNode(Path.Combine(root, "missing"), true);
      await missing.LoadChildrenAsync();
      Assert.IsEmpty(missing.Children);
      Assert.IsFalse(string.IsNullOrEmpty(missing.ErrorMessage));
    }
    finally
    {
      var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Zero.Ide.Workspace.Tests")) + Path.DirectorySeparatorChar;
      Assert.IsTrue(Path.GetFullPath(root).StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase));
      Directory.Delete(root, true);
    }
  }

  private static Mock<IEditorDialogService> CreateDialogs()
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    return dialogs;
  }

  private static MainWindowViewModel CreateShell(Mock<IEditorDialogService> dialogs) => new(new EditorViewModel(), dialogs.Object);

  private static IEnumerable<IDockable> Descendants(IDockable dockable)
  {
    yield return dockable;

    if (dockable is not IDock dock || dock.VisibleDockables is null)
      yield break;

    foreach (var child in dock.VisibleDockables)
      foreach (var descendant in Descendants(child))
        yield return descendant;
  }

  private static async Task RunAsync(MainWindowViewModel shell, DelegateCommand command)
  {
    Assert.IsTrue(command.CanExecute());
    var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    PropertyChangedEventHandler changed = (_, e) =>
    {
      if (e.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy)
        complete.TrySetResult();
    };

    shell.PropertyChanged += changed;
    try
    {
      command.Execute();
      await complete.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    finally
    {
      shell.PropertyChanged -= changed;
    }
  }

  private sealed class TestFile
  {
    public TestFile(string name, string text = "")
    {
      File.SetupGet(file => file.Name).Returns(name);
      File.SetupGet(file => file.Path).Returns(new Uri(Path.Combine(Path.GetTempPath(), "Zero.Ide.Dock.Tests", name)));
      File.Setup(file => file.OpenReadAsync()).ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(text)));
      File.Setup(file => file.OpenWriteAsync()).ReturnsAsync(() => NewWriteStream());
    }

    public Mock<IStorageFile> File { get; } = new();

    public List<MemoryStream> Writes { get; } = [];

    public string WrittenText => Encoding.UTF8.GetString(Writes.Last().ToArray());

    public MemoryStream NewWriteStream()
    {
      var stream = new MemoryStream();
      Writes.Add(stream);
      return stream;
    }
  }
}
