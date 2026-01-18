// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Zero.Exceptions;

namespace Zero.Assembly;

/// <summary>Zero Assembly Language Assembler.</summary>
/// <remarks>r: register, m: memory address, imm: immediate (i.e. a value).</remarks>
public class Assembler
{
  private const char ColonChar = ':';
  private const char CommaChar = ',';
  private const char CommentLine1 = ';';
  private const string CommentLine2 = "//";
  private const char SpaceChar = ' ';
  private const char TabChar = '\t';

  /// <summary>32-bit registers.</summary>
  private static readonly Dictionary<string, int> Reg32 = new(StringComparer.OrdinalIgnoreCase)
  {
    ["EAX"] = 0,
    ["ECX"] = 1,
    ["EDX"] = 2,
    ["EBX"] = 3,
    ["ESP"] = 4,
    ["EBP"] = 5,
    ["ESI"] = 6,
    ["EDI"] = 7,
  };

  private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<AsmLine> _lines = new();
  private int _offset;

  public byte[] Assemble(string[] source)
  {
    _labels.Clear();
    _lines.Clear();
    _offset = 0;

    // 1st-Pass: Parse and size
    for (int i = 0; i < source.Length; i++)
    {
      var raw = source[i];
      var line = StripComment(raw);

      if (string.IsNullOrWhiteSpace(line))
        continue;

      // Labels: can be on their own line or prefixed before instruction
      int colon = line.IndexOf(ColonChar);
      if (colon >= 0)
      {
        var label = line[..colon].Trim();

        if (!IsValidLabel(label))
          throw new AssemblerException($"Invalid label '{label}' at line {i + 1}");

        if (_labels.ContainsKey(label))
          throw new AssemblerException($"Duplicate label '{label}' at line {i + 1}");

        _labels[label] = _offset;
        line = line[(colon + 1)..].Trim();

        if (string.IsNullOrEmpty(line))
          continue;
      }

      line = line.Trim();
      var parsed = ParseLine(line, i + 1);
      parsed.Offset = _offset;
      parsed.Size = EstimateSize(parsed);

      _offset += parsed.Size;
      _lines.Add(parsed);
    }

    // Pass 2: Emit bytes
    var output = new List<byte>(_offset);
    foreach (var ln in _lines)
    {
      // Maintain the exact offset for accurate rel32
      while (output.Count < ln.Offset)
        output.Add(0xCC); // Shouldn't happen

      EmitLine(output, ln);
    }

    return output.ToArray();
  }

  private static int CountDataBytes(List<string> ops, int unitSize)
  {
    if (ops.Count == 0)
      return 0;

    return ops.Count * unitSize;
  }

  /// <summary>Basic size estimation for two-pass assembly.</summary>
  /// <param name="ln">Assembly line.</param>
  /// <returns>Size.</returns>
  private static int EstimateSize(AsmLine ln)
  {
    switch (ln.Mnemonic)
    {
      case Mnemonic.NOP:
      case Mnemonic.RET:
        return 1;

      case Mnemonic.INT:
        return 2;  // CD ib

      case Mnemonic.DB:
        return CountDataBytes(ln.Operands, 1);

      case Mnemonic.DW:
        return CountDataBytes(ln.Operands, 2);

      case Mnemonic.DD:
        return CountDataBytes(ln.Operands, 4);

      case Mnemonic.MOV: // MOV r32, imm32 => B8+r + imm32
        return 5;

      case Mnemonic.PUSH:
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: PUSH expects 1 operand");
        return IsRegister32(ln.Operands[0]) ? 1 : 5; // reg or imm32

      case Mnemonic.POP:
        return 1; // POP r32

      case Mnemonic.ADD:
      case Mnemonic.SUB:
      case Mnemonic.CMP:
        // We emit 81 /ext r + imm32
        return 6; // opcode + ModRM + imm32

      case Mnemonic.JMP:
      case Mnemonic.CALL:
        return 5; // E9/E8 + rel32

      case Mnemonic.JZ:
      case Mnemonic.JNZ:
        return 6; // 0F 84/85 + rel32

      default:
        throw new AssemblerException($"Line {ln.LineNumber}: Unknown mnemonic '{ln.Mnemonic}'");
    }
  }

  private static bool IsRegister32(string s) =>
      Reg32.ContainsKey(s.Trim().ToUpperInvariant());

  private static bool IsValidLabel(string line)
  {
    if (string.IsNullOrWhiteSpace(line))
      return false;

    if (!(char.IsLetter(line[0]) || line[0] == '_' || line[0] == '.'))
      return false;

    foreach (var c in line)
    {
      if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        return false;
    }

    return true;
  }

  private static byte ModRM(byte mod, byte register, byte rm)
  {
    // return (byte)((mod << 6) | ((register & 7) << 3) | (rm & 7));
    return (byte)((mod << 0b0110) | ((register & 0b0111) << 0b0011) | (rm & 0b0111));
  }

  private static int ParseInt(string s, int lineNumber)
  {
    s = s.Trim();
    try
    {
      if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return Convert.ToInt32(s[2..], 16);

      if (s.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        return Convert.ToInt32(s[..^1], 16);

      return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
    catch
    {
      throw new AssemblerException($"Line {lineNumber}: Invalid number '{s}'");
    }
  }

  /// <summary>Parse the line number, assembly mnemonic, and any following data.</summary>
  /// <param name="line">Source code line.</param>
  /// <param name="lineNum">Source line number.</param>
  /// <returns>Assembly line parsed.</returns>
  private static AsmLine ParseLine(string line, int lineNum)
  {
    // Split mnemonic and operands (space or tab characters)
    var firstSpace = line.IndexOfAny([SpaceChar, TabChar]);

    string mnemonic, rest;
    if (firstSpace < 0)
    {
      // Whole line is the mnemonic
      mnemonic = line;
      rest = string.Empty;
    }
    else
    {
      mnemonic = line[..firstSpace];
      rest = line[(firstSpace + 1)..];
    }

    mnemonic = mnemonic.Trim().ToUpperInvariant();
    var ops = SplitOperands(rest);

    return new AsmLine
    {
      LineNumber = lineNum,
      Mnemonic = mnemonic,
      Operands = ops,
    };
  }

  /// <summary>Parse line's operands after the mnemonic.</summary>
  /// <param name="s">Source code.</param>
  /// <returns>Parsed data.</returns>
  private static List<string> SplitOperands(string s)
  {
    var list = new List<string>();
    if (string.IsNullOrWhiteSpace(s))
      return list;

    var parts = s.Split(CommaChar);
    foreach (var p in parts)
    {
      var t = p.Trim();
      if (!string.IsNullOrEmpty(t))
        list.Add(t);
    }

    return list;
  }

  /// <summary>Strip comment from source code line.</summary>
  /// <param name="line">Source code line.</param>
  /// <returns>Line without code comment.</returns>
  private static string StripComment(string line)
  {
    var idx = line.IndexOf(CommentLine1);
    return idx >= 0 ? line[..idx] : line;
  }

  /// <summary>Emit line.</summary>
  /// <param name="bytes">Bytes.</param>
  /// <param name="ln">Assembly code line.</param>
  private void EmitLine(List<byte> bytes, AsmLine ln)
  {
    switch (ln.Mnemonic)
    {
      case Mnemonic.NOP: bytes.Add(0x90); break;
      case Mnemonic.RET: bytes.Add(0xC3); break;

      // Call to interrupt (0xCC, 0xCD)
      case Mnemonic.INT:
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: INT expects 1 operand");
        bytes.Add(0xCD);
        bytes.Add((byte)(ParseInt(ln.Operands[0], ln.LineNumber) & 0xFF));
        break;

      // Define byte
      case Mnemonic.DB:
        foreach (var op in ln.Operands)
        {
          var v = ParseInt(op, ln.LineNumber);
          bytes.Add((byte)(v & 0xFF));
        }

        break;

      case Mnemonic.DW:
        foreach (var op in ln.Operands)
        {
          var v = ParseInt(op, ln.LineNumber);
          bytes.Add((byte)(v & 0xFF));
          bytes.Add((byte)((v >> 8) & 0xFF));
        }

        break;

      case Mnemonic.DD:
        foreach (var op in ln.Operands)
        {
          var v = ParseInt(op, ln.LineNumber);
          bytes.AddRange(BitConverter.GetBytes(v));
        }

        break;

      // MOV r32, imm32
      case Mnemonic.MOV:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException($"Line {ln.LineNumber}: MOV expects 2 operands");

        var dst = ln.Operands[0];
        var src = ln.Operands[1];

        if (!IsRegister32(dst))
          throw new AssemblerException($"Line {ln.LineNumber}: MOV supports only r32, imm32");

        int r = Reg32[dst.ToUpperInvariant()];
        int imm = ParseInt(src, ln.LineNumber);
        bytes.Add((byte)(0xB8 + r));
        bytes.AddRange(BitConverter.GetBytes(imm));
        break;
      }
      case Mnemonic.PUSH:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: PUSH expects 1 operand");

        var op = ln.Operands[0];
        if (IsRegister32(op))
        {
          int r = Reg32[op.ToUpperInvariant()];
          bytes.Add((byte)(0x50 + r));
        }
        else
        {
          int imm = ParseInt(op, ln.LineNumber);
          bytes.Add(0x68);
          bytes.AddRange(BitConverter.GetBytes(imm));
        }

        break;
      }
      case Mnemonic.POP:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: POP expects 1 operand");

        var op = ln.Operands[0];
        if (!IsRegister32(op))
          throw new AssemblerException($"Line {ln.LineNumber}: POP supports only r32");

        int r = Reg32[op.ToUpperInvariant()];
        bytes.Add((byte)(0x58 + r));
        break;
      }
      case Mnemonic.ADD:
      case Mnemonic.SUB:
      case Mnemonic.CMP:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException($"Line {ln.LineNumber}: {ln.Mnemonic} expects 2 operands");

        // Zero v0.1 (2003) spec says to swap these.. keeping traditional ASM below
        var dst = ln.Operands[0];
        var src = ln.Operands[1];
        if (!IsRegister32(dst))
          throw new AssemblerException($"Line {ln.LineNumber}: {ln.Mnemonic} supports r32, imm32");

        int r = Reg32[dst.ToUpperInvariant()];
        int imm = ParseInt(src, ln.LineNumber);

        // 0x81 /ext r + imm32 (mod=11, rm=r)
        bytes.Add(0x81);
        byte ext = ln.Mnemonic switch
        {
          Mnemonic.ADD => 0,
          Mnemonic.SUB => 5,
          Mnemonic.CMP => 7,
          _ => throw new InvalidOperationException()
        };

        bytes.Add(ModRM(0b0011, ext, (byte)r));
        bytes.AddRange(BitConverter.GetBytes(imm));
        break;
      }
      case Mnemonic.JMP:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: JMP expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 5;
        int rel32 = target - next; // relative location

        bytes.Add(0xE9);
        bytes.AddRange(BitConverter.GetBytes(rel32));
        break;
      }
      case Mnemonic.CALL:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: CALL expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 5;
        int rel32 = target - next;

        bytes.Add(0xE8);
        bytes.AddRange(BitConverter.GetBytes(rel32));
        break;
      }
      case Mnemonic.JZ:
      case Mnemonic.JNZ:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: {ln.Mnemonic} expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 6;
        int rel32 = target - next;

        bytes.Add(0x0F);
        bytes.Add(ln.Mnemonic == "JZ" ? (byte)0x84 : (byte)0x85);
        bytes.AddRange(BitConverter.GetBytes(rel32));
        break;
      }
      default:
        throw new AssemblerException($"Line {ln.LineNumber}: Unknown mnemonic '{ln.Mnemonic}'");
    }
  }

  private int ResolveLabel(string label, int line)
  {
    label = label.Trim();
    if (!_labels.TryGetValue(label, out var addr))
      throw new AssemblerException($"Line {line}: Undefined label '{label}'");

    return addr;
  }
}
