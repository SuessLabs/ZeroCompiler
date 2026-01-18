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
  public const string Version = "0.1.0";

  private string? _inputPath;
  private string? _outputPath;

  public ErrorCode Compile(string inputPath, string outputPath, SourceType sourceType)
  {
    _inputPath = inputPath;
    _outputPath = outputPath;

    string[] source = File.ReadAllLines(inputPath);

    if (sourceType == SourceType.Assembly)
    {
      var assembler = new Assembler();
      var code = assembler.Assemble(source);
      var pe = new PeWriter();
      var exeBytes = pe.BuildPe32Executable(code);

      File.WriteAllBytes(outputPath, exeBytes);
      Console.WriteLine($"Compiled {outputPath} ({exeBytes.Length} bytes)");
    }
    else
    {
      throw new NotImplementedException();
    }

    return ErrorCode.None;
  }
}
