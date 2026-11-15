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
      {
        quote = current;
      }
      else if (current == delimiter)
      {
        return i;
      }
    }

    if (quote != Constants.NullChar)
      throw new AssemblerException(lineNumber, $"Unterminated quoted string");

    return -1;
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
      {
        return result.ToString();
      }
      else
      {
        throw new AssemblerException(lineNumber, $"Unexpected text after quoted string");
      }
    }

    throw new AssemblerException(lineNumber, $"Unterminated quoted string");
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
        throw new AssemblerException(lineNumber, $"Empty operand");

      operands.Add(operand);

      if (comma < 0)
        return operands;
      start = comma + 1;
    }
  }

  // Replace comments with whitespace so tokens and source line numbers stay separate.
  internal static string[] StripComments(string[] source)
  {
    var result = new string[source.Length];
    int blockStart = 0;
    for (int line = 0; line < source.Length; line++)
    {
      var text = source[line];
      var characters = text.ToCharArray();
      char quote = Constants.NullChar;

      for (int i = 0; i < text.Length; i++)
      {
        char current = text[i];
        char next = i + 1 < text.Length ? text[i + 1] : Constants.NullChar;

        if (blockStart != 0)
        {
          characters[i] = Constants.SpaceChar;

          if (current == '*' && next == '/')
          {
            characters[++i] = Constants.SpaceChar;
            blockStart = 0;
          }
        }
        else if (quote != Constants.NullChar)
        {
          if (current == quote)
          {
            if (next == quote)
              i++;
            else
              quote = Constants.NullChar;
          }
        }
        else if (current is Constants.SingleQuote or Constants.DoubleQuote)
        {
          quote = current;
        }
        else if (current == Constants.CommentLine1 || (current == '/' && next == '/'))
        {
          for (; i < characters.Length; i++)
            characters[i] = Constants.SpaceChar;
        }
        else if (current == '/' && next == '*')
        {
          blockStart = line + 1;
          characters[i] = Constants.SpaceChar;
          characters[++i] = Constants.SpaceChar;
        }
      }

      if (quote != Constants.NullChar)
        throw new AssemblerException(line + 1, "Unterminated quoted string");

      result[line] = new string(characters);
    }

    if (blockStart != 0)
      throw new AssemblerException(blockStart, "Unterminated block comment");
    return result;
  }
}
