// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Zero.Assembly;

namespace Zero;

public enum SourceType
{
  Assembly,
  Zero,
}

public class Zero
{
  public const string Version = "0.0.1";
  private const string ObjExt = ".obj";

  private string? _inputPath;
  private string? _outputPath;

  public static byte[]? CompileAsm(string[] source, bool createObjFile = false)
  {
    var assembler = new Assembler();
    var byteCode = assembler.Assemble(source);

    if (byteCode is null)
      return null;

    if (createObjFile)
    {
      //var objFile = Path.GetFileNameWithoutExtension(_inputPath);
      //objFile += ObjExt;
      //File.WriteAllBytes(objFile, code)
    }

    var pe = new PeWriter();
    var exeBytes = pe.BuildPe32Executable(byteCode);

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
      }
    }
    else
    {
      throw new NotImplementedException();
    }

    return ErrorCode.None;
  }
}
