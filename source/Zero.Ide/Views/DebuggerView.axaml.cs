// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Zero.Ide.Models;

namespace Zero.Ide.Views;

public partial class DebuggerView : UserControl
{
  public DebuggerView() => InitializeComponent();

  private async void OnCopyEntryAsync(object? sender, RoutedEventArgs e)
  {
    if (sender is MenuItem { Tag: string text } && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
      await clipboard.SetTextAsync(text);
  }

  private async void OnValuesKeyDownAsync(object? sender, KeyEventArgs e)
  {
    var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
    if (e.Key != Key.C || e.KeyModifiers != modifier || ValuesTree.SelectedItem is not DebugValueNode node)
      return;

    if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
    {
      e.Handled = true;
      await clipboard.SetTextAsync(node.Value);
    }
  }
}
