// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

namespace Zero.Ide.Services;

public sealed class ProgramRunner : IProgramRunner
{
  public void Run(string executablePath, string workingDirectory)
  {
    executablePath = Path.GetFullPath(executablePath);
    var target = ExecutableInspector.ReadTarget(executablePath);

    if (!target.CanRunOnCurrentHost)
      throw new InvalidOperationException($"The build succeeded, but {target} cannot run on this host. Run it on a compatible operating system and architecture.");

    // Start the executable directly; don't use file associations.
    using var process = Process.Start(new ProcessStartInfo(executablePath)
    {
      UseShellExecute = false,
      CreateNoWindow = false,
      WorkingDirectory = workingDirectory,
    }) ?? throw new InvalidOperationException("The compiled program could not be started.");
    // Disposing our process handle does not stop the child. Keep the IDE editable.
  }
}
