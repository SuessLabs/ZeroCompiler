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
  public static byte[] Encode(AsmLine line)
  {
    if (line.Operands.Count == 0)
      throw new AssemblerException(line.LineNumber, $"{line.Mnemonic} expects data");

    int width = line.Mnemonic switch
    {
      Mnemonic.DB => 1,
      Mnemonic.DW => 2,
      Mnemonic.DD => 4,
      Mnemonic.DQ => 8,
      _ => throw new InvalidOperationException()
    };

    var bytes = new List<byte>();

    foreach (var operand in line.Operands)
    {
      if (width == 1 && operand[0] is '\'' or '"')
      {
        bytes.AddRange(Encoding.UTF8.GetBytes(AssemblySyntax.ReadString(operand, line.LineNumber)));
      }
      else
      {
        ulong value = IntegerLiteral.Parse(operand, line.LineNumber);
        for (int i = 0; i < width; i++)
          bytes.Add((byte)(value >> (i * 8)));
      }
    }

    return bytes.ToArray();
  }

  public static bool IsDirective(string mnemonic) => mnemonic is Mnemonic.DB or Mnemonic.DW or Mnemonic.DD or Mnemonic.DQ;
}
