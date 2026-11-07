// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
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

    var inputPath = args[0];
    if (!File.Exists(inputPath))
    {
      Console.Error.WriteLine($"Error: file not found: {inputPath}");
      return (int)ErrorCode.SourceFileNotFound;
    }

    var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".exe");

    var errCode = CompileCode(inputPath, outputPath);

    return errCode;
  }

  private static int CompileCode(string inputPath, string outputPath)
  {
    var check = SanitizeInput(inputPath, outputPath);
    if (check != 0)
      return check;

    var zero = new Zero();
    var ret = zero.Compile(inputPath, outputPath, SourceType.Assembly);

    return (int)ret;
  }

  private static void PrintUsage()
  {
    Console.WriteLine(
      """
      Usage: zero [code (.z, .zasm, .zsrc, .zero)]
      usage: zero [commands]

      Code:
        Source code of application

      Commands:
        -h|--help

      Copyright 2003-2026 Xeno Innovations, Inc.
      """);
  }

  private static int SanitizeInput(string inputPath, string outputFile)
  {
    if (!File.Exists(inputPath))
    {
      Console.Error.WriteLine($"Error: File not found: {inputPath}");
      return 2;
    }

    return 0;
  }
}
