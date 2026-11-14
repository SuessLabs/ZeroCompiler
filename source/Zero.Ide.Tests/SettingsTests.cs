// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Styling;
using Moq;
using Prism.Container.DryIoc;
using Prism.Dialogs;
using Prism.Ioc;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.Themes;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class SettingsTests
{
  private string _directory = string.Empty;
  private string _filePath = string.Empty;

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
    options.TrimTrailingWhitespaceOnSave = true;
    options.SelectedTheme = options.Themes.Single(t => t.Value == IdeTheme.CoolSlate);
    options.CmdApply.Execute();

    Assert.IsFalse(closed);
    Assert.IsTrue(changed);
    Assert.AreEqual(18, editor.Settings.FontSize);
    Assert.IsTrue(editor.Settings.WordWrap);
    Assert.IsTrue(editor.Settings.TrimTrailingWhitespaceOnSave);
    Assert.AreEqual(IdeTheme.CoolSlate, editor.Settings.Theme);
    Assert.AreEqual(service.Current, new IniSettingsService(_filePath).Current);
    Assert.AreEqual("mov eax, 1", editor.Text);
    Assert.IsTrue(editor.Document.UndoStack.CanUndo);
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
    options.TrimTrailingWhitespaceOnSave = true;
    options.SelectedTheme = options.Themes.Single(t => t.Value == IdeTheme.Dark);
    options.CmdCancel.Execute();

    Assert.AreEqual(ButtonResult.Cancel, result?.Result);
    Assert.AreEqual(IdeSettings.Default, service.Current);
    Assert.IsFalse(File.Exists(_filePath));
  }

  [TestCleanup]
  public void Cleanup()
  {
    var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Zero.Ide.Settings.Tests")) + Path.DirectorySeparatorChar;
    var directory = Path.GetFullPath(_directory);

    Assert.IsTrue(directory.StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase));

    if (Directory.Exists(directory))
      Directory.Delete(directory, true);
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
  public void FailedMenuThemeSaveKeepsPreviousSelectionAndReportsError()
  {
    var service = new Mock<ISettingsService>();
    service.SetupGet(s => s.Current).Returns(IdeSettings.Default);
    service.Setup(s => s.Save(It.IsAny<IdeSettings>())).Throws(new IOException("Disk full"));

    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    var main = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, service.Object);
    main.CmdThemeDark.Execute();

    Assert.IsTrue(main.IsSystemTheme);
    Assert.IsFalse(main.IsDarkTheme);

    dialogs.Verify(d => d.ShowMessageAsync("Operation failed", "Disk full"), Times.Once);
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
      Theme = IdeTheme.CoolSlate,
      TrimTrailingWhitespaceOnSave = true,
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

  [TestInitialize]
  public void Initialize()
  {
    _directory = Path.Combine(Path.GetTempPath(), "Zero.Ide.Settings.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_directory);
    _filePath = Path.Combine(_directory, "settings.ini");
  }

  [TestMethod]
  [DataRow("true", true)]
  [DataRow("FALSE", false)]
  [DataRow("invalid", false)]
  public void CleanupPreferenceLoadsAndOptionsReflectIt(string value, bool expected)
  {
    File.WriteAllText(_filePath, $"[CodeCleanup]\nTrimTrailingWhitespaceOnSave={value}\n");
    var settings = new IniSettingsService(_filePath);
    var options = new OptionsDialogViewModel(settings, Mock.Of<IEditorDialogService>());
    options.OnDialogOpened(new DialogParameters());
    options.SelectedCategory = options.Categories.Single(c => c.Key == "editor").Children.Single(c => c.Key == "cleanup");
    Assert.IsTrue(options.ShowCodeCleanup);
    Assert.AreEqual("Code Cleanup", options.PageTitle);
    Assert.AreEqual(expected, options.TrimTrailingWhitespaceOnSave);
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
  public void MenuThemeChangesPreserveEditorSettingsAndTrackOptionsChanges()
  {
    var service = new IniSettingsService(_filePath);
    service.Save(service.Current with { FontSize = 19, WordWrap = true });
    var main = new MainWindowViewModel(new EditorViewModel(service), Mock.Of<IEditorDialogService>(), service);

    Assert.IsTrue(main.IsSystemTheme);

    main.CmdThemeCoolSlate.Execute();

    Assert.IsTrue(main.IsCoolSlateTheme);
    Assert.IsFalse(main.IsSystemTheme);
    Assert.AreEqual(19, service.Current.FontSize);
    Assert.IsTrue(service.Current.WordWrap);

    var notifications = 0;
    main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(main.IsLightTheme)) notifications++; };
    var options = new OptionsDialogViewModel(service, Mock.Of<IEditorDialogService>());
    options.OnDialogOpened(new DialogParameters());
    Assert.AreEqual(IdeTheme.CoolSlate, options.SelectedTheme.Value);

    options.SelectedTheme = options.Themes.Single(t => t.Value == IdeTheme.Light);
    options.CmdApply.Execute();
    Assert.IsTrue(main.IsLightTheme);
    Assert.IsFalse(main.IsCoolSlateTheme);
    Assert.AreEqual(1, notifications);

    main.CmdThemeDark.Execute();
    Assert.IsTrue(main.IsDarkTheme);

    main.CmdThemeSystem.Execute();
    Assert.IsTrue(main.IsSystemTheme);
    Assert.AreEqual(IdeTheme.System, new IniSettingsService(_filePath).Current.Theme);
  }

  [TestMethod]
  public void MissingFileUsesDefaultsWithoutCreatingFile()
  {
    var service = new IniSettingsService(_filePath);
    Assert.AreEqual(IdeSettings.Default, service.Current);
    Assert.IsFalse(File.Exists(_filePath));
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
  public void SavePreservesUnknownSectionsAndEscapedValues()
  {
    File.WriteAllText(_filePath, "[Future]\nValue=\"a \"\"quote\"\"; # = value\"\n[Editor]\nFutureOption=enabled\n");
    var service = new IniSettingsService(_filePath);
    service.Save(service.Current with { FontSize = 18 });
    var contents = File.ReadAllText(_filePath);

    Assert.Contains("[Future]", contents);
    Assert.Contains("Value=\"a \"\"quote\"\"; # = value\"", contents);
    Assert.Contains("FutureOption=\"enabled\"", contents);
  }

  [TestMethod]
  [DataRow("System", IdeTheme.System)]
  [DataRow("light", IdeTheme.Light)]
  [DataRow("DARK", IdeTheme.Dark)]
  [DataRow("CoolSlate", IdeTheme.CoolSlate)]
  [DataRow("unknown", IdeTheme.System)]
  [DataRow("99", IdeTheme.System)]
  public void ThemeLoadsAndRoundTripsWithSafeFallback(string value, IdeTheme expected)
  {
    File.WriteAllText(_filePath, $"[Appearance]\nTheme={value}\n");
    var service = new IniSettingsService(_filePath);

    Assert.AreEqual(expected, service.Current.Theme);

    service.Save(service.Current);
    Assert.AreEqual(expected, new IniSettingsService(_filePath).Current.Theme);
  }

  [TestMethod]
  public void ThemeVariantsRetainSystemFollowingAndDarkSlateFallback()
  {
    Assert.AreSame(ThemeVariant.Default, IdeThemes.GetVariant(IdeTheme.System));
    Assert.AreSame(ThemeVariant.Light, IdeThemes.GetVariant(IdeTheme.Light));
    Assert.AreSame(ThemeVariant.Dark, IdeThemes.GetVariant(IdeTheme.Dark));
    Assert.AreSame(ThemeVariant.Dark, IdeThemes.CoolSlateVariant.InheritVariant);
    Assert.IsTrue(IdeThemes.IsDark(IdeThemes.GetVariant(IdeTheme.CoolSlate)));
    Assert.IsFalse(IdeThemes.IsDark(ThemeVariant.Light));
  }
}
