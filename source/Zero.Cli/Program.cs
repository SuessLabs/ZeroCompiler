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
    var errCode = CompileCode(inputPath, "test.exe");

    return errCode;
  }

  private static int CompileCode(string inputPath, string outputPath)
  {
    var check = SanitizeInput(inputPath, outputPath);
    if (check != 0)
      return check;

    var ret = CompileCode(inputPath, outputPath);
    return ret;
  }

  private static void PrintUsage()
  {
    Console.WriteLine(
      """
      Usage: zero [App-Code (.z, .zero)]
      usage: zero [Commands]

      App-Code:
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
