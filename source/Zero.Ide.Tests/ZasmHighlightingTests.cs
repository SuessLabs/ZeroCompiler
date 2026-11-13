// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using Zero.Assembly;
using Zero.Ide.Highlighting;

namespace Zero.Ide.Tests;

[TestClass]
public class ZasmHighlightingTests
{
  [TestMethod]
  [DataRow("#TARGET linux-x64", "#TARGET", "Directive")]
  [DataRow("#import kernel32.dll, ExitProcess", "#import", "Directive")]
  [DataRow("using \"windows/kernel32.zh\"", "using", "Directive")]
  [DataRow("using \"windows/kernel32.zh\"", "windows/kernel32.zh", "String")]
  [DataRow("#TARGET linux-x64", "linux-x64", "Target")]
  [DataRow("mOv R15D, 0xFE", "mOv", "Instruction")]
  [DataRow("mOv R15D, 0xFE", "R15D", "Register")]
  [DataRow("mov ch, 0FFh", "ch", "Register")]
  [DataRow("mov rax, [rip + message]", "rip", "Register")]
  [DataRow("mov eax, 0xFE", "0xFE", "Number")]
  [DataRow("mov eax, FFh", "FFh", "Number")]
  [DataRow("add esp, -42", "-42", "Number")]
  [DataRow(".loop: jnz8 .loop", ".loop", "Label")]
  [DataRow("message db 'Hello', 0", "message", "Label")]
  [DataRow("message db 'Hello', 0", "db", "Data")]
  [DataRow("; #TARGET mov eax, 42", "#TARGET", "Comment")]
  [DataRow("db 'Hello; #IMPORT', 0", "#IMPORT", "String")]
  [DataRow("db 'It''s; #TARGET', 0", "#TARGET", "String")]
  [DataRow("db \"Say \"\"hello; #IMPORT\"\"\", 0", "#IMPORT", "String")]
  [DataRow("db 'text' ; mov eax, 1", "mov", "Comment")]
  [DataRow("movable eax_value", "movable", null)]
  [DataRow("movable eax_value", "eax_value", null)]
  public void HighlightsZasmTokensInBothThemes(string text, string token, string? expectedColor)
  {
    foreach (var dark in new[] { true, false })
    {
      var document = new TextDocument(text);
      using var highlighter = new DocumentHighlighter(document, ZasmHighlighting.Create(dark));
      var offset = text.IndexOf(token, StringComparison.Ordinal);
      var section = highlighter.HighlightLine(1).Sections.LastOrDefault(s => s.Offset <= offset && offset < s.Offset + s.Length);
      Assert.AreEqual(expectedColor, section?.Color.Name, $"Token '{token}', dark theme: {dark}");
    }
  }

  [TestMethod]
  public void AllCompilerInstructionsRemainHighlighted()
  {
    foreach (var field in typeof(Mnemonic).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
      if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
      var instruction = (string)field.GetRawConstantValue()!;
      var expected = instruction is "DB" or "DW" or "DD" or "DQ" ? "Data" : "Instruction";
      HighlightsZasmTokensInBothThemes(instruction + " eax, 1", instruction, expected);
    }
  }

  [TestMethod]
  public void UnterminatedStringDoesNotColorNextLine()
  {
    var document = new TextDocument("db 'unterminated\nmov eax, 1");
    using var highlighter = new DocumentHighlighter(document, ZasmHighlighting.Create(true));
    Assert.AreEqual("Instruction", highlighter.HighlightLine(2).Sections[0].Color.Name);
  }
}
