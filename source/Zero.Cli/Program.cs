// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Cli;

internal class Program
{
  public static void Main(string[] args)
  {
    Console.WriteLine("Zero Compiler v{0}", Zero.Version);

    if (!ParseCommands(args))
      PrintUsage();
  }

  private static void CompileCode(string v)
  {
  }

  private static bool ParseCommands(string[] args)
  {
    if (args.Length == 0 || args.Length > 1)
      return false;

    if (args[0] == "-h" || args[0] == "--help")
      return false;

    CompileCode(args[0]);

    return true;
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

      Copyright 2001-2026 Xeno Innovations, Inc.
      """);
  }
}
