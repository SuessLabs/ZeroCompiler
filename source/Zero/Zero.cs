// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Zero.Assembly;
using Zero.Compilation;
using Zero.Dbg;
using Zero.Exceptions;
using Zero.Parsing;

namespace Zero;

public class Zero
{
  private static ErrorCode _lastError = ErrorCode.None;

  public static string Version { get; } = typeof(Zero)
    .Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

  public string? OutputPath { get; private set; }

  public CompilationTarget? Target { get; private set; }

  public static byte[]? CompileAsm(string[] source)
  {
    ErrorCode errorCode = ErrorCode.None;
    byte[]? exeBytes = null;

    try
    {
      var assembler = new Assembler();
      ////assembler.HasObjectFiles = false; // for linking & debugging
      exeBytes = assembler.Assemble(source);
    }
    catch (AssemblerException ex)
    {
      Console.Error.WriteLine($"Assembly error: {ex.Message}");
      errorCode = ErrorCode.AssemblyError;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Unexpected error: {ex.Message}");
      errorCode = ErrorCode.UnexpectedError;
    }

    _lastError = errorCode;
    return exeBytes;
  }

  public ErrorCode Compile(string inputPath, string? outputPath, SourceType sourceType, CompilerOptions? options = null)
  {
    OutputPath = null;
    Target = null;

    if (sourceType == SourceType.Assembly)
    {
      byte[]? exeBytes;
      DebugMap? debugMap = null;

      try
      {
        var loader = new SourceLoader();
        var source = loader.Load(inputPath, options);
        var assembler = new Assembler(options?.Target) { EmitDebugInformation = options?.EmitDebugInformation == true };

        exeBytes = assembler.Assemble(source);

        if (options?.EmitDebugInformation == true)
        {
          debugMap = assembler.DebugMap;
          var mappedFiles = new System.Collections.Generic.HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

          foreach (var file in debugMap.Sources)
            mappedFiles.Add(file.FilePath);

          foreach (var file in loader.SourceFiles)
            if (mappedFiles.Add(file)) debugMap.Sources.Add(new DebugSource { FilePath = file, Hash = DebugMap.HashFile(file) });
        }

        Target = assembler.Target;
      }
      catch (AssemblerException ex)
      {
        Console.Error.WriteLine($"Assembly error: {ex.Message}");
        return ErrorCode.AssemblyError;
      }

      if (exeBytes is not null)
      {
        outputPath ??= Target == CompilationTarget.LinuxX64 ? Path.ChangeExtension(inputPath, null) : Path.ChangeExtension(inputPath, ".exe");
        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
          throw new IOException("The output path must differ from the input source path.");

        File.WriteAllBytes(outputPath, exeBytes);
        var mapPath = outputPath + DebugMap.Extension;

        if (debugMap is not null)
        {
          debugMap.ExecutableHash = DebugMap.HashFile(outputPath);
          debugMap.Save(mapPath);
        }
        else if (File.Exists(mapPath))
        {
          File.Delete(mapPath);
        }

        if (Target == CompilationTarget.LinuxX64 && OperatingSystem.IsLinux())
        {
          File.SetUnixFileMode(
            outputPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        OutputPath = outputPath;
        Console.WriteLine($"Compiled {outputPath} ({exeBytes.Length} bytes)");
      }
      else
      {
        Console.WriteLine($"Compilation Errors, exiting.");
        return ErrorCode.AssemblyError;
      }
    }
    else
    {
      _lastError = ErrorCode.NotImplemented;
      throw new NotImplementedException();
    }

    return ErrorCode.None;
  }
}
