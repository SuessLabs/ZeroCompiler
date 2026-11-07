// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

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
  public void CompileImportLeaJZ8Test()
  {
    var lines = TestData.AsmData.ImportTableShortJumps.Split(Environment.NewLine);
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
  [DataRow("hello-console.zasm", "Hello from Zero!\r\n")]
  [DataRow("hello-db-vars.zasm", "Hello, World!\r\n")]
  [DataRow("hello-concat.zasm", "Hello\r\nZero Assembly!\r\nHello Zero Assembly!\r\n")]
  [DataRow("v002-integer-math.zasm", "7 + 5 = 12\r\n6 * 7 = 42\r\n")]
  public void ConsoleSamplePrintsExpectedOutput(string sampleName, string expectedOutput)
  {
    if (!OperatingSystem.IsWindows())
      Assert.Inconclusive("The sample produces a Windows x86 executable.");

    var source = Path.Combine(AppContext.BaseDirectory, "samples", sampleName);
    var bytes = Zero.CompileAsm(File.ReadAllLines(source));
    Assert.IsNotNull(bytes);

    var executable = Path.Combine(Path.GetTempPath(), $"zero-console-{Guid.NewGuid():N}.exe");
    try
    {
      File.WriteAllBytes(executable, bytes);
      using var process = Process.Start(new ProcessStartInfo(executable)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
      });
      Assert.IsNotNull(process);

      if (!process.WaitForExit(10000))
      {
        process.Kill();
        process.WaitForExit();
        Assert.Fail("The console sample did not exit within 10 seconds.");
      }

      Assert.AreEqual(0, process.ExitCode);
      Assert.AreEqual(expectedOutput, process.StandardOutput.ReadToEnd());
    }
    finally
    {
      File.Delete(executable);
    }
  }
}
