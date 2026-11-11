// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Zero.Assembly;

namespace Zero.Compilation;

internal interface IInstructionEncoder
{
  int GetSize(AsmLine line);
  byte[] Encode(AssemblyProgram program, IReadOnlyDictionary<string, ulong> addresses, ulong codeAddress);
}
