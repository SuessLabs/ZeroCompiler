// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text;
using Zero.Exceptions;

namespace Zero.Parsing;

internal static class AssemblySyntax
{
  // Delimiters inside quoted strings are data. A doubled quote escapes itself.
  internal static int FindUnquoted(string text, char delimiter, int lineNumber, int start = 0)
  {
    char quote = Constants.NullChar;
    for (int i = start; i < text.Length; i++)
    {
      var current = text[i];
      if (quote != Constants.NullChar)
      {
        if (current == quote)
        {
          if (i + 1 < text.Length && text[i + 1] == quote)
            i++;
          else
            quote = Constants.NullChar;
        }
      }
      else if (current is Constants.SingleQuote or Constants.DoubleQuote)
        quote = current;
      else if (current == delimiter)
        return i;
    }

    if (quote != Constants.NullChar)
      throw new AssemblerException($"Line {lineNumber}: Unterminated quoted string");
    return -1;
  }

  internal static List<string> SplitOperands(string text, int lineNumber)
  {
    var operands = new List<string>();
    if (string.IsNullOrWhiteSpace(text))
      return operands;

    int start = 0;
    while (true)
    {
      int comma = FindUnquoted(text, Constants.CommaChar, lineNumber, start);
      var operand = (comma < 0 ? text[start..] : text[start..comma]).Trim();

      if (operand.Length == 0)
        throw new AssemblerException($"Line {lineNumber}: Empty operand");

      operands.Add(operand);

      if (comma < 0)
        return operands;
      start = comma + 1;
    }
  }

  internal static string ReadString(string operand, int lineNumber)
  {
    char quote = operand[0];
    var result = new StringBuilder();
    for (int i = 1; i < operand.Length; i++)
    {
      char current = operand[i];
      if (current != quote)
        result.Append(current);

      else if (i + 1 < operand.Length && operand[i + 1] == quote)
      {
        result.Append(quote);
        i++;
      }
      else if (i == operand.Length - 1)
        return result.ToString();
      else
        throw new AssemblerException($"Line {lineNumber}: Unexpected text after quoted string");
    }

    throw new AssemblerException($"Line {lineNumber}: Unterminated quoted string");
  }
}
