// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Zero.Parsing;

namespace Zero.Exceptions;

public class AssemblerException : Exception
{
  public AssemblerException(int lineNumber, string message)
    : base($"Line {lineNumber}: {message}")
  {
    LineNumber = lineNumber;
    Detail = message;
  }

  public AssemblerException(ErrorCode errCode, int lineNumber, string message)
    : this(lineNumber, $"Error {errCode}: {message}")
  {
  }

  public AssemblerException(string message) : base(message)
  {
  }

  public AssemblerException(ErrorCode errCode, string message)
    : base($"Error {errCode}: {message}")
  {
  }

  public int? LineNumber { get; }

  private string Detail { get; } = "";

  internal AssemblerException AtSource(SourceLine source) =>
    new($"{source.FilePath}({source.LineNumber}): {(LineNumber.HasValue ? Detail : Message)}");
}
