// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero;

public enum SourceType
{
  Assembly,
  Zero,
}

public class Zero
{
  public static string Version { get; } = typeof(Zero).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
  private const string ObjExt = ".obj";

  private static ErrorCode _lastError = ErrorCode.None;
  private string? _inputPath;
  private string? _outputPath;

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

  public ErrorCode Compile(string inputPath, string outputPath, SourceType sourceType)
  {
    _inputPath = inputPath;
    _outputPath = outputPath;

    string[] source = File.ReadAllLines(inputPath);

    if (sourceType == SourceType.Assembly)
    {
      byte[]? exeBytes = CompileAsm(source);

      if (exeBytes is not null)
      {
        // Generate EXE
        File.WriteAllBytes(outputPath, exeBytes);
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
