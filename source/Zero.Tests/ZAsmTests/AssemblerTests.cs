// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public class AssemblerTests
{
  [TestMethod]
  public void CompileBasicTest()
  {
    var lines = TestData.AsmData.MovCmpJz.Split(Environment.NewLine);
    var bytes = Zero.CompileAsm(lines);

    Assert.IsNotNull(bytes);
  }
}
