// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Zero.Ide.Services;

public sealed record CompilerResult(int ExitCode, string Output, string OutputPath)
{
  public bool Succeeded => ExitCode == 0;
}

public interface ICompilerService
{
  Task<CompilerResult> BuildAsync(string compilerPath, string sourcePath, string configuration, bool cleanFirst);

  void Clean(string sourcePath, string configuration);
}
