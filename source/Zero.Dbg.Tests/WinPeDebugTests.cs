// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Zero.Dbg;
using Zero.Dbg.WinPe;

namespace Zero.Dbg.Tests;

[TestClass]
public class WinPeDebugTests
{
  [TestMethod]
  public async Task BreakpointInHeaderResolvesOriginalFileAndLineAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("""
      #IMPORT kernel32.dll, ExitProcess
      jmp start
      using "helper.zh"
      start: call helper
      push 0
      call [ExitProcess]
      """, "// header\nhelper: mov eax, 42\nret");

    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options() with { Breakpoints = [new(fixture.Header, 2)] })
                 .WaitAsync(TimeSpan.FromSeconds(10));

    await PausedAsync(events);

    session.Continue();
    var stopped = await PausedAsync(events);

    Assert.AreEqual(fixture.Header, stopped.Location!.FilePath);
    Assert.AreEqual(2, stopped.Location.Line);

    await session.StopAsync()
                 .WaitAsync(TimeSpan.FromSeconds(10));
  }

  [TestMethod]
  public async Task BreakpointsRearmAcrossLoopIterationsAndCanBeRemovedAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("""
      #IMPORT kernel32.dll, ExitProcess
      mov eax, 0
      again: add eax, 1
      cmp eax, 3
      jnz again
      push 0
      call [ExitProcess]
      """);

    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options(3)).WaitAsync(TimeSpan.FromSeconds(10));
    await PausedAsync(events);

    for (uint i = 0; i < 2; i++)
    {
      session.Continue();
      var stopped = await PausedAsync(events);
      Assert.AreEqual(3, stopped.Location!.Line);
      Assert.AreEqual(i, Register(stopped, "EAX"));
    }

    session.SetBreakpoints([]);
    session.Continue();
    Assert.AreEqual(0u, (await NextAsync(events, DebugState.Exited)).ExitCode);
  }

  [TestMethod]
  public async Task ContinuingAfterStepOntoBreakpointDoesNotStopTwiceAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("#IMPORT kernel32.dll, ExitProcess\nmov eax, 42\npush 0\ncall [ExitProcess]");
    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options(3)).WaitAsync(TimeSpan.FromSeconds(10));
    await PausedAsync(events);

    session.StepOver();

    Assert.AreEqual(3, (await PausedAsync(events)).Location!.Line);

    session.Continue();
    var next = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    Assert.AreEqual(DebugState.Running, next.State);

    next = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    Assert.AreEqual(DebugState.Exited, next.State, next.Reason);
  }

  [TestMethod]
  public async Task RunningProgramCanBePausedAndStoppedAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("loop: jmp loop");
    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options()).WaitAsync(TimeSpan.FromSeconds(10));
    await PausedAsync(events);
    session.Continue();

    await NextAsync(events, DebugState.Running);
    session.Pause();

    Assert.AreEqual(DebugState.Paused, (await PausedAsync(events)).State);

    await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
    Assert.AreEqual(DebugState.Exited, session.Snapshot.State);
  }

  [TestMethod]
  public async Task StepOverExecutesInstructionsAndSkipsInternalCallsAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("""
      #IMPORT kernel32.dll, ExitProcess
      mov eax, 1
      call helper
      add eax, 3
      push 0
      call [ExitProcess]
      helper: add eax, 2
      ret
      """);

    await using var session = new WinPeDebugSession();
    var events = Watch(session);
    await session.LaunchAsync(fixture.Options()).WaitAsync(TimeSpan.FromSeconds(10));

    Assert.AreEqual(2, (await PausedAsync(events)).Location!.Line);

    session.StepOver();
    var atCall = await PausedAsync(events);

    Assert.AreEqual(3, atCall.Location!.Line);
    Assert.AreEqual(1u, Register(atCall, "EAX"));

    session.StepOver();
    var afterCall = await PausedAsync(events);

    Assert.AreEqual(4, afterCall.Location!.Line);
    Assert.AreEqual(3u, Register(afterCall, "EAX"));

    session.StepOver();
    var afterAdd = await PausedAsync(events);

    Assert.AreEqual(5, afterAdd.Location!.Line);
    Assert.AreEqual(6u, Register(afterAdd, "EAX"));

    session.Continue();

    Assert.AreEqual(0u, (await NextAsync(events, DebugState.Exited)).ExitCode);
  }

  [TestMethod]
  public async Task StepOverImportedStdcallReturnsToCallerAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("""
      #IMPORT kernel32.dll, Sleep, ExitProcess
      push 0
      call [Sleep]
      mov eax, 42
      push 0
      call [ExitProcess]
      """);

    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options(4)).WaitAsync(TimeSpan.FromSeconds(10));
    await PausedAsync(events);

    session.StepOver();
    Assert.AreEqual(3, (await PausedAsync(events)).Location!.Line);

    session.StepOver();
    Assert.AreEqual(4, (await PausedAsync(events)).Location!.Line);

    session.Continue();
    Assert.AreEqual(0u, (await NextAsync(events, DebugState.Exited)).ExitCode);
  }

  [TestMethod]
  public async Task UserBreakpointInsideCallInterruptsStepOverAsync()
  {
    RequireWindows();
    using var fixture = new DebugFixture("""
      #IMPORT kernel32.dll, ExitProcess
      call helper
      push 0
      call [ExitProcess]
      helper: mov eax, 42
      ret
      """);

    await using var session = new WinPeDebugSession();
    var events = Watch(session);

    await session.LaunchAsync(fixture.Options(5)).WaitAsync(TimeSpan.FromSeconds(10));
    await PausedAsync(events);

    session.StepOver();
    Assert.AreEqual(5, (await PausedAsync(events)).Location!.Line);

    session.Continue();
    Assert.AreEqual(3, (await PausedAsync(events)).Location!.Line);

    await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
  }

  private static async Task<DebugSnapshot> NextAsync(Channel<DebugSnapshot> events, DebugState state)
  {
    while (true)
    {
      var snapshot = await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
      Assert.AreNotEqual(DebugState.Faulted, snapshot.State, snapshot.Reason);

      if (snapshot.State == state)
        return snapshot;

      if (snapshot.State == DebugState.Exited)
        Assert.Fail(snapshot.Reason);
    }
  }

  private static Task<DebugSnapshot> PausedAsync(Channel<DebugSnapshot> events) => NextAsync(events, DebugState.Paused);

  private static uint Register(DebugSnapshot snapshot, string name)
  {
    foreach (var register in snapshot.Registers!)
      if (register.Name == name)
        return register.Value;

    throw new InvalidOperationException($"Register {name} not found.");
  }

  private static void RequireWindows()
  {
    if (!WinPeDebugSession.IsSupported)
      Assert.Inconclusive("Native Windows x64 is required to debug PE32/WOW64 programs.");
  }

  private static Channel<DebugSnapshot> Watch(IDebugSession session)
  {
    var events = Channel.CreateUnbounded<DebugSnapshot>();
    session.Changed += (_, snapshot) => events.Writer.TryWrite(snapshot);

    return events;
  }
}
