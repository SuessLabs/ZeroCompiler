// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Zero.Compilation;

namespace Zero.Tests.ZAsmTests;

[TestClass]
public sealed class LinuxRuntimeTests
{
  [TestMethod]
  public void HelloSampleExecutesOnLinuxX64()
  {
    RequireLinuxX64();
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples/hello-linux64-cli.zasm"));
    Run(source, 0, "Hello Zero from Linux x86-64!\n");
  }

  [TestMethod]
  [DataRow("mov edi, 37\nmov eax, 60\nsyscall", 37)]
  [DataRow("sub rsp, 16\nmov rax, 37\nmov [rsp + 8], rax\nmov r8, [rsp + 8]\npush r8\npop rdi\nadd rsp, 16\nmov eax, 60\nsyscall", 37)]
  [DataRow("mov r8, 5\nxor edi, edi\nloop: add edi, 1\nsub r8, 1\njnz loop\ncall helper\nmov eax, 60\nsyscall\nhelper: add edi, 32\nret", 37)]
  [DataRow("lea r8, [helper]\ncall r8\nmov eax, 60\nsyscall\nhelper: mov edi, 19\nret", 19)]
  public void GeneratedInstructionsExecuteCorrectly(string instructions, int exitCode)
  {
    RequireLinuxX64();
    Run("#TARGET linux-x64\n" + instructions, exitCode, "");
  }

  private static void RequireLinuxX64()
  {
    if (!CompilationTarget.LinuxX64.CanRunOnCurrentHost)
      Assert.Inconclusive("Native Linux x86-64 is required; run these tests in Linux/WSL or the Linux CI job.");
  }

  private static void Run(string source, int exitCode, string expectedOutput)
  {
    var directory = Directory.CreateTempSubdirectory("zero-linux-runtime-");
    try
    {
      var input = Path.Combine(directory.FullName, "program.zasm");
      File.WriteAllText(input, source);
      var compiler = new Zero();
      Assert.AreEqual(ErrorCode.None, compiler.Compile(input, null, SourceType.Assembly));
      Assert.IsNotNull(compiler.OutputPath);
      var start = new ProcessStartInfo(compiler.OutputPath)
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      };
      using var process = Process.Start(start);
      Assert.IsNotNull(process);
      if (!process.WaitForExit(10000))
      {
        process.Kill();
        process.WaitForExit();
        Assert.Fail("Linux sample did not exit within 10 seconds.");
      }

      Assert.AreEqual(exitCode, process.ExitCode, process.StandardError.ReadToEnd());
      Assert.AreEqual(expectedOutput, process.StandardOutput.ReadToEnd());
    }
    finally
    {
      directory.Delete(recursive: true);
    }
  }
}
