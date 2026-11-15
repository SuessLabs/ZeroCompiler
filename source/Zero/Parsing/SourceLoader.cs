// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Zero.Compilation;
using Zero.Exceptions;

namespace Zero.Parsing;

/// <summary>Expands using directives while preserving original source locations.</summary>
public sealed class SourceLoader
{
  public IReadOnlyList<string> SourceFiles { get; private set; } = [];

  public IReadOnlyList<SourceLine> Load(string inputPath, CompilerOptions? options = null)
  {
    options ??= new CompilerOptions();

    var resolver = new IncludeResolver(options);
    var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    var included = new HashSet<string>(comparer);
    var active = new List<string>();
    var lines = new List<SourceLine>();
    var sourceFiles = new List<string>();

    SourceFiles = sourceFiles;

    Expand(Path.GetFullPath(inputPath, Path.GetFullPath(options.WorkingDirectory)), null);

    return lines;

    void Expand(string path, SourceLine? requester)
    {
      // Test the active chain first: an include-once rule must not hide cycles.
      foreach (var ancestor in active)
      {
        if (comparer.Equals(ancestor, path))
          throw new AssemblerException($"{requester!.FilePath}({requester.LineNumber}): Circular include: {string.Join(" -> ", active)} -> {path}");
      }

      if (!included.Add(path))
        return;

      sourceFiles.Add(path);

      active.Add(path);
      string[] source;

      try
      {
        source = File.ReadAllLines(path);
      }
      catch (Exception ex) when (requester is not null && ex is IOException or UnauthorizedAccessException)
      {
        throw new AssemblerException($"{requester.FilePath}({requester.LineNumber}): Cannot read include '{path}': {ex.Message}");
      }

      try
      {
        source = AssemblySyntax.StripComments(source);
      }
      catch (AssemblerException ex) when (ex.LineNumber is > 0)
      {
        throw ex.AtSource(new SourceLine(source[ex.LineNumber.Value - 1], path, ex.LineNumber.Value));
      }

      for (int i = 0; i < source.Length; i++)
      {
        var line = new SourceLine(source[i], path, i + 1);
        string? name;

        try
        {
          name = ReadDirective(line.Text, line.LineNumber);
        }
        catch (AssemblerException ex)
        {
          throw ex.AtSource(line);
        }

        if (name is null)
          lines.Add(line);
        else
          Expand(resolver.Resolve(name, line), line);
      }

      active.RemoveAt(active.Count - 1);
    }
  }

  private static string? ReadDirective(string text, int lineNumber)
  {
    text = text.Trim();
    if (!text.Equals(Constants.UsingKeyword, StringComparison.OrdinalIgnoreCase) &&
        !(text.Length > Constants.UsingKeyword.Length &&
          text.StartsWith(Constants.UsingKeyword, StringComparison.OrdinalIgnoreCase) &&
          char.IsWhiteSpace(text[Constants.UsingKeyword.Length])))
    {
      return null;
    }

    var operand = text[Constants.UsingKeyword.Length..].Trim();

    if (operand.Length < 2 || operand[0] != Constants.DoubleQuote)
      throw new AssemblerException(lineNumber, "USING expects a double-quoted filename");

    var name = AssemblySyntax.ReadString(operand, lineNumber);

    if (string.IsNullOrWhiteSpace(name))
      throw new AssemblerException(lineNumber, "USING filename cannot be empty");

    return name;
  }
}
