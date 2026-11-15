// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Zero.Ide.Models;

public static class BuildConstants
{
  public const string DebugConfiguration = "Debug";
  public const string LegacyBinaryExtension = ".bin";
  public const string OutputDirectoryName = ".zeroide";
  public const string ReleaseConfiguration = "Release";
  public const string StagingExtension = ".building";
  public const string WindowsExecutableExtension = ".exe";

  public static IReadOnlyList<string> Configurations { get; } =
    Array.AsReadOnly<string>([DebugConfiguration, ReleaseConfiguration]);

  public static bool IsSupportedConfiguration(string configuration) =>
    configuration is DebugConfiguration or ReleaseConfiguration;
}
