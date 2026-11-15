// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using Dock.Model.Mvvm.Controls;
using Zero.Dbg;
using Zero.Ide.Models;

namespace Zero.Ide.ViewModels;

public sealed class DebuggerViewModel : Tool
{
  private readonly DebugValueNode _registerScope = new("Registers");
  private string _location = "";
  private string _status = "Start debugging with F5. Toggle a breakpoint with F9 or click the editor gutter.";

  public DebuggerViewModel()
  {
    Id = "Debugger";
    Title = "Debugger — Locals";
    CanClose = false;
    CanFloat = true;
    CanPin = false;
    Values.Add(_registerScope);
  }

  public string Location { get => _location; private set => SetProperty(ref _location, value); }

  public ObservableCollection<RegisterValue> Registers { get; } = [];
  public string Status { get => _status; private set => SetProperty(ref _status, value); }
  public ObservableCollection<DebugValueNode> Values { get; } = [];

  public void Update(DebugSnapshot snapshot)
  {
    Status = $"{snapshot.State}: {snapshot.Reason}";

    Location = snapshot.State == DebugState.Paused
      ? $"Thread {snapshot.ThreadId} · EIP 0x{snapshot.InstructionPointer:X8}" +
        (snapshot.Location is { } location ? $" · {location.FilePath}:{location.Line}" : " · Outside mapped ZASM source")
      : "";

    Registers.Clear();
    if (snapshot.Registers is not null)
      foreach (var register in snapshot.Registers)
        Registers.Add(register);

    // Update existing nodes in place so stepping retains selection and expansion.
    var values = snapshot.Registers;
    if (values is null)
    {
      _registerScope.Children.Clear();
      return;
    }

    foreach (var stale in _registerScope.Children.Where(node => !values.Any(register => register.Name == node.Name)).ToArray())
      _registerScope.Children.Remove(stale);

    foreach (var register in values)
    {
      var node = _registerScope.Children.FirstOrDefault(child => child.Name == register.Name);
      if (node is null)
        _registerScope.Children.Add(new DebugValueNode(register.Name, register.Hex, "uint32"));
      else
        node.Value = register.Hex;
    }
  }
}
