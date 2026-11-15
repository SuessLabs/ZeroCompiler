// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Ide.Models;

public static class SourceFileExtensions
{
  /// <summary>The Zero assembly file extension (.zasm).</summary>
  public const string Assembly = ".zasm";

  /// <summary>The wildcard pattern for Zero assembly files (*.zasm).</summary>
  public const string AssemblyPattern = "*" + Assembly;

  /// <summary>The Zero header file extension (.zh).</summary>
  public const string Header = ".zh";

  /// <summary>The wildcard pattern for Zero header files (*.zh).</summary>
  public const string HeaderPattern = "*" + Header;

  /// <summary>The Zero source file extension (.zsrc).</summary>
  public const string Source = ".zsrc";

  /// <summary>The wildcard pattern for Zero source files (*.zsrc).</summary>
  public const string SourcePattern = "*" + Source;

  /// <summary>The Zero file extension (.zero).</summary>
  public const string Zero = ".zero";

  /// <summary>The wildcard pattern for Zero files (*.zero).</summary>
  public const string ZeroPattern = "*" + Zero;
}
