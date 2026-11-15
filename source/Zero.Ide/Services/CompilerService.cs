// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Zero.Compilation;
using Zero.Dbg;
using Zero.Ide.Models;

namespace Zero.Ide.Services;

public sealed class CompilerService : ICompilerService
{
  public async Task<CompilerResult> BuildAsync(string compilerPath, string sourcePath, string configuration, bool cleanFirst)
  {
    if (string.IsNullOrWhiteSpace(compilerPath) ||
        !Path.IsPathFullyQualified(compilerPath) ||
        !File.Exists(compilerPath))
    {
      throw new InvalidOperationException("Select the Zero CLI executable in Tools > Options > Compiler before building.");
    }

    sourcePath = Path.GetFullPath(sourcePath);
    var outputBasePath = GetOutputBasePath(sourcePath, configuration);

    if (!File.Exists(sourcePath))
      throw new FileNotFoundException("Save the source file before building.", sourcePath);

    if (cleanFirst)
      Clean(sourcePath, configuration);

    Directory.CreateDirectory(Path.GetDirectoryName(outputBasePath)!);
    var stagingPath = outputBasePath + "." + Guid.NewGuid().ToString("N") + BuildConstants.StagingExtension;
    var published = false;

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
    startInfo.ArgumentList.Add(stagingPath);

    if (configuration == BuildConstants.DebugConfiguration)
      startInfo.ArgumentList.Add("--debug");

    try
    {
      using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("The Zero compiler could not be started.");

      var standardOutput = process.StandardOutput.ReadToEndAsync();
      var standardError = process.StandardError.ReadToEndAsync();

      await Task.WhenAll(standardOutput, standardError, process.WaitForExitAsync());

      var output = (await standardOutput) + Environment.NewLine + (await standardError);
      var exitCode = process.ExitCode;
      var outputPath = outputBasePath;

      if (exitCode == 0 && !File.Exists(stagingPath))
      {
        exitCode = -1;
        output += Environment.NewLine + "The compiler reported success but did not produce an output file.";
      }

      if (exitCode == 0)
      {
        // Read the generated format, so cross-compilation and included #TARGET directives work.
        var target = ExecutableInspector.ReadTarget(stagingPath);

        if (target.Format == ExecutableFormat.Pe)
          outputPath += BuildConstants.WindowsExecutableExtension;

        RemoveArtifacts(outputBasePath);
        File.Move(stagingPath, outputPath, overwrite: true);

        if (File.Exists(stagingPath + DebugMap.Extension))
          File.Move(stagingPath + DebugMap.Extension, outputPath + DebugMap.Extension, overwrite: true);

        published = true;
        output = output.Replace(stagingPath, outputPath, StringComparison.Ordinal);
      }

      return new CompilerResult(exitCode, output.Trim(), outputPath);
    }
    finally
    {
      if (File.Exists(stagingPath))
        File.Delete(stagingPath);

      if (File.Exists(stagingPath + DebugMap.Extension))
        File.Delete(stagingPath + DebugMap.Extension);

      // A failed build must not leave an older executable that looks like a fresh result.
      if (!published)
        RemoveArtifacts(outputBasePath);
    }
  }

  public void Clean(string sourcePath, string configuration)
  {
    // Delete only the known artifact for this source/configuration, never a source directory.
    RemoveArtifacts(GetOutputBasePath(sourcePath, configuration));
  }

  private static string GetOutputBasePath(string sourcePath, string configuration)
  {
    if (!BuildConstants.IsSupportedConfiguration(configuration))
      throw new ArgumentException("Choose Debug or Release.");

    sourcePath = Path.GetFullPath(sourcePath);

    if (!string.Equals(Path.GetExtension(sourcePath), SourceFileExtensions.Assembly, StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException($"Only {SourceFileExtensions.Assembly} source files can be built.");

    return Path.Combine(
      Path.GetDirectoryName(sourcePath)!,
      BuildConstants.OutputDirectoryName,
      configuration,
      Path.GetFileName(sourcePath),
      Path.GetFileNameWithoutExtension(sourcePath));
  }

  private static void RemoveArtifacts(string outputBasePath)
  {
    foreach (var path in new[]
    {
      outputBasePath,
      outputBasePath + BuildConstants.WindowsExecutableExtension,
      outputBasePath + BuildConstants.LegacyBinaryExtension,
    })
    {
      if (File.Exists(path))
        File.Delete(path);

      if (File.Exists(path + DebugMap.Extension))
        File.Delete(path + DebugMap.Extension);
    }
  }
}
