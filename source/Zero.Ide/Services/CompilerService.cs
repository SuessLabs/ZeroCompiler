// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Zero.Ide.Services;

public sealed class CompilerService : ICompilerService
{
  public async Task<CompilerResult> BuildAsync(string compilerPath, string sourcePath, string configuration, bool cleanFirst)
  {
    if (string.IsNullOrWhiteSpace(compilerPath) ||
        !Path.IsPathFullyQualified(compilerPath) ||
        !File.Exists(compilerPath))
      throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");

    sourcePath = Path.GetFullPath(sourcePath);
    var outputPath = GetOutputPath(sourcePath, configuration);

    if (!File.Exists(sourcePath))
      throw new FileNotFoundException("Save the source file before building.", sourcePath);

    if (cleanFirst)
      Clean(sourcePath, configuration);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

    // The assembler performs a full compilation; don't leave stale output after failure.
    if (File.Exists(outputPath))
      File.Delete(outputPath);

    var startInfo = new ProcessStartInfo(compilerPath)
    {
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      WorkingDirectory = Path.GetDirectoryName(sourcePath)!,
    };

    // ArgumentList safely handles paths with spaces, quotes, and shell metacharacters.
    startInfo.ArgumentList.Add(sourcePath);
    startInfo.ArgumentList.Add(outputPath);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The Zero compiler could not be started.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();

    await Task.WhenAll(standardOutput, standardError, process.WaitForExitAsync());

    var output = (await standardOutput) + Environment.NewLine + (await standardError);
    var exitCode = process.ExitCode;

    if (exitCode == 0 && !File.Exists(outputPath))
    {
      exitCode = -1;
      output += Environment.NewLine + "The compiler reported success but did not produce an output file.";
    }

    if (exitCode != 0 && File.Exists(outputPath))
      File.Delete(outputPath);

    return new CompilerResult(exitCode, output.Trim(), outputPath);
  }

  public void Clean(string sourcePath, string configuration)
  {
    // Delete only the known artifact for this source/configuration, never a source directory.
    var outputPath = GetOutputPath(sourcePath, configuration);
    if (File.Exists(outputPath)) File.Delete(outputPath);
  }

  /// <summary>Gets binary output path for specified source file.</summary>
  /// <param name="sourcePath"></param>
  /// <param name="configuration"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  private static string GetOutputPath(string sourcePath, string configuration)
  {
    if (configuration is not ("Debug" or "Release"))
      throw new ArgumentException("Choose Debug or Release.");

    sourcePath = Path.GetFullPath(sourcePath);

    if (!string.Equals(Path.GetExtension(sourcePath), ".zasm", StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException("Only .zasm source files can be built.");

    // A target-neutral extension works for both PE and ELF output selected by #TARGET.
    return Path.Combine(
      Path.GetDirectoryName(sourcePath)!,
      ".zeroide",
      configuration,
      Path.GetFileName(sourcePath),
      Path.GetFileNameWithoutExtension(sourcePath) + ".bin");
  }
}
