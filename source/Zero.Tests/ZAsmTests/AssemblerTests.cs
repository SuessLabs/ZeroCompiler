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

  [TestMethod]
  public void CompileImportTableTest()
  {
    var lines = TestData.AsmData.ImportTable1.Split(Environment.NewLine);
    var bytes = Zero.CompileAsm(lines);

    Assert.IsNotNull(bytes);
  }

  [TestMethod]
  public void CompileImportLeaJZ8Test()
  {
    var lines = TestData.AsmData.ImportTableShortJumps.Split(Environment.NewLine);
    var bytes = Zero.CompileAsm(lines);

    Assert.IsNotNull(bytes);
  }
}
