// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Formats.Pe;

internal sealed class PeExecutableWriter : IExecutableWriter
{
  private readonly PeWriter _writer = new();
  private PeWriter.PePlan _plan = null!;

  public ExecutableLayout Plan(AssemblyProgram program)
  {
    foreach (var import in program.Imports)
    {
      if (!import.Dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        throw new AssemblerException(program.FirstImportLine ?? 1, "Windows PE imports require DLLs; Linux shared-library linking is not implemented");
    }

    _plan = _writer.PlanLayout(program.CodeSize, program.Imports);
    var layout = new ExecutableLayout((ulong)_plan.ImageBase + _plan.TextRva);
    foreach (var symbol in _plan.ExternSymbolMap)
      layout.Symbols.Add(symbol.Key, symbol.Value);
    return layout;
  }

  public byte[] Write(byte[] code, ExecutableLayout layout) => _writer.BuildPe32Executable(code, _plan);
}
