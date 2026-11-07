// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Zero.Cli;

internal class Program
{
  public static int Main(string[] args)
  {
    Console.WriteLine("Zero Compiler v{0}", Zero.Version);

    if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
    {
      PrintUsage();
      return 0;
    }

    var run = string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase);
    var inputIndex = run ? 1 : 0;
    var argumentCount = args.Length - inputIndex;

    if (argumentCount < 1 || argumentCount > 2)
    {
      Console.Error.WriteLine("Error: expected a source file and an optional output file.");
      PrintUsage();
      return (int)ErrorCode.InvalidCommandLineArguments;
    }

    var inputPath = args[inputIndex];
    if (!File.Exists(inputPath))
    {
      Console.Error.WriteLine($"Error: file not found: {inputPath}");
      return (int)ErrorCode.SourceFileNotFound;
    }

    var outputPath = argumentCount == 2 ? args[inputIndex + 1] : Path.ChangeExtension(inputPath, ".exe");
    try
    {
      var compiler = new Zero();
      var result = compiler.Compile(inputPath, outputPath, SourceType.Assembly);
      if (result != ErrorCode.None || !run)
        return (int)result;

      // Resolve relative output paths explicitly; inherit the caller's console and directory.
      using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(outputPath))
      {
        UseShellExecute = false,
      });

      if (process is null)
        throw new InvalidOperationException("The executable could not be started.");

      process.WaitForExit();
      return process.ExitCode;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                              ArgumentException or NotSupportedException or
                              Win32Exception or InvalidOperationException)
    {
      Console.Error.WriteLine($"Error: {ex.Message}");
      return (int)ErrorCode.UnexpectedError;
    }
  }

  private static void PrintUsage()
  {
    Console.WriteLine(
      """
      Usage: zcc <source> [output.exe]
             zcc run <source> [output.exe]

      Compile Zero assembly source (.zasm). The output defaults to the source
      path with an .exe extension. Use run to compile and execute the program.
      Execution waits for the program to exit and returns its exit code.

      Commands:
        -h|--help    Show this help.

      Copyright 2003-2026 Xeno Innovations, Inc.
      """);
  }

}
