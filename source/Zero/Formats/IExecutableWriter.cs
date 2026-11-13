// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Zero.Assembly;

namespace Zero.Formats;

internal sealed class ExecutableLayout(ulong codeAddress)
{
  public ulong CodeAddress { get; } = codeAddress;
  public Dictionary<string, ulong> Symbols { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal interface IExecutableWriter
{
  ExecutableLayout Plan(AssemblyProgram program);
  byte[] Write(byte[] code, ExecutableLayout layout);
}
