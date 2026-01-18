// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace Zero.Assembly;

/// <summary>List of x86 instruction mnemonic.</summary>
/// <remarks>r: register, m: memory address, imm: immediate (i.e. a value).</remarks>
public class Mnemonic
{
  /// <summary>Add with carry</summary>
  public const string ADC = "ADC";

  /// <summary>Add (0x00...0x05, 0x80/0...0x81/0, 0x83/0).</summary>
  public const string ADD = "ADD";

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

  public const string JMP = "JMP";
  public const string JNZ = "JNZ";
  public const string JZ = "JZ";

  /// <summary>Move r32, imm32 (0xA0...0xA3, 0x8C, 0x8E).</summary>
  public const string MOV = "MOV";

  /// <summary>Move byte from string to string (0xA4).</summary>
  public const string MOVSB = "MOVSB";

  /// <summary>Move word from string to string (0xA5).</summary>
  public const string MOVSW = "MOVSW";

  /// <summary>Unsigned multiply(0xF7/4, 0xF6/4).</summary>
  public const string MUL = "MUL";

  /// <summary>No operation (	0x90).</summary>
  public const string NOP = "NOP";

  public const string POP = "POP";
  public const string PUSH = "PUSH";

  /// <summary>Return from procedure.</summary>
  public const string RET = "RET";

  public const string SUB = "SUB";
}
