// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Zero.Ide.Models;

public sealed record IdeSettings
{
  public static IdeSettings Default { get; } = new();

  public string CompilerPath { get; init; } = string.Empty;
  public string FontFamily { get; init; } = "Cascadia Code, Consolas, Menlo, monospace";
  public double FontSize { get; init; } = 14;
  public int IndentationSize { get; init; } = 2;
  public bool ConvertTabsToSpaces { get; init; } = true;
  public bool ShowLineNumbers { get; init; } = true;
  public bool WordWrap { get; init; }

  public void Validate()
  {
    if (CompilerPath.Contains('\r') || CompilerPath.Contains('\n'))
      throw new ArgumentException("The compiler path must be a single line.");

    if (CompilerPath.Length > 0 && (!Path.IsPathFullyQualified(CompilerPath) || !File.Exists(CompilerPath)))
      throw new ArgumentException("Select an existing compiler executable using its full path, or leave the path blank.");

    if (string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Contains('\r') || FontFamily.Contains('\n'))
      throw new ArgumentException("Enter a font family on a single line.");

    if (!double.IsFinite(FontSize) || FontSize < 8 || FontSize > 48)
      throw new ArgumentException("Font size must be between 8 and 48.");

    if (IndentationSize < 1 || IndentationSize > 8)
      throw new ArgumentException("Indentation size must be between 1 and 8.");
  }
}
