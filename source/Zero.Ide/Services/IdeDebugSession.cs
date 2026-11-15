// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Zero.Dbg;
using Zero.Dbg.WinPe;

namespace Zero.Ide.Services;

/// <summary>Keeps native debug events off the UI thread and marshals immutable snapshots back to it.</summary>
public sealed class IdeDebugSession : IDebugSession
{
  private readonly WinPeDebugSession _session = new();

  public IdeDebugSession() => _session.Changed += OnChanged;

  public event EventHandler<DebugSnapshot>? Changed;

  public DebugSnapshot Snapshot => _session.Snapshot;

  public void Continue() => _session.Continue();

  public async ValueTask DisposeAsync()
  {
    _session.Changed -= OnChanged;
    await _session.DisposeAsync();
  }

  public Task LaunchAsync(DebugLaunchOptions options) =>
    _session.LaunchAsync(options);

  public void Pause() => _session.Pause();

  public void SetBreakpoints(IReadOnlyList<SourceBreakpoint> breakpoints) =>
    _session.SetBreakpoints(breakpoints);

  public void StepOver() => _session.StepOver();

  public Task StopAsync() => _session.StopAsync();

  private void OnChanged(object? sender, DebugSnapshot snapshot) =>
    Dispatcher.UIThread.Post(() => Changed?.Invoke(this, snapshot));
}
