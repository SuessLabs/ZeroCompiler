// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Moq;
using Prism.Commands;
using Prism.Container.DryIoc;
using Prism.Ioc;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

[TestClass]
public class BuildTests
{
  [TestMethod]
  public async Task BuildAndRunLaunchesOnlyAfterSuccessfulCompilationAsync()
  {
    var file = CreateFile("sample.zasm");
    var compiler = new Mock<ICompilerService>(MockBehavior.Strict);
    var runner = new Mock<IProgramRunner>(MockBehavior.Strict);
    var sequence = new MockSequence();

    compiler.InSequence(sequence)
      .Setup(c => c.BuildAsync("compiler", file.Object.Path.LocalPath, "Debug", false))
      .ReturnsAsync(new CompilerResult(0, "Compiled sample", "sample.exe"));

    runner.InSequence(sequence).Setup(r => r.Run("sample.exe", Path.GetDirectoryName(file.Object.Path.LocalPath)!));
    var shell = new MainWindowViewModel(CreateEditor(), CreateDialogs(file.Object).Object,
      compilerService: compiler.Object, programRunner: runner.Object);

    await ExecuteAsync(shell, shell.CmdBuildAndRun);
    runner.Verify(r => r.Run("sample.exe", It.IsAny<string>()), Times.Once);
    Assert.AreEqual("Idle", shell.BuildStatus);
    Assert.IsFalse(shell.IsBusy);
    Assert.Contains("Started", shell.Status);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task BuildAndRunDoesNotLaunchAfterFailureOrSaveCancellationAsync(bool cancelSave)
  {
    var file = CreateFile("sample.zasm");
    var compiler = new Mock<ICompilerService>();
    compiler.Setup(c => c.BuildAsync(
      compilerPath: It.IsAny<string>(),
      sourcePath: It.IsAny<string>(),
      configuration: It.IsAny<string>(),
      cleanFirst: false))
      .ReturnsAsync(new CompilerResult(2, "Assembly error", "sample.exe"));

    var runner = new Mock<IProgramRunner>();
    var shell = new MainWindowViewModel(
      CreateEditor(),
      CreateDialogs(cancelSave ? null : file.Object).Object,
      compilerService: compiler.Object, programRunner: runner.Object);

    await ExecuteAsync(shell, shell.CmdBuildAndRun);
    runner.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    Assert.AreEqual(cancelSave ? "Idle" : "Build failure", shell.BuildStatus);
  }

  [TestMethod]
  public async Task RunFailurePreservesSuccessfulBuildDiagnosticsAndAllowsRetryAsync()
  {
    var file = CreateFile("sample.zasm");
    var compiler = new Mock<ICompilerService>();
    compiler.Setup(c => c.BuildAsync(
      It.IsAny<string>(),
      It.IsAny<string>(),
      It.IsAny<string>(),
      false))
      .ReturnsAsync(new CompilerResult(0, "Compiled sample", "sample.exe"));

    var runner = new Mock<IProgramRunner>();
    runner.Setup(r => r.Run(It.IsAny<string>(), It.IsAny<string>())).Throws(new InvalidOperationException("Incompatible host"));
    var dialogs = CreateDialogs(file.Object);
    var shell = new MainWindowViewModel(CreateEditor(), dialogs.Object, compilerService: compiler.Object, programRunner: runner.Object);

    await ExecuteAsync(shell, shell.CmdBuildAndRun);

    Assert.AreEqual("Run failure", shell.BuildStatus);
    Assert.IsTrue(shell.HasRunFailed);
    Assert.IsFalse(shell.HasBuildFailed);
    Assert.IsFalse(shell.IsBusy);
    Assert.Contains("Compiled sample", shell.BuildOutput);
    Assert.Contains("Incompatible host", shell.BuildOutput);

    dialogs.Verify(d => d.ShowMessageAsync("Run failure", "Incompatible host"), Times.Once);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Idle", shell.BuildStatus);
    Assert.IsFalse(shell.HasRunFailed);
    runner.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
  }

  [TestMethod]
  public void ContainerInjectsCompilerServiceIntoBuildCommands()
  {
    var container = new DryIocContainerExtension();
    container.RegisterInstance<ICompilerService>(Mock.Of<ICompilerService>());
    container.RegisterInstance<IProgramRunner>(Mock.Of<IProgramRunner>());
    container.RegisterInstance<IEditorDialogService>(Mock.Of<IEditorDialogService>());
    container.RegisterInstance<ISettingsService>(Mock.Of<ISettingsService>(s => s.Current == IdeSettings.Default));
    container.RegisterSingleton<EditorViewModel>();
    container.RegisterSingleton<MainWindowViewModel>();
    var shell = container.Resolve<MainWindowViewModel>();

    Assert.IsTrue(shell.CmdBuild.CanExecute());
    Assert.IsTrue(shell.CmdRebuild.CanExecute());
    Assert.IsTrue(shell.CmdClean.CanExecute());
    Assert.IsTrue(shell.CmdBuildAndRun.CanExecute());
  }

  [TestMethod]
  public async Task SaveFailurePreventsCompilerInvocationAsync()
  {
    var file = CreateFile("sample.zasm");
    file.Setup(f => f.OpenWriteAsync()).ThrowsAsync(new IOException("Disk full"));

    var compiler = new Mock<ICompilerService>();
    var editor = CreateEditor();
    editor.Text = "ret";

    var shell = new MainWindowViewModel(editor, CreateDialogs(file.Object).Object, compilerService: compiler.Object);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Build failure", shell.BuildStatus);
    Assert.IsTrue(editor.IsModified);
    Assert.Contains("Disk full", shell.BuildOutput);

    compiler.Verify(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
  }

  [TestMethod]
  public async Task BuildSavesCurrentTextAndTransitionsFromBuildingToIdleAsync()
  {
    var editor = CreateEditor();
    editor.Text = "ret \t";

    var file = CreateFile("sample.ZASM");
    var dialogs = CreateDialogs(file.Object);
    var compiler = new Mock<ICompilerService>();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var result = new TaskCompletionSource<CompilerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

    compiler.Setup(c => c.BuildAsync("compiler", file.Object.Path.LocalPath, "Release", true))
      .Callback(() => started.TrySetResult()).Returns(result.Task);

    var shell = new MainWindowViewModel(editor, dialogs.Object, compilerService: compiler.Object)
    {
      BuildConfiguration = "Release",
    };

    var finished = ExecuteAsync(shell, shell.CmdRebuild);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual("ret", editor.Text);
    Assert.IsFalse(editor.IsModified);
    Assert.IsTrue(shell.IsBuilding);
    Assert.AreEqual("Building...", shell.BuildStatus);
    Assert.IsFalse(shell.CmdBuild.CanExecute());
    Assert.IsFalse(shell.CmdSave.CanExecute());
    Assert.IsFalse(await shell.CanCloseAsync());

    shell.CmdBuild.Execute(); // A programmatic re-entry must not start another process.
    result.SetResult(new CompilerResult(0, "Compiled sample", "sample.exe"));
    await finished;

    Assert.AreEqual("Idle", shell.BuildStatus);
    Assert.IsFalse(shell.IsBusy);
    Assert.IsTrue(shell.CmdRebuild.CanExecute());
    Assert.Contains("Build succeeded", shell.Status);
    Assert.Contains("Compiled sample", shell.BuildOutput);

    compiler.Verify(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
  }

  [TestMethod]
  public async Task FailedBuildDisplaysDiagnosticsAndSuccessfulRetryClearsFailureAsync()
  {
    var file = CreateFile("sample.zasm");
    var dialogs = CreateDialogs(file.Object);
    var compiler = new Mock<ICompilerService>();

    compiler
      .SetupSequence(c => c.BuildAsync("compiler", file.Object.Path.LocalPath, "Debug", false))
      .ReturnsAsync(new CompilerResult(2, "Assembly error: unknown instruction", "sample.exe"))
      .ReturnsAsync(new CompilerResult(0, "Compiled sample", "sample.exe"));

    var shell = new MainWindowViewModel(CreateEditor(), dialogs.Object, compilerService: compiler.Object);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Build failure", shell.BuildStatus);
    Assert.IsTrue(shell.HasBuildFailed);
    Assert.IsFalse(shell.IsBuilding);

    dialogs.Verify(d => d.ShowMessageAsync("Build failure", It.Is<string>(s => s.Contains("unknown instruction"))), Times.Once);
    await ExecuteAsync(shell, shell.CmdBuildOutput);

    dialogs.Verify(d => d.ShowMessageAsync("Build output", It.Is<string>(s => s.Contains("unknown instruction"))), Times.Once);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Idle", shell.BuildStatus);
    Assert.IsFalse(shell.HasBuildFailed);

    dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>()), Times.Once);
    await ExecuteAsync(shell, shell.CmdClean);
    compiler.Verify(c => c.Clean(file.Object.Path.LocalPath, "Debug"), Times.Once);
  }

  [TestMethod]
  public async Task CancellingSavePreventsCompilationAsync()
  {
    var dialogs = CreateDialogs(null);
    var compiler = new Mock<ICompilerService>();
    var shell = new MainWindowViewModel(CreateEditor(), dialogs.Object, compilerService: compiler.Object);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Idle", shell.BuildStatus);
    Assert.IsFalse(shell.IsBusy);
    compiler.Verify(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
  }

  [TestMethod]
  public async Task CompilerLaunchExceptionBecomesBuildFailureAsync()
  {
    var file = CreateFile("sample.zasm");
    var dialogs = CreateDialogs(file.Object);
    var compiler = new Mock<ICompilerService>();

    compiler
      .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
      .ThrowsAsync(new IOException("Compiler could not start"));

    var shell = new MainWindowViewModel(CreateEditor(), dialogs.Object, compilerService: compiler.Object);
    await ExecuteAsync(shell, shell.CmdBuild);
    Assert.AreEqual("Build failure", shell.BuildStatus);
    Assert.IsFalse(shell.IsBuilding);
    Assert.IsFalse(shell.IsBusy);
    Assert.Contains("could not start", shell.BuildOutput);
  }

  [TestMethod]
  public async Task NonZasmFileIsRejectedBeforeCompilerInvocationAsync()
  {
    var file = CreateFile("sample.txt");
    var compiler = new Mock<ICompilerService>();
    var shell = new MainWindowViewModel(CreateEditor(), CreateDialogs(file.Object).Object, compilerService: compiler.Object);

    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Build failure", shell.BuildStatus);
    Assert.Contains("Only .zasm", shell.BuildOutput);
    compiler.Verify(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
  }

  [TestMethod]
  public async Task MissingCompilerExplainsHowToConfigureItAsync()
  {
    var compiler = new Mock<ICompilerService>();
    var dialogs = CreateDialogs(null);

    var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, compilerService: compiler.Object);
    await ExecuteAsync(shell, shell.CmdBuild);

    Assert.AreEqual("Build failure", shell.BuildStatus);
    Assert.Contains("Tools > Options > Compiler", shell.BuildOutput);

    dialogs.Verify(d => d.SaveFileAsync(It.IsAny<string>()), Times.Never);
  }

  [TestMethod]
  public void CleanOnlyRemovesSelectedSourceAndConfigurationArtifact()
  {
    var root = Path.Combine(Path.GetTempPath(), "Zero.Ide.Build.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);

    try
    {
      var source = Path.Combine(root, "sample.zasm");
      File.WriteAllText(source, "ret");

      var debug = Path.Combine(root, ".zeroide", "Debug", "sample.zasm", "sample.exe");
      var release = Path.Combine(root, ".zeroide", "Release", "sample.zasm", "sample.exe");

      Directory.CreateDirectory(Path.GetDirectoryName(debug)!);
      Directory.CreateDirectory(Path.GetDirectoryName(release)!);
      File.WriteAllText(debug, "debug");
      File.WriteAllText(release, "release");
      var linuxDebug = Path.Combine(Path.GetDirectoryName(debug)!, "sample");
      var legacyDebug = Path.Combine(Path.GetDirectoryName(debug)!, "sample.bin");
      File.WriteAllText(linuxDebug, "linux");
      File.WriteAllText(legacyDebug, "legacy");

      var service = new CompilerService();
      service.Clean(source, "Debug");
      service.Clean(source, "Debug"); // A repeated clean is harmless.

      Assert.IsFalse(File.Exists(debug));
      Assert.IsFalse(File.Exists(linuxDebug));
      Assert.IsFalse(File.Exists(legacyDebug));
      Assert.IsTrue(File.Exists(release));
      Assert.AreEqual("ret", File.ReadAllText(source));
      Assert.ThrowsExactly<ArgumentException>(() => service.Clean(source, "../../"));
    }
    finally
    {
      var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Zero.Ide.Build.Tests")) + Path.DirectorySeparatorChar;
      Assert.IsTrue(Path.GetFullPath(root).StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase));
      Directory.Delete(root, true);
    }
  }

  private static EditorViewModel CreateEditor()
  {
    var settings = new Mock<ISettingsService>();
    settings
      .SetupGet(s => s.Current)
      .Returns(IdeSettings.Default with { CompilerPath = "compiler", TrimTrailingWhitespaceOnSave = true });

    return new EditorViewModel(settings.Object);
  }

  private static Mock<IStorageFile> CreateFile(string name)
  {
    var file = new Mock<IStorageFile>();
    file.SetupGet(f => f.Name).Returns(name);
    file.SetupGet(f => f.Path).Returns(new Uri(Path.Combine(Path.GetTempPath(), name)));
    file.Setup(f => f.OpenWriteAsync()).ReturnsAsync(() => new MemoryStream());
    return file;
  }

  private static Mock<IEditorDialogService> CreateDialogs(IStorageFile? file)
  {
    var dialogs = new Mock<IEditorDialogService>();
    dialogs.Setup(d => d.SaveFileAsync(It.IsAny<string>())).ReturnsAsync(file);
    dialogs.Setup(d => d.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    return dialogs;
  }

  private static async Task ExecuteAsync(MainWindowViewModel shell, DelegateCommand command)
  {
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    shell.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy) completed.TrySetResult();
    };

    command.Execute();
    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
  }
}
