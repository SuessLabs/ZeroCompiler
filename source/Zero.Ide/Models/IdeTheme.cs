// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Ide.Models;

public enum IdeTheme
{
  System,
  Light,
  Dark,
  CoolSlate,
}

public sealed record ThemeChoice(IdeTheme Value, string Name);
