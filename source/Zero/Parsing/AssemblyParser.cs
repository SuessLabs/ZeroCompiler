// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using Zero.Assembly;
using Zero.Compilation;
using Zero.Core;
using Zero.Exceptions;

namespace Zero.Parsing;

/// <summary>Shared syntax and symbol collection for all executable backends.</summary>
internal static class AssemblyParser
{
  internal static string StripComment(string text, int line)
  {
    int comment = AssemblySyntax.FindUnquoted(text, Constants.CommentLine1, line);
    return (comment < 0 ? text : text[..comment]).Trim();
  }

  internal static CompilationTarget ResolveTarget(string[] source, CompilationTarget? requested)
  {
    CompilationTarget? declared = null;
    for (int i = 0; i < source.Length; i++)
    {
      var text = StripComment(source[i], i + 1);
      var instruction = ReadLine(text, i + 1);
      if (instruction.Mnemonic != Keyword.Target)
        continue;
      if (instruction.Operands.Count != 1)
        throw new AssemblerException(i + 1, "#TARGET expects one target name");
      CompilationTarget target;
      try
      {
        target = CompilationTarget.Parse(instruction.Operands[0]);
      }
      catch (ArgumentException ex)
      {
        throw new AssemblerException(i + 1, ex.Message);
      }

      if ((requested is not null && requested != target) || (declared is not null && declared != target))
        throw new AssemblerException(i + 1, "#TARGET conflicts with the requested or previously declared target");
      declared = target;
    }

    return requested ?? declared ?? CompilationTarget.WindowsX86;
  }

  public static AssemblyProgram Parse(string[] source, IInstructionEncoder encoder)
  {
    var program = new AssemblyProgram();
    for (int i = 0; i < source.Length; i++)
    {
      var text = StripComment(source[i], i + 1);
      if (text.Length == 0)
        continue;

      int colon = AssemblySyntax.FindUnquoted(text, Constants.ColonChar, i + 1);
      if (colon >= 0)
      {
        AddLabel(text[..colon].Trim(), i + 1);
        text = text[(colon + 1)..].Trim();
        if (text.Length == 0)
          continue;
      }

      int space = text.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);
      if (space >= 0)
      {
        var tail = text[(space + 1)..].TrimStart();
        int end = tail.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);
        var directive = end < 0 ? tail : tail[..end];
        if (directive.Equals(Mnemonic.DB, StringComparison.OrdinalIgnoreCase))
        {
          AddLabel(text[..space], i + 1);
          text = tail;
        }
      }

      var line = ReadLine(text, i + 1);
      if (line.Mnemonic == Keyword.Target)
      {
        if (colon >= 0)
          throw new AssemblerException(i + 1, "#TARGET must be a standalone statement");
        continue;
      }

      if (line.Mnemonic == Keyword.ImportLib)
      {
        if (line.Operands.Count < 2)
          throw new AssemblerException(i + 1, "IMPORT expects 'dll, func[, ...]'");
        program.FirstImportLine ??= i + 1;
        var library = line.Operands[0];
        if (!library.Contains('.'))
          library += ".dll";
        ImportRequest? request = null;
        foreach (var item in program.Imports)
        {
          if (item.Dll.Equals(library, StringComparison.OrdinalIgnoreCase))
            request = item;
        }

        if (request is null)
        {
          request = new ImportRequest { Dll = library };
          program.Imports.Add(request);
        }

        for (int operand = 1; operand < line.Operands.Count; operand++)
        {
          var function = line.Operands[operand];
          if (!request.Functions.Contains(function, StringComparer.OrdinalIgnoreCase))
            request.Functions.Add(function);
        }

        continue;
      }

      line.Offset = program.CodeSize;
      line.Size = encoder.GetSize(line);
      program.CodeSize = checked(program.CodeSize + line.Size);
      program.Lines.Add(line);
    }

    return program;

    void AddLabel(string name, int number)
    {
      if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] is '_' or '.') || IsRegisterName(name))
        throw new AssemblerException(number, $"Invalid label '{name}'");
      foreach (char character in name)
      {
        if (!(char.IsLetterOrDigit(character) || character is '_' or '.'))
          throw new AssemblerException(number, $"Invalid label '{name}'");
      }

      if (!program.Labels.TryAdd(name, program.CodeSize))
        throw new AssemblerException(number, $"Duplicate label '{name}'");
      program.LabelLines.Add(name, number);
    }
  }

  private static AsmLine ReadLine(string text, int number)
  {
    int space = text.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);
    return new AsmLine
    {
      LineNumber = number,
      Mnemonic = (space < 0 ? text : text[..space]).ToUpperInvariant(),
      Operands = AssemblySyntax.SplitOperands(space < 0 ? "" : text[(space + 1)..], number),
    };
  }

  private static bool IsRegisterName(string name)
  {
    name = name.ToUpperInvariant();
    if (name is "RIP" or "RAX" or "RBX" or "RCX" or "RDX" or "RSP" or "RBP" or "RSI" or "RDI" or
      "EAX" or "EBX" or "ECX" or "EDX" or "ESP" or "EBP" or "ESI" or "EDI" or
      "AX" or "BX" or "CX" or "DX" or "SP" or "BP" or "SI" or "DI" or
      "AL" or "AH" or "BL" or "BH" or "CL" or "CH" or "DL" or "DH")
      return true;
    for (int i = 0; i < 16; i++)
    {
      if (name == $"R{i}" || name == $"R{i}D" || name == $"XMM{i}")
        return true;
    }

    return false;
  }
}
