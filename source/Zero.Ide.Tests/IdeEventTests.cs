// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Prism.Commands;
using Zero.Dbg;
using Zero.Ide.Events;
using Zero.Ide.Models;
using Zero.Ide.Services;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Tests;

/// <summary>Verifies event delivery isolation and notifications emitted by execution, workspace, and debugger workflows.</summary>
[TestClass]
public class IdeEventTests
{
  /// <summary>Gets the absolute assembly source path used by mock compilation requests.</summary>
  private static string SourcePath => Path.Combine(Path.GetTempPath(), "event-program" + SourceFileExtensions.Assembly);

  /// <summary>Verifies that a blocked subscriber cannot delay state-machine completion or another subscriber's notifications.</summary>
  [TestMethod]
  public async Task BlockedSubscriberDoesNotStallStateMachineOrOtherSubscribersAsync()
  {
    using var events = new IdeEventService();
    var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    using var slowSubscription = events.Subscribe<ExecutionStateChangedEvent>(async _ =>
    {
      blocked.TrySetResult();
      await release.Task;
    });

    using var fastSubscription = events.Subscribe<ExecutionStateChangedEvent>(notification =>
    {
      if (notification.Stage == BuildExecutionStage.Completed)
        completed.TrySetResult();
    });

    try
    {
      var compiler = CreateCompiler();
      var outcome = await new EditorExecutionCoordinator(compiler.Object, events: events)
        .ExecuteAsync(Request(), _ => { })
        .WaitAsync(TimeSpan.FromSeconds(5));

      await Task.WhenAll(blocked.Task, completed.Task).WaitAsync(TimeSpan.FromSeconds(5));
      Assert.AreEqual(BuildExecutionStage.Completed, outcome.Stage);
      Assert.IsFalse(release.Task.IsCompleted);
    }
    finally
    {
      release.TrySetResult();
    }
  }

  /// <summary>Verifies debugger pauses publish copied register values and session exits publish a separate notification.</summary>
  [TestMethod]
  public void DebuggerPausePublishesCopiedValuesAndSessionExitIsDistinct()
  {
    var session = new Mock<IDebugSession>();
    var events = new Mock<IIdeEventService>();
    var pauses = new List<DebugStoppedEvent>();
    var ends = new List<DebugSessionEndedEvent>();

    events.Setup(service => service.Publish(It.IsAny<DebugStoppedEvent>())).Callback<DebugStoppedEvent>(pauses.Add);
    events.Setup(service => service.Publish(It.IsAny<DebugSessionEndedEvent>())).Callback<DebugSessionEndedEvent>(ends.Add);

    _ = new MainWindowViewModel(new EditorViewModel(), Mock.Of<IEditorDialogService>(), debugSession: session.Object, events: events.Object);
    var registers = new List<RegisterValue> { new("EAX", 42) };

    session.Raise(
      s => s.Changed += null,
      session.Object,
      new DebugSnapshot(DebugState.Paused, "Breakpoint", ThreadId: 7, Registers: registers));

    registers[0] = new RegisterValue("EAX", 99);

    Assert.HasCount(1, pauses);
    Assert.AreEqual(7u, pauses[0].ThreadId);
    Assert.AreEqual(42u, pauses[0].Registers[0].Value);

    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Running, "Continuing"));

    Assert.HasCount(1, pauses);

    session.Raise(s => s.Changed += null, session.Object, new DebugSnapshot(DebugState.Exited, "Exited", ExitCode: 0));

    Assert.HasCount(1, ends);
    Assert.AreEqual(0u, ends[0].ExitCode);
  }

  /// <summary>Verifies bounded buffering drops oldest pending notifications and service disposal disables further publication and subscriptions.</summary>
  [TestMethod]
  public async Task LaggingSubscriptionHasBoundedBufferAndDisposalStopsQueuedDeliveryAsync()
  {
    using var events = new IdeEventService();

    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var received = new ConcurrentQueue<int>();
    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var last = IdeEventService.SubscriptionBufferCapacity + 20;

    using var subscription = events.Subscribe<TestEvent>(async notification =>
    {
      received.Enqueue(notification.Number);
      if (notification.Number == 0)
      {
        entered.TrySetResult();
        await release.Task;
      }

      if (notification.Number == last)
        finished.TrySetResult();
    });

    events.Publish(new TestEvent(0));
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

    for (var index = 1; index <= last; index++)
      events.Publish(new TestEvent(index));

    release.TrySetResult();

    await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.HasCount(IdeEventService.SubscriptionBufferCapacity + 1, received);
    Assert.AreSequenceEqual(
      new[] { 0 }
      .Concat(
        Enumerable.Range(last - IdeEventService.SubscriptionBufferCapacity + 1,
        IdeEventService.SubscriptionBufferCapacity))
      .ToArray(),
      received.ToArray());

    subscription.Dispose();
    events.Dispose();
    events.Publish(new TestEvent(last + 1)); // Shutdown drops notifications without invoking code or waiting.
    Assert.ThrowsExactly<ObjectDisposedException>(() => events.Subscribe<TestEvent>(_ => { }));
  }

  /// <summary>Verifies per-subscription ordering and continued delivery when another subscriber throws.</summary>
  [TestMethod]
  public async Task SubscribersReceiveOrderedNotificationsAndExceptionsAreIsolatedAsync()
  {
    using var events = new IdeEventService();
    var received = new ConcurrentQueue<int>();
    var healthy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var faulty = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    using var healthySubscription = events.Subscribe<TestEvent>(notification =>
    {
      received.Enqueue(notification.Number);
      if (notification.Number == 3)
        healthy.TrySetResult();
    });

    using var faultySubscription = events.Subscribe<TestEvent>(async notification =>
    {
      await Task.Yield();
      if (notification.Number == 3)
        faulty.TrySetResult();

      throw new InvalidOperationException("Extension failed");
    });

    for (var index = 0; index < 4; index++)
      events.Publish(new TestEvent(index));

    await Task.WhenAll(healthy.Task, faulty.Task).WaitAsync(TimeSpan.FromSeconds(5));
    Assert.AreSequenceEqual(new[] { 0, 1, 2, 3 }, received.ToArray());
  }

  /// <summary>Verifies correlated state and compiler notifications across successful, failed, canceled, and invalid execution requests.</summary>
  /// <param name="scenario">The execution outcome or failure path to simulate.</param>
  [TestMethod]
  [DataRow("success")]
  [DataRow("compiler-failure")]
  [DataRow("compiler-exception")]
  [DataRow("cancel")]
  [DataRow("validation")]
  [DataRow("run-failure")]
  public async Task WorkflowPublishesCorrelatedBuildEventsAtCompilerBoundariesAsync(string scenario)
  {
    var events = new Mock<IIdeEventService>();
    var started = new List<BuildStartedEvent>();
    var builds = new List<BuildCompletedEvent>();
    var states = new List<ExecutionStateChangedEvent>();

    events.Setup(service => service.Publish(It.IsAny<BuildStartedEvent>())).Callback<BuildStartedEvent>(started.Add);
    events.Setup(service => service.Publish(It.IsAny<BuildCompletedEvent>())).Callback<BuildCompletedEvent>(builds.Add);
    events.Setup(service => service.Publish(It.IsAny<ExecutionStateChangedEvent>())).Callback<ExecutionStateChangedEvent>(states.Add);

    var compiler = CreateCompiler(scenario == "compiler-failure" ? 2 : 0);

    if (scenario == "compiler-exception")
    {
      compiler
        .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
        .ThrowsAsync(new IOException("Compiler missing"));
    }

    var runner = new Mock<IProgramRunner>();

    runner
      .Setup(r => r.Run(It.IsAny<string>(), It.IsAny<string>()))
      .Callback(() =>
    {
      Assert.HasCount(1, builds, "Compilation notification must precede launch.");
      throw new InvalidOperationException("Cannot run");
    });

    var request = Request() with { Mode = scenario == "run-failure" ? BuildExecutionMode.Run : BuildExecutionMode.Build };

    if (scenario == "cancel")
      request = request with { SaveSourceAsync = () => Task.FromResult<string?>(null) };

    if (scenario == "validation")
      request = request with { CompilerPath = "" };

    var outcome = await new EditorExecutionCoordinator(compiler.Object, runner.Object, events.Object).ExecuteAsync(request, _ => { });
    Assert.AreEqual(outcome.Stage, states.Last().Stage);
    Assert.IsTrue(states.All(notification => notification.ExecutionId == states[0].ExecutionId));

    if (scenario is "cancel" or "validation")
    {
      Assert.IsEmpty(started);
      Assert.IsEmpty(builds);
      compiler.VerifyNoOtherCalls();
      return;
    }

    Assert.HasCount(1, started);
    Assert.HasCount(1, builds);
    Assert.AreEqual(started[0].ExecutionId, builds[0].ExecutionId);
    Assert.AreEqual(states[0].ExecutionId, builds[0].ExecutionId);
    Assert.AreEqual(SourcePath, builds[0].SourcePath);
    Assert.AreEqual(BuildConstants.DebugConfiguration, builds[0].Configuration);
    Assert.AreEqual(scenario is "success" or "run-failure", builds[0].Succeeded);

    if (scenario == "compiler-exception")
      Assert.AreEqual("Compiler missing", builds[0].ErrorMessage);
  }

  /// <summary>Verifies workspace notifications follow explorer initialization and are omitted when folder selection is canceled.</summary>
  [TestMethod]
  public async Task WorkspaceEventIsPublishedAfterExplorerInitializationAndNotOnCancelAsync()
  {
    var folder = Directory.CreateTempSubdirectory("zero-workspace-events-");
    try
    {
      var events = new Mock<IIdeEventService>();
      var dialogs = new Mock<IEditorDialogService>();

      dialogs.SetupSequence(d => d.OpenFolderAsync()).ReturnsAsync(folder.FullName).ReturnsAsync((string?)null);

      var shell = new MainWindowViewModel(new EditorViewModel(), dialogs.Object, events: events.Object);

      events.Setup(service => service.Publish(It.IsAny<WorkspaceOpenedEvent>())).Callback<WorkspaceOpenedEvent>(notification =>
      {
        Assert.AreEqual(folder.FullName, notification.RootPath);
        Assert.AreEqual(folder.FullName, shell.SolutionExplorer.Roots.Single().Path);
      });

      await ExecuteAsync(shell, shell.CmdOpenFolder);
      await ExecuteAsync(shell, shell.CmdOpenFolder);

      events.Verify(service => service.Publish(It.IsAny<WorkspaceOpenedEvent>()), Times.Once);
    }
    finally
    {
      folder.Delete();
    }
  }

  /// <summary>Creates a mock compiler that returns a completed compilation with the specified exit code.</summary>
  /// <param name="exitCode">The compiler exit code to report.</param>
  /// <returns>A compiler mock configured for non-clean build requests.</returns>
  private static Mock<ICompilerService> CreateCompiler(int exitCode = 0)
  {
    var compiler = new Mock<ICompilerService>();
    compiler
      .Setup(c => c.BuildAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
      .ReturnsAsync(new CompilerResult(exitCode, "compiler output", "program.exe"));
    return compiler;
  }

  /// <summary>Executes a shell command and waits for its busy state to clear within the test timeout.</summary>
  /// <param name="shell">The shell that reports command progress.</param>
  /// <param name="command">The command to execute.</param>
  /// <returns>A task that completes when the command finishes.</returns>
  private static async Task ExecuteAsync(MainWindowViewModel shell, DelegateCommand command)
  {
    var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    shell.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy)
        completed.TrySetResult();
    };

    command.Execute();

    await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
  }

  /// <summary>Creates a build-only Debug request whose save callback returns the test source path.</summary>
  /// <returns>The execution request used to exercise workflow notifications.</returns>
  private static EditorExecutionRequest Request() =>
    new(
      "compiler",
      BuildConstants.DebugConfiguration,
      false,
      BuildExecutionMode.Build,
      () => Task.FromResult<string?>(SourcePath));

  /// <summary>Carries a sequence number for event ordering and buffer-capacity assertions.</summary>
  /// <param name="Number">The sequence number used to identify delivery order and dropped notifications.</param>
  private sealed record TestEvent(int Number) : IdeEvent;
}
