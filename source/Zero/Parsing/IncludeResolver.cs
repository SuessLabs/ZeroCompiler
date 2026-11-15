// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Zero.Compilation;
using Zero.Exceptions;

namespace Zero.Parsing;

internal sealed class IncludeResolver
{
  private readonly List<string> _roots = [];
  private readonly string _workingDirectory;

  public IncludeResolver(CompilerOptions options)
  {
    _workingDirectory = Path.GetFullPath(options.WorkingDirectory);
    _roots.Add(_workingDirectory);

    foreach (var directory in options.IncludeDirectories)
      _roots.Add(Path.GetFullPath(directory, _workingDirectory));

    var compilerDirectory = Path.GetFullPath(options.CompilerDirectory, _workingDirectory);
    _roots.Add(Path.Combine(compilerDirectory, "include"));

    // Installed distributions keep the executable in Zero/bin and headers in Zero/include.
    if (string.Equals(
      Path.GetFileName(Path.TrimEndingDirectorySeparator(compilerDirectory)),
      "bin",
      StringComparison.OrdinalIgnoreCase))
    {
      _roots.Add(Path.GetFullPath(Path.Combine(compilerDirectory, "..", "include")));
    }
  }

  public string Resolve(string name, SourceLine requester)
  {
    var searched = new List<string>();
    if (Path.IsPathRooted(name))
    {
      var path = Path.GetFullPath(name, _workingDirectory);
      if (File.Exists(path))
        return path;
      searched.Add(path);
    }
    else
    {
      foreach (var root in _roots)
      {
        var path = Path.GetFullPath(Path.Combine(root, name));
        if (File.Exists(path))
          return path;
        searched.Add(path);
      }
    }

    throw new AssemblerException($"{requester.FilePath}({requester.LineNumber}): Include file '{name}' not found. Searched:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", searched)}");
  }
}
