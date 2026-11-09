// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Zero.Compilation;

/// <summary>Source search paths for a single compilation.</summary>
public sealed class CompilerOptions
{
  public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

  public string CompilerDirectory { get; set; } = AppContext.BaseDirectory;

  public List<string> IncludeDirectories { get; } = [];
}
