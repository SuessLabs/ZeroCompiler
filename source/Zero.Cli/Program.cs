// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Zero.Compilation;

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
    var options = new CompilerOptions();
    var positional = new List<string>();

    for (int i = inputIndex; i < args.Length; i++)
    {
      if (args[i] is "-I" or "--include")
      {
        if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith('-'))
        {
          Console.Error.WriteLine("Error: -I/--include requires an include directory.");
          return (int)ErrorCode.InvalidCommandLineArguments;
        }

        options.IncludeDirectories.Add(args[++i]);
      }
      else if (args[i] == "--target")
      {
        if (i + 1 >= args.Length)
        {
          Console.Error.WriteLine("Error: --target requires windows-x86 or linux-x64.");
          return (int)ErrorCode.InvalidCommandLineArguments;
        }

        try
        {
          var target = CompilationTarget.Parse(args[++i]);
          if (options.Target is not null && options.Target != target)
            throw new ArgumentException("Conflicting --target options.");
          options.Target = target;
        }
        catch (ArgumentException ex)
        {
          Console.Error.WriteLine($"Error: {ex.Message}");
          return (int)ErrorCode.InvalidCommandLineArguments;
        }
      }
      else if (args[i].StartsWith('-'))
      {
        Console.Error.WriteLine($"Error: unknown option: {args[i]}");
        return (int)ErrorCode.InvalidCommandLineArguments;
      }
      else
      { 
        positional.Add(args[i]);
      }
    }

    var argumentCount = positional.Count;

    // Verify that we have a source file and an optional output file.
    if (argumentCount < 1 || argumentCount > 2)
    {
      Console.Error.WriteLine("Error: expected a source file and an optional output file.");
      PrintUsage();
      return (int)ErrorCode.InvalidCommandLineArguments;
    }

    // Verify that the source file exists.
    var inputPath = positional[0];
    if (!File.Exists(inputPath))
    {
      Console.Error.WriteLine($"Error: file not found: {inputPath}");
      return (int)ErrorCode.SourceFileNotFound;
    }

    var outputPath = argumentCount == 2 ? positional[1] : null;

    try
    {
      // Start Your Engines! Compile the source file(s)
      var compiler = new Zero();
      var result = compiler.Compile(inputPath, outputPath, SourceType.Assembly, options);

      if (result != ErrorCode.None || !run)
        return (int)result;

      if (compiler.Target?.CanRunOnCurrentHost != true)
      {
        Console.Error.WriteLine($"Error: compiled {compiler.Target} to '{compiler.OutputPath}', but this host cannot run that target. Run Linux output in Linux or WSL.");
        return (int)ErrorCode.UnexpectedError;
      }

      // Resolve relative output paths explicitly; inherit the caller's console and directory.
      using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(compiler.OutputPath!))
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
      Usage: zero <source> [output] [--target <target>] [-I <directory>]...
             zero run <source> [output] [--target <target>] [-I <directory>]...

      Compile Zero assembly source (.zasm). Targets: windows-x86 (PE32, default)
      and linux-x64 (static ELF64, Linux syscalls). A source #TARGET directive
      may select the target; an explicit --target must agree with it.
      Default output: .exe for Windows, no extension for Linux.
      Use run to compile and execute on a compatible host.
      Execution waits for the program to exit and returns its exit code.

      Commands:
        -h|--help    Show this help.
        -I|--include <directory>    Add an include search directory (repeatable).
        --target <target>          Select windows-x86 or linux-x64.

      Includes search the current working directory, then -I directories in order,
      then the include folder beside the compiler. The same options work with zero.

      Copyright 2003-2026 Xeno Innovations, Inc.
      """);
  }
}
