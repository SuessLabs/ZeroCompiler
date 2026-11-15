// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Dock.Model.Mvvm.Controls;
using Zero.Dbg;

namespace Zero.Ide.ViewModels;

public sealed class DebuggerViewModel : Tool
{
  private string _location = "";
  private string _status = "Start debugging with F5. Toggle a breakpoint with F9 or click the editor gutter.";

  public DebuggerViewModel()
  {
    Id = "Debugger";
    Title = "Debugger — Registers";
    CanClose = false;
    CanFloat = true;
    CanPin = false;
  }

  public string Location { get => _location; private set => SetProperty(ref _location, value); }

  public ObservableCollection<RegisterValue> Registers { get; } = [];

  public string Status { get => _status; private set => SetProperty(ref _status, value); }

  public void Update(DebugSnapshot snapshot)
  {
    Status = $"{snapshot.State}: {snapshot.Reason}";

    Location = snapshot.State == DebugState.Paused
      ? $"Thread {snapshot.ThreadId} · EIP 0x{snapshot.InstructionPointer:X8}" +
        (snapshot.Location is { } location ? $" · {location.FilePath}:{location.Line}" : " · Outside mapped ZASM source")
      : "";

    Registers.Clear();
    if (snapshot.Registers is not null)
      foreach (var register in snapshot.Registers) Registers.Add(register);
  }
}
