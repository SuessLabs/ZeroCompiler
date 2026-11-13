// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Zero.Assembly;
using Zero.Compilation;
using Zero.Exceptions;
using Zero.Parsing;

namespace Zero.Targets.X64;

/// <summary>Essential AMD64 integer encodings; sizes and emission share one path.</summary>
internal sealed class X64Encoder : IInstructionEncoder
{
  private readonly record struct CpuRegister(int Number, int Width);
  private readonly record struct MemoryOperand(int Base, string? Target, long Displacement);
  private static readonly Dictionary<string, CpuRegister> Registers = CreateRegisters();

  public int GetSize(AsmLine line) => EncodeLine(line, null, 0).Length;

  public byte[] Encode(AssemblyProgram program, IReadOnlyDictionary<string, ulong> addresses, ulong codeAddress)
  {
    var bytes = new List<byte>(program.CodeSize);
    foreach (var line in program.Lines)
    {
      var instruction = EncodeLine(line, addresses, codeAddress);
      if (bytes.Count != line.Offset || instruction.Length != line.Size)
        throw new AssemblerException(line.LineNumber, "Instruction size/offset mismatch");
      bytes.AddRange(instruction);
    }

    return bytes.ToArray();
  }

  private static Dictionary<string, CpuRegister> CreateRegisters()
  {
    var registers = new Dictionary<string, CpuRegister>(StringComparer.OrdinalIgnoreCase);
    string[] wide = ["RAX", "RCX", "RDX", "RBX", "RSP", "RBP", "RSI", "RDI"];
    string[] narrow = ["EAX", "ECX", "EDX", "EBX", "ESP", "EBP", "ESI", "EDI"];

    for (int i = 0; i < 16; i++)
    {
      registers.Add(i < 8 ? wide[i] : $"R{i}", new(i, 64));
      registers.Add(i < 8 ? narrow[i] : $"R{i}D", new(i, 32));
    }

    return registers;
  }

  private static byte[] EncodeLine(AsmLine line, IReadOnlyDictionary<string, ulong>? symbols, ulong codeAddress)
  {
    if (AssemblyData.IsDirective(line.Mnemonic))
      return AssemblyData.Encode(line);
    var bytes = new List<byte>();
    var ops = line.Operands;
    switch (line.Mnemonic)
    {
      case "NOP":
        Count(0);
        bytes.Add(0x90);
        break;
      case "SYSCALL":
        Count(0);
        bytes.AddRange([0x0F, 0x05]);
        break;
      case "RET":
        if (ops.Count == 0)
          bytes.Add(0xC3);
        else
        {
          Count(1);
          ulong value = IntegerLiteral.Parse(ops[0], line.LineNumber);
          if (value > ushort.MaxValue)
            throw Error("RET cleanup must be between 0 and 65535");
          bytes.Add(0xC2);
          Immediate(value, 2);
        }

        break;
      case "INT":
        Count(1);
        ulong interrupt = IntegerLiteral.Parse(ops[0], line.LineNumber);
        if (interrupt > byte.MaxValue)
          throw Error("INT expects a byte value");
        bytes.Add(0xCD);
        Immediate(interrupt, 1);
        break;
      case "MOV":
      {
        Count(2);
        if (Registers.TryGetValue(ops[0], out var destination))
        {
          if (Registers.TryGetValue(ops[1], out var source))
          {
            SameWidth(destination, source);
            Rex(destination.Width, source.Number, destination.Number);
            bytes.Add(0x89);
            ModRm(3, source.Number, destination.Number);
          }
          else if (IsMemory(ops[1]))
            Memory(0x8B, destination.Number, destination.Width, ops[1]);
          else
          {
            ulong value = Value(ops[1]);
            if (destination.Width == 32)
              Check32(value, false);
            Rex(destination.Width, 0, destination.Number);
            bytes.Add((byte)(0xB8 + (destination.Number & 7)));
            Immediate(value, destination.Width / 8);
          }
        }
        else if (IsMemory(ops[0]) && Registers.TryGetValue(ops[1], out var source))
          Memory(0x89, source.Number, source.Width, ops[0]);
        else
          throw Error("MOV expects r32/r64, register/immediate/[memory], or [memory], r32/r64");
        break;
      }
      case "LEA":
      {
        Count(2);
        var destination = Register(ops[0]);
        if (!IsMemory(ops[1]))
          throw Error("LEA expects a register and [memory]");
        Memory(0x8D, destination.Number, destination.Width, ops[1]);
        break;
      }
      case "PUSH":
      case "POP":
      {
        Count(1);
        if (Registers.TryGetValue(ops[0], out var register))
        {
          if (register.Width != 64)
            throw Error("PUSH/POP require a 64-bit register in this target");

          Rex(32, 0, register.Number);
          bytes.Add((byte)((line.Mnemonic == "PUSH" ? 0x50 : 0x58) + (register.Number & 7)));
        }
        else if (line.Mnemonic == "PUSH" && !IsMemory(ops[0]))
        {
          ulong value = Value(ops[0]);
          Check32(value, true);
          bytes.Add(0x68);
          Immediate(value, 4);
        }
        else
          throw Error("PUSH expects r64 or signed imm32; POP expects r64");
        break;
      }
      case "ADD":
      case "SUB":
      case "CMP":
      case "AND":
      case "OR":
      case "XOR":
      case "TEST":
      {
        Count(2);
        var destination = Register(ops[0]);
        if (Registers.TryGetValue(ops[1], out var source))
        {
          SameWidth(destination, source);
          Rex(destination.Width, source.Number, destination.Number);
          bytes.Add(line.Mnemonic switch
          {
            "ADD" => 0x01, "SUB" => 0x29, "CMP" => 0x39, "AND" => 0x21,
            "OR" => 0x09, "XOR" => 0x31, _ => 0x85,
          });
          ModRm(3, source.Number, destination.Number);
        }
        else
        {
          ulong value = Value(ops[1]);
          Check32(value, destination.Width == 64);
          int extension = line.Mnemonic switch
          {
            "ADD" => 0, "OR" => 1, "AND" => 4, "SUB" => 5, "XOR" => 6, "CMP" => 7, _ => 0,
          };
          Rex(destination.Width, 0, destination.Number);
          bytes.Add(line.Mnemonic == "TEST" ? (byte)0xF7 : (byte)0x81);
          ModRm(3, extension, destination.Number);
          Immediate(value, 4);
        }

        break;
      }
      case "CALL":
      case "JMP":
      case "JZ":
      case "JNZ":
      case "JZ8":
      case "JNZ8":
      {
        Count(1);
        if (line.Mnemonic is "CALL" or "JMP" && Registers.TryGetValue(ops[0], out var register))
        {
          if (register.Width != 64)
            throw Error("Indirect calls/jumps require a 64-bit register");
          Rex(32, 0, register.Number);
          bytes.Add(0xFF);
          ModRm(3, line.Mnemonic == "CALL" ? 2 : 4, register.Number);
        }
        else if (line.Mnemonic is "CALL" or "JMP" && IsMemory(ops[0]))
          Memory(0xFF, line.Mnemonic == "CALL" ? 2 : 4, 32, ops[0]);
        else
        {
          bool shortJump = line.Mnemonic is "JZ8" or "JNZ8";
          long distance = symbols is null ? 0 : Relative(Symbol(ops[0]));
          if (shortJump)
          {
            if (distance < sbyte.MinValue || distance > sbyte.MaxValue)
              throw Error("Short jump out of range");
            bytes.Add(line.Mnemonic == "JZ8" ? (byte)0x74 : (byte)0x75);
            Immediate(unchecked((ulong)distance), 1);
          }
          else
          {
            if (distance < int.MinValue || distance > int.MaxValue)
              throw Error("Relative branch out of range");
            if (line.Mnemonic is "JZ" or "JNZ")
              bytes.AddRange([0x0F, line.Mnemonic == "JZ" ? (byte)0x84 : (byte)0x85]);
            else
              bytes.Add(line.Mnemonic == "CALL" ? (byte)0xE8 : (byte)0xE9);
            Immediate(unchecked((ulong)distance), 4);
          }
        }

        break;
      }
      default:
        throw Error($"Unsupported x86-64 mnemonic '{line.Mnemonic}'");
    }

    return bytes.ToArray();

    AssemblerException Error(string detail) => new(line.LineNumber, detail);

    void Count(int count)
    {
      if (ops.Count != count)
        throw Error($"{line.Mnemonic} expects {count} operand(s)");
    }

    CpuRegister Register(string name) => Registers.TryGetValue(name, out var register)
      ? register : throw Error($"Expected a supported 32-bit or 64-bit register, got '{name}'");

    void SameWidth(CpuRegister first, CpuRegister second)
    {
      if (first.Width != second.Width)
        throw Error("Register widths must match");
    }

    ulong Symbol(string name)
    {
      if (symbols is null)
        return 0;
      if (!symbols.TryGetValue(name, out ulong address))
        throw Error($"Undefined symbol '{name}'");
      return address;
    }

    ulong Value(string token)
    {
      if (symbols is not null && symbols.TryGetValue(token, out ulong address))
        return address;
      if (Registers.ContainsKey(token) || IsMemory(token))
        throw Error($"Invalid immediate '{token}'");
      if (Regex.IsMatch(token, "^[A-Za-z_.][A-Za-z0-9_.]*$") && !Regex.IsMatch(token, "^[0-9A-Fa-f]+[hH]$"))
        return Symbol(token);
      return IntegerLiteral.Parse(token, line.LineNumber);
    }

    void Check32(ulong value, bool signed)
    {
      long signedValue = unchecked((long)value);
      if (signed ? signedValue < int.MinValue || signedValue > int.MaxValue
                  : value > uint.MaxValue && (signedValue < int.MinValue || signedValue >= 0))
        throw Error(signed ? "Immediate must fit signed 32 bits for this encoding" : "Immediate must fit 32 bits");
    }

    void Rex(int width, int reg, int memoryBase)
    {
      int prefix = 0x40 | (width == 64 ? 8 : 0) | (reg >= 8 ? 4 : 0) | (memoryBase >= 8 ? 1 : 0);
      if (prefix != 0x40)
        bytes.Add((byte)prefix);
    }

    void ModRm(int mode, int reg, int rm) => bytes.Add((byte)((mode << 6) | ((reg & 7) << 3) | (rm & 7)));

    void Immediate(ulong value, int size)
    {
      for (int i = 0; i < size; i++)
        bytes.Add((byte)(value >> (i * 8)));
    }

    long Relative(ulong address)
    {
      try
      {
        return checked((long)address - (long)(codeAddress + (ulong)line.Offset + (ulong)line.Size));
      }
      catch (OverflowException)
      {
        throw Error("Relative address is out of range");
      }
    }

    void Memory(byte opcode, int reg, int width, string operand)
    {
      var memory = ParseMemory(operand);
      Rex(width, reg, memory.Base);
      bytes.Add(opcode);
      if (memory.Base < 0)
        ModRm(0, reg, 5); // RIP-relative disp32.
      else
      {
        ModRm(2, reg, memory.Base); // Fixed disp32 keeps both passes deterministic.
        if ((memory.Base & 7) == 4)
          bytes.Add(0x24); // SIB: no index, RSP/R12 base.
      }

      long displacement = memory.Displacement;
      if (memory.Target is not null && symbols is not null)
      {
        ulong address = Value(memory.Target);
        displacement = Relative(address);
      }

      if (displacement < int.MinValue || displacement > int.MaxValue)
        throw Error("Memory displacement must fit signed 32 bits");
      Immediate(unchecked((ulong)displacement), 4);
    }

    MemoryOperand ParseMemory(string operand)
    {
      var inner = operand.Trim()[1..^1].Trim();
      if (inner.Length == 0)
        throw Error("Empty memory operand");
      if (inner.Equals("RIP", StringComparison.OrdinalIgnoreCase))
        return new(-1, null, 0);
      if (Registers.TryGetValue(inner, out var register))
      {
        if (register.Width != 64)
          throw Error("Memory bases must be 64-bit registers");
        return new(register.Number, null, 0);
      }

      var match = Regex.Match(inner, "^([A-Za-z][A-Za-z0-9]*)\\s*([+-])\\s*(.+)$");
      if (match.Success)
      {
        var name = match.Groups[1].Value;
        var right = match.Groups[3].Value.Trim();
        bool minus = match.Groups[2].Value == "-";
        bool rip = name.Equals("RIP", StringComparison.OrdinalIgnoreCase);
        if (rip && !minus && Regex.IsMatch(right, "^[A-Za-z_.][A-Za-z0-9_.]*$") && !Regex.IsMatch(right, "^[0-9A-Fa-f]+[hH]$"))
          return new(-1, right, 0);
        var baseRegister = rip ? new CpuRegister(-1, 64) : Register(name);
        if (baseRegister.Width != 64)
          throw Error("Memory bases must be 64-bit registers");
        long displacement = unchecked((long)IntegerLiteral.Parse(right, line.LineNumber));
        if (minus)
        {
          if (displacement == long.MinValue)
            throw Error("Memory displacement is out of range");
          displacement = -displacement;
        }

        return new(baseRegister.Number, null, displacement);
      }

      if (inner.Contains('+') || inner.Contains('-'))
        throw Error("Unsupported address expression");
      return new(-1, inner, 0);
    }
  }

  private static bool IsMemory(string text) => text.StartsWith('[') && text.EndsWith(']');
}
