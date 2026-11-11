// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Zero.Exceptions;
using Zero.Parsing;

namespace Zero.Assembly;

internal static class AssemblyData
{
  public static bool IsDirective(string mnemonic) => mnemonic is "DB" or "DW" or "DD" or "DQ";

  public static byte[] Encode(AsmLine line)
  {
    if (line.Operands.Count == 0)
      throw new AssemblerException(line.LineNumber, $"{line.Mnemonic} expects data");
    int width = line.Mnemonic switch { "DB" => 1, "DW" => 2, "DD" => 4, "DQ" => 8, _ => throw new InvalidOperationException() };
    var bytes = new List<byte>();
    foreach (var operand in line.Operands)
    {
      if (width == 1 && operand[0] is '\'' or '"')
        bytes.AddRange(Encoding.UTF8.GetBytes(AssemblySyntax.ReadString(operand, line.LineNumber)));
      else
      {
        ulong value = IntegerLiteral.Parse(operand, line.LineNumber);
        for (int i = 0; i < width; i++)
          bytes.Add((byte)(value >> (i * 8)));
      }
    }

    return bytes.ToArray();
  }
}
