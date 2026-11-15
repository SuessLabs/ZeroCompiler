// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using Avalonia.Threading;
using Moq;
using Zero.Ide.Models;
using Zero.Ide.Services;

namespace Zero.Ide.Tests;

[TestClass]
[DoNotParallelize]
public class EditorExecutionCoordinatorTests
{
  private static string SourcePath => Path.Combine(Path.GetTempPath(), "program" + SourceFileExtensions.Assembly);

  [TestMethod]
  public async Task CancelingSaveNeverValidatesBuildsOrLaunchesAsync()
  {
    var compiler = new Mock<ICompilerService>(MockBehavior.Strict);
    var runner = new Mock<IProgramRunner>(MockBehavior.Strict);
    var stages = new List<EditorExecutionStage>();

    var result = await new EditorExecutionCoordinator(compiler.Object, runner.Object).ExecuteAsync(
      Request(EditorExecutionMode.Run) with
      {
        SaveSourceAsync = () => Task.FromResult<string?>(null)
      }, update => stages.Add(update.Stage));

    Assert.AreEqual(EditorExecutionStage.Canceled, result.Stage);
    Assert.AreSequenceEqual(new[] { EditorExecutionStage.ValidateCompiler, EditorExecutionStage.Save, EditorExecutionStage.Canceled }, stages);

    compiler.VerifyNoOtherCalls();
    runner.VerifyNoOtherCalls();
  }

  [TestMethod]
  public async Task ConcurrentExecutionIsRejectedWithoutASecondCompilerProcessAsync()
  {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var finished = new TaskCompletionSource<CompilerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    var compiler = new Mock<ICompilerService>();

    compiler
      .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
      .Callback(() => entered.SetResult()).Returns(finished.Task);

    var coordinator = new EditorExecutionCoordinator(compiler.Object);
    var first = coordinator.ExecuteAsync(Request(EditorExecutionMode.Build), _ => { });

    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

    try
    {
      await Assert.ThrowsExactlyAsync<InvalidOperationException>(
        () => coordinator.ExecuteAsync(Request(EditorExecutionMode.Build), _ => { }));
    }
    finally
    {
      finished.TrySetResult(new CompilerResult(0, "compiled", "program.exe"));
      await first;
    }

    compiler.Verify(
      c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false),
      Times.Once);
  }

  [TestMethod]
  [DataRow("missing-compiler")]
  [DataRow("invalid-source")]
  [DataRow("save-error")]
  [DataRow("compiler-error")]
  [DataRow("compiler-exception")]
  public async Task FailuresStopTheWorkflowBeforeLaunchingAsync(string failure)
  {
    var compiler = new Mock<ICompilerService>();
    var compilation = new CompilerResult(2, "bad instruction", "program.exe");
    compiler
      .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
      .ReturnsAsync(compilation);

    if (failure == "compiler-exception")
      compiler
        .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
        .ThrowsAsync(new IOException("Unable to start"));

    var runner = new Mock<IProgramRunner>(MockBehavior.Strict);
    var request = Request(EditorExecutionMode.Run);

    if (failure == "missing-compiler")
      request = request with { CompilerPath = "" };

    if (failure == "invalid-source")
      request = request with { SaveSourceAsync = () => Task.FromResult<string?>(Path.ChangeExtension(SourcePath, SourceFileExtensions.Source)) };

    if (failure == "save-error")
      request = request with { SaveSourceAsync = () => throw new IOException("Disk full") };

    var result = await new EditorExecutionCoordinator(compiler.Object, runner.Object).ExecuteAsync(request, _ => { });
    Assert.AreEqual(EditorExecutionStage.Failed, result.Stage);

    if (failure == "compiler-error")
      Assert.AreSame(compilation, result.Compilation);
    else
      Assert.IsNotNull(result.Error);

    if (failure is "missing-compiler" or "invalid-source" or "save-error")
      compiler.VerifyNoOtherCalls();

    runner.VerifyNoOtherCalls();
  }

  [TestMethod]
  [DataRow(EditorExecutionMode.Run, EditorExecutionStage.RunFailed)]
  [DataRow(EditorExecutionMode.Debug, EditorExecutionStage.Failed)]
  public async Task LaunchFailuresPreserveCompilerOutputAndPermitRetryAsync(EditorExecutionMode mode, EditorExecutionStage expected)
  {
    var compiler = new Mock<ICompilerService>();

    compiler.Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
      .ReturnsAsync(new CompilerResult(0, "compiled successfully", "program.exe"));

    var runner = new Mock<IProgramRunner>();
    runner.Setup(r => r.Run(It.IsAny<string>(), It.IsAny<string>()))
          .Throws(new InvalidOperationException("Cannot run"));

    var coordinator = new EditorExecutionCoordinator(compiler.Object, runner.Object);
    var result = await coordinator.ExecuteAsync(Request(mode) with { StartDebuggerAsync = (_, _) => throw new InvalidOperationException("No debug map") }, _ => { });

    Assert.AreEqual(expected, result.Stage);
    Assert.AreEqual("compiled successfully", result.Compilation!.Output);
    Assert.IsNotNull(result.Error);
    Assert.AreEqual(EditorExecutionStage.Completed, (await coordinator.ExecuteAsync(Request(EditorExecutionMode.Build), _ => { })).Stage);
  }

  [TestMethod]
  [DataRow(EditorExecutionMode.Build)]
  [DataRow(EditorExecutionMode.Run)]
  [DataRow(EditorExecutionMode.Debug)]
  public async Task SuccessfulWorkflowSavesBuildsThenUsesOnlyRequestedLauncherAsync(EditorExecutionMode mode)
  {
    var calls = new List<string>();
    var compiler = new Mock<ICompilerService>();
    var configuration = mode == EditorExecutionMode.Debug ? BuildConstants.DebugConfiguration : BuildConstants.ReleaseConfiguration;

    compiler
      .Setup(c => c.BuildAsync("compiler", SourcePath, configuration, true))
      .Callback(() => calls.Add("compile"))
      .ReturnsAsync(new CompilerResult(0, "compiled", "program.exe"));

    var runner = new Mock<IProgramRunner>();
    runner.Setup(r => r.Run("program.exe", Path.GetDirectoryName(SourcePath)!))
          .Callback(() => calls.Add("run"));

    var request = new EditorExecutionRequest(
      "compiler",
      BuildConstants.ReleaseConfiguration,
      true,
      mode,
      () =>
      {
        calls.Add("save");
        return Task.FromResult<string?>(SourcePath);
      },
      (executable, source) =>
      {
        Assert.AreEqual("program.exe", executable);
        Assert.AreEqual(SourcePath, source);
        calls.Add("debug");
        return Task.CompletedTask;
      });

    var updates = new List<EditorExecutionUpdate>();
    var result = await new EditorExecutionCoordinator(compiler.Object, runner.Object).ExecuteAsync(request, updates.Add);

    Assert.AreEqual(EditorExecutionStage.Completed, result.Stage);
    Assert.AreSequenceEqual(
      mode == EditorExecutionMode.Build
      ? new[] { "save", "compile" }
      : new[] { "save", "compile", mode == EditorExecutionMode.Run ? "run" : "debug" }, calls);

    Assert.AreEqual(EditorExecutionStage.ValidateCompiler, updates[0].Stage);
    Assert.IsGreaterThan(updates.FindIndex(update => update.Stage == EditorExecutionStage.Build), updates.FindIndex(update => update.Stage == EditorExecutionStage.Built));
  }

  [TestMethod]
  public async Task WorkflowCallbacksRemainOnAvaloniaUiThreadAfterAsyncTransitionsAsync()
  {
    using var session = HeadlessUnitTestSession.StartNew(typeof(DebuggerDockRenderingTests.DockingTestApp));
    await session.Dispatch(async () =>
    {
      var updates = new List<EditorExecutionUpdate>();
      var compiler = new Mock<ICompilerService>();

      compiler.Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
        .Returns(async () =>
        {
          await Task.Delay(1);
          return new CompilerResult(0, "compiled", "program.exe");
        });

      var coordinator = new EditorExecutionCoordinator(compiler.Object);
      var result = await coordinator.ExecuteAsync(Request(EditorExecutionMode.Debug) with
      {
        SaveSourceAsync = async () =>
        {
          Assert.IsTrue(Dispatcher.UIThread.CheckAccess());
          await Task.Delay(1);
          return SourcePath;
        },
        StartDebuggerAsync = (_, _) =>
        {
          Assert.IsTrue(Dispatcher.UIThread.CheckAccess());
          return Task.CompletedTask;
        },
      }, update =>
      {
        Assert.IsTrue(Dispatcher.UIThread.CheckAccess());
        updates.Add(update);
      });

      Assert.AreEqual(EditorExecutionStage.Completed, result.Stage);
      Assert.Contains(update => update.Stage == EditorExecutionStage.Launch, updates);

      return 0;
    }, CancellationToken.None);
  }

  private static EditorExecutionRequest Request(EditorExecutionMode mode) =>
    new(
      "compiler",
      BuildConstants.DebugConfiguration,
      false,
      mode,
      () => Task.FromResult<string?>(SourcePath));
}
