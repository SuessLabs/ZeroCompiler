// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Zero.Assembly;
using Zero.Exceptions;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class CommentTests
{
  [TestMethod]
  [DataRow("windows-x86")]
  [DataRow("linux-x64")]
  public void CommentsDoNotChangeEmittedExecutable(string target)
  {
    var expected = new Assembler().Assemble([$"#TARGET {target}", "mov eax, 42", "ret"]);
    var actual = new Assembler().Assemble(
    [
      "// #TARGET invalid ' /*",
      "/* #TARGET invalid",
      "invalid_instruction ; // ' \"",
      "*/",
      $"#TARGET /* target */ {target} // trailing comment",
      "mov/* separator */eax,/* first */42/* second */ // ' /*",
      "/* before */ret/* after */ ; /*",
    ]);

    CollectionAssert.AreEqual(expected, actual);
  }

  [TestMethod]
  [DataRow("db 'http://example; /* literal */', 0")]
  [DataRow("db \"Say \"\"// /* text */\"\"\", 0")]
  [DataRow("db 'It''s // /* text */', 0")]
  public void QuotedCommentMarkersRemainData(string source)
  {
    var expected = new Assembler().Assemble([source]);
    var actual = new Assembler().Assemble([source + " /* ' \" */ // ignored"]);

    CollectionAssert.AreEqual(expected, actual);
  }

  [TestMethod]
  public void UnterminatedBlockReportsOpeningLine()
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() =>
      new Assembler().Assemble(["nop", "/* begins here", "ret"]));

    Assert.AreEqual(2, error.LineNumber);
    Assert.Contains("Unterminated block comment", error.Message);
  }

  [TestMethod]
  public void ErrorsAfterBlockCommentsKeepOriginalLine()
  {
    var error = Assert.ThrowsExactly<AssemblerException>(() =>
      new Assembler().Assemble(["/*", "ignored", "*/", "invalid_instruction"]));

    Assert.AreEqual(4, error.LineNumber);
  }
}
