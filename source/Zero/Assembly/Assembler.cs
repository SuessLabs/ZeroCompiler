// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Zero.Core;
using Zero.Exceptions;
using Zero.Parsing;

namespace Zero.Assembly;

/// <summary>Zero Assembly Language Assembler.</summary>
/// <remarks>r: register, m: memory address, imm: immediate (i.e. a value).</remarks>
public class Assembler
{
  /// <summary>32-bit registers.</summary>
  private static readonly Dictionary<string, int> Registers = new(StringComparer.OrdinalIgnoreCase)
  {
    // 64-bit registers
    [Register.RAX] = 0,
    [Register.RCX] = 1,
    [Register.RDX] = 2,
    [Register.RBX] = 3,
    [Register.RSP] = 4,
    [Register.RBP] = 5,
    [Register.RSI] = 6,
    [Register.RDI] = 7,
    // 32-bit registers
    [Register.EAX] = 0, // Accumulator register
    [Register.ECX] = 1, // Count register
    [Register.EDX] = 2, // Data register
    [Register.EBX] = 3, // Base register
    [Register.ESP] = 4, // Stack pointer
    [Register.EBP] = 5, // Base pointer
    [Register.ESI] = 6, // Source index
    [Register.EDI] = 7, // Destination index
    // 16-bit registers
    [Register.AX] = 0,
    [Register.CX] = 1,
    [Register.DX] = 2,
    [Register.BX] = 3,
    [Register.SP] = 4,
    [Register.BP] = 5,
    [Register.SI] = 6,
    [Register.DI] = 7,
    // 8-bit registers
    [Register.AL] = 0,
    [Register.CL] = 1,
    [Register.DL] = 2,
    [Register.BL] = 3,
    [Register.AH] = 4,
    [Register.CH] = 5,
    [Register.DH] = 6,
    [Register.BH] = 7,
    // 128-bit registers
    [Register.XMM0] = 0,
    [Register.XMM1] = 1,
    [Register.XMM2] = 2,
    [Register.XMM3] = 3,
    [Register.XMM4] = 4,
    [Register.XMM5] = 5,
    [Register.XMM6] = 6,
    [Register.XMM7] = 7,
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

    // Address operands need virtual addresses; jumps still use section offsets.
    var symbols = new Dictionary<string, uint>(plan.ExternSymbolMap, StringComparer.OrdinalIgnoreCase);
    foreach (var (label, offset) in _labels)
    {
      if (!symbols.TryAdd(label, plan.ImageBase + plan.TextRva + (uint)offset))
        throw new AssemblerException($"Label '{label}' conflicts with an imported symbol");
    }

    // Pass 2: Emit code with resolved addresses for local and imported symbols.
    var codeBytes = EmitBytes(symbols);

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

  private static byte[] GetDefinedBytes(AsmLine line)
  {
    if (line.Operands.Count == 0)
      throw new AssemblerException($"Line {line.LineNumber}: DB expects at least one operand");

    var bytes = new List<byte>();
    foreach (var operand in line.Operands)
    {
      if (operand[0] is Constants.SingleQuote or Constants.DoubleQuote)
        bytes.AddRange(Encoding.UTF8.GetBytes(AssemblySyntax.ReadString(operand, line.LineNumber)));
      else
        bytes.Add((byte)(ParseInt(operand, line.LineNumber) & 0xFF));
    }

    return bytes.ToArray();
  }

  private static bool IsRegister32(string token) => token.Trim().ToUpperInvariant() is
    Register.EAX or Register.ECX or Register.EDX or Register.EBX or
    Register.ESP or Register.EBP or Register.ESI or Register.EDI;

  private static int MemoryInstructionSize(string operand, int lineNumber)
  {
    if (!IsMemory(operand))
      throw new AssemblerException($"Line {lineNumber}: Expected [mem]");

    var inner = operand.Trim()[1..^1].Trim();
    int plus = inner.IndexOf(Constants.PlusChar);
    var memoryBase = (plus < 0 ? inner : inner[..plus]).Trim();
    return memoryBase.Equals(Register.ESP, StringComparison.OrdinalIgnoreCase) ? 7 : 6;
  }

  private static ushort ReturnStackBytes(AsmLine line)
  {
    if (line.Operands.Count > 1)
      throw new AssemblerException($"Line {line.LineNumber}: RET expects zero or one operand");

    int bytes = line.Operands.Count == 0 ? 0 : ParseInt(line.Operands[0], line.LineNumber);
    if (bytes < 0 || bytes > ushort.MaxValue)
      throw new AssemblerException($"Line {line.LineNumber}: RET stack size must be between 0 and 65535");
    return (ushort)bytes;
  }

  /// <summary>Basic size estimation for two-pass assembly.</summary>
  /// <param name="ln">Assembly line.</param>
  /// <returns>Size.</returns>
  private static int EstimateSize(AsmLine ln)
  {
    switch (ln.Mnemonic)
    {
      case Mnemonic.NOP:
        return 1;

      case Mnemonic.RET:
        _ = ReturnStackBytes(ln);
        return ln.Operands.Count == 0 ? 1 : 3;

      case Mnemonic.INT:
        return 2;

      case Mnemonic.DB: return GetDefinedBytes(ln).Length;
      case Mnemonic.DW: return CountDataBytes(ln.Operands, 2);
      case Mnemonic.DD: return CountDataBytes(ln.Operands, 4);

      case Mnemonic.MOV:
        if (ln.Operands.Count != 2 || !IsRegister32(ln.Operands[0]))
          throw new AssemblerException($"Line {ln.LineNumber}: MOV expects r32, imm32 or r32, [mem]");
        return IsMemory(ln.Operands[1]) ? MemoryInstructionSize(ln.Operands[1], ln.LineNumber) : 5;

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
      {
        if (ln.Operands.Count != 2 || !IsMemory(ln.Operands[1]))
          throw new AssemblerException($"Line {ln.LineNumber}: LEA expects a register and [mem]");

        return MemoryInstructionSize(ln.Operands[1], ln.LineNumber);
      }

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
    token.Trim().StartsWith(Constants.BracketStart) && token.Trim().EndsWith(Constants.BracketEnd);

  private static bool IsMemoryAbsolute(string token)
  {
    token = token.Trim();

    // Absolute [imm32] or [symbol]
    return token.StartsWith(Constants.BracketStart) && token.EndsWith(Constants.BracketEnd) &&
          !token.Contains(Constants.PlusChar) && !token.Contains(Constants.MinusChar);
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
    if (!(char.IsLetter(line[0]) || line[0] == Constants.UnderscoreChar || line[0] == Constants.PeriodChar))
      return false;

    // Must contain alpha-numeric only
    foreach (var c in line)
      if (!(char.IsLetterOrDigit(c) || c == Constants.UnderscoreChar || c == Constants.PeriodChar))
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
      if (s.StartsWith(Constants.HexPrefix, StringComparison.OrdinalIgnoreCase))
        return Convert.ToInt32(s[2..], 16);

      if (s.EndsWith(Constants.HexSuffix, StringComparison.OrdinalIgnoreCase))
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
    var firstSpace = line.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);

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
    var ops = AssemblySyntax.SplitOperands(rest, lineNum);

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
  private static byte ScaleIndexBase(byte scale, byte index, byte bBase) =>
    (byte)((scale << 6) | ((index & 7) << 3) | (bBase & 7));

  /// <summary>Strip comment from source code line.</summary>
  /// <param name="line">Source code line.</param>
  /// <returns>Line without code comment.</returns>
  private static string StripComment(string line, int lineNumber)
  {
    var idx = AssemblySyntax.FindUnquoted(line, Constants.CommentLine1, lineNumber);
    return idx >= 0 ? line[..idx] : line;
  }

  private void RegisterLabel(string label, int lineNumber)
  {
    if (!IsValidLabel(label) || IsRegister(label))
      throw new AssemblerException($"Line {lineNumber}: Invalid label '{label}'");

    if (!_labels.TryAdd(label, _offset))
      throw new AssemblerException($"Line {lineNumber}: Duplicate label '{label}'");
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
      var line = StripComment(raw, i + 1);

      if (string.IsNullOrWhiteSpace(line))
        continue;

      // Labels: can be on their own line or prefixed before instruction
      int colon = AssemblySyntax.FindUnquoted(line, Constants.ColonChar, i + 1);
      if (colon >= 0)
      {
        var label = line[..colon].Trim();

        RegisterLabel(label, i + 1);
        line = line[(colon + 1)..].Trim();

        if (string.IsNullOrEmpty(line))
          continue;
      }

      // Clean up line for ez-parsing
      line = line.Trim();

      // A byte declaration can name its data without a colon: message db 'Hi', 0.
      int firstSpace = line.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);
      if (firstSpace >= 0)
      {
        var declaration = line[(firstSpace + 1)..].TrimStart();
        int directiveEnd = declaration.IndexOfAny([Constants.SpaceChar, Constants.TabChar]);
        var directive = directiveEnd < 0 ? declaration : declaration[..directiveEnd];

        if (directive.Equals(Mnemonic.DB, StringComparison.OrdinalIgnoreCase))
        {
          RegisterLabel(line[..firstSpace], i + 1);
          line = declaration;
        }
      }

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
      case Mnemonic.RET:
        if (ln.Operands.Count == 0)
          bytes.Add(0xC3);
        else
        {
          bytes.Add(0xC2);
          bytes.AddRange(BitConverter.GetBytes(ReturnStackBytes(ln)));
        }

        break;

      // Call to interrupt (0xCC, 0xCD)
      case Mnemonic.INT:
        if (ln.Operands.Count != 1)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: INT expects 1 operand");

        bytes.Add(0xCD);
        bytes.Add((byte)(ParseInt(ln.Operands[0], ln.LineNumber) & 0xFF));
        break;

      // Define byte
      case Mnemonic.DB:
        bytes.AddRange(GetDefinedBytes(ln));
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

      // MOV r32, imm32 or MOV r32, [mem].
      case Mnemonic.MOV:
      {
        if (ln.Operands.Count != 2)
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: MOV expects 2 operands");

        //// if (_swapOperand)
        var dst = ln.Operands[0];
        var src = ln.Operands[1];

        if (!IsRegister32(dst))
          throw new AssemblerException(ErrorCode.SyntaxError, $"Line {ln.LineNumber}: MOV destination must be a 32-bit register");

        int r = Registers[dst.ToUpperInvariant()];
        if (IsMemory(src))
        {
          EmitMemoryInstruction(bytes, 0x8B, r, src, ln, externs);
          break;
        }

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

        EmitMemoryInstruction(bytes, 0x8D, rd, src, ln, externs);
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
        bytes.Add(ln.Mnemonic == Mnemonic.JZ ? (byte)0x84 : (byte)0x85);
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

        bytes.Add(ln.Mnemonic == Mnemonic.JZ8 ? (byte)0x74 : (byte)0x75);
        ////bytes.Add((sbyte)rel8);
        bytes.Add((byte)rel8);
        break;
      }

      default:
        throw new AssemblerException(ErrorCode.UnknownSymbol, $"Line {ln.LineNumber}: Unknown mnemonic '{ln.Mnemonic}'");
    }
  }

  private void EmitMemoryInstruction(List<byte> bytes, byte opcode, int register, string operand,
                                    AsmLine line, IReadOnlyDictionary<string, uint> symbols)
  {
    ParseMemory(operand, line, symbols, out bool hasBase, out int baseReg, out int displacement, out bool needsSib);
    bytes.Add(opcode);
    if (!hasBase)
      bytes.Add(ModRM(0b00, (byte)register, 0b101));
    else if (needsSib)
    {
      bytes.Add(ModRM(0b10, (byte)register, 0b100));
      bytes.Add(ScaleIndexBase(0, 0b100, 0b100));
    }
    else
      bytes.Add(ModRM(0b10, (byte)register, (byte)baseReg));

    bytes.AddRange(BitConverter.GetBytes(displacement));
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
    var plusIdx = inner.IndexOf(Constants.PlusChar);
    if (plusIdx >= 0)
    {
      var left = inner[..plusIdx].Trim();
      var right = inner[(plusIdx + 1)..].Trim();
      if (!IsRegister32(left))
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
      if (!IsRegister32(inner))
        throw new AssemblerException($"Line {ln.LineNumber}: Memory addressing requires a 32-bit register");

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
    if (!dll.Contains(Constants.PeriodChar))
      dll += Constants.DotDll;

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

  /// <summary>Get a numeric value or the virtual address of a local/imported symbol.</summary>
  /// <param name="token">Input operand.</param>
  /// <param name="ln"><see cref="AsmLine"/>.</param>
  /// <param name="externs">Known externs.</param>
  /// <returns></returns>
  /// <exception cref="AssemblerException"></exception>
  private int ResolveValue(string token, AsmLine ln, IReadOnlyDictionary<string, uint> externs)
  {
    token = token.Trim();

    // Look up symbols before numbers so names ending in 'h' (such as path) work.
    if (externs.TryGetValue(token, out var addr))
      return unchecked((int)addr);

    // Immediate number (decimal, hex only)
    if (char.IsDigit(token[0]) ||
        token.StartsWith(Constants.HexPrefix, StringComparison.OrdinalIgnoreCase) ||
        token.EndsWith(Constants.HexSuffix, StringComparison.OrdinalIgnoreCase))
      return ParseInt(token, ln.LineNumber);

    throw new AssemblerException(ErrorCode.UnknownSymbol, $"Line {ln.LineNumber}: Unknown symbol or value '{token}'");
  }
}
