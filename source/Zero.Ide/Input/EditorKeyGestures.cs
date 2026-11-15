// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Avalonia.Input;

namespace Zero.Ide.Input;

public static class EditorKeyGestures
{
  public static KeyGesture Build { get; } = new(Key.B, KeyModifiers.Control);
  public static KeyGesture BuildAndRun { get; } = new(Key.F5);
  public static KeyGesture CloseDocument { get; } = new(Key.W, KeyModifiers.Control);
  public static KeyGesture NewFile { get; } = new(Key.N, KeyModifiers.Control);
  public static KeyGesture OpenFile { get; } = new(Key.O, KeyModifiers.Control);
  public static KeyGesture Rebuild { get; } = new(Key.B, KeyModifiers.Control | KeyModifiers.Shift);
  public static KeyGesture Save { get; } = new(Key.S, KeyModifiers.Control);
  public static KeyGesture SaveAll { get; } = new(Key.S, KeyModifiers.Control | KeyModifiers.Alt);
  public static KeyGesture SaveAs { get; } = new(Key.S, KeyModifiers.Control | KeyModifiers.Shift);
}
