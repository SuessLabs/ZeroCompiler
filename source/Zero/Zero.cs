// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Zero;

public class Zero
{
  public const string Version = "0.1.0";

  private string? _inputPath;

  private string? _outputPath;

  public int Compile(string inputPath, string outputPath)
  {
    _inputPath = inputPath;
    _outputPath = outputPath;

    var source = ReadSource(inputPath);
    ParseTokens(source);

    return ErrorCode.None;
  }

  /// <summary>Parse commands and tokenize.</summary>
  /// <param name="source"></param>
  public void ParseTokens(string source)
  {
    var parts = new List<string>();
    var sb = new StringBuilder();

    bool inString = false;
    bool escape = false;

    foreach (char c in source)
    {
      if (inString)
      {
        if (escape)
          escape = false;
        else if (c == '\\')
          escape = true;
        else if (c == '\"')
          inString = false;
      }
      else
      {

        if (c == ';')
        {
          parts.Add(sb.ToString());
          sb.Clear();
        }
        else
        {
          sb.Append(c);
          if (c == '\"')
            inString = true;
        }
      }
    }
  }

  /// <summary>Open file and read source.</summary>
  /// <param name="inputPath">Input file path.</param>
  /// <returns>String of code to read.</returns>
  public string ReadSource(string inputPath)
  {
    return File.ReadAllText(inputPath);
  }
}
