// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Assembly;

/// <summary>List of x86 instruction mnemonic.</summary>
/// <remarks>
///   r: register, m: memory address, imm: immediate (i.e. a value).
///   References:
///     * http://unixwiz.net/techtips/x86-jumps.html
///     * https://en.wikipedia.org/wiki/List_of_x86_instructions
/// </remarks>
public class Mnemonic
{
  /// <summary>Add with carry (0x10...0x15, 0x80...0x81/2, 0x83/2).</summary>
  public const string ADC = "ADC";

  /// <summary>Add (0x00...0x05, 0x80/0...0x81/0, 0x83/0).</summary>
  /// <remarks><![CDATA[(1) r += r/m/imm; (2) m += r/imm]]></remarks>
  public const string ADD = "ADD";

  /// <summary>Logical AND (0x20...0x25, 0x80...0x81/4, 0x83/4).</summary>
  public const string AND = "AND";

  /// <summary>Call procedure (0x9A, 0xE8, 0xFF/2, 0xFF/3).</summary>
  public const string CALL = "CALL";

  /// <summary>Compare operands (0x38...0x3D, 0x80...0x81/7, 0x83/7).</summary>
  public const string CMP = "CMP";

  /// <summary>Compare bytes in memory (0xA6). May be used with a REPE or REPNE prefix to test and repeat the instruction CX times.</summary>
  /// <remarks><![CDATA[if (DF==0) *(byte*)SI++ - *(byte*)ES:DI++; else *(byte*) SI-- - *(byte*) ES:DI--;]]></remarks>
  public const string CMPSB = "CMPSB";

  /// <summary>Compare words in memory (0xA7) May be used with a REPE or REPNE prefix to test and repeat the instruction CX times.</summary>
  /// <remarks><![CDATA[if (DF==0) *(word*)SI++ - *(word*)ES:DI++; else *(word*) SI-- - *(word*) ES:DI--;]]></remarks>
  public const string CMPSW = "CMPSW";

  /// <summary>Data section - Define byte.</summary>
  public const string DB = "DB";

  public const string DD = "DD";

  /// <summary>Text section - ___.</summary>
  public const string DW = "DW";

  /// <summary>Call to interrupt (0xCC, 0xCD).</summary>
  public const string INT = "INT";

  /// <summary>Jump (0xE9...0xEB, 0xFF/4, 0xFF/5).</summary>
  public const string JMP = "JMP";

  /// <summary>Jump if not zero; flags: ZF=0 (0x75) same as JNE.</summary>
  public const string JNZ = "JNZ";

  /// <summary>Short jump if not zero (.mylabel:).</summary>
  public const string JNZ8 = "JNZ8";

  /// <summary>Jump if zero; flags: ZF=1 (0x74) same as JE jump if equal.</summary>
  public const string JZ = "JZ";

  /// <summary>Short jump if zero (.mylabel:).</summary>
  public const string JZ8 = "JZ8";

  /// <summary>	Load Effective Address (0x8D).</summary>
  public const string LEA = "LEA";

  /// <summary>Move r32, imm32 (0xA0...0xA3, 0x8C, 0x8E).</summary>
  public const string MOV = "MOV";

  /// <summary>Move byte from string to string (0xA4).</summary>
  public const string MOVSB = "MOVSB";

  /// <summary>Move word from string to string (0xA5).</summary>
  public const string MOVSW = "MOVSW";

  /// <summary>Unsigned multiply (0xF7/4, 0xF6/4).</summary>
  public const string MUL = "MUL";

  /// <summary>No operation (	0x90).</summary>
  public const string NOP = "NOP";

  /// <summary>Negate the operand, logical NOT (0xF6/2...0xF7/2).</summary>
  /// <remarks><![CDATA[r/m ^= -1]]></remarks>
  public const string NOT = "NOT";

  /// <summary>Logical OR (0x08...0x0D, 0x80...0x81/1, 0x83/1).</summary>
  public const string OR = "OR";

  /// <summary>Pop data from stack (0x07, 0x17, 0x1F, 0x58...0x5F, 0x8F/0).</summary>
  /// <remarks><![CDATA[r/m/sreg = *SP++;]]></remarks>
  public const string POP = "POP";

  /// <summary>Pop FLAGS register from stack (0x9D).</summary>
  /// <remarks><![CDATA[FLAGS = *SP++;]]></remarks>
  public const string POPF = "POPF";

  /// <summary>Push data onto stack (0x06, 0x0E, 0x16, 0x1E, 0x50...0x57, 0xFF/6).</summary>
  /// <remarks><![CDATA[*--SP = r/m/sreg;]]></remarks>
  public const string PUSH = "PUSH";

  /// <summary>Push FLAGS onto stack (0x9C).</summary>
  /// <remarks><![CDATA[*--SP = FLAGS;]]></remarks>
  public const string PUSHF = "PUSHF";

  /// <summary>Return from procedure.</summary>
  public const string RET = "RET";

  /// <summary>Subtraction with borrow (0x18...0x1D, 0x80...0x81/3, 0x83/3).</summary>
  /// <remarks><![CDATA[r -= (r/m/imm+CF);]]></remarks>
  public const string SBB = "SBB";

  /// <summary>Subtraction (0x28...0x2D, 0x80...0x81/5, 0x83/5).</summary>
  /// <remarks><![CDATA[(1) r -= r/m/imm; (2) m -= r/imm;]]></remarks>
  public const string SUB = "SUB";

  /// <summary>Logical compare 'AND' (0x84, 0x85, 0xA8, 0xA9, 0xF6/0, 0xF7/0).</summary>
  /// <remarks><![CDATA[(1) r & r/m/imm; (2) m & r/imm;]]></remarks>
  public const string TEST = "TEST";

  /// <summary>Exclusive OR (0x30...0x35, 0x80...0x81/6, 0x83/6).</summary>
  /// <remarks><![CDATA[(1) r ^+= r/m/imm; (2) m ^= r/imm;]]></remarks>
  public const string XOR = "XOR";
}
