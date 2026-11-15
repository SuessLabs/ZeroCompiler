// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Zero.Compilation;
using Zero.Dbg;
using Zero.Exceptions;
using Zero.Formats;
using Zero.Formats.Elf;
using Zero.Formats.Pe;
using Zero.Parsing;
using Zero.Targets.X64;
using Zero.Targets.X86;

namespace Zero.Assembly;

/// <summary>Coordinates shared parsing, target encoding, and executable writing.</summary>
public class Assembler(CompilationTarget? requestedTarget = null)
{
  public DebugMap DebugMap { get; private set; } = new();

  public bool EmitDebugInformation { get; init; }

  public IReadOnlyList<ImportRequest> Imports { get; private set; } = [];

  public CompilationTarget Target { get; private set; } = CompilationTarget.WindowsX86;

  public byte[]? Assemble(string[] source)
  {
    Imports = [];
    DebugMap = new DebugMap();
    source = AssemblySyntax.StripComments(source);
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
    if (EmitDebugInformation)
    {
      DebugMap.Target = Target.ToString();
      DebugMap.PreferredImageBase = layout.ImageBase;
      DebugMap.EntryRva = checked((uint)(layout.CodeAddress - layout.ImageBase));

      foreach (var line in program.Lines)
      {
        if (line.Mnemonic is Mnemonic.DB or Mnemonic.DW or Mnemonic.DD or Mnemonic.DQ)
          continue;

        DebugMap.Instructions.Add(new DebugInstruction
        {
          Line = line.LineNumber,
          Rva = checked((uint)(layout.CodeAddress - layout.ImageBase + (ulong)line.Offset)),
          Size = line.Size,
          IsCall = line.Mnemonic == Mnemonic.CALL,
        });
      }
    }

    return writer.Write(code, layout);
  }

  public byte[]? Assemble(IReadOnlyList<SourceLine> source)
  {
    var text = new string[source.Count];
    for (int i = 0; i < source.Count; i++)
      text[i] = source[i].Text;

    try
    {
      var bytes = Assemble(text);
      if (!EmitDebugInformation)
        return bytes;

      var files = new HashSet<string>(System.OperatingSystem.IsWindows() ? System.StringComparer.OrdinalIgnoreCase : System.StringComparer.Ordinal);

      foreach (var line in source)
      {
        var path = System.IO.Path.GetFullPath(line.FilePath);
        if (files.Add(path))
          DebugMap.Sources.Add(new DebugSource { FilePath = path, Hash = DebugMap.HashFile(path) });
      }

      foreach (var instruction in DebugMap.Instructions)
      {
        var line = source[instruction.Line - 1];
        instruction.FilePath = System.IO.Path.GetFullPath(line.FilePath);
        instruction.Line = line.LineNumber;
      }

      return bytes;
    }
    catch (AssemblerException ex) when (ex.LineNumber is > 0 && ex.LineNumber <= source.Count)
    {
      throw ex.AtSource(source[ex.LineNumber.Value - 1]);
    }
  }
}
