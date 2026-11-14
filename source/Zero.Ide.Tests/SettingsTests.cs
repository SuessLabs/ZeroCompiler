// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Moq;
using Prism.Container.DryIoc;
using Prism.Dialogs;
using Prism.Ioc;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class SettingsTests
{
  private string _directory = string.Empty;
  private string _filePath = string.Empty;

  [TestInitialize]
  public void Initialize()
  {
    _directory = Path.Combine(Path.GetTempPath(), "Zero.Ide.Settings.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_directory);
    _filePath = Path.Combine(_directory, "settings.ini");
  }

  [TestCleanup]
  public void Cleanup()
  {
    var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Zero.Ide.Settings.Tests")) + Path.DirectorySeparatorChar;
    var directory = Path.GetFullPath(_directory);
    Assert.IsTrue(directory.StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase));
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
  }

  [TestMethod]
  public void MissingFileUsesDefaultsWithoutCreatingFile()
  {
    var service = new IniSettingsService(_filePath);
    Assert.AreEqual(IdeSettings.Default, service.Current);
    Assert.IsFalse(File.Exists(_filePath));
  }

  [TestMethod]
  public void ContainerInjectsSharedSettingsIntoEditor()
  {
    var service = new IniSettingsService(_filePath);
    var container = new DryIocContainerExtension();
    container.RegisterInstance<ISettingsService>(service);
    container.RegisterSingleton<EditorViewModel>();
    var editor = container.Resolve<EditorViewModel>();
    service.Save(IdeSettings.Default with { FontSize = 18 });
    Assert.AreSame(service.Current, editor.Settings);
  }

  [TestMethod]
  public void IniRoundTripPreservesPathsAndEditorSettings()
  {
    var compiler = Path.Combine(_directory, "Zero compiler;#=test.exe");
    File.WriteAllText(compiler, "test executable");
    var expected = new IdeSettings
    {
      CompilerPath = compiler,
      FontFamily = "Consolas, monospace",
      FontSize = 15.5,
      IndentationSize = 4,
      ConvertTabsToSpaces = false,
      ShowLineNumbers = false,
      WordWrap = true,
    };
    var service = new IniSettingsService(_filePath);
    var notifications = 0;
    service.SettingsChanged += (_, _) => notifications++;
    service.Save(expected);
    Assert.AreEqual(expected, new IniSettingsService(_filePath).Current);
    Assert.AreEqual(1, notifications);
    StringAssert.Contains(File.ReadAllText(_filePath), "[Compiler]");
    StringAssert.Contains(File.ReadAllText(_filePath), "[Editor]");
    Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
  }

  [TestMethod]
  public void MalformedValuesFallBackIndividuallyAndKeysIgnoreCase()
  {
    File.WriteAllText(_filePath, """
      ; hand-edited preferences
      [eDiToR]
      FontSize=NaN
      IndentationSize=999
      FontFamily=""
      ShowLineNumbers=invalid
      ConvertTabsToSpaces=false
      WordWrap=TRUE
      malformed line
      """);
    var actual = new IniSettingsService(_filePath).Current;
    Assert.AreEqual(14, actual.FontSize);
    Assert.AreEqual(2, actual.IndentationSize);
    Assert.AreEqual(IdeSettings.Default.FontFamily, actual.FontFamily);
    Assert.IsTrue(actual.ShowLineNumbers);
    Assert.IsFalse(actual.ConvertTabsToSpaces);
    Assert.IsTrue(actual.WordWrap);
  }

  [TestMethod]
  public void SavePreservesUnknownSectionsAndEscapedValues()
  {
    File.WriteAllText(_filePath, "[Future]\nValue=\"a \"\"quote\"\"; # = value\"\n[Editor]\nFutureOption=enabled\n");
    var service = new IniSettingsService(_filePath);
    service.Save(service.Current with { FontSize = 18 });
    var contents = File.ReadAllText(_filePath);
    StringAssert.Contains(contents, "[Future]");
    StringAssert.Contains(contents, "Value=\"a \"\"quote\"\"; # = value\"");
    StringAssert.Contains(contents, "FutureOption=\"enabled\"");
  }

  [TestMethod]
  public void InvalidSettingsDoNotOverwriteFileOrPublishChanges()
  {
    var service = new IniSettingsService(_filePath);
    service.Save(IdeSettings.Default);
    var original = File.ReadAllText(_filePath);
    var notifications = 0;
    service.SettingsChanged += (_, _) => notifications++;
    Assert.ThrowsExactly<ArgumentException>(() => service.Save(service.Current with { FontSize = 100 }));
    Assert.AreEqual(original, File.ReadAllText(_filePath));
    Assert.AreEqual(IdeSettings.Default, service.Current);
    Assert.AreEqual(0, notifications);
  }

  [TestMethod]
  public void SaveFailureLeavesCurrentSettingsUnchanged()
  {
    var blocker = Path.Combine(_directory, "blocker");
    File.WriteAllText(blocker, "not a directory");
    var service = new IniSettingsService(Path.Combine(blocker, "settings.ini"));
    Assert.ThrowsExactly<IOException>(() => service.Save(IdeSettings.Default with { FontSize = 18 }));
    Assert.AreEqual(IdeSettings.Default, service.Current);
  }

  [TestMethod]
  public void CancelDiscardsPendingEdits()
  {
    var service = new IniSettingsService(_filePath);
    var options = new OptionsDialogViewModel(service, Mock.Of<IEditorDialogService>());
    IDialogResult? result = null;
    DialogUtilities.InitializeListener(options, (IDialogResult value) => result = value);
    options.OnDialogOpened(new DialogParameters());
    options.FontSize = 20;
    options.CompilerPath = "not saved";
    options.CmdCancel.Execute();
    Assert.AreEqual(ButtonResult.Cancel, result?.Result);
    Assert.AreEqual(IdeSettings.Default, service.Current);
    Assert.IsFalse(File.Exists(_filePath));
  }

  [TestMethod]
  public void ApplyUpdatesEditorAndPersistsWithoutClosing()
  {
    var service = new IniSettingsService(_filePath);
    var editor = new EditorViewModel(service);
    editor.Document.Insert(0, "mov eax, 1");
    var options = new OptionsDialogViewModel(service, Mock.Of<IEditorDialogService>());
    var closed = false;
    var changed = false;
    DialogUtilities.InitializeListener(options, (IDialogResult _) => closed = true);
    editor.PropertyChanged += (_, e) => changed |= e.PropertyName == nameof(EditorViewModel.Settings);
    options.OnDialogOpened(new DialogParameters());
    options.FontSize = 18;
    options.WordWrap = true;
    options.CmdApply.Execute();
    Assert.IsFalse(closed);
    Assert.IsTrue(changed);
    Assert.AreEqual(18, editor.Settings.FontSize);
    Assert.IsTrue(editor.Settings.WordWrap);
    Assert.AreEqual(service.Current, new IniSettingsService(_filePath).Current);
    Assert.AreEqual("mov eax, 1", editor.Text);
    Assert.IsTrue(editor.Document.UndoStack.CanUndo);
  }

  [TestMethod]
  public void OkDoesNotCloseWhenSaveFails()
  {
    var service = new Mock<ISettingsService>();
    service.SetupGet(s => s.Current).Returns(IdeSettings.Default);
    service.SetupGet(s => s.FilePath).Returns(_filePath);
    service.Setup(s => s.Save(It.IsAny<IdeSettings>())).Throws(new IOException("Disk full"));
    var options = new OptionsDialogViewModel(service.Object, Mock.Of<IEditorDialogService>());
    var closed = false;
    DialogUtilities.InitializeListener(options, (IDialogResult _) => closed = true);
    options.OnDialogOpened(new DialogParameters());
    options.CmdOk.Execute();
    Assert.IsFalse(closed);
    Assert.AreEqual("Disk full", options.ErrorMessage);
  }

  [TestMethod]
  public void OkSavesAndCloses()
  {
    var service = new IniSettingsService(_filePath);
    var options = new OptionsDialogViewModel(service, Mock.Of<IEditorDialogService>());
    IDialogResult? result = null;
    DialogUtilities.InitializeListener(options, (IDialogResult value) => result = value);
    options.OnDialogOpened(new DialogParameters());
    options.IndentationSize = 4;
    options.CmdOk.Execute();
    Assert.AreEqual(ButtonResult.OK, result?.Result);
    Assert.AreEqual(4, new IniSettingsService(_filePath).Current.IndentationSize);
  }
}
