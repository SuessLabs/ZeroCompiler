// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero;

public enum ErrorCode
{
  None = 0,
  InvalidCommandLineArguments,
  SourceFileNotFound,

  AssemblyError,
  UnexpectedError,

  NotImplemented,
  SyntaxErrorImport,

  /// <summary>Unknown mnemonic or symbol.</summary>
  UnknownSymbol,
  SyntaxError,
}
