// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Zero.Exceptions;

namespace Zero.Parsing;

internal static class IntegerLiteral
{
  public static ulong Parse(string text, int line)
  {
    text = text.Trim();
    try
    {
      if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return ulong.Parse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
      if (text.EndsWith('h') || text.EndsWith('H'))
        return ulong.Parse(text[..^1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
      return text.StartsWith('-')
        ? unchecked((ulong)long.Parse(text, CultureInfo.InvariantCulture))
        : ulong.Parse(text, CultureInfo.InvariantCulture);
    }
    catch (Exception ex) when (ex is FormatException or OverflowException)
    {
      throw new AssemblerException(line, $"Invalid integer '{text}'");
    }
  }
}
