// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Zero.Assembly;

public class AsmLine
{
  public int LineNumber { get; set; }

  public string Mnemonic { get; set; } = "";

  public int Offset { get; set; }

  public List<string> Operands { get; set; } = [];

  public int Size { get; set; }
}
