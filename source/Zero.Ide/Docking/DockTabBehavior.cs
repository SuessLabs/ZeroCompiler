// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia.Input;
using Avalonia.Interactivity;
using Dock.Avalonia.Controls;

namespace Zero.Ide.Docking;

internal static class DockTabBehavior
{
  private static bool _registered;

  public static void Register()
  {
    if (_registered)
      return;

    _registered = true;

    InputElement.PointerPressedEvent.AddClassHandler<DocumentTabStripItem>((tab, e) =>
    {
      if (e.GetCurrentPoint(tab).Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed)
        return;

      e.Handled = true;

      if (tab.DataContext is EditorDocument document)
        document.Factory?.CloseDockable(document);
    }, RoutingStrategies.Tunnel);
  }
}
