// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Zero.Ide.ViewModels;

namespace Zero.Ide.Models;

/// <summary>A debugger value or scope that can contain registers, locals, or nested members.</summary>
public sealed class DebugValueNode(string name, string value = "", string type = "") : ViewModelBase
{
  private bool _isExpanded = true;
  private string _value = value;

  public ObservableCollection<DebugValueNode> Children { get; } = [];

  public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

  public string Name { get; } = name;

  public string Type { get; } = type;

  public string Value { get => _value; set => SetProperty(ref _value, value); }
}
