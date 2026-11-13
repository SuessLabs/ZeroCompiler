// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Zero.Compilation;
using Zero.Exceptions;
using Zero.Formats;
using Zero.Formats.Elf;
using Zero.Formats.Pe;
using Zero.Parsing;
using Zero.Targets.X86;
using Zero.Targets.X64;

namespace Zero.Assembly;

/// <summary>Coordinates shared parsing, target encoding, and executable writing.</summary>
public class Assembler(CompilationTarget? requestedTarget = null)
{
  public CompilationTarget Target { get; private set; } = CompilationTarget.WindowsX86;
  public IReadOnlyList<ImportRequest> Imports { get; private set; } = [];

  public byte[]? Assemble(string[] source)
  {
    Imports = [];
    Target = AssemblyParser.ResolveTarget(source, requestedTarget);
    IInstructionEncoder encoder;
    IExecutableWriter writer;
    if (Target == CompilationTarget.WindowsX86)
    {
      encoder = new X86Encoder();
      writer = new PeExecutableWriter();
    }
    else if (Target == CompilationTarget.LinuxX64)
    {
      encoder = new X64Encoder();
      writer = new Elf64Writer();
    }
    else
      throw new AssemblerException($"Target '{Target}' ({Target.Format}) is not implemented. Supported targets: windows-x86, linux-x64.");

    var program = AssemblyParser.Parse(source, encoder);
    Imports = program.Imports;
    if (program.CodeSize == 0)
      throw new AssemblerException("No instructions or data were emitted");
    var layout = writer.Plan(program);
    foreach (var label in program.Labels)
    {
      if (!layout.Symbols.TryAdd(label.Key, checked(layout.CodeAddress + (ulong)label.Value)))
        throw new AssemblerException(program.LabelLines[label.Key], $"Label '{label.Key}' conflicts with an imported symbol");
    }

    var code = encoder.Encode(program, layout.Symbols, layout.CodeAddress);
    return writer.Write(code, layout);
  }

  public byte[]? Assemble(IReadOnlyList<SourceLine> source)
  {
    var text = new string[source.Count];
    for (int i = 0; i < source.Count; i++)
      text[i] = source[i].Text;
    try
    {
      return Assemble(text);
    }
    catch (AssemblerException ex) when (ex.LineNumber is > 0 && ex.LineNumber <= source.Count)
    {
      throw ex.AtSource(source[ex.LineNumber.Value - 1]);
    }
  }
}
