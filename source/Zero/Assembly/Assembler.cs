// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Zero.Core;
using Zero.Exceptions;

namespace Zero.Assembly;

/// <summary>Zero Assembly Language Assembler.</summary>
/// <remarks>r: register, m: memory address, imm: immediate (i.e. a value).</remarks>
public class Assembler
{
  private const char BracketEnd = ']';
  private const char BracketStart = '[';
  private const char ColonChar = ':';
  private const char CommaChar = ',';
  private const char CommentLine1 = ';';
  private const string CommentLine2 = "//";
  private const string DotDll = ".dll";
  private const string HexPrefix = "0x";
  private const string HexSuffix = "h";
  private const char PeriodChar = '.';
  private const char SpaceChar = ' ';
  private const char TabChar = '\t';

  /// <summary>32-bit registers.</summary>
  private static readonly Dictionary<string, int> Registers = new(StringComparer.OrdinalIgnoreCase)
  {
    // 32-bit registers
    ["EAX"] = 0, // Accumulator register
    ["ECX"] = 1, // Count register
    ["EDX"] = 2, // Data register
    ["EBX"] = 3, // Base register
    ["ESP"] = 4, // Stack pointer
    ["EBP"] = 5, // Base pointer
    ["ESI"] = 6, // Source index
    ["EDI"] = 7, // Destination index
    // 16-bit registers
    ["AX"] = 0,
    ["CX"] = 1,
    ["DX"] = 2,
    ["BX"] = 3,
    ["SP"] = 4,
    ["BP"] = 5,
    ["SI"] = 6,
    ["DI"] = 7,
    // 8-bit registers
    ["AL"] = 0,
    ["CL"] = 1,
    ["DL"] = 2,
    ["BL"] = 3,
    ["AH"] = 4,
    ["CH"] = 5,
    ["DH"] = 6,
    ["BH"] = 7,
    // 128-bit registers
    ["XMM0"] = 0,
    ["XMM1"] = 1,
    ["XMM2"] = 2,
    ["XMM3"] = 3,
    ["XMM4"] = 4,
    ["XMM5"] = 5,
    ["XMM6"] = 6,
    ["XMM7"] = 7,
  };

  private readonly List<ImportRequest> _imports = [];
  private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<AsmLine> _lines = [];

  /// <summary>Indicates that the Intel-like mnemonic syntax (ADD EAX, 4) is swapped (ADD 4, EAX).</summary>
  /// <remarks>This is implemented yet.</remarks>
  private readonly bool _swapOperand = false;

  private int _offset;
  public IReadOnlyList<ImportRequest> Imports => _imports;

  /// <summary>Assemble executable.</summary>
  /// <param name="source">Source code lines.</param>
  /// <returns>PE bytes.</returns>
  public byte[]? Assemble(string[] source)
  {
    // Pass 1: Parse, collect labels, imports, compute code size.
    int codeSize = AsmParseAndSize(source);

    // TODO: Support other ELF and others
    // Plan PE layout (including .idata) so we can resolve IAT absolute address
    var pe = new PeWriter();
    var plan = pe.PlanLayout(codeSize, Imports);

    // Pass 2: Emit code, now that extern symbols (IAT_*) have absolute addresses
    var codeBytes = EmitBytes(plan.ExternSymbolMap);

    ////if (HasObjectFile)
    ////{
    ////  var objFile = Path.GetFileNameWithoutExtension(_inputPath);
    ////  objFile += ObjExt;
    ////  File.WriteAllBytes(objFile, code)
    ////}

    var exeBytes = pe.BuildPe32Executable(codeBytes, plan);
    return exeBytes;
  }

  private static int CountDataBytes(List<string> ops, int unitSize) =>
      ops.Count * unitSize;

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
        return 2;

      case Mnemonic.DB: return CountDataBytes(ln.Operands, 1);
      case Mnemonic.DW: return CountDataBytes(ln.Operands, 2);
      case Mnemonic.DD: return CountDataBytes(ln.Operands, 4);

      case Mnemonic.MOV: // MOV r32, imm32 => B8+r + imm32
        return 5;

      case Mnemonic.PUSH:
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: PUSH expects 1 operand");
        return IsRegister(ln.Operands[0]) ? 1 : 5; // reg or imm32

      case Mnemonic.POP:
        return 1;

      case Mnemonic.ADD:
      case Mnemonic.SUB:
      case Mnemonic.CMP:
      case Mnemonic.AND:
      case Mnemonic.OR:
      case Mnemonic.XOR:
        // We emit 81 /ext r + imm32
        // 6:  opcode + ModRM + imm32
        // imm32 form: 81 /ext r + imm32 (6 bytes)
        // reg-reg form: 2 bytes (op + ModRM); estimate worst-case
        return (ln.Operands.Count == 2 && IsRegister(ln.Operands[1])) ? 2 : 6;

      case Mnemonic.TEST:
        // TEST r32, r32 => 2 bytes; TEST r32, imm32 => 6 bytes
        return (ln.Operands.Count == 2 && IsRegister(ln.Operands[1])) ? 2 : 6;

      case Mnemonic.LEA:
        // 8D /r + [ModRM] + optional SIB + disp32
        // Estimate worst-case with disp32 + SIB: 7 bytes
        return 7;

      case Mnemonic.CALL:
        // CALL label => 5; CALL [abs32] => FF 15 + disp32 => 6
        if (ln.Operands.Count != 1)
          throw new AssemblerException($"Line {ln.LineNumber}: CALL expects 1 operand");
        return IsMemoryAbsolute(ln.Operands[0]) ? 6 : 5;

        // Jump to label (label:)
      case Mnemonic.JMP:
        return 5;

      case Mnemonic.JZ:
      case Mnemonic.JNZ:
        return 6; // 0F 84/85 + rel32

      // Short jumps  (.mylabel:)
      case Mnemonic.JZ8:
      case Mnemonic.JNZ8:
        return 2; // 74/75 + rel8

      default:
        throw new AssemblerException($"Line {ln.LineNumber}: Unknown mnemonic '{ln.Mnemonic}'");
    }
  }

  /// <summary>Is token a memory pointer ('[eax]').</summary>
  /// <param name="token">Input token.</param>
  /// <returns>True if it is memory pointer.</returns>
  private static bool IsMemory(string token) =>
    token.Trim().StartsWith(BracketStart) && token.Trim().EndsWith(BracketEnd);

  private static bool IsMemoryAbsolute(string token)
  {
    token = token.Trim();

    // Absolute [imm32] or [symbol]
    return token.StartsWith(BracketStart) && token.EndsWith(BracketEnd) &&
          !token.Contains('+') && !token.Contains("-");
  }

  /// <summary>Is token a register.</summary>
  /// <param name="token"></param>
  /// <returns>True if valid register.</returns>
  private static bool IsRegister(string token) =>
    Registers.ContainsKey(token.Trim().ToUpperInvariant());

  /// <summary>Validates label naming conventions. Must start with a letter and not contain '_' or '.'.</summary>
  /// <param name="line">Line text.</param>
  /// <returns>True when valid.</returns>
  private static bool IsValidLabel(string line)
  {
    if (string.IsNullOrWhiteSpace(line))
      return false;

    // Must start with a letter, not underscore or period (section)
    if (!(char.IsLetter(line[0]) || line[0] == '_' || line[0] == '.'))
      return false;

    // Must contain alpha-numeric only
    foreach (var c in line)
      if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
        return false;

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
      if (s.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        return Convert.ToInt32(s[2..], 16);

      if (s.EndsWith(HexSuffix, StringComparison.OrdinalIgnoreCase))
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

  /// <summary>Scale, Index, Base.</summary>
  /// <param name="scale">Scale value.</param>
  /// <param name="index">Index value.</param>
  /// <param name="bBase">Base value.</param>
  /// <returns></returns>
  private static byte SIB(byte scale, byte index, byte bBase) =>
    (byte)((scale << 6) | ((index & 7) << 3) | (bBase & 7));

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

  /// <summary>Pass 1) Parse and Size. Parse, Collect labels, imports and compute size.</summary>
  /// <param name="source">Source code lines.</param>
  /// <returns>Size computed.</returns>
  /// <exception cref="AssemblerException"><see cref="AssemblerException"/>.</exception>
  private int AsmParseAndSize(string[] source)
  {
    _labels.Clear();
    _lines.Clear();
    _imports.Clear();
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

      // Clean up line for ez-parsing
      line = line.Trim();
      var parsed = ParseLine(line, i + 1);

      // .idata importing externals
      // Non-emitting directives (e.g., IMPORT) don't affect offsets
      if (parsed.Mnemonic.Equals(Keyword.ImportLib, StringComparison.OrdinalIgnoreCase))
      {
        RegisterImport(parsed);
        continue;
      }

      parsed.Offset = _offset;
      parsed.Size = EstimateSize(parsed);
      _offset += parsed.Size;
      _lines.Add(parsed);
    }

    return _offset;

    /*
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
    */
  }

  private byte[] EmitBytes(IReadOnlyDictionary<string, uint> externs)
  {
    var bytes = new List<byte>(_offset);
    foreach (var asmLine in _lines)
    {
      // Keep offsets exact
      while (bytes.Count < asmLine.Offset)
        bytes.Add(0xCC);

      EmitLine(bytes, asmLine, externs);
    }

    return bytes.ToArray();
  }

  /// <summary>Emit line.</summary>
  /// <param name="bytes">Bytes.</param>
  /// <param name="ln">Assembly code line.</param>
  /// <param name="externs">Imported external libraries.</param>
  private void EmitLine(List<byte> bytes, AsmLine ln, IReadOnlyDictionary<string, uint> externs)
  {
    switch (ln.Mnemonic)
    {
      case Mnemonic.NOP: bytes.Add(0x90); break;
      case Mnemonic.RET: bytes.Add(0xC3); break;

      // Call to interrupt (0xCC, 0xCD)
      case Mnemonic.INT:
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: INT expects 1 operand");

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

      // MOV r32, imm32; MOV DEST, SRC;
      case Mnemonic.MOV:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: MOV expects 2 operands");

        //// if (_swapOperand)
        var dst = ln.Operands[0];
        var src = ln.Operands[1];

        if (!IsRegister(dst))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: MOV supports only r32, imm32");

        int r = Registers[dst.ToUpperInvariant()];
        int imm = ResolveValue(src, ln, externs); // allow labels or numbers

        bytes.Add((byte)(0xB8 + r));
        bytes.AddRange(BitConverter.GetBytes(imm));
        break;
      }
      case Mnemonic.PUSH:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: PUSH expects 1 operand");

        var op = ln.Operands[0];
        if (IsRegister(op))
        {
          int r = Registers[op.ToUpperInvariant()];
          bytes.Add((byte)(0x50 + r));
        }
        else
        {
          int imm = ResolveValue(op, ln, externs);
          bytes.Add(0x68);
          bytes.AddRange(BitConverter.GetBytes(imm));
        }

        break;
      }
      case Mnemonic.POP:
      {
        if (ln.Operands.Count != 1 || !IsRegister(ln.Operands[0]))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: POP expects 1 operand (r32)");

        int r = Registers[ln.Operands[0].ToUpperInvariant()];
        bytes.Add((byte)(0x58 + r));
        break;
      }
      case Mnemonic.ADD:
      case Mnemonic.SUB:
      case Mnemonic.CMP:
      case Mnemonic.AND:
      case Mnemonic.OR:
      case Mnemonic.XOR:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: {ln.Mnemonic} expects 2 operands");

        // Zero v0.0.1 (2003) spec says to swap these (i.e. AT&T).. keeping Intel ASM below
        //// if (_swapOperand)
        var dst = ln.Operands[0];
        var src = ln.Operands[1];

        if (!IsRegister(dst))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: {ln.Mnemonic} only supports r32 as first operand");

        int rd = Registers[dst.ToUpperInvariant()];
        if (IsRegister(src))
        {
          // reg, reg => opcode (r/m32, r32) with ModRM(mod=11, reg=src, rm=dst)
          var rs = Registers[src.ToUpperInvariant()];
          byte opcode = ln.Mnemonic switch
          {
            Mnemonic.AND => 0x21,
            Mnemonic.OR => 0x09,
            Mnemonic.XOR => 0x31,
            Mnemonic.ADD => 0x01,
            Mnemonic.SUB => 0x29,
            Mnemonic.CMP => 0x39,
            _ => throw new InvalidOperationException()
          };

          bytes.Add(opcode);
          bytes.Add(ModRM(0b0011, (byte)rs, (byte)rd));
        }
        else
        {
          int imm = ResolveValue(src, ln, externs);
          byte ext = ln.Mnemonic switch
          {
            Mnemonic.ADD => 0,  // /0
            Mnemonic.OR => 1,   // /1
            Mnemonic.ADC => 2,  // (not implemented)
            Mnemonic.SBB => 3,  // (not implemented)
            Mnemonic.AND => 4,  // /4
            Mnemonic.SUB => 5,  // /5
            Mnemonic.XOR => 6,  // /6
            Mnemonic.CMP => 7,  // /7
            _ => throw new InvalidOperationException()
          };

          // 0x81 /ext r + imm32
          bytes.Add(0x81);
          bytes.Add(ModRM(0b0011, ext, (byte)rd));
          bytes.AddRange(BitConverter.GetBytes(imm));
        }

        break;
      }
      case Mnemonic.TEST:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: TEST expects 2 operands");
        var a = ln.Operands[0];
        var b = ln.Operands[1];

        if (!IsRegister(a))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: TEST supports r32 as first operand");

        int ra = Registers[a.ToUpperInvariant()];
        if (IsRegister(b))
        {
          int rb = Registers[b.ToUpperInvariant()];
          bytes.Add(0x85); // TEST r/m32, r32
          bytes.Add(ModRM(0b11, (byte)rb, (byte)ra));
        }
        else
        {
          int imm = ResolveValue(b, ln, externs);
          bytes.Add(0xF7); // TEST r/m32, imm32 => F7 /0
          bytes.Add(ModRM(0b11, 0, (byte)ra));
          bytes.AddRange(BitConverter.GetBytes(imm));
        }

        break;
      }
      case Mnemonic.LEA:
      {
        // LEA r32, [disp32] or [reg + disp32] or [reg] (we always encode with disp32)
        if (ln.Operands.Count != 2)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: LEA expects 2 operands");

        var dst = ln.Operands[0];
        var src = ln.Operands[1];
        if (!IsRegister(dst) || !IsMemory(src))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: LEA syntax: LEA r32, [mem]");

        int rd = Registers[dst.ToUpperInvariant()];

        ParseMemory(src, ln, externs, out bool hasBase, out int baseReg, out int disp32, out bool needsSib);

        bytes.Add(0x8D); // LEA
        if (!hasBase)
        {
          // [disp32] => mod=00, rm=101 + disp32
          bytes.Add(ModRM(0b00, (byte)rd, 0b101));
          bytes.AddRange(BitConverter.GetBytes(disp32));
        }
        else
        {
          // use mod=10 (disp32) to keep it simple
          if (baseReg == 4) // ESP needs SIB
          {
            bytes.Add(ModRM(0b10, (byte)rd, 0b100));
            bytes.Add(SIB(0, 0b100, 0b100)); // scale=1, index=none(100), base=ESP(100)
            bytes.AddRange(BitConverter.GetBytes(disp32));
          }
          else
          {
            bytes.Add(ModRM(0b10, (byte)rd, (byte)baseReg));
            bytes.AddRange(BitConverter.GetBytes(disp32));
          }
        }

        break;
      }
      case Mnemonic.CALL:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: CALL expects 1 operand");
        var op = ln.Operands[0];

        if (IsMemoryAbsolute(op))
        {
          // CALL [abs32] => FF /2 [disp32] => FF 15 dd dd dd dd
          int abs = ResolveMemoryAbsolute(op, ln, externs);
          bytes.Add(0xFF);
          bytes.Add(ModRM(0b00, 0b010, 0b101)); // /2, rm=101 => disp32 follows
          bytes.AddRange(BitConverter.GetBytes(abs));
        }
        else
        {
          // CALL rel32 (label)
          int target = ResolveLabel(op, ln.LineNumber);
          int next = ln.Offset + 5;
          int rel32 = target - next;
          bytes.Add(0xE8);
          bytes.AddRange(BitConverter.GetBytes(rel32));
        }

        break;
      }
      case Mnemonic.JMP:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: JMP expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 5;
        int rel32 = target - next; // relative location

        bytes.Add(0xE9);
        bytes.AddRange(BitConverter.GetBytes(rel32));
        break;
      }
      case Mnemonic.JZ:
      case Mnemonic.JNZ:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: {ln.Mnemonic} expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 6;
        int rel32 = target - next;

        bytes.Add(0x0F);
        bytes.Add(ln.Mnemonic == "JZ" ? (byte)0x84 : (byte)0x85);
        bytes.AddRange(BitConverter.GetBytes(rel32));
        break;
      }

      case Mnemonic.JZ8:
      case Mnemonic.JNZ8:
      {
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: {ln.Mnemonic} expects 1 operand");

        int target = ResolveLabel(ln.Operands[0], ln.LineNumber);
        int next = ln.Offset + 2;
        int rel8 = target - next;

        if (rel8 < -128 || rel8 > 127)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: short jump out of range ({rel8})");

        bytes.Add(ln.Mnemonic == "JZ8" ? (byte)0x74 : (byte)0x75);
        ////bytes.Add((sbyte)rel8);
        bytes.Add((byte)rel8);
        break;
      }

      default:
        throw new AssemblerException(ErrorCode.UnknownSymbol, $"Line {ln.LineNumber}: Unknown mnemonic '{ln.Mnemonic}'");
    }
  }

  private void ParseMemory(string s, AsmLine ln, IReadOnlyDictionary<string, uint> externs,
                           out bool hasBase, out int baseReg, out int disp32, out bool needsSib)
  {
    // Supports:
    //   [disp32]                => no base
    //   [reg] or [reg + imm32]  => base present (always use disp32)
    var inner = s.Trim()[1..^1].Trim();
    hasBase = false;
    baseReg = 0;
    disp32 = 0;
    needsSib = false;

    if (string.IsNullOrWhiteSpace(inner))
      throw new AssemblerException($"Line {ln.LineNumber}: Empty memory operand");

    // reg [+ imm] ?
    var plusIdx = inner.IndexOf('+');
    if (plusIdx >= 0)
    {
      var left = inner[..plusIdx].Trim();
      var right = inner[(plusIdx + 1)..].Trim();
      if (!IsRegister(left))
        throw new AssemblerException($"Line {ln.LineNumber}: Unsupported memory base '{left}'");
      hasBase = true;
      baseReg = Registers[left.ToUpperInvariant()];
      disp32 = ResolveValue(right, ln, externs);
      needsSib = baseReg == 4; // ESP base requires SIB
      return;
    }

    // plain reg OR disp32/symbol
    if (IsRegister(inner))
    {
      hasBase = true;
      baseReg = Registers[inner.ToUpperInvariant()];
      disp32 = 0;
      needsSib = baseReg == 4;
    }
    else
    {
      // disp32 or symbol
      disp32 = ResolveValue(inner, ln, externs);
      hasBase = false;
      needsSib = false;
    }
  }

  /// <summary>Register imports for .idata section.</summary>
  /// <param name="line">Code line.</param>
  /// <exception cref="AssemblerException">Assembler exception.</exception>
  private void RegisterImport(AsmLine line)
  {
    // Syntax: #IMPORT library.dll, Function1[, Func2, ...]
    // At this time, we're not including function arguments (used for intellisense)
    if (line.Operands.Count < 2)
      throw new AssemblerException(ErrorCode.SyntaxErrorImport, $"Line {line.LineNumber}: IMPORT expects 'dll, func[, ...]'");

    var dll = line.Operands[0].Trim();
    if (!dll.Contains(PeriodChar))
      dll += DotDll;

    ////var req = _imports.FirstOrDefault(x => x.Dll.Equals(dll, StringComparison.OrdinalIgnoreCase));
    ImportRequest? req = null;
    foreach (var item in _imports)
    {
      if (item.Dll.Equals(dll, StringComparison.OrdinalIgnoreCase))
      {
        req = item;
        break;
      }
    }

    if (req is null)
    {
      req = new ImportRequest { Dll = dll };
      _imports.Add(req);
    }

    for (int i = 1; i < line.Operands.Count; i++)
    {
      var fun = line.Operands[i].Trim();
      if (string.IsNullOrWhiteSpace(fun))
        continue;

      if (!req.Functions.Contains(fun, StringComparer.OrdinalIgnoreCase))
        req.Functions.Add(fun);
    }
  }

  private int ResolveLabel(string label, int line)
  {
    label = label.Trim();
    if (!_labels.TryGetValue(label, out var addr))
      throw new AssemblerException($"Line {line}: Undefined label '{label}'");

    return addr;
  }

  private int ResolveMemoryAbsolute(string mem, AsmLine ln, IReadOnlyDictionary<string, uint> externs)
  {
    var inner = mem.Trim()[1..^1].Trim();
    return ResolveValue(inner, ln, externs);
  }

  /// <summary>Get numeric value, label offset, or extern symbol offset.</summary>
  /// <param name="token">Input operand.</param>
  /// <param name="ln"><see cref="AsmLine"/>.</param>
  /// <param name="externs">Known externs.</param>
  /// <returns></returns>
  /// <exception cref="AssemblerException"></exception>
  private int ResolveValue(string token, AsmLine ln, IReadOnlyDictionary<string, uint> externs)
  {
    token = token.Trim();

    // Immediate number (decimal, hex only)
    if (char.IsDigit(token[0]) ||
        token.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase) ||
        token.EndsWith(HexSuffix, StringComparison.OrdinalIgnoreCase))
      return ParseInt(token, ln.LineNumber);

    // Label (code offset)
    if (_labels.TryGetValue(token, out var offset))
      return offset;

    // Extern symbol (i.e., IAT_ExitProcess)
    if (externs != null && externs.TryGetValue(token, out var addr))
      return unchecked((int)addr);

    throw new AssemblerException(ErrorCode.UnknownSymbol, $"Line {ln.LineNumber}: Unknown symbol or value '{token}'");
  }
}
