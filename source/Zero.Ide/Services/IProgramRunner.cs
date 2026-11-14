// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Ide.Services;

public interface IProgramRunner
{
  void Run(string executablePath, string workingDirectory);
}
